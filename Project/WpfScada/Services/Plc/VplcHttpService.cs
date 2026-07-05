using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfScada.Services.Plc;

/// <summary>
/// vPLC HTTP API 客户端。
/// 通过 vplc 的 REST API (http://localhost:1201/api/vplc) 读写数据，
/// 不影响 S7Service 连接真实 PLC 的逻辑。
/// </summary>
public sealed class VplcHttpService
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:1201"), Timeout = TimeSpan.FromSeconds(3) };
    private VplcSnapshot? _lastSnapshot;

    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>测试连接 vplc 是否可达。</summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/vplc");
            if (resp.IsSuccessStatusCode)
            {
                await RefreshSnapshotAsync();
                IsConnected = _lastSnapshot != null;
                LastError = _lastSnapshot != null ? null : "vPLC 返回数据格式异常";
                return IsConnected;
            }
            IsConnected = false;
            LastError = $"HTTP {(int)resp.StatusCode}";
            return false;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
            return false;
        }
    }

    public void Disconnect()
    {
        IsConnected = false;
        _lastSnapshot = null;
    }

    /// <summary>获取 vplc 最新快照。</summary>
    public async Task<VplcSnapshot?> GetSnapshotAsync()
    {
        await RefreshSnapshotAsync();
        return _lastSnapshot;
    }

    private async Task RefreshSnapshotAsync()
    {
        try
        {
            var snap = await _http.GetFromJsonAsync<VplcSnapshot>("/api/vplc");
            _lastSnapshot = snap;
            IsConnected = snap != null;
        }
        catch
        {
            IsConnected = false;
        }
    }

    /// <summary>写入一个地址的值。</summary>
    public async Task<bool> WriteAsync(string area, int dbNumber, int offset, double value, string type = "real", int? bit = null)
    {
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["area"] = area,
                ["dbNumber"] = dbNumber,
                ["offset"] = offset,
                ["value"] = value,
                ["type"] = type,
            };
            if (bit.HasValue) payload["bit"] = bit.Value;

            var resp = await _http.PostAsJsonAsync("/api/vplc/write", payload);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读取 DB 的一个 REAL 值。</summary>
    public float? ReadReal(int dbNumber, int offset)
    {
        if (_lastSnapshot?.DB?.TryGetValue($"DB{dbNumber}", out var db) == true && offset + 4 <= db.Length)
        {
            var buf = new byte[4];
            Array.Copy(db, offset, buf, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buf);
            return BitConverter.ToSingle(buf);
        }
        return null;
    }

    /// <summary>读取 I 或 Q 或 M 区的一个字节。</summary>
    public byte? ReadByte(string area, int offset)
    {
        var snap = _lastSnapshot;
        if (snap == null) return null;
        var arr = area.ToUpperInvariant() switch
        {
            "I" => snap.GetPE(),
            "Q" => snap.GetPA(),
            "M" => snap.GetMK(),
            _ => null,
        };
        if (arr != null && offset < arr.Length)
            return arr[offset];
        return null;
    }
}

public class VplcSnapshot
{
    [JsonPropertyName("DB")]
    public Dictionary<string, byte[]>? DB { get; set; }

    [JsonPropertyName("PE")]
    public JsonElement? PE { get; set; }

    [JsonPropertyName("PA")]
    public JsonElement? PA { get; set; }

    [JsonPropertyName("MK")]
    public JsonElement? MK { get; set; }

    /// <summary>将 JsonElement 转为 byte[]，兼容 int[] 格式。</summary>
    private static byte[]? ToByteArray(JsonElement? el)
    {
        if (el == null) return null;
        try { return el.Value.Deserialize<byte[]>(); }
        catch { }
        try
        {
            var arr = el.Value.Deserialize<int[]>();
            return arr?.Select(i => (byte)i).ToArray();
        }
        catch { return null; }
    }

    public byte[]? GetPE() => ToByteArray(PE);
    public byte[]? GetPA() => ToByteArray(PA);
    public byte[]? GetMK() => ToByteArray(MK);
}
