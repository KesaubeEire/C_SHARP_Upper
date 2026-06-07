using System;
using System.Collections.Concurrent;

namespace TEST_101.Core
{
    /// <summary>
    /// 事件总线 —— 模块间解耦通信的核心
    ///
    /// 设计模式：观察者模式（Pub/Sub）
    /// 面试考点：为什么用事件总线？解耦、异步、线程安全
    /// </summary>
    public sealed class EventBus
    {
        // 单例模式（懒加载，线程安全）
        private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
        public static EventBus Instance => _instance.Value;

        // 线程安全的事件订阅表
        // Key: 事件类型, Value: 处理程序列表
        private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _handlers = new();

        // 私有构造函数
        private EventBus() { }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件数据类型</typeparam>
        /// <param name="handler">事件处理程序</param>
        public void Subscribe<T>(Action<T> handler)
        {
            var handlers = _handlers.GetOrAdd(typeof(T), _ => new ConcurrentBag<Delegate>());
            handlers.Add(handler);
        }

        /// <summary>
        /// 取消订阅
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                // ConcurrentBag 不支持直接移除，这里简化处理
                // 实际项目中可以用 ConcurrentDictionary + HashSet 替代
            }
        }

        /// <summary>
        /// 发布事件（同步）
        /// </summary>
        public void Publish<T>(T eventData)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        ((Action<T>)handler)?.Invoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        // 记录异常，不影响其他处理器
                        Console.WriteLine($"[EventBus] 处理事件异常: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 发布事件（异步）
        /// </summary>
        public void PublishAsync<T>(T eventData)
        {
            Task.Run(() => Publish(eventData));
        }

        /// <summary>
        /// 清除所有订阅
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }
    }

    // ========== 预定义事件类型 ==========

    /// <summary>
    /// 数据更新事件 —— 当 Modbus 读取到新数据时触发
    /// </summary>
    public record DataUpdatedEvent(
        string DeviceId,
        ushort StartAddress,
        ushort[] Values,
        DateTime Timestamp
    );

    /// <summary>
    /// 报警事件 —— 当检测到报警条件时触发
    /// </summary>
    public record AlarmEvent(
        string RuleName,
        string DeviceId,
        ushort Address,
        double CurrentValue,
        double Threshold,
        AlarmLevel Level,
        DateTime Timestamp
    );

    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public record ConnectionChangedEvent(
        string DeviceId,
        bool IsConnected,
        string StatusMessage
    );

    /// <summary>
    /// 报警等级枚举
    /// </summary>
    public enum AlarmLevel
    {
        Warning,    // 警告
        Fault,      // 故障
        Emergency   // 紧急
    }
}
