using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace McPanel
{
    internal sealed class CloudCheckResult
    {
        public string Provider = "";
        public bool Ok;
        public bool Reachable;
        public int StatusCode;
        public string Message = "";
        public string Detail = "";
    }
    internal static class CloudBackupService
    {
        public const string GoogleDrive = "GoogleDrive";
        public const string MediaFire = "MediaFire";
        public const string Mega = "Mega";

        public static readonly string[] Providers =
            new string[] { GoogleDrive, MediaFire, Mega };

        public static string DisplayName(string provider)
        {
            if (provider == GoogleDrive) return "Google Drive";
            if (provider == MediaFire) return "MediaFire";
            if (provider == Mega) return "MEGA";
            return provider;
        }

        public static string DocsUrl(string provider)
        {
            if (provider == GoogleDrive) return "https://developers.google.com/drive/api/guides/about-sdk";
            if (provider == MediaFire) return "https://www.mediafire.com/developers/core_api/1.5/getting_started/";
            if (provider == Mega) return "https://mega.io/developers";
            return "https://www.mediafire.com/developers/";
        }

        public static string Setting(IReadOnlyDictionary<string, string> settings, string key)
        {
            if (settings == null) return "";
            string value;
            return settings.TryGetValue(key, out value) && value != null ? value.Trim() : "";
        }

        public static IDictionary<string, string> SettingsFromConfig()
        {
            if (AppConfig.Instance.CloudSettings == null)
                AppConfig.Instance.CloudSettings = new Dictionary<string, string>();
            return AppConfig.Instance.CloudSettings;
        }

        public static async Task<CloudCheckResult> ValidateAsync(string provider,
            IReadOnlyDictionary<string, string> settings, HttpClient client,
            CancellationToken cancellationToken)
        {
            if (provider == GoogleDrive)
                return await ValidateGoogleAsync(settings, client, cancellationToken);
            if (provider == MediaFire)
                return await ValidateMediaFireAsync(settings, client, cancellationToken);
            return await ValidateMegaAsync(settings, client, cancellationToken);
        }

        private static async Task<CloudCheckResult> ValidateGoogleAsync(
            IReadOnlyDictionary<string, string> settings, HttpClient client,
            CancellationToken cancellationToken)
        {
            CloudCheckResult result = new CloudCheckResult();
            result.Provider = GoogleDrive;
            string token = Setting(settings, "google.token");
            string folder = Setting(settings, "google.folder");
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                    "https://www.googleapis.com/drive/v3/about?fields=user"))
                {
                    if (!string.IsNullOrEmpty(token))
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                    using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken))
                    {
                        result.StatusCode = (int)response.StatusCode;
                        string body = await response.Content.ReadAsStringAsync();
                        if (response.IsSuccessStatusCode)
                        {
                            result.Reachable = true;
                            result.Ok = true;
                            result.Message = "API disponible y token valido.";
                            result.Detail = string.IsNullOrEmpty(folder)
                                ? "Falta la carpeta destino (opcional)."
                                : "Carpeta destino: " + folder;
                        }
                        else if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            result.Reachable = true;
                            result.Ok = false;
                            result.Message = "API disponible. Token ausente o invalido.";
                            result.Detail = "HTTP 401. Genera un token OAuth con permiso drive.file.";
                        }
                        else
                        {
                            result.Reachable = true;
                            result.Ok = false;
                            result.Message = "La API respondio HTTP " + result.StatusCode + ".";
                            result.Detail = Shorten(body, 240);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Reachable = false;
                result.Message = "No se pudo contactar la API.";
                result.Detail = ex.Message;
            }
            return result;
        }

        private static async Task<CloudCheckResult> ValidateMediaFireAsync(
            IReadOnlyDictionary<string, string> settings, HttpClient client,
            CancellationToken cancellationToken)
        {
            CloudCheckResult result = new CloudCheckResult();
            result.Provider = MediaFire;
            string session = Setting(settings, "mediafire.session");
            string appId = Setting(settings, "mediafire.appid");
            try
            {
                string url = "https://www.mediafire.com/api/1.5/system/get_version.php?response_format=json";
                if (!string.IsNullOrEmpty(session))
                    url = "https://www.mediafire.com/api/1.5/user/get_info.php?response_format=json&session_token=" +
                        Uri.EscapeDataString(session);

                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken))
                {
                    result.StatusCode = (int)response.StatusCode;
                    string body = await response.Content.ReadAsStringAsync();
                    result.Reachable = response.IsSuccessStatusCode;
                    if (!response.IsSuccessStatusCode)
                    {
                        result.Ok = false;
                        result.Message = "La API respondio HTTP " + result.StatusCode + ".";
                        result.Detail = Shorten(body, 240);
                        return result;
                    }

                    bool success = false;
                    string apiResult = "";
                    string apiMessage = "";
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(body))
                        {
                            JsonElement responseElement;
                            if (doc.RootElement.TryGetProperty("response", out responseElement))
                            {
                                JsonElement value;
                                if (responseElement.TryGetProperty("result", out value))
                                    apiResult = value.GetString() ?? "";
                                if (responseElement.TryGetProperty("message", out value))
                                    apiMessage = value.GetString() ?? "";
                            }
                        }
                    }
                    catch { }

                    success = string.Equals(apiResult, "Success", StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(session))
                    {
                        result.Ok = success;
                        result.Message = success
                            ? "API disponible y session token valido."
                            : "API disponible. Session token invalido.";
                        result.Detail = success ? "Sesion autenticada." :
                            (string.IsNullOrEmpty(apiMessage) ? "La API rechazo la sesion." : apiMessage);
                    }
                    else
                    {
                        result.Ok = false;
                        result.Message = "API disponible. Falta el session token.";
                        result.Detail = string.IsNullOrEmpty(appId)
                            ? "Configura App ID y session token."
                            : "App ID: " + appId;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Reachable = false;
                result.Message = "No se pudo contactar la API.";
                result.Detail = ex.Message;
            }
            return result;
        }

        private static async Task<CloudCheckResult> ValidateMegaAsync(
            IReadOnlyDictionary<string, string> settings, HttpClient client,
            CancellationToken cancellationToken)
        {
            CloudCheckResult result = new CloudCheckResult();
            result.Provider = Mega;
            string session = Setting(settings, "mega.session");
            string email = Setting(settings, "mega.email");
            try
            {
                string payload = string.IsNullOrEmpty(session)
                    ? "[{\"a\":\"us\"}]"
                    : "[{\"a\":\"us\",\"sess\":" + JsonSerializer.Serialize(session) + "}]";
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post,
                    "https://g.api.mega.co.nz/cs?id=0"))
                {
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken))
                    {
                        result.StatusCode = (int)response.StatusCode;
                        string body = await response.Content.ReadAsStringAsync();
                        result.Reachable = response.IsSuccessStatusCode;
                        if (!response.IsSuccessStatusCode)
                        {
                            result.Ok = false;
                            result.Message = "La API respondio HTTP " + result.StatusCode + ".";
                            result.Detail = Shorten(body, 240);
                            return result;
                        }

                        int apiCode = int.MinValue;
                        try
                        {
                            using (JsonDocument doc = JsonDocument.Parse(body))
                            {
                                if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                                    doc.RootElement.GetArrayLength() > 0)
                                {
                                    JsonElement first = doc.RootElement[0];
                                    JsonElement value;
                                    if (first.ValueKind == JsonValueKind.Object &&
                                        first.TryGetProperty("e", out value) &&
                                        value.ValueKind == JsonValueKind.Number)
                                        apiCode = value.GetInt32();
                                    else if (first.ValueKind == JsonValueKind.Number)
                                        apiCode = first.GetInt32();
                                }
                            }
                        }
                        catch { }

                        bool validJson = apiCode != int.MinValue ||
                            body.TrimStart().StartsWith("[", StringComparison.Ordinal);
                        if (!validJson)
                        {
                            result.Ok = false;
                            result.Message = "La API respondio, pero el formato no es JSON de MEGA.";
                            result.Detail = Shorten(body, 240);
                            return result;
                        }

                        if (!string.IsNullOrEmpty(session))
                        {
                            result.Ok = apiCode >= 0;
                            result.Message = apiCode >= 0
                                ? "API disponible y sesion MEGA valida."
                                : "API disponible. Sesion MEGA invalida.";
                            result.Detail = "Codigo de la API: " + apiCode +
                                (string.IsNullOrEmpty(email) ? "" : " | " + email);
                        }
                        else
                        {
                            result.Ok = false;
                            result.Message = "API disponible. Falta la sesion MEGA.";
                            result.Detail = "Codigo de la API: " + apiCode +
                                " (sin sesion).";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Reachable = false;
                result.Message = "No se pudo contactar la API.";
                result.Detail = ex.Message;
            }
            return result;
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }
    }
}
