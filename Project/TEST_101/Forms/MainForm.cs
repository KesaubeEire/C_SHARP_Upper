using System;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;
using TEST_101.Alarm;
using TEST_101.Chart;
using TEST_101.Core;
using TEST_101.Recipe;
using TEST_101.Report;
using TEST_101.Storage;

namespace TEST_101.Forms
{
    /// <summary>
    /// 主界面 —— TabControl 容器
    ///
    /// 控件布局 → MainForm.Designer.cs（设计器可编辑）
    /// 事件绑定 + 动态数据 → 本文件
    /// </summary>
    public partial class MainForm : Form
    {
        // 核心服务
        private DatabaseManager? _database;
        private AlarmManager? _alarmManager;
        private RecipeManager? _recipeManager;
        private ReportGenerator? _reportGenerator;
        private ChartDataManager? _chartDataManager;

        // ──── Modbus 模块核心组件 ────
        private ModbusTransport _mb_transport = null!;
        private InputHistoryManager _mb_history = null!;
        private ModbusPollingService _mb_pollingService = null!;
        private bool _mb_isTcpMode = false;
        private string _mb_lastDeviceId = "1";
        private ushort _mb_lastStartAddr = 0;
        private HistoryDropDown? _mb_currentDropdown;

        public MainForm()
        {
            InitializeComponent();   // Designer 生成的控件布局
            BindEvents();            // 事件绑定
            InitDynamicControls();   // 运行时动态创建的控件（如 ScottPlot）
            InitializeServices();    // 后端服务
        }

        // ========== 事件绑定 ==========

        private void BindEvents()
        {
            // 时间更新
            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => _lbTime.Text = "🕐 " + DateTime.Now.ToString("HH:mm:ss");
            timer.Start();

            // Modbus 事件绑定
            BindModbusEvents();
        }

        /// <summary>绑定所有 Modbus 控件的事件处理器</summary>
        private void BindModbusEvents()
        {
            _mb_btn_refresh.Click += mb_btn_refresh_Click;
            _mb_btn_open.Click += mb_btn_open_Click;
            _mb_btn_read.Click += mb_btn_read_Click;
            _mb_btn_clear.Click += mb_btn_clear_Click;
            _mb_drop_mode.SelectedIndexChanged += mb_drop_mode_SelectedIndexChanged;
            _mb_btn_learn.Click += (s, e) => ShowLearnDialog();
        }

        // ========== 动态控件（设计器做不了的） ==========

        private void InitDynamicControls()
        {
            // 初始化 Modbus 模块
            InitModbusModule();

            // ScottPlot 曲线控件（自定义 UserControl，无法在设计器中拖放）
            var chartControl = new RealtimeChartControl { Dock = DockStyle.Fill };
            _panelChartArea.Controls.Add(chartControl);

            // 初始化图表数据管理器
            _chartDataManager = new ChartDataManager(chartControl);

            // 报警规则示例数据
            _gridAlarmRules.Rows.Add("伺服过速", "PLC-1", "D100", "> 1500", "故障", true);
            _gridAlarmRules.Rows.Add("变频器过流", "PLC-1", "D202", "> 10.0", "警告", true);
            _gridAlarmRules.Rows.Add("温度过高", "PLC-2", "D300", "> 80.0", "紧急", true);

            // 配方示例数据
            _gridRecipes.Rows.Add("产品A-标准", "2024-01-15 10:30", "12", "v1.2");
            _gridRecipes.Rows.Add("产品A-快速", "2024-01-15 11:00", "12", "v1.0");
            _gridRecipes.Rows.Add("产品B-标准", "2024-01-16 09:00", "15", "v2.1");

            // 配方参数示例数据
            _gridRecipeParams.Rows.Add("1", "伺服转速", "D100", "1000", "1200", "rpm");
            _gridRecipeParams.Rows.Add("2", "伺服转矩限制", "D102", "100", "100", "%");
            _gridRecipeParams.Rows.Add("3", "变频器频率", "D200", "50", "50", "Hz");
            _gridRecipeParams.Rows.Add("4", "加速时间", "D204", "1000", "1000", "ms");

            // 报表示例数据
            _gridReport.Rows.Add("08:00-09:00", "125", "123", "2", "3.1s", "正常");
            _gridReport.Rows.Add("09:00-10:00", "130", "128", "2", "3.0s", "正常");
            _gridReport.Rows.Add("10:00-11:00", "128", "125", "3", "3.3s", "正常");
            _gridReport.Rows.Add("11:00-12:00", "135", "133", "2", "2.9s", "正常");
        }

