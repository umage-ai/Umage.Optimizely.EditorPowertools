using System.Net;
using System.Net.Sockets;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

/// <summary>
/// Guards server-side fetches of user-supplied URLs against SSRF (internal services,
/// cloud metadata). <see cref="CreatePinnedHandler"/> validates the destination address
/// inside the connect callback — on the same DNS answer the socket connects to — which
/// closes the validate-then-fetch window (DNS rebinding) that a separate pre-check
/// leaves open. Redirects stay disabled so a validated URL cannot bounce elsewhere.
/// </summary>
internal static class SsrfProtection
{
    internal static SocketsHttpHandler CreatePinnedHandler() => new()
    {
        AllowAutoRedirect = false,
        ConnectCallback = static async (context, ct) =>
        {
            var host = context.DnsEndPoint.Host;
            IPAddress[] addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);

            var allowed = addresses.Where(IsPublicAddress).ToArray();
            if (allowed.Length == 0)
                throw new HttpRequestException("Destination address is not allowed.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    internal static bool IsPublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                0 => false,                                   // 0.0.0.0/8
                10 => false,                                  // 10.0.0.0/8 (private)
                100 when b[1] is >= 64 and <= 127 => false,   // 100.64.0.0/10 (CGNAT)
                169 when b[1] == 254 => false,                // 169.254.0.0/16 (link-local + metadata)
                172 when b[1] is >= 16 and <= 31 => false,    // 172.16.0.0/12 (private)
                192 when b[1] == 168 => false,                // 192.168.0.0/16 (private)
                _ => true
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !ip.IsIPv6LinkLocal
                && !ip.IsIPv6SiteLocal
                && !ip.IsIPv6UniqueLocal
                && !ip.Equals(IPAddress.IPv6Any);
        }

        return false;
    }
}
