using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace WpfScada.Controls.Input;

/// <summary>
/// 封装 Wpf.Ui AutoSuggestBox，支持通过 <see cref="IInputHistoryService"/> 管理历史记录。
/// 每条建议项右侧显示删除按钮，点击即从历史和界面移除。
/// </summary>
public class HistorySuggestBox : Wpf.Ui.Controls.AutoSuggestBox
{
    /// <summary>用于读取/保存历史记录的 Service key。</summary>
    public static readonly DependencyProperty HistoryKeyProperty = DependencyProperty.Register(
        nameof(HistoryKey), typeof(string), typeof(HistorySuggestBox), new PropertyMetadata(null));

    /// <summary>外部注入的历史服务。</summary>
    public static readonly DependencyProperty HistoryServiceProperty = DependencyProperty.Register(
        nameof(HistoryService), typeof(IInputHistoryService), typeof(HistorySuggestBox),
        new PropertyMetadata(null, OnHistoryServiceChanged));

    public string HistoryKey
    {
        get => (string)GetValue(HistoryKeyProperty);
        set => SetValue(HistoryKeyProperty, value);
    }

    public IInputHistoryService? HistoryService
    {
        get => (IInputHistoryService)GetValue(HistoryServiceProperty);
        set => SetValue(HistoryServiceProperty, value);
    }

    private static void OnHistoryServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HistorySuggestBox box && e.NewValue is IInputHistoryService svc)
            box.ReloadHistory(svc);
    }

    /// <summary>当前历史条目（包装后的 ViewModel，支持删除操作）。</summary>
    public ObservableCollection<HistoryItem> HistoryItems { get; } = [];

    /// <summary>建议列表始终用 HistoryItems。</summary>
    public HistorySuggestBox()
    {
        OriginalItemsSource = HistoryItems;
    }

    public void ReloadHistory(IInputHistoryService? svc = null)
    {
        svc ??= HistoryService;
        if (svc == null || string.IsNullOrEmpty(HistoryKey)) return;

        var raw = svc.GetHistory(HistoryKey);
        HistoryItems.Clear();
        foreach (var item in raw)
            HistoryItems.Add(new HistoryItem(item, this));
    }

    /// <summary>用户连接成功/输入后，调用此方法记录历史。</summary>
    public void AddCurrentToHistory(IInputHistoryService? svc = null)
    {
        svc ??= HistoryService;
        if (svc == null || string.IsNullOrEmpty(HistoryKey) || string.IsNullOrWhiteSpace(Text)) return;

        svc.AddEntry(HistoryKey, Text);
        ReloadHistory(svc);
    }

    /// <summary>删除单条历史。</summary>
    internal void RemoveItem(HistoryItem item)
    {
        var svc = HistoryService;
        if (svc == null || string.IsNullOrEmpty(HistoryKey)) return;

        svc.RemoveEntry(HistoryKey, item.Value);
        HistoryItems.Remove(item);
    }
}

/// <summary>一条历史记录的可观测包装，支持删除。</summary>
public class HistoryItem
{
    public string Value { get; }
    public HistorySuggestBox Owner { get; }

    public HistoryItem(string value, HistorySuggestBox owner)
    {
        Value = value;
        Owner = owner;
        RemoveCommand = new RelayCommand<object>(_ => Owner.RemoveItem(this));
    }

    public void Remove()
    {
        Owner.RemoveItem(this);
    }

    public ICommand RemoveCommand { get; }
}

/// <summary>View 层绑定用的接口（避免 ViewModel 直接依赖具体 Service）。</summary>
public interface IInputHistoryService
{
    List<string> GetHistory(string key);
    void AddEntry(string key, string value);
    void RemoveEntry(string key, string value);
}
