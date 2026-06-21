using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf.Controls.DbPanel;

/// <summary>
/// DB 块读取面板 — 导入 .db 文件 → 解析结构 → 读取 PLC 值 → 显示
/// </summary>
public partial class DbPanel : UserControl
{
    private S7Service? _plc;
    private List<UdtStructure> _importedUdts = [];
    private DbStructure? _currentDb;

    private readonly ObservableCollection<DbVariableDisplay> _variables = [];

    // UDT 引用检查缓存
    private HashSet<string>? _missingUdts;

    public DbPanel()
    {
        InitializeComponent();
        listVariables.ItemsSource = _variables;
        UpdateEmptyState();
    }

    /// <summary>注入 S7 服务实例（与主窗口共享）</summary>
    public void Init(S7Service plc)
    {
        _plc = plc;
        UpdateConnectionStatus();
    }

    /// <summary>注入已导入的 UDT 列表（用于解析 UDT 引用）</summary>
    public void SetImportedUdts(IEnumerable<UdtStructure> udts)
    {
        _importedUdts = udts.ToList();
        // 如果已有 DB 加载，重新检测 UDT
        if (_currentDb != null)
            DetectMissingUdts();
    }

    /// <summary>要导入的 DB 结构（外部导入面板共享）</summary>
    public IEnumerable<DbStructure> GetImportedDbs()
        => _currentDb != null ? [_currentDb] : [];

    // ===================== 事件处理 =====================

    /// <summary>导入 .db 文件</summary>
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

        // 设置 DB 号
        if (!int.TryParse(txtDbNumber.Text, out int dbNum) || dbNum <= 0)
            dbNum = 1;

