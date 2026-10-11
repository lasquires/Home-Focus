using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HomeFocus.Shared;

namespace HomeFocus.App;

internal sealed class AgentClient
{
    public async Task<AgentResponse> SendAsync(AgentRequest request, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 48191, ct);
        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 8192, true) { AutoFlush = true };
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonDefaults.Options));
        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(line)) throw new IOException("HomeFocus Agent did not answer.");
        return JsonSerializer.Deserialize<AgentResponse>(line, JsonDefaults.Options) ?? throw new IOException("Invalid agent response.");
    }
}
