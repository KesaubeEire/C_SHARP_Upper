using System.Data;
using TEST_101.Storage.Models;

namespace TEST_101.Storage.Repositories
{
    /// <summary>
    /// 生产数据仓储
    ///
    /// 面试考点：Repository 模式是什么？数据访问层的抽象
    /// </summary>
    public class ProductionRepository
    {
        private readonly DatabaseManager _db;

        public ProductionRepository(DatabaseManager db)
        {
            _db = db;
        }

        /// <summary>
        /// 插入一条生产数据
        /// </summary>
        public void Insert(ProductionRecord record)
        {
            const string sql = @"
                INSERT INTO production_data (timestamp, device_id, address, raw_value, actual_value, unit, name)
                VALUES (@timestamp, @device_id, @address, @raw_value, @actual_value, @unit, @name)";

            _db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                ["@timestamp"] = record.Timestamp.ToString("o"),
                ["@device_id"] = record.DeviceId,
                ["@address"] = record.Address,
                ["@raw_value"] = record.RawValue,
                ["@actual_value"] = record.ActualValue,
                ["@unit"] = record.Unit,
                ["@name"] = record.Name
            });
        }

        /// <summary>
        /// 批量插入（用于高频数据采集）
        /// </summary>
        public void BulkInsert(List<ProductionRecord> records)
        {
            var dt = new DataTable();
            dt.Columns.Add("timestamp", typeof(string));
            dt.Columns.Add("device_id", typeof(string));
            dt.Columns.Add("address", typeof(int));
            dt.Columns.Add("raw_value", typeof(int));
            dt.Columns.Add("actual_value", typeof(double));
            dt.Columns.Add("unit", typeof(string));
            dt.Columns.Add("name", typeof(string));

            foreach (var r in records)
            {
                dt.Rows.Add(
                    r.Timestamp.ToString("o"),
                    r.DeviceId,
                    (int)r.Address,
                    (int)r.RawValue,
                    r.ActualValue,
                    r.Unit,
                    r.Name
                );
            }

            _db.BulkInsert("production_data", dt);
        }

        /// <summary>
        /// 按时间范围查询
        /// </summary>
        public List<ProductionRecord> GetByTimeRange(DateTime start, DateTime end, string? deviceId = null)
        {
            var sql = "SELECT * FROM production_data WHERE timestamp BETWEEN @start AND @end";
            var parameters = new Dictionary<string, object>
            {
                ["@start"] = start.ToString("o"),
                ["@end"] = end.ToString("o")
            };

            if (!string.IsNullOrEmpty(deviceId))
            {
                sql += " AND device_id = @device_id";
                parameters["@device_id"] = deviceId;
            }

            sql += " ORDER BY timestamp";

            var dt = _db.ExecuteQuery(sql, parameters);
            return DataTableToList(dt);
        }

        /// <summary>
        /// 获取最新 N 条记录
        /// </summary>
        public List<ProductionRecord> GetLatest(int count, string? deviceId = null)
        {
            var sql = "SELECT * FROM production_data";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(deviceId))
            {
                sql += " WHERE device_id = @device_id";
                parameters["@device_id"] = deviceId;
            }

            sql += " ORDER BY timestamp DESC LIMIT @count";
            parameters["@count"] = count;

            var dt = _db.ExecuteQuery(sql, parameters);
            return DataTableToList(dt);
        }

        /// <summary>
        /// 清理旧数据（保留最近 N 天）
        /// </summary>
        public int CleanupOldDays(int keepDays = 30)
        {
            var cutoff = DateTime.Now.AddDays(-keepDays).ToString("o");
            return _db.ExecuteNonQuery(
                "DELETE FROM production_data WHERE timestamp < @cutoff",
                new Dictionary<string, object> { ["@cutoff"] = cutoff }
            );
        }

        /// <summary>
        /// DataTable 转 List
        /// </summary>
        private static List<ProductionRecord> DataTableToList(DataTable dt)
        {
            var list = new List<ProductionRecord>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new ProductionRecord
                {
                    Id = (long)row["id"],
                    Timestamp = DateTime.Parse((string)row["timestamp"]),
                    DeviceId = (string)row["device_id"],
                    Address = (ushort)(long)row["address"],
                    RawValue = (ushort)(long)row["raw_value"],
                    ActualValue = row["actual_value"] != DBNull.Value ? (double)row["actual_value"] : 0,
                    Unit = row["unit"] != DBNull.Value ? (string)row["unit"] : "",
                    Name = row["name"] != DBNull.Value ? (string)row["name"] : ""
                });
            }
            return list;
        }
    }
}