        // 检查 DB 号是否已被当前导入占用
        if (_currentDb != null && _currentDb.DbNumber == dbNum)
        {
            var r = MessageBox.Show(
                $"DB{dbNum} 已导入，是否替换当前数据？", "提示",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        db.DbNumber = dbNum;
        _currentDb = db;
        txtDbNumber.Text = dbNum.ToString();

        // 检测 UDT 引用
        DetectMissingUdts();

        // 展开变量列表（含 UDT 展开）
        ExpandVariables();

        // 更新 UI
        txtDbInfo.Text = $"已导入: {db.Label}";
        txtDbInfo.Foreground = (Brush)FindResource("AccentGreen");
        txtOperationInfo.Text = "";
        UpdateDbSizeInfo();
        UpdateEmptyState();
    }

    /// <summary>清除当前 DB 数据</summary>
    private void OnClear(object sender, RoutedEventArgs e)
    {
        _currentDb = null;
        _variables.Clear();
        txtDbInfo.Text = "未导入 DB 文件";
        txtDbInfo.Foreground = (Brush)FindResource("TextDim");
        txtDbSize.Text = "";
        txtOperationInfo.Text = "";
        txtUdtHint.Text = "";
        UpdateEmptyState();
    }

    /// <summary>读取 DB 块数据</summary>
    private async void OnReadDb(object sender, RoutedEventArgs e)
    {
        if (_plc == null)
        {
            txtOperationInfo.Text = "⚠ 服务未初始化";
            return;
        }
        if (_currentDb == null)
        {
            txtOperationInfo.Text = "⚠ 请先导入 .db 文件";
            return;
        }
        if (!_plc.IsConnected)
        {
            txtOperationInfo.Text = "⚠ 请先连接 PLC";
            return;
        }

        // 如果有未知 UDT 引用，警告但继续
        if (_missingUdts != null && _missingUdts.Count > 0)
        {
            var r = MessageBox.Show(
                $"以下 UDT 未导入，相关变量可能无法正确读取:\n{string.Join("\n", _missingUdts.Select(u => $"  • {u}"))}\n\n是否继续读取？",
                "UDT 引用缺失", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }

        btnReadDb.IsEnabled = false;
        btnReadDb.Content = "⏳ 读取中...";
        txtOperationInfo.Text = "正在读取...";
        txtOperationInfo.Foreground = (Brush)FindResource("TextSecondary");

        try
        {
            await ReadDbValuesAsync();
        }
        catch (Exception ex)
        {
            txtOperationInfo.Text = $"❌ 读取异常: {ex.Message}";
            txtOperationInfo.Foreground = (Brush)FindResource("AccentRed");
        }
        finally
        {
            btnReadDb.IsEnabled = true;
            btnReadDb.Content = "📡 读取 DB 块";
        }
    }

    // ===================== UDT 检测 =====================

    /// <summary>检测当前 DB 中引用的 UDT 是否都已导入</summary>
    private void DetectMissingUdts()
    {
        _missingUdts = null;
        if (_currentDb == null) return;

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(SiemensDataTypes.Known.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var v in _currentDb.Variables)
        {
            string type = v.DataType.Trim().Trim('"');
            string upper = type.ToUpper();

            // 跳过基本类型、STRUCT、STRING（含 IEC_TIMER 等已知类型）
            if (known.Contains(upper)) continue;
            if (upper.StartsWith("STRUCT")) continue;
            if (upper.StartsWith("STRING")) continue;

            // 检查 ARRAY[...] OF X — 提取元素类型 X
            if (upper.StartsWith("ARRAY"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(upper,
                    @"ARRAY\s*\[(\d+)\.\.(\d+)\]\s*OF\s+(.+)");
                if (m.Success)
                {
                    string elemType = m.Groups[3].Value.Trim().Trim('"');
                    if (!known.Contains(elemType) && !elemType.StartsWith("STRUCT"))
                        referenced.Add(elemType);
                }
                continue;
            }

            // 剩下的大概率是 UDT 引用
            referenced.Add(type);
        }

        if (referenced.Count == 0)
        {
            txtUdtHint.Text = "✅ 未检测到 UDT 引用";
            txtUdtHint.Foreground = (Brush)FindResource("AccentGreen");
            return;
        }

        var importedNames = new HashSet<string>(
            _importedUdts.Select(u => u.UdtName), StringComparer.OrdinalIgnoreCase);

        var missing = referenced.Where(r => !importedNames.Contains(r)).ToList();
        var found = referenced.Where(r => importedNames.Contains(r)).ToList();

        if (missing.Count > 0)
        {
            _missingUdts = [.. missing];
            txtUdtHint.Text = $"⚠ 缺少 UDT: {string.Join(", ", missing)}\n" +
                              (found.Count > 0 ? $"✅ 已找到 UDT: {string.Join(", ", found)}" : "");
            txtUdtHint.Foreground = (Brush)FindResource("AccentOrange");
        }
        else
        {
            _missingUdts = null;
            txtUdtHint.Text = $"✅ 所有 UDT 已就绪 ({string.Join(", ", found)})";
            txtUdtHint.Foreground = (Brush)FindResource("AccentGreen");
        }
    }

    // ===================== 变量展开 =====================

    /// <summary>
    /// 展开 DB 变量列表，将 UDT 引用替换为子变量列表
    /// </summary>
    private void ExpandVariables()
    {
        _variables.Clear();
        if (_currentDb == null) return;

        // 重新计算偏移（含 BOOL 位偏移）
        var expanded = RecalculateOffsetsWithBits(_currentDb.Variables);

        foreach (var display in expanded)
            _variables.Add(display);
    }

    /// <summary>
    /// 重新计算偏移量 — BOOL 变量用位偏移（0.0-0.7），其他类型两字节对齐
    /// </summary>
    private List<DbVariableDisplay> RecalculateOffsetsWithBits(List<DbVariable> variables)
    {
        var result = new List<DbVariableDisplay>();
        int byteOffset = 0;
        int bitOffset = 0;

        foreach (var v in variables)
        {
            string upperType = v.DataType.Trim().Trim('"').ToUpper();

            // 检查是否为 UDT 引用（去除引号）
            var udt = FindMatchingUdt(v.DataType.Trim().Trim('"'));
            if (udt != null)
            {
                // UDT 展开 — 对齐到 2 字节边界
                if (bitOffset > 0) { byteOffset++; bitOffset = 0; }
                if (byteOffset % 2 != 0) byteOffset++;

                // 递归展开 UDT 子变量
                int udtBaseOffset = byteOffset;
                int udtBitOffset = 0;

                foreach (var subVar in udt.Variables)
                {
                    string subUpper = subVar.DataType.Trim().ToUpper();
                    if (subUpper == "BOOL")
                    {
                        // BOOL in UDT → bit offset
                        result.Add(new DbVariableDisplay
                        {
                            OffsetDisplay = $"{udtBaseOffset}.{udtBitOffset}",
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
                        // Non-BOOL in UDT — 两字节对齐
                        if (udtBitOffset > 0) { udtBaseOffset++; udtBitOffset = 0; }
                        if (udtBaseOffset % 2 != 0) udtBaseOffset++;
                        SiemensDataTypes.TryResolve(subUpper, out int subSize, out _);

                        result.Add(new DbVariableDisplay
                        {
                            OffsetDisplay = $"{udtBaseOffset}",
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
                    // BOOL → 位偏移
                    result.Add(new DbVariableDisplay
                    {
                        OffsetDisplay = $"{byteOffset}.{bitOffset}",
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
                    // 非 BOOL：清除位偏移 → 两字节（字）对齐
                    if (bitOffset > 0) { byteOffset++; bitOffset = 0; }
                    if (byteOffset % 2 != 0) byteOffset++;

                    result.Add(new DbVariableDisplay
                    {
                        OffsetDisplay = $"{byteOffset}",
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
                    OffsetDisplay = $"{byteOffset}",
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

    /// <summary>查找匹配的 UDT 定义（去除引号后匹配）</summary>
    private UdtStructure? FindMatchingUdt(string typeName)
    {
        string name = typeName.Trim().Trim('"');
        return _importedUdts.FirstOrDefault(u =>
            string.Equals(u.UdtName, name, StringComparison.OrdinalIgnoreCase));
    }

    // ===================== PLC 读取 =====================

    /// <summary>异步读取 DB 块所有变量值</summary>
    private async Task ReadDbValuesAsync()
    {
        if (_plc == null || _currentDb == null) return;

        // 1. 收集所有需要读取的唯一字节地址（每个变量可能有多个字节）
        var allAddresses = _variables
            .SelectMany(v => Enumerable.Range(v.ByteOffset, Math.Max(v.Size, 1)))
            .Distinct()
            .OrderBy(a => a)
            .ToArray();

        if (allAddresses.Length == 0) return;

        int dbNum = _currentDb.DbNumber;

        // 2. 后台线程执行 PLC 读取（S7Service.ReadBytes 内部合并连续段 + 线程安全）
        var rawBytes = await Task.Run(() =>
            _plc.ReadBytes(S7Service.AreaDB, allAddresses, dbNum));

        // 3. 检查读取是否部分失败
        if (rawBytes.Count == 0 && _plc.LastError != null)
        {
            txtOperationInfo.Text = $"❌ 读取失败: {_plc.LastError}";
            txtOperationInfo.Foreground = (Brush)FindResource("AccentRed");
            return;
        }

        // 4. 解码每个变量的值
        int successCount = 0;
        foreach (var display in _variables)
        {
            display.Value = DecodeValue(display, rawBytes);
            if (display.Value != "?") successCount++;
        }

        txtOperationInfo.Text = $"✅ 读取完成 ({successCount}/{_variables.Count} 个变量成功)";
        txtOperationInfo.Foreground = (Brush)FindResource("AccentGreen");
    }

    /// <summary>
    /// 将原始字节解码为可读值（S7 大端字节序）
    /// </summary>
    private string DecodeValue(DbVariableDisplay display, Dictionary<int, byte> rawBytes)
    {
        int offset = display.ByteOffset;
        string type = display.DataType.Trim().ToUpper();

        if (display.IsBit)
        {
            // BOOL — 提取指定位
            if (rawBytes.TryGetValue(offset, out byte b))
            {
                bool val = (b & (1 << display.BitOffset)) != 0;
                return val ? "true" : "false";
            }
            return "?";
        }

        // 收集所需的连续字节
        int size = display.Size;
        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            if (rawBytes.TryGetValue(offset + i, out byte b))
                bytes[i] = b;
            else
                return "?";
        }

        // 基本类型：S7 大端字节序 → 本机（小端）
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

                "DWORD" or "UDINT" or "TIME" or "S5TIME" or "TOD" => ToUInt32BE(bytes).ToString(),
                "DINT" => ToInt32BE(bytes).ToString(),
                "REAL" => ToFloatBE(bytes).ToString("F3"),
                "LREAL" => ToDoubleBE(bytes).ToString("F3"),
                "DT" => DecodeS7DateTime(bytes),

                "LWORD" or "ULINT" => ToUInt64BE(bytes).ToString(),
                "LINT" => ToInt64BE(bytes).ToString(),

                "BOOL" => (bytes[0] != 0).ToString(),

                _ => BitConverter.ToString(bytes).Replace("-", " ")
            };
        }

        // STRING[n] 特殊处理
        if (type.StartsWith("STRING"))
        {
            int maxLen = size - 2; // 去除 2 字节头
            int actualLen = Math.Min(bytes[1], maxLen); // bytes[1] = 实际长度
            if (actualLen > size - 2) actualLen = size - 2;
            if (actualLen < 0) actualLen = 0;
            try
            {
                return System.Text.Encoding.ASCII.GetString(bytes, 2, actualLen);
            }
            catch
            {
                return "(编码错误)";
            }
        }

        // DATE_AND_TIME (DT): BCD 编码的 8 字节
        if (type == "DT")
            return DecodeS7DateTime(bytes);

        // 其他未知类型：显示 hex
        return $"0x{BitConverter.ToString(bytes).Replace("-", "")}";
    }

    // ===================== 字节序转换 =====================

    private static ushort ToUInt16BE(byte[] b) => (ushort)((b[0] << 8) | b[1]);
    private static short ToInt16BE(byte[] b) => (short)((b[0] << 8) | b[1]);
    private static uint ToUInt32BE(byte[] b) =>
        (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    private static int ToInt32BE(byte[] b) =>
        (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    private static float ToFloatBE(byte[] b)
    {
        // IEEE 754 big-endian → 先反转成小端再转换
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

    /// <summary>S7 DT (DATE_AND_TIME) 解码 — BCD 编码 8 字节</summary>
    private static string DecodeS7DateTime(byte[] b)
    {
        if (b.Length < 8) return "?";
        try
        {
            int year  = 2000 + BcdToByte(b[0]);
            int month = BcdToByte(b[1]);
            int day   = BcdToByte(b[2]);
            int hour  = BcdToByte(b[3]);
            int min   = BcdToByte(b[4]);
            int sec   = BcdToByte(b[5]);
            // b[6] 保留/星期几
            // b[7] ±时区
            return $"{year:D4}-{month:D2}-{day:D2} {hour:D2}:{min:D2}:{sec:D2}";
        }
        catch { return "?"; }
    }

    private static int BcdToByte(byte b) => (b >> 4) * 10 + (b & 0x0F);

    // ===================== UI 辅助 =====================

    private void UpdateConnectionStatus()
    {
        bool connected = _plc?.IsConnected ?? false;
        statusIndicator.Fill = connected
            ? (Brush)FindResource("AccentGreen")
            : (Brush)FindResource("AccentRed");
        txtStatus.Text = connected ? "已连接" : "未连接";
    }

    private void UpdateDbSizeInfo()
    {
        if (_currentDb != null)
            txtDbSize.Text = $"| DB{_currentDb.DbNumber} 共 {_currentDb.TotalSize} 字节 | {_variables.Count} 个变量";
        else
            txtDbSize.Text = "";
    }

    private void UpdateEmptyState()
    {
        bool empty = _variables.Count == 0;
        emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        listVariables.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>定期调用以更新连接状态（主窗口轮询）</summary>
    public void RefreshConnectionStatus()
    {
        UpdateConnectionStatus();
    }
}