        // ========== Modbus 模块初始化 ==========

        /// <summary>初始化 Modbus 核心组件和 UI 默认值（原 ModbusForm_Load + InitUI）</summary>
        private void InitModbusModule()
        {
            // 初始化核心组件
            _mb_transport = new ModbusTransport(this, () => _mb_isTcpMode);
            _mb_transport.FrameReceived += OnFrameReceived;
            _mb_transport.ErrorOccurred += OnError;
            _mb_transport.ConnectionChanged += OnConnectionChanged;

            _mb_history = new InputHistoryManager();

            // ★ 初始化轮询服务
            _mb_pollingService = new ModbusPollingService(_mb_transport, () => _mb_isTcpMode);
            _mb_pollingService.DataReceived += OnPollingDataReceived;
            _mb_pollingService.DeviceOnlineChanged += OnPollingDeviceOnlineChanged;
            _mb_pollingService.ServiceStateChanged += OnPollingServiceStateChanged;

            InitModbusDefaults();
            RefreshComPorts();
        }

        /// <summary>设置 Modbus UI 默认值（原 InitUI）</summary>
        private void InitModbusDefaults()
        {
            _mb_drop_baud.Text = "9600";

            // 模式
            _mb_drop_mode.SelectedIndex = 0;
            _mb_isTcpMode = false;
            ShowTcpControls(false);

            // 功能码
            _mb_drop_func.Items.Clear();
            _mb_drop_func.Items.Add("01 读线圈");
            _mb_drop_func.Items.Add("02 读离散输入");
            _mb_drop_func.Items.Add("03 读保持寄存器");
            _mb_drop_func.Items.Add("04 读输入寄存器");
            _mb_drop_func.Items.Add("05 写单线圈");
            _mb_drop_func.Items.Add("06 写单寄存器");
            _mb_drop_func.Items.Add("15 写多线圈");
            _mb_drop_func.Items.Add("16 写多寄存器");
            _mb_drop_func.SelectedIndex = 2;

            // 默认值
            _mb_box_dev.Text = "1";
            _mb_box_addr.Text = "0";
            _mb_box_count.Text = "10";

            _mb_drop_stop.SelectedIndex = 0;
            _mb_drop_parity.SelectedIndex = 0;

            // DataGridView 初始列
            SetupGridColumns(false);

            // 为每个输入框添加历史下拉按钮
            AddHistoryButton(_mb_box_dev, "dev_addr");
            AddHistoryButton(_mb_box_addr, "start_addr");
            AddHistoryButton(_mb_box_count, "count");
            AddHistoryButton(_mb_box_ip, "tcp_ip");
            AddHistoryButton(_mb_box_port, "tcp_port");
        }

        /// <summary>弹出学习对话框</summary>
        private void ShowLearnDialog()
        {
            var output = new System.Text.StringBuilder();

            var originalOut = Console.Out;
            using var writer = new System.IO.StringWriter();
            Console.SetOut(writer);

            CSharpConceptsDemo.CurrentIsTcpMode = () => _mb_isTcpMode;

            CSharpConceptsDemo.Demo01_Delegates_FuncAndAction();
            output.AppendLine(writer.GetStringBuilder().ToString());
            writer.GetStringBuilder().Clear();

            output.AppendLine(new string('─', 55));
            CSharpConceptsDemo.Demo02_Lambda_ThreeWays();
            output.AppendLine(writer.GetStringBuilder().ToString());
            writer.GetStringBuilder().Clear();

            output.AppendLine(new string('─', 55));
            CSharpConceptsDemo.Demo03_Events_PubSub();
            output.AppendLine(writer.GetStringBuilder().ToString());
            writer.GetStringBuilder().Clear();

            output.AppendLine(new string('─', 55));
            output.AppendLine(CSharpConceptsDemo.Demo04_Reflection_ScanOurProtocol());
            writer.GetStringBuilder().Clear();

            output.AppendLine(new string('─', 55));
            CSharpConceptsDemo.Demo05_AllTogether();
            output.AppendLine(writer.GetStringBuilder().ToString());
            writer.GetStringBuilder().Clear();

            output.AppendLine(new string('─', 55));
            output.AppendLine(CSharpConceptsDemo.GetQuickReference());

            Console.SetOut(originalOut);

            var form = new Form
            {
                Text = "🔬 C# 高级语法 — 基于本项目代码的学习演示",
                Size = new Size(900, 650),
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Consolas", 10F)
            };

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(0, 255, 0),
                Font = new Font("Consolas", 10F),
                Text = output.ToString(),
                WordWrap = false,
                TabStop = false
            };
            textBox.Select(0, 0);
            form.Controls.Add(textBox);
            form.ShowDialog();
        }

