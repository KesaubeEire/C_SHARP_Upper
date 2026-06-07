using System.Data;
using TEST_101.Storage.Models;

namespace TEST_101.Storage.Repositories
{
    /// <summary>
    /// 报警记录仓储
    /// </summary>
    public class AlarmRepository
    {
        private readonly DatabaseManager _db;

        public AlarmRepository(DatabaseManager db)
        {
            _db = db;
        }

        /// <summary>
        /// 插入报警记录
        /// </summary>
        public void Insert(AlarmRecord record)
        {
            const string sql = @"
                INSERT INTO alarm_history (timestamp, rule_name, device_id, address, current_value, threshold, level, status)
                VALUES (@timestamp, @rule_name, @device_id, @address, @current_value, @threshold, @level, @status)";

            _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@timestamp"] = record.Timestamp.ToString("o"),
                ["@rule_name"] = record.RuleName,
                ["@device_id"] = record.DeviceId,
                ["@address"] = record.Address,
                ["@current_value"] = record.CurrentValue,
                ["@threshold"] = record.Threshold,
                ["@level"] = record.Level,
                ["@status"] = record.Status
            });
        }

        /// <summary>
        /// 确认报警
        /// </summary>
        public void Confirm(long id, string operatorName = "操作员")
        {
            const string sql = @"
                UPDATE alarm_history
                SET status = '已确认', confirmed_at = @confirmed_at, confirmed_by = @confirmed_by
                WHERE id = @id";

            _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@id"] = id,
                ["@confirmed_at"] = DateTime.Now.ToString("o"),
                ["@confirmed_by"] = operatorName
            });
        }

        /// <summary>
        /// 复位报警
        /// </summary>
        public void Reset(long id)
        {
            const string sql = "UPDATE alarm_history SET status = '已复位' WHERE id = @id";
            _db.ExecuteNonQuery(sql, new Dictionary<string, object> { ["@id"] = id });
        }

        /// <summary>
        /// 查询未确认的报警
        /// </summary>
        public List<AlarmRecord> GetUnconfirmed()
        {
            var dt = _db.ExecuteQuery(
                "SELECT * FROM alarm_history WHERE status = '未确认' ORDER BY timestamp DESC");
            return DataTableToList(dt);
        }

        /// <summary>
        /// 按时间范围查询报警
        /// </summary>
        public List<AlarmRecord> GetByTimeRange(DateTime start, DateTime end)
        {
            var dt = _db.ExecuteQuery(
                "SELECT * FROM alarm_history WHERE timestamp BETWEEN @start AND @end ORDER BY timestamp DESC",
                new Dictionary<string, object>
                {
                    ["@start"] = start.ToString("o"),
                    ["@end"] = end.ToString("o")
                });
            return DataTableToList(dt);
        }

        /// <summary>
        /// 获取报警统计
        /// </summary>
        public Dictionary<string, int> GetStatistics(DateTime start, DateTime end)
        {
            var dt = _db.ExecuteQuery(@"
                SELECT level, COUNT(*) as count
                FROM alarm_history
                WHERE timestamp BETWEEN @start AND @end
                GROUP BY level",
                new Dictionary<string, object>
                {
                    ["@start"] = start.ToString("o"),
                    ["@end"] = end.ToString("o")
                });

            var stats = new Dictionary<string, int>();
            foreach (DataRow row in dt.Rows)
            {
                stats[(string)row["level"]] = (int)(long)row["count"];
            }
            return stats;
        }

        private static List<AlarmRecord> DataTableToList(DataTable dt)
        {
            var list = new List<AlarmRecord>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new AlarmRecord
                {
                    Id = (long)row["id"],
                    Timestamp = DateTime.Parse((string)row["timestamp"]),
                    RuleName = (string)row["rule_name"],
                    DeviceId = (string)row["device_id"],
                    Address = row["address"] != DBNull.Value ? (ushort)(long)row["address"] : (ushort)0,
                    CurrentValue = row["current_value"] != DBNull.Value ? (double)row["current_value"] : 0,
                    Threshold = row["threshold"] != DBNull.Value ? (double)row["threshold"] : 0,
                    Level = (string)row["level"],
                    Status = (string)row["status"],
                    ConfirmedAt = row["confirmed_at"] != DBNull.Value ? DateTime.Parse((string)row["confirmed_at"]) : null,
                    ConfirmedBy = row["confirmed_by"] != DBNull.Value ? (string)row["confirmed_by"] : null
                });
            }
            return list;
        }
    }
}
