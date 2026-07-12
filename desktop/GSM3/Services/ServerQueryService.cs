namespace GSM3.Services;

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

/// <summary>
/// Query result returned by A2S_INFO or Minecraft server ping.
/// </summary>
public class ServerQueryResult
{
    public string ServerName { get; set; } = "";
    public string Map { get; set; } = "";
    public string GameName { get; set; } = "";
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int Bots { get; set; }
    public string Version { get; set; } = "";
    public long PingMs { get; set; }
    public bool IsOnline { get; set; }
    /// <summary>"A2S" or "MC"</summary>
    public string Protocol { get; set; } = "";
}

/// <summary>
/// Saved / recent query entry persisted across sessions.
/// </summary>
public class SavedQuery
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "A2S"; // "A2S" or "MC"
    public string Label { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service for querying game server status via A2S_INFO (Source/Steam) and
/// Minecraft Server List Ping protocols.
/// </summary>
public class ServerQueryService
{
    private readonly string _savedQueriesPath;
    private List<SavedQuery> _savedQueries = new();

    public ServerQueryService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GSM3");
        Directory.CreateDirectory(appData);
        _savedQueriesPath = Path.Combine(appData, "saved_queries.json");
        LoadSavedQueries();
    }

    // ── A2S_INFO ────────────────────────────────────────────────────────────

    /// <summary>
    /// Queries a Source / Steam game server using the A2S_INFO protocol (UDP).
    /// Timeout: 3 seconds.
    /// Reference: https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO
    /// </summary>
    public async Task<ServerQueryResult> QueryA2SInfoAsync(string host, int port)
    {
        var sw = Stopwatch.StartNew();
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 3000;
        udp.Client.SendTimeout = 3000;

        try
        {
            // Build request: 0xFF 0xFF 0xFF 0xFF 0x54 + "Source Engine Query\0"
            var queryString = Encoding.UTF8.GetBytes("Source Engine Query\0");
            var request = new byte[5 + queryString.Length];
            request[0] = 0xFF;
            request[1] = 0xFF;
            request[2] = 0xFF;
            request[3] = 0xFF;
            request[4] = 0x54; // A2S_INFO header
            Array.Copy(queryString, 0, request, 5, queryString.Length);

            using var cts = new CancellationTokenSource(3000);

            await udp.SendAsync(request, request.Length, host, port);

            // Wait for response
            var receiveTask = udp.ReceiveAsync(cts.Token);
            var udpResult = await receiveTask;
            sw.Stop();

            var data = udpResult.Buffer;
            if (data.Length < 6 || data[4] != 0x49) // 'I' = 0x49 for standard response
            {
                // Might be a challenge response (0x41 = 'A')
                if (data.Length >= 9 && data[4] == 0x41)
                {
                    // Re-send with challenge
                    var challengeRequest = new byte[request.Length + 4];
                    Array.Copy(request, challengeRequest, request.Length);
                    Array.Copy(data, 5, challengeRequest, request.Length, 4);

                    await udp.SendAsync(challengeRequest, challengeRequest.Length, host, port);
                    udpResult = await udp.ReceiveAsync(cts.Token);
                    data = udpResult.Buffer;
                    sw.Stop();

                    if (data.Length < 6 || data[4] != 0x49)
                    {
                        return new ServerQueryResult { IsOnline = false, Protocol = "A2S" };
                    }
                }
                else
                {
                    return new ServerQueryResult { IsOnline = false, Protocol = "A2S" };
                }
            }

            return ParseA2SResponse(data, sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            return new ServerQueryResult { IsOnline = false, Protocol = "A2S", PingMs = sw.ElapsedMilliseconds };
        }
    }

    private static ServerQueryResult ParseA2SResponse(byte[] data, long pingMs)
    {
        var result = new ServerQueryResult
        {
            IsOnline = true,
            Protocol = "A2S",
            PingMs = pingMs,
        };

        try
        {
            // Skip 4-byte header (0xFFFFFFFF) + 1 byte header type (0x49)
            int offset = 5;
            // Protocol byte
            offset++; // skip protocol version

            result.ServerName = ReadNullTerminatedString(data, ref offset);
            result.Map = ReadNullTerminatedString(data, ref offset);

            // Game folder
            var gameFolder = ReadNullTerminatedString(data, ref offset);
            result.GameName = ReadNullTerminatedString(data, ref offset);

            if (string.IsNullOrEmpty(result.GameName))
                result.GameName = gameFolder;

            // Steam App ID (2 bytes)
            if (offset + 2 <= data.Length) offset += 2;

            // Player count
            if (offset < data.Length) result.CurrentPlayers = data[offset++];
            // Max players
            if (offset < data.Length) result.MaxPlayers = data[offset++];
            // Bots
            if (offset < data.Length) result.Bots = data[offset++];
        }
        catch
        {
            // Partial parse is acceptable; return what we got.
        }

        return result;
    }

    private static string ReadNullTerminatedString(byte[] data, ref int offset)
    {
        int start = offset;
        while (offset < data.Length && data[offset] != 0)
            offset++;
        var s = Encoding.UTF8.GetString(data, start, offset - start);
        if (offset < data.Length) offset++; // skip null terminator
        return s;
    }

    // ── TCP Ping ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts a plain TCP connection to host:port with a 3-second timeout.
    /// Returns IsOnline=true if the connection succeeds, with PingMs set to the round-trip time.
    /// </summary>
    public async Task<ServerQueryResult> QueryTcpPingAsync(string host, int port)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(3000);
            await tcp.ConnectAsync(host, port, cts.Token);
            sw.Stop();

            return new ServerQueryResult
            {
                IsOnline = true,
                Protocol = "TCP",
                PingMs = sw.ElapsedMilliseconds,
                ServerName = $"TCP {host}:{port}",
            };
        }
        catch
        {
            sw.Stop();
            return new ServerQueryResult
            {
                IsOnline = false,
                Protocol = "TCP",
                PingMs = sw.ElapsedMilliseconds,
                ServerName = $"TCP {host}:{port}",
            };
        }
    }

    // ── Minecraft Server List Ping ──────────────────────────────────────────

    /// <summary>
    /// Queries a Minecraft Java Edition server using the Server List Ping protocol (TCP).
    /// Timeout: 5 seconds.
    /// Reference: https://wiki.vg/Server_List_Ping
    /// </summary>
    public async Task<ServerQueryResult> QueryMinecraftAsync(string host, int port)
    {
        var sw = Stopwatch.StartNew();
        using var tcp = new TcpClient();

        try
        {
            using var cts = new CancellationTokenSource(5000);
            await tcp.ConnectAsync(host, port, cts.Token);

            var stream = tcp.GetStream();
            stream.ReadTimeout = 5000;
            stream.WriteTimeout = 5000;

            // 1. Send Handshake packet (packet ID 0x00)
            //    [VarInt protocolVersion] [String host] [UShort port] [VarInt nextState=1]
            using var handshakeData = new MemoryStream();
            WriteVarInt(handshakeData, -1);                  // protocol version (-1 = let server decide)
            WriteString(handshakeData, host);                // server address
            handshakeData.WriteByte((byte)(port >> 8));      // port big-endian
            handshakeData.WriteByte((byte)(port & 0xFF));
            WriteVarInt(handshakeData, 1);                   // next state = Status

            var handshakePayload = handshakeData.ToArray();
            await WritePacketAsync(stream, 0x00, handshakePayload, cts.Token);

            // 2. Send Status Request packet (packet ID 0x00, no payload)
            await WritePacketAsync(stream, 0x00, Array.Empty<byte>(), cts.Token);

            // 3. Read Status Response
            var (packetId, responsePayload) = await ReadPacketAsync(stream, cts.Token);

            if (packetId != 0x00 || responsePayload.Length == 0)
            {
                return new ServerQueryResult { IsOnline = false, Protocol = "MC" };
            }

            // Parse JSON string from response payload
            int jsonOffset = 0;
            var jsonString = ReadVarIntString(responsePayload, ref jsonOffset);

            sw.Stop();
            return ParseMinecraftResponse(jsonString, sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            return new ServerQueryResult { IsOnline = false, Protocol = "MC", PingMs = sw.ElapsedMilliseconds };
        }
    }

    private static ServerQueryResult ParseMinecraftResponse(string json, long pingMs)
    {
        var result = new ServerQueryResult
        {
            IsOnline = true,
            Protocol = "MC",
            PingMs = pingMs,
        };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // description can be a string or an object with "text"
            if (root.TryGetProperty("description", out var desc))
            {
                if (desc.ValueKind == JsonValueKind.String)
                {
                    result.ServerName = desc.GetString() ?? "";
                }
                else if (desc.ValueKind == JsonValueKind.Object)
                {
                    if (desc.TryGetProperty("text", out var textEl))
                        result.ServerName = textEl.GetString() ?? "";

                    // Some servers put extra text in "extra" array
                    if (desc.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
                    {
                        var sb = new StringBuilder(result.ServerName);
                        foreach (var item in extra.EnumerateArray())
                        {
                            if (item.TryGetProperty("text", out var t))
                                sb.Append(t.GetString() ?? "");
                        }
                        result.ServerName = sb.ToString();
                    }
                }
            }

            if (root.TryGetProperty("players", out var players))
            {
                if (players.TryGetProperty("online", out var online))
                    result.CurrentPlayers = online.GetInt32();
                if (players.TryGetProperty("max", out var max))
                    result.MaxPlayers = max.GetInt32();
            }

            if (root.TryGetProperty("version", out var version))
            {
                if (version.TryGetProperty("name", out var name))
                    result.Version = name.GetString() ?? "";
            }

            result.GameName = "Minecraft";
        }
        catch
        {
            // Partial parse; return what we have.
        }

        return result;
    }

    // ── Minecraft packet helpers ────────────────────────────────────────────

    private static async Task WritePacketAsync(NetworkStream stream, int packetId, byte[] payload, CancellationToken ct)
    {
        using var packetBody = new MemoryStream();
        WriteVarInt(packetBody, packetId);
        packetBody.Write(payload, 0, payload.Length);

        var body = packetBody.ToArray();
        using var packet = new MemoryStream();
        WriteVarInt(packet, body.Length);
        packet.Write(body, 0, body.Length);

        var bytes = packet.ToArray();
        await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<(int packetId, byte[] payload)> ReadPacketAsync(NetworkStream stream, CancellationToken ct)
    {
        int length = await ReadVarIntFromStreamAsync(stream, ct);
        if (length <= 0 || length > 1024 * 1024 * 2) // sanity: max 2 MB
            return (-1, Array.Empty<byte>());

        var data = new byte[length];
        int totalRead = 0;
        while (totalRead < length)
        {
            int read = await stream.ReadAsync(data.AsMemory(totalRead, length - totalRead), ct);
            if (read == 0) throw new IOException("Connection closed");
            totalRead += read;
        }

        int offset = 0;
        int packetId = ReadVarInt(data, ref offset);
        var payload = new byte[data.Length - offset];
        Array.Copy(data, offset, payload, 0, payload.Length);
        return (packetId, payload);
    }

    private static async Task<int> ReadVarIntFromStreamAsync(NetworkStream stream, CancellationToken ct)
    {
        int value = 0;
        int bitOffset = 0;
        byte[] buf = new byte[1];

        while (bitOffset < 35)
        {
            int read = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (read == 0) throw new IOException("Connection closed");

            value |= (buf[0] & 0x7F) << bitOffset;
            if ((buf[0] & 0x80) == 0) return value;
            bitOffset += 7;
        }

        throw new InvalidDataException("VarInt too large");
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var unsigned = (uint)value;
        while (unsigned >= 0x80)
        {
            stream.WriteByte((byte)(unsigned | 0x80));
            unsigned >>= 7;
        }
        stream.WriteByte((byte)unsigned);
    }

    private static int ReadVarInt(byte[] data, ref int offset)
    {
        int value = 0;
        int bitOffset = 0;
        while (bitOffset < 35)
        {
            if (offset >= data.Length) throw new InvalidDataException("VarInt truncated");
            byte b = data[offset++];
            value |= (b & 0x7F) << bitOffset;
            if ((b & 0x80) == 0) return value;
            bitOffset += 7;
        }
        throw new InvalidDataException("VarInt too large");
    }

    private static void WriteString(Stream stream, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ReadVarIntString(byte[] data, ref int offset)
    {
        int length = ReadVarInt(data, ref offset);
        if (length <= 0 || offset + length > data.Length)
            return "";
        var s = Encoding.UTF8.GetString(data, offset, length);
        offset += length;
        return s;
    }

    // ── Saved queries management ────────────────────────────────────────────

    public List<SavedQuery> GetSavedQueries() => new(_savedQueries);

    public void AddSavedQuery(SavedQuery query)
    {
        // Avoid duplicates based on host+port+protocol
        _savedQueries.RemoveAll(q =>
            q.Host.Equals(query.Host, StringComparison.OrdinalIgnoreCase) &&
            q.Port == query.Port &&
            q.Protocol == query.Protocol);

        _savedQueries.Insert(0, query);

        // Keep a maximum of 50 saved queries
        if (_savedQueries.Count > 50)
            _savedQueries = _savedQueries.Take(50).ToList();

        PersistSavedQueries();
    }

    public void RemoveSavedQuery(string id)
    {
        _savedQueries.RemoveAll(q => q.Id == id);
        PersistSavedQueries();
    }

    private void LoadSavedQueries()
    {
        try
        {
            if (File.Exists(_savedQueriesPath))
            {
                var json = File.ReadAllText(_savedQueriesPath);
                _savedQueries = JsonSerializer.Deserialize<List<SavedQuery>>(json) ?? new();
            }
        }
        catch
        {
            _savedQueries = new();
        }
    }

    private void PersistSavedQueries()
    {
        try
        {
            var json = JsonSerializer.Serialize(_savedQueries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_savedQueriesPath, json);
        }
        catch
        {
            // Silently ignore persistence failures.
        }
    }
}
