using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class DbMonitorPage : Page
{
    private readonly S7Service _s7;
    private readonly AppConfigService _config;
    private DbStructure? _currentDb;
    private List<UdtStructure> _importedUdts = [];
    private HashSet<string>? _missingUdts;
    private readonly ObservableCollection<DbVariableDisplay> _variables = [];

    public DbMonitorPage(S7Service s7, AppConfigService config)
    {
        _s7 = s7;
        _config = config;
        InitializeComponent();
        variableList.ItemsSource = _variables;
        LoadPersistedUdts();
        LoadPersistedDb();
        dbNumberInput.TextChanged += (_, _) =>
        {
            _config.DbNumberInput = dbNumberInput.Text;
            _config.Save();
        };
        UpdateEmptyState();
    }

    /// <summary>注入已导入的 UDT 列表（由外部调用，如侧边栏同步）</summary>
    public void SetImportedUdts(List<UdtStructure> udts)
    {
        _importedUdts = udts;
        if (_currentDb != null)
            DetectMissingUdts();
    }

    /// <summary>从持久化配置加载已导入的 UDT</summary>
    private void LoadPersistedUdts()
    {
        foreach (var info in _config.ImportedUdts)
        {
            var variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson);
            if (variables != null)
            {
                _importedUdts.Add(new UdtStructure
                {
                    UdtName = info.UdtName,
                    SourceFile = info.SourceFile,
                    Variables = variables
                });
            }
        }
    }

    /// <summary>从持久化配置恢复上次导入的 DB</summary>
    private void LoadPersistedDb()
    {
        dbNumberInput.Text = _config.DbNumberInput;

        var lastDb = _config.ImportedDbs.LastOrDefault();
        if (lastDb == null) return;

        var variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(lastDb.VariablesJson);
        if (variables == null) return;

        _currentDb = new DbStructure
        {
            DbNumber = lastDb.DbNumber,
            DbName = lastDb.DbName,
            SourceFile = lastDb.SourceFile,
            Variables = variables
        };

        DetectMissingUdts();
        ExpandVariables();
        dbInfoText.Text = $"已恢复: {_currentDb.Label}";
        dbInfoText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
        UpdateDbSizeInfo();
        UpdateEmptyState();
    }

    /// <summary>保存当前 DB 到持久化配置</summary>
    private void PersistDbs()
    {
        _config.ImportedDbs.Clear();
        if (_currentDb != null)
        {
            _config.ImportedDbs.Add(new ImportedDbInfo
            {
                DbNumber = _currentDb.DbNumber,
                DbName = _currentDb.DbName,
                SourceFile = _currentDb.SourceFile,
                VariablesJson = System.Text.Json.JsonSerializer.Serialize(_currentDb.Variables)
            });
        }
        _config.Save();
    }

    /// <summary>保存当前 UDT 列表到持久化配置</summary>
    private void PersistUdts()
    {
        _config.ImportedUdts.Clear();
        foreach (var udt in _importedUdts)
        {
            _config.ImportedUdts.Add(new ImportedUdtInfo
            {
                UdtName = udt.UdtName,
                SourceFile = udt.SourceFile,
                VariablesJson = System.Text.Json.JsonSerializer.Serialize(udt.Variables)
            });
        }
        _config.Save();
    }

    /// <summary>导入 .udt 文件</summary>
    private async void OnImportUdt(object sender, RoutedEventArgs e)
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
            if (udt.ParseError != null)
            {
                await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"解析失败: {udt.ParseError}" }.ShowDialogAsync();
                return;
            }

            // 检查是否已存在同名 UDT
            var existing = _importedUdts.FindIndex(u =>
                string.Equals(u.UdtName, udt.UdtName, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                var r = System.Windows.MessageBox.Show(
                    $"UDT \"{udt.UdtName}\" 已导入，是否替换？", "提示",
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (r != System.Windows.MessageBoxResult.Yes) return;
                _importedUdts.RemoveAt(existing);
            }

            _importedUdts.Add(udt);
            PersistUdts();

            // 如果已有 DB 加载，重新检测 UDT 引用
            if (_currentDb != null)
            {
                DetectMissingUdts();
                ExpandVariables();
            }

            operStatusText.Text = $"✅ 已导入 UDT: {udt.UdtName}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"导入失败: {ex.Message}" }.ShowDialogAsync();
        }
    }

    /// <summary>刷新连接状态指示（由外部周期调用）</summary>
    public void RefreshConnectionStatus()
    {
        connIndicator.Fill = _s7.IsConnected
            ? GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96))
            : GetThemeBrush("TextFillColorDisabledBrush", Color.FromRgb(102, 102, 102));
        statusText.Text = _s7.IsConnected ? "已连接" : "未连接";
    }

    private static Brush GetThemeBrush(string key, Color fallback)
    {
        return Application.Current.TryFindResource(key) as Brush
               ?? new SolidColorBrush(fallback);
    }

    // ===================== 导入 .db =====================

    private async void OnImportDb(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DB 文件|*.db;*.txt|所有文件|*.*",
            Title = "选择 TIA Portal 导出的 .db 文件"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var db = DbFileParser.Parse(dialog.FileName);
            if (db.HasUnknownType)
            {
                await new Wpf.Ui.Controls.MessageBox { Title = "未知数据类型", Content = $"解析警告: {db.ParseError}\n\n未识别的变量类型将被跳过。" }.ShowDialogAsync();
            }
            if (db.ParseError != null && db.Variables.Count == 0)
            {
                await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"解析失败: {db.ParseError}" }.ShowDialogAsync();
                return;
            }

            if (!int.TryParse(dbNumberInput.Text, out int dbNum) || dbNum <= 0)
                dbNum = 1;

            db.DbNumber = dbNum;
            _currentDb = db;
            dbNumberInput.Text = dbNum.ToString();
            PersistDbs();

            // UDT 检测
            DetectMissingUdts();

            // 展开变量列表（重新计算偏移 + UDT 展开）
            ExpandVariables();

            // 更新 UI
            dbInfoText.Text = $"已导入: {db.Label}";
            dbInfoText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
            UpdateDbSizeInfo();
            UpdateEmptyState();
            operStatusText.Text = "";
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "错误", Content = $"导入失败: {ex.Message}" }.ShowDialogAsync();
        }
    }

    private void OnClearDb(object sender, RoutedEventArgs e)
    {
        _currentDb = null;
        _variables.Clear();
        _missingUdts = null;
        _config.ImportedDbs.Clear();
        _config.Save();
        dbInfoText.Text = "未导入 DB 文件";
        dbInfoText.Foreground = GetThemeBrush("TextFillColorSecondaryBrush", Color.FromRgb(128, 128, 128));
        dbSizeText.Text = "";
        udtHintText.Text = "";
        operStatusText.Text = "";
        UpdateEmptyState();
    }

    // ===================== UDT 检测 =====================

    private void DetectMissingUdts()
    {
        _missingUdts = null;
        if (_currentDb == null) return;

        var knownTypes = new HashSet<string>(SiemensDataTypes.Known.Keys, StringComparer.OrdinalIgnoreCase);
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in _currentDb.Variables)
        {
            string type = v.DataType.Trim().Trim('"');
            string upper = type.ToUpperInvariant();

            // 跳过基本类型和 STRUCT
            if (knownTypes.Contains(upper)) continue;
            if (upper.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase)) continue;
            if (upper.StartsWith("STRING", StringComparison.OrdinalIgnoreCase)) continue;

            // ARRAY[...] OF X — 提取元素类型 X
            if (upper.StartsWith("ARRAY", StringComparison.OrdinalIgnoreCase))
            {
                var m = System.Text.RegularExpressions.Regex.Match(upper,
                    @"ARRAY\s*\[(\d+)\.\.(\d+)\]\s*OF\s+(.+)");
                if (m.Success)
                {
                    string elemType = m.Groups[3].Value.Trim().Trim('"');
                    if (!knownTypes.Contains(elemType) && !elemType.StartsWith("STRUCT", StringComparison.OrdinalIgnoreCase))
                        referenced.Add(elemType);
                }
                continue;
            }

            // 剩下的大概率是 UDT 引用
            referenced.Add(type);
        }

        if (referenced.Count == 0)
        {
            udtHintText.Text = "✅ 未检测到 UDT 引用";
            udtHintText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
            return;
        }

        var importedNames = new HashSet<string>(
            _importedUdts.Select(u => u.UdtName), StringComparer.OrdinalIgnoreCase);

        var missing = referenced.Where(r => !importedNames.Contains(r)).ToList();
        var found = referenced.Where(r => importedNames.Contains(r)).ToList();

        if (missing.Count > 0)
        {
            _missingUdts = [.. missing];
            udtHintText.Text = $"⚠ 缺少 UDT: {string.Join(", ", missing)}" +
                (found.Count > 0 ? $"\n✅ 已找到 UDT: {string.Join(", ", found)}" : "");
            udtHintText.Foreground = GetThemeBrush("SystemFillColorCautionBrush", Color.FromRgb(243, 156, 18));
        }
        else
        {
            _missingUdts = null;
            udtHintText.Text = $"✅ 所有 UDT 已就绪 ({string.Join(", ", found)})";
            udtHintText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
        }
    }

    // ===================== 变量展开 =====================

    private void ExpandVariables()
    {
        _variables.Clear();
        if (_currentDb == null) return;

        var expanded = RecalculateOffsetsWithBits(_currentDb.Variables);
        foreach (var display in expanded)
            _variables.Add(display);
    }

    /// <summary>
    /// 重新计算偏移量 — BOOL 变量用位偏移（0.0-0.7），非 BOOL 类型两字节字对齐
    /// </summary>
    private List<DbVariableDisplay> RecalculateOffsetsWithBits(List<DbVariable> variables)
    {
        var result = new List<DbVariableDisplay>();
        int byteOffset = 0;
        int bitOffset = 0;

        foreach (var v in variables)
        {
            string rawType = v.DataType.Trim().Trim('"');
            string upperType = rawType.ToUpperInvariant();

            // 检查是否为 UDT 引用
            var udt = FindMatchingUdt(rawType);
            if (udt != null)
            {
                // UDT 展开 — 对齐到 2 字节边界
                if (bitOffset > 0) { byteOffset++; bitOffset = 0; }
                if (byteOffset % 2 != 0) byteOffset++;

                int udtBaseOffset = byteOffset;
                int udtBitOffset = 0;

                foreach (var subVar in udt.Variables)
                {
                    string subUpper = subVar.DataType.Trim().ToUpperInvariant();
                    if (subUpper == "BOOL")
                    {
                        result.Add(new DbVariableDisplay
                        {
                            ByteOffset = udtBaseOffset,
                            BitOffset = udtBitOffset,
                            Name = $"{v.Name}.{subVar.Name}",
                            DataType = subVar.DataType,
                            Size = subVar.Size,
                            InitialValue = subVar.InitialValue,
                            Comment = subVar.Comment,
                            IsFromUdt = true,
                            UdtName = udt.UdtName,
                        });
                        udtBitOffset++;
                        if (udtBitOffset > 7) { udtBaseOffset++; udtBitOffset = 0; }
                    }
                    else
                    {
                        if (udtBitOffset > 0) { udtBaseOffset++; udtBitOffset = 0; }
                        if (udtBaseOffset % 2 != 0) udtBaseOffset++;

                        result.Add(new DbVariableDisplay
                        {
                            ByteOffset = udtBaseOffset,
                            BitOffset = -1,
                            Name = $"{v.Name}.{subVar.Name}",
                            DataType = subVar.DataType,
                            Size = subVar.Size,
                            InitialValue = subVar.InitialValue,
                            Comment = subVar.Comment,
                            IsFromUdt = true,
                            UdtName = udt.UdtName,
                        });
                        udtBaseOffset += subVar.Size;
                    }
                }

                byteOffset = udtBaseOffset;
                bitOffset = 0;
                continue;
            }

            // 非 UDT：正常解析
            if (SiemensDataTypes.TryResolve(upperType, out int size, out _))
            {
                if (upperType == "BOOL")
                {
                    result.Add(new DbVariableDisplay
                    {
                        ByteOffset = byteOffset,
                        BitOffset = bitOffset,
                        Name = v.Name,
                        DataType = v.DataType,
                        Size = 1,
                        InitialValue = v.InitialValue,
                        Comment = v.Comment,
                    });
                    bitOffset++;
                    if (bitOffset > 7) { byteOffset++; bitOffset = 0; }
                }
                else
                {
                    if (bitOffset > 0) { byteOffset++; bitOffset = 0; }
                    if (byteOffset % 2 != 0) byteOffset++;

                    result.Add(new DbVariableDisplay
                    {
                        ByteOffset = byteOffset,
                        BitOffset = -1,
                        Name = v.Name,
                        DataType = v.DataType,
                        Size = size,
                        InitialValue = v.InitialValue,
                        Comment = v.Comment,
                    });
                    byteOffset += size;
                }
            }
            else
            {
                // 未知类型 — 占位
                if (bitOffset > 0) { byteOffset++; bitOffset = 0; }
                result.Add(new DbVariableDisplay
                {
                    ByteOffset = byteOffset,
                    BitOffset = -1,
                    Name = v.Name,
                    DataType = v.DataType + " (?)",
                    Size = 2,
                    Value = "?",
                });
                byteOffset += 2;
            }
        }

        return result;
    }

    private UdtStructure? FindMatchingUdt(string typeName)
    {
        string name = typeName.Trim().Trim('"');
        return _importedUdts.FirstOrDefault(u =>
            string.Equals(u.UdtName, name, StringComparison.OrdinalIgnoreCase));
    }

    // ===================== PLC 读取 =====================

    private async void OnReadDb(object sender, RoutedEventArgs e)
    {
        if (_currentDb == null)
        {
            operStatusText.Text = "⚠ 请先导入 .db 文件";
            return;
        }
        if (!_s7.IsConnected)
        {
            operStatusText.Text = "⚠ 请先连接 PLC";
            return;
        }

        // UDT 缺失检查
        if (_missingUdts != null && _missingUdts.Count > 0)
        {
            var r = System.Windows.MessageBox.Show(
                $"以下 UDT 未导入，相关变量可能无法正确读取:\n{string.Join("\n", _missingUdts.Select(u => $"  • {u}"))}\n\n是否继续读取？",
                "UDT 引用缺失", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (r != System.Windows.MessageBoxResult.Yes) return;
        }

        btnReadDb.IsEnabled = false;
        btnReadDb.Content = "⏳ 读取中...";
        operStatusText.Text = "正在读取...";

        try
        {
            await ReadDbValuesAsync();
        }
        catch (Exception ex)
        {
            operStatusText.Text = $"❌ 读取异常: {ex.Message}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorCriticalBrush", Color.FromRgb(231, 76, 60));
        }
        finally
        {
            btnReadDb.IsEnabled = true;
            btnReadDb.Content = "📡 读取 DB";
        }
    }

    private async Task ReadDbValuesAsync()
    {
        if (_currentDb == null) return;

        // 1. 计算需要读取的范围（最小偏移 → 最大偏移+大小）
        int minOffset = _variables.Min(v => v.ByteOffset);
        int maxEnd = _variables.Max(v => v.ByteOffset + Math.Max(v.Size, 1));
        int totalLength = maxEnd - minOffset;

        if (totalLength <= 0) return;

        int dbNum = _currentDb.DbNumber;

        // 2. 后台线程一次读取连续块（比逐字节或分组更高效）
        byte[]? rawBuffer = await Task.Run(() => _s7.ReadBytesRaw(S7Service.AreaDB, minOffset, totalLength, dbNum));

        if (rawBuffer == null)
        {
            operStatusText.Text = $"❌ 读取失败: {_s7.LastError ?? "未知错误"}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorCriticalBrush", Color.FromRgb(231, 76, 60));
            return;
        }

        // 3. 构建偏移→字节的字典，方便后续解码
        var bytes = new Dictionary<int, byte>(totalLength);
        for (int i = 0; i < totalLength; i++)
            bytes[minOffset + i] = rawBuffer[i];

        // 4. 解码每个变量的值
        int successCount = 0;
        foreach (var display in _variables)
        {
            display.Value = DecodeValue(display, bytes);
            if (display.Value != "?") successCount++;
        }

        operStatusText.Text = $"✅ 读取完成 ({successCount}/{_variables.Count} 个变量成功)";
        operStatusText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
    }

    // ===================== 值解码 =====================

    private static string DecodeValue(DbVariableDisplay display, Dictionary<int, byte> rawBytes)
    {
        int offset = display.ByteOffset;
        string rawType = display.DataType.Trim().Trim('"');
        string type = rawType.ToUpperInvariant();

        // BOOL — 提取指定位
        if (display.IsBit)
        {
            if (rawBytes.TryGetValue(offset, out byte b))
            {
                bool val = (b & (1 << display.BitOffset)) != 0;
                return val ? "true" : "false";
            }
            return "?";
        }

        // 收集所需的连续字节
        int size = display.Size;
        if (size <= 0) size = 1;
        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            if (rawBytes.TryGetValue(offset + i, out byte b))
                bytes[i] = b;
            else
                return "?";
        }

        // 已知基本类型解码
        if (SiemensDataTypes.Known.TryGetValue(type, out _))
        {
            return type switch
            {
                "BYTE" or "USINT" => bytes[0].ToString(),
                "SINT" => ((sbyte)bytes[0]).ToString(),
                "CHAR" => ((char)bytes[0]).ToString(),

                "WORD" or "UINT" => ToUInt16BE(bytes).ToString(),
                "INT" => ToInt16BE(bytes).ToString(),
                "DATE" => ToUInt16BE(bytes).ToString(),

                "DWORD" or "UDINT" or "TIME" or "S5TIME" or "TOD" or "TIME_OF_DAY" => ToUInt32BE(bytes).ToString(),
                "DINT" => ToInt32BE(bytes).ToString(),
                "REAL" => ToFloatBE(bytes).ToString("F3"),

                "LREAL" => ToDoubleBE(bytes).ToString("F3"),
                "DT" or "DATE_AND_TIME" => DecodeS7DateTime(bytes),

                "LWORD" or "ULINT" => ToUInt64BE(bytes).ToString(),
                "LINT" => ToInt64BE(bytes).ToString(),

                "BOOL" => (bytes[0] != 0).ToString(),

                _ => BitConverter.ToString(bytes).Replace("-", " ")
            };
        }

        // STRING[n] 特殊处理
        if (type.StartsWith("STRING", StringComparison.OrdinalIgnoreCase))
        {
            int maxLen = size - 2;
            int actualLen = Math.Min(bytes[1], maxLen);
            if (actualLen < 0) actualLen = 0;
            try
            {
                return Encoding.ASCII.GetString(bytes, 2, actualLen);
            }
            catch
            {
                return "(编码错误)";
            }
        }

        // 未知类型：hex 显示
        return $"0x{BitConverter.ToString(bytes).Replace("-", "")}";
    }

    // ===================== 字节序转换 =====================

    private static ushort ToUInt16BE(byte[] b) =>
        (ushort)((b[0] << 8) | b[1]);

    private static short ToInt16BE(byte[] b) =>
        (short)((b[0] << 8) | b[1]);

    private static uint ToUInt32BE(byte[] b) =>
        (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);

    private static int ToInt32BE(byte[] b) =>
        (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];

    private static float ToFloatBE(byte[] b)
    {
        byte[] le = [b[3], b[2], b[1], b[0]];
        return BitConverter.ToSingle(le);
    }

    private static double ToDoubleBE(byte[] b)
    {
        byte[] le = [b[7], b[6], b[5], b[4], b[3], b[2], b[1], b[0]];
        return BitConverter.ToDouble(le);
    }

    private static ulong ToUInt64BE(byte[] b)
    {
        if (b.Length < 8) return 0;
        return ((ulong)b[0] << 56) | ((ulong)b[1] << 48) | ((ulong)b[2] << 40) | ((ulong)b[3] << 32)
             | ((ulong)b[4] << 24) | ((ulong)b[5] << 16) | ((ulong)b[6] << 8) | b[7];
    }

    private static long ToInt64BE(byte[] b) => (long)ToUInt64BE(b);

    private static string DecodeS7DateTime(byte[] b)
    {
        if (b.Length < 8) return "?";
        try
        {
            int year = 2000 + BcdToByte(b[0]);
            int month = BcdToByte(b[1]);
            int day = BcdToByte(b[2]);
            int hour = BcdToByte(b[3]);
            int min = BcdToByte(b[4]);
            int sec = BcdToByte(b[5]);
            return $"{year:D4}-{month:D2}-{day:D2} {hour:D2}:{min:D2}:{sec:D2}";
        }
        catch { return "?"; }
    }

    private static int BcdToByte(byte b) => (b >> 4) * 10 + (b & 0x0F);

    // ===================== 数值写入 =====================

    /// <summary>改动按钮 — 写入数值到 PLC</summary>
    private async void OnValueEdit(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DbVariableDisplay v)
        {
            if (_currentDb == null) return;
            if (!_s7.IsConnected)
            {
                await new Wpf.Ui.Controls.MessageBox { Title = "提示", Content = "PLC 未连接" }.ShowDialogAsync();
                return;
            }
            await WriteEditedValue(v);
        }
    }

    private async Task WriteEditedValue(DbVariableDisplay v)
    {
        if (_currentDb == null) return;
        if (!_s7.IsConnected)
        {
            await new Wpf.Ui.Controls.MessageBox { Title = "提示", Content = "PLC 未连接" }.ShowDialogAsync();
            return;
        }

        string input = v.InputValue?.Trim() ?? "";
        if (input.Length == 0) return;

        string type = v.DataType.Trim().Trim('"').ToUpperInvariant();
        int dbNum = _currentDb.DbNumber;

        byte[]? data = EncodeForWrite(input, type);
        if (data == null)
        {
            operStatusText.Text = $"⚠ 无法解析 \"{input}\" 为 {type}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorCautionBrush", Color.FromRgb(243, 156, 18));
            return;
        }

        if (_s7.WriteBytesRaw(S7Service.AreaDB, v.ByteOffset, data, dbNum))
        {
            v.Value = input;
            operStatusText.Text = $"✅ 写入 {v.Name} = {input}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
        }
        else
        {
            operStatusText.Text = $"❌ 写入失败: {_s7.LastError}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorCriticalBrush", Color.FromRgb(231, 76, 60));
        }
    }

    /// <summary>将字符串按西门子大端编码为字节数组</summary>
    private static byte[]? EncodeForWrite(string input, string type)
    {
        try
        {
            return type switch
            {
                "BYTE" or "USINT" => [byte.Parse(input)],
                "SINT" or "CHAR" => [(byte)sbyte.Parse(input)],
                "INT" => [(byte)(short.Parse(input) >> 8), (byte)short.Parse(input)],
                "UINT" or "WORD" => FromUInt16BE(ushort.Parse(input)),
                "DINT" => FromInt32BE(int.Parse(input)),
                "UDINT" or "DWORD" or "TIME" or "S5TIME" or "TOD" or "TIME_OF_DAY" => FromUInt32BE(uint.Parse(input)),
                "REAL" => FromFloatBE(float.Parse(input)),
                _ => null
            };
        }
        catch { return null; }
    }

    private static byte[] FromUInt16BE(ushort val) => [(byte)(val >> 8), (byte)val];
    private static byte[] FromInt32BE(int val) => [(byte)(val >> 24), (byte)(val >> 16), (byte)(val >> 8), (byte)val];
    private static byte[] FromUInt32BE(uint val) => [(byte)(val >> 24), (byte)(val >> 16), (byte)(val >> 8), (byte)val];
    private static byte[] FromFloatBE(float val)
    {
        byte[] le = BitConverter.GetBytes(val);
        return [le[3], le[2], le[1], le[0]];
    }

    // ===================== UI 辅助 =====================

    private void UpdateDbSizeInfo()
    {
        if (_currentDb == null)
        {
            dbSizeText.Text = "";
            return;
        }

        // 估算总大小：从展开后变量计算终点
        int totalSize = 0;
        if (_variables.Count > 0)
        {
            totalSize = _variables.Max(v => v.ByteOffset + Math.Max(v.Size, 1));
        }

        dbSizeText.Text = $"DB{_currentDb.DbNumber} 共 {totalSize} 字节 | {_variables.Count} 个变量";
    }

    private void UpdateEmptyState()
    {
        bool empty = _variables.Count == 0;
        emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        variableList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    // ===================== BOOL 写入操作 =====================

    /// <summary>按1松0 — 按下设1</summary>
    private void OnBitPressDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement fe && fe.DataContext is DbVariableDisplay v)
        {
            fe.CaptureMouse();
            WriteBit(v, true);
        }
    }

    /// <summary>按1松0 — 松开设0</summary>
    private void OnBitPressUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DbVariableDisplay v)
        {
            fe.ReleaseMouseCapture();
            WriteBit(v, false);
        }
    }

    /// <summary>按1松0 — 鼠标移出也设0</summary>
    private void OnBitPressLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DbVariableDisplay v && fe.IsMouseCaptured)
        {
            fe.ReleaseMouseCapture();
            WriteBit(v, false);
        }
    }

    /// <summary>取反 — 翻转位值</summary>
    private void OnBitToggle(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement fe && fe.DataContext is DbVariableDisplay v)
            ToggleBit(v);
    }

    /// <summary>写入指定位（true=1, false=0）</summary>
    private void WriteBit(DbVariableDisplay v, bool setBit)
    {
        if (_currentDb == null || !_s7.IsConnected) return;

        int dbNum = _currentDb.DbNumber;
        byte? currentByte = _s7.ReadByte(S7Service.AreaDB, v.ByteOffset, dbNum);
        if (!currentByte.HasValue) return;

        byte newVal;
        if (setBit)
            newVal = (byte)(currentByte.Value | (byte)(1 << v.BitOffset));
        else
            newVal = (byte)(currentByte.Value & (byte)~(1 << v.BitOffset));

        if (_s7.WriteByte(S7Service.AreaDB, v.ByteOffset, newVal, dbNum))
        {
            v.Value = setBit ? "true" : "false";
            operStatusText.Text = $"✅ {(setBit ? "按下" : "松开")} {v.Name} = {(setBit ? "true" : "false")}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
        }
    }

    /// <summary>翻转位值</summary>
    private void ToggleBit(DbVariableDisplay v)
    {
        if (_currentDb == null) { operStatusText.Text = "⚠ 未导入 DB"; return; }
        if (!_s7.IsConnected) { operStatusText.Text = "⚠ PLC 未连接"; return; }

        int dbNum = _currentDb.DbNumber;
        byte? currentByte = _s7.ReadByte(S7Service.AreaDB, v.ByteOffset, dbNum);
        if (!currentByte.HasValue)
        {
            operStatusText.Text = $"❌ 读取位失败: {_s7.LastError}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorCriticalBrush", Color.FromRgb(231, 76, 60));
            return;
        }

        byte newVal = (byte)(currentByte.Value ^ (byte)(1 << v.BitOffset));

        if (_s7.WriteByte(S7Service.AreaDB, v.ByteOffset, newVal, dbNum))
        {
            bool isSet = (newVal & (byte)(1 << v.BitOffset)) != 0;
            v.Value = isSet ? "true" : "false";
            operStatusText.Text = $"✅ 取反 {v.Name} = {(isSet ? "true" : "false")}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorSuccessBrush", Color.FromRgb(39, 174, 96));
        }
        else
        {
            operStatusText.Text = $"❌ 取反写入失败: {_s7.LastError}";
            operStatusText.Foreground = GetThemeBrush("SystemFillColorCriticalBrush", Color.FromRgb(231, 76, 60));
        }
    }
}
