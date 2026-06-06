using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace TEST_101
{
    public partial class 串口通信调试工具 : Form
    {
        SerialPort sp = new SerialPort();

        public 串口通信调试工具()
        {
            InitializeComponent();
        }

        #region ======== 旧代码：模拟监视器 ========
        private void button3_Click(object sender, EventArgs e)
        {
            lb_title_status.Text = "设备状态：● 已连接";
            lb_title_status.ForeColor = Color.Green;
            timer1.Start();  // 启动定时器
        }

        private void button4_Click(object sender, EventArgs e)
        {
            lb_title_status.Text = "设备状态：○ 已断开";
            lb_title_status.ForeColor = Color.Red;
            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Random rnd = new Random();
            txtTemp.Text = (20 + rnd.NextDouble() * 10).ToString("F1");
            txtPressure.Text = (1.0 + rnd.NextDouble() * 0.5).ToString("F2");
            txtSpeed.Text = (1400 + rnd.Next(0, 200)).ToString();

            // 记录日志
            string log = DateTime.Now.ToString("HH:mm:ss") + $"  温度:{txtTemp.Text}  压力:{txtPressure.Text}  转速:{txtSpeed.Text}";
            listBox1.Items.Insert(0, log);

            // 限制日志数量，保持界面流畅
            if (listBox1.Items.Count > 100)
            {
                listBox1.Items.RemoveAt(listBox1.Items.Count - 1);
            }
            listBox1.Items.Insert(0, log);
        }
        #endregion

        // ======== 新代码：串口通信 ========

        // ====== 刷新 COM 口列表的函数 ======
        private void RefreshComPorts()
        {
            string currentCom = drop_com.Text ?? string.Empty;  // 防止 Text 为 null

            drop_com.Items.Clear();
            drop_com.Text = "";

            string[] ports = SerialPort.GetPortNames().Distinct().ToArray();

            if (ports.Length == 0)
            {
                lb_status.Text = "未检测到可用 COM 口";
                return;
            }

            foreach (string port in ports)
            {
                drop_com.Items.Add(port);
            }

            // 如果之前选中的 COM 口还在，就继续选中它；否则选第一个
            if (drop_com.Items.Contains(currentCom))
                drop_com.Text = currentCom;
            else
                drop_com.SelectedIndex = 0;

            lb_status.Text = $"检测到 {ports.Length} 个 COM 口";
        }

        // ====== 窗体加载时自动刷新 ======
        private void 串口通信调试工具_Load(object sender, EventArgs e)
        {
            RefreshComPorts();
            drop_baud.Text = "9600";
        }

        // ====== 点刷新按钮 ======
        private void btn_refresh_Click(object sender, EventArgs e)
        {
            RefreshComPorts();
        }

        private void btn_open_Click(object sender, EventArgs e)
        {
            if (!sp.IsOpen)
            {
                if (string.IsNullOrWhiteSpace(drop_com.Text))
                {
                    MessageBox.Show("请先选择一个 COM 口");
                    return;
                }

                try
                {
                    sp.PortName = drop_com.Text;
                    sp.BaudRate = int.Parse(drop_baud.Text);
                    sp.DataBits = 8;
                    sp.StopBits = StopBits.One;
                    sp.Parity = Parity.None;
                    sp.DataReceived += Sp_DataReceived;

                    sp.Open();
                    btn_open.Text = "关闭串口";
                    lb_status.Text = $"已打开 {drop_com.Text}，{drop_baud.Text} 波特率";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开串口失败：" + ex.Message);
                }
            }
            else
            {
                sp.Close();
                btn_open.Text = "打开串口";
                lb_status.Text = "已断开";
            }
        }

        private void btn_send_Click(object sender, EventArgs e)
        {
            if (!sp.IsOpen)
            {
                MessageBox.Show("请先打开串口");
                return;
            }

            string sendData = (box_send.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(sendData)) return;

            try
            {
                if (chk_hex.Checked)
                {
                    string[] hexArray = sendData.Split(' ');
                    byte[] buffer = new byte[hexArray.Length];
                    for (int i = 0; i < hexArray.Length; i++)
                        buffer[i] = Convert.ToByte(hexArray[i], 16);
                    sp.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    sp.Write(sendData);
                }

                box_recv.AppendText($"发送 → {sendData}\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送失败：" + ex.Message);
            }
        }

        private void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string data = sp.ReadExisting() ?? string.Empty;
            this.Invoke(new Action(() =>
            {
                box_recv.AppendText($"{DateTime.Now:HH:mm:ss} → {data}\r\n");
            }));
        }

        private void btn_clear_Click(object sender, EventArgs e)
        {
            box_recv.Clear();
        }

        private void 串口通信调试工具_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (sp.IsOpen) sp.Close();

            // 重新显示主窗体
            ModbusForm? mainForm = Application.OpenForms["ModbusForm"] as ModbusForm;
            if (mainForm is not null)
                mainForm.Show();
        }

        private void btn_go_modbus_Click(object sender, EventArgs e)
        {
            this.Close();  // 关闭当前窗体
        }
    }
}