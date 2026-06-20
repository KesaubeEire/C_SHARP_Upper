using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestWpf.Models;

/// <summary>
/// 一个位（Bit）的视图模型 — 显示 0/1，支持点击切换
/// </summary>
public class BitViewModel : INotifyPropertyChanged
{
    private bool _isSet;

    public int Index { get; }
    public bool IsReadOnly { get; }
    public ByteRowViewModel? Parent { get; set; }

    public bool IsSet
    {
        get => _isSet;
        set
        {
            if (_isSet != value)
            {
                _isSet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayChar));
                OnPropertyChanged(nameof(BgColor));
            }
        }
    }

    public string DisplayChar => _isSet ? "1" : "0";
    public string BgColor => _isSet ? "#27AE60" : "#3A3A5C";

    public BitViewModel(int index, bool isReadOnly)
    {
        Index = index;
        IsReadOnly = isReadOnly;
    }

    public void Toggle()
    {
        if (!IsReadOnly)
        {
            IsSet = !IsSet;
            Parent?.NotifyBitChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
