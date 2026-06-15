using System.Drawing;
using System.Windows.Forms;

namespace TEST_101.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            _statusStrip = new StatusStrip();
            _lbConnectionStatus = new ToolStripStatusLabel();
            _lbAlarmCount = new ToolStripStatusLabel();
            _lbTime = new ToolStripStatusLabel();
            _tabControl = new TabControl();
            _tabMonitor = new TabPage();
            _panelMonitor = new Panel();
            _mb_lb_title = new Label();
            _mb_lb_legend = new Label();
            _mb_lb_mode = new Label();
            _mb_drop_mode = new ComboBox();
            _mb_lb_ip = new Label();
            _mb_box_ip = new TextBox();
            _mb_lb_port = new Label();
            _mb_box_port = new TextBox();
            _mb_lb_com = new Label();
            _mb_drop_com = new ComboBox();
            _mb_lb_baud = new Label();
            _mb_drop_baud = new ComboBox();
            _mb_lb_stop = new Label();
            _mb_drop_stop = new ComboBox();
            _mb_lb_parity = new Label();
            _mb_drop_parity = new ComboBox();
            _mb_btn_refresh = new Button();
            _mb_btn_open = new Button();
            _mb_lb_status = new Label();
            _mb_sep1 = new Label();
            _mb_lb_dev = new Label();
            _mb_box_dev = new TextBox();
            _mb_lb_func = new Label();
            _mb_drop_func = new ComboBox();
            _mb_lb_addr = new Label();
            _mb_box_addr = new TextBox();
            _mb_lb_count = new Label();
            _mb_box_count = new TextBox();
            _mb_btn_read = new Button();
            _mb_btn_polling = new Button();
            _mb_lb_polling_status = new Label();
            _mb_lb_poll_interval_label = new Label();
            _mb_polling_interval = new NumericUpDown();
            _mb_lb_send_hex = new Label();
            _mb_box_send_hex = new RichTextBox();
            _mb_lb_recv_hex = new Label();
            _mb_box_recv_hex = new RichTextBox();
            _mb_lb_result = new Label();
            _mb_grid_result = new DataGridView();
            _mb_btn_clear = new Button();
            _mb_lb_log = new Label();
            _mb_box_recv = new TextBox();
            _mb_btn_learn = new Button();
            _tabChart = new TabPage();
            _splitChart = new SplitContainer();
            _panelChartConfig = new Panel();
            _listChannels = new ListBox();
            _lbChannelConfig = new Label();
            _panelChartBtns = new FlowLayoutPanel();
            _btnChartStart = new Button();
            _btnChartPause = new Button();
            _btnChartClear = new Button();
            _btnChartExport = new Button();
            _panelChartArea = new Panel();
            _tabAlarm = new TabPage();
            _splitAlarm = new SplitContainer();
            _panelAlarmRules = new Panel();
            _gridAlarmRules = new DataGridView();
            _colRuleName = new DataGridViewTextBoxColumn();
            _colRuleDevice = new DataGridViewTextBoxColumn();
            _colRuleAddress = new DataGridViewTextBoxColumn();
            _colRuleCondition = new DataGridViewTextBoxColumn();
            _colRuleLevel = new DataGridViewTextBoxColumn();
            _colRuleEnabled = new DataGridViewCheckBoxColumn();
            _lbAlarmRules = new Label();
            _panelAlarmRuleBtns = new FlowLayoutPanel();
            _btnAddRule = new Button();
            _btnEditRule = new Button();
            _btnDeleteRule = new Button();
            _panelAlarmList = new Panel();
            _gridAlarms = new DataGridView();
            _colAlarmTime = new DataGridViewTextBoxColumn();
            _colAlarmLevel = new DataGridViewTextBoxColumn();
            _colAlarmDevice = new DataGridViewTextBoxColumn();
            _colAlarmDesc = new DataGridViewTextBoxColumn();
            _colAlarmStatus = new DataGridViewTextBoxColumn();
            _lbAlarmList = new Label();
            _panelAlarmBtns = new FlowLayoutPanel();
            _btnConfirmAlarm = new Button();
            _btnResetAlarm = new Button();
            _btnExportAlarm = new Button();
            _tabRecipe = new TabPage();
            _splitRecipe = new SplitContainer();
            _panelRecipeList = new Panel();
            _gridRecipes = new DataGridView();
            _colRecipeName = new DataGridViewTextBoxColumn();
            _colRecipeTime = new DataGridViewTextBoxColumn();
            _colRecipeParams = new DataGridViewTextBoxColumn();
            _colRecipeVersion = new DataGridViewTextBoxColumn();
            _lbRecipeList = new Label();
            _panelRecipeBtns = new FlowLayoutPanel();
            _btnNewRecipe = new Button();
            _btnCopyRecipe = new Button();
            _btnDeleteRecipe = new Button();
            _panelRecipeEdit = new Panel();
            _gridRecipeParams = new DataGridView();
            _colParamIndex = new DataGridViewTextBoxColumn();
            _colParamName = new DataGridViewTextBoxColumn();
            _colParamAddress = new DataGridViewTextBoxColumn();
            _colParamCurrent = new DataGridViewTextBoxColumn();
            _colParamNew = new DataGridViewTextBoxColumn();
            _colParamUnit = new DataGridViewTextBoxColumn();
            _lbRecipeEdit = new Label();
            _panelRecipeEditBtns = new FlowLayoutPanel();
            _btnReadPlc = new Button();
            _btnDownloadPlc = new Button();
            _btnSaveRecipe = new Button();
            _tabReport = new TabPage();
            _panelReport = new Panel();
            _gridReport = new DataGridView();
            _colRptTime = new DataGridViewTextBoxColumn();
            _colRptCount = new DataGridViewTextBoxColumn();
            _colRptGood = new DataGridViewTextBoxColumn();
            _colRptDefect = new DataGridViewTextBoxColumn();
            _colRptCycle = new DataGridViewTextBoxColumn();
            _colRptStatus = new DataGridViewTextBoxColumn();
            _panelStats = new FlowLayoutPanel();
            _cardTotal = new Panel();
            _cardQualify = new Panel();
            _cardCycle = new Panel();
            _cardAlarmCount = new Panel();
            _panelReportFilter = new FlowLayoutPanel();
            _lbReportType = new Label();
            _dropReportType = new ComboBox();
            _lbReportDate = new Label();
            _dateReport = new DateTimePicker();
            _btnGenerateReport = new Button();
            _panelReportBtns = new FlowLayoutPanel();
            _btnExportExcel = new Button();
            _btnPrint = new Button();
            _statusStrip.SuspendLayout();
            _tabControl.SuspendLayout();
            _tabMonitor.SuspendLayout();
            _panelMonitor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_mb_polling_interval).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_mb_grid_result).BeginInit();
            _tabChart.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_splitChart).BeginInit();
            _splitChart.Panel1.SuspendLayout();
            _splitChart.Panel2.SuspendLayout();
            _splitChart.SuspendLayout();
            _panelChartConfig.SuspendLayout();
            _panelChartBtns.SuspendLayout();
            _tabAlarm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_splitAlarm).BeginInit();
            _splitAlarm.Panel1.SuspendLayout();
            _splitAlarm.Panel2.SuspendLayout();
            _splitAlarm.SuspendLayout();
            _panelAlarmRules.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridAlarmRules).BeginInit();
            _panelAlarmRuleBtns.SuspendLayout();
            _panelAlarmList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridAlarms).BeginInit();
            _panelAlarmBtns.SuspendLayout();
            _tabRecipe.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_splitRecipe).BeginInit();
            _splitRecipe.Panel1.SuspendLayout();
            _splitRecipe.Panel2.SuspendLayout();
            _splitRecipe.SuspendLayout();
            _panelRecipeList.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridRecipes).BeginInit();
            _panelRecipeBtns.SuspendLayout();
            _panelRecipeEdit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridRecipeParams).BeginInit();
            _panelRecipeEditBtns.SuspendLayout();
            _tabReport.SuspendLayout();
            _panelReport.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)_gridReport).BeginInit();
            _panelStats.SuspendLayout();
            _panelReportFilter.SuspendLayout();
            _panelReportBtns.SuspendLayout();
            SuspendLayout();
            // 
            // _statusStrip
            // 
            _statusStrip.Items.AddRange(new ToolStripItem[] { _lbConnectionStatus, _lbAlarmCount, _lbTime });
            _statusStrip.Location = new Point(0, 771);
            _statusStrip.Name = "_statusStrip";
            _statusStrip.Size = new Size(1026, 22);
            _statusStrip.TabIndex = 1;
            // 
            // _lbConnectionStatus
            // 
            _lbConnectionStatus.Name = "_lbConnectionStatus";
            _lbConnectionStatus.Size = new Size(95, 17);
            _lbConnectionStatus.Text = "📡 设备: 未连接";
            // 
            // _lbAlarmCount
            // 
            _lbAlarmCount.Name = "_lbAlarmCount";
            _lbAlarmCount.Size = new Size(66, 17);
            _lbAlarmCount.Text = "⚠️ 报警: 0";
            // 
            // _lbTime
            // 
            _lbTime.Name = "_lbTime";
            _lbTime.Size = new Size(76, 17);
            _lbTime.Text = "🕐 00:00:00";
            // 
            // _tabControl
            // 
            _tabControl.Controls.Add(_tabMonitor);
            _tabControl.Controls.Add(_tabChart);
            _tabControl.Controls.Add(_tabAlarm);
            _tabControl.Controls.Add(_tabRecipe);
            _tabControl.Controls.Add(_tabReport);
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Font = new Font("微软雅黑", 10F);
            _tabControl.Location = new Point(0, 0);
            _tabControl.Name = "_tabControl";
            _tabControl.SelectedIndex = 0;
            _tabControl.Size = new Size(1026, 771);
            _tabControl.TabIndex = 0;
            // 
            // _tabMonitor
            // 
            _tabMonitor.Controls.Add(_panelMonitor);
            _tabMonitor.Location = new Point(4, 28);
            _tabMonitor.Name = "_tabMonitor";
            _tabMonitor.Size = new Size(1018, 739);
            _tabMonitor.TabIndex = 0;
            _tabMonitor.Text = "📡 通讯监控";
            // 
            // _panelMonitor
            // 
            _panelMonitor.AutoScroll = true;
            _panelMonitor.Controls.Add(_mb_lb_title);
            _panelMonitor.Controls.Add(_mb_lb_legend);
            _panelMonitor.Controls.Add(_mb_lb_mode);
            _panelMonitor.Controls.Add(_mb_drop_mode);
            _panelMonitor.Controls.Add(_mb_lb_ip);
            _panelMonitor.Controls.Add(_mb_box_ip);
            _panelMonitor.Controls.Add(_mb_lb_port);
            _panelMonitor.Controls.Add(_mb_box_port);
            _panelMonitor.Controls.Add(_mb_lb_com);
            _panelMonitor.Controls.Add(_mb_drop_com);
            _panelMonitor.Controls.Add(_mb_lb_baud);
            _panelMonitor.Controls.Add(_mb_drop_baud);
            _panelMonitor.Controls.Add(_mb_lb_stop);
            _panelMonitor.Controls.Add(_mb_drop_stop);
            _panelMonitor.Controls.Add(_mb_lb_parity);
            _panelMonitor.Controls.Add(_mb_drop_parity);
            _panelMonitor.Controls.Add(_mb_btn_refresh);
            _panelMonitor.Controls.Add(_mb_btn_open);
            _panelMonitor.Controls.Add(_mb_lb_status);
            _panelMonitor.Controls.Add(_mb_sep1);
            _panelMonitor.Controls.Add(_mb_lb_dev);
            _panelMonitor.Controls.Add(_mb_box_dev);
            _panelMonitor.Controls.Add(_mb_lb_func);
            _panelMonitor.Controls.Add(_mb_drop_func);
            _panelMonitor.Controls.Add(_mb_lb_addr);
            _panelMonitor.Controls.Add(_mb_box_addr);
            _panelMonitor.Controls.Add(_mb_lb_count);
            _panelMonitor.Controls.Add(_mb_box_count);
            _panelMonitor.Controls.Add(_mb_btn_read);
            _panelMonitor.Controls.Add(_mb_btn_polling);
            _panelMonitor.Controls.Add(_mb_lb_polling_status);
            _panelMonitor.Controls.Add(_mb_lb_poll_interval_label);
            _panelMonitor.Controls.Add(_mb_polling_interval);
            _panelMonitor.Controls.Add(_mb_lb_send_hex);
            _panelMonitor.Controls.Add(_mb_box_send_hex);
            _panelMonitor.Controls.Add(_mb_lb_recv_hex);
            _panelMonitor.Controls.Add(_mb_box_recv_hex);
            _panelMonitor.Controls.Add(_mb_lb_result);
            _panelMonitor.Controls.Add(_mb_grid_result);
            _panelMonitor.Controls.Add(_mb_btn_clear);
            _panelMonitor.Controls.Add(_mb_lb_log);
            _panelMonitor.Controls.Add(_mb_box_recv);
            _panelMonitor.Controls.Add(_mb_btn_learn);
            _panelMonitor.Dock = DockStyle.Fill;
            _panelMonitor.Location = new Point(0, 0);
            _panelMonitor.Name = "_panelMonitor";
            _panelMonitor.Size = new Size(1018, 739);
            _panelMonitor.TabIndex = 0;
            // 
            // _mb_lb_title
            // 
            _mb_lb_title.AutoSize = true;
            _mb_lb_title.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            _mb_lb_title.Location = new Point(12, 9);
            _mb_lb_title.Name = "_mb_lb_title";
            _mb_lb_title.Size = new Size(173, 26);
            _mb_lb_title.TabIndex = 0;
            _mb_lb_title.Text = "Modbus 调试助手";
            // 
            // _mb_lb_legend
            // 
            _mb_lb_legend.AutoSize = true;
            _mb_lb_legend.Location = new Point(12, 36);
            _mb_lb_legend.Name = "_mb_lb_legend";
            _mb_lb_legend.Size = new Size(537, 20);
            _mb_lb_legend.TabIndex = 1;
            _mb_lb_legend.Text = "RTU图例：地址=灰  功能码(01=蓝 02=绿 03=紫 04=黄 错误=红)  数据=黄  CRC=橙";
            // 
            // _mb_lb_mode
            // 
            _mb_lb_mode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _mb_lb_mode.AutoSize = true;
            _mb_lb_mode.Location = new Point(585, 14);
            _mb_lb_mode.Name = "_mb_lb_mode";
            _mb_lb_mode.Size = new Size(37, 20);
            _mb_lb_mode.TabIndex = 2;
            _mb_lb_mode.Text = "模式";
            // 
            // _mb_drop_mode
            // 
            _mb_drop_mode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _mb_drop_mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mb_drop_mode.FormattingEnabled = true;
            _mb_drop_mode.Items.AddRange(new object[] { "RTU", "TCP" });
            _mb_drop_mode.Location = new Point(628, 10);
            _mb_drop_mode.Name = "_mb_drop_mode";
            _mb_drop_mode.Size = new Size(60, 27);
            _mb_drop_mode.TabIndex = 3;
            // 
            // _mb_lb_ip
            // 
            _mb_lb_ip.AutoSize = true;
            _mb_lb_ip.Font = new Font("Microsoft YaHei UI", 9F);
            _mb_lb_ip.Location = new Point(12, 74);
            _mb_lb_ip.Name = "_mb_lb_ip";
            _mb_lb_ip.Size = new Size(43, 17);
            _mb_lb_ip.TabIndex = 4;
            _mb_lb_ip.Text = "IP地址";
            _mb_lb_ip.Visible = false;
            // 
            // _mb_box_ip
            // 
            _mb_box_ip.Font = new Font("Consolas", 9F);
            _mb_box_ip.Location = new Point(60, 71);
            _mb_box_ip.Name = "_mb_box_ip";
            _mb_box_ip.Size = new Size(130, 22);
            _mb_box_ip.TabIndex = 5;
            _mb_box_ip.Text = "192.168.0.1";
            _mb_box_ip.Visible = false;
            // 
            // _mb_lb_port
            // 
            _mb_lb_port.AutoSize = true;
            _mb_lb_port.Font = new Font("Microsoft YaHei UI", 9F);
            _mb_lb_port.Location = new Point(205, 74);
            _mb_lb_port.Name = "_mb_lb_port";
            _mb_lb_port.Size = new Size(32, 17);
            _mb_lb_port.TabIndex = 6;
            _mb_lb_port.Text = "端口";
            _mb_lb_port.Visible = false;
            // 
            // _mb_box_port
            // 
            _mb_box_port.Font = new Font("Consolas", 9F);
            _mb_box_port.Location = new Point(235, 71);
            _mb_box_port.Name = "_mb_box_port";
            _mb_box_port.Size = new Size(55, 22);
            _mb_box_port.TabIndex = 7;
            _mb_box_port.Text = "502";
            _mb_box_port.Visible = false;
            // 
            // _mb_lb_com
            // 
            _mb_lb_com.AutoSize = true;
            _mb_lb_com.Location = new Point(12, 74);
            _mb_lb_com.Name = "_mb_lb_com";
            _mb_lb_com.Size = new Size(57, 20);
            _mb_lb_com.TabIndex = 8;
            _mb_lb_com.Text = "COM口";
            // 
            // _mb_drop_com
            // 
            _mb_drop_com.DropDownStyle = ComboBoxStyle.DropDownList;
            _mb_drop_com.FormattingEnabled = true;
            _mb_drop_com.Location = new Point(65, 70);
            _mb_drop_com.Name = "_mb_drop_com";
            _mb_drop_com.Size = new Size(100, 27);
            _mb_drop_com.TabIndex = 9;
            // 
            // _mb_lb_baud
            // 
            _mb_lb_baud.AutoSize = true;
            _mb_lb_baud.Location = new Point(178, 74);
            _mb_lb_baud.Name = "_mb_lb_baud";
            _mb_lb_baud.Size = new Size(51, 20);
            _mb_lb_baud.TabIndex = 10;
            _mb_lb_baud.Text = "波特率";
            // 
            // _mb_drop_baud
            // 
            _mb_drop_baud.DropDownStyle = ComboBoxStyle.DropDownList;
            _mb_drop_baud.FormattingEnabled = true;
            _mb_drop_baud.Items.AddRange(new object[] { "4800", "9600", "19200", "38400", "115200" });
            _mb_drop_baud.Location = new Point(228, 70);
            _mb_drop_baud.Name = "_mb_drop_baud";
            _mb_drop_baud.Size = new Size(75, 27);
            _mb_drop_baud.TabIndex = 11;
            // 
            // _mb_lb_stop
            // 
            _mb_lb_stop.AutoSize = true;
            _mb_lb_stop.Location = new Point(310, 74);
            _mb_lb_stop.Name = "_mb_lb_stop";
            _mb_lb_stop.Size = new Size(51, 20);
            _mb_lb_stop.TabIndex = 12;
            _mb_lb_stop.Text = "停止位";
            // 
            // _mb_drop_stop
            // 
            _mb_drop_stop.DropDownStyle = ComboBoxStyle.DropDownList;
            _mb_drop_stop.FormattingEnabled = true;
            _mb_drop_stop.Items.AddRange(new object[] { "1", "1.5", "2" });
            _mb_drop_stop.Location = new Point(358, 70);
            _mb_drop_stop.Name = "_mb_drop_stop";
            _mb_drop_stop.Size = new Size(55, 27);
            _mb_drop_stop.TabIndex = 13;
            // 
            // _mb_lb_parity
            // 
            _mb_lb_parity.AutoSize = true;
            _mb_lb_parity.Location = new Point(420, 74);
            _mb_lb_parity.Name = "_mb_lb_parity";
            _mb_lb_parity.Size = new Size(37, 20);
            _mb_lb_parity.TabIndex = 14;
            _mb_lb_parity.Text = "校验";
            // 
            // _mb_drop_parity
            // 
            _mb_drop_parity.DropDownStyle = ComboBoxStyle.DropDownList;
            _mb_drop_parity.FormattingEnabled = true;
            _mb_drop_parity.Items.AddRange(new object[] { "无", "奇校验", "偶校验", "Mark", "Space" });
            _mb_drop_parity.Location = new Point(455, 70);
            _mb_drop_parity.Name = "_mb_drop_parity";
            _mb_drop_parity.Size = new Size(80, 27);
            _mb_drop_parity.TabIndex = 15;
            // 
            // _mb_btn_refresh
            // 
            _mb_btn_refresh.Font = new Font("Microsoft YaHei UI", 9F);
            _mb_btn_refresh.Location = new Point(548, 67);
            _mb_btn_refresh.Name = "_mb_btn_refresh";
            _mb_btn_refresh.Size = new Size(70, 32);
            _mb_btn_refresh.TabIndex = 16;
            _mb_btn_refresh.Text = "刷新COM";
            _mb_btn_refresh.UseVisualStyleBackColor = true;
            // 
            // _mb_btn_open
            // 
            _mb_btn_open.BackColor = Color.FromArgb(60, 140, 60);
            _mb_btn_open.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            _mb_btn_open.ForeColor = Color.White;
            _mb_btn_open.Location = new Point(628, 67);
            _mb_btn_open.Name = "_mb_btn_open";
            _mb_btn_open.Size = new Size(85, 32);
            _mb_btn_open.TabIndex = 17;
            _mb_btn_open.Text = "打开串口";
            _mb_btn_open.UseVisualStyleBackColor = false;
            // 
            // _mb_lb_status
            // 
            _mb_lb_status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _mb_lb_status.AutoSize = true;
            _mb_lb_status.Location = new Point(733, 74);
            _mb_lb_status.Name = "_mb_lb_status";
            _mb_lb_status.Size = new Size(51, 20);
            _mb_lb_status.TabIndex = 18;
            _mb_lb_status.Text = "已断开";
            // 
            // _mb_sep1
            // 
            _mb_sep1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _mb_sep1.BorderStyle = BorderStyle.Fixed3D;
            _mb_sep1.Location = new Point(12, 109);
            _mb_sep1.Name = "_mb_sep1";
            _mb_sep1.Size = new Size(928, 2);
            _mb_sep1.TabIndex = 19;
            // 
            // _mb_lb_dev
            // 
            _mb_lb_dev.AutoSize = true;
            _mb_lb_dev.Location = new Point(12, 129);
            _mb_lb_dev.Name = "_mb_lb_dev";
            _mb_lb_dev.Size = new Size(65, 20);
            _mb_lb_dev.TabIndex = 20;
            _mb_lb_dev.Text = "设备地址";
            // 
            // _mb_box_dev
            // 
            _mb_box_dev.Location = new Point(80, 126);
            _mb_box_dev.Name = "_mb_box_dev";
            _mb_box_dev.Size = new Size(50, 25);
            _mb_box_dev.TabIndex = 21;
            _mb_box_dev.Text = "1";
            _mb_box_dev.TextAlign = HorizontalAlignment.Center;
            // 
            // _mb_lb_func
            // 
            _mb_lb_func.AutoSize = true;
            _mb_lb_func.Location = new Point(145, 129);
            _mb_lb_func.Name = "_mb_lb_func";
            _mb_lb_func.Size = new Size(51, 20);
            _mb_lb_func.TabIndex = 22;
            _mb_lb_func.Text = "功能码";
            // 
            // _mb_drop_func
            // 
            _mb_drop_func.DropDownStyle = ComboBoxStyle.DropDownList;
            _mb_drop_func.FormattingEnabled = true;
            _mb_drop_func.Location = new Point(195, 126);
            _mb_drop_func.Name = "_mb_drop_func";
            _mb_drop_func.Size = new Size(155, 27);
            _mb_drop_func.TabIndex = 23;
            // 
            // _mb_lb_addr
            // 
            _mb_lb_addr.AutoSize = true;
            _mb_lb_addr.Location = new Point(365, 129);
            _mb_lb_addr.Name = "_mb_lb_addr";
            _mb_lb_addr.Size = new Size(65, 20);
            _mb_lb_addr.TabIndex = 24;
            _mb_lb_addr.Text = "起始地址";
            // 
            // _mb_box_addr
            // 
            _mb_box_addr.Location = new Point(430, 126);
            _mb_box_addr.Name = "_mb_box_addr";
            _mb_box_addr.Size = new Size(60, 25);
            _mb_box_addr.TabIndex = 25;
            _mb_box_addr.Text = "0";
            _mb_box_addr.TextAlign = HorizontalAlignment.Center;
            // 
            // _mb_lb_count
            // 
            _mb_lb_count.AutoSize = true;
            _mb_lb_count.Location = new Point(505, 129);
            _mb_lb_count.Name = "_mb_lb_count";
            _mb_lb_count.Size = new Size(37, 20);
            _mb_lb_count.TabIndex = 26;
            _mb_lb_count.Text = "数量";
            // 
            // _mb_box_count
            // 
            _mb_box_count.Location = new Point(543, 126);
            _mb_box_count.Name = "_mb_box_count";
            _mb_box_count.Size = new Size(55, 25);
            _mb_box_count.TabIndex = 27;
            _mb_box_count.Text = "10";
            _mb_box_count.TextAlign = HorizontalAlignment.Center;
            // 
            // _mb_btn_read
            // 
            _mb_btn_read.BackColor = Color.FromArgb(0, 120, 215);
            _mb_btn_read.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _mb_btn_read.ForeColor = Color.White;
            _mb_btn_read.Location = new Point(615, 122);
            _mb_btn_read.Name = "_mb_btn_read";
            _mb_btn_read.Size = new Size(100, 32);
            _mb_btn_read.TabIndex = 28;
            _mb_btn_read.Text = "读取";
            _mb_btn_read.UseVisualStyleBackColor = false;
            // 
            // _mb_btn_polling
            // 
            _mb_btn_polling.BackColor = Color.FromArgb(60, 140, 60);
            _mb_btn_polling.FlatAppearance.BorderColor = Color.FromArgb(255, 255, 200);
            _mb_btn_polling.FlatAppearance.BorderSize = 2;
            _mb_btn_polling.FlatStyle = FlatStyle.Flat;
            _mb_btn_polling.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _mb_btn_polling.ForeColor = Color.White;
            _mb_btn_polling.Location = new Point(725, 122);
            _mb_btn_polling.Name = "_mb_btn_polling";
            _mb_btn_polling.Size = new Size(95, 32);
            _mb_btn_polling.TabIndex = 55;
            _mb_btn_polling.Text = "▶ 轮询";
            _mb_btn_polling.UseVisualStyleBackColor = false;
            _mb_btn_polling.Click += _mb_btn_polling_Click;
            // 
            // _mb_lb_polling_status
            // 
            _mb_lb_polling_status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _mb_lb_polling_status.Font = new Font("Consolas", 9F);
            _mb_lb_polling_status.ForeColor = Color.FromArgb(100, 100, 100);
            _mb_lb_polling_status.Location = new Point(80, 162);
            _mb_lb_polling_status.Name = "_mb_lb_polling_status";
            _mb_lb_polling_status.Size = new Size(860, 20);
            _mb_lb_polling_status.TabIndex = 56;
            _mb_lb_polling_status.Text = "队列=0  成功率=0%  已发=0  失败=0";
            _mb_lb_polling_status.TextAlign = ContentAlignment.MiddleRight;
            _mb_lb_polling_status.Visible = false;
            // 
            // _mb_lb_poll_interval_label
            // 
            _mb_lb_poll_interval_label.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _mb_lb_poll_interval_label.AutoSize = true;
            _mb_lb_poll_interval_label.Font = new Font("Microsoft YaHei UI", 9F);
            _mb_lb_poll_interval_label.Location = new Point(836, 128);
            _mb_lb_poll_interval_label.Name = "_mb_lb_poll_interval_label";
            _mb_lb_poll_interval_label.Size = new Size(32, 17);
            _mb_lb_poll_interval_label.TabIndex = 57;
            _mb_lb_poll_interval_label.Text = "间隔";
            // 
            // _mb_polling_interval
            // 
            _mb_polling_interval.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _mb_polling_interval.Font = new Font("Microsoft YaHei UI", 9F);
            _mb_polling_interval.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            _mb_polling_interval.Location = new Point(868, 124);
            _mb_polling_interval.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
            _mb_polling_interval.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            _mb_polling_interval.Name = "_mb_polling_interval";
            _mb_polling_interval.Size = new Size(72, 23);
            _mb_polling_interval.TabIndex = 58;
            _mb_polling_interval.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            // 
            // _mb_lb_send_hex
            // 
            _mb_lb_send_hex.AutoSize = true;
            _mb_lb_send_hex.Location = new Point(12, 191);
            _mb_lb_send_hex.Name = "_mb_lb_send_hex";
            _mb_lb_send_hex.Size = new Size(65, 20);
            _mb_lb_send_hex.TabIndex = 29;
            _mb_lb_send_hex.Text = "发送报文";
            // 
            // _mb_box_send_hex
            // 
            _mb_box_send_hex.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _mb_box_send_hex.BackColor = Color.FromArgb(245, 245, 245);
            _mb_box_send_hex.Font = new Font("Consolas", 10F);
            _mb_box_send_hex.Location = new Point(80, 191);
            _mb_box_send_hex.Name = "_mb_box_send_hex";
            _mb_box_send_hex.ReadOnly = true;
            _mb_box_send_hex.Size = new Size(914, 23);
            _mb_box_send_hex.TabIndex = 30;
            _mb_box_send_hex.Text = "";
            // 
            // _mb_lb_recv_hex
            // 
            _mb_lb_recv_hex.AutoSize = true;
            _mb_lb_recv_hex.Location = new Point(12, 226);
            _mb_lb_recv_hex.Name = "_mb_lb_recv_hex";
            _mb_lb_recv_hex.Size = new Size(65, 20);
            _mb_lb_recv_hex.TabIndex = 31;
            _mb_lb_recv_hex.Text = "接收报文";
            // 
            // _mb_box_recv_hex
            // 
            _mb_box_recv_hex.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _mb_box_recv_hex.BackColor = Color.FromArgb(245, 245, 245);
            _mb_box_recv_hex.Font = new Font("Consolas", 10F);
            _mb_box_recv_hex.Location = new Point(80, 226);
            _mb_box_recv_hex.Name = "_mb_box_recv_hex";
            _mb_box_recv_hex.ReadOnly = true;
            _mb_box_recv_hex.Size = new Size(914, 23);
            _mb_box_recv_hex.TabIndex = 32;
            _mb_box_recv_hex.Text = "";
            // 
            // _mb_lb_result
            // 
            _mb_lb_result.AutoSize = true;
            _mb_lb_result.Location = new Point(12, 261);
            _mb_lb_result.Name = "_mb_lb_result";
            _mb_lb_result.Size = new Size(65, 20);
            _mb_lb_result.TabIndex = 33;
            _mb_lb_result.Text = "数据解析";
            // 
            // _mb_grid_result
            // 
            _mb_grid_result.AllowUserToAddRows = false;
            _mb_grid_result.AllowUserToDeleteRows = false;
            _mb_grid_result.AllowUserToResizeRows = false;
            _mb_grid_result.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _mb_grid_result.BackgroundColor = Color.FromArgb(250, 250, 250);
            _mb_grid_result.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
            _mb_grid_result.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            _mb_grid_result.ColumnHeadersHeight = 35;
            _mb_grid_result.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Consolas", 10F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            _mb_grid_result.DefaultCellStyle = dataGridViewCellStyle2;
            _mb_grid_result.Location = new Point(80, 261);
            _mb_grid_result.Name = "_mb_grid_result";
            _mb_grid_result.ReadOnly = true;
            _mb_grid_result.RowHeadersVisible = false;
            _mb_grid_result.Size = new Size(914, 325);
            _mb_grid_result.TabIndex = 34;
            // 
            // _mb_btn_clear
            // 
            _mb_btn_clear.Location = new Point(19, 288);
            _mb_btn_clear.Name = "_mb_btn_clear";
            _mb_btn_clear.Size = new Size(50, 32);
            _mb_btn_clear.TabIndex = 35;
            _mb_btn_clear.Text = "清空";
            _mb_btn_clear.UseVisualStyleBackColor = true;
            // 
            // _mb_lb_log
            // 
            _mb_lb_log.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _mb_lb_log.AutoSize = true;
            _mb_lb_log.Location = new Point(12, 592);
            _mb_lb_log.Name = "_mb_lb_log";
            _mb_lb_log.Size = new Size(65, 20);
            _mb_lb_log.TabIndex = 36;
            _mb_lb_log.Text = "通信日志";
            // 
            // _mb_box_recv
            // 
            _mb_box_recv.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _mb_box_recv.BackColor = Color.FromArgb(30, 30, 30);
            _mb_box_recv.Font = new Font("Consolas", 9F);
            _mb_box_recv.ForeColor = Color.FromArgb(0, 255, 0);
            _mb_box_recv.Location = new Point(80, 592);
            _mb_box_recv.Multiline = true;
            _mb_box_recv.Name = "_mb_box_recv";
            _mb_box_recv.ReadOnly = true;
            _mb_box_recv.ScrollBars = ScrollBars.Both;
            _mb_box_recv.Size = new Size(914, 135);
            _mb_box_recv.TabIndex = 37;
            _mb_box_recv.WordWrap = false;
            // 
            // _mb_btn_learn
            // 
            _mb_btn_learn.BackColor = Color.FromArgb(255, 245, 215);
            _mb_btn_learn.Cursor = Cursors.Hand;
            _mb_btn_learn.FlatAppearance.BorderColor = Color.FromArgb(200, 180, 120);
            _mb_btn_learn.FlatStyle = FlatStyle.Flat;
            _mb_btn_learn.Font = new Font("微软雅黑", 9F);
            _mb_btn_learn.Location = new Point(777, 9);
            _mb_btn_learn.Margin = new Padding(0);
            _mb_btn_learn.Name = "_mb_btn_learn";
            _mb_btn_learn.Size = new Size(155, 28);
            _mb_btn_learn.TabIndex = 38;
            _mb_btn_learn.Text = "🔬 学委托/事件/反射";
            _mb_btn_learn.UseVisualStyleBackColor = false;
            // 
            // _tabChart
            // 
            _tabChart.Controls.Add(_splitChart);
            _tabChart.Location = new Point(4, 28);
            _tabChart.Name = "_tabChart";
            _tabChart.Size = new Size(1018, 739);
            _tabChart.TabIndex = 1;
            _tabChart.Text = "📈 实时曲线";
            // 
            // _splitChart
            // 
            _splitChart.Dock = DockStyle.Fill;
            _splitChart.Location = new Point(0, 0);
            _splitChart.Name = "_splitChart";
            // 
            // _splitChart.Panel1
            // 
            _splitChart.Panel1.Controls.Add(_panelChartConfig);
            // 
            // _splitChart.Panel2
            // 
            _splitChart.Panel2.Controls.Add(_panelChartArea);
            _splitChart.Size = new Size(1018, 739);
            _splitChart.SplitterDistance = 181;
            _splitChart.TabIndex = 0;
            // 
            // _panelChartConfig
            // 
            _panelChartConfig.Controls.Add(_listChannels);
            _panelChartConfig.Controls.Add(_lbChannelConfig);
            _panelChartConfig.Controls.Add(_panelChartBtns);
            _panelChartConfig.Dock = DockStyle.Fill;
            _panelChartConfig.Location = new Point(0, 0);
            _panelChartConfig.Name = "_panelChartConfig";
            _panelChartConfig.Padding = new Padding(10);
            _panelChartConfig.Size = new Size(181, 739);
            _panelChartConfig.TabIndex = 0;
            // 
            // _listChannels
            // 
            _listChannels.Dock = DockStyle.Fill;
            _listChannels.Font = new Font("Consolas", 9F);
            _listChannels.Items.AddRange(new object[] { "CH1: 伺服转速 (D100) 🔴", "CH2: 伺服转矩 (D102) 🔵", "CH3: 变频器频率 (D200) \U0001f7e2", "CH4: 电流 (D202) \U0001f7e1" });
            _listChannels.Location = new Point(10, 40);
            _listChannels.Name = "_listChannels";
            _listChannels.Size = new Size(161, 649);
            _listChannels.TabIndex = 0;
            // 
            // _lbChannelConfig
            // 
            _lbChannelConfig.Dock = DockStyle.Top;
            _lbChannelConfig.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            _lbChannelConfig.Location = new Point(10, 10);
            _lbChannelConfig.Name = "_lbChannelConfig";
            _lbChannelConfig.Size = new Size(161, 30);
            _lbChannelConfig.TabIndex = 1;
            _lbChannelConfig.Text = "通道配置";
            // 
            // _panelChartBtns
            // 
            _panelChartBtns.Controls.Add(_btnChartStart);
            _panelChartBtns.Controls.Add(_btnChartPause);
            _panelChartBtns.Controls.Add(_btnChartClear);
            _panelChartBtns.Controls.Add(_btnChartExport);
            _panelChartBtns.Dock = DockStyle.Bottom;
            _panelChartBtns.Location = new Point(10, 689);
            _panelChartBtns.Name = "_panelChartBtns";
            _panelChartBtns.Size = new Size(161, 40);
            _panelChartBtns.TabIndex = 2;
            // 
            // _btnChartStart
            // 
            _btnChartStart.Location = new Point(3, 3);
            _btnChartStart.Name = "_btnChartStart";
            _btnChartStart.Size = new Size(80, 32);
            _btnChartStart.TabIndex = 0;
            _btnChartStart.Text = "▶ 开始";
            // 
            // _btnChartPause
            // 
            _btnChartPause.Location = new Point(3, 41);
            _btnChartPause.Name = "_btnChartPause";
            _btnChartPause.Size = new Size(80, 32);
            _btnChartPause.TabIndex = 1;
            _btnChartPause.Text = "⏸ 暂停";
            // 
            // _btnChartClear
            // 
            _btnChartClear.Location = new Point(3, 79);
            _btnChartClear.Name = "_btnChartClear";
            _btnChartClear.Size = new Size(80, 32);
            _btnChartClear.TabIndex = 2;
            _btnChartClear.Text = "🗑️ 清空";
            // 
            // _btnChartExport
            // 
            _btnChartExport.Location = new Point(3, 117);
            _btnChartExport.Name = "_btnChartExport";
            _btnChartExport.Size = new Size(80, 32);
            _btnChartExport.TabIndex = 3;
            _btnChartExport.Text = "💾 导出";
            // 
            // _panelChartArea
            // 
            _panelChartArea.Dock = DockStyle.Fill;
            _panelChartArea.Location = new Point(0, 0);
            _panelChartArea.Name = "_panelChartArea";
            _panelChartArea.Size = new Size(833, 739);
            _panelChartArea.TabIndex = 0;
            // 
            // _tabAlarm
            // 
            _tabAlarm.Controls.Add(_splitAlarm);
            _tabAlarm.Location = new Point(4, 28);
            _tabAlarm.Name = "_tabAlarm";
            _tabAlarm.Size = new Size(1018, 739);
            _tabAlarm.TabIndex = 2;
            _tabAlarm.Text = "⚠️ 报警系统";
            // 
            // _splitAlarm
            // 
            _splitAlarm.Dock = DockStyle.Fill;
            _splitAlarm.Location = new Point(0, 0);
            _splitAlarm.Name = "_splitAlarm";
            // 
            // _splitAlarm.Panel1
            // 
            _splitAlarm.Panel1.Controls.Add(_panelAlarmRules);
            // 
            // _splitAlarm.Panel2
            // 
            _splitAlarm.Panel2.Controls.Add(_panelAlarmList);
            _splitAlarm.Size = new Size(1018, 739);
            _splitAlarm.SplitterDistance = 255;
            _splitAlarm.TabIndex = 0;
            // 
            // _panelAlarmRules
            // 
            _panelAlarmRules.Controls.Add(_gridAlarmRules);
            _panelAlarmRules.Controls.Add(_lbAlarmRules);
            _panelAlarmRules.Controls.Add(_panelAlarmRuleBtns);
            _panelAlarmRules.Dock = DockStyle.Fill;
            _panelAlarmRules.Location = new Point(0, 0);
            _panelAlarmRules.Name = "_panelAlarmRules";
            _panelAlarmRules.Padding = new Padding(10);
            _panelAlarmRules.Size = new Size(255, 739);
            _panelAlarmRules.TabIndex = 0;
            // 
            // _gridAlarmRules
            // 
            _gridAlarmRules.AllowUserToAddRows = false;
            _gridAlarmRules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridAlarmRules.BackgroundColor = Color.White;
            _gridAlarmRules.ColumnHeadersHeight = 35;
            _gridAlarmRules.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridAlarmRules.Columns.AddRange(new DataGridViewColumn[] { _colRuleName, _colRuleDevice, _colRuleAddress, _colRuleCondition, _colRuleLevel, _colRuleEnabled });
            _gridAlarmRules.Dock = DockStyle.Fill;
            _gridAlarmRules.Location = new Point(10, 40);
            _gridAlarmRules.Name = "_gridAlarmRules";
            _gridAlarmRules.ReadOnly = true;
            _gridAlarmRules.RowHeadersVisible = false;
            _gridAlarmRules.Size = new Size(235, 649);
            _gridAlarmRules.TabIndex = 0;
            // 
            // _colRuleName
            // 
            _colRuleName.HeaderText = "规则名称";
            _colRuleName.Name = "_colRuleName";
            _colRuleName.ReadOnly = true;
            // 
            // _colRuleDevice
            // 
            _colRuleDevice.HeaderText = "设备";
            _colRuleDevice.Name = "_colRuleDevice";
            _colRuleDevice.ReadOnly = true;
            // 
            // _colRuleAddress
            // 
            _colRuleAddress.HeaderText = "地址";
            _colRuleAddress.Name = "_colRuleAddress";
            _colRuleAddress.ReadOnly = true;
            // 
            // _colRuleCondition
            // 
            _colRuleCondition.HeaderText = "条件";
            _colRuleCondition.Name = "_colRuleCondition";
            _colRuleCondition.ReadOnly = true;
            // 
            // _colRuleLevel
            // 
            _colRuleLevel.HeaderText = "等级";
            _colRuleLevel.Name = "_colRuleLevel";
            _colRuleLevel.ReadOnly = true;
            // 
            // _colRuleEnabled
            // 
            _colRuleEnabled.HeaderText = "启用";
            _colRuleEnabled.Name = "_colRuleEnabled";
            _colRuleEnabled.ReadOnly = true;
            // 
            // _lbAlarmRules
            // 
            _lbAlarmRules.Dock = DockStyle.Top;
            _lbAlarmRules.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            _lbAlarmRules.Location = new Point(10, 10);
            _lbAlarmRules.Name = "_lbAlarmRules";
            _lbAlarmRules.Size = new Size(235, 30);
            _lbAlarmRules.TabIndex = 1;
            _lbAlarmRules.Text = "报警规则";
            // 
            // _panelAlarmRuleBtns
            // 
            _panelAlarmRuleBtns.Controls.Add(_btnAddRule);
            _panelAlarmRuleBtns.Controls.Add(_btnEditRule);
            _panelAlarmRuleBtns.Controls.Add(_btnDeleteRule);
            _panelAlarmRuleBtns.Dock = DockStyle.Bottom;
            _panelAlarmRuleBtns.Location = new Point(10, 689);
            _panelAlarmRuleBtns.Name = "_panelAlarmRuleBtns";
            _panelAlarmRuleBtns.Size = new Size(235, 40);
            _panelAlarmRuleBtns.TabIndex = 2;
            // 
            // _btnAddRule
            // 
            _btnAddRule.Location = new Point(3, 3);
            _btnAddRule.Name = "_btnAddRule";
            _btnAddRule.Size = new Size(80, 32);
            _btnAddRule.TabIndex = 0;
            _btnAddRule.Text = "➕ 添加";
            // 
            // _btnEditRule
            // 
            _btnEditRule.Location = new Point(89, 3);
            _btnEditRule.Name = "_btnEditRule";
            _btnEditRule.Size = new Size(80, 32);
            _btnEditRule.TabIndex = 1;
            _btnEditRule.Text = "✏️ 编辑";
            // 
            // _btnDeleteRule
            // 
            _btnDeleteRule.Location = new Point(3, 41);
            _btnDeleteRule.Name = "_btnDeleteRule";
            _btnDeleteRule.Size = new Size(80, 32);
            _btnDeleteRule.TabIndex = 2;
            _btnDeleteRule.Text = "🗑️ 删除";
            // 
            // _panelAlarmList
            // 
            _panelAlarmList.Controls.Add(_gridAlarms);
            _panelAlarmList.Controls.Add(_lbAlarmList);
            _panelAlarmList.Controls.Add(_panelAlarmBtns);
            _panelAlarmList.Dock = DockStyle.Fill;
            _panelAlarmList.Location = new Point(0, 0);
            _panelAlarmList.Name = "_panelAlarmList";
            _panelAlarmList.Padding = new Padding(10);
            _panelAlarmList.Size = new Size(759, 739);
            _panelAlarmList.TabIndex = 0;
            // 
            // _gridAlarms
            // 
            _gridAlarms.AllowUserToAddRows = false;
            _gridAlarms.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridAlarms.BackgroundColor = Color.White;
            _gridAlarms.ColumnHeadersHeight = 35;
            _gridAlarms.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridAlarms.Columns.AddRange(new DataGridViewColumn[] { _colAlarmTime, _colAlarmLevel, _colAlarmDevice, _colAlarmDesc, _colAlarmStatus });
            _gridAlarms.Dock = DockStyle.Fill;
            _gridAlarms.Location = new Point(10, 40);
            _gridAlarms.Name = "_gridAlarms";
            _gridAlarms.ReadOnly = true;
            _gridAlarms.RowHeadersVisible = false;
            _gridAlarms.Size = new Size(739, 649);
            _gridAlarms.TabIndex = 0;
            // 
            // _colAlarmTime
            // 
            _colAlarmTime.HeaderText = "时间";
            _colAlarmTime.Name = "_colAlarmTime";
            _colAlarmTime.ReadOnly = true;
            // 
            // _colAlarmLevel
            // 
            _colAlarmLevel.HeaderText = "等级";
            _colAlarmLevel.Name = "_colAlarmLevel";
            _colAlarmLevel.ReadOnly = true;
            // 
            // _colAlarmDevice
            // 
            _colAlarmDevice.HeaderText = "设备";
            _colAlarmDevice.Name = "_colAlarmDevice";
            _colAlarmDevice.ReadOnly = true;
            // 
            // _colAlarmDesc
            // 
            _colAlarmDesc.HeaderText = "描述";
            _colAlarmDesc.Name = "_colAlarmDesc";
            _colAlarmDesc.ReadOnly = true;
            // 
            // _colAlarmStatus
            // 
            _colAlarmStatus.HeaderText = "状态";
            _colAlarmStatus.Name = "_colAlarmStatus";
            _colAlarmStatus.ReadOnly = true;
            // 
            // _lbAlarmList
            // 
            _lbAlarmList.Dock = DockStyle.Top;
            _lbAlarmList.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            _lbAlarmList.Location = new Point(10, 10);
            _lbAlarmList.Name = "_lbAlarmList";
            _lbAlarmList.Size = new Size(739, 30);
            _lbAlarmList.TabIndex = 1;
            _lbAlarmList.Text = "实时报警";
            // 
            // _panelAlarmBtns
            // 
            _panelAlarmBtns.Controls.Add(_btnConfirmAlarm);
            _panelAlarmBtns.Controls.Add(_btnResetAlarm);
            _panelAlarmBtns.Controls.Add(_btnExportAlarm);
            _panelAlarmBtns.Dock = DockStyle.Bottom;
            _panelAlarmBtns.Location = new Point(10, 689);
            _panelAlarmBtns.Name = "_panelAlarmBtns";
            _panelAlarmBtns.Size = new Size(739, 40);
            _panelAlarmBtns.TabIndex = 2;
            // 
            // _btnConfirmAlarm
            // 
            _btnConfirmAlarm.Location = new Point(3, 3);
            _btnConfirmAlarm.Name = "_btnConfirmAlarm";
            _btnConfirmAlarm.Size = new Size(80, 32);
            _btnConfirmAlarm.TabIndex = 0;
            _btnConfirmAlarm.Text = "✅ 确认";
            // 
            // _btnResetAlarm
            // 
            _btnResetAlarm.Location = new Point(89, 3);
            _btnResetAlarm.Name = "_btnResetAlarm";
            _btnResetAlarm.Size = new Size(80, 32);
            _btnResetAlarm.TabIndex = 1;
            _btnResetAlarm.Text = "🔄 复位";
            // 
            // _btnExportAlarm
            // 
            _btnExportAlarm.Location = new Point(175, 3);
            _btnExportAlarm.Name = "_btnExportAlarm";
            _btnExportAlarm.Size = new Size(80, 32);
            _btnExportAlarm.TabIndex = 2;
            _btnExportAlarm.Text = "📤 导出";
            // 
            // _tabRecipe
            // 
            _tabRecipe.Controls.Add(_splitRecipe);
            _tabRecipe.Location = new Point(4, 28);
            _tabRecipe.Name = "_tabRecipe";
            _tabRecipe.Size = new Size(1018, 739);
            _tabRecipe.TabIndex = 3;
            _tabRecipe.Text = "📋 配方管理";
            // 
            // _splitRecipe
            // 
            _splitRecipe.Dock = DockStyle.Fill;
            _splitRecipe.Location = new Point(0, 0);
            _splitRecipe.Name = "_splitRecipe";
            // 
            // _splitRecipe.Panel1
            // 
            _splitRecipe.Panel1.Controls.Add(_panelRecipeList);
            // 
            // _splitRecipe.Panel2
            // 
            _splitRecipe.Panel2.Controls.Add(_panelRecipeEdit);
            _splitRecipe.Size = new Size(1018, 739);
            _splitRecipe.SplitterDistance = 291;
            _splitRecipe.TabIndex = 0;
            // 
            // _panelRecipeList
            // 
            _panelRecipeList.Controls.Add(_gridRecipes);
            _panelRecipeList.Controls.Add(_lbRecipeList);
            _panelRecipeList.Controls.Add(_panelRecipeBtns);
            _panelRecipeList.Dock = DockStyle.Fill;
            _panelRecipeList.Location = new Point(0, 0);
            _panelRecipeList.Name = "_panelRecipeList";
            _panelRecipeList.Padding = new Padding(10);
            _panelRecipeList.Size = new Size(291, 739);
            _panelRecipeList.TabIndex = 0;
            // 
            // _gridRecipes
            // 
            _gridRecipes.AllowUserToAddRows = false;
            _gridRecipes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridRecipes.BackgroundColor = Color.White;
            _gridRecipes.ColumnHeadersHeight = 35;
            _gridRecipes.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridRecipes.Columns.AddRange(new DataGridViewColumn[] { _colRecipeName, _colRecipeTime, _colRecipeParams, _colRecipeVersion });
            _gridRecipes.Dock = DockStyle.Fill;
            _gridRecipes.Location = new Point(10, 40);
            _gridRecipes.Name = "_gridRecipes";
            _gridRecipes.ReadOnly = true;
            _gridRecipes.RowHeadersVisible = false;
            _gridRecipes.Size = new Size(271, 649);
            _gridRecipes.TabIndex = 0;
            // 
            // _colRecipeName
            // 
            _colRecipeName.HeaderText = "名称";
            _colRecipeName.Name = "_colRecipeName";
            _colRecipeName.ReadOnly = true;
            // 
            // _colRecipeTime
            // 
            _colRecipeTime.HeaderText = "创建时间";
            _colRecipeTime.Name = "_colRecipeTime";
            _colRecipeTime.ReadOnly = true;
            // 
            // _colRecipeParams
            // 
            _colRecipeParams.HeaderText = "参数数量";
            _colRecipeParams.Name = "_colRecipeParams";
            _colRecipeParams.ReadOnly = true;
            // 
            // _colRecipeVersion
            // 
            _colRecipeVersion.HeaderText = "版本";
            _colRecipeVersion.Name = "_colRecipeVersion";
            _colRecipeVersion.ReadOnly = true;
            // 
            // _lbRecipeList
            // 
            _lbRecipeList.Dock = DockStyle.Top;
            _lbRecipeList.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            _lbRecipeList.Location = new Point(10, 10);
            _lbRecipeList.Name = "_lbRecipeList";
            _lbRecipeList.Size = new Size(271, 30);
            _lbRecipeList.TabIndex = 1;
            _lbRecipeList.Text = "配方列表";
            // 
            // _panelRecipeBtns
            // 
            _panelRecipeBtns.Controls.Add(_btnNewRecipe);
            _panelRecipeBtns.Controls.Add(_btnCopyRecipe);
            _panelRecipeBtns.Controls.Add(_btnDeleteRecipe);
            _panelRecipeBtns.Dock = DockStyle.Bottom;
            _panelRecipeBtns.Location = new Point(10, 689);
            _panelRecipeBtns.Name = "_panelRecipeBtns";
            _panelRecipeBtns.Size = new Size(271, 40);
            _panelRecipeBtns.TabIndex = 2;
            // 
            // _btnNewRecipe
            // 
            _btnNewRecipe.Location = new Point(3, 3);
            _btnNewRecipe.Name = "_btnNewRecipe";
            _btnNewRecipe.Size = new Size(80, 32);
            _btnNewRecipe.TabIndex = 0;
            _btnNewRecipe.Text = "➕ 新建";
            // 
            // _btnCopyRecipe
            // 
            _btnCopyRecipe.Location = new Point(89, 3);
            _btnCopyRecipe.Name = "_btnCopyRecipe";
            _btnCopyRecipe.Size = new Size(80, 32);
            _btnCopyRecipe.TabIndex = 1;
            _btnCopyRecipe.Text = "📋 复制";
            // 
            // _btnDeleteRecipe
            // 
            _btnDeleteRecipe.Location = new Point(175, 3);
            _btnDeleteRecipe.Name = "_btnDeleteRecipe";
            _btnDeleteRecipe.Size = new Size(80, 32);
            _btnDeleteRecipe.TabIndex = 2;
            _btnDeleteRecipe.Text = "🗑️ 删除";
            // 
            // _panelRecipeEdit
            // 
            _panelRecipeEdit.Controls.Add(_gridRecipeParams);
            _panelRecipeEdit.Controls.Add(_lbRecipeEdit);
            _panelRecipeEdit.Controls.Add(_panelRecipeEditBtns);
            _panelRecipeEdit.Dock = DockStyle.Fill;
            _panelRecipeEdit.Location = new Point(0, 0);
            _panelRecipeEdit.Name = "_panelRecipeEdit";
            _panelRecipeEdit.Padding = new Padding(10);
            _panelRecipeEdit.Size = new Size(723, 739);
            _panelRecipeEdit.TabIndex = 0;
            // 
            // _gridRecipeParams
            // 
            _gridRecipeParams.AllowUserToAddRows = false;
            _gridRecipeParams.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridRecipeParams.BackgroundColor = Color.White;
            _gridRecipeParams.ColumnHeadersHeight = 35;
            _gridRecipeParams.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridRecipeParams.Columns.AddRange(new DataGridViewColumn[] { _colParamIndex, _colParamName, _colParamAddress, _colParamCurrent, _colParamNew, _colParamUnit });
            _gridRecipeParams.Dock = DockStyle.Fill;
            _gridRecipeParams.Location = new Point(10, 40);
            _gridRecipeParams.Name = "_gridRecipeParams";
            _gridRecipeParams.RowHeadersVisible = false;
            _gridRecipeParams.Size = new Size(703, 649);
            _gridRecipeParams.TabIndex = 0;
            // 
            // _colParamIndex
            // 
            _colParamIndex.HeaderText = "序号";
            _colParamIndex.Name = "_colParamIndex";
            _colParamIndex.ReadOnly = true;
            // 
            // _colParamName
            // 
            _colParamName.HeaderText = "参数名称";
            _colParamName.Name = "_colParamName";
            // 
            // _colParamAddress
            // 
            _colParamAddress.HeaderText = "PLC地址";
            _colParamAddress.Name = "_colParamAddress";
            _colParamAddress.ReadOnly = true;
            // 
            // _colParamCurrent
            // 
            _colParamCurrent.HeaderText = "当前值";
            _colParamCurrent.Name = "_colParamCurrent";
            _colParamCurrent.ReadOnly = true;
            // 
            // _colParamNew
            // 
            _colParamNew.HeaderText = "新值";
            _colParamNew.Name = "_colParamNew";
            // 
            // _colParamUnit
            // 
            _colParamUnit.HeaderText = "单位";
            _colParamUnit.Name = "_colParamUnit";
            _colParamUnit.ReadOnly = true;
            // 
            // _lbRecipeEdit
            // 
            _lbRecipeEdit.Dock = DockStyle.Top;
            _lbRecipeEdit.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            _lbRecipeEdit.Location = new Point(10, 10);
            _lbRecipeEdit.Name = "_lbRecipeEdit";
            _lbRecipeEdit.Size = new Size(703, 30);
            _lbRecipeEdit.TabIndex = 1;
            _lbRecipeEdit.Text = "配方参数";
            // 
            // _panelRecipeEditBtns
            // 
            _panelRecipeEditBtns.Controls.Add(_btnReadPlc);
            _panelRecipeEditBtns.Controls.Add(_btnDownloadPlc);
            _panelRecipeEditBtns.Controls.Add(_btnSaveRecipe);
            _panelRecipeEditBtns.Dock = DockStyle.Bottom;
            _panelRecipeEditBtns.Location = new Point(10, 689);
            _panelRecipeEditBtns.Name = "_panelRecipeEditBtns";
            _panelRecipeEditBtns.Size = new Size(703, 40);
            _panelRecipeEditBtns.TabIndex = 2;
            // 
            // _btnReadPlc
            // 
            _btnReadPlc.Location = new Point(3, 3);
            _btnReadPlc.Name = "_btnReadPlc";
            _btnReadPlc.Size = new Size(100, 32);
            _btnReadPlc.TabIndex = 0;
            _btnReadPlc.Text = "📥 从PLC读取";
            // 
            // _btnDownloadPlc
            // 
            _btnDownloadPlc.Location = new Point(109, 3);
            _btnDownloadPlc.Name = "_btnDownloadPlc";
            _btnDownloadPlc.Size = new Size(100, 32);
            _btnDownloadPlc.TabIndex = 1;
            _btnDownloadPlc.Text = "📤 下发到PLC";
            // 
            // _btnSaveRecipe
            // 
            _btnSaveRecipe.Location = new Point(215, 3);
            _btnSaveRecipe.Name = "_btnSaveRecipe";
            _btnSaveRecipe.Size = new Size(80, 32);
            _btnSaveRecipe.TabIndex = 2;
            _btnSaveRecipe.Text = "💾 保存";
            // 
            // _tabReport
            // 
            _tabReport.Controls.Add(_panelReport);
            _tabReport.Location = new Point(4, 28);
            _tabReport.Name = "_tabReport";
            _tabReport.Size = new Size(1018, 739);
            _tabReport.TabIndex = 4;
            _tabReport.Text = "📊 生产报表";
            // 
            // _panelReport
            // 
            _panelReport.Controls.Add(_gridReport);
            _panelReport.Controls.Add(_panelStats);
            _panelReport.Controls.Add(_panelReportFilter);
            _panelReport.Controls.Add(_panelReportBtns);
            _panelReport.Dock = DockStyle.Fill;
            _panelReport.Location = new Point(0, 0);
            _panelReport.Name = "_panelReport";
            _panelReport.Padding = new Padding(10);
            _panelReport.Size = new Size(1018, 739);
            _panelReport.TabIndex = 0;
            // 
            // _gridReport
            // 
            _gridReport.AllowUserToAddRows = false;
            _gridReport.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridReport.BackgroundColor = Color.White;
            _gridReport.ColumnHeadersHeight = 35;
            _gridReport.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridReport.Columns.AddRange(new DataGridViewColumn[] { _colRptTime, _colRptCount, _colRptGood, _colRptDefect, _colRptCycle, _colRptStatus });
            _gridReport.Dock = DockStyle.Fill;
            _gridReport.Location = new Point(10, 160);
            _gridReport.Name = "_gridReport";
            _gridReport.ReadOnly = true;
            _gridReport.RowHeadersVisible = false;
            _gridReport.Size = new Size(998, 529);
            _gridReport.TabIndex = 0;
            // 
            // _colRptTime
            // 
            _colRptTime.HeaderText = "时间";
            _colRptTime.Name = "_colRptTime";
            _colRptTime.ReadOnly = true;
            // 
            // _colRptCount
            // 
            _colRptCount.HeaderText = "产量";
            _colRptCount.Name = "_colRptCount";
            _colRptCount.ReadOnly = true;
            // 
            // _colRptGood
            // 
            _colRptGood.HeaderText = "合格";
            _colRptGood.Name = "_colRptGood";
            _colRptGood.ReadOnly = true;
            // 
            // _colRptDefect
            // 
            _colRptDefect.HeaderText = "不合格";
            _colRptDefect.Name = "_colRptDefect";
            _colRptDefect.ReadOnly = true;
            // 
            // _colRptCycle
            // 
            _colRptCycle.HeaderText = "节拍";
            _colRptCycle.Name = "_colRptCycle";
            _colRptCycle.ReadOnly = true;
            // 
            // _colRptStatus
            // 
            _colRptStatus.HeaderText = "状态";
            _colRptStatus.Name = "_colRptStatus";
            _colRptStatus.ReadOnly = true;
            // 
            // _panelStats
            // 
            _panelStats.Controls.Add(_cardTotal);
            _panelStats.Controls.Add(_cardQualify);
            _panelStats.Controls.Add(_cardCycle);
            _panelStats.Controls.Add(_cardAlarmCount);
            _panelStats.Dock = DockStyle.Top;
            _panelStats.Location = new Point(10, 60);
            _panelStats.Name = "_panelStats";
            _panelStats.Padding = new Padding(0, 10, 0, 0);
            _panelStats.Size = new Size(998, 100);
            _panelStats.TabIndex = 1;
            // 
            // _cardTotal
            // 
            _cardTotal.BackColor = Color.FromArgb(240, 248, 255);
            _cardTotal.Location = new Point(10, 20);
            _cardTotal.Margin = new Padding(10);
            _cardTotal.Name = "_cardTotal";
            _cardTotal.Size = new Size(150, 70);
            _cardTotal.TabIndex = 0;
            // 
            // _cardQualify
            // 
            _cardQualify.BackColor = Color.FromArgb(240, 248, 255);
            _cardQualify.Location = new Point(180, 20);
            _cardQualify.Margin = new Padding(10);
            _cardQualify.Name = "_cardQualify";
            _cardQualify.Size = new Size(150, 70);
            _cardQualify.TabIndex = 1;
            // 
            // _cardCycle
            // 
            _cardCycle.BackColor = Color.FromArgb(240, 248, 255);
            _cardCycle.Location = new Point(350, 20);
            _cardCycle.Margin = new Padding(10);
            _cardCycle.Name = "_cardCycle";
            _cardCycle.Size = new Size(150, 70);
            _cardCycle.TabIndex = 2;
            // 
            // _cardAlarmCount
            // 
            _cardAlarmCount.BackColor = Color.FromArgb(240, 248, 255);
            _cardAlarmCount.Location = new Point(520, 20);
            _cardAlarmCount.Margin = new Padding(10);
            _cardAlarmCount.Name = "_cardAlarmCount";
            _cardAlarmCount.Size = new Size(150, 70);
            _cardAlarmCount.TabIndex = 3;
            // 
            // _panelReportFilter
            // 
            _panelReportFilter.Controls.Add(_lbReportType);
            _panelReportFilter.Controls.Add(_dropReportType);
            _panelReportFilter.Controls.Add(_lbReportDate);
            _panelReportFilter.Controls.Add(_dateReport);
            _panelReportFilter.Controls.Add(_btnGenerateReport);
            _panelReportFilter.Dock = DockStyle.Top;
            _panelReportFilter.Location = new Point(10, 10);
            _panelReportFilter.Name = "_panelReportFilter";
            _panelReportFilter.Size = new Size(998, 50);
            _panelReportFilter.TabIndex = 2;
            // 
            // _lbReportType
            // 
            _lbReportType.AutoSize = true;
            _lbReportType.Location = new Point(3, 0);
            _lbReportType.Name = "_lbReportType";
            _lbReportType.Padding = new Padding(0, 8, 0, 0);
            _lbReportType.Size = new Size(68, 28);
            _lbReportType.TabIndex = 0;
            _lbReportType.Text = "报表类型:";
            // 
            // _dropReportType
            // 
            _dropReportType.DropDownStyle = ComboBoxStyle.DropDownList;
            _dropReportType.Items.AddRange(new object[] { "日报", "周报", "月报" });
            _dropReportType.Location = new Point(77, 3);
            _dropReportType.Name = "_dropReportType";
            _dropReportType.Size = new Size(100, 27);
            _dropReportType.TabIndex = 1;
            // 
            // _lbReportDate
            // 
            _lbReportDate.AutoSize = true;
            _lbReportDate.Location = new Point(183, 0);
            _lbReportDate.Name = "_lbReportDate";
            _lbReportDate.Padding = new Padding(10, 8, 0, 0);
            _lbReportDate.Size = new Size(50, 28);
            _lbReportDate.TabIndex = 2;
            _lbReportDate.Text = "日期:";
            // 
            // _dateReport
            // 
            _dateReport.Location = new Point(239, 3);
            _dateReport.Name = "_dateReport";
            _dateReport.Size = new Size(150, 25);
            _dateReport.TabIndex = 3;
            // 
            // _btnGenerateReport
            // 
            _btnGenerateReport.BackColor = Color.FromArgb(60, 140, 60);
            _btnGenerateReport.FlatStyle = FlatStyle.Flat;
            _btnGenerateReport.ForeColor = Color.White;
            _btnGenerateReport.Location = new Point(395, 3);
            _btnGenerateReport.Name = "_btnGenerateReport";
            _btnGenerateReport.Size = new Size(100, 32);
            _btnGenerateReport.TabIndex = 4;
            _btnGenerateReport.Text = "📊 生成报表";
            _btnGenerateReport.UseVisualStyleBackColor = false;
            // 
            // _panelReportBtns
            // 
            _panelReportBtns.Controls.Add(_btnExportExcel);
            _panelReportBtns.Controls.Add(_btnPrint);
            _panelReportBtns.Dock = DockStyle.Bottom;
            _panelReportBtns.Location = new Point(10, 689);
            _panelReportBtns.Name = "_panelReportBtns";
            _panelReportBtns.Size = new Size(998, 40);
            _panelReportBtns.TabIndex = 3;
            // 
            // _btnExportExcel
            // 
            _btnExportExcel.Location = new Point(3, 3);
            _btnExportExcel.Name = "_btnExportExcel";
            _btnExportExcel.Size = new Size(100, 32);
            _btnExportExcel.TabIndex = 0;
            _btnExportExcel.Text = "📊 导出 Excel";
            // 
            // _btnPrint
            // 
            _btnPrint.Location = new Point(109, 3);
            _btnPrint.Name = "_btnPrint";
            _btnPrint.Size = new Size(80, 32);
            _btnPrint.TabIndex = 1;
            _btnPrint.Text = "🖨️ 打印";
            // 
            // MainForm
            // 
            ClientSize = new Size(1026, 793);
            Controls.Add(_tabControl);
            Controls.Add(_statusStrip);
            Font = new Font("微软雅黑", 9F);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "🏭 工业监控系统 v1.0";
            _statusStrip.ResumeLayout(false);
            _statusStrip.PerformLayout();
            _tabControl.ResumeLayout(false);
            _tabMonitor.ResumeLayout(false);
            _panelMonitor.ResumeLayout(false);
            _panelMonitor.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)_mb_polling_interval).EndInit();
            ((System.ComponentModel.ISupportInitialize)_mb_grid_result).EndInit();
            _tabChart.ResumeLayout(false);
            _splitChart.Panel1.ResumeLayout(false);
            _splitChart.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_splitChart).EndInit();
            _splitChart.ResumeLayout(false);
            _panelChartConfig.ResumeLayout(false);
            _panelChartBtns.ResumeLayout(false);
            _tabAlarm.ResumeLayout(false);
            _splitAlarm.Panel1.ResumeLayout(false);
            _splitAlarm.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_splitAlarm).EndInit();
            _splitAlarm.ResumeLayout(false);
            _panelAlarmRules.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridAlarmRules).EndInit();
            _panelAlarmRuleBtns.ResumeLayout(false);
            _panelAlarmList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridAlarms).EndInit();
            _panelAlarmBtns.ResumeLayout(false);
            _tabRecipe.ResumeLayout(false);
            _splitRecipe.Panel1.ResumeLayout(false);
            _splitRecipe.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_splitRecipe).EndInit();
            _splitRecipe.ResumeLayout(false);
            _panelRecipeList.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridRecipes).EndInit();
            _panelRecipeBtns.ResumeLayout(false);
            _panelRecipeEdit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridRecipeParams).EndInit();
            _panelRecipeEditBtns.ResumeLayout(false);
            _tabReport.ResumeLayout(false);
            _panelReport.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)_gridReport).EndInit();
            _panelStats.ResumeLayout(false);
            _panelReportFilter.ResumeLayout(false);
            _panelReportFilter.PerformLayout();
            _panelReportBtns.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // ==================== 控件字段声明 ====================

        // 状态栏
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _lbConnectionStatus;
        private ToolStripStatusLabel _lbAlarmCount;
        private ToolStripStatusLabel _lbTime;

        // Tab 控件
        private TabControl _tabControl;

        // Tab 1: 通讯监控 — Modbus 控件（全部在 _panelMonitor 内）
        private TabPage _tabMonitor;
        private Panel _panelMonitor;
        private Label _mb_lb_title;
        private Label _mb_lb_legend;
        private Label _mb_lb_mode;
        private ComboBox _mb_drop_mode;
        private Label _mb_lb_ip;
        private TextBox _mb_box_ip;
        private Label _mb_lb_port;
        private TextBox _mb_box_port;
        private Label _mb_lb_com;
        private ComboBox _mb_drop_com;
        private Label _mb_lb_baud;
        private ComboBox _mb_drop_baud;
        private Label _mb_lb_stop;
        private ComboBox _mb_drop_stop;
        private Label _mb_lb_parity;
        private ComboBox _mb_drop_parity;
        private Button _mb_btn_refresh;
        private Button _mb_btn_open;
        private Label _mb_lb_status;
        private Label _mb_sep1;
        private Label _mb_lb_dev;
        private TextBox _mb_box_dev;
        private Label _mb_lb_func;
        private ComboBox _mb_drop_func;
        private Label _mb_lb_addr;
        private TextBox _mb_box_addr;
        private Label _mb_lb_count;
        private TextBox _mb_box_count;
        private Button _mb_btn_read;
        private Button _mb_btn_polling;
        private Label _mb_lb_polling_status;
        private Label _mb_lb_poll_interval_label;
        private NumericUpDown _mb_polling_interval;
        private Label _mb_lb_send_hex;
        private RichTextBox _mb_box_send_hex;
        private Label _mb_lb_recv_hex;
        private RichTextBox _mb_box_recv_hex;
        private Label _mb_lb_result;
        private DataGridView _mb_grid_result;
        private Button _mb_btn_clear;
        private Label _mb_lb_log;
        private TextBox _mb_box_recv;
        private Button _mb_btn_learn;

        // Tab 2: 实时曲线
        private TabPage _tabChart;
        private SplitContainer _splitChart;
        private Panel _panelChartConfig;
        private Label _lbChannelConfig;
        private ListBox _listChannels;
        private FlowLayoutPanel _panelChartBtns;
        private Button _btnChartStart;
        private Button _btnChartPause;
        private Button _btnChartClear;
        private Button _btnChartExport;
        private Panel _panelChartArea;  // RealtimeChartControl 在运行时动态创建并放入此面板

        // Tab 3: 报警系统
        private TabPage _tabAlarm;
        private SplitContainer _splitAlarm;
        private Panel _panelAlarmRules;
        private Label _lbAlarmRules;
        private DataGridView _gridAlarmRules;
        private DataGridViewTextBoxColumn _colRuleName;
        private DataGridViewTextBoxColumn _colRuleDevice;
        private DataGridViewTextBoxColumn _colRuleAddress;
        private DataGridViewTextBoxColumn _colRuleCondition;
        private DataGridViewTextBoxColumn _colRuleLevel;
        private DataGridViewCheckBoxColumn _colRuleEnabled;
        private FlowLayoutPanel _panelAlarmRuleBtns;
        private Button _btnAddRule;
        private Button _btnEditRule;
        private Button _btnDeleteRule;
        private Panel _panelAlarmList;
        private Label _lbAlarmList;
        private DataGridView _gridAlarms;
        private DataGridViewTextBoxColumn _colAlarmTime;
        private DataGridViewTextBoxColumn _colAlarmLevel;
        private DataGridViewTextBoxColumn _colAlarmDevice;
        private DataGridViewTextBoxColumn _colAlarmDesc;
        private DataGridViewTextBoxColumn _colAlarmStatus;
        private FlowLayoutPanel _panelAlarmBtns;
        private Button _btnConfirmAlarm;
        private Button _btnResetAlarm;
        private Button _btnExportAlarm;

        // Tab 4: 配方管理
        private TabPage _tabRecipe;
        private SplitContainer _splitRecipe;
        private Panel _panelRecipeList;
        private Label _lbRecipeList;
        private DataGridView _gridRecipes;
        private DataGridViewTextBoxColumn _colRecipeName;
        private DataGridViewTextBoxColumn _colRecipeTime;
        private DataGridViewTextBoxColumn _colRecipeParams;
        private DataGridViewTextBoxColumn _colRecipeVersion;
        private FlowLayoutPanel _panelRecipeBtns;
        private Button _btnNewRecipe;
        private Button _btnCopyRecipe;
        private Button _btnDeleteRecipe;
        private Panel _panelRecipeEdit;
        private Label _lbRecipeEdit;
        private DataGridView _gridRecipeParams;
        private DataGridViewTextBoxColumn _colParamIndex;
        private DataGridViewTextBoxColumn _colParamName;
        private DataGridViewTextBoxColumn _colParamAddress;
        private DataGridViewTextBoxColumn _colParamCurrent;
        private DataGridViewTextBoxColumn _colParamNew;
        private DataGridViewTextBoxColumn _colParamUnit;
        private FlowLayoutPanel _panelRecipeEditBtns;
        private Button _btnReadPlc;
        private Button _btnDownloadPlc;
        private Button _btnSaveRecipe;

        // Tab 5: 生产报表
        private TabPage _tabReport;
        private Panel _panelReport;
        private FlowLayoutPanel _panelReportFilter;
        private Label _lbReportType;
        private ComboBox _dropReportType;
        private Label _lbReportDate;
        private DateTimePicker _dateReport;
        private Button _btnGenerateReport;
        private FlowLayoutPanel _panelStats;
        private Panel _cardTotal;
        private Panel _cardQualify;
        private Panel _cardCycle;
        private Panel _cardAlarmCount;
        private DataGridView _gridReport;
        private DataGridViewTextBoxColumn _colRptTime;
        private DataGridViewTextBoxColumn _colRptCount;
        private DataGridViewTextBoxColumn _colRptGood;
        private DataGridViewTextBoxColumn _colRptDefect;
        private DataGridViewTextBoxColumn _colRptCycle;
        private DataGridViewTextBoxColumn _colRptStatus;
        private FlowLayoutPanel _panelReportBtns;
        private Button _btnExportExcel;
        private Button _btnPrint;
    }
}
