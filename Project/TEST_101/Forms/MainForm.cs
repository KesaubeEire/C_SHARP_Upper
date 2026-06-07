using System.Drawing;
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
    /// 整合所有模块：通讯监控、实时曲线、报警系统、配方管理、生产报表
    /// </summary>
    public partial class MainForm : Form
    {
        // 核心服务
        private DatabaseManager? _database;
        private AlarmManager? _alarmManager;
        private RecipeManager? _recipeManager;
        private ReportGenerator? _reportGenerator;
        private ChartDataManager? _chartDataManager;

        // UI 控件
        private TabControl _tabControl = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lbConnectionStatus = null!;
        private ToolStripStatusLabel _lbAlarmCount = null!;
        private ToolStripStatusLabel _lbTime = null!;

        public MainForm()
        {
            InitializeComponent();
            InitializeServices();
        }

        private void InitializeComponent()
        {
            // 窗体设置
            Text = "🏭 工业监控系统 v1.0";
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei", 9F);

            // 状态栏
            _statusStrip = new StatusStrip();
            _lbConnectionStatus = new ToolStripStatusLabel("📡 设备: 未连接");
            _lbAlarmCount = new ToolStripStatusLabel("⚠️ 报警: 0");
            _lbTime = new ToolStripStatusLabel("🕐 " + DateTime.Now.ToString("HH:mm:ss"));
            _statusStrip.Items.AddRange(new ToolStripItem[]
            {
                _lbConnectionStatus,
                new ToolStripStatusLabel { Spring = true },
                _lbAlarmCount,
                _lbTime
            });
            Controls.Add(_statusStrip);

            // Tab 控件
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 10F)
            };

            // 添加 Tab 页
            _tabControl.TabPages.Add(CreateMonitorTab());
            _tabControl.TabPages.Add(CreateChartTab());
            _tabControl.TabPages.Add(CreateAlarmTab());
            _tabControl.TabPages.Add(CreateRecipeTab());
            _tabControl.TabPages.Add(CreateReportTab());

            Controls.Add(_tabControl);

            // 定时器更新时间
            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => _lbTime.Text = "🕐 " + DateTime.Now.ToString("HH:mm:ss");
            timer.Start();
        }

        private void InitializeServices()
        {
            try
            {
                // 初始化数据库（默认 SQLite，可在配置中切换为 SQL Server）
                _database = DatabaseManager.CreateSQLite("monitor.db");
                // 如果要用 SQL Server，改为：
                // _database = DatabaseManager.CreateSqlServer("localhost", "MonitorDB", "sa", "YourPassword123");

                // 初始化各模块
                _alarmManager = new AlarmManager(_database);
                _recipeManager = new RecipeManager(_database);
                _reportGenerator = new ReportGenerator(_database);

                // 订阅报警事件
                _alarmManager.OnAlarmTriggered += alarm =>
                {
                    Invoke(() =>
                    {
                        _lbAlarmCount.Text = $"⚠️ 报警: {_alarmManager.GetUnconfirmedAlarms().Count}";
                        MessageBox.Show($"报警: {alarm.RuleName}\n设备: {alarm.DeviceId}\n当前值: {alarm.CurrentValue:F2}",
                            "报警提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                };

                // 订阅连接状态事件
                EventBus.Instance.Subscribe<ConnectionChangedEvent>(e =>
                {
                    Invoke(() =>
                    {
                        _lbConnectionStatus.Text = e.IsConnected
                            ? $"📡 设备: {e.DeviceId} 已连接"
                            : "📡 设备: 未连接";
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化服务失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Tab 页创建

        /// <summary>
        /// 创建通讯监控 Tab
        /// </summary>
        private TabPage CreateMonitorTab()
        {
            var tab = new TabPage("📡 通讯监控");

            // 这里可以嵌入原有的 ModbusForm 内容
            // 或者创建一个新的监控面板
            var panel = new Panel { Dock = DockStyle.Fill };

            var label = new Label
            {
                Text = "通讯监控模块\n\n" +
                       "功能：\n" +
                       "• Modbus RTU/TCP 通讯\n" +
                       "• 串口配置与管理\n" +
                       "• 数据读写操作\n" +
                       "• 通信日志记录\n\n" +
                       "点击下方按钮打开完整的 Modbus 调试工具",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 12F)
            };
            panel.Controls.Add(label);

            var btnOpen = new Button
            {
                Text = "打开 Modbus 调试工具",
                Size = new Size(200, 40),
                Location = new Point(600, 400),
                BackColor = Color.FromArgb(60, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOpen.Click += (s, e) =>
            {
                var modbusForm = new ModbusForm();
                modbusForm.Show();
            };
            panel.Controls.Add(btnOpen);

            tab.Controls.Add(panel);
            return tab;
        }

        /// <summary>
        /// 创建实时曲线 Tab
        /// </summary>
        private TabPage CreateChartTab()
        {
            var tab = new TabPage("📈 实时曲线");

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 250,
                Orientation = Orientation.Vertical
            };

            // 左侧：通道配置
            var configPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var configLabel = new Label
            {
                Text = "通道配置",
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                Height = 30
            };
            configPanel.Controls.Add(configLabel);

            // 通道列表
            var channelList = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F)
            };
            channelList.Items.AddRange(new object[]
            {
                "CH1: 伺服转速 (D100) 🔴",
                "CH2: 伺服转矩 (D102) 🔵",
                "CH3: 变频器频率 (D200) 🟢",
                "CH4: 电流 (D202) 🟡"
            });
            configPanel.Controls.Add(channelList);

            // 按钮面板
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };

            var btnStart = new Button { Text = "▶ 开始", Width = 80 };
            var btnPause = new Button { Text = "⏸ 暂停", Width = 80 };
            var btnClear = new Button { Text = "🗑️ 清空", Width = 80 };
            var btnExport = new Button { Text = "💾 导出", Width = 80 };

            btnPanel.Controls.AddRange(new Control[] { btnStart, btnPause, btnClear, btnExport });
            configPanel.Controls.Add(btnPanel);

            splitContainer.Panel1.Controls.Add(configPanel);

            // 右侧：曲线图表
            var chartControl = new RealtimeChartControl { Dock = DockStyle.Fill };
            splitContainer.Panel2.Controls.Add(chartControl);

            // 初始化数据管理器
            _chartDataManager = new ChartDataManager(chartControl);

            tab.Controls.Add(splitContainer);
            return tab;
        }

        /// <summary>
        /// 创建报警系统 Tab
        /// </summary>
        private TabPage CreateAlarmTab()
        {
            var tab = new TabPage("⚠️ 报警系统");

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                Orientation = Orientation.Vertical
            };

            // 左侧：报警规则
            var rulesPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var rulesLabel = new Label
            {
                Text = "报警规则",
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                Height = 30
            };
            rulesPanel.Controls.Add(rulesLabel);

            var rulesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            rulesGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "规则名称" },
                new DataGridViewTextBoxColumn { Name = "Device", HeaderText = "设备" },
                new DataGridViewTextBoxColumn { Name = "Address", HeaderText = "地址" },
                new DataGridViewTextBoxColumn { Name = "Condition", HeaderText = "条件" },
                new DataGridViewTextBoxColumn { Name = "Level", HeaderText = "等级" },
                new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "启用" }
            });
            rulesGrid.Rows.Add("伺服过速", "PLC-1", "D100", "> 1500", "故障", true);
            rulesGrid.Rows.Add("变频器过流", "PLC-1", "D202", "> 10.0", "警告", true);
            rulesGrid.Rows.Add("温度过高", "PLC-2", "D300", "> 80.0", "紧急", true);
            rulesPanel.Controls.Add(rulesGrid);

            // 规则操作按钮
            var rulesBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };
            var btnAddRule = new Button { Text = "➕ 添加", Width = 80 };
            var btnEditRule = new Button { Text = "✏️ 编辑", Width = 80 };
            var btnDeleteRule = new Button { Text = "🗑️ 删除", Width = 80 };
            rulesBtnPanel.Controls.AddRange(new Control[] { btnAddRule, btnEditRule, btnDeleteRule });
            rulesPanel.Controls.Add(rulesBtnPanel);

            splitContainer.Panel1.Controls.Add(rulesPanel);

            // 右侧：报警列表
            var alarmPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var alarmLabel = new Label
            {
                Text = "实时报警",
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                Height = 30
            };
            alarmPanel.Controls.Add(alarmLabel);

            var alarmGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            alarmGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "时间" },
                new DataGridViewTextBoxColumn { Name = "Level", HeaderText = "等级" },
                new DataGridViewTextBoxColumn { Name = "Device", HeaderText = "设备" },
                new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "描述" },
                new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态" },
                new DataGridViewButtonColumn { Name = "Action", HeaderText = "操作" }
            });
            alarmPanel.Controls.Add(alarmGrid);

            // 报警操作按钮
            var alarmBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };
            var btnConfirm = new Button { Text = "✅ 确认", Width = 80 };
            var btnReset = new Button { Text = "🔄 复位", Width = 80 };
            var btnExportAlarm = new Button { Text = "📤 导出", Width = 80 };
            alarmBtnPanel.Controls.AddRange(new Control[] { btnConfirm, btnReset, btnExportAlarm });
            alarmPanel.Controls.Add(alarmBtnPanel);

            splitContainer.Panel2.Controls.Add(alarmPanel);

            tab.Controls.Add(splitContainer);
            return tab;
        }

        /// <summary>
        /// 创建配方管理 Tab
        /// </summary>
        private TabPage CreateRecipeTab()
        {
            var tab = new TabPage("📋 配方管理");

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 350,
                Orientation = Orientation.Vertical
            };

            // 左侧：配方列表
            var listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var listLabel = new Label
            {
                Text = "配方列表",
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                Height = 30
            };
            listPanel.Controls.Add(listLabel);

            var recipeList = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            recipeList.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "名称" },
                new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "创建时间" },
                new DataGridViewTextBoxColumn { Name = "Params", HeaderText = "参数数量" },
                new DataGridViewTextBoxColumn { Name = "Version", HeaderText = "版本" },
                new DataGridViewButtonColumn { Name = "Action", HeaderText = "操作" }
            });
            recipeList.Rows.Add("产品A-标准", "2024-01-15 10:30", "12", "v1.2", "加载");
            recipeList.Rows.Add("产品A-快速", "2024-01-15 11:00", "12", "v1.0", "加载");
            recipeList.Rows.Add("产品B-标准", "2024-01-16 09:00", "15", "v2.1", "加载");
            listPanel.Controls.Add(recipeList);

            // 配方操作按钮
            var listBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };
            var btnNewRecipe = new Button { Text = "➕ 新建", Width = 80 };
            var btnCopyRecipe = new Button { Text = "📋 复制", Width = 80 };
            var btnDeleteRecipe = new Button { Text = "🗑️ 删除", Width = 80 };
            var btnImport = new Button { Text = "📥 导入", Width = 80 };
            var btnExport = new Button { Text = "📤 导出", Width = 80 };
            listBtnPanel.Controls.AddRange(new Control[] { btnNewRecipe, btnCopyRecipe, btnDeleteRecipe, btnImport, btnExport });
            listPanel.Controls.Add(listBtnPanel);

            splitContainer.Panel1.Controls.Add(listPanel);

            // 右侧：配方参数编辑
            var editPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var editLabel = new Label
            {
                Text = "配方参数（当前: 产品A-标准 v1.2）",
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                Height = 30
            };
            editPanel.Controls.Add(editLabel);

            var paramGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            paramGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Index", HeaderText = "序号", ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "参数名称" },
                new DataGridViewTextBoxColumn { Name = "Address", HeaderText = "PLC地址", ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "Current", HeaderText = "当前值", ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "New", HeaderText = "新值" },
                new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "单位", ReadOnly = true }
            });
            paramGrid.Rows.Add("1", "伺服转速", "D100", "1000", "1200", "rpm");
            paramGrid.Rows.Add("2", "伺服转矩限制", "D102", "100", "100", "%");
            paramGrid.Rows.Add("3", "变频器频率", "D200", "50", "50", "Hz");
            paramGrid.Rows.Add("4", "加速时间", "D204", "1000", "1000", "ms");
            editPanel.Controls.Add(paramGrid);

            // 参数操作按钮
            var editBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };
            var btnReadPlc = new Button { Text = "📥 从PLC读取", Width = 100 };
            var btnDownload = new Button { Text = "📤 下发到PLC", Width = 100 };
            var btnSave = new Button { Text = "💾 保存", Width = 80 };
            editBtnPanel.Controls.AddRange(new Control[] { btnReadPlc, btnDownload, btnSave });
            editPanel.Controls.Add(editBtnPanel);

            splitContainer.Panel2.Controls.Add(editPanel);

            tab.Controls.Add(splitContainer);
            return tab;
        }

        /// <summary>
        /// 创建生产报表 Tab
        /// </summary>
        private TabPage CreateReportTab()
        {
            var tab = new TabPage("📊 生产报表");

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // 顶部筛选区
            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                FlowDirection = FlowDirection.LeftToRight
            };

            filterPanel.Controls.Add(new Label { Text = "报表类型:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            var reportType = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            reportType.Items.AddRange(new[] { "日报", "周报", "月报" });
            reportType.SelectedIndex = 0;
            filterPanel.Controls.Add(reportType);

            filterPanel.Controls.Add(new Label { Text = "日期:", AutoSize = true, Padding = new Padding(10, 8, 0, 0) });
            var datePicker = new DateTimePicker { Width = 150 };
            filterPanel.Controls.Add(datePicker);

            var btnGenerate = new Button
            {
                Text = "📊 生成报表",
                Width = 100,
                BackColor = Color.FromArgb(60, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            filterPanel.Controls.Add(btnGenerate);

            panel.Controls.Add(filterPanel);

            // 统计概览
            var statsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 100,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0)
            };

            var stats = new[]
            {
                ("总产量", "1,250"),
                ("合格率", "98.5%"),
                ("平均节拍", "3.2s"),
                ("报警次数", "5")
            };

            foreach (var (title, value) in stats)
            {
                var card = new Panel
                {
                    Size = new Size(150, 70),
                    BackColor = Color.FromArgb(240, 248, 255),
                    Margin = new Padding(10)
                };
                card.Controls.Add(new Label
                {
                    Text = title,
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft YaHei", 9F)
                });
                card.Controls.Add(new Label
                {
                    Text = value,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft YaHei", 16F, FontStyle.Bold)
                });
                statsPanel.Controls.Add(card);
            }

            panel.Controls.Add(statsPanel);

            // 详细数据表格
            var dataGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dataGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "时间" },
                new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "产量" },
                new DataGridViewTextBoxColumn { Name = "Good", HeaderText = "合格" },
                new DataGridViewTextBoxColumn { Name = "Defect", HeaderText = "不合格" },
                new DataGridViewTextBoxColumn { Name = "Cycle", HeaderText = "节拍" },
                new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态" }
            });
            dataGrid.Rows.Add("08:00-09:00", "125", "123", "2", "3.1s", "正常");
            dataGrid.Rows.Add("09:00-10:00", "130", "128", "2", "3.0s", "正常");
            dataGrid.Rows.Add("10:00-11:00", "128", "125", "3", "3.3s", "正常");
            dataGrid.Rows.Add("11:00-12:00", "135", "133", "2", "2.9s", "正常");
            panel.Controls.Add(dataGrid);

            // 底部操作按钮
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };
            var btnExportExcel = new Button { Text = "📊 导出 Excel", Width = 100 };
            var btnPrint = new Button { Text = "🖨️ 打印", Width = 80 };
            btnPanel.Controls.AddRange(new Control[] { btnExportExcel, btnPrint });
            panel.Controls.Add(btnPanel);

            tab.Controls.Add(panel);
            return tab;
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 释放资源
            _chartDataManager?.Dispose();
            _reportGenerator?.Dispose();
            _recipeManager?.Dispose();
            _alarmManager?.Dispose();
            _database?.Dispose();
        }
    }
}
