using ScottPlot;
using ScottPlot.WinForms;
using System.Collections.Concurrent;
using TEST_101.Core;
using Color = System.Drawing.Color;

namespace TEST_101.Chart
{
    /// <summary>
    /// 实时曲线控件
    ///
    /// 面试考点：
    /// 1. ScottPlot 是什么？高性能 .NET 绘图库
    /// 2. 如何实现实时滚动？定时更新数据 + 自动调整轴范围
    /// 3. 多条曲线如何管理？每个通道独立数据队列
    /// </summary>
    public class RealtimeChartControl : UserControl
    {
        private readonly FormsPlot _formsPlot;
        private readonly ConcurrentDictionary<int, ChannelData> _channels = new();
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private int _maxPoints = 500;
        private bool _isPaused;

        public RealtimeChartControl()
        {
            // 初始化 ScottPlot 控件
            _formsPlot = new FormsPlot
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_formsPlot);

            // 配置图表
            _formsPlot.Plot.Title("实时数据曲线");
            _formsPlot.Plot.XLabel("时间");
            _formsPlot.Plot.YLabel("数值");

            // 刷新定时器
            _refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 100 // 100ms 刷新一次
            };
            _refreshTimer.Tick += (s, e) => RefreshChart();
            _refreshTimer.Start();
        }

        /// <summary>
        /// 添加通道
        /// </summary>
        public void AddChannel(ChannelConfig config)
        {
            var channelData = new ChannelData
            {
                Config = config,
                TimeData = new ConcurrentQueue<double>(),
                ValueData = new ConcurrentQueue<double>()
            };

            _channels[config.ChannelId] = channelData;
        }

        /// <summary>
        /// 移除通道
        /// </summary>
        public void RemoveChannel(int channelId)
        {
            _channels.TryRemove(channelId, out _);
        }

        /// <summary>
        /// 添加数据点
        /// </summary>
        public void AddDataPoint(int channelId, DateTime timestamp, double value)
        {
            if (_channels.TryGetValue(channelId, out var data))
            {
                // ScottPlot 使用 OADate 作为时间轴
                data.TimeData.Enqueue(timestamp.ToOADate());
                data.ValueData.Enqueue(value);

                // 限制数据点数量
                while (data.TimeData.Count > _maxPoints)
                {
                    data.TimeData.TryDequeue(out _);
                    data.ValueData.TryDequeue(out _);
                }
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearAll()
        {
            foreach (var channel in _channels.Values)
            {
                while (channel.TimeData.TryDequeue(out _)) { }
                while (channel.ValueData.TryDequeue(out _)) { }
            }
        }

        /// <summary>
        /// 暂停/恢复刷新
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_isPaused)
                _refreshTimer.Stop();
            else
                _refreshTimer.Start();
        }

        /// <summary>
        /// 截图保存
        /// </summary>
        public void SaveImage(string filePath, int width = 1920, int height = 1080)
        {
            _formsPlot.Plot.Save(filePath, width, height);
        }

        /// <summary>
        /// 导出数据为 CSV
        /// </summary>
        public void ExportCsv(string filePath)
        {
            using var writer = new StreamWriter(filePath);

            // 写入表头
            var headers = _channels.Values.Select(c => c.Config.Name);
            writer.WriteLine("时间," + string.Join(",", headers));

            // 写入数据（按时间戳对齐）
            var allTimes = _channels.Values
                .SelectMany(c => c.TimeData)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            foreach (var time in allTimes)
            {
                var line = DateTime.FromOADate(time).ToString("HH:mm:ss.fff");
                foreach (var channel in _channels.Values)
                {
                    var timeArray = channel.TimeData.ToArray();
                    var valueArray = channel.ValueData.ToArray();
                    var index = Array.IndexOf(timeArray, time);
                    line += "," + (index >= 0 ? valueArray[index].ToString("F2") : "");
                }
                writer.WriteLine(line);
            }
        }

        /// <summary>
        /// 刷新图表
        /// </summary>
        private void RefreshChart()
        {
            if (_isPaused) return;

            // 清除旧的散点图
            _formsPlot.Plot.Clear();

            // 重新绘制所有通道
            foreach (var channel in _channels.Values)
            {
                var timeArray = channel.TimeData.ToArray();
                var valueArray = channel.ValueData.ToArray();

                if (timeArray.Length > 0)
                {
                    var scatter = _formsPlot.Plot.Add.Scatter(timeArray, valueArray);
                    scatter.LineWidth = 2;
                    scatter.MarkerSize = 0;
                    scatter.Color = new ScottPlot.Color(
                        Color.FromArgb(channel.Config.Color).R,
                        Color.FromArgb(channel.Config.Color).G,
                        Color.FromArgb(channel.Config.Color).B);
                }
            }

            // 自动调整轴范围
            _formsPlot.Plot.Axes.AutoScale();
            _formsPlot.Refresh();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _formsPlot?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 通道内部数据
        /// </summary>
        private class ChannelData
        {
            public required ChannelConfig Config { get; init; }
            public required ConcurrentQueue<double> TimeData { get; init; }
            public required ConcurrentQueue<double> ValueData { get; init; }
        }
    }
}
