using TEST_101.Storage.Models;

namespace TEST_101.Report
{
    /// <summary>
    /// 统计计算器
    ///
    /// 计算产量、合格率、平均节拍等生产指标
    /// </summary>
    public class StatisticsCalculator
    {
        /// <summary>
        /// 计算生产统计
        /// </summary>
        public ProductionStatistics Calculate(List<ProductionRecord> records, List<AlarmRecord> alarms)
        {
            var stats = new ProductionStatistics();

            if (records.Count == 0) return stats;

            // 基础统计
            stats.TotalCount = records.Count;
            stats.StartTime = records.First().Timestamp;
            stats.EndTime = records.Last().Timestamp;
            stats.Duration = stats.EndTime - stats.StartTime;

            // 合格率计算（假设地址 100 的值表示合格数量）
            var qualityRecords = records.Where(r => r.Address == 100).ToList();
            if (qualityRecords.Count > 0)
            {
                stats.GoodCount = (int)qualityRecords.Last().ActualValue;
                stats.DefectCount = stats.TotalCount - stats.GoodCount;
                stats.QualifyRate = stats.TotalCount > 0
                    ? (double)stats.GoodCount / stats.TotalCount * 100
                    : 0;
            }

            // 平均节拍计算（假设地址 102 的值表示节拍时间 ms）
            var cycleRecords = records.Where(r => r.Address == 102).ToList();
            if (cycleRecords.Count > 0)
            {
                stats.AvgCycleTime = cycleRecords.Average(r => r.ActualValue);
            }

            // 报警统计
            stats.AlarmCount = alarms.Count;
            stats.WarningCount = alarms.Count(a => a.Level == "Warning");
            stats.FaultCount = alarms.Count(a => a.Level == "Fault");
            stats.EmergencyCount = alarms.Count(a => a.Level == "Emergency");

            return stats;
        }

        /// <summary>
        /// 按小时分组统计
        /// </summary>
        public List<HourlyStatistics> CalculateHourly(List<ProductionRecord> records)
        {
            var hourly = new List<HourlyStatistics>();

            var grouped = records.GroupBy(r => r.Timestamp.Hour);
            foreach (var group in grouped)
            {
                hourly.Add(new HourlyStatistics
                {
                    Hour = group.Key,
                    Count = group.Count(),
                    AvgValue = group.Average(r => r.ActualValue),
                    MinValue = group.Min(r => r.ActualValue),
                    MaxValue = group.Max(r => r.ActualValue)
                });
            }

            return hourly.OrderBy(h => h.Hour).ToList();
        }
    }

    /// <summary>
    /// 生产统计结果
    /// </summary>
    public class ProductionStatistics
    {
        public int TotalCount { get; set; }
        public int GoodCount { get; set; }
        public int DefectCount { get; set; }
        public double QualifyRate { get; set; }
        public double AvgCycleTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int AlarmCount { get; set; }
        public int WarningCount { get; set; }
        public int FaultCount { get; set; }
        public int EmergencyCount { get; set; }
    }

    /// <summary>
    /// 每小时统计
    /// </summary>
    public class HourlyStatistics
    {
        public int Hour { get; set; }
        public int Count { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
    }
}
