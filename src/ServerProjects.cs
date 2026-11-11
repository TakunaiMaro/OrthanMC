using System;
using System.Collections.Generic;
using System.IO;

namespace McPanel
{
    internal sealed class ServerEntry
    {
        public string Name = "";
        public string Path = "";
        public bool HasJar;
        public bool IsActive;
        public bool IsExternal;
    }

    // Un solo lugar decide donde viven los proyectos y cual esta activo.
    // Setup y el selector de servidores usan exactamente las mismas reglas.
    internal static class ServerProjects
    {
        public const string DefaultRoot = @"C:\ServersMC";

        public static string Root
        {
            get
            {
                try
                {
                    string configured = AppConfig.Instance.RootServerFolder;
                    if (!string.IsNullOrWhiteSpace(configured)) return configured;
                }
                catch { }
                return DefaultRoot;
            }
        }

        public static string ActivePath
        {
            get
            {
                try
                {
                    string folder = AppConfig.Instance.ServerJarFolder;
                    if (!string.IsNullOrWhiteSpace(folder)) return folder;
                }
                catch { }
                return "";
            }
        }

        public static string ActiveName
        {
            get
            {
                try
                {
                    string name = AppConfig.Instance.ActiveServer;
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
                catch { }
                string path = ActivePath;
                if (string.IsNullOrWhiteSpace(path)) return "Sin servidor";
                return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        public static void EnsureRoot()
        {
            try { Directory.CreateDirectory(Root); } catch { }
        }

        public static List<ServerEntry> List()
        {
            List<ServerEntry> list = new List<ServerEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string active = Normalize(ActivePath);

            try
            {
                string root = Root;
                if (Directory.Exists(root))
                {
                    List<string> dirs = new List<string>(Directory.GetDirectories(root));
                    dirs.Sort(StringComparer.OrdinalIgnoreCase);
                    string backups = Normalize(BackupsRoot);
                    foreach (string dir in dirs)
                    {
                        string full = Normalize(dir);
                        // La carpeta de copias de seguridad vive en la raiz
                        // pero no es un servidor: nunca debe aparecer como perfil.
                        if (string.Equals(full, backups, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!seen.Add(full)) continue;
                        ServerEntry entry = new ServerEntry();
                        entry.Name = Path.GetFileName(dir);
                        entry.Path = dir;
                        entry.HasJar = File.Exists(Path.Combine(dir, "server.jar"));
                        entry.IsActive = string.Equals(full, active, StringComparison.OrdinalIgnoreCase);
                        list.Add(entry);
                    }
                }
            }
            catch { }

            // El servidor actual puede estar fuera de RaizServer (instalacion
            // anterior). Se muestra igualmente para no perderlo al cambiar.
            if (!string.IsNullOrWhiteSpace(active) && Directory.Exists(active) && !seen.Contains(active))
            {
                ServerEntry entry = new ServerEntry();
                entry.Name = ActiveName;
                entry.Path = ActivePath;
                entry.HasJar = File.Exists(Path.Combine(ActivePath, "server.jar"));
                entry.IsActive = true;
                entry.IsExternal = true;
                list.Insert(0, entry);
            }

            return list;
        }

        public static bool SetActive(ServerEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Path)) return false;
            try
            {
                AppConfig config = AppConfig.Instance;
                config.ServerJarFolder = entry.Path;
                config.RootServerFolder = Root;
                config.ActiveServer = string.IsNullOrWhiteSpace(entry.Name)
                    ? Path.GetFileName(entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    : entry.Name;
                config.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool SamePath(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        // Las copias viven en la raiz (C:\ServersMC\Backups\<servidor>), nunca
        // dentro de la carpeta del servidor, para que no se muevan ni se
        // borren junto con el.
        public static string BackupsRoot
        {
            get { return Path.Combine(Root, "Backups"); }
        }

        public static string BackupDirFor(string serverPath)
        {
            string name = "";
            try
            {
                name = Path.GetFileName((serverPath ?? "").TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch { }
            if (string.IsNullOrWhiteSpace(name)) name = "Servidor";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return Path.Combine(BackupsRoot, name);
        }

        public static string JarsBackupDirFor(string serverPath)
        {
            return Path.Combine(BackupDirFor(serverPath), "jars");
        }

        // Mueve las copias antiguas que quedaron dentro de la carpeta del
        // servidor a la nueva ubicacion. Se llama antes de listar o crear.
        public static void MigrateLegacyBackups(string serverPath)
        {
            try
            {
                string legacy = Path.Combine(serverPath, "backups");
                string target = BackupDirFor(serverPath);
                if (SamePath(legacy, target)) return;
                if (!Directory.Exists(legacy)) return;
                Directory.CreateDirectory(target);

                foreach (string file in Directory.GetFiles(legacy, "backup-*.zip"))
                {
                    string dest = UniquePath(Path.Combine(target, Path.GetFileName(file)));
                    try { File.Move(file, dest); } catch { }
                }

                string legacyJars = Path.Combine(legacy, "jars");
                if (Directory.Exists(legacyJars))
                {
                    string targetJars = Path.Combine(target, "jars");
                    Directory.CreateDirectory(targetJars);
                    foreach (string file in Directory.GetFiles(legacyJars, "*.jar"))
                    {
                        string dest = UniquePath(Path.Combine(targetJars, Path.GetFileName(file)));
                        try { File.Move(file, dest); } catch { }
                    }
                    try { if (Directory.GetFileSystemEntries(legacyJars).Length == 0) Directory.Delete(legacyJars); } catch { }
                }

                try { if (Directory.GetFileSystemEntries(legacy).Length == 0) Directory.Delete(legacy); } catch { }
            }
            catch { }
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i.ToString() + ext);
                if (!File.Exists(candidate)) return candidate;
            }
            return path;
        }

        private static string Normalize(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return (path ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}
