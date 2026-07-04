using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TEST_101
{
    /// <summary>
    /// 输入框历史记录管理器 — Add / Get / Remove / Clear，自动持久化到 JSON。
    /// 线程安全（lock 保护）。
    /// </summary>
    public class InputHistoryManager
    {
        private const int MaxPerField = 30;

        private readonly string _filePath;
        private readonly Dictionary<string, List<string>> _history;
        private readonly object _lock = new();

        public InputHistoryManager(string? customPath = null)
        {
            _filePath = customPath
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TEST_101", "history.json");

            _history = Load();
        }

        // ========== 读取 ==========

        public List<string> GetHistory(string fieldKey)
        {
            lock (_lock)
            {
                return _history.TryGetValue(fieldKey, out var list)
                    ? new List<string>(list)
                    : new List<string>();
            }
        }

        // ========== 添加 ==========

        public void Add(string fieldKey, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            lock (_lock)
            {
                if (!_history.TryGetValue(fieldKey, out var list))
                {
                    list = new List<string>();
                    _history[fieldKey] = list;
                }

                // 去重：如果已有，移到最新
                list.Remove(value);
                list.Insert(0, value);

                // 截断
                while (list.Count > MaxPerField)
                    list.RemoveAt(list.Count - 1);

                Save();
            }
        }

        // ========== 删除 ==========

        public void Remove(string fieldKey, string value)
        {
            lock (_lock)
            {
                if (_history.TryGetValue(fieldKey, out var list))
                {
                    list.Remove(value);
                    if (list.Count == 0)
                        _history.Remove(fieldKey);
                    Save();
                }
            }
        }

        // ========== 清空 ==========

        public void ClearField(string fieldKey)
        {
            lock (_lock)
            {
                _history.Remove(fieldKey);
                Save();
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _history.Clear();
                Save();
            }
        }

        // ========== 持久化 ==========

        private Dictionary<string, List<string>> Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (data != null)
                    {
                        // 截断每字段
                        foreach (var key in data.Keys.ToList())
                        {
                            while (data[key].Count > MaxPerField)
                                data[key].RemoveAt(data[key].Count - 1);
                        }
                        return data;
                    }
                }
            }
            catch { /* 文件损坏或无权限 → 从空开始 */ }

            return new Dictionary<string, List<string>>();
        }

        private void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(_history,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { /* 静默失败，不影响主流程 */ }
        }
    }
}
