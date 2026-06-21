using System.IO;
using System.Text.Json;
using TestWpf.Models;

namespace TestWpf.Services;

/// <summary>
/// 对标 Web localStorage — 所有用户输入自动存 JSON，重启还原
/// 数据存在: app_config.json（在 exe 同目录）
/// </summary>
public sealed class AppConfig
{
    private static readonly string ConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_config.json");

    // ===== 连接 =====
    public string IP { get; set; } = "192.168.0.1";
    public string LocalIP { get; set; } = "";
    public int Port { get; set; } = 102;
    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 0;

    // ===== 手动模式地址 =====
    public string ManualIAddress { get; set; } = "0,1";
    public string ManualQAddress { get; set; } = "0";
    public string ManualMAddress { get; set; } = "0,1";

    // ===== 自动轮询范围 =====
    public int PollIStart { get; set; } = 0;
    public int PollIEnd { get; set; } = 2;
    public int PollQStart { get; set; } = 0;
    public int PollQEnd { get; set; } = 1;
    public int PollMStart { get; set; } = 0;
    public int PollMEnd { get; set; } = 10;
    public bool PollEnableI { get; set; } = true;
    public bool PollEnableQ { get; set; } = true;
    public bool PollEnableM { get; set; } = true;

    // ===== 轮询间隔 =====
    public int PollIntervalMs { get; set; } = 50;

    // ===== DB 轮询列表 =====
    public List<DbPollItem> DbItems { get; set; } = [];

    // ===== 导入的 DB 结构 =====
    public List<ImportedDbInfo> ImportedDbs { get; set; } = [];

    // ===== 导入的 UDT 结构 =====
    public List<ImportedUdtInfo> ImportedUdts { get; set; } = [];

    // ===== 主题 =====
    public string ThemeMode { get; set; } = "Dark";

    // ===== 窗口状态 =====
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public double WindowWidth { get; set; } = 1060;
    public double WindowHeight { get; set; } = 820;
    public string WindowState { get; set; } = "Normal";

    // ===== 加载/保存 =====

    /// <summary>从文件加载，文件不存在则返回默认值</summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // 文件损坏等情况，返回默认
        }
        return new AppConfig();
    }

    /// <summary>保存到文件</summary>
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
        }
    }
}
