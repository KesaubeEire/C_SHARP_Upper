using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Sharp7;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class DbMonitorPage : Page
{
    private readonly S7Service _s7;
    private DbStructure? _currentDb;
    private readonly ObservableCollection<DbVariableDisplay> _variables = [];
    private List<UdtStructure> _importedUdts = [];

    public DbMonitorPage(S7Service s7)
    {
        _s7 = s7;
        InitializeComponent();
        variableList.ItemsSource = _variables;
    }

    public void SetImportedUdts(List<UdtStructure> udts)
    {
        _importedUdts = udts;
    }

    public void RefreshConnectionStatus()
    {
        connIndicator.Fill = _s7.IsConnected
            ? GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96))
            : GetThemeBrush("TextFillColorDisabledBrush", Color.FromRgb(102, 102, 102));
        statusText.Text = _s7.IsConnected ? "已连接" : "未连接";
    }

    private static System.Windows.Media.Brush GetThemeBrush(string key, Color fallback)
    {
        return Application.Current.TryFindResource(key) as System.Windows.Media.Brush
               ?? new SolidColorBrush(fallback);
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
            _currentDb = DbFileParser.Parse(dialog.FileName);
            var input = new Wpf.Ui.Controls.ContentDialog();
            // Simplified - use a basic approach
            if (!int.TryParse(dbNumberInput.Text, out int dbNum))
                dbNum = 1;
            _currentDb.DbNumber = dbNum;

            // Detect UDT references
            ExpandVariables(_currentDb);
            dbInfoText.Text = $"DB{_currentDb.DbNumber}: {_currentDb.DbName} ({_currentDb.Variables.Count} 个变量)";
            RefreshConnectionStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败: {ex.Message}", "错误");
        }
    }

    private void ExpandVariables(DbStructure db)
    {
        _variables.Clear();
        int offset = 0;
        foreach (var v in db.Variables)
        {
            // Check UDT reference
            var udt = _importedUdts.FirstOrDefault(u => u.UdtName == v.DataType);
            if (udt != null)
            {
                foreach (var sub in udt.Variables)
                {
                    _variables.Add(new DbVariableDisplay
                    {
                        ByteOffset = offset + sub.Offset,
                        Name = $"{v.Name}.{sub.Name}",
                        DataType = sub.DataType,
                        Size = sub.Size,
                        InitialValue = sub.InitialValue,
                        Comment = sub.Comment,
                        IsFromUdt = true,
                        UdtName = v.DataType
                    });
                }
                offset += udt.Variables.Sum(x => x.Size);
            }
            else
            {
                _variables.Add(new DbVariableDisplay
                {
                    ByteOffset = v.Offset,
                    BitOffset = -1,
                    Name = v.Name,
                    DataType = v.DataType,
                    Size = v.Size,
                    InitialValue = v.InitialValue,
                    Comment = v.Comment
                });
                offset = v.Offset + v.Size;
            }
        }
    }

    private async void OnReadDb(object sender, RoutedEventArgs e)
    {
        if (_currentDb == null)
        {
            MessageBox.Show("请先导入 DB 结构", "提示");
            return;
        }

        if (!_s7.IsConnected)
        {
            MessageBox.Show("请先连接 PLC", "提示");
            return;
        }

        statusText.Text = "读取中...";
        int dbNum = _currentDb.DbNumber;

        try
        {
            await Task.Run(() =>
            {
                var displayList = _variables.ToList();
                if (displayList.Count == 0) return;

                var addrs = displayList.Select(v => v.ByteOffset).Distinct().OrderBy(a => a).ToArray();
                var bytes = _s7.ReadBytes(S7Service.AreaDB, addrs, dbNum);

                Dispatcher.InvokeAsync(() =>
                {
                    foreach (var v in displayList)
                    {
                        if (bytes.TryGetValue(v.ByteOffset, out byte b))
                        {
                            v.Value = DecodeValue(b, v.DataType, v.Size, bytes, v.ByteOffset);
                        }
                    }
                    statusText.Text = $"读取完成 ({displayList.Count} 个变量)";
                });
            });
        }
        catch (Exception ex)
        {
            statusText.Text = $"读取失败: {ex.Message}";
        }
    }

    private static string DecodeValue(byte firstByte, string type, int size, Dictionary<int, byte> allBytes, int baseOffset)
    {
        try
        {
            var buf = new byte[Math.Max(size, 1)];
            buf[0] = firstByte;
            for (int i = 1; i < size && i < 4; i++)
            {
                if (allBytes.TryGetValue(baseOffset + i, out byte b))
                    buf[i] = b;
            }

            return type switch
            {
                "BOOL" => firstByte != 0 ? "true" : "false",
                "BYTE" or "CHAR" => firstByte.ToString(),
                "INT" => S7.GetIntAt(buf, 0).ToString(),
                "DINT" => S7.GetDIntAt(buf, 0).ToString(),
                "REAL" => S7.GetRealAt(buf, 0).ToString("F3"),
                "WORD" => S7.GetWordAt(buf, 0).ToString(),
                _ => $"0x{BitConverter.ToString(buf)}"
            };
        }
        catch { return "???"; }
    }

    private void OnClearDb(object sender, RoutedEventArgs e)
    {
        _currentDb = null;
        _variables.Clear();
        dbInfoText.Text = "";
    }
}
