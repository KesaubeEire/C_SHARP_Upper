using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class ImportPage : Page
{
    private readonly AppConfigService _config;
    public ObservableCollection<DbStructure> ImportedDbs { get; } = [];
    public ObservableCollection<UdtStructure> ImportedUdts { get; } = [];

    public event EventHandler? ListChanged;

    public ImportPage(AppConfigService config)
    {
        _config = config;
        InitializeComponent();
        dbList.ItemsSource = ImportedDbs;
        udtList.ItemsSource = ImportedUdts;
        RestoreFromConfig();
    }

    private void RestoreFromConfig()
    {
        foreach (var d in _config.ImportedDbs)
        {
            ImportedDbs.Add(new DbStructure
            {
                DbNumber = d.DbNumber,
                DbName = d.DbName,
                SourceFile = d.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(d.VariablesJson) ?? []
            });
        }
        foreach (var u in _config.ImportedUdts)
        {
            ImportedUdts.Add(new UdtStructure
            {
                UdtName = u.UdtName,
                SourceFile = u.SourceFile,
                Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(u.VariablesJson) ?? []
            });
        }
    }

    public void Restore(IEnumerable<DbStructure> dbs, IEnumerable<UdtStructure> udts)
    {
        ImportedDbs.Clear();
        ImportedUdts.Clear();
        foreach (var d in dbs) ImportedDbs.Add(d);
        foreach (var u in udts) ImportedUdts.Add(u);
    }

    private void OnImportDb(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DB 文件|*.db;*.txt|All files|*.*",
            Title = "导入 DB 结构"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var db = DbFileParser.Parse(dialog.FileName);
            int dbNum = 1;
            {
                db.DbNumber = dbNum;
                ImportedDbs.Add(db);
                ListChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败: {ex.Message}", "错误");
        }
    }

    private void OnImportUdt(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "UDT 文件|*.udt;*.txt|All files|*.*",
            Title = "导入 UDT 结构"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var udt = UdtFileParser.Parse(dialog.FileName);
            ImportedUdts.Add(udt);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败: {ex.Message}", "错误");
        }
    }

    private void OnDeleteDb(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.Tag is DbStructure db)
        {
            ImportedDbs.Remove(db);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDeleteUdt(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.Tag is UdtStructure udt)
        {
            ImportedUdts.Remove(udt);
            ListChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
