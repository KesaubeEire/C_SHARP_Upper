using System;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;

namespace TEST_101
{
    /// <summary>
    /// Modbus 调试助手 — UI 胶水层。
    /// 协议逻辑 → ModbusProtocol，通信管理 → ModbusTransport，历史记录 → InputHistoryManager。
    /// </summary>
    public partial class ModbusForm : Form
    {
        // ──── 核心组件 ────
        private ModbusTransport _transport = null!;
        private InputHistoryManager _history = null!;

        private bool _isTcpMode = false;

        // ★ 当前打开的历史下拉面板（用于切换和 ESC 关闭）
        private HistoryDropDown? _currentDropdown;

        public ModbusForm()
        {
            InitializeComponent();
        }

        // ========== 窗体加载 ==========
        private void ModbusForm_Load(object sender, EventArgs e)
        {
            // 初始化核心组件
            _transport = new ModbusTransport(this, () => _isTcpMode);
            _transport.FrameReceived += OnFrameReceived;
            _transport.ErrorOccurred += OnError;
            _transport.ConnectionChanged += OnConnectionChanged;

            _history = new InputHistoryManager();

            InitUI();
            RefreshComPorts();
        }

        private void InitUI()
        {
            drop_baud.Text = "9600";

            // 模式
            drop_mode.SelectedIndex = 0;
            _isTcpMode = false;
            ShowTcpControls(false);

            // 功能码
            drop_func.Items.Clear();
            drop_func.Items.Add("01 读线圈");
            drop_func.Items.Add("02 读离散输入");
            drop_func.Items.Add("03 读保持寄存器");
            drop_func.Items.Add("04 读输入寄存器");
            drop_func.Items.Add("05 写单线圈");
            drop_func.Items.Add("06 写单寄存器");
            drop_func.Items.Add("15 写多线圈");
            drop_func.Items.Add("16 写多寄存器");
            drop_func.SelectedIndex = 2;

            // 默认值
            box_dev.Text = "1";
            box_addr.Text = "0";
            box_count.Text = "10";

            drop_stop.SelectedIndex = 0;
            drop_parity.SelectedIndex = 0;

            // DataGridView 初始列
            SetupGridColumns(false);

            // 为每个输入框添加历史下拉按钮
            AddHistoryButton(box_dev, "dev_addr");
            AddHistoryButton(box_addr, "start_addr");
            AddHistoryButton(box_count, "count");
            AddHistoryButton(box_ip, "tcp_ip");
            AddHistoryButton(box_port, "tcp_port");

            // ★ 添加"高级语法学习"按钮（运行时动态创建，不碰 Designer）
            AddLearnButton();
        }

        /// <summary>在标题区域添加一个"🔬 学习"按钮</summary>
        private void AddLearnButton()
        {
            var btn = new Button
            {
                Text = "🔬 学委托/事件/反射",
                Width = 155,
                Height = 30,
                Font = new Font("Microsoft YaHei", 9F),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 245, 215),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 180, 120);
            // 放在标题右侧
            btn.Location = new Point(
                lb_title.Right + 15,
                lb_title.Top + 2);
            btn.Click += (s, e) => ShowLearnDialog();
            this.Controls.Add(btn);
        }

