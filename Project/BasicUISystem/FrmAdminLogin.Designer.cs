namespace BasicUISystem
{
    partial class FrmAdminLogin
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pnlTitle = new Panel();
            lblTitle = new Label();
            btnClose = new Button();
            lblAccount = new Label();
            lblPassword = new Label();
            txtAccount = new TextBox();
            txtPassword = new TextBox();
            chkRemember = new CheckBox();
            btnLogin = new Button();
            lblStatus = new Label();
            pnlTitle.SuspendLayout();
            SuspendLayout();
            // 
            // pnlTitle
            // 
            pnlTitle.BackColor = Color.FromArgb(70, 130, 180);
            pnlTitle.Controls.Add(lblTitle);
            pnlTitle.Controls.Add(btnClose);
            pnlTitle.Dock = DockStyle.Top;
            pnlTitle.Font = new Font("浪漫雅圆", 11.9999981F);
            pnlTitle.Location = new Point(0, 0);
            pnlTitle.Name = "pnlTitle";
            pnlTitle.Size = new Size(393, 34);
            pnlTitle.TabIndex = 0;
            pnlTitle.MouseDown += pnlTitle_MouseDown;
            pnlTitle.MouseMove += pnlTitle_MouseMove;
            pnlTitle.MouseUp += pnlTitle_MouseUp;
            // 
            // lblTitle
            // 
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Font = new Font("浪漫雅圆", 11.9999981F);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(127, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(139, 34);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "课程管理系统";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.MouseDown += pnlTitle_MouseDown;
            lblTitle.MouseMove += pnlTitle_MouseMove;
            lblTitle.MouseUp += pnlTitle_MouseUp;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.BackColor = Color.Transparent;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 53, 69);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("浪漫雅圆", 11.9999981F);
            btnClose.ForeColor = Color.White;
            btnClose.Location = new Point(355, 0);
            btnClose.Margin = new Padding(0);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(38, 34);
            btnClose.TabIndex = 0;
            btnClose.Text = "×";
            btnClose.UseVisualStyleBackColor = false;
            btnClose.Click += btnClose_Click;
            // 
            // lblAccount
            // 
            lblAccount.AutoSize = true;
            lblAccount.Font = new Font("浪漫雅圆", 11.9999981F);
            lblAccount.ForeColor = Color.FromArgb(200, 200, 210);
            lblAccount.Location = new Point(45, 105);
            lblAccount.Name = "lblAccount";
            lblAccount.Size = new Size(90, 16);
            lblAccount.TabIndex = 5;
            lblAccount.Text = "管理员账号：";
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Font = new Font("浪漫雅圆", 11.9999981F);
            lblPassword.ForeColor = Color.FromArgb(200, 200, 210);
            lblPassword.Location = new Point(45, 145);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(90, 16);
            lblPassword.TabIndex = 4;
            lblPassword.Text = "管理员密码：";
            // 
            // txtAccount
            // 
            txtAccount.BackColor = Color.FromArgb(50, 50, 58);
            txtAccount.BorderStyle = BorderStyle.FixedSingle;
            txtAccount.Font = new Font("浪漫雅圆", 11.9999981F);
            txtAccount.ForeColor = Color.White;
            txtAccount.Location = new Point(156, 101);
            txtAccount.Name = "txtAccount";
            txtAccount.Size = new Size(185, 26);
            txtAccount.TabIndex = 0;
            // 
            // txtPassword
            // 
            txtPassword.BackColor = Color.FromArgb(50, 50, 58);
            txtPassword.BorderStyle = BorderStyle.FixedSingle;
            txtPassword.Font = new Font("浪漫雅圆", 11.9999981F);
            txtPassword.ForeColor = Color.White;
            txtPassword.Location = new Point(156, 141);
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '●';
            txtPassword.Size = new Size(185, 26);
            txtPassword.TabIndex = 1;
            // 
            // chkRemember
            // 
            chkRemember.AutoSize = true;
            chkRemember.Font = new Font("浪漫雅圆", 11.9999981F);
            chkRemember.ForeColor = Color.FromArgb(160, 160, 170);
            chkRemember.Location = new Point(45, 199);
            chkRemember.Name = "chkRemember";
            chkRemember.Size = new Size(90, 20);
            chkRemember.TabIndex = 2;
            chkRemember.Text = "记住密码";
            chkRemember.UseVisualStyleBackColor = true;
            // 
            // btnLogin
            // 
            btnLogin.BackColor = Color.FromArgb(70, 130, 180);
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 150, 200);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.Font = new Font("浪漫雅圆", 11.9999981F);
            btnLogin.ForeColor = Color.White;
            btnLogin.Location = new Point(191, 191);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(150, 34);
            btnLogin.TabIndex = 3;
            btnLogin.Text = "登 录 系 统";
            btnLogin.UseVisualStyleBackColor = false;
            btnLogin.Click += btnLogin_Click;
            btnLogin.MouseEnter += btnLogin_MouseEnter;
            btnLogin.MouseLeave += btnLogin_MouseLeave;
            // 
            // lblStatus
            // 
            lblStatus.Font = new Font("浪漫雅圆", 11.9999981F);
            lblStatus.ForeColor = Color.OrangeRed;
            lblStatus.Location = new Point(0, 60);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(393, 20);
            lblStatus.TabIndex = 0;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.Visible = false;
            // 
            // FrmAdminLogin
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(32, 32, 38);
            ClientSize = new Size(393, 270);
            Controls.Add(lblStatus);
            Controls.Add(btnLogin);
            Controls.Add(chkRemember);
            Controls.Add(txtPassword);
            Controls.Add(txtAccount);
            Controls.Add(lblPassword);
            Controls.Add(lblAccount);
            Controls.Add(pnlTitle);
            Font = new Font("微软雅黑", 9F);
            FormBorderStyle = FormBorderStyle.None;
            Name = "FrmAdminLogin";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "课程管理系统";
            pnlTitle.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        private Panel pnlTitle;
        private Label lblTitle;
        private Button btnClose;
        private Label lblAccount;
        private Label lblPassword;
        private TextBox txtAccount;
        private TextBox txtPassword;
        private CheckBox chkRemember;
        private Button btnLogin;
        private Label lblStatus;
    }
}
