namespace TEST_101
{
    partial class ModbusForm
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
            lb_title = new Label();
            btn_back = new Button();
            lb_com = new Label();
            drop_com = new ComboBox();
            lb_baud = new Label();
            drop_baud = new ComboBox();
            lb_stop = new Label();
            drop_stop = new ComboBox();
            lb_parity = new Label();
            drop_parity = new ComboBox();
            btn_refresh = new Button();
            btn_open = new Button();
            lb_status = new Label();
            lb_dev = new Label();
            box_dev = new TextBox();
            lb_func = new Label();
            drop_func = new ComboBox();
            lb_addr = new Label();
            box_addr = new TextBox();
            lb_count = new Label();
            box_count = new TextBox();
            btn_read = new Button();
            lb_send_hex = new Label();
            box_send_hex = new RichTextBox();
            lb_recv_hex = new Label();
            box_recv_hex = new RichTextBox();
            lb_result = new Label();
            grid_result = new DataGridView();
            btn_clear = new Button();
            lb_legend = new Label();
            lb_log = new Label();
            box_recv = new TextBox();
            sep1 = new Label();
            lb_mode = new Label();
            drop_mode = new ComboBox();
            lb_ip = new Label();
            box_ip = new TextBox();
            lb_port = new Label();
            box_port = new TextBox();
            ((System.ComponentModel.ISupportInitialize)grid_result).BeginInit();
            SuspendLayout();
            // 
            // lb_title
            // 
            lb_title.AutoSize = true;
            lb_title.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            lb_title.Location = new Point(12, 9);
            lb_title.Name = "lb_title";
            lb_title.Size = new Size(173, 26);
            lb_title.TabIndex = 43;
            lb_title.Text = "Modbus 调试助手";
            // 
            // btn_back
            // 
            btn_back.Font = new Font("Microsoft YaHei UI", 10F);
            btn_back.Location = new Point(820, 10);
            btn_back.Name = "btn_back";
            btn_back.Size = new Size(115, 36);
            btn_back.TabIndex = 0;
            btn_back.Text = "串口工具";
            btn_back.UseVisualStyleBackColor = true;
            btn_back.Click += btn_back_Click;
            // 
            // lb_com
            // 
            lb_com.AutoSize = true;
            lb_com.Location = new Point(12, 74);
            lb_com.Name = "lb_com";
            lb_com.Size = new Size(50, 17);
            lb_com.TabIndex = 42;
            lb_com.Text = "COM口";
            // 
            // drop_com
            // 
            drop_com.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_com.FormattingEnabled = true;
            drop_com.Location = new Point(65, 70);
            drop_com.Name = "drop_com";
            drop_com.Size = new Size(100, 25);
            drop_com.TabIndex = 1;
            // 
            // lb_baud
            // 
            lb_baud.AutoSize = true;
            lb_baud.Location = new Point(178, 74);
            lb_baud.Name = "lb_baud";
            lb_baud.Size = new Size(44, 17);
            lb_baud.TabIndex = 41;
            lb_baud.Text = "波特率";
            // 
            // drop_baud
            // 
            drop_baud.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_baud.FormattingEnabled = true;
            drop_baud.Items.AddRange(new object[] { "4800", "9600", "19200", "38400", "115200" });
            drop_baud.Location = new Point(228, 70);
            drop_baud.Name = "drop_baud";
            drop_baud.Size = new Size(75, 25);
            drop_baud.TabIndex = 2;
            // 
            // lb_stop
            // 
            lb_stop.AutoSize = true;
            lb_stop.Location = new Point(310, 74);
            lb_stop.Name = "lb_stop";
            lb_stop.Size = new Size(44, 17);
            lb_stop.TabIndex = 44;
            lb_stop.Text = "停止位";
            // 
            // drop_stop
            // 
            drop_stop.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_stop.FormattingEnabled = true;
            drop_stop.Items.AddRange(new object[] { "1", "1.5", "2" });
            drop_stop.Location = new Point(358, 70);
            drop_stop.Name = "drop_stop";
            drop_stop.Size = new Size(55, 25);
            drop_stop.TabIndex = 3;
            // 
            // lb_parity
            // 
            lb_parity.AutoSize = true;
            lb_parity.Location = new Point(420, 74);
            lb_parity.Name = "lb_parity";
            lb_parity.Size = new Size(32, 17);
            lb_parity.TabIndex = 45;
            lb_parity.Text = "校验";
            // 
            // drop_parity
            // 
            drop_parity.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_parity.FormattingEnabled = true;
            drop_parity.Items.AddRange(new object[] { "无", "奇校验", "偶校验", "Mark", "Space" });
            drop_parity.Location = new Point(455, 70);
            drop_parity.Name = "drop_parity";
            drop_parity.Size = new Size(80, 25);
            drop_parity.TabIndex = 4;
            // 
            // btn_refresh
            // 
            btn_refresh.Font = new Font("Microsoft YaHei UI", 9F);
            btn_refresh.Location = new Point(548, 67);
            btn_refresh.Name = "btn_refresh";
            btn_refresh.Size = new Size(70, 30);
            btn_refresh.TabIndex = 5;
            btn_refresh.Text = "刷新COM";
            btn_refresh.UseVisualStyleBackColor = true;
            btn_refresh.Click += btn_refresh_Click;
            // 
            // btn_open
            // 
            btn_open.BackColor = Color.FromArgb(60, 140, 60);
            btn_open.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btn_open.ForeColor = Color.White;
            btn_open.Location = new Point(628, 67);
            btn_open.Name = "btn_open";
            btn_open.Size = new Size(85, 30);
            btn_open.TabIndex = 6;
            btn_open.Text = "打开串口";
            btn_open.UseVisualStyleBackColor = false;
            btn_open.Click += btn_open_Click;
            // 
            // lb_status
            // 
            lb_status.AutoSize = true;
            lb_status.Location = new Point(725, 74);
            lb_status.Name = "lb_status";
            lb_status.Size = new Size(44, 17);
            lb_status.TabIndex = 40;
            lb_status.Text = "已断开";
            // 
            // lb_dev
            // 
            lb_dev.AutoSize = true;
            lb_dev.Location = new Point(12, 129);
            lb_dev.Name = "lb_dev";
            lb_dev.Size = new Size(56, 17);
            lb_dev.TabIndex = 38;
            lb_dev.Text = "设备地址";
            // 
            // box_dev
            // 
            box_dev.Location = new Point(80, 126);
            box_dev.Name = "box_dev";
            box_dev.Size = new Size(50, 23);
            box_dev.TabIndex = 10;
            box_dev.Text = "1";
            box_dev.TextAlign = HorizontalAlignment.Center;
            // 
            // lb_func
            // 
            lb_func.AutoSize = true;
            lb_func.Location = new Point(145, 129);
            lb_func.Name = "lb_func";
            lb_func.Size = new Size(44, 17);
            lb_func.TabIndex = 37;
            lb_func.Text = "功能码";
            // 
            // drop_func
            // 
            drop_func.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_func.FormattingEnabled = true;
            drop_func.Location = new Point(195, 126);
            drop_func.Name = "drop_func";
            drop_func.Size = new Size(155, 25);
            drop_func.TabIndex = 11;
            // 
            // lb_addr
            // 
            lb_addr.AutoSize = true;
            lb_addr.Location = new Point(365, 129);
            lb_addr.Name = "lb_addr";
            lb_addr.Size = new Size(56, 17);
            lb_addr.TabIndex = 36;
            lb_addr.Text = "起始地址";
            // 
            // box_addr
            // 
            box_addr.Location = new Point(430, 126);
            box_addr.Name = "box_addr";
            box_addr.Size = new Size(60, 23);
            box_addr.TabIndex = 12;
            box_addr.Text = "0";
            box_addr.TextAlign = HorizontalAlignment.Center;
            // 
            // lb_count
            // 
            lb_count.AutoSize = true;
            lb_count.Location = new Point(505, 129);
            lb_count.Name = "lb_count";
            lb_count.Size = new Size(32, 17);
            lb_count.TabIndex = 35;
            lb_count.Text = "数量";
            // 
            // box_count
            // 
            box_count.Location = new Point(543, 126);
            box_count.Name = "box_count";
            box_count.Size = new Size(55, 23);
            box_count.TabIndex = 13;
            box_count.Text = "10";
            box_count.TextAlign = HorizontalAlignment.Center;
            // 
            // btn_read
            // 
            btn_read.BackColor = Color.FromArgb(0, 120, 215);
            btn_read.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            btn_read.ForeColor = Color.White;
            btn_read.Location = new Point(615, 122);
            btn_read.Name = "btn_read";
            btn_read.Size = new Size(100, 32);
            btn_read.TabIndex = 14;
            btn_read.Text = "读取";
            btn_read.UseVisualStyleBackColor = false;
            btn_read.Click += btn_read_Click;
            // 
            // lb_send_hex
            // 
            lb_send_hex.AutoSize = true;
            lb_send_hex.Location = new Point(12, 169);
            lb_send_hex.Name = "lb_send_hex";
            lb_send_hex.Size = new Size(56, 17);
            lb_send_hex.TabIndex = 34;
            lb_send_hex.Text = "发送报文";
            // 
            // box_send_hex
            // 
            box_send_hex.BackColor = Color.FromArgb(245, 245, 245);
            box_send_hex.Font = new Font("Consolas", 10F);
            box_send_hex.Location = new Point(80, 166);
            box_send_hex.Name = "box_send_hex";
            box_send_hex.ReadOnly = true;
            box_send_hex.Size = new Size(852, 23);
            box_send_hex.TabIndex = 20;
            box_send_hex.Text = "";
            // 
            // lb_recv_hex
            // 
            lb_recv_hex.AutoSize = true;
            lb_recv_hex.Location = new Point(12, 204);
            lb_recv_hex.Name = "lb_recv_hex";
            lb_recv_hex.Size = new Size(56, 17);
            lb_recv_hex.TabIndex = 33;
            lb_recv_hex.Text = "接收报文";
            // 
            // box_recv_hex
            // 
            box_recv_hex.BackColor = Color.FromArgb(245, 245, 245);
            box_recv_hex.Font = new Font("Consolas", 10F);
            box_recv_hex.Location = new Point(80, 201);
            box_recv_hex.Name = "box_recv_hex";
            box_recv_hex.ReadOnly = true;
            box_recv_hex.Size = new Size(852, 23);
            box_recv_hex.TabIndex = 21;
            box_recv_hex.Text = "";
            // 
            // lb_result
            // 
            lb_result.AutoSize = true;
            lb_result.Location = new Point(12, 239);
            lb_result.Name = "lb_result";
            lb_result.Size = new Size(56, 17);
            lb_result.TabIndex = 32;
            lb_result.Text = "数据解析";
            // 
            // grid_result
            // 
            grid_result.AllowUserToAddRows = false;
            grid_result.AllowUserToDeleteRows = false;
            grid_result.AllowUserToResizeRows = false;
            grid_result.BackgroundColor = Color.FromArgb(250, 250, 250);
            grid_result.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
            grid_result.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            grid_result.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Consolas", 10F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            grid_result.DefaultCellStyle = dataGridViewCellStyle2;
            grid_result.Location = new Point(80, 236);
            grid_result.Name = "grid_result";
            grid_result.ReadOnly = true;
            grid_result.RowHeadersVisible = false;
            grid_result.Size = new Size(852, 195);
            grid_result.TabIndex = 22;
            // 
            // btn_clear
            // 
            btn_clear.Location = new Point(12, 266);
            btn_clear.Name = "btn_clear";
            btn_clear.Size = new Size(50, 30);
            btn_clear.TabIndex = 23;
            btn_clear.Text = "清空";
            btn_clear.UseVisualStyleBackColor = true;
            btn_clear.Click += btn_clear_Click;
            // 
            // lb_legend
            // 
            lb_legend.AutoSize = true;
            lb_legend.Location = new Point(12, 36);
            lb_legend.Name = "lb_legend";
            lb_legend.Size = new Size(472, 17);
            lb_legend.TabIndex = 46;
            lb_legend.Text = "RTU图例：地址=灰  功能码(01=蓝 02=绿 03=紫 04=黄 错误=红)  数据=黄  CRC=橙";
            // 
            // lb_log
            // 
            lb_log.AutoSize = true;
            lb_log.Location = new Point(12, 446);
            lb_log.Name = "lb_log";
            lb_log.Size = new Size(56, 17);
            lb_log.TabIndex = 31;
            lb_log.Text = "通信日志";
            // 
            // box_recv
            // 
            box_recv.BackColor = Color.FromArgb(30, 30, 30);
            box_recv.Font = new Font("Consolas", 9F);
            box_recv.ForeColor = Color.FromArgb(0, 255, 0);
            box_recv.Location = new Point(80, 443);
            box_recv.Multiline = true;
            box_recv.Name = "box_recv";
            box_recv.ReadOnly = true;
            box_recv.ScrollBars = ScrollBars.Both;
            box_recv.Size = new Size(852, 145);
            box_recv.TabIndex = 30;
            box_recv.WordWrap = false;
            // 
            // sep1
            // 
            sep1.BorderStyle = BorderStyle.Fixed3D;
            sep1.Location = new Point(12, 109);
            sep1.Name = "sep1";
            sep1.Size = new Size(920, 2);
            sep1.TabIndex = 39;
            // 
            // lb_mode
            // 
            lb_mode.AutoSize = true;
            lb_mode.Location = new Point(588, 14);
            lb_mode.Name = "lb_mode";
            lb_mode.Size = new Size(32, 17);
            lb_mode.TabIndex = 47;
            lb_mode.Text = "模式";
            // 
            // drop_mode
            // 
            drop_mode.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_mode.FormattingEnabled = true;
            drop_mode.Items.AddRange(new object[] { "RTU", "TCP" });
            drop_mode.Location = new Point(620, 10);
            drop_mode.Name = "drop_mode";
            drop_mode.Size = new Size(60, 25);
            drop_mode.TabIndex = 7;
            drop_mode.SelectedIndexChanged += drop_mode_SelectedIndexChanged;
            // 
            // lb_ip
            // 
            lb_ip.AutoSize = true;
            lb_ip.Font = new Font("Microsoft YaHei UI", 9F);
            lb_ip.Location = new Point(12, 74);
            lb_ip.Name = "lb_ip";
            lb_ip.Size = new Size(43, 17);
            lb_ip.TabIndex = 48;
            lb_ip.Text = "IP地址";
            lb_ip.Visible = false;
            // 
            // box_ip
            // 
            box_ip.Font = new Font("Consolas", 9F);
            box_ip.Location = new Point(60, 71);
            box_ip.Name = "box_ip";
            box_ip.Size = new Size(130, 22);
            box_ip.TabIndex = 8;
            box_ip.Text = "192.168.0.1";
            box_ip.Visible = false;
            // 
            // lb_port
            // 
            lb_port.AutoSize = true;
            lb_port.Font = new Font("Microsoft YaHei UI", 9F);
            lb_port.Location = new Point(205, 74);
            lb_port.Name = "lb_port";
            lb_port.Size = new Size(32, 17);
            lb_port.TabIndex = 49;
            lb_port.Text = "端口";
            lb_port.Visible = false;
            // 
            // box_port
            // 
            box_port.Font = new Font("Consolas", 9F);
            box_port.Location = new Point(235, 71);
            box_port.Name = "box_port";
            box_port.Size = new Size(55, 22);
            box_port.TabIndex = 9;
            box_port.Text = "502";
            box_port.Visible = false;
            // 
            // ModbusForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(960, 598);
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Controls.Add(box_recv);
            Controls.Add(lb_log);
            Controls.Add(lb_legend);
            Controls.Add(drop_mode);
            Controls.Add(lb_mode);
            Controls.Add(btn_clear);
            Controls.Add(grid_result);
            Controls.Add(lb_result);
            Controls.Add(box_recv_hex);
            Controls.Add(lb_recv_hex);
            Controls.Add(box_send_hex);
            Controls.Add(lb_send_hex);
            Controls.Add(btn_read);
            Controls.Add(box_count);
            Controls.Add(lb_count);
            Controls.Add(box_addr);
            Controls.Add(lb_addr);
            Controls.Add(drop_func);
            Controls.Add(lb_func);
            Controls.Add(box_dev);
            Controls.Add(lb_dev);
            Controls.Add(sep1);
            Controls.Add(lb_status);
            Controls.Add(btn_open);
            Controls.Add(btn_refresh);
            Controls.Add(box_port);
            Controls.Add(lb_port);
            Controls.Add(box_ip);
            Controls.Add(lb_ip);
            Controls.Add(drop_parity);
            Controls.Add(lb_parity);
            Controls.Add(drop_stop);
            Controls.Add(lb_stop);
            Controls.Add(drop_baud);
            Controls.Add(lb_baud);
            Controls.Add(drop_com);
            Controls.Add(lb_com);
            Controls.Add(btn_back);
            Controls.Add(lb_title);
            Name = "ModbusForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Modbus 调试助手";
            FormClosing += ModbusForm_FormClosing;
            Load += ModbusForm_Load;
            ((System.ComponentModel.ISupportInitialize)grid_result).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        // ==================== 控件字段 ====================
        private Label lb_title;
        private Button btn_back;

        private Label lb_com;
        private ComboBox drop_com;
        private Label lb_baud;
        private ComboBox drop_baud;
        private Label lb_stop;
        private ComboBox drop_stop;
        private Label lb_parity;
        private ComboBox drop_parity;
        private Button btn_refresh;
        private Button btn_open;
        private Label lb_status;

        private Label lb_dev;
        private TextBox box_dev;
        private Label lb_func;
        private ComboBox drop_func;
        private Label lb_addr;
        private TextBox box_addr;
        private Label lb_count;
        private TextBox box_count;
        private Button btn_read;

        private Label lb_send_hex;
        private RichTextBox box_send_hex;
        private Label lb_recv_hex;
        private RichTextBox box_recv_hex;
        private Label lb_result;
        private DataGridView grid_result;

        private Button btn_clear;
        private Label lb_log;
        private Label lb_legend;
        private TextBox box_recv;
        private Label sep1;

        private Label lb_mode;
        private ComboBox drop_mode;
        private Label lb_ip;
        private TextBox box_ip;
        private Label lb_port;
        private TextBox box_port;
    }
}
