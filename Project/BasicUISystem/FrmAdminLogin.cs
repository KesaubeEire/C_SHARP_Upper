using System.Drawing.Drawing2D;

namespace BasicUISystem
{
    public partial class FrmAdminLogin : Form
    {
        private bool _dragging = false;
        private Point _startPoint = Point.Empty;
        private static readonly string CredFile = Path.Combine(
            Application.LocalUserAppDataPath, "login_cred.json");

        public FrmAdminLogin()
        {
            InitializeComponent();
            LoadRememberedPassword();
            SetRoundedRegion();
            pnlTitle.Paint += pnlTitle_Paint;
        }

        // =====  圆角 + 标题栏渐变  =====

        private void SetRoundedRegion()
        {
            using var path = new GraphicsPath();
            int d = 12 * 2;
            var r = ClientRectangle;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }

        private void pnlTitle_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var r = pnlTitle.ClientRectangle;
            using var brush = new LinearGradientBrush(
                r,
                Color.FromArgb(70, 130, 180),
                Color.FromArgb(100, 80, 160),
                LinearGradientMode.Horizontal);
            g.FillRectangle(brush, r);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            SetRoundedRegion();
        }

        // =====  记住密码  =====

        private void LoadRememberedPassword()
        {
            try
            {
                if (File.Exists(CredFile))
                {
                    var cred = System.Text.Json.JsonSerializer.Deserialize<Credential>(File.ReadAllText(CredFile));
                    if (cred != null)
                    {
                        txtAccount.Text = cred.Account;
                        txtPassword.Text = cred.Password;
                        chkRemember.Checked = true;
                    }
                }
            }
            catch { }
        }

        private void SaveCredential(string account, string password)
        {
            var dir = Path.GetDirectoryName(CredFile);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(CredFile, System.Text.Json.JsonSerializer.Serialize(
                new Credential { Account = account, Password = password }));
        }

        private void ClearCredential()
        {
            if (File.Exists(CredFile))
                File.Delete(CredFile);
        }

        private class Credential
        {
            public string Account { get; set; } = "";
            public string Password { get; set; } = "";
        }

        // =====  登录  =====

        private void btnLogin_Click(object? sender, EventArgs e)
        {
            string account = txtAccount.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                lblStatus.Text = "账号和密码不能为空";
                lblStatus.Visible = true;
                return;
            }

            if (account == "admin" && password == "123456")
            {
                if (chkRemember.Checked)
                    SaveCredential(account, password);
                else
                    ClearCredential();

                lblStatus.ForeColor = Color.LimeGreen;
                lblStatus.Text = "登录成功！";
                lblStatus.Visible = true;
            }
            else
            {
                lblStatus.ForeColor = Color.OrangeRed;
                lblStatus.Text = "账号或密码错误";
                lblStatus.Visible = true;
            }
        }

        private void btnLogin_MouseEnter(object? sender, EventArgs e)
        {
            btnLogin.BackColor = Color.FromArgb(90, 150, 200);
        }

        private void btnLogin_MouseLeave(object? sender, EventArgs e)
        {
            btnLogin.BackColor = Color.FromArgb(70, 130, 180);
        }

        // =====  标题栏拖拽  =====

        private void pnlTitle_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _startPoint = e.Location;
            }
        }

        private void pnlTitle_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                var p = PointToScreen(e.Location);
                Location = new Point(p.X - _startPoint.X, p.Y - _startPoint.Y);
            }
        }

        private void pnlTitle_MouseUp(object? sender, MouseEventArgs e)
        {
            _dragging = false;
        }

        private void btnClose_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
