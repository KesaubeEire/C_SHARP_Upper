using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WpfScada.Models.Plc;

public class NetworkAdapter
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public bool IsLoopback { get; set; }
    public string Display => $"{Name} ({Ip})";

    public static List<NetworkAdapter> Enumerate()
    {
        return [.. NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses
                .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(ua => new NetworkAdapter
                {
                    Name = ni.Name,
                    Ip = ua.Address.ToString(),
                    IsLoopback = IPAddress.IsLoopback(ua.Address)
                }))];
    }
}
