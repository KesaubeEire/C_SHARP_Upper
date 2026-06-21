using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TestWpf.Controls;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf.Controls.Sidebar;

/// <summary>
/// 导入 DB/UDT 面板 — 文件导入、已导入列表管理
/// </summary>
public partial class ImportPanel : UserControl
{
    private readonly ObservableCollection<DbStructure> _importedDbs = [];
    private readonly ObservableCollection<UdtStructure> _importedUdts = [];

    public ImportPanel()
    {
        InitializeComponent();
        listImportedDb.ItemsSource = _importedDbs;
        listImportedUdt.ItemsSource = _importedUdts;
    }

    public ObservableCollection<DbStructure> ImportedDbs => _importedDbs;
    public ObservableCollection<UdtStructure> ImportedUdts => _importedUdts;

    /// <summary>导入列表变化时通知外部保存配置</summary>
    public event EventHandler? ListChanged;

    private void OnImportDb(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DB 文件 (*.db)|*.db|所有文件 (*.*)|*.*",
            Title = "选择 TIA Portal 导出的 .db 文件",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;

        var db = DbFileParser.Parse(dlg.FileName);
        if (db.HasUnknownType)
        {
            MessageBox.Show($"解析失败: {db.ParseError}", "未知数据类型", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (db.ParseError != null)
        {
            MessageBox.Show($"解析失败: {db.ParseError}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var inputDlg = new InputDialog($"请输入 DB{db.DbName} 的 DB 编号:", "1");
        if (inputDlg.ShowDialog() != true) return;
        if (!int.TryParse(inputDlg.InputText, out int dbNum) || dbNum <= 0)
        {
            MessageBox.Show("无效的 DB 编号", "错误");
            return;
        }
        if (_importedDbs.Any(d => d.DbNumber == dbNum))
        {
            MessageBox.Show($"DB{dbNum} 已导入，请先删除再重新导入", "提示");
            return;
        }

        db.DbNumber = dbNum;
        _importedDbs.Add(db);
        ListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnImportUdt(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "UDT 文件 (*.udt)|*.udt|所有文件 (*.*)|*.*",
            Title = "选择 TIA Portal 导出的 .udt 文件",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;

        var udt = UdtFileParser.Parse(dlg.FileName);
        if (udt.HasUnknownType)
        {
            MessageBox.Show($"解析失败: {udt.ParseError}", "未知数据类型", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (udt.ParseError != null)
        {
            MessageBox.Show($"解析失败: {udt.ParseError}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (_importedUdts.Any(u => u.UdtName == udt.UdtName))
        {
            MessageBox.Show($"UDT \"{udt.UdtName}\" 已导入", "提示");
            return;
        }
        _importedUdts.Add(udt);
        ListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteDb(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DbStructure db)
        {
            _importedDbs.Remove(db);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDeleteUdt(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UdtStructure udt)
        {
            _importedUdts.Remove(udt);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>从配置恢复导入列表</summary>
    public void Restore(IEnumerable<DbStructure> dbs, IEnumerable<UdtStructure> udts)
    {
        _importedDbs.Clear();
        foreach (var db in dbs) _importedDbs.Add(db);
        _importedUdts.Clear();
        foreach (var udt in udts) _importedUdts.Add(udt);
    }
}
