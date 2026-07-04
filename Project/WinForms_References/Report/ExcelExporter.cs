using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using TEST_101.Storage.Models;

namespace TEST_101.Report
{
    /// <summary>
    /// Excel 导出器
    ///
    /// 面试考点：
    /// 1. EPPlus 是什么？.NET 的 Excel 操作库
    /// 2. 如何处理大数据量？分批写入 + 虚拟模式
    /// 3. 如何设置样式？使用 ExcelRange.Style
    /// </summary>
    public class ExcelExporter
    {
        static ExcelExporter()
        {
            // 设置 EPPlus 许可证上下文（非商业用途）
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// 导出生产日报
        /// </summary>
        public void ExportDailyReport(string filePath, DateTime date,
            ProductionStatistics stats, List<HourlyStatistics> hourlyStats)
        {
            using var package = new ExcelPackage();

            // 创建工作表
            var ws = package.Workbook.Worksheets.Add("生产日报");

            // 标题
            ws.Cells["A1"].Value = $"生产日报 - {date:yyyy-MM-dd}";
            ws.Cells["A1:F1"].Merge = true;
            ws.Cells["A1"].Style.Font.Size = 16;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // 统计概览
            ws.Cells["A3"].Value = "统计概览";
            ws.Cells["A3"].Style.Font.Bold = true;
            ws.Cells["A3"].Style.Font.Size = 12;

            var overviewData = new[]
            {
                new[] { "总产量", stats.TotalCount.ToString(), "合格率", $"{stats.QualifyRate:F1}%" },
                new[] { "合格数", stats.GoodCount.ToString(), "不合格数", stats.DefectCount.ToString() },
                new[] { "平均节拍", $"{stats.AvgCycleTime:F1} ms", "报警次数", stats.AlarmCount.ToString() },
                new[] { "工作时长", $"{stats.Duration.TotalHours:F1} 小时", "开始时间", stats.StartTime.ToString("HH:mm:ss") }
            };

            for (int i = 0; i < overviewData.Length; i++)
            {
                ws.Cells[$"A{4 + i}"].Value = overviewData[i][0];
                ws.Cells[$"B{4 + i}"].Value = overviewData[i][1];
                ws.Cells[$"D{4 + i}"].Value = overviewData[i][2];
                ws.Cells[$"E{4 + i}"].Value = overviewData[i][3];

                ws.Cells[$"A{4 + i}"].Style.Font.Bold = true;
                ws.Cells[$"D{4 + i}"].Style.Font.Bold = true;
            }

            // 详细数据表格
            ws.Cells["A9"].Value = "每小时统计";
            ws.Cells["A9"].Style.Font.Bold = true;
            ws.Cells["A9"].Style.Font.Size = 12;

            // 表头
            var headers = new[] { "时间", "产量", "平均值", "最小值", "最大值" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[10, i + 1].Value = headers[i];
                ws.Cells[10, i + 1].Style.Font.Bold = true;
                ws.Cells[10, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[10, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                ws.Cells[10, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // 数据行
            for (int i = 0; i < hourlyStats.Count; i++)
            {
                var row = 11 + i;
                var stat = hourlyStats[i];

                ws.Cells[row, 1].Value = $"{stat.Hour:D2}:00-{stat.Hour:D2}:59";
                ws.Cells[row, 2].Value = stat.Count;
                ws.Cells[row, 3].Value = stat.AvgValue;
                ws.Cells[row, 4].Value = stat.MinValue;
                ws.Cells[row, 5].Value = stat.MaxValue;

                // 设置边框
                for (int j = 1; j <= 5; j++)
                {
                    ws.Cells[row, j].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                // 数字格式
                ws.Cells[row, 3].Style.Numberformat.Format = "0.00";
                ws.Cells[row, 4].Style.Numberformat.Format = "0.00";
                ws.Cells[row, 5].Style.Numberformat.Format = "0.00";
            }

            // 调整列宽
            ws.Cells.AutoFitColumns();

            // 保存文件
            package.SaveAs(new FileInfo(filePath));
        }

        /// <summary>
        /// 导出报警记录
        /// </summary>
        public void ExportAlarmHistory(string filePath, List<AlarmRecord> alarms)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("报警记录");

            // 标题
            ws.Cells["A1"].Value = "报警历史记录";
            ws.Cells["A1:F1"].Merge = true;
            ws.Cells["A1"].Style.Font.Size = 16;
            ws.Cells["A1"].Style.Font.Bold = true;

            // 表头
            var headers = new[] { "时间", "规则", "设备", "当前值", "阈值", "等级", "状态" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[3, i + 1].Value = headers[i];
                ws.Cells[3, i + 1].Style.Font.Bold = true;
                ws.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
            }

            // 数据
            for (int i = 0; i < alarms.Count; i++)
            {
                var row = 4 + i;
                var alarm = alarms[i];

                ws.Cells[row, 1].Value = alarm.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cells[row, 2].Value = alarm.RuleName;
                ws.Cells[row, 3].Value = alarm.DeviceId;
                ws.Cells[row, 4].Value = alarm.CurrentValue;
                ws.Cells[row, 5].Value = alarm.Threshold;
                ws.Cells[row, 6].Value = alarm.Level;
                ws.Cells[row, 7].Value = alarm.Status;

                // 根据等级设置颜色
                var levelColor = alarm.Level switch
                {
                    "Warning" => Color.Yellow,
                    "Fault" => Color.Orange,
                    "Emergency" => Color.Red,
                    _ => Color.White
                };
                ws.Cells[row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(levelColor);
            }

            ws.Cells.AutoFitColumns();
            package.SaveAs(new FileInfo(filePath));
        }
    }

}
