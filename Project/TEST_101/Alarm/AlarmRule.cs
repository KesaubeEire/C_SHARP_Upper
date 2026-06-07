using TEST_101.Core;

namespace TEST_101.Alarm
{
    /// <summary>
    /// 报警规则
    ///
    /// 定义什么条件下触发报警
    /// </summary>
    public class AlarmRule
    {
        /// <summary>规则 ID</summary>
        public string RuleId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>规则名称</summary>
        public string Name { get; set; } = "";

        /// <summary>设备标识</summary>
        public string DeviceId { get; set; } = "";

        /// <summary>寄存器地址</summary>
        public ushort Address { get; set; }

        /// <summary>报警条件</summary>
        public AlarmCondition Condition { get; set; }

        /// <summary>阈值</summary>
        public double Threshold { get; set; }

        /// <summary>报警等级</summary>
        public AlarmLevel Level { get; set; } = AlarmLevel.Warning;

        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 检查是否触发报警
        /// </summary>
        public bool Check(double value)
        {
            if (!IsEnabled) return false;

            return Condition switch
            {
                AlarmCondition.GreaterThan => value > Threshold,
                AlarmCondition.LessThan => value < Threshold,
                AlarmCondition.GreaterThanOrEqual => value >= Threshold,
                AlarmCondition.LessThanOrEqual => value <= Threshold,
                AlarmCondition.Equal => Math.Abs(value - Threshold) < 0.001,
                _ => false
            };
        }

        /// <summary>
        /// 获取描述
        /// </summary>
        public string GetDescription()
        {
            var conditionText = Condition switch
            {
                AlarmCondition.GreaterThan => ">",
                AlarmCondition.LessThan => "<",
                AlarmCondition.GreaterThanOrEqual => ">=",
                AlarmCondition.LessThanOrEqual => "<=",
                AlarmCondition.Equal => "==",
                _ => "?"
            };
            return $"{Name}: {conditionText} {Threshold}";
        }
    }

    /// <summary>
    /// 报警条件枚举
    /// </summary>
    public enum AlarmCondition
    {
        GreaterThan,          // 大于
        LessThan,             // 小于
        GreaterThanOrEqual,   // 大于等于
        LessThanOrEqual,      // 小于等于
        Equal                 // 等于
    }
}
