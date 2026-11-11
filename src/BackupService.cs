#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace McPanel
{
    internal sealed class BackupEntry
    {
        public string FilePath;
        public string FileName;
        public string Kind = "";
        public DateTime When;
        public long Size;

        public string Label
        {
            get
            {
                if (Kind == "players") return "Solo jugadores";
                if (Kind == "worldfull") return "Mundo completo";
                if (Kind == "preversion") return "Antes de cambiar version";
                return "Mundo + jugadores";
            }
        }
    }

    internal sealed class BackupResult
    {
        public bool Ok;
        public string Error = "";
        public string FilePath = "";
        public string Kind = "";
        public long Size;
        public int Files;
        public long SkippedRegionFiles;
    }

    internal sealed class BackupService
    {
        public string GetBackupDir(ServerPaths paths)
        {
            return ServerProjects.BackupDirFor(paths.ServerDir);
        }

        public BackupResult CreateBackup(ServerPaths paths, string mode, bool recentOnly,
            bool pauseSaves, ConsoleBridge bridge, Action<double, string> progress, bool preVersion)
        {
            BackupResult result = new BackupResult();
            string tmp = "";
            bool savePaused = false;

            try
            {
                string levelName = ReadLevelName(paths);
                string worldDir = Path.Combine(paths.ServerDir, levelName);
                if (!Directory.Exists(worldDir))
                {
                    result.Error = "No se encontro la carpeta del mundo: " + worldDir;
                    return result;
                }

                if (mode != "players") mode = "world";
                result.Kind = preVersion ? "preversion" : (mode == "players" ? "players" : (recentOnly ? "world" : "worldfull"));

                if (progress != null) progress(0.0, "Preparando archivos...");

                if (pauseSaves && bridge != null && bridge.IsRunning)
                {
                    bridge.Send("save-off");
                    savePaused = true;
                    if (progress != null) progress(0.02, "Guardando el mundo en disco...");
                    bool saved = bridge.SendAndWait("save-all flush", "Saved the game", 25000);
                    if (!saved)
                    {
                        result.Error = "El servidor no confirmo 'save-all flush'. Se cancela la copia para no guardar datos inconsistentes.";
                        return result;
                    }
                }

                List<string> files = new List<string>();
                List<string> entries = new List<string>();
                long totalBytes = 0;
                long skipped = 0;
                DateTime cutoff = DateTime.UtcNow.AddHours(-24);

                foreach (string file in Directory.EnumerateFiles(worldDir, "*", SearchOption.AllDirectories))
                {
                    string rel = MakeRelative(worldDir, file).Replace('\\', '/');
                    if (string.Equals(rel, "session.lock", StringComparison.OrdinalIgnoreCase)) continue;

                    if (mode == "players")
                    {
                        if (!IsPlayerFile(rel)) continue;
                    }
                    else if (recentOnly && IsRegionFile(rel))
                    {
                        DateTime modified;
                        try { modified = File.GetLastWriteTimeUtc(file); }
                        catch { modified = DateTime.MinValue; }
                        if (modified < cutoff)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    long len = 0;
                    try { len = new FileInfo(file).Length; } catch { }
                    files.Add(file);
                    entries.Add(rel);
                    totalBytes += len;
                }

                result.SkippedRegionFiles = skipped;
                if (files.Count == 0)
                {
                    result.Error = mode == "players"
                        ? "Todavia no hay datos de jugadores para copiar."
                        : "La carpeta del mundo esta vacia.";
                    return result;
                }

                ServerProjects.MigrateLegacyBackups(paths.ServerDir);
                string backupDir = GetBackupDir(paths);
                Directory.CreateDirectory(backupDir);
                string prefix = preVersion ? "backup-preversion-"
                    : (mode == "players" ? "backup-players-"
                    : (recentOnly ? "backup-world-" : "backup-worldfull-"));
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
                string finalPath = Path.Combine(backupDir, prefix + stamp + ".zip");
                int suffix = 1;
                while (File.Exists(finalPath))
                {
                    finalPath = Path.Combine(backupDir, prefix + stamp + "_" + suffix.ToString() + ".zip");
                    suffix++;
                }
                tmp = finalPath + ".tmp";
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 131072))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    WriteInfoEntry(zip, levelName, mode, recentOnly, preVersion, files.Count, skipped);

                    long written = 0;
                    long lastReported = 0;
                    DateTime lastReportAt = DateTime.UtcNow;
                    byte[] buffer = new byte[131072];

                    for (int i = 0; i < files.Count; i++)
                    {
                        string entryName = levelName + "/" + entries[i];
                        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                        entry.LastWriteTime = ClampZipTime(File.GetLastWriteTime(files[i]));

                        using (FileStream src = new FileStream(files[i], FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete, 131072))
                        using (Stream dst = entry.Open())
                        {
                            int read;
                            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                dst.Write(buffer, 0, read);
                                written += read;
                            }
                        }

                        if (progress != null && totalBytes > 0)
                        {
                            double frac = Math.Min(1.0, (double)written / totalBytes);
                            if (written - lastReported >= 2 * 1024 * 1024 ||
                                (DateTime.UtcNow - lastReportAt).TotalMilliseconds >= 250 ||
                                i == files.Count - 1)
                            {
                                lastReported = written;
                                lastReportAt = DateTime.UtcNow;
                                progress(frac, "Comprimiendo " + Utils.HumanBytes(written) +
                                    " de " + Utils.HumanBytes(totalBytes));
                            }
                        }
                    }
                }

                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tmp, finalPath);

                result.Ok = true;
                result.FilePath = finalPath;
                try { result.Size = new FileInfo(finalPath).Length; } catch { }
                result.Files = files.Count;
                Cleanup(paths, AppConfig.Instance.BackupRetention <= 0 ? 5 : AppConfig.Instance.BackupRetention);
                if (progress != null) progress(1.0, "Copia completada: " + Utils.HumanBytes(result.Size));
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                try { if (!string.IsNullOrEmpty(tmp) && File.Exists(tmp)) File.Delete(tmp); } catch { }
                return result;
            }
            finally
            {
                if (savePaused && bridge != null && bridge.IsRunning) bridge.Send("save-on");
            }
        }

        public List<BackupEntry> ListBackups(ServerPaths paths)
        {
            List<BackupEntry> list = new List<BackupEntry>();
            ServerProjects.MigrateLegacyBackups(paths.ServerDir);
            try
            {
                string dir = GetBackupDir(paths);
                if (!Directory.Exists(dir)) return list;
                foreach (string file in Directory.GetFiles(dir, "backup-*.zip"))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        BackupEntry entry = new BackupEntry();
                        entry.FilePath = file;
                        entry.FileName = fi.Name;
                        entry.When = fi.LastWriteTime;
                        entry.Size = fi.Length;
                        if (fi.Name.StartsWith("backup-players-", StringComparison.OrdinalIgnoreCase)) entry.Kind = "players";
                        else if (fi.Name.StartsWith("backup-worldfull-", StringComparison.OrdinalIgnoreCase)) entry.Kind = "worldfull";
                        else if (fi.Name.StartsWith("backup-preversion-", StringComparison.OrdinalIgnoreCase)) entry.Kind = "preversion";
                        else entry.Kind = "world";
                        list.Add(entry);
                    }
                    catch { }
                }
            }
            catch { }
            list.Sort(delegate(BackupEntry a, BackupEntry b) { return b.When.CompareTo(a.When); });
            return list;
        }

        public void Cleanup(ServerPaths paths, int keep)
        {
            try
            {
                if (keep < 1) keep = 1;
                string dir = GetBackupDir(paths);
                if (!Directory.Exists(dir)) return;
                List<FileInfo> files = new List<FileInfo>();
                foreach (string file in Directory.GetFiles(dir, "backup-*.zip"))
                {
                    try { files.Add(new FileInfo(file)); } catch { }
                }
                files.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                for (int i = keep; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        private static void WriteInfoEntry(ZipArchive zip, string levelName, string mode,
            bool recentOnly, bool preVersion, int files, long skipped)
        {
            try
            {
                ZipArchiveEntry entry = zip.CreateEntry("mcpanel-backup.txt", CompressionLevel.NoCompression);
                using (StreamWriter w = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                {
                    w.WriteLine("Orthanc Mc - copia de seguridad");
                    w.WriteLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    w.WriteLine("Carpeta del mundo: " + levelName);
                    if (preVersion)
                        w.WriteLine("Tipo: copia completa antes de cambiar server.jar");
                    else if (mode == "players")
                        w.WriteLine("Tipo: solo jugadores (playerdata, stats, advancements)");
                    else if (recentOnly)
                        w.WriteLine("Tipo: mundo + jugadores (regiones modificadas en las ultimas 24 h)");
                    else
                        w.WriteLine("Tipo: mundo completo + jugadores");
                    w.WriteLine("Archivos incluidos: " + files.ToString());
                    w.WriteLine("Regiones omitidas por antiguedad: " + skipped.ToString());
                    w.WriteLine();
                    w.WriteLine("Para restaurar, extrae el contenido en la carpeta del servidor.");
                }
            }
            catch { }
        }

        private static string ReadLevelName(ServerPaths paths)
        {
            string level = "world";
            try
            {
                PropertiesFile props = new PropertiesFile(paths.Properties);
                string value = props.Get("level-name", "world");
                if (!string.IsNullOrWhiteSpace(value)) level = value.Trim();
            }
            catch { }
            level = Path.GetFileName(level);
            if (string.IsNullOrWhiteSpace(level)) level = "world";
            return level;
        }

        private static bool IsPlayerFile(string rel)
        {
            return rel.StartsWith("playerdata/", StringComparison.OrdinalIgnoreCase) ||
                   rel.StartsWith("stats/", StringComparison.OrdinalIgnoreCase) ||
                   rel.StartsWith("advancements/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRegionFile(string rel)
        {
            return rel.EndsWith(".mca", StringComparison.OrdinalIgnoreCase) ||
                   rel.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeRelative(string baseDir, string file)
        {
            if (!baseDir.EndsWith("\\", StringComparison.Ordinal)) baseDir += "\\";
            if (file.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                return file.Substring(baseDir.Length);
            return Path.GetFileName(file);
        }

        private static DateTime ClampZipTime(DateTime value)
        {
            DateTime min = new DateTime(1980, 1, 1);
            DateTime max = new DateTime(2107, 12, 31, 23, 59, 58);
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
