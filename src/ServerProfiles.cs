#nullable disable
using System;
using System.Collections.Generic;
using System.IO;

namespace McPanel
{
    // Metadatos que el panel guarda por cada servidor. Viven en config.json
    // para que las tarjetas de "Perfiles" conserven horas, screenshot y
    // decisiones del usuario entre ejecuciones.
    public sealed class ProfileMeta
    {
        public string ScreenshotPath { get; set; } = "";
        public bool AskScreenshot { get; set; } = true;
        public DateTime? LastOpenedUtc { get; set; }
        public double TotalSeconds { get; set; }

        // Cache de la ultima exploracion, para pintar la tarjeta al instante.
        public long CachedSize { get; set; }
        public string CachedVersion { get; set; } = "";
        public string CachedProvider { get; set; } = "";
        public string CachedType { get; set; } = "";
        public int CachedAddonCount { get; set; }
        public bool HasScanCache { get; set; }
    }

    internal sealed class ProfileScan
    {
        public string Version = "";
        public string Provider = "";
        public AddonKind Kind;
        public int AddonCount;
        public long SizeBytes;
        public bool FromCache;
    }

    internal static class ProfileStore
    {
        public static string ProfilesDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "McPanel", "profiles");
            }
        }

        public static string Key(string path)
        {
            try
            {
                return Path.GetFullPath(path ?? "")
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToLowerInvariant();
            }
            catch
            {
                return (path ?? "").TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
            }
        }

        public static ProfileMeta Get(string path)
        {
            try
            {
                AppConfig config = AppConfig.Instance;
                if (config.Profiles == null)
                    config.Profiles = new Dictionary<string, ProfileMeta>();
                string key = Key(path);
                ProfileMeta meta;
                if (!config.Profiles.TryGetValue(key, out meta) || meta == null)
                {
                    meta = new ProfileMeta();
                    config.Profiles[key] = meta;
                }
                return meta;
            }
            catch
            {
                return new ProfileMeta();
            }
        }

        public static void Save()
        {
            try { AppConfig.Instance.Save(); } catch { }
        }

        public static void AddSeconds(string path, double seconds)
        {
            if (string.IsNullOrWhiteSpace(path) || seconds <= 0) return;
            ProfileMeta meta = Get(path);
            meta.TotalSeconds += seconds;
            if (meta.TotalSeconds < 0) meta.TotalSeconds = 0;
        }

        public static void TouchOpened(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            Get(path).LastOpenedUtc = DateTime.UtcNow;
        }

        // Copia la screenshot elegida a una carpeta propia del panel. Asi la
        // tarjeta sigue funcionando aunque el usuario limpie .minecraft.
        public static string ImportScreenshot(string serverPath, string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return source ?? "";
                string dir = ProfilesDir;
                Directory.CreateDirectory(dir);
                string name = SafeName(Path.GetFileName(
                    (serverPath ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                if (string.IsNullOrWhiteSpace(name)) name = "server";
                string ext = Path.GetExtension(source);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                string dest = Path.Combine(dir, name + "-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ext.ToLowerInvariant());
                File.Copy(source, dest, true);
                return dest;
            }
            catch
            {
                return source ?? "";
            }
        }

        public static ProfileScan Scan(string serverPath)
        {
            ProfileScan scan = new ProfileScan();
            try
            {
                string jar = Path.Combine(serverPath, "server.jar");
                JarMetadata jarMeta = new JarMetadata();
                if (File.Exists(jar))
                {
                    try { jarMeta = JarInspector.Inspect(jar); } catch { }
                }

                scan.Provider = (jarMeta.Provider ?? "").Trim();
                scan.Version = (jarMeta.Id ?? "").Trim();
                scan.Kind = DetectKind(serverPath, scan.Provider);
                scan.AddonCount = CountAddons(serverPath, scan.Kind);

                if (string.IsNullOrWhiteSpace(scan.Provider))
                    scan.Provider = TypeLabel(scan.Kind);
                if (string.IsNullOrWhiteSpace(scan.Version))
                    scan.Version = "desconocida";

                scan.SizeBytes = DirectorySize(serverPath);
            }
            catch
            {
                if (string.IsNullOrWhiteSpace(scan.Provider)) scan.Provider = "Vanilla";
                if (string.IsNullOrWhiteSpace(scan.Version)) scan.Version = "desconocida";
            }
            return scan;
        }

        public static void ApplyScan(string path, ProfileScan scan)
        {
            if (scan == null) return;
            ProfileMeta meta = Get(path);
            meta.CachedSize = scan.SizeBytes;
            meta.CachedVersion = scan.Version ?? "";
            meta.CachedProvider = scan.Provider ?? "";
            meta.CachedType = TypeLabel(scan.Kind);
            meta.CachedAddonCount = scan.AddonCount;
            meta.HasScanCache = true;
        }

        public static ProfileScan CachedScan(string path)
        {
            ProfileMeta meta = Get(path);
            ProfileScan scan = new ProfileScan();
            scan.FromCache = meta.HasScanCache;
            scan.SizeBytes = meta.CachedSize;
            scan.Version = meta.CachedVersion ?? "";
            scan.Provider = meta.CachedProvider ?? "";
            scan.AddonCount = meta.CachedAddonCount;
            scan.Kind = KindFromLabel(meta.CachedType);
            if (string.IsNullOrWhiteSpace(scan.Provider)) scan.Provider = TypeLabel(scan.Kind);
            if (string.IsNullOrWhiteSpace(scan.Version)) scan.Version = "desconocida";
            return scan;
        }

        public static string TypeLabel(AddonKind kind)
        {
            if (kind == AddonKind.Mods) return "Modded";
            if (kind == AddonKind.Plugins) return "Plugins";
            return "Vanilla";
        }

        private static AddonKind KindFromLabel(string label)
        {
            if (string.Equals(label, "Modded", StringComparison.OrdinalIgnoreCase)) return AddonKind.Mods;
            if (string.Equals(label, "Plugins", StringComparison.OrdinalIgnoreCase)) return AddonKind.Plugins;
            return AddonKind.None;
        }

        // Misma logica que usa el HUD, pero tomando la carpeta como parametro.
        private static AddonKind DetectKind(string dir, string provider)
        {
            AddonKind kind = AddonKindFor(provider);
            if (kind != AddonKind.None) return kind;

            try
            {
                string libs = Path.Combine(dir, "libraries", "net");
                if (Directory.Exists(Path.Combine(libs, "neoforged", "neoforge")) ||
                    Directory.Exists(Path.Combine(libs, "minecraftforge", "forge")))
                    return AddonKind.Mods;
            }
            catch { }

            try
            {
                bool hasPlugins = HasAddonFiles(Path.Combine(dir, "plugins"), ".jar", ".offplugin");
                bool hasMods = HasAddonFiles(Path.Combine(dir, "mods"), ".jar", ".offmod");
                if (hasPlugins && !hasMods) return AddonKind.Plugins;
                if (hasMods && !hasPlugins) return AddonKind.Mods;
                if (hasPlugins) return AddonKind.Plugins;
                if (hasMods) return AddonKind.Mods;
            }
            catch { }

            return AddonKind.None;
        }

        private static AddonKind AddonKindFor(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return AddonKind.None;
            string text = provider.ToLowerInvariant();
            if (text.Contains("paper") || text.Contains("purpur") ||
                text.Contains("spigot") || text.Contains("bukkit") ||
                text.Contains("folia"))
                return AddonKind.Plugins;
            if (text.Contains("fabric") || text.Contains("forge") ||
                text.Contains("neoforge") || text.Contains("quilt"))
                return AddonKind.Mods;
            return AddonKind.None;
        }

        private static bool HasAddonFiles(string dir, string enabledExt, string disabledExt)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                foreach (string file in Directory.GetFiles(dir))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(enabledExt, StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(disabledExt, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static int CountAddons(string dir, AddonKind kind)
        {
            if (kind == AddonKind.None) return 0;
            string folder = Path.Combine(dir, kind == AddonKind.Mods ? "mods" : "plugins");
            string disabled = kind == AddonKind.Mods ? ".offmod" : ".offplugin";
            int count = 0;
            try
            {
                if (!Directory.Exists(folder)) return 0;
                foreach (string file in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(disabled, StringComparison.OrdinalIgnoreCase))
                        count++;
                }
            }
            catch { }
            return count;
        }

        private static long DirectorySize(string root)
        {
            long total = 0;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return 0;
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(dir))
                    {
                        try { total += new FileInfo(file).Length; } catch { }
                    }
                    foreach (string sub in Directory.EnumerateDirectories(dir))
                        pending.Push(sub);
                }
                catch { }
            }
            return total;
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "server";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
