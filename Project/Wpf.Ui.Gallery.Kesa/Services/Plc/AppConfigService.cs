using System.Text.Json;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

public class AppConfigService
{
    private readonly string _filePath;

    public string IP { get; set; } = "";
    public int Port { get; set; } = 102;
    public int Rack { get; set; }
    public int Slot { get; set; } = 1;
    public string LocalIP { get; set; } = "";
    public string ThemeMode { get; set; } = "Dark";
    public string ManualIAddress { get; set; } = "0,1,8";
    public string ManualQAddress { get; set; } = "";
    public string ManualMAddress { get; set; } = "";
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public double WindowWidth { get; set; } = 1060;
    public double WindowHeight { get; set; } = 820;
    public string WindowState { get; set; } = "Normal";
    public List<ImportedDbInfo> ImportedDbs { get; set; } = [];
    public List<ImportedUdtInfo> ImportedUdts { get; set; } = [];

    public AppConfigService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "kesa_config.json");
    }

    public static AppConfigService Load(string? filePath = null)
    {
        var path = filePath ?? Path.Combine(AppContext.BaseDirectory, "kesa_config.json");
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
