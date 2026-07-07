using System.Text.Json;
using Microsoft.Extensions.Logging;
using WpfScada.Models.Plc;

namespace WpfScada.Services.Plc;

public class AppConfigService
{
    private readonly string _filePath;
    private readonly ILogger<AppConfigService>? _logger;

    // ── 持久化属性（通过 DTO 反序列化填充） ──
    public string IP { get; set; } = "192.168.0.1";
    public int Port { get; set; } = 102;
    public int Rack { get; set; }
    public int Slot { get; set; } = 1;
    public string LocalIP { get; set; } = "";
    public string ThemeMode { get; set; } = "Dark";
    public string ManualIAddress { get; set; } = "0,1,8";
    public string ManualQAddress { get; set; } = "0";
    public string ManualMAddress { get; set; } = "0";
    public int PollInterval { get; set; } = 500;
    public string DbNumberInput { get; set; } = "1";
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public double WindowWidth { get; set; } = 1060;
    public double WindowHeight { get; set; } = 820;
    public string WindowState { get; set; } = "Normal";
    public bool ShowGallery { get; set; } = false;
    public bool UseVplcHttp { get; set; }
    public List<ImportedDbInfo> ImportedDbs { get; set; } = [];
    public List<ImportedUdtInfo> ImportedUdts { get; set; } = [];

    private AppConfigService(string filePath, ILogger<AppConfigService>? logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    // 反序列化 JSON 专用 —— 无参构造函数给 System.Text.Json 用
    // ReSharper disable once UnusedMember.Local
    private AppConfigService()
    {
        _filePath = Path.Combine(AppDataDir, "kesa_config.json");
    }

    private static string AppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfScada");

    /// <summary>
    /// 从 JSON 文件加载配置。若文件不存在或解析失败返回默认配置。
    /// logger 可选，传 null 时失败静默处理。
    /// </summary>
    public static AppConfigService Load(string? filePath = null, ILogger<AppConfigService>? logger = null)
    {
        var path = filePath ?? Path.Combine(AppDataDir, "kesa_config.json");
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<AppConfigDto>(json);
                if (dto != null)
                    return new AppConfigService(path, logger).Apply(dto);
            }
        }
        catch (Exception ex) { logger?.LogWarning(ex, "配置加载失败"); }
        return new AppConfigService(path, logger);
    }

    /// <summary>仅用于诊断，暴露文件路径</summary>
    public string GetFilePath() => _filePath;

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "配置保存失败"); }
    }

    /// <summary>
    /// DTO 属性映射到主对象。返回 this 用于链式调用。
    /// DTO 不包含 _filePath / _logger 等私有字段，避免反序列化干扰。
    /// </summary>
    private AppConfigService Apply(AppConfigDto dto)
    {
        IP = dto.IP;
        Port = dto.Port;
        Rack = dto.Rack;
        Slot = dto.Slot;
        LocalIP = dto.LocalIP;
        ThemeMode = dto.ThemeMode;
        ManualIAddress = dto.ManualIAddress;
        ManualQAddress = dto.ManualQAddress;
        ManualMAddress = dto.ManualMAddress;
        PollInterval = dto.PollInterval;
        DbNumberInput = dto.DbNumberInput;
        WindowLeft = dto.WindowLeft;
        WindowTop = dto.WindowTop;
        WindowWidth = dto.WindowWidth;
        WindowHeight = dto.WindowHeight;
        WindowState = dto.WindowState;
        ShowGallery = dto.ShowGallery;
        UseVplcHttp = dto.UseVplcHttp;
        ImportedDbs = dto.ImportedDbs;
        ImportedUdts = dto.ImportedUdts;
        return this;
    }

    /// <summary>仅供 JSON 反序列化使用的扁平 DTO。</summary>
    // ReSharper disable UnusedMember.Local
    private sealed record AppConfigDto
    {
        public string IP { get; set; } = "192.168.0.1";
        public int Port { get; set; } = 102;
        public int Rack { get; set; } = 0;
        public int Slot { get; set; } = 1;
        public string LocalIP { get; set; } = "";
        public string ThemeMode { get; set; } = "Dark";
        public string ManualIAddress { get; set; } = "0,1,8";
        public string ManualQAddress { get; set; } = "0";
        public string ManualMAddress { get; set; } = "0";
        public int PollInterval { get; set; } = 500;
        public string DbNumberInput { get; set; } = "1";
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public double WindowWidth { get; set; } = 1060;
        public double WindowHeight { get; set; } = 820;
        public string WindowState { get; set; } = "Normal";
        public bool ShowGallery { get; set; } = false;
        public bool UseVplcHttp { get; set; } = false;
        public List<ImportedDbInfo> ImportedDbs { get; set; } = [];
        public List<ImportedUdtInfo> ImportedUdts { get; set; } = [];
    }
    // ReSharper restore UnusedMember.Local
}
