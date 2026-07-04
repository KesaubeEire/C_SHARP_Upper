using System.Text.Json;
using WpfScada.Models.Plc;

namespace WpfScada.Services.Plc;

public class AppConfigService
{
    private readonly string _filePath;

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
    public List<ImportedDbInfo> ImportedDbs { get; set; } = [];
    public List<ImportedUdtInfo> ImportedUdts { get; set; } = [];

    /// <summary>System.Text.Json 反序列化专用无参构造函数</summary>
    public AppConfigService()
    {
        _filePath = Path.Combine(AppDataDir, "kesa_config.json");
    }

    public AppConfigService(string filePath)
    {
        _filePath = filePath;
    }

    private static string AppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfScada");

    public static AppConfigService Load(string? filePath = null)
    {
        var path = filePath ?? Path.Combine(AppDataDir, "kesa_config.json");
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfigService>(json) ?? new AppConfigService(path);
            }
        }
        catch { }
        return new AppConfigService(path);
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
        catch { }
    }
}
