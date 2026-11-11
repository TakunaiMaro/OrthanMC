#nullable disable
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace McPanel
{
    // Resultado de una comprobacion de actualizaciones.
    internal sealed class UpdateInfo
    {
        public bool HasUpdate;
        public string CurrentVersion = "";
        public string LatestVersion = "";
        public string ReleaseName = "";
        public string ReleaseUrl = ReleaseConfig.ReleasesUrl;
        public string Notes = "";
        public string Error = "";
    }

    // Comprueba la ultima release publicada en GitHub. No descarga ni
    // ejecuta nada por si solo: la interfaz ofrece abrir el release y el
    // usuario decide cuando actualizar.
    internal static class UpdateService
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(12);
            // GitHub exige un User-Agent en la API publica.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OrthancMc-Updater/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    Assembly assembly = typeof(UpdateService).Assembly;
                    AssemblyInformationalVersionAttribute info =
                        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
                    {
                        string text = info.InformationalVersion.Trim();
                        int plus = text.IndexOf('+');
                        if (plus > 0) text = text.Substring(0, plus);
                        if (!string.IsNullOrWhiteSpace(text)) return text;
                    }

                    Version version = assembly.GetName().Version;
                    if (version != null) return version.ToString(3);
                }
                catch { }
                return "0.0.0";
            }
        }

        public static async Task<UpdateInfo> CheckAsync()
        {
            UpdateInfo result = new UpdateInfo();
            result.CurrentVersion = CurrentVersion;

            try
            {
                using (HttpResponseMessage response =
                    await Client.GetAsync(ReleaseConfig.LatestReleaseApi).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        result.Error = "El repositorio todavia no tiene releases publicadas.";
                        return result;
                    }

                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    using (JsonDocument document = JsonDocument.Parse(json))
                    {
                        JsonElement root = document.RootElement;
                        string tag = GetString(root, "tag_name");
                        result.LatestVersion = Normalize(tag);
                        result.ReleaseName = GetString(root, "name");
                        result.ReleaseUrl = GetString(root, "html_url");
                        result.Notes = GetString(root, "body");

                        if (string.IsNullOrWhiteSpace(result.ReleaseUrl))
                            result.ReleaseUrl = ReleaseConfig.ReleasesUrl;

                        bool prerelease = root.TryGetProperty("prerelease", out JsonElement pre) &&
                            pre.ValueKind == JsonValueKind.True;

                        if (string.IsNullOrWhiteSpace(result.LatestVersion))
                        {
                            result.Error = "La release publicada no tiene un tag de version.";
                        }
                        else if (!prerelease)
                        {
                            result.HasUpdate = IsNewer(result.LatestVersion, result.CurrentVersion);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = "No se pudo comprobar la actualizacion: " + ex.Message;
            }

            return result;
        }

        public static bool IsNewer(string latest, string current)
        {
            Version latestVersion = ParseVersion(latest);
            Version currentVersion = ParseVersion(current);
            if (latestVersion == null || currentVersion == null) return false;
            return latestVersion > currentVersion;
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string value = text.Trim();
            int space = value.IndexOf(' ');
            if (space > 0) value = value.Substring(0, space);
            if (value.Length > 1 && (value[0] == 'v' || value[0] == 'V'))
                value = value.Substring(1);
            return value.Trim();
        }

        private static Version ParseVersion(string text)
        {
            string value = Normalize(text);
            if (string.IsNullOrWhiteSpace(value)) return null;

            int cut = value.IndexOfAny(new char[] { '-', '+' });
            if (cut >= 0) value = value.Substring(0, cut);

            string[] parts = value.Split('.');
            int major = 0, minor = 0, build = 0, revision = 0;
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            if (parts.Length > 2) int.TryParse(parts[2], out build);
            if (parts.Length > 3) int.TryParse(parts[3], out revision);

            if (major == 0 && minor == 0 && build == 0 && revision == 0) return null;
            return new Version(major, minor, build, revision);
        }

        private static string GetString(JsonElement element, string property)
        {
            try
            {
                if (element.TryGetProperty(property, out JsonElement value) &&
                    value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? "";
            }
            catch { }
            return "";
        }
    }
}