        /// <summary>弹出学习对话框</summary>
        private void ShowLearnDialog()
        {
            // 收集各 Demo 的输出
            var output = new System.Text.StringBuilder();

            // 捕获 Console 输出
            var originalOut = Console.Out;
            using var writer = new System.IO.StringWriter();
            Console.SetOut(writer);

            // ★ 注入真实的模式状态，而不是写死 false
            CSharpConceptsDemo.CurrentIsTcpMode = () => _isTcpMode;

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

            // ★ 最后打印速查表
            output.AppendLine(new string('─', 55));
            output.AppendLine(CSharpConceptsDemo.GetQuickReference());

            Console.SetOut(originalOut);

            // 弹窗显示
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
            textBox.Select(0, 0); // 取消选中
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
                // 切换：如果已打开则关闭，否则弹出
                if (_currentDropdown != null && !_currentDropdown.IsDisposed)
                {
                    _currentDropdown.Close();
                    _currentDropdown = null;
                    return;
                }

                var dropdown = new HistoryDropDown(_history, target, fieldKey);
                dropdown.FormClosed += (_, _) => _currentDropdown = null;
                _currentDropdown = dropdown;
                dropdown.ShowDropdown();
            };
            target.Parent!.Controls.Add(btn);
        }

        // ========== 刷新 COM 口 ==========
        private void RefreshComPorts()
        {
            string currentCom = drop_com.Text ?? string.Empty;
            drop_com.Items.Clear();
            drop_com.Text = "";

            string[] ports = _transport.GetPortNames().Distinct().ToArray();

            if (ports.Length == 0)
            {
                lb_status.Text = "未检测到可用 COM 口";
                return;
            }

            foreach (string port in ports)
                drop_com.Items.Add(port);

            if (drop_com.Items.Contains(currentCom))
                drop_com.Text = currentCom;
            else
                drop_com.SelectedIndex = 0;

            lb_status.Text = $"检测到 {ports.Length} 个 COM 口";
        }

        // ========== Transport 事件回调（已在 UI 线程）==========

        private void OnFrameReceived(byte[] buffer, bool isTcp)
        {
            // 1. 着色显示原始帧
            byte funcCode = buffer.Length >= 2 ? buffer[1] : (byte)0;
            string hex = BitConverter.ToString(buffer).Replace("-", " ");
            ColorizeHexFrame(box_recv_hex, hex, funcCode, isTcp);
            box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 接收 → {hex}\r\n");

            // 2. 如果是 TCP，跳过 MBAP 头再解析
            byte[] pduBuf = isTcp && buffer.Length > ModbusProtocol.MBAP_HEADER_SIZE
                ? buffer.Skip(ModbusProtocol.MBAP_HEADER_SIZE).ToArray()
                : buffer;

            // 3. 解析
            var result = ModbusProtocol.ParseResponse(pduBuf);
            FillGrid(result);
        }

        private void OnError(string msg)
        {
            box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
        }

        private void OnConnectionChanged(bool connected, string statusText)
        {
            lb_status.Text = statusText;
            if (connected)
            {
                btn_open.Text = _isTcpMode ? "断开" : "关闭串口";
                btn_open.BackColor = Color.FromArgb(220, 80, 80);
            }
            else
            {
                btn_open.Text = _isTcpMode ? "连接" : "打开串口";
                btn_open.BackColor = Color.FromArgb(60, 140, 60);
            }
        }

        // ========== 填充 DataGridView ==========

        private void FillGrid(ModbusParseResult result)
        {
            if (result.IsError)
            {
                SetupGridColumns(false);
                grid_result.Rows.Add("—", $"❌ {result.ErrorMessage}", "", "", "", "");
                return;
            }

            if (result.Bits.Count > 0)
            {
                SetupGridColumns(true);
                foreach (var bit in result.Bits)
                    grid_result.Rows.Add(bit.Index.ToString(), bit.IsOn ? "ON" : "OFF", $"0x{bit.RawByte:X2}");
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
                    grid_result.Rows.Add(reg.Index.ToString(), dec, hex, bin, oct, signed);
                }
            }
            else
            {
                // 其他功能码（无数据）
            }
        }

        // ========== 模式切换 ==========
        private void drop_mode_SelectedIndexChanged(object sender, EventArgs e)
        {
            _isTcpMode = drop_mode.SelectedIndex == 1;

            if (!_isTcpMode)
                _transport.DisconnectTcp();
            else if (_transport.IsSerialOpen)
            {
                _transport.CloseSerial();
                btn_open.Text = "打开串口";
                btn_open.BackColor = Color.FromArgb(60, 140, 60);
            }

            ShowTcpControls(_isTcpMode);
            lb_legend.Text = _isTcpMode
                ? "TCP图例：MBAP头=钢蓝  单元ID=灰  功能码(01=蓝 02=绿 03=紫 04=黄 错误=红)  数据=黄"
                : "RTU图例：地址=灰  功能码(01=蓝 02=绿 03=紫 04=黄 错误=红)  数据=黄  CRC=橙";
            lb_status.Text = "已断开";
        }

        private void ShowTcpControls(bool showTcp)
        {
            lb_ip.Visible = showTcp;
            box_ip.Visible = showTcp;
            lb_port.Visible = showTcp;
            box_port.Visible = showTcp;

            lb_com.Visible = !showTcp;
            drop_com.Visible = !showTcp;
            lb_baud.Visible = !showTcp;
            drop_baud.Visible = !showTcp;
            lb_stop.Visible = !showTcp;
            drop_stop.Visible = !showTcp;
            lb_parity.Visible = !showTcp;
            drop_parity.Visible = !showTcp;
            btn_refresh.Visible = !showTcp;

            btn_open.Text = showTcp ? "连接" : "打开串口";
        }

        // ========== 打开/关闭连接 ==========
        private void btn_open_Click(object sender, EventArgs e)
        {
            if (_isTcpMode)
            {
                if (_transport.IsTcpConnected)
                    _transport.DisconnectTcp();
                else
                    _transport.ConnectTcp(box_ip.Text.Trim(), int.Parse(box_port.Text.Trim()));
                return;
            }

            if (!_transport.IsSerialOpen)
            {
                if (string.IsNullOrWhiteSpace(drop_com.Text))
                {
                    MessageBox.Show("请先选择一个 COM 口");
                    return;
                }
                try
                {
                    _transport.OpenSerial(drop_com.Text, int.Parse(drop_baud.Text),
                        ParseStopBits(drop_stop.Text), ParseParity(drop_parity.Text));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开串口失败：" + ex.Message);
                }
            }
            else
            {
                _transport.CloseSerial();
            }
        }

        // ========== 发送按钮 ==========
        private void btn_read_Click(object sender, EventArgs e)
        {
            // 连接检查
            if (_isTcpMode && !_transport.IsTcpConnected)
            {
                MessageBox.Show("请先连接 TCP");
                return;
            }
            if (!_isTcpMode && !_transport.IsSerialOpen)
            {
                MessageBox.Show("请先打开串口");
                return;
            }

            try
            {
                byte devAddr = byte.Parse(box_dev.Text.Trim());
                string funcStr = drop_func.Text.Substring(0, 2);
                byte funcCode = byte.Parse(funcStr);

                // ★ [3] 十六进制地址输入：自动识别 0x 前缀
                string addrText = box_addr.Text.Trim();
                ushort startAddr = addrText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToUInt16(addrText.Substring(2), 16)
                    : ushort.Parse(addrText);

                ushort count = ushort.Parse(box_count.Text.Trim());

                // ★ [7] 协议数量校验
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
                        $"功能码 {funcStr}（{drop_func.Text.Substring(3)}）的写入功能暂未实现。\n\n" +
                        "当前仅支持读取功能码：01 读线圈、02 读离散输入、03 读保持寄存器、04 读输入寄存器。",
                        "功能未实现", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 记录历史
                _history.Add("dev_addr", box_dev.Text.Trim());
                _history.Add("start_addr", addrText);
                _history.Add("count", box_count.Text.Trim());
                if (_isTcpMode)
                {
                    _history.Add("tcp_ip", box_ip.Text.Trim());
                    _history.Add("tcp_port", box_port.Text.Trim());
                }

                // 发送
                var (frame, fc) = _transport.SendReadRequest(devAddr, funcCode, startAddr, count);
                ColorizeHexFrame(box_send_hex, BitConverter.ToString(frame).Replace("-", " "), fc, _isTcpMode);
                box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 发送 → {box_send_hex.Text}\r\n");
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

        // ========== DataGridView 列初始化 ==========
        private void SetupGridColumns(bool isBit)
        {
            grid_result.Columns.Clear();
            grid_result.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid_result.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (isBit)
            {
                var colIdx = grid_result.Columns.Add("Col_Index", "序号");
                grid_result.Columns[colIdx].Width = 50;
                grid_result.Columns[colIdx].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colState = grid_result.Columns.Add("Col_State", "状态");
                grid_result.Columns[colState].Width = 65;
                grid_result.Columns[colState].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colRaw = grid_result.Columns.Add("Col_RawByte", "原始字节");
                grid_result.Columns[colRaw].Width = 90;
                grid_result.Columns[colRaw].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            else
            {
                var colIdx = grid_result.Columns.Add("Col_Index", "序号");
                grid_result.Columns[colIdx].Width = 50;
                grid_result.Columns[colIdx].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colDEC = grid_result.Columns.Add("Col_DEC", "DEC");
                grid_result.Columns[colDEC].Width = 65;
                grid_result.Columns[colDEC].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                var colHEX = grid_result.Columns.Add("Col_HEX", "HEX");
                grid_result.Columns[colHEX].Width = 70;
                grid_result.Columns[colHEX].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid_result.Columns[colHEX].DefaultCellStyle.Font = new Font("Consolas", 10F);

                var colBIN = grid_result.Columns.Add("Col_BIN", "BIN");
                grid_result.Columns[colBIN].Width = 170;
                grid_result.Columns[colBIN].DefaultCellStyle.Font = new Font("Consolas", 10F);

                var colOCT = grid_result.Columns.Add("Col_OCT", "OCT");
                grid_result.Columns[colOCT].Width = 70;
                grid_result.Columns[colOCT].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid_result.Columns[colOCT].DefaultCellStyle.Font = new Font("Consolas", 10F);

                var colSigned = grid_result.Columns.Add("Col_Signed", "有符号");
                grid_result.Columns[colSigned].Width = 70;
                grid_result.Columns[colSigned].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
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

        // ========== 控件事件 ==========
        private void btn_refresh_Click(object sender, EventArgs e) => RefreshComPorts();

        private void btn_clear_Click(object sender, EventArgs e)
        {
            box_recv.Clear();
            box_send_hex.Clear();
            box_recv_hex.Clear();
            grid_result.Rows.Clear();
        }

        private void btn_back_Click(object sender, EventArgs e)
        {
            串口通信调试工具 serialForm = new 串口通信调试工具();
            serialForm.Show();
            this.Hide();
        }

        // ========== 窗口关闭 ==========
        private void ModbusForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _transport?.Dispose();
        }

        // ★ ESC 关闭下拉面板
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _currentDropdown != null && !_currentDropdown.IsDisposed)
            {
                _currentDropdown.Close();
                _currentDropdown = null;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
