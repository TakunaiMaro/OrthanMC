#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace McPanel
{
    internal sealed class ServerPaths
    {
        public string BaseDir = @"C:\Servers\Casual";
        public string ServerDir = @"C:\Servers\Casual";
        public string Jar = @"C:\Servers\Casual\server.jar";
        public string Properties = @"C:\Servers\Casual\server.properties";
        public string RunBat = @"C:\Servers\Casual\run.bat";
        public string Eula = @"C:\Servers\Casual\eula.txt";
        public string LogsDir = @"C:\Servers\Casual\logs";
        public string LatestLog = @"C:\Servers\Casual\logs\latest.log";
        public string WorldDir = @"C:\Servers\Casual\world";
        public string OpsJson = @"C:\Servers\Casual\ops.json";
        public string WhitelistJson = @"C:\Servers\Casual\whitelist.json";
        public string BansJson = @"C:\Servers\Casual\banned-players.json";
        public string PlayitExe = @"C:\Program Files\playit_gg\bin\playit.exe";
        public string PlayitService = @"C:\Program Files\playit_gg\bin\playitd-service.exe";
        public string PlayitLogs = @"C:\ProgramData\playit_gg\logs\playitd.log";
        public string PlayitToml = @"C:\ProgramData\playit_gg\playit.toml";
        public string JavaLatest = @"C:\Program Files\Java\latest\bin\java.exe";
        public string Java25 = @"C:\Program Files\Java\jdk-25.0.2\bin\java.exe";
        public string Java21 = @"C:\Program Files\Java\jdk-21.0.10\bin\java.exe";

        // La carpeta y el ejecutable de Playit salen del setup inicial
        // (AppConfig); si el usuario no los definio se usan los defaults.
        public ServerPaths()
        {
            try
            {
                AppConfig config = AppConfig.Instance;
                if (!string.IsNullOrWhiteSpace(config.ServerJarFolder) && Directory.Exists(config.ServerJarFolder))
                {
                    SetBaseDir(config.ServerJarFolder);
                }
                if (!string.IsNullOrWhiteSpace(config.PlayitLocation) && File.Exists(config.PlayitLocation))
                {
                    PlayitExe = config.PlayitLocation;
                }
            }
            catch
            {
                // Ante cualquier problema de configuracion se mantienen las rutas por defecto.
            }
        }

        public void SetBaseDir(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            BaseDir = dir;
            ServerDir = dir;
            Jar = Path.Combine(dir, "server.jar");
            Properties = Path.Combine(dir, "server.properties");
            RunBat = Path.Combine(dir, "run.bat");
            Eula = Path.Combine(dir, "eula.txt");
            LogsDir = Path.Combine(dir, "logs");
            LatestLog = Path.Combine(dir, "logs", "latest.log");
            WorldDir = Path.Combine(dir, "world");
            OpsJson = Path.Combine(dir, "ops.json");
            WhitelistJson = Path.Combine(dir, "whitelist.json");
            BansJson = Path.Combine(dir, "banned-players.json");
        }
    }

    internal sealed class RamInfo
    {
        public long TotalBytes;
        public long FreeBytes;
        public long UsedBytes;
        public double Percent;
    }

    internal sealed class ServerStatus
    {
        public bool Running;
        public int Pid;
        public string ProcessName;
        public double CpuPercent;
        public long RamBytes;
        public DateTime LastSeen;
    }

    internal sealed class PlayerInfo
    {
        public string Name;
        public string Ip;
        public string Uuid;
        public DateTime LoggedAt;
    }

    internal sealed class PlayitStatus
    {
        public bool ServiceRunning;
        public bool HasTunnels;
        public string AgentId;
        public int ClientCount;
        public string LastLine;
        public DateTime LastUpdate;
        public List<string> Recent;
    }

    internal sealed class SystemInfo
    {
        public RamInfo Ram = new RamInfo();
        public List<string> JavaPaths = new List<string>();
    }

    internal static class Utils
    {
        public static string ReadAll(string path)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            catch { }
            return "";
        }

        public static void WriteAll(string path, string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            catch { }
        }

        public static long ToMb(long b)
        {
            return b / (1024 * 1024);
        }

        public static string FormatMb(long mb)
        {
            if (mb >= 1024) return string.Format("{0:0.00} GB", mb / 1024.0);
            return mb.ToString() + " MB";
        }

        public static string HumanBytes(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return string.Format("{0:0.0} KB", b / 1024.0);
            if (b < 1024 * 1024 * 1024) return string.Format("{0:0.0} MB", b / (1024.0 * 1024.0));
            return string.Format("{0:0.00} GB", b / (1024.0 * 1024.0 * 1024.0));
        }

        public static long ParseRamMb(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            string t = s.Trim().ToUpperInvariant();
            long val = 0;
            string num = Regex.Replace(t, "[^0-9]", "");
            long.TryParse(num, out val);
            if (t.Contains("G")) return val * 1024;
            return val;
        }
    }

    internal sealed class PropertiesFile
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PropertiesFile(string path)
        {
            _path = path;
            Load();
        }

        public void Load()
        {
            _map.Clear();
            try
            {
                if (!File.Exists(_path)) return;
                string[] lines = File.ReadAllLines(_path);
                foreach (string raw in lines)
                {
                    string line = raw.TrimEnd('\r', '\n');
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (key.Length == 0) continue;
                    _map[key] = val;
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                List<string> outLines = new List<string>();
                if (File.Exists(_path))
                {
                    string[] lines = File.ReadAllLines(_path);
                    foreach (string raw in lines)
                    {
                        string line = raw.TrimEnd('\r', '\n');
                        if (string.IsNullOrEmpty(line))
                        {
                            outLines.Add(line);
                            continue;
                        }
                        if (line.StartsWith("#"))
                        {
                            outLines.Add(line);
                            continue;
                        }
                        int eq = line.IndexOf('=');
                        if (eq < 0)
                        {
                            outLines.Add(line);
                            continue;
                        }
                        string key = line.Substring(0, eq).Trim();
                        if (_map.ContainsKey(key))
                        {
                            outLines.Add(key + "=" + _map[key]);
                        }
                        else
                        {
                            outLines.Add(line);
                        }
                    }
                }
                foreach (KeyValuePair<string, string> kv in _map)
                {
                    bool found = false;
                    foreach (string ol in outLines)
                    {
                        string t = ol.TrimEnd('\r', '\n');
                        if (t.StartsWith("#")) continue;
                        int eq = t.IndexOf('=');
                        if (eq < 0) continue;
                        if (string.Equals(t.Substring(0, eq).Trim(), kv.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        outLines.Add(kv.Key + "=" + kv.Value);
                    }
                }
                File.WriteAllLines(_path, outLines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        public string Get(string key, string def)
        {
            if (string.IsNullOrEmpty(key)) return def;
            string k = key;
            foreach (KeyValuePair<string, string> kv in _map)
            {
                if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            }
            return def;
        }

        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            string realKey = key;
            foreach (KeyValuePair<string, string> kv in _map)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) { realKey = kv.Key; break; }
            }
            _map[realKey] = value ?? "";
        }

        public bool GetBool(string key, bool def)
        {
            string v = Get(key, def ? "true" : "false").ToLowerInvariant();
            return v == "true" || v == "1" || v == "yes" || v == "on";
        }

        public void SetBool(string key, bool v)
        {
            Set(key, v ? "true" : "false");
        }

        public int GetInt(string key, int def)
        {
            int r;
            if (int.TryParse(Get(key, def.ToString()), out r)) return r;
            return def;
        }

        public void SetInt(string key, int v)
        {
            Set(key, v.ToString());
        }

        public List<KeyValuePair<string, string>> All()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> kv in _map) list.Add(kv);
            return list;
        }
    }

    internal class ProcessScanner
    {
        public ServerStatus GetServerStatus()
        {
            ServerStatus st = new ServerStatus();
            try
            {
                Process[] procs = Process.GetProcesses();
                foreach (Process p in procs)
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();
                        if (name == "java" || name == "javaw")
                        {
                            try
                            {
                                string cmd = GetCommandLine(p.Id);
                                if (cmd.IndexOf("server.jar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cmd.IndexOf("minecraft_server", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    st.Running = true;
                                    st.Pid = p.Id;
                                    st.ProcessName = p.ProcessName;
                                    try { st.RamBytes = p.WorkingSet64; } catch { }
                                    st.LastSeen = DateTime.Now;
                                    return st;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    if (p != null) p.Dispose();
                }
            }
            catch { }
            return st;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int infoClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr baseAddress, byte[] buffer, int size, out int lpNumberOfBytesRead);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_1;
            public IntPtr Reserved2_2;
            public IntPtr UniqueProcessId;
            public IntPtr Reserved3;
        }

        private static string GetCommandLine(int pid)
        {
            try
            {
                IntPtr h = OpenProcess(0x0010, false, pid);
                if (h == IntPtr.Zero) return "";
                PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                int ret;
                int status = NtQueryInformationProcess(h, 0, ref pbi, System.Runtime.InteropServices.Marshal.SizeOf(pbi), out ret);
                if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero)
                {
                    CloseHandle(h);
                    return "";
                }
                // Minimal approach: use WMI alternative via CIM is heavy; fallback returns empty.
                CloseHandle(h);
                return "";
            }
            catch
            {
                return "";
            }
        }
    }

    internal class ServiceCtrl
    {
        private readonly string _serviceName;
        public ServiceCtrl(string name)
        {
            _serviceName = name;
        }

        public bool IsRunning()
        {
            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Start()
        {
            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                }
            }
            catch { }
        }

        public void Stop()
        {
            try
            {
                using (ServiceController sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running) sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
            }
            catch { }
        }

        public void Restart()
        {
            Stop();
            Thread.Sleep(1000);
            Start();
        }
    }

    internal class LogTail
    {
        public List<string> ReadLast(string path, int count)
        {
            List<string> lines = new List<string>();
            try
            {
                if (!File.Exists(path)) return lines;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    List<string> all = new List<string>();
                    string s;
                    while ((s = sr.ReadLine()) != null) all.Add(s);
                    int start = all.Count - count;
                    if (start < 0) start = 0;
                    for (int i = start; i < all.Count; i++) lines.Add(all[i]);
                }
            }
            catch { }
            return lines;
        }

        public List<PlayerInfo> GetOnlineFromLog(string latestLog)
        {
            List<PlayerInfo> list = new List<PlayerInfo>();
            try
            {
                if (!File.Exists(latestLog)) return list;
                List<string> all = new List<string>();
                using (FileStream fs = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    string s;
                    while ((s = sr.ReadLine()) != null) all.Add(s);
                }
                Dictionary<string, PlayerInfo> map = new Dictionary<string, PlayerInfo>(StringComparer.OrdinalIgnoreCase);
                Regex login = new Regex(@"([A-Za-z0-9_]+)\[\/([^\]]+)\]\s+logged in", RegexOptions.Compiled);
                Regex joined = new Regex(@"System chat:\s*([A-Za-z0-9_]+)\s+joined the game", RegexOptions.Compiled);
                Regex left = new Regex(@"System chat:\s*([A-Za-z0-9_]+)\s+left the game", RegexOptions.Compiled);
                Regex lost = new Regex(@"([A-Za-z0-9_]+)\s+lost connection", RegexOptions.Compiled);
                for (int i = 0; i < all.Count; i++)
                {
                    string l = all[i];
                    Match m = login.Match(l);
                    if (m.Success)
                    {
                        string name = m.Groups[1].Value.Trim();
                        string ip = m.Groups[2].Value.Trim();
                        if (!map.ContainsKey(name))
                        {
                            PlayerInfo pi = new PlayerInfo();
                            pi.Name = name;
                            pi.Ip = ip;
                            pi.LoggedAt = DateTime.Now;
                            map[name] = pi;
                        }
                        else
                        {
                            map[name].Ip = ip;
                        }
                        continue;
                    }
                    Match mj = joined.Match(l);
                    if (mj.Success)
                    {
                        string name = mj.Groups[1].Value.Trim();
                        if (!map.ContainsKey(name))
                        {
                            PlayerInfo pi = new PlayerInfo();
                            pi.Name = name;
                            pi.Ip = "";
                            pi.LoggedAt = DateTime.Now;
                            map[name] = pi;
                        }
                        continue;
                    }
                    Match ml = left.Match(l);
                    if (ml.Success)
                    {
                        string name = ml.Groups[1].Value.Trim();
                        if (map.ContainsKey(name)) map.Remove(name);
                        continue;
                    }
                    Match mlo = lost.Match(l);
                    if (mlo.Success)
                    {
                        string name = mlo.Groups[1].Value.Trim();
                        if (map.ContainsKey(name)) map.Remove(name);
                        continue;
                    }
                }
                foreach (KeyValuePair<string, PlayerInfo> kv in map) list.Add(kv.Value);
            }
            catch { }
            return list;
        }

        public PlayitStatus ParsePlayit(string playitLog)
        {
            PlayitStatus st = new PlayitStatus();
            st.Recent = new List<string>();
            try
            {
                if (!File.Exists(playitLog)) return st;
                using (FileStream fs = new FileStream(playitLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    List<string> all = new List<string>();
                    string s;
                    while ((s = sr.ReadLine()) != null) all.Add(s);
                    int start = all.Count - 150;
                    if (start < 0) start = 0;
                    for (int i = start; i < all.Count; i++)
                    {
                        string line = all[i];
                        st.Recent.Add(line);
                        if (line.IndexOf("New TCP Client", StringComparison.OrdinalIgnoreCase) >= 0) st.ClientCount++;
                        if (line.IndexOf("playit connected", StringComparison.OrdinalIgnoreCase) >= 0) st.HasTunnels = true;
                        if (line.IndexOf("tunnel_count", StringComparison.OrdinalIgnoreCase) >= 0) st.HasTunnels = true;
                        if (line.IndexOf("agent_id=", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Regex ar = new Regex(@"agent_id=([a-f0-9\-]+)", RegexOptions.Compiled);
                            Match am = ar.Match(line);
                            if (am.Success) st.AgentId = am.Groups[1].Value;
                        }
                        st.LastLine = line;
                    }
                    st.LastUpdate = DateTime.Now;
                }
            }
            catch { }
            return st;
        }
    }

    internal class RamMonitor
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public RamInfo Get()
        {
            RamInfo r = new RamInfo();
            try
            {
                MEMORYSTATUSEX ms = new MEMORYSTATUSEX();
                ms.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(ms);
                if (GlobalMemoryStatusEx(ref ms))
                {
                    r.TotalBytes = (long)ms.ullTotalPhys;
                    r.FreeBytes = (long)ms.ullAvailPhys;
                    r.UsedBytes = r.TotalBytes - r.FreeBytes;
                    r.Percent = r.TotalBytes == 0 ? 0 : (r.UsedBytes * 100.0) / r.TotalBytes;
                }
            }
            catch { }
            return r;
        }
    }

    // Arranque de instalaciones Forge/NeoForge: cuando existe un win_args.txt
    // generado por el instalador, el servidor no se inicia con -jar server.jar.
    internal static class ModdedLaunch
    {
        public static string DetectMinecraftVersion(ServerPaths p)
        {
            string version;
            string args = FindArgsFile(p, "neoforged", "neoforge",
                delegate(string name) { return JarCatalog.NeoForgeMcVersion(name); }, out version);
            if (!string.IsNullOrWhiteSpace(args) && !string.IsNullOrWhiteSpace(version))
                return version;

            args = FindArgsFile(p, "minecraftforge", "forge",
                delegate(string name) { return JarCatalog.ForgeMcVersion(name); }, out version);
            if (!string.IsNullOrWhiteSpace(args) && !string.IsNullOrWhiteSpace(version))
                return version;
            return null;
        }

        public static string BuildArguments(ServerPaths p, int ramMb)
        {
            string version;
            string args = FindArgsFile(p, "neoforged", "neoforge",
                delegate(string name) { return JarCatalog.NeoForgeMcVersion(name); }, out version);
            if (string.IsNullOrWhiteSpace(args))
            {
                args = FindArgsFile(p, "minecraftforge", "forge",
                    delegate(string name) { return JarCatalog.ForgeMcVersion(name); }, out version);
            }
            if (string.IsNullOrWhiteSpace(args)) return null;
            return string.Format("-Xmx{0}M -Xms{0}M @\"{1}\" nogui", ramMb, args);
        }

        private static string FindArgsFile(ServerPaths p, string group, string artifact,
            Func<string, string> versionMap, out string minecraftVersion)
        {
            minecraftVersion = null;
            try
            {
                string root = Path.Combine(p.ServerDir, "libraries", "net", group, artifact);
                if (!Directory.Exists(root)) return null;

                string bestArgs = null;
                string bestName = null;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    string args = Path.Combine(dir, "win_args.txt");
                    if (!File.Exists(args)) continue;
                    if (bestArgs == null || JarCatalog.CompareDotted(name, bestName) > 0)
                    {
                        bestArgs = args;
                        bestName = name;
                    }
                }
                if (bestArgs == null) return null;
                minecraftVersion = versionMap != null ? versionMap(bestName) : null;
                return bestArgs;
            }
            catch
            {
                return null;
            }
        }
    }

    internal class ConsoleBridge
    {
        private Process _proc;
        private readonly object _lock = new object();
        private readonly object _waitLock = new object();
        private bool _running;
        private string _waitNeedle;
        private TaskCompletionSource<bool> _waitTcs;

        public event Action<string> Output;

        public bool IsRunning
        {
            get { lock (_lock) { return _running && _proc != null && !_proc.HasExited; } }
        }

        public bool Start(ServerPaths p, int ramMb)
        {
            Stop();
            try
            {
                string java = null;
                int required = 0;
                try { required = JarInspector.Inspect(p.Jar).JavaMajor; } catch { }

                // Forge/NeoForge se arrancan por win_args.txt y su server.jar
                // es el instalador, que no declara java_version. La version de
                // Minecraft sale del nombre de la carpeta de librerias.
                if (required <= 0)
                {
                    string launchVersion = ModdedLaunch.DetectMinecraftVersion(p);
                    if (!string.IsNullOrWhiteSpace(launchVersion))
                        required = JarCatalog.JavaForMinecraft(launchVersion);
                }
                if (required <= 0)
                {
                    try
                    {
                        AppConfig cfg = AppConfig.Instance;
                        FileInfo info = new FileInfo(p.Jar);
                        if (info.Exists && cfg.InstalledJarSize > 0 &&
                            cfg.InstalledJarSize == info.Length &&
                            cfg.InstalledJarWriteTicks == info.LastWriteTimeUtc.Ticks)
                            required = JarCatalog.JavaForMinecraft(cfg.InstalledJarVersion);
                    }
                    catch { }
                }

                if (required > 0)
                {
                    java = JavaRuntimeManager.Find(required);
                    if (string.IsNullOrWhiteSpace(java))
                    {
                        if (Output != null)
                        {
                            Output("[PANEL] Este server.jar necesita Java " + required +
                                ", pero no hay una instalacion compatible.");
                            Output("[PANEL] Abre 'Versiones del Servidor' para descargar el JDK.");
                        }
                        lock (_lock) { _running = false; }
                        return false;
                    }
                }
                else
                {
                    java = File.Exists(p.Java25) ? p.Java25 : (File.Exists(p.Java21) ? p.Java21 : (File.Exists(p.JavaLatest) ? p.JavaLatest : "java"));
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = java;
                string launchArgs = ModdedLaunch.BuildArguments(p, ramMb);
                psi.Arguments = !string.IsNullOrWhiteSpace(launchArgs)
                    ? launchArgs
                    : string.Format("-Xmx{0}M -Xms{0}M -jar \"{1}\" nogui", ramMb, p.Jar);
                psi.WorkingDirectory = p.ServerDir;
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                // Sin BOM: el primer comando (save-off) llegaria con bytes
                // invisibles y el servidor lo rechazaria como desconocido.
                psi.StandardInputEncoding = new UTF8Encoding(false);
                _proc = new Process();
                _proc.StartInfo = psi;
                _proc.EnableRaisingEvents = true;
                _proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { EmitLine("[OUT] ", e.Data); };
                _proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { EmitLine("[ERR] ", e.Data); };
                _proc.Exited += delegate { lock (_lock) { _running = false; } };
                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                lock (_lock) { _running = true; }
                return true;
            }
            catch (Exception ex)
            {
                if (Output != null) Output("[PANEL] Error al iniciar: " + ex.Message);
                lock (_lock) { _running = false; }
                return false;
            }
        }

        private void EmitLine(string prefix, string data)
        {
            if (data == null) return;
            if (Output != null) Output(prefix + data);
            lock (_waitLock)
            {
                if (_waitNeedle != null && _waitTcs != null &&
                    data.IndexOf(_waitNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _waitTcs.TrySetResult(true);
                }
            }
        }

        // Envia un comando y espera a que la consola del servidor muestre una
        // linea concreta. Se usa para save-all flush antes de comprimir el mundo.
        public bool SendAndWait(string cmd, string expected, int timeoutMs)
        {
            if (!IsRunning) return false;
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_waitLock)
            {
                _waitNeedle = expected;
                _waitTcs = tcs;
            }
            try
            {
                Send(cmd);
                return tcs.Task.Wait(Math.Max(500, timeoutMs));
            }
            catch
            {
                return false;
            }
            finally
            {
                lock (_waitLock)
                {
                    _waitNeedle = null;
                    _waitTcs = null;
                }
            }
        }

        public void Send(string cmd)
        {
            try
            {
                if (!IsRunning || _proc == null) return;
                _proc.StandardInput.WriteLine(cmd);
                _proc.StandardInput.Flush();
            }
            catch { }
        }

        public void Stop()
        {
            // Guardar un mundo grande puede tardar. Es preferible esperar a
            // matar java a los 4 segundos y arriesgar el mundo.
            StopGracefully(60000);
        }

        // Detiene el servidor esperando el guardado real. El cambio de version
        // usa un timeout amplio para no matar java mientras escribe el mundo.
        public bool StopGracefully(int timeoutMs)
        {
            Process proc;
            lock (_lock) { proc = _proc; }
            bool exited = true;
            try
            {
                if (proc != null && !proc.HasExited)
                {
                    try { Send("stop"); } catch { }
                    try { exited = proc.WaitForExit(Math.Max(2000, timeoutMs)); } catch { exited = false; }
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { }
                        try { proc.WaitForExit(5000); } catch { }
                    }
                }
            }
            catch { }
            try
            {
                if (proc != null)
                {
                    proc.Dispose();
                    lock (_lock) { if (ReferenceEquals(_proc, proc)) _proc = null; }
                }
            }
            catch { }
            lock (_lock) { _running = false; }
            return exited;
        }
    }
}
