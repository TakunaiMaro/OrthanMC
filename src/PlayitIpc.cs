#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace McPanel
{
    internal sealed class PlayitTunnel
    {
        public string DisplayAddress = "";
        public string Destination = "";
        public bool Disabled;

        public string Label
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DisplayAddress)) return Destination;
                if (string.IsNullOrWhiteSpace(Destination)) return DisplayAddress;
                return DisplayAddress + " -> " + Destination;
            }
        }
    }

    // playitd publica su estado por un named pipe con lineas JSON (IPC v2).
    // No hace falta adivinar el dominio en logs ni hardcodearlo: el agente
    // devuelve el display_address real cada vez que se consulta.
    internal static class PlayitIpc
    {
        private const string PipeName = "playitd-system";
        private const int IpcVersion = 2;

        public static List<PlayitTunnel> GetTunnels(int timeoutMs)
        {
            List<PlayitTunnel> result = new List<PlayitTunnel>();
            try
            {
                using (NamedPipeClientStream pipe = new NamedPipeClientStream(
                    ".", PipeName, PipeDirection.InOut, PipeOptions.None))
                {
                    pipe.Connect(Math.Max(200, timeoutMs));

                    using (StreamReader reader = new StreamReader(
                        pipe, new UTF8Encoding(false), false, 4096, true))
                    using (StreamWriter writer = new StreamWriter(
                        pipe, new UTF8Encoding(false), 4096, true))
                    {
                        writer.NewLine = "\n";
                        writer.AutoFlush = true;

                        // El servidor saluda primero.
                        string hello = ReadLine(reader, timeoutMs);
                        if (string.IsNullOrEmpty(hello)) return result;

                        writer.WriteLine("{\"ipc_version\":" + IpcVersion +
                            ",\"request_id\":1,\"request\":{\"type\":\"get_state\"}}");

                        for (int i = 0; i < 8; i++)
                        {
                            string line = ReadLine(reader, timeoutMs);
                            if (string.IsNullOrEmpty(line)) break;
                            if (!IsWantedStateResponse(line)) continue;

                            using (JsonDocument doc = JsonDocument.Parse(line))
                            {
                                JsonElement root = doc.RootElement;
                                JsonElement data = root.GetProperty("data");
                                JsonElement response = data.GetProperty("response");
                                JsonElement state = response.GetProperty("data");
                                if (!string.Equals(GetString(state, "state"), "running",
                                    StringComparison.OrdinalIgnoreCase)) break;

                                JsonElement running = state.GetProperty("data");
                                JsonElement tunnels;
                                if (!running.TryGetProperty("tunnels", out tunnels) ||
                                    tunnels.ValueKind != JsonValueKind.Array) break;

                                foreach (JsonElement t in tunnels.EnumerateArray())
                                {
                                    PlayitTunnel tunnel = new PlayitTunnel();
                                    tunnel.DisplayAddress = GetString(t, "display_address") ?? "";
                                    tunnel.Destination = GetString(t, "destination") ?? "";
                                    bool disabled = false;
                                    JsonElement dis;
                                    if (t.TryGetProperty("is_disabled", out dis))
                                    {
                                        if (dis.ValueKind == JsonValueKind.True) disabled = true;
                                        else if (dis.ValueKind == JsonValueKind.False) disabled = false;
                                    }
                                    tunnel.Disabled = disabled;
                                    result.Add(tunnel);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Servicio apagado, pipe ocupado o version distinta: el panel
                // conserva el ultimo dominio conocido.
            }
            return result;
        }

        public static PlayitTunnel PickForPort(List<PlayitTunnel> tunnels, int port)
        {
            if (tunnels == null || tunnels.Count == 0) return null;
            string suffix = ":" + port.ToString();
            foreach (PlayitTunnel t in tunnels)
            {
                if (!t.Disabled && t.Destination != null &&
                    t.Destination.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            foreach (PlayitTunnel t in tunnels)
            {
                if (!t.Disabled && !string.IsNullOrWhiteSpace(t.DisplayAddress)) return t;
            }
            return null;
        }

        private static bool IsWantedStateResponse(string line)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(line))
                {
                    JsonElement root = doc.RootElement;
                    string kind = GetString(root, "message_kind");
                    if (!string.Equals(kind, "response", StringComparison.OrdinalIgnoreCase)) return false;
                    JsonElement data = root.GetProperty("data");
                    if (data.GetProperty("request_id").GetInt64() != 1) return false;
                    JsonElement response = data.GetProperty("response");
                    return string.Equals(GetString(response, "type"), "state",
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetString(JsonElement element, string name)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            return null;
        }

        private static string ReadLine(StreamReader reader, int timeoutMs)
        {
            try
            {
                Task<string> task = reader.ReadLineAsync();
                if (!task.Wait(Math.Max(250, timeoutMs))) return null;
                return task.Result;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class NetworkInfo
    {
        private static readonly object Sync = new object();
        private static string _cached = "";
        private static DateTime _cachedAt = DateTime.MinValue;

        public static string GetLocalIPv4()
        {
            lock (Sync)
            {
                if ((DateTime.UtcNow - _cachedAt).TotalSeconds < 10) return _cached;
            }

            string best = "";
            int bestScore = int.MinValue;
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    IPInterfaceProperties props = nic.GetIPProperties();
                    bool hasGateway = props.GatewayAddresses != null && props.GatewayAddresses.Count > 0;

                    foreach (UnicastIPAddressInformation info in props.UnicastAddresses)
                    {
                        IPAddress ip = info.Address;
                        if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) continue;
                        string text = ip.ToString();
                        if (text.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                        if (IPAddress.IsLoopback(ip)) continue;

                        int score = 0;
                        if (IsPrivate(text)) score += 20;
                        if (hasGateway) score += 10;
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 6;
                        else if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 5;
                        else if (nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet) score += 4;
                        string type = nic.NetworkInterfaceType.ToString();
                        if (type.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            type.IndexOf("Hyper-V", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            type.IndexOf("VMware", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            type.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0)
                            score -= 6;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = text;
                        }
                    }
                }
            }
            catch { }

            lock (Sync)
            {
                _cached = best;
                _cachedAt = DateTime.UtcNow;
            }
            return best;
        }

        private static bool IsPrivate(string ip)
        {
            if (ip.StartsWith("10.", StringComparison.Ordinal)) return true;
            if (ip.StartsWith("192.168.", StringComparison.Ordinal)) return true;
            if (ip.StartsWith("172.", StringComparison.Ordinal))
            {
                string[] parts = ip.Split('.');
                int second;
                if (parts.Length > 1 && int.TryParse(parts[1], out second))
                    return second >= 16 && second <= 31;
            }
            return false;
        }
    }
}
