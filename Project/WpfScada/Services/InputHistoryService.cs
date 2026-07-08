using System.Text.Json;
using Microsoft.Extensions.Logging;
using WpfScada.Controls.Input;

namespace WpfScada.Services;

public sealed class InputHistoryService : IInputHistoryService
{
    private const int MaxPerKey = 20;
    private readonly string _filePath;
    private readonly Dictionary<string, List<string>> _data;
    private readonly object _lock = new();
    private readonly ILogger<InputHistoryService> _logger;

    public InputHistoryService(ILogger<InputHistoryService> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfScada");
        _filePath = Path.Combine(dir, "history.json");
        _data = Load();
    }

    public List<string> GetHistory(string key)
    {
        lock (_lock)
        {
            return _data.TryGetValue(key, out var list) ? [.. list] : [];
        }
    }

    public void AddEntry(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        lock (_lock)
        {
            if (!_data.TryGetValue(key, out var list))
            {
                list = [];
                _data[key] = list;
            }

            list.Remove(value);
            list.Insert(0, value);

            while (list.Count > MaxPerKey)
                list.RemoveAt(list.Count - 1);

            Save();
        }
    }

    public void RemoveEntry(string key, string value)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(key, out var list))
            {
                list.Remove(value);
                if (list.Count == 0) _data.Remove(key);
                Save();
            }
        }
    }

    private Dictionary<string, List<string>> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? [];
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "加载历史失败"); }
        return [];
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "保存历史失败"); }
    }
}
