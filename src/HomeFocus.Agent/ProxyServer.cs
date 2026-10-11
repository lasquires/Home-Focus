using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HomeFocus.Agent;

internal sealed class ProxyServer
{
    readonly PolicyEngine policy;
    readonly TcpListener listener = new(IPAddress.Loopback, HomeFocus.Shared.HomeFocusPaths.ProxyPort);
    public ProxyServer(PolicyEngine policy) => this.policy = policy;

    public async Task RunAsync(CancellationToken ct)
    {
        listener.Start(100);
        Log.Write($"Proxy listening on 127.0.0.1:{HomeFocus.Shared.HomeFocusPaths.ProxyPort}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleAsync(client, ct), ct);
            }
        }
        finally { listener.Stop(); }
    }

    async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.NoDelay = true;
            using var incoming = client.GetStream();
            string header;
            try { header = await ReadHeadersAsync(incoming, ct); }
            catch { return; }
            if (string.IsNullOrWhiteSpace(header)) return;

            var lines = header.Split("\r\n", StringSplitOptions.None);
            var requestLine = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length < 2) return;

            if (requestLine[0].Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                var target = requestLine[1];
                var colon = target.LastIndexOf(':');
                var host = colon > 0 ? target[..colon] : target;
                var port = colon > 0 && int.TryParse(target[(colon + 1)..], out var p) ? p : 443;
                if ((port != 443 && port != 80) || !policy.HostAllowed(host))
                {
                    await WriteTextAsync(incoming, "HTTP/1.1 403 Forbidden\r\nConnection: close\r\nContent-Length: 0\r\n\r\n", ct);
                    return;
                }

                try
                {
                    using var upstream = new TcpClient();
                    await upstream.ConnectAsync(host, port, ct);
                    using var outgoing = upstream.GetStream();
                    await WriteTextAsync(incoming, "HTTP/1.1 200 Connection Established\r\nProxy-Agent: HomeFocus/1.0\r\n\r\n", ct);
                    await RelayBothWays(incoming, outgoing, ct);
                }
                catch { await SafeCloseAsync(incoming, ct); }
                return;
            }

            if (!Uri.TryCreate(requestLine[1], UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            {
                await WriteTextAsync(incoming, "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 0\r\n\r\n", ct);
                return;
            }
            if (!policy.HostAllowed(uri.Host))
            {
                await WriteTextAsync(incoming, "HTTP/1.1 403 Forbidden\r\nConnection: close\r\nContent-Length: 0\r\n\r\n", ct);
                return;
            }

            var portHttp = uri.IsDefaultPort ? (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80) : uri.Port;
            if (portHttp != 80 && portHttp != 443)
            {
                await WriteTextAsync(incoming, "HTTP/1.1 403 Forbidden\r\nConnection: close\r\nContent-Length: 0\r\n\r\n", ct);
                return;
            }
            try
            {
                using var upstream = new TcpClient();
                await upstream.ConnectAsync(uri.Host, portHttp, ct);
                using var outgoing = upstream.GetStream();
                var path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
                lines[0] = $"{requestLine[0]} {path} {requestLine[2]}";
                var rewritten = string.Join("\r\n", lines) + "\r\n\r\n";
                await WriteTextAsync(outgoing, rewritten, ct);
                await RelayBothWays(incoming, outgoing, ct);
            }
            catch { }
        }
    }

    static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        var data = new List<byte>(4096);
        var one = new byte[1];
        while (data.Count < 64 * 1024)
        {
            var n = await stream.ReadAsync(one, ct);
            if (n == 0) break;
            data.Add(one[0]);
            var c = data.Count;
            if (c >= 4 && data[c - 4] == 13 && data[c - 3] == 10 && data[c - 2] == 13 && data[c - 1] == 10)
                break;
        }
        return Encoding.ASCII.GetString(data.ToArray()).TrimEnd('\r', '\n');
    }

    static async Task WriteTextAsync(NetworkStream s, string text, CancellationToken ct)
        => await s.WriteAsync(Encoding.ASCII.GetBytes(text), ct);

    static async Task RelayBothWays(NetworkStream a, NetworkStream b, CancellationToken ct)
    {
        var t1 = Pump(a, b, ct);
        var t2 = Pump(b, a, ct);
        await Task.WhenAny(t1, t2);
    }

    static async Task Pump(Stream input, Stream output, CancellationToken ct)
    {
        try { await input.CopyToAsync(output, 64 * 1024, ct); }
        catch { }
        try { await output.FlushAsync(ct); } catch { }
    }

    static async Task SafeCloseAsync(Stream stream, CancellationToken ct)
    {
        try { await WriteTextAsync((NetworkStream)stream, "HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 0\r\n\r\n", ct); } catch { }
    }
}
