using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WindowsControlPanel.Service;

public sealed class NetworkStatusSnapshot
{
    public DateTime Timestamp { get; init; }
    public IReadOnlyList<NetworkAdapterSnapshot> Adapters { get; init; } = Array.Empty<NetworkAdapterSnapshot>();

    public int ActiveAdapterCount => Adapters.Count(adapter => adapter.IsUp);

    public string Summary =>
        Adapters.Count == 0
            ? "未发现可显示的网络适配器。"
            : $"发现 {Adapters.Count} 个网络适配器，当前在线 {ActiveAdapterCount} 个。";
}

public sealed class NetworkAdapterSnapshot
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string InterfaceType { get; init; } = "未知";
    public string OperationalStatus { get; init; } = "未知";
    public string Speed { get; init; } = "未知";
    public string MacAddress { get; init; } = "未报告";
    public string DnsSuffix { get; init; } = "未配置";
    public bool IsUp { get; init; }
    public bool? IsDhcpEnabled { get; init; }
    public IReadOnlyList<string> IPv4Addresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GatewayAddresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DhcpServers { get; init; } = Array.Empty<string>();
    public string Warning { get; init; } = string.Empty;

    public string IPv4Summary => JoinOrFallback(IPv4Addresses, "未分配 IPv4");
    public string DnsSummary => JoinOrFallback(DnsServers, "未配置 IPv4 DNS");
    public string GatewaySummary => JoinOrFallback(GatewayAddresses, "未配置 IPv4 网关");
    public string DhcpServerSummary => JoinOrFallback(DhcpServers, "未报告 DHCP 服务器");

    public string DhcpSummary =>
        IsDhcpEnabled switch
        {
            true => $"已启用；服务器：{DhcpServerSummary}",
            false => "已关闭或静态 IPv4",
            _ => "未报告"
        };

    public string ListSummary =>
        $"{OperationalStatus} | {InterfaceType} | IPv4: {IPv4Summary}";

    public string DetailText
    {
        get
        {
            var lines = new List<string>
            {
                $"名称：{Name}",
                $"描述：{Description}",
                $"状态：{OperationalStatus}",
                $"类型：{InterfaceType}",
                $"链路速度：{Speed}",
                $"MAC：{MacAddress}",
                $"DNS 后缀：{DnsSuffix}",
                $"IPv4：{IPv4Summary}",
                $"默认网关：{GatewaySummary}",
                $"DNS：{DnsSummary}",
                $"DHCP：{DhcpSummary}"
            };

            if (!string.IsNullOrWhiteSpace(Warning))
            {
                lines.Add($"提示：{Warning}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string JoinOrFallback(IReadOnlyList<string> values, string fallback)
    {
        return values.Count == 0 ? fallback : string.Join(", ", values);
    }
}

public interface INetworkStatusService
{
    Task<NetworkStatusSnapshot> GetNetworkStatusAsync();
}

public sealed class NetworkStatusService : INetworkStatusService
{
    public Task<NetworkStatusSnapshot> GetNetworkStatusAsync()
    {
        return Task.Run(() =>
        {
            var adapters = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(CreateSnapshot)
                .OrderByDescending(adapter => adapter.IsUp)
                .ThenBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return new NetworkStatusSnapshot
            {
                Timestamp = DateTime.Now,
                Adapters = adapters
            };
        });
    }

    private static NetworkAdapterSnapshot CreateSnapshot(NetworkInterface adapter)
    {
        try
        {
            var properties = adapter.GetIPProperties();
            var ipv4Properties = GetIPv4Properties(adapter, properties);

            var ipv4Addresses = properties
                .UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(FormatIPv4Address)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var dnsServers = properties
                .DnsAddresses
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var gateways = properties
                .GatewayAddresses
                .Where(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(gateway => gateway.Address)
                .Where(address => !IPAddress.Any.Equals(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var dhcpServers = properties
                .DhcpServerAddresses
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new NetworkAdapterSnapshot
            {
                Id = adapter.Id,
                Name = adapter.Name,
                Description = adapter.Description,
                InterfaceType = FormatInterfaceType(adapter.NetworkInterfaceType),
                OperationalStatus = FormatOperationalStatus(adapter.OperationalStatus),
                Speed = FormatSpeed(GetSpeed(adapter)),
                MacAddress = FormatPhysicalAddress(adapter),
                DnsSuffix = string.IsNullOrWhiteSpace(properties.DnsSuffix) ? "未配置" : properties.DnsSuffix,
                IsUp = adapter.OperationalStatus == OperationalStatus.Up,
                IsDhcpEnabled = ipv4Properties?.IsDhcpEnabled,
                IPv4Addresses = ipv4Addresses,
                DnsServers = dnsServers,
                GatewayAddresses = gateways,
                DhcpServers = dhcpServers
            };
        }
        catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException or PlatformNotSupportedException)
        {
            return new NetworkAdapterSnapshot
            {
                Id = adapter.Id,
                Name = adapter.Name,
                Description = adapter.Description,
                InterfaceType = FormatInterfaceType(adapter.NetworkInterfaceType),
                OperationalStatus = FormatOperationalStatus(adapter.OperationalStatus),
                Speed = FormatSpeed(GetSpeed(adapter)),
                MacAddress = FormatPhysicalAddress(adapter),
                IsUp = adapter.OperationalStatus == OperationalStatus.Up,
                Warning = $"读取适配器详细信息失败：{ex.Message}"
            };
        }
    }

    private static IPv4InterfaceProperties? GetIPv4Properties(
        NetworkInterface adapter,
        IPInterfaceProperties properties
    )
    {
        try
        {
            return adapter.Supports(NetworkInterfaceComponent.IPv4)
                ? properties.GetIPv4Properties()
                : null;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static string FormatIPv4Address(UnicastIPAddressInformation address)
    {
        var mask = address.IPv4Mask?.ToString();
        return string.IsNullOrWhiteSpace(mask)
            ? address.Address.ToString()
            : $"{address.Address} / {mask}";
    }

    private static string FormatPhysicalAddress(NetworkInterface adapter)
    {
        try
        {
            var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
            return bytes.Length == 0
                ? "未报告"
                : string.Join("-", bytes.Select(value => value.ToString("X2")));
        }
        catch (NetworkInformationException)
        {
            return "未报告";
        }
    }

    private static long GetSpeed(NetworkInterface adapter)
    {
        try
        {
            return adapter.Speed;
        }
        catch (NetworkInformationException)
        {
            return -1;
        }
    }

    private static string FormatSpeed(long speed)
    {
        if (speed <= 0)
        {
            return "未知";
        }

        var megaBits = speed / 1_000_000d;
        return megaBits >= 1000
            ? $"{megaBits / 1000:0.##} Gbps"
            : $"{megaBits:0.#} Mbps";
    }

    private static string FormatInterfaceType(NetworkInterfaceType type)
    {
        return type switch
        {
            NetworkInterfaceType.Ethernet => "以太网",
            NetworkInterfaceType.Wireless80211 => "无线网络",
            NetworkInterfaceType.Ppp => "PPP/VPN",
            NetworkInterfaceType.Tunnel => "隧道",
            NetworkInterfaceType.FastEthernetFx => "百兆以太网",
            NetworkInterfaceType.FastEthernetT => "百兆以太网",
            NetworkInterfaceType.GigabitEthernet => "千兆以太网",
            _ => type.ToString()
        };
    }

    private static string FormatOperationalStatus(OperationalStatus status)
    {
        return status switch
        {
            OperationalStatus.Up => "在线",
            OperationalStatus.Down => "离线",
            OperationalStatus.Testing => "测试中",
            OperationalStatus.Dormant => "休眠",
            OperationalStatus.NotPresent => "未插入",
            OperationalStatus.LowerLayerDown => "底层离线",
            _ => status.ToString()
        };
    }
}
