#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace McPanel
{
    internal sealed class JarOption
    {
        public string ProviderId = "";
        public string ProviderName = "";
        public string Channel = "vanilla"; // vanilla | snapshot | plugin | mod
        public string MinecraftVersion = "";
        public string Build = "";
        public string MetaUrl = "";
        // "jar" descarga un servidor ejecutable; "installer" descarga el
        // instalador oficial de Forge/NeoForge y hay que ejecutarlo.
        public string Kind = "jar";

        public string Label
        {
            get
            {
                string text = ProviderName + " " + MinecraftVersion;
                if (!string.IsNullOrEmpty(Build)) text += " (build " + Build + ")";
                return text;
            }
        }
    }

    internal sealed class JarDownload
    {
        public JarOption Option;
        public string Url = "";
        public string FileName = "";
        public long Size;
        public string Hash = "";
        public string HashAlgorithm = "SHA256";
        public int JavaMajor;
    }

    internal sealed class JarMetadata
    {
        public string Id = "";
        public string Name = "";
        public string Provider = "";
        public int JavaMajor;
        public bool Stable;
        public bool HasVersionJson;
    }

    // Cada proveedor se registra con su canal. Para agregar mods mas adelante
    // basta con crear descriptores con Channel="mod" y su Resolve/List.
    internal sealed class JarProvider
    {
        public string Id = "";
        public string Name = "";
        public string Channel = "";
        public Func<Task<List<JarOption>>> List;
        public Func<JarOption, Task<JarDownload>> Resolve;
    }

    internal static class JarCatalog
    {
        private const string MojangManifest = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        private const string PaperBase = "https://fill.papermc.io/v3/projects/paper";
        private const string FoliaBase = "https://fill.papermc.io/v3/projects/folia";
        private const string PurpurBase = "https://api.purpurmc.org/v2/purpur";
        private const string ForgeBase = "https://maven.minecraftforge.net/net/minecraftforge/forge";
        private const string ForgeMeta = ForgeBase + "/maven-metadata.xml";
        private const string NeoForgeBase = "https://maven.neoforged.net/releases/net/neoforged/neoforge";
        private const string NeoForgeMeta = NeoForgeBase + "/maven-metadata.xml";
        private const string FabricBase = "https://meta.fabricmc.net/v2/versions";
        private const string FabricGame = FabricBase + "/game";
        private const string FabricLoader = FabricBase + "/loader";
        private const string FabricInstaller = FabricBase + "/installer";

        private static readonly HttpClient Api = CreateClient();

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(35);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "McPanel/1.0 (server manager)");
            return client;
        }

        public static JarProvider GetVanillaProvider(bool snapshots)
        {
            JarProvider p = new JarProvider();
            p.Id = snapshots ? "snapshot" : "vanilla";
            p.Name = snapshots ? "Snapshot" : "Vanilla";
            p.Channel = snapshots ? "snapshot" : "vanilla";
            p.List = delegate { return ListMojangAsync(snapshots); };
            p.Resolve = ResolveMojangAsync;
            return p;
        }

        public static List<JarProvider> GetPluginProviders()
        {
            List<JarProvider> list = new List<JarProvider>();

            JarProvider paper = new JarProvider();
            paper.Id = "paper";
            paper.Name = "Paper";
            paper.Channel = "plugin";
            paper.List = ListPaperAsync;
            paper.Resolve = ResolvePaperAsync;
            list.Add(paper);

            JarProvider folia = new JarProvider();
            folia.Id = "folia";
            folia.Name = "Folia";
            folia.Channel = "plugin";
            folia.List = ListFoliaAsync;
            folia.Resolve = ResolveFoliaAsync;
            list.Add(folia);

            JarProvider purpur = new JarProvider();
            purpur.Id = "purpur";
            purpur.Name = "Purpur";
            purpur.Channel = "plugin";
            purpur.List = ListPurpurAsync;
            purpur.Resolve = ResolvePurpurAsync;
            list.Add(purpur);

            // Futuro: Fabric/Forge/NeoForge con Channel="mod".
            return list;
        }

        public static List<JarProvider> GetModProviders()
        {
            List<JarProvider> list = new List<JarProvider>();

            JarProvider fabric = new JarProvider();
            fabric.Id = "fabric";
            fabric.Name = "Fabric";
            fabric.Channel = "mod";
            fabric.List = ListFabricAsync;
            fabric.Resolve = ResolveFabricAsync;
            list.Add(fabric);

            JarProvider forge = new JarProvider();
            forge.Id = "forge";
            forge.Name = "Forge";
            forge.Channel = "mod";
            forge.List = ListForgeAsync;
            forge.Resolve = ResolveForgeAsync;
            list.Add(forge);

            JarProvider neo = new JarProvider();
            neo.Id = "neoforged";
            neo.Name = "NeoForge";
            neo.Channel = "mod";
            neo.List = ListNeoForgeAsync;
            neo.Resolve = ResolveNeoForgeAsync;
            list.Add(neo);

            return list;
        }

        public static async Task<List<JarOption>> ListMojangAsync(bool snapshots)
        {
            List<JarOption> list = new List<JarOption>();
            using (JsonDocument doc = await GetJsonAsync(MojangManifest, CancellationToken.None))
            {
                JsonElement versions = doc.RootElement.GetProperty("versions");
                string wanted = snapshots ? "snapshot" : "release";
                foreach (JsonElement v in versions.EnumerateArray())
                {
                    string type = GetString(v, "type");
                    if (!string.Equals(type, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    JarOption option = new JarOption();
                    option.ProviderId = snapshots ? "snapshot" : "vanilla";
                    option.ProviderName = snapshots ? "Snapshot" : "Vanilla";
                    option.Channel = snapshots ? "snapshot" : "vanilla";
                    option.MinecraftVersion = GetString(v, "id") ?? "";
                    option.MetaUrl = GetString(v, "url") ?? "";
                    list.Add(option);
                    if (list.Count >= (snapshots ? 60 : 120)) break;
                }
            }
            return list;
        }

        public static async Task<JarDownload> ResolveMojangAsync(JarOption option)
        {
            using (JsonDocument doc = await GetJsonAsync(option.MetaUrl, CancellationToken.None))
            {
                JsonElement root = doc.RootElement;
                JsonElement downloads = root.GetProperty("downloads");
                JsonElement server = downloads.GetProperty("server");

                JarDownload d = new JarDownload();
                d.Option = option;
                d.Url = GetString(server, "url") ?? "";
                d.FileName = Path.GetFileName(new Uri(d.Url).LocalPath);
                d.Size = GetLong(server, "size");
                d.Hash = GetString(server, "sha1") ?? "";
                d.HashAlgorithm = "SHA1";

                JsonElement jv;
                if (root.TryGetProperty("javaVersion", out jv) &&
                    jv.ValueKind == JsonValueKind.Object)
                {
                    JsonElement major;
                    if (jv.TryGetProperty("majorVersion", out major)) d.JavaMajor = major.GetInt32();
                }
                else
                {
                    JsonElement jver;
                    if (root.TryGetProperty("java_version", out jver) &&
                        jver.ValueKind == JsonValueKind.Number)
                        d.JavaMajor = jver.GetInt32();
                }
                return d;
            }
        }

        public static Task<List<JarOption>> ListPaperAsync()
        {
            return ListFillProjectAsync(PaperBase, "paper", "Paper");
        }

        public static Task<JarDownload> ResolvePaperAsync(JarOption option)
        {
            return ResolveFillProjectAsync(option, PaperBase, "paper");
        }

        public static Task<List<JarOption>> ListFoliaAsync()
        {
            return ListFillProjectAsync(FoliaBase, "folia", "Folia");
        }

        public static Task<JarDownload> ResolveFoliaAsync(JarOption option)
        {
            return ResolveFillProjectAsync(option, FoliaBase, "folia");
        }

        // Paper y Folia comparten la API v3 de PaperMC (fill.papermc.io):
        // el mismo formato de versiones, builds y descargas.
        private static async Task<List<JarOption>> ListFillProjectAsync(string baseUrl,
            string providerId, string providerName)
        {
            List<JarOption> list = new List<JarOption>();
            using (JsonDocument doc = await GetJsonAsync(baseUrl, CancellationToken.None))
            {
                JsonElement versions = doc.RootElement.GetProperty("versions");
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty group in versions.EnumerateObject())
                {
                    foreach (JsonElement item in group.Value.EnumerateArray())
                    {
                        string version = item.GetString();
                        if (string.IsNullOrWhiteSpace(version)) continue;
                        if (version.IndexOf("-", StringComparison.Ordinal) >= 0) continue;
                        if (!seen.Add(version)) continue;

                        JarOption option = new JarOption();
                        option.ProviderId = providerId;
                        option.ProviderName = providerName;
                        option.Channel = "plugin";
                        option.MinecraftVersion = version;
                        list.Add(option);
                    }
                }
            }
            return list;
        }

        private static async Task<JarDownload> ResolveFillProjectAsync(JarOption option,
            string baseUrl, string providerId)
        {
            string url = baseUrl + "/versions/" +
                Uri.EscapeDataString(option.MinecraftVersion) + "/builds";
            using (JsonDocument doc = await GetJsonAsync(url, CancellationToken.None))
            {
                JsonElement builds = doc.RootElement;
                JsonElement chosen = default(JsonElement);
                bool found = false;
                foreach (JsonElement build in builds.EnumerateArray())
                {
                    string channel = GetString(build, "channel");
                    if (string.Equals(channel, "STABLE", StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = build;
                        found = true;
                        break;
                    }
                    // Si no hay build estable, se usa el mas reciente (el
                    // endpoint devuelve los builds del mas nuevo al mas viejo).
                    if (!found && chosen.ValueKind == JsonValueKind.Undefined) chosen = build;
                }
                if (!found && chosen.ValueKind == JsonValueKind.Undefined)
                    throw new Exception(option.ProviderName + " no tiene builds para " +
                        option.MinecraftVersion + ".");

                JsonElement downloads = chosen.GetProperty("downloads");
                JsonElement server = default(JsonElement);
                if (downloads.TryGetProperty("server:default", out server) == false)
                {
                    foreach (JsonProperty p in downloads.EnumerateObject())
                    {
                        server = p.Value;
                        break;
                    }
                }
                if (server.ValueKind == JsonValueKind.Undefined)
                    throw new Exception(option.ProviderName + " no publico un jar de servidor para " +
                        option.MinecraftVersion + ".");

                JarDownload d = new JarDownload();
                d.Option = option;
                d.Url = GetString(server, "url") ?? "";
                d.FileName = GetString(server, "name") ?? (providerId + ".jar");
                d.Size = GetLong(server, "size");
                JsonElement checksums;
                if (server.TryGetProperty("checksums", out checksums))
                    d.Hash = GetString(checksums, "sha256") ?? "";
                d.HashAlgorithm = "SHA256";
                d.Option.Build = GetInt(chosen, "id").ToString();
                return d;
            }
        }

        public static async Task<List<JarOption>> ListPurpurAsync()
        {
            List<JarOption> list = new List<JarOption>();
            using (JsonDocument doc = await GetJsonAsync(PurpurBase, CancellationToken.None))
            {
                JsonElement versions = doc.RootElement.GetProperty("versions");
                List<string> items = new List<string>();
                foreach (JsonElement v in versions.EnumerateArray())
                {
                    string version = v.GetString();
                    if (!string.IsNullOrWhiteSpace(version)) items.Add(version);
                }
                items.Reverse();
                foreach (string version in items)
                {
                    JarOption option = new JarOption();
                    option.ProviderId = "purpur";
                    option.ProviderName = "Purpur";
                    option.Channel = "plugin";
                    option.MinecraftVersion = version;
                    list.Add(option);
                }
            }
            return list;
        }

        public static async Task<JarDownload> ResolvePurpurAsync(JarOption option)
        {
            string version = Uri.EscapeDataString(option.MinecraftVersion);
            string url = PurpurBase + "/" + version + "/latest";
            using (JsonDocument doc = await GetJsonAsync(url, CancellationToken.None))
            {
                JsonElement root = doc.RootElement;
                JarDownload d = new JarDownload();
                d.Option = option;
                d.Url = PurpurBase + "/" + version + "/latest/download";
                d.FileName = "purpur-" + option.MinecraftVersion + ".jar";
                d.Hash = GetString(root, "md5") ?? "";
                d.HashAlgorithm = "MD5";
                d.Option.Build = GetString(root, "build") ?? "";
                return d;
            }
        }

        // ------------------------------------------------------------------
        // Proveedores Modded
        // ------------------------------------------------------------------

        public static async Task<List<JarOption>> ListFabricAsync()
        {
            List<JarOption> list = new List<JarOption>();
            using (JsonDocument doc = await GetJsonAsync(FabricGame, CancellationToken.None))
            {
                foreach (JsonElement item in doc.RootElement.EnumerateArray())
                {
                    JsonElement stable;
                    if (!item.TryGetProperty("stable", out stable) ||
                        stable.ValueKind != JsonValueKind.True) continue;
                    string version = GetString(item, "version");
                    if (string.IsNullOrWhiteSpace(version)) continue;

                    JarOption option = new JarOption();
                    option.ProviderId = "fabric";
                    option.ProviderName = "Fabric";
                    option.Channel = "mod";
                    option.MinecraftVersion = version;
                    option.Kind = "jar";
                    list.Add(option);
                    if (list.Count >= 120) break;
                }
            }
            return list;
        }

        public static async Task<JarDownload> ResolveFabricAsync(JarOption option)
        {
            string loader = await LatestStableAsync(FabricLoader);
            string installer = await LatestStableAsync(FabricInstaller);
            if (string.IsNullOrWhiteSpace(loader) || string.IsNullOrWhiteSpace(installer))
                throw new Exception("Fabric no publico un loader/instalador estable.");

            string game = Uri.EscapeDataString(option.MinecraftVersion);
            JarDownload d = new JarDownload();
            d.Option = option;
            d.Url = FabricLoader + "/" + game + "/" + Uri.EscapeDataString(loader) +
                "/" + Uri.EscapeDataString(installer) + "/server/jar";
            d.FileName = "fabric-server-mc." + option.MinecraftVersion +
                "-loader." + loader + "-launcher.jar";
            d.Option.Build = loader;
            return d;
        }

        public static async Task<List<JarOption>> ListForgeAsync()
        {
            string xml = await GetTextAsync(ForgeMeta, CancellationToken.None);
            XDocument doc = XDocument.Parse(xml);
            Dictionary<string, string> latest = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();
            foreach (XElement element in doc.Descendants("version"))
            {
                string full = element.Value.Trim();
                string mc = ForgeMcVersion(full);
                string build = ForgeBuild(full);
                if (string.IsNullOrWhiteSpace(mc) || string.IsNullOrWhiteSpace(build)) continue;
                if (!latest.ContainsKey(mc)) order.Add(mc);
                string current;
                if (!latest.TryGetValue(mc, out current) || CompareDotted(build, current) > 0)
                    latest[mc] = build;
            }
            order.Sort(delegate(string a, string b) { return CompareDotted(b, a); });
            List<JarOption> list = new List<JarOption>();
            foreach (string mc in order)
            {
                JarOption option = new JarOption();
                option.ProviderId = "forge";
                option.ProviderName = "Forge";
                option.Channel = "mod";
                option.MinecraftVersion = mc;
                option.Build = latest[mc];
                option.Kind = "installer";
                list.Add(option);
            }
            return list;
        }

        public static async Task<JarDownload> ResolveForgeAsync(JarOption option)
        {
            string full = option.MinecraftVersion + "-" + option.Build;
            JarDownload d = new JarDownload();
            d.Option = option;
            d.Url = ForgeBase + "/" + Uri.EscapeDataString(full) +
                "/forge-" + Uri.EscapeDataString(full) + "-installer.jar";
            d.FileName = "forge-" + full + "-installer.jar";
            d.Option.Build = option.Build;
            return d;
        }

        public static async Task<List<JarOption>> ListNeoForgeAsync()
        {
            string xml = await GetTextAsync(NeoForgeMeta, CancellationToken.None);
            XDocument doc = XDocument.Parse(xml);
            Dictionary<string, string> latest = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();
            foreach (XElement element in doc.Descendants("version"))
            {
                string build = element.Value.Trim();
                string mc = NeoForgeMcVersion(build);
                if (string.IsNullOrWhiteSpace(mc)) continue;
                if (!latest.ContainsKey(mc)) order.Add(mc);
                string current;
                if (!latest.TryGetValue(mc, out current) || CompareDotted(build, current) > 0)
                    latest[mc] = build;
            }
            order.Sort(delegate(string a, string b) { return CompareDotted(b, a); });
            List<JarOption> list = new List<JarOption>();
            foreach (string mc in order)
            {
                JarOption option = new JarOption();
                option.ProviderId = "neoforged";
                option.ProviderName = "NeoForge";
                option.Channel = "mod";
                option.MinecraftVersion = mc;
                option.Build = latest[mc];
                option.Kind = "installer";
                list.Add(option);
            }
            return list;
        }

        public static async Task<JarDownload> ResolveNeoForgeAsync(JarOption option)
        {
            string build = option.Build;
            JarDownload d = new JarDownload();
            d.Option = option;
            d.Url = NeoForgeBase + "/" + Uri.EscapeDataString(build) +
                "/neoforge-" + Uri.EscapeDataString(build) + "-installer.jar";
            d.FileName = "neoforge-" + build + "-installer.jar";
            return d;
        }

        // Version de Minecraft que corresponde a una version de NeoForge.
        // Esquema antiguo: 20.4.237 -> 1.20.4, 21.1.77 -> 1.21.1, 47.1.106 -> 1.20.1.
        // Esquema nuevo:  26.1.2.126 -> 26.1.2, 26.3.0.101 -> 26.3.
        internal static string NeoForgeMcVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            string[] parts = version.Split('.');
            int major, minor, patch;
            if (parts.Length < 3 ||
                !int.TryParse(parts[0], out major) ||
                !int.TryParse(parts[1], out minor) ||
                !int.TryParse(parts[2], out patch)) return null;
            if (major == 47) return "1.20.1";
            if (major >= 26) return patch == 0 ? major + "." + minor : major + "." + minor + "." + patch;
            if (major >= 20) return "1." + major + (minor == 0 ? "" : "." + minor);
            return null;
        }

        internal static string ForgeMcVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            int dash = version.IndexOf('-');
            if (dash <= 0) return null;
            string mc = version.Substring(0, dash);
            if (!mc.StartsWith("1.", StringComparison.Ordinal) &&
                !mc.StartsWith("26.", StringComparison.Ordinal)) return null;
            return mc;
        }

        internal static string ForgeBuild(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            int dash = version.IndexOf('-');
            if (dash <= 0 || dash + 1 >= version.Length) return null;
            return version.Substring(dash + 1);
        }

        // Compara versiones con puntos/guiones numericamente. Se usa para
        // elegir el build mas reciente y para ordenar los catalogos.
        internal static int CompareDotted(string a, string b)
        {
            string[] pa = (a ?? "").Split(new char[] { '.', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries);
            string[] pb = (b ?? "").Split(new char[] { '.', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries);
            int count = Math.Min(pa.Length, pb.Length);
            for (int i = 0; i < count; i++)
            {
                long na, nb;
                bool aNumber = long.TryParse(pa[i], out na);
                bool bNumber = long.TryParse(pb[i], out nb);
                if (aNumber && bNumber)
                {
                    if (na != nb) return na.CompareTo(nb);
                }
                else
                {
                    int cmp = string.Compare(pa[i], pb[i], StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
            }
            return pa.Length.CompareTo(pb.Length);
        }

        internal static int JavaForMinecraft(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return 0;
            string text = version.Trim();
            if (text.StartsWith("26.", StringComparison.Ordinal) || text == "26") return 25;

            if (!text.StartsWith("1.", StringComparison.Ordinal)) return 0;
            string[] parts = text.Split('.');
            int minor;
            int patch = 0;
            if (parts.Length < 2 || !int.TryParse(parts[1], out minor)) return 0;
            if (parts.Length > 2) int.TryParse(parts[2], out patch);

            if (minor >= 21) return 21;
            if (minor == 20 && patch >= 5) return 21;
            if (minor >= 18) return 17;
            if (minor == 17) return 16;
            return 8;
        }

        private static async Task<string> LatestStableAsync(string url)
        {
            using (JsonDocument doc = await GetJsonAsync(url, CancellationToken.None))
            {
                string first = null;
                foreach (JsonElement item in doc.RootElement.EnumerateArray())
                {
                    string version = GetString(item, "version");
                    if (string.IsNullOrWhiteSpace(version)) continue;
                    if (first == null) first = version;
                    JsonElement stable;
                    if (item.TryGetProperty("stable", out stable) &&
                        stable.ValueKind == JsonValueKind.True) return version;
                }
                return first;
            }
        }

        private static async Task<string> GetTextAsync(string url, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new Exception("URL vacia.");
            using (HttpResponseMessage response = await Api.GetAsync(url, token))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(token);
            }
        }

        private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new Exception("URL vacia.");
            using (HttpResponseMessage response = await Api.GetAsync(url, token))
            {
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync(token);
                return JsonDocument.Parse(body);
            }
        }

        private static string GetString(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            return null;
        }

        private static long GetLong(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number)
                return value.GetInt64();
            return 0;
        }

        private static int GetInt(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number)
                return value.GetInt32();
            return 0;
        }
    }

    internal static class HttpDownloader
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "McPanel/1.0 (server manager)");
            return client;
        }

        public static async Task DownloadAsync(string url, string destination, long expectedSize,
            string expectedHash, string hashAlgorithm, Action<double, string> progress,
            CancellationToken token)
        {
            string tmp = destination + ".part";
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
                string dir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                using (HttpResponseMessage response = await Client.GetAsync(url,
                    HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    long total = expectedSize > 0 ? expectedSize : (response.Content.Headers.ContentLength ?? 0);
                    using (Stream input = await response.Content.ReadAsStreamAsync(token))
                    using (FileStream output = new FileStream(tmp, FileMode.Create, FileAccess.Write,
                        FileShare.None, 131072))
                    using (IncrementalHash hash = IncrementalHash.CreateHash(HashName(hashAlgorithm)))
                    {
                        byte[] buffer = new byte[131072];
                        long received = 0;
                        DateTime last = DateTime.UtcNow;
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read, token);
                            hash.AppendData(buffer, 0, read);
                            received += read;

                            if ((DateTime.UtcNow - last).TotalMilliseconds >= 200)
                            {
                                last = DateTime.UtcNow;
                                Report(progress, received, total);
                            }
                        }
                        await output.FlushAsync(token);
                        Report(progress, received, total);

                        if (!string.IsNullOrWhiteSpace(expectedHash))
                        {
                            string actual = Convert.ToHexString(hash.GetHashAndReset());
                            if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                                throw new Exception("El archivo descargado no coincide con el hash esperado.");
                        }
                    }
                }

                if (File.Exists(destination)) File.Delete(destination);
                File.Move(tmp, destination);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        private static HashAlgorithmName HashName(string name)
        {
            if (string.Equals(name, "SHA1", StringComparison.OrdinalIgnoreCase)) return HashAlgorithmName.SHA1;
            if (string.Equals(name, "MD5", StringComparison.OrdinalIgnoreCase)) return HashAlgorithmName.MD5;
            return HashAlgorithmName.SHA256;
        }

        private static void Report(Action<double, string> progress, long received, long total)
        {
            if (progress == null) return;
            double fraction = total > 0 ? Math.Min(1.0, (double)received / total) : 0;
            string text = total > 0
                ? "Descargando " + ((int)(fraction * 100)).ToString() + "% (" +
                  Utils.HumanBytes(received) + " / " + Utils.HumanBytes(total) + ")"
                : "Descargando " + Utils.HumanBytes(received);
            progress(fraction, text);
        }
    }

    internal static class JarInspector
    {
        public static JarMetadata Inspect(string jarPath)
        {
            JarMetadata meta = new JarMetadata();
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(jarPath))
                {
                    ZipArchiveEntry manifest = zip.GetEntry("META-INF/MANIFEST.MF");
                    if (manifest != null)
                    {
                        using (StreamReader reader = new StreamReader(manifest.Open()))
                        {
                            string text = reader.ReadToEnd();
                            string lower = text.ToLowerInvariant();
                            if (lower.Contains("purpur")) meta.Provider = "Purpur";
                            else if (lower.Contains("folia")) meta.Provider = "Folia";
                            else if (lower.Contains("paperclip") || lower.Contains("papermc"))
                                meta.Provider = "Paper";
                            else if (lower.Contains("neoforge")) meta.Provider = "NeoForge";
                            else if (lower.Contains("forge")) meta.Provider = "Forge";
                            else if (lower.Contains("fabric")) meta.Provider = "Fabric";
                        }
                    }

                    // Folia usa el mismo paperclip que Paper y no lo indica en
                    // el manifiesto; su API incluida en las librerias lo delata.
                    if (string.Equals(meta.Provider, "Paper", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(meta.Provider))
                    {
                        foreach (ZipArchiveEntry lib in zip.Entries)
                        {
                            if (lib.FullName.StartsWith("dev/folia/", StringComparison.OrdinalIgnoreCase) ||
                                lib.FullName.IndexOf("/dev/folia/", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                meta.Provider = "Folia";
                                break;
                            }
                        }
                    }

                    ZipArchiveEntry entry = zip.GetEntry("version.json");
                    if (string.IsNullOrWhiteSpace(meta.Provider) &&
                        (zip.GetEntry("fabric-server-launch.properties") != null ||
                         zip.GetEntry("net/fabricmc/loader/impl/launch/server/FabricServerLauncher.class") != null))
                        meta.Provider = "Fabric";
                    if (entry == null) return meta;

                    meta.HasVersionJson = true;
                    using (Stream stream = entry.Open())
                    using (JsonDocument doc = JsonDocument.Parse(stream))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement value;
                        if (root.TryGetProperty("id", out value) && value.ValueKind == JsonValueKind.String)
                            meta.Id = value.GetString();
                        if (root.TryGetProperty("name", out value) && value.ValueKind == JsonValueKind.String)
                            meta.Name = value.GetString();
                        if (root.TryGetProperty("stable", out value) && value.ValueKind == JsonValueKind.True)
                            meta.Stable = true;

                        if (root.TryGetProperty("java_version", out value) &&
                            value.ValueKind == JsonValueKind.Number)
                            meta.JavaMajor = value.GetInt32();
                        else if (root.TryGetProperty("javaVersion", out value) &&
                                 value.ValueKind == JsonValueKind.Object)
                        {
                            JsonElement major;
                            if (value.TryGetProperty("majorVersion", out major))
                                meta.JavaMajor = major.GetInt32();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(meta.Provider))
                        meta.Provider = meta.Stable ? "Vanilla" : "Snapshot";
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(meta.Id)) meta.Id = "";
            return meta;
        }
    }

    internal sealed class JarInstallResult
    {
        public bool Ok;
        public string Error = "";
        public string OldJarPath = "";
    }

    internal static class JarInstaller
    {
        public static JarInstallResult Install(ServerPaths paths, string newJarPath, string oldLabel)
        {
            JarInstallResult result = new JarInstallResult();
            try
            {
                if (!File.Exists(newJarPath)) throw new Exception("No se encontro el jar descargado.");
                Directory.CreateDirectory(paths.ServerDir);

                ServerProjects.MigrateLegacyBackups(paths.ServerDir);
                string jarsDir = ServerProjects.JarsBackupDirFor(paths.ServerDir);
                Directory.CreateDirectory(jarsDir);
                if (File.Exists(paths.Jar))
                {
                    string safe = SafeName(oldLabel);
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string oldPath = Path.Combine(jarsDir, "server-" + safe + "-" + stamp + ".jar");
                    File.Copy(paths.Jar, oldPath, false);
                    result.OldJarPath = oldPath;
                    CleanupOldJars(jarsDir, 3);
                }

                string staged = paths.Jar + ".new";
                if (File.Exists(staged)) File.Delete(staged);
                File.Copy(newJarPath, staged, true);
                if (File.Exists(paths.Jar))
                    File.Replace(staged, paths.Jar, null);
                else
                    File.Move(staged, paths.Jar);

                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                try
                {
                    string staged = paths.Jar + ".new";
                    if (File.Exists(staged)) File.Delete(staged);
                }
                catch { }
                return result;
            }
        }

        private static void CleanupOldJars(string dir, int keep)
        {
            try
            {
                List<FileInfo> files = new List<FileInfo>();
                foreach (string file in Directory.GetFiles(dir, "server-*.jar"))
                {
                    try { files.Add(new FileInfo(file)); } catch { }
                }
                files.Sort(delegate(FileInfo a, FileInfo b)
                {
                    return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc);
                });
                for (int i = keep; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "jar";
            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_') sb.Append(c);
                else if (char.IsWhiteSpace(c)) sb.Append('_');
            }
            string text = sb.ToString();
            if (text.Length > 60) text = text.Substring(0, 60);
            return string.IsNullOrEmpty(text) ? "jar" : text;
        }
    }

    internal sealed class ModdedInstallResult
    {
        public bool Ok;
        public string Error = "";
    }

    // Ejecuta el instalador oficial de Forge/NeoForge con --installServer.
    // El instalador descarga las librerias y deja los win_args.txt que luego
    // usa ConsoleBridge para arrancar el servidor.
    internal static class ModdedInstaller
    {
        public static async Task<ModdedInstallResult> InstallServerAsync(string installerPath,
            ServerPaths paths, string javaExe, Action<string> log)
        {
            ModdedInstallResult result = new ModdedInstallResult();
            Process process = null;
            try
            {
                if (!File.Exists(installerPath))
                    throw new Exception("No se encontro el instalador descargado.");
                if (string.IsNullOrWhiteSpace(javaExe) || !File.Exists(javaExe))
                    throw new Exception("No se encontro el ejecutable de Java para el instalador.");

                Directory.CreateDirectory(paths.ServerDir);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = javaExe;
                psi.Arguments = "-jar \"" + installerPath + "\" --installServer";
                psi.WorkingDirectory = paths.ServerDir;
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                process = new Process();
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data) && log != null)
                        log("[INSTALADOR] " + e.Data);
                };
                process.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrWhiteSpace(e.Data) && log != null)
                        log("[INSTALADOR] " + e.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Task wait = process.WaitForExitAsync();
                Task completed = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromMinutes(20)));
                if (completed != wait)
                {
                    try { process.Kill(true); } catch { }
                    throw new Exception("El instalador tardo mas de 20 minutos y se cancelo.");
                }
                await wait;
                if (process.ExitCode != 0)
                    throw new Exception("El instalador termino con codigo " +
                        process.ExitCode + ". Revisa la consola para mas detalles.");

                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                if (process != null)
                {
                    try { process.Dispose(); } catch { }
                }
            }
        }
    }

    internal sealed class JavaAsset
    {
        public int Major;
        public string Url = "";
        public string FileName = "";
        public long Size;
        public string Sha256 = "";
        public string ReleaseName = "";
    }

    internal static class JavaRuntimeManager
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, string> Cache = new Dictionary<int, string>();
        private static readonly HttpClient Api = CreateClient();

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(35);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "McPanel/1.0 (server manager)");
            return client;
        }

        public static string RootDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "McPanel", "jdk");
            }
        }

        public static string Find(int major)
        {
            if (major <= 0) return null;
            lock (Sync)
            {
                string cached;
                if (Cache.TryGetValue(major, out cached)) return cached;
            }

            string found = FindUncached(major);
            lock (Sync) Cache[major] = found;
            return found;
        }

        public static void ResetCache(int major)
        {
            lock (Sync) Cache.Remove(major);
        }

        private static string FindUncached(int major)
        {
            List<string> candidates = new List<string>();
            List<string> known = new List<string>();
            known.Add(Path.Combine(RootDir, major.ToString(), "bin", "java.exe"));

            ServerPaths paths = new ServerPaths();
            if (major == 25) known.Add(paths.Java25);
            if (major == 21) known.Add(paths.Java21);
            if (major == 25) known.Add(paths.JavaLatest);

            // Las rutas conocidas se usan sin lanzar java -version: el panel las
            // administra o fueron verificadas por el setup. Esto mantiene el
            // arranque del servidor instantaneo.
            foreach (string path in known)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
            }

            candidates.AddRange(known);

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
                candidates.Add(Path.Combine(javaHome, "bin", "java.exe"));

            try
            {
                string[] programFiles =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };
                foreach (string root in programFiles)
                {
                    if (string.IsNullOrWhiteSpace(root)) continue;
                    AddCandidates(candidates, Path.Combine(root, "Java"), "jdk-" + major);
                    AddCandidates(candidates, Path.Combine(root, "Eclipse Adoptium"), "jdk-" + major);
                    AddCandidates(candidates, Path.Combine(root, "Microsoft"), "jdk-" + major);
                }
            }
            catch { }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "where.exe";
                psi.Arguments = "java.exe";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);
                    foreach (string line in output.Split(new char[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        string path = line.Trim();
                        if (File.Exists(path)) candidates.Add(path);
                    }
                }
            }
            catch { }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) continue;
                if (!seen.Add(candidate)) continue;
                int detected = DetectMajor(candidate);
                if (detected == major) return candidate;
            }
            return null;
        }

        private static void AddCandidates(List<string> list, string root, string prefix)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                foreach (string dir in Directory.GetDirectories(root, prefix + "*"))
                    list.Add(Path.Combine(dir, "bin", "java.exe"));
            }
            catch { }
        }

        public static int DetectMajor(string javaExe)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = javaExe;
                psi.Arguments = "-XshowSettings:properties -version";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(3500))
                    {
                        try { p.Kill(); } catch { }
                        return 0;
                    }
                    return ParseMajor(output);
                }
            }
            catch
            {
                return 0;
            }
        }

        private static int ParseMajor(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            Match spec = Regex.Match(text, @"java\.specification\.version\s*=\s*(\d+)");
            if (spec.Success)
            {
                int value;
                if (int.TryParse(spec.Groups[1].Value, out value)) return value;
            }
            Match version = Regex.Match(text, @"(?:java|openjdk) version ""(\d+)(?:\.(\d+))?");
            if (version.Success)
            {
                int value;
                if (int.TryParse(version.Groups[1].Value, out value))
                {
                    if (value == 1 && version.Groups[2].Success)
                    {
                        int old;
                        if (int.TryParse(version.Groups[2].Value, out old)) return old;
                    }
                    return value;
                }
            }
            return 0;
        }

        public static async Task<JavaAsset> GetAssetAsync(int major)
        {
            string url = "https://api.adoptium.net/v3/assets/latest/" + major.ToString() +
                "/hotspot?architecture=x64&image_type=jdk&os=windows&vendor=eclipse";
            using (HttpResponseMessage response = await Api.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                        return null;
                    JsonElement first = root[0];
                    JavaAsset asset = new JavaAsset();
                    asset.Major = major;
                    asset.ReleaseName = GetString(first, "release_name") ?? ("JDK " + major);
                    JsonElement binary;
                    if (!first.TryGetProperty("binary", out binary)) return null;
                    JsonElement package;
                    if (!binary.TryGetProperty("package", out package)) return null;
                    asset.Url = GetString(package, "link") ?? "";
                    asset.FileName = GetString(package, "name") ?? ("jdk-" + major + ".zip");
                    asset.Sha256 = GetString(package, "checksum") ?? "";
                    JsonElement size;
                    if (package.TryGetProperty("size", out size) && size.ValueKind == JsonValueKind.Number)
                        asset.Size = size.GetInt64();
                    if (string.IsNullOrWhiteSpace(asset.Url)) return null;
                    return asset;
                }
            }
        }

        public static async Task<string> DownloadAsync(JavaAsset asset, Action<double, string> progress,
            CancellationToken token)
        {
            if (asset == null) throw new Exception("No hay un JDK disponible para descargar.");
            string target = Path.Combine(RootDir, asset.Major.ToString());
            string javaExe = Path.Combine(target, "bin", "java.exe");
            if (File.Exists(javaExe)) return javaExe;

            string tempRoot = Path.Combine(Path.GetTempPath(), "McPanel", "jdk-" + asset.Major.ToString());
            string zipPath = tempRoot + ".zip";
            string extractDir = tempRoot + "-extract";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
                await HttpDownloader.DownloadAsync(asset.Url, zipPath, asset.Size, asset.Sha256,
                    "SHA256", progress, token);

                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                string found = "";
                foreach (string file in Directory.GetFiles(extractDir, "java.exe", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase))
                    {
                        found = file;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(found))
                    throw new Exception("El JDK descargado no contiene bin\\java.exe.");

                string root = Path.GetDirectoryName(Path.GetDirectoryName(found));
                if (Directory.Exists(target))
                {
                    try { Directory.Delete(target, true); } catch { }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                Directory.Move(root, target);
                ResetCache(asset.Major);

                if (!File.Exists(javaExe))
                    throw new Exception("El JDK se extrajo pero no se encontro java.exe.");
                return javaExe;
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
            }
        }

        private static string GetString(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            return null;
        }
    }
}
