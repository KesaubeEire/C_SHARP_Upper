namespace TEST_101.Core
{
    /// <summary>
    /// 数据点模型 —— 统一的数据表示
    ///
    /// 用于实时曲线、数据库存储、报警检测等模块
    /// </summary>
    public class DataPoint
    {
        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>设备标识</summary>
        public string DeviceId { get; set; } = "";

        /// <summary>寄存器地址</summary>
        public ushort Address { get; set; }

        /// <summary>原始值（16位无符号）</summary>
        public ushort RawValue { get; set; }

        /// <summary>
        /// 缩放后的实际值
        /// 例如：RawValue=1000, Scale=0.1 → ActualValue=100.0
        /// </summary>
        public double ActualValue { get; set; }

        /// <summary>单位（rpm, Hz, °C, % 等）</summary>
        public string Unit { get; set; } = "";

        /// <summary>数据名称（用于显示）</summary>
        public string Name { get; set; } = "";

        /// <summary>是否有效</summary>
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// 通道配置 —— 定义一个数据采集通道
    ///
    /// 对应界面上的"通道配置"面板
    /// </summary>
    public class ChannelConfig
    {
        /// <summary>通道编号</summary>
        public int ChannelId { get; set; }

        /// <summary>通道名称</summary>
        public string Name { get; set; } = "";

        /// <summary>设备标识</summary>
        public string DeviceId { get; set; } = "";

        /// <summary>寄存器地址</summary>
        public ushort Address { get; set; }

        /// <summary>缩放系数</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>偏移量</summary>
        public double Offset { get; set; } = 0.0;

        /// <summary>单位</summary>
        public string Unit { get; set; } = "";

        /// <summary>曲线颜色（ARGB）</summary>
        public int Color { get; set; } = 0xFF0000; // 默认红色

        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>报警上限</summary>
        public double? AlarmHigh { get; set; }

        /// <summary>报警下限</summary>
        public double? AlarmLow { get; set; }

        /// <summary>
        /// 将原始值转换为实际值
        /// </summary>
        public double ConvertValue(ushort rawValue)
        {
            return rawValue * Scale + Offset;
        }
    }

    /// <summary>
    /// 设备配置 —— 定义一个 PLC 设备
    /// </summary>
    public class DeviceConfig
    {
        /// <summary>设备唯一标识</summary>
        public string DeviceId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>设备名称</summary>
        public string Name { get; set; } = "";

        /// <summary>设备类型（PLC-200SMART, PLC-1200, PLC-1500 等）</summary>
        public string DeviceType { get; set; } = "";

        /// <summary>通讯模式（RTU, TCP）</summary>
        public CommMode Mode { get; set; } = CommMode.RTU;

        // RTU 参数
        /// <summary>串口号</summary>
        public string ComPort { get; set; } = "COM1";

        /// <summary>波特率</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>站号</summary>
        public byte Station { get; set; } = 1;

        // TCP 参数
        /// <summary>IP 地址</summary>
        public string IpAddress { get; set; } = "192.168.1.1";

        /// <summary>端口号</summary>
        public int Port { get; set; } = 502;

        /// <summary>采集周期（毫秒）</summary>
        public int PollInterval { get; set; } = 200;

        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 克隆配置（用于编辑对话框的取消操作）
        /// </summary>
        public DeviceConfig Clone()
        {
            return (DeviceConfig)this.MemberwiseClone();
        }
    }

    /// <summary>
    /// 通讯模式枚举
    /// </summary>
    public enum CommMode
    {
        RTU,
        TCP
    }
}