        /// <summary>在文本框右侧动态添加历史下拉按钮（不碰 Designer）</summary>
        private void AddHistoryButton(TextBox target, string fieldKey)
        {
            var btn = new Button
            {
                Text = "▾",
                Width = 12,
                Height = target.Height,
                Font = new Font("Microsoft YaHei", 8F),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(230, 230, 230),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Location = new Point(target.Right + 2, target.Top);
            btn.Click += (s, e) =>
            {
                if (_mb_currentDropdown != null && !_mb_currentDropdown.IsDisposed)
                {
                    _mb_currentDropdown.Close();
                    _mb_currentDropdown = null;
                    return;
                }

                var dropdown = new HistoryDropDown(_mb_history, target, fieldKey);
                dropdown.FormClosed += (_, _) => _mb_currentDropdown = null;
                _mb_currentDropdown = dropdown;
                dropdown.ShowDropdown();
            };
            target.Parent!.Controls.Add(btn);
        }

        // ========== 刷新 COM 口 ==========
        private void RefreshComPorts()
        {
            string currentCom = _mb_drop_com.Text ?? string.Empty;
            _mb_drop_com.Items.Clear();
            _mb_drop_com.Text = "";

            string[] ports = _mb_transport.GetPortNames().Distinct().ToArray();

            if (ports.Length == 0)
            {
                _mb_lb_status.Text = "未检测到可用 COM 口";
                return;
            }

            foreach (string port in ports)
                _mb_drop_com.Items.Add(port);

            if (_mb_drop_com.Items.Contains(currentCom))
                _mb_drop_com.Text = currentCom;
            else
                _mb_drop_com.SelectedIndex = 0;

            _mb_lb_status.Text = $"检测到 {ports.Length} 个 COM 口";
        }

        // ========== Transport 事件回调（已在 UI 线程）==========

        private void OnFrameReceived(byte[] buffer, bool isTcp)
        {
            // 1. 着色显示原始帧
            byte funcCode = buffer.Length >= 2 ? buffer[1] : (byte)0;
            string hex = BitConverter.ToString(buffer).Replace("-", " ");
            ColorizeHexFrame(_mb_box_recv_hex, hex, funcCode, isTcp);
            _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 接收 → {hex}\r\n");

            // 2. 如果是 TCP，跳过 MBAP 头再解析
            byte[] pduBuf = isTcp && buffer.Length > ModbusProtocol.MBAP_HEADER_SIZE
                ? buffer.Skip(ModbusProtocol.MBAP_HEADER_SIZE).ToArray()
                : buffer;

            // 3. 解析
            var result = ModbusProtocol.ParseResponse(pduBuf);
            FillGrid(result);

            // 4. 桥接到 EventBus → 图表 / 报警 / 报表都能收到数据
            if (!result.IsError && result.Registers.Count > 0)
            {
                var values = result.Registers.Select(r => r.Value).ToArray();
                EventBus.Instance.Publish(new DataUpdatedEvent(
                    DeviceId: _mb_lastDeviceId,
                    StartAddress: _mb_lastStartAddr,
                    Values: values,
                    Timestamp: DateTime.Now
                ));
            }
        }

        private void OnError(string msg)
        {
            _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
        }

        private void OnConnectionChanged(bool connected, string statusText)
        {
            _mb_lb_status.Text = statusText;
            if (connected)
            {
                _mb_btn_open.Text = _mb_isTcpMode ? "断开" : "关闭串口";
                _mb_btn_open.BackColor = Color.FromArgb(220, 80, 80);
            }
            else
            {
                _mb_btn_open.Text = _mb_isTcpMode ? "连接" : "打开串口";
                _mb_btn_open.BackColor = Color.FromArgb(60, 140, 60);
            }

            // 桥接到 EventBus → MainForm 状态栏同步更新
            EventBus.Instance.Publish(new ConnectionChangedEvent(
                DeviceId: _mb_lastDeviceId,
                IsConnected: connected,
                StatusMessage: statusText
            ));
        }

        // ========== 填充 DataGridView ==========

        private void FillGrid(ModbusParseResult result)
        {
            if (result.IsError)
            {
                SetupGridColumns(false);
                _mb_grid_result.Rows.Add("—", $"❌ {result.ErrorMessage}", "", "", "", "");
                return;
            }

            if (result.Bits.Count > 0)
            {
                SetupGridColumns(true);
                foreach (var bit in result.Bits)
                    _mb_grid_result.Rows.Add(bit.Index.ToString(), bit.IsOn ? "ON" : "OFF", $"0x{bit.RawByte:X2}");
            }
            else if (result.Registers.Count > 0)
            {
                SetupGridColumns(false);
                foreach (var reg in result.Registers)
                {
                    string dec = reg.Value.ToString();
                    string hex = $"0x{reg.Value:X4}";
                    string bin = FormatBinary(reg.Value);
                    string oct = Convert.ToString(reg.Value, 8);
                    string signed = ((short)reg.Value).ToString();
                    _mb_grid_result.Rows.Add(reg.Index.ToString(), dec, hex, bin, oct, signed);
                }
            }
            else
            {
                // 其他功能码（无数据）
            }
        }

        // ========== 模式切换 ==========
        private void mb_drop_mode_SelectedIndexChanged(object sender, EventArgs e)
        {
            _mb_isTcpMode = _mb_drop_mode.SelectedIndex == 1;

            if (!_mb_isTcpMode)
                _mb_transport.DisconnectTcp();
            else if (_mb_transport.IsSerialOpen)
            {
                _mb_transport.CloseSerial();
                _mb_btn_open.Text = "打开串口";
                _mb_btn_open.BackColor = Color.FromArgb(60, 140, 60);
            }

            ShowTcpControls(_mb_isTcpMode);
            _mb_lb_legend.Text = _mb_isTcpMode
                ? "TCP图例：MBAP头=钢蓝  单元ID=灰  功能码(01=蓝 02=绿 03=紫 04=黄 错误=红)  数据=黄"
                : "RTU图例：地址=灰  功能码(01=蓝 02=绿 03=紫 04=黄 错误=红)  数据=黄  CRC=橙";
            _mb_lb_status.Text = "已断开";
        }

        private void ShowTcpControls(bool showTcp)
        {
            _mb_lb_ip.Visible = showTcp;
            _mb_box_ip.Visible = showTcp;
            _mb_lb_port.Visible = showTcp;
            _mb_box_port.Visible = showTcp;

            _mb_lb_com.Visible = !showTcp;
            _mb_drop_com.Visible = !showTcp;
            _mb_lb_baud.Visible = !showTcp;
            _mb_drop_baud.Visible = !showTcp;
            _mb_lb_stop.Visible = !showTcp;
            _mb_drop_stop.Visible = !showTcp;
            _mb_lb_parity.Visible = !showTcp;
            _mb_drop_parity.Visible = !showTcp;
            _mb_btn_refresh.Visible = !showTcp;

            _mb_btn_open.Text = showTcp ? "连接" : "打开串口";
        }

        // ========== 打开/关闭连接 ==========
        private void mb_btn_open_Click(object sender, EventArgs e)
        {
            if (_mb_isTcpMode)
            {
                if (_mb_transport.IsTcpConnected)
                    _mb_transport.DisconnectTcp();
                else
                    _mb_transport.ConnectTcp(_mb_box_ip.Text.Trim(), int.Parse(_mb_box_port.Text.Trim()));
                return;
            }

            if (!_mb_transport.IsSerialOpen)
            {
                if (string.IsNullOrWhiteSpace(_mb_drop_com.Text))
                {
                    MessageBox.Show("请先选择一个 COM 口");
                    return;
                }
                try
                {
                    _mb_transport.OpenSerial(_mb_drop_com.Text, int.Parse(_mb_drop_baud.Text),
                        ParseStopBits(_mb_drop_stop.Text), ParseParity(_mb_drop_parity.Text));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开串口失败：" + ex.Message);
                }
            }
            else
            {
                _mb_transport.CloseSerial();
            }
        }

        // ========== 发送按钮 ==========
        private void mb_btn_read_Click(object sender, EventArgs e)
        {
            // 连接检查
            if (_mb_isTcpMode && !_mb_transport.IsTcpConnected)
            {
                MessageBox.Show("请先连接 TCP");
                return;
            }
            if (!_mb_isTcpMode && !_mb_transport.IsSerialOpen)
            {
                MessageBox.Show("请先打开串口");
                return;
            }

            try
            {
                byte devAddr = byte.Parse(_mb_box_dev.Text.Trim());
                string funcStr = _mb_drop_func.Text.Substring(0, 2);
                byte funcCode = byte.Parse(funcStr);

                string addrText = _mb_box_addr.Text.Trim();
                ushort startAddr = addrText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToUInt16(addrText.Substring(2), 16)
                    : ushort.Parse(addrText);

                // 记录请求参数，用于收到响应后桥接到 EventBus
                _mb_lastDeviceId = devAddr.ToString();
                _mb_lastStartAddr = startAddr;

                ushort count = ushort.Parse(_mb_box_count.Text.Trim());

                // 协议数量校验
                int maxCount = ModbusProtocol.GetMaxCount(funcCode);
                if (maxCount > 0 && count > maxCount)
                {
                    MessageBox.Show(
                        $"功能码 {funcStr} 单次最多读 {maxCount} 个，\n" +
                        $"当前填写了 {count} 个，超出协议限制。\n\n" +
                        $"建议：分多次读取，每次不超过 {maxCount}。",
                        "数量超限", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 写功能码检查
                if (funcCode == 0x05 || funcCode == 0x06 || funcCode == 0x0F || funcCode == 0x10)
                {
                    MessageBox.Show(
                        $"功能码 {funcStr}（{_mb_drop_func.Text.Substring(3)}）的写入功能暂未实现。\n\n" +
                        "当前仅支持读取功能码：01 读线圈、02 读离散输入、03 读保持寄存器、04 读输入寄存器。",
                        "功能未实现", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 记录历史
                _mb_history.Add("dev_addr", _mb_box_dev.Text.Trim());
                _mb_history.Add("start_addr", addrText);
                _mb_history.Add("count", _mb_box_count.Text.Trim());
                if (_mb_isTcpMode)
                {
                    _mb_history.Add("tcp_ip", _mb_box_ip.Text.Trim());
                    _mb_history.Add("tcp_port", _mb_box_port.Text.Trim());
                }

                // ★ 如果轮询正在运行，把请求入队（共享队列）
                if (_mb_pollingService.IsRunning)
                {
                    _mb_pollingService.Enqueue(new ModbusRequest(devAddr, funcCode, startAddr, count,
                        $"手动#{devAddr} @{startAddr}"));
                    _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 📥 手动请求已入队 (Dev#{devAddr}, @{startAddr})\r\n");
                }
                else
                {
                    // 直接发送（原有逻辑）
                    var (frame, fc) = _mb_transport.SendReadRequest(devAddr, funcCode, startAddr, count);
                    ColorizeHexFrame(_mb_box_send_hex, BitConverter.ToString(frame).Replace("-", " "), fc, _mb_isTcpMode);
                    _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 发送 → {_mb_box_send_hex.Text}\r\n");
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("参数格式错误，请检查设备地址、起始地址、数量的输入");
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成指令失败：" + ex.Message);
            }
        }

        // ========== ★ 轮询服务 ==========

        private void _mb_btn_polling_Click(object? sender, EventArgs e)
        {
            if (_mb_pollingService.IsRunning)
            {
                _mb_pollingService.Stop();
                _mb_btn_polling.Text = "▶ 轮询";
                _mb_btn_polling.BackColor = Color.FromArgb(60, 140, 60);
                _mb_btn_polling.ForeColor = Color.White;
                _mb_lb_polling_status.Visible = false;
                _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] ⏹ 轮询已停止\r\n");
            }
            else
            {
                if (_mb_isTcpMode && !_mb_transport.IsTcpConnected)
                { MessageBox.Show("请先连接 TCP"); return; }
                if (!_mb_isTcpMode && !_mb_transport.IsSerialOpen)
                { MessageBox.Show("请先打开串口"); return; }

                try
                {
                    byte devAddr = byte.Parse(_mb_box_dev.Text.Trim());
                    string funcStr = _mb_drop_func.Text.Substring(0, 2);
                    byte funcCode = byte.Parse(funcStr);
                    string addrText = _mb_box_addr.Text.Trim();
                    ushort startAddr = addrText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToUInt16(addrText.Substring(2), 16)
                        : ushort.Parse(addrText);
                    ushort count = ushort.Parse(_mb_box_count.Text.Trim());

                    _mb_pollingService.ClearPollingConfigs();
                    _mb_pollingService.AddPollingConfig(new PollingConfig
                    {
                        DeviceAddr = devAddr,
                        FuncCode = funcCode,
                        StartAddr = startAddr,
                        Count = count,
                        IntervalMs = 1000,
                        Tag = $"Dev#{devAddr} @{startAddr}"
                    });

                    _mb_pollingService.StartPolling();
                    _mb_btn_polling.Text = "⏹ 停止";
                    _mb_btn_polling.BackColor = Color.FromArgb(200, 70, 70);
                    _mb_btn_polling.ForeColor = Color.White;
                    _mb_lb_polling_status.Visible = true;
                    UpdatePollingStatusText();
                    _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] ▶ 轮询已启动 (Dev#{devAddr}, F{funcStr}, @{startAddr}, x{count})\r\n");
                }
                catch (FormatException)
                {
                    MessageBox.Show("轮询参数格式错误，请检查设备地址、起始地址、数量");
                }
            }
        }

        private void OnPollingDataReceived(PollingResult result)
        {
            if (IsDisposed) return;
            Invoke(() =>
            {
                string hex = result.RawFrame.Length > 0
                    ? BitConverter.ToString(result.RawFrame).Replace("-", " ")
                    : "(无响应)";
                string prefix = result.IsTimeout ? "⏱" : result.ParseResult.IsError ? "❌" : "✅";
                _mb_box_recv.AppendText($"[{result.Request.Tag}] {prefix} [{DateTime.Now:HH:mm:ss}] {hex}" +
                    $" ({(int)result.Elapsed.TotalMilliseconds}ms)\r\n");

                if (!result.ParseResult.IsError && result.RawFrame.Length > 0)
                {
                    byte funcCode = result.RawFrame.Length >= 2 ? result.RawFrame[1] : result.Request.FuncCode;
                    ColorizeHexFrame(_mb_box_recv_hex, hex, funcCode, _mb_isTcpMode);
                    FillGrid(result.ParseResult);
                }

                if (!result.ParseResult.IsError && result.ParseResult.Registers.Count > 0)
                {
                    var values = result.ParseResult.Registers.Select(r => r.Value).ToArray();
                    EventBus.Instance.Publish(new DataUpdatedEvent(
                        DeviceId: result.Request.DeviceAddr.ToString(),
                        StartAddress: result.Request.StartAddr,
                        Values: values,
                        Timestamp: DateTime.Now
                    ));
                }

                UpdatePollingStatusText();
            });
        }

        private void OnPollingDeviceOnlineChanged(byte address, bool isOnline)
        {
            if (IsDisposed) return;
            Invoke(() =>
            {
                string status = isOnline ? "🟢 在线" : "🔴 离线";
                _mb_box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] Dev#{address} → {status}\r\n");
                UpdatePollingStatusText();
            });
        }

        private void OnPollingServiceStateChanged(bool isRunning)
        {
            if (IsDisposed) return;
            Invoke(() =>
            {
                if (!isRunning && !_mb_btn_polling.IsDisposed)
                {
                    _mb_btn_polling.Text = "▶ 轮询";
                    _mb_btn_polling.BackColor = Color.FromArgb(60, 140, 60);
                    _mb_btn_polling.ForeColor = Color.White;
                    _mb_lb_polling_status.Visible = false;
                }
            });
        }

        private void UpdatePollingStatusText()
        {
            if (!_mb_lb_polling_status.Visible) return;

            var stats = _mb_pollingService.GetStats();
            var configs = _mb_pollingService.PollingConfigs;

            string devices = string.Join(", ",
                configs.Select(c =>
                {
                    var state = _mb_pollingService.GetDeviceState(c.DeviceAddr);
                    string icon = state.IsOnline ? "🟢" : "🔴";
                    string skipInfo = state.ShouldSkip()
                        ? $" (退避{state.SecondsUntilRetry():F0}s)" : "";
                    return $"{icon} {c.Tag}{skipInfo}";
                }));

            _mb_lb_polling_status.Text = $"[队列={stats.QueueLength}] [成功率={stats.SuccessRate}%]" +
                $" [已发={stats.RequestsSent}] [失败={stats.RequestsFailed}]   {devices}";
        }

        // ========== DataGridView 列初始化 ==========
        private void SetupGridColumns(bool isBit)
        {
            _mb_grid_result.Columns.Clear();
            _mb_grid_result.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _mb_grid_result.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (isBit)
            {
                var colIdx = _mb_grid_result.Columns.Add("Col_Index", "序号");
                _mb_grid_result.Columns[colIdx].Width = 50;
                _mb_grid_result.Columns[colIdx].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colState = _mb_grid_result.Columns.Add("Col_State", "状态");
                _mb_grid_result.Columns[colState].Width = 65;
                _mb_grid_result.Columns[colState].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colRaw = _mb_grid_result.Columns.Add("Col_RawByte", "原始字节");
                _mb_grid_result.Columns[colRaw].Width = 90;
                _mb_grid_result.Columns[colRaw].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            else
            {
                var colIdx = _mb_grid_result.Columns.Add("Col_Index", "序号");
                _mb_grid_result.Columns[colIdx].Width = 50;
                _mb_grid_result.Columns[colIdx].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colDEC = _mb_grid_result.Columns.Add("Col_DEC", "DEC");
                _mb_grid_result.Columns[colDEC].Width = 65;
                _mb_grid_result.Columns[colDEC].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colHEX = _mb_grid_result.Columns.Add("Col_HEX", "HEX");
                _mb_grid_result.Columns[colHEX].Width = 70;
                _mb_grid_result.Columns[colHEX].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _mb_grid_result.Columns[colHEX].DefaultCellStyle.Font = new Font("Consolas", 10F);

                var colBIN = _mb_grid_result.Columns.Add("Col_BIN", "BIN");
                _mb_grid_result.Columns[colBIN].Width = 170;
                _mb_grid_result.Columns[colBIN].DefaultCellStyle.Font = new Font("Consolas", 10F);

                var colOCT = _mb_grid_result.Columns.Add("Col_OCT", "OCT");
                _mb_grid_result.Columns[colOCT].Width = 70;
                _mb_grid_result.Columns[colOCT].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _mb_grid_result.Columns[colOCT].DefaultCellStyle.Font = new Font("Consolas", 10F);

                var colSigned = _mb_grid_result.Columns.Add("Col_Signed", "有符号");
                _mb_grid_result.Columns[colSigned].Width = 70;
                _mb_grid_result.Columns[colSigned].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        // ========== 逐字段着色（UI 层 — 依赖 RichTextBox）==========
        private static void ColorizeHexFrame(RichTextBox rtb, string hexText, byte funcCode, bool isTcp = false)
        {
            rtb.Clear();
            rtb.AppendText(hexText);

            string[] bytes = hexText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int totalBytes = bytes.Length;
            if (totalBytes < 3) return;

            int pos = 0;
            for (int i = 0; i < totalBytes; i++)
            {
                Color color;
                if (isTcp && i < 6)
                    color = Color.FromArgb(210, 225, 240);       // MBAP头 → 浅钢蓝
                else if (isTcp && i == 6)
                    color = Color.FromArgb(225, 225, 225);       // 单元ID → 浅灰
                else if (!isTcp && i == 0)
                    color = Color.FromArgb(225, 225, 225);       // 地址 → 浅灰
                else if ((isTcp && i == 7) || (!isTcp && i == 1))
                    color = GetFuncCodeColor(funcCode);           // 功能码 → 按类型
                else if (!isTcp && i >= totalBytes - 2)
                    color = Color.FromArgb(255, 225, 190);        // CRC → 浅橙
                else
                    color = Color.FromArgb(255, 255, 210);        // 数据 → 浅黄

                rtb.Select(pos, 2);
                rtb.SelectionBackColor = color;
                pos += 2;
                if (i < totalBytes - 1) pos++;
            }
            rtb.Select(0, 0);
        }

        private static Color GetFuncCodeColor(byte funcCode)
        {
            if ((funcCode & 0x80) != 0)
                return Color.FromArgb(255, 215, 215);

            return funcCode switch
            {
                0x01 => Color.FromArgb(220, 238, 255),
                0x02 => Color.FromArgb(220, 255, 225),
                0x03 => Color.FromArgb(240, 225, 255),
                0x04 => Color.FromArgb(255, 248, 200),
                0x05 or 0x06 or 0x0F or 0x10 => Color.FromArgb(255, 225, 225),
                _ => Color.FromArgb(245, 245, 245)
            };
        }

        // ========== 二进制格式化 ==========
        private static string FormatBinary(ushort value)
        {
            string bin = Convert.ToString(value, 2).PadLeft(16, '0');
            return $"{bin.Substring(0, 4)} {bin.Substring(4, 4)} {bin.Substring(8, 4)} {bin.Substring(12, 4)}";
        }

        // ========== 停止位 / 校验位解析 ==========
        private static StopBits ParseStopBits(string text) => text switch
        {
            "1.5" => StopBits.OnePointFive,
            "2" => StopBits.Two,
            _ => StopBits.One
        };

        private static Parity ParseParity(string text) => text switch
        {
            "奇校验" => Parity.Odd,
            "偶校验" => Parity.Even,
            "Mark" => Parity.Mark,
            "Space" => Parity.Space,
            _ => Parity.None
        };

        // ========== Modbus 控件事件 ==========
        private void mb_btn_refresh_Click(object sender, EventArgs e) => RefreshComPorts();

        private void mb_btn_clear_Click(object sender, EventArgs e)
        {
            _mb_box_recv.Clear();
            _mb_box_send_hex.Clear();
            _mb_box_recv_hex.Clear();
            _mb_grid_result.Rows.Clear();
        }

        // ========== ESC 关闭下拉面板 ==========
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _mb_currentDropdown != null && !_mb_currentDropdown.IsDisposed)
            {
                _mb_currentDropdown.Close();
                _mb_currentDropdown = null;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ========== 后端服务初始化 ==========

        private void InitializeServices()
        {
            try
            {
                _database = DatabaseManager.CreateSQLite("monitor.db");

                _alarmManager = new AlarmManager(_database);
                _recipeManager = new RecipeManager(_database);
                _reportGenerator = new ReportGenerator(_database);

                // 报警事件 → UI 更新
                _alarmManager.OnAlarmTriggered += alarm =>
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        Invoke(() =>
                        {
                            _lbAlarmCount.Text = $"⚠️ 报警: {_alarmManager.GetUnconfirmedAlarms().Count}";
                            MessageBox.Show(
                                $"报警: {alarm.RuleName}\n设备: {alarm.DeviceId}\n当前值: {alarm.CurrentValue:F2}",
                                "报警提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        });
                    }
                    catch (ObjectDisposedException) { }
                };

                // 连接状态事件 → 状态栏
                EventBus.Instance.Subscribe<ConnectionChangedEvent>(e =>
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        Invoke(() =>
                        {
                            _lbConnectionStatus.Text = e.IsConnected
                                ? $"📡 设备: {e.DeviceId} 已连接"
                                : "📡 设备: 未连接";
                        });
                    }
                    catch (ObjectDisposedException) { }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化服务失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ========== 窗口关闭 ==========

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _mb_pollingService?.Dispose();
            _mb_transport?.Dispose();
            _chartDataManager?.Dispose();
            _reportGenerator?.Dispose();
            _recipeManager?.Dispose();
            _alarmManager?.Dispose();
            _database?.Dispose();
        }
    }
}
