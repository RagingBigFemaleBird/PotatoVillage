using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    /// <summary>
    /// Background service that responds to UDP discovery requests from clients.
    /// This allows clients on the local network to find the server automatically.
    /// </summary>
    public class DiscoveryService : BackgroundService
    {
        private const int DiscoveryPort = 47777;
        private const string DiscoveryMessage = "POTATOVILLAGE_DISCOVER";
        private const string ResponsePrefix = "POTATOVILLAGE_SERVER:";

        private readonly ILogger<DiscoveryService> _logger;
        private readonly IConfiguration _configuration;

        public DiscoveryService(ILogger<DiscoveryService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Discovery service starting on port {Port}", DiscoveryPort);

            try
            {
                using var udpClient = new UdpClient(DiscoveryPort);
                udpClient.EnableBroadcast = true;

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync(stoppingToken);
                        var message = Encoding.UTF8.GetString(result.Buffer);

                        if (message == DiscoveryMessage)
                        {
                            _logger.LogInformation("Received discovery request from {RemoteEndPoint}", result.RemoteEndPoint);

                            // Get server URL to send back
                            var serverUrl = GetServerUrl(result.RemoteEndPoint);
                            var response = $"{ResponsePrefix}{serverUrl}";
                            var responseData = Encoding.UTF8.GetBytes(response);

                            await udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                            _logger.LogInformation("Sent discovery response: {ServerUrl}", serverUrl);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing discovery request");
                    }
                }
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Could not start discovery service on port {Port}. Discovery will be unavailable.", DiscoveryPort);
            }

            _logger.LogInformation("Discovery service stopped");
        }

        private string GetServerUrl(IPEndPoint clientEndPoint)
        {
            // Get the local IP address that can reach the client
            var localIp = GetLocalIpAddress(clientEndPoint.Address);
            
            // Get the port from configuration or use default
            var port = _configuration["SERVER_PORT"] ?? 
                       Environment.GetEnvironmentVariable("PORT") ?? 
                       "5000";

            return $"http://{localIp}:{port}/gamehub";
        }

        private static string GetLocalIpAddress(IPAddress clientAddress)
        {
            try
            {
                // Try to find the best local IP to reach the client
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(clientAddress, 1);
                var localEndPoint = socket.LocalEndPoint as IPEndPoint;
                return localEndPoint?.Address.ToString() ?? GetFallbackIpAddress();
            }
            catch
            {
                return GetFallbackIpAddress();
            }
        }

        private static string GetFallbackIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return "localhost";
        }
    }
}
