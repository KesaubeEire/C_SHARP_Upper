using System.Net.NetworkInformation;
using System.Net;

namespace TestWpf.Models;

public class NetworkAdapter
{
    public string Name { get; init; } = "";
    public string Ip { get; init; } = "";
    public bool IsLoopback { get; init; }
    public string Display => $"{Name} ({Ip})";

    /// <summary>枚举所有可用的 IPv4 网卡</summary>
    public static List<NetworkAdapter> Enumerate()
    {
        var list = new List<NetworkAdapter>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            var ipProps = ni.GetIPProperties();
            var addr = ipProps.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (addr == null) continue;
            list.Add(new NetworkAdapter
            {
                Name = ni.Name,
                Ip = addr.Address.ToString(),
                IsLoopback = IPAddress.IsLoopback(addr.Address)
            });
        }
        return list;
    }
}
