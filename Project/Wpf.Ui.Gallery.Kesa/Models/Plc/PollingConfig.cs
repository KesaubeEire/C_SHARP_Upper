using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wpf.Ui.Gallery.Models.Plc;

public class FastPathConfig
{
    public bool EnableI { get; set; } = true;
    public bool EnableQ { get; set; } = true;
    public bool EnableM { get; set; } = true;
    public string PollIAddr { get; set; } = "";
    public string PollQAddr { get; set; } = "";
    public string PollMAddr { get; set; } = "";

    public int[] ResolveAddr(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return [];
        return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => int.TryParse(x, out _))
                .Select(int.Parse)
                .ToArray();
    }
}

public class DbPollItem : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _status = "等待";
    private string? _label;

    public int DbNumber { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; }
    public bool Enabled { get => _enabled; set { _enabled = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string? Label { get => _label; set { _label = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class PollingConfig
{
    public FastPathConfig Fast { get; set; } = new();
    public List<DbPollItem> DbItems { get; set; } = [];
    public int FastInterval { get; set; } = 500;
    public string DbIp { get; set; } = "";
    public int DbRack { get; set; }
    public int DbSlot { get; set; }
}
