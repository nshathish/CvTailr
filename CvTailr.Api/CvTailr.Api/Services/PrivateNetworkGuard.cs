using System.Net;
using System.Net.Sockets;

namespace CvTailr.Api.Services;

/// <summary>
/// SSRF guard: decides whether a resolved IP address is safe to connect to from
/// <see cref="JdUrlFetcher"/>. Blocks loopback, private, link-local (incl. the 169.254.169.254
/// cloud metadata address), carrier-grade NAT, unspecified, and multicast ranges, for both IPv4
/// and IPv6 (including IPv4-mapped IPv6 forms of the above).
/// </summary>
internal static class PrivateNetworkGuard
{
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return false;

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(address),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(address),
            _ => false
        };
    }

    private static bool IsPublicIPv4(IPAddress address)
    {
        var b = address.GetAddressBytes();

        if (b[0] == 10) return false; // 10.0.0.0/8
        if (b[0] == 172 && b[1] is >= 16 and <= 31) return false; // 172.16.0.0/12
        if (b[0] == 192 && b[1] == 168) return false; // 192.168.0.0/16
        if (b[0] == 169 && b[1] == 254) return false; // 169.254.0.0/16 (incl. 169.254.169.254)
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return false; // 100.64.0.0/10 (CGNAT)
        if (b[0] is >= 224 and <= 239) return false; // 224.0.0.0/4 (multicast)

        return true;
    }

    private static bool IsPublicIPv6(IPAddress address)
    {
        if (address.Equals(IPAddress.IPv6Loopback)) return false;
        if (address.IsIPv6LinkLocal) return false; // fe80::/10
        if (address.IsIPv6Multicast) return false;

        var b = address.GetAddressBytes();
        if ((b[0] & 0xFE) == 0xFC) return false; // fc00::/7 (unique local)

        return true;
    }
}
