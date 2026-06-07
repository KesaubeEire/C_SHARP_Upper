using System.Collections.Concurrent;
using TEST_101.Core;
using TEST_101.Storage;
using TEST_101.Storage.Models;
using TEST_101.Storage.Repositories;

namespace TEST_101.Alarm
{
    /// <summary>
    /// 报警管理器
    ///
    /// 面试考点：
    /// 1. 报警等级如何划分？警告/故障/紧急
    /// 2. 如何防止报警抖动？增加延时确认
    /// 3. 报警如何存储？数据库 + 内存缓存
    /// </summary>
    public class AlarmManager : IDisposable
    {
        private readonly AlarmRepository _repository;
        private readonly ConcurrentDictionary<string, AlarmRule> _rules = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAlarmTime = new();
        private readonly TimeSpan _alarmCooldown = TimeSpan.FromSeconds(5); // 报警冷却时间
        private bool _disposed;

        // 事件：新报警触发
        public event Action<AlarmRecord>? OnAlarmTriggered;

        public AlarmManager(DatabaseManager db)
        {
            _repository = new AlarmRepository(db);

            // 订阅数据更新事件
            EventBus.Instance.Subscribe<DataUpdatedEvent>(OnDataUpdated);
        }

        /// <summary>
        /// 添加报警规则
        /// </summary>
        public void AddRule(AlarmRule rule)
        {
            _rules[rule.RuleId] = rule;
        }

        /// <summary>
        /// 移除报警规则
        /// </summary>
        public void RemoveRule(string ruleId)
        {
            _rules.TryRemove(ruleId, out _);
        }

        /// <summary>
        /// 获取所有规则
        /// </summary>
        public List<AlarmRule> GetRules()
        {
            return _rules.Values.ToList();
        }

        /// <summary>
        /// 获取未确认的报警
        /// </summary>
        public List<AlarmRecord> GetUnconfirmedAlarms()
        {
            return _repository.GetUnconfirmed();
        }

        /// <summary>
        /// 确认报警
        /// </summary>
        public void ConfirmAlarm(long alarmId, string operatorName = "操作员")
        {
            _repository.Confirm(alarmId, operatorName);
        }

        /// <summary>
        /// 复位报警
        /// </summary>
        public void ResetAlarm(long alarmId)
        {
            _repository.Reset(alarmId);
        }

        /// <summary>
        /// 查询报警历史
        /// </summary>
        public List<AlarmRecord> GetAlarmHistory(DateTime start, DateTime end)
        {
            return _repository.GetByTimeRange(start, end);
        }

        /// <summary>
        /// 获取报警统计
        /// </summary>
        public Dictionary<string, int> GetStatistics(DateTime start, DateTime end)
        {
            return _repository.GetStatistics(start, end);
        }

        /// <summary>
        /// 处理数据更新事件
        /// </summary>
        private void OnDataUpdated(DataUpdatedEvent e)
        {
            foreach (var rule in _rules.Values)
            {
                if (rule.DeviceId != e.DeviceId) continue;

                // 查找对应地址的数据
                for (int i = 0; i < e.Values.Length; i++)
                {
                    if (e.StartAddress + i != rule.Address) continue;

                    var value = (double)e.Values[i];

                    // 检查是否触发报警
                    if (!rule.Check(value)) continue;

                    // 检查冷却时间（防止报警抖动）
                    var key = $"{rule.RuleId}_{e.DeviceId}_{rule.Address}";
                    if (_lastAlarmTime.TryGetValue(key, out var lastTime))
                    {
                        if (DateTime.Now - lastTime < _alarmCooldown)
                            continue;
                    }

                    // 触发报警
                    _lastAlarmTime[key] = DateTime.Now;

                    var record = new AlarmRecord
                    {
                        Timestamp = e.Timestamp,
                        RuleName = rule.Name,
                        DeviceId = e.DeviceId,
                        Address = rule.Address,
                        CurrentValue = value,
                        Threshold = rule.Threshold,
                        Level = rule.Level.ToString(),
                        Status = "未确认"
                    };

                    _repository.Insert(record);

                    // 触发事件（用于 UI 更新）
                    OnAlarmTriggered?.Invoke(record);

                    // 发布到事件总线
                    EventBus.Instance.Publish(new AlarmEvent(
                        rule.Name, e.DeviceId, rule.Address,
                        value, rule.Threshold, rule.Level, e.Timestamp));
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
