using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PotatoVillage.Services
{
    public class ServerDiscoveryService
    {
        private const int DiscoveryPort = 47777;
        private const string DiscoveryMessage = "POTATOVILLAGE_DISCOVER";
        private const string ResponsePrefix = "POTATOVILLAGE_SERVER:";
        
        // Default Azure production URL
        public const string DefaultServerUrl = "https://potatovillage-server.livelysmoke-6f078c90.eastus.azurecontainerapps.io/gamehub";
        
        // Local development URL
        public const string LocalServerUrl = "http://localhost:5000/gamehub";

        /// <summary>
        /// Attempts to discover a server on the local network.
        /// Returns the server URL if found, or the default URL if not.
        /// </summary>
        public static async Task<string> DiscoverServerAsync(int timeoutMs = 3000)
        {
            try
            {
                // First, try to find a local server via UDP broadcast
                var localServer = await DiscoverLocalServerAsync(timeoutMs);
                if (!string.IsNullOrEmpty(localServer))
                {
                    return localServer;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Local discovery failed: {ex.Message}");
            }

            // Fall back to default Azure URL
            return DefaultServerUrl;
        }

        /// <summary>
        /// Discovers a server on the local network using UDP broadcast.
        /// </summary>
        private static async Task<string?> DiscoverLocalServerAsync(int timeoutMs)
        {
            using var client = new UdpClient();
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = timeoutMs;

            var requestData = Encoding.UTF8.GetBytes(DiscoveryMessage);
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

            // Send discovery request
            await client.SendAsync(requestData, requestData.Length, broadcastEndpoint);

            // Wait for response with timeout
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                var receiveTask = client.ReceiveAsync();
                var completedTask = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs, cts.Token));

                if (completedTask == receiveTask)
                {
                    var result = await receiveTask;
                    var response = Encoding.UTF8.GetString(result.Buffer);

                    if (response.StartsWith(ResponsePrefix))
                    {
                        var serverUrl = response.Substring(ResponsePrefix.Length);
                        System.Diagnostics.Debug.WriteLine($"Discovered local server: {serverUrl}");
                        return serverUrl;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout - no server found
            }
            catch (SocketException)
            {
                // Network error - no server found
            }

            return null;
        }

        /// <summary>
        /// Gets a list of known server URLs for manual selection.
        /// </summary>
        public static List<string> GetKnownServers()
        {
            return new List<string>
            {
                DefaultServerUrl,
                LocalServerUrl,
                "http://10.0.2.2:5000/gamehub" // Android emulator localhost
            };
        }

        /// <summary>
        /// Checks if a server is reachable at the given URL.
        /// </summary>
        public static async Task<bool> IsServerReachableAsync(string hubUrl, int timeoutMs = 5000)
        {
            try
            {
                // Extract base URL from hub URL
                var uri = new Uri(hubUrl);
                var baseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

                var response = await httpClient.GetAsync(baseUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
