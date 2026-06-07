using TEST_101.Storage;
using TEST_101.Storage.Repositories;

namespace TEST_101.Report
{
    /// <summary>
    /// 报表生成器
    ///
    /// 整合统计计算和 Excel 导出功能
    /// </summary>
    public class ReportGenerator : IDisposable
    {
        private readonly ProductionRepository _productionRepo;
        private readonly AlarmRepository _alarmRepo;
        private readonly StatisticsCalculator _calculator;
        private readonly ExcelExporter _exporter;
        private bool _disposed;

        public ReportGenerator(DatabaseManager db)
        {
            _productionRepo = new ProductionRepository(db);
            _alarmRepo = new AlarmRepository(db);
            _calculator = new StatisticsCalculator();
            _exporter = new ExcelExporter();
        }

        /// <summary>
        /// 生成日报
        /// </summary>
        public void GenerateDailyReport(DateTime date, string outputPath)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            // 获取数据
            var productionData = _productionRepo.GetByTimeRange(start, end);
            var alarmData = _alarmRepo.GetByTimeRange(start, end);

            // 计算统计
            var stats = _calculator.Calculate(productionData, alarmData);
            var hourlyStats = _calculator.CalculateHourly(productionData);

            // 导出 Excel
            _exporter.ExportDailyReport(outputPath, date, stats, hourlyStats);
        }

        /// <summary>
        /// 导出报警记录
        /// </summary>
        public void ExportAlarmHistory(DateTime start, DateTime end, string outputPath)
        {
            var alarms = _alarmRepo.GetByTimeRange(start, end);
            _exporter.ExportAlarmHistory(outputPath, alarms);
        }

        /// <summary>
        /// 获取生产统计（用于 UI 显示）
        /// </summary>
        public ProductionStatistics GetStatistics(DateTime start, DateTime end)
        {
            var productionData = _productionRepo.GetByTimeRange(start, end);
            var alarmData = _alarmRepo.GetByTimeRange(start, end);
            return _calculator.Calculate(productionData, alarmData);
        }

        /// <summary>
        /// 获取每小时统计（用于 UI 图表）
        /// </summary>
        public List<HourlyStatistics> GetHourlyStatistics(DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);
            var productionData = _productionRepo.GetByTimeRange(start, end);
            return _calculator.CalculateHourly(productionData);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
