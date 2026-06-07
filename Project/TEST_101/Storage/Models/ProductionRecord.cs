namespace TEST_101.Storage.Models
{
    /// <summary>
    /// 生产数据记录
    /// </summary>
    public class ProductionRecord
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = "";
        public ushort Address { get; set; }
        public ushort RawValue { get; set; }
        public double ActualValue { get; set; }
        public string Unit { get; set; } = "";
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// 报警记录
    /// </summary>
    public class AlarmRecord
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string RuleName { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public ushort Address { get; set; }
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
        public string Level { get; set; } = "Warning";
        public string Status { get; set; } = "未确认";
        public DateTime? ConfirmedAt { get; set; }
        public string? ConfirmedBy { get; set; }
    }

    /// <summary>
    /// 配方记录
    /// </summary>
    public class RecipeRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string ParametersJson { get; set; } = "{}";
        public int Version { get; set; } = 1;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// 操作日志
    /// </summary>
    public class OperationLog
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Operator { get; set; }
        public string Action { get; set; } = "";
        public string? Details { get; set; }
    }
}
