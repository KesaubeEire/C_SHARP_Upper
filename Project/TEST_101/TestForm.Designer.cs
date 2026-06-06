namespace TEST_101
{
    partial class 串口通信调试工具
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            lb_title_status = new Label();
            lb_TEMP = new Label();
            txtTemp = new TextBox();
            txtSpeed = new TextBox();
            label1 = new Label();
            txtPressure = new TextBox();
            label2 = new Label();
            button3 = new Button();
            button4 = new Button();
            listBox1 = new ListBox();
            timer1 = new System.Windows.Forms.Timer(components);
            sp_title = new Label();
            lb_com = new Label();
            lb_baud = new Label();
            drop_com = new ComboBox();
            drop_baud = new ComboBox();
            btn_open = new Button();
            lb_send = new Label();
            box_send = new TextBox();
            btn_send = new Button();
            chk_hex = new CheckBox();
            lb_recv = new Label();
            box_recv = new TextBox();
            btn_clear = new Button();
            statusStrip1 = new StatusStrip();
            lb_status = new Label();
            btn_refresh = new Button();
            btn_go_modbus = new Button();
            SuspendLayout();
            // 
            // lb_title_status
            // 
            lb_title_status.AutoSize = true;
            lb_title_status.Location = new Point(12, 38);
            lb_title_status.Name = "lb_title_status";
            lb_title_status.Size = new Size(56, 17);
            lb_title_status.TabIndex = 2;
            lb_title_status.Text = "设备状态";
            // 
            // lb_TEMP
            // 
            lb_TEMP.AutoSize = true;
            lb_TEMP.Location = new Point(12, 73);
            lb_TEMP.Name = "lb_TEMP";
            lb_TEMP.Size = new Size(32, 17);
            lb_TEMP.TabIndex = 3;
            lb_TEMP.Text = "温度";
            // 
            // txtTemp
            // 
            txtTemp.Location = new Point(73, 70);
            txtTemp.Name = "txtTemp";
            txtTemp.Size = new Size(100, 23);
            txtTemp.TabIndex = 6;
            // 
            // txtSpeed
            // 
            txtSpeed.Location = new Point(73, 140);
            txtSpeed.Name = "txtSpeed";
            txtSpeed.Size = new Size(100, 23);
            txtSpeed.TabIndex = 8;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 143);
            label1.Name = "label1";
            label1.Size = new Size(32, 17);
            label1.TabIndex = 7;
            label1.Text = "速度";
            // 
            // txtPressure
            // 
            txtPressure.Location = new Point(73, 105);
            txtPressure.Name = "txtPressure";
            txtPressure.Size = new Size(100, 23);
            txtPressure.TabIndex = 10;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 108);
            label2.Name = "label2";
            label2.Size = new Size(32, 17);
            label2.TabIndex = 9;
            label2.Text = "压力";
            // 
            // button3
            // 
            button3.Font = new Font("Microsoft YaHei UI", 10F);
            button3.Location = new Point(12, 178);
            button3.Name = "button3";
            button3.Size = new Size(76, 36);
            button3.TabIndex = 11;
            button3.Text = "开始监视";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Font = new Font("Microsoft YaHei UI", 10F);
            button4.Location = new Point(97, 178);
            button4.Name = "button4";
            button4.Size = new Size(76, 36);
            button4.TabIndex = 12;
            button4.Text = "停止监视";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.Location = new Point(12, 232);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(245, 106);
            listBox1.TabIndex = 13;
            // 
            // timer1
            // 
            timer1.Interval = 1000;
            timer1.Tick += timer1_Tick;
            // 
            // sp_title
            // 
            sp_title.AutoSize = true;
            sp_title.Location = new Point(264, 35);
            sp_title.Name = "sp_title";
            sp_title.Size = new Size(56, 17);
            sp_title.TabIndex = 14;
            sp_title.Text = "串口通信";
            // 
            // lb_com
            // 
            lb_com.AutoSize = true;
            lb_com.Location = new Point(264, 78);
            lb_com.Name = "lb_com";
            lb_com.Size = new Size(50, 17);
            lb_com.TabIndex = 15;
            lb_com.Text = "COM口";
            // 
            // lb_baud
            // 
            lb_baud.AutoSize = true;
            lb_baud.Location = new Point(465, 78);
            lb_baud.Name = "lb_baud";
            lb_baud.Size = new Size(44, 17);
            lb_baud.TabIndex = 16;
            lb_baud.Text = "波特率";
            // 
            // drop_com
            // 
            drop_com.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_com.FormattingEnabled = true;
            drop_com.Items.AddRange(new object[] { "-COM1", "-COM2", "-COM3" });
            drop_com.Location = new Point(329, 74);
            drop_com.Name = "drop_com";
            drop_com.Size = new Size(121, 25);
            drop_com.TabIndex = 17;
            // 
            // drop_baud
            // 
            drop_baud.DropDownStyle = ComboBoxStyle.DropDownList;
            drop_baud.FormattingEnabled = true;
            drop_baud.IntegralHeight = false;
            drop_baud.Items.AddRange(new object[] { "4800", "9600", "19200", "38400", "115200" });
            drop_baud.Location = new Point(536, 74);
            drop_baud.Name = "drop_baud";
            drop_baud.Size = new Size(121, 25);
            drop_baud.TabIndex = 18;
            // 
            // btn_open
            // 
            btn_open.Font = new Font("Microsoft YaHei UI", 10F);
            btn_open.Location = new Point(329, 24);
            btn_open.Name = "btn_open";
            btn_open.Size = new Size(76, 36);
            btn_open.TabIndex = 19;
            btn_open.Text = "打开串口";
            btn_open.UseVisualStyleBackColor = true;
            // 
            // lb_send
            // 
            lb_send.AutoSize = true;
            lb_send.Location = new Point(264, 121);
            lb_send.Name = "lb_send";
            lb_send.Size = new Size(32, 17);
            lb_send.TabIndex = 20;
            lb_send.Text = "发送";
            // 
            // box_send
            // 
            box_send.Location = new Point(329, 118);
            box_send.Name = "box_send";
            box_send.Size = new Size(270, 23);
            box_send.TabIndex = 21;
            // 
            // btn_send
            // 
            btn_send.Font = new Font("Microsoft YaHei UI", 10F);
            btn_send.Location = new Point(617, 113);
            btn_send.Name = "btn_send";
            btn_send.Size = new Size(55, 30);
            btn_send.TabIndex = 22;
            btn_send.Text = "发送";
            btn_send.UseVisualStyleBackColor = true;
            // 
            // chk_hex
            // 
            chk_hex.AutoSize = true;
            chk_hex.Location = new Point(683, 120);
            chk_hex.Name = "chk_hex";
            chk_hex.Size = new Size(79, 21);
            chk_hex.TabIndex = 23;
            chk_hex.Text = "HEX 发送";
            chk_hex.UseVisualStyleBackColor = true;
            // 
            // lb_recv
            // 
            lb_recv.AutoSize = true;
            lb_recv.Location = new Point(264, 164);
            lb_recv.Name = "lb_recv";
            lb_recv.Size = new Size(32, 17);
            lb_recv.TabIndex = 24;
            lb_recv.Text = "接收";
            // 
            // box_recv
            // 
            box_recv.Location = new Point(329, 161);
            box_recv.Multiline = true;
            box_recv.Name = "box_recv";
            box_recv.Size = new Size(270, 103);
            box_recv.TabIndex = 25;
            // 
            // btn_clear
            // 
            btn_clear.Font = new Font("Microsoft YaHei UI", 10F);
            btn_clear.Location = new Point(617, 156);
            btn_clear.Name = "btn_clear";
            btn_clear.Size = new Size(55, 30);
            btn_clear.TabIndex = 26;
            btn_clear.Text = "清空";
            btn_clear.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            statusStrip1.Location = new Point(0, 419);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(784, 22);
            statusStrip1.TabIndex = 27;
            statusStrip1.Text = "statusStrip1";
            // 
            // lb_status
            // 
            lb_status.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lb_status.Location = new Point(597, 384);
            lb_status.Name = "lb_status";
            lb_status.Size = new Size(162, 20);
            lb_status.TabIndex = 28;
            lb_status.Text = "接收";
            lb_status.TextAlign = ContentAlignment.MiddleRight;
            lb_status.UseCompatibleTextRendering = true;
            // 
            // btn_refresh
            // 
            btn_refresh.Font = new Font("Microsoft YaHei UI", 10F);
            btn_refresh.Location = new Point(428, 24);
            btn_refresh.Name = "btn_refresh";
            btn_refresh.Size = new Size(93, 36);
            btn_refresh.TabIndex = 29;
            btn_refresh.Text = "刷新 COM";
            btn_refresh.UseVisualStyleBackColor = true;
            btn_refresh.Click += btn_refresh_Click;
            // 
            // btn_go_modbus
            // 
            btn_go_modbus.Font = new Font("Microsoft YaHei UI", 10F);
            btn_go_modbus.Location = new Point(656, 12);
            btn_go_modbus.Name = "btn_go_modbus";
            btn_go_modbus.Size = new Size(116, 36);
            btn_go_modbus.TabIndex = 30;
            btn_go_modbus.Text = "Modbus 调试";
            btn_go_modbus.UseVisualStyleBackColor = true;
            btn_go_modbus.Click += btn_go_modbus_Click;
            // 
            // 串口通信调试工具
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 441);
            Controls.Add(btn_go_modbus);
            Controls.Add(btn_refresh);
            Controls.Add(lb_status);
            Controls.Add(statusStrip1);
            Controls.Add(btn_clear);
            Controls.Add(box_recv);
            Controls.Add(lb_recv);
            Controls.Add(chk_hex);
            Controls.Add(btn_send);
            Controls.Add(box_send);
            Controls.Add(lb_send);
            Controls.Add(btn_open);
            Controls.Add(drop_baud);
            Controls.Add(drop_com);
            Controls.Add(lb_baud);
            Controls.Add(lb_com);
            Controls.Add(sp_title);
            Controls.Add(listBox1);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(txtPressure);
            Controls.Add(label2);
            Controls.Add(txtSpeed);
            Controls.Add(label1);
            Controls.Add(txtTemp);
            Controls.Add(lb_TEMP);
            Controls.Add(lb_title_status);
            Name = "串口通信调试工具";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "串口通信调试工具";
            FormClosing += 串口通信调试工具_FormClosing;
            Load += 串口通信调试工具_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label lb_title_status;
        private Label lb_TEMP;
        private TextBox txtTemp;
        private TextBox txtSpeed;
        private Label label1;
        private TextBox txtPressure;
        private Label label2;
        private Button button3;
        private Button button4;
        private ListBox listBox1;
        private System.Windows.Forms.Timer timer1;
        private Label sp_title;
        private Label lb_com;
        private Label lb_baud;
        private ComboBox drop_com;
        private ComboBox drop_baud;
        private Button btn_open;
        private Label lb_send;
        private TextBox box_send;
        private Button btn_send;
        private CheckBox chk_hex;
        private Label lb_recv;
        private TextBox box_recv;
        private Button btn_clear;
        private StatusStrip statusStrip1;
        private Label lb_status;
        private Button btn_refresh;
        private Button btn_go_modbus;
    }
}
