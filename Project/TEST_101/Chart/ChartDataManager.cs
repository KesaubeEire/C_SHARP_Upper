using TEST_101.Core;

namespace TEST_101.Chart
{
    /// <summary>
    /// 曲线数据管理器
    ///
    /// 负责：
    /// 1. 接收 EventBus 的数据更新事件
    /// 2. 转换数据格式
    /// 3. 分发到曲线控件
    /// </summary>
    public class ChartDataManager : IDisposable
    {
        private readonly RealtimeChartControl _chart;
        private readonly List<ChannelConfig> _channels = new();
        private bool _disposed;

        public ChartDataManager(RealtimeChartControl chart)
        {
            _chart = chart;

            // 订阅数据更新事件
            EventBus.Instance.Subscribe<DataUpdatedEvent>(OnDataUpdated);
        }

        /// <summary>
        /// 配置通道
        /// </summary>
        public void ConfigureChannels(List<ChannelConfig> channels)
        {
            // 清除旧通道
            foreach (var oldChannel in _channels)
            {
                _chart.RemoveChannel(oldChannel.ChannelId);
            }
            _channels.Clear();

            // 添加新通道
            foreach (var channel in channels.Where(c => c.IsEnabled))
            {
                _channels.Add(channel);
                _chart.AddChannel(channel);
            }
        }

        /// <summary>
        /// 添加单个通道
        /// </summary>
        public void AddChannel(ChannelConfig channel)
        {
            _channels.Add(channel);
            _chart.AddChannel(channel);
        }

        /// <summary>
        /// 处理数据更新事件
        /// </summary>
        private void OnDataUpdated(DataUpdatedEvent e)
        {
            foreach (var channel in _channels)
            {
                if (channel.DeviceId == e.DeviceId)
                {
                    // 查找对应地址的数据
                    for (int i = 0; i < e.Values.Length; i++)
                    {
                        if (e.StartAddress + i == channel.Address)
                        {
                            var actualValue = channel.ConvertValue(e.Values[i]);
                            _chart.AddDataPoint(channel.ChannelId, e.Timestamp, actualValue);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public List<ChannelConfig> GetChannels()
        {
            return _channels.ToList();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
