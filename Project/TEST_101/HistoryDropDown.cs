using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TEST_101
{
    /// <summary>
    /// 历史记录下拉面板 — 弹出在文本框下方，支持逐条删除。
    /// </summary>
    public class HistoryDropDown : Form
    {
        private readonly InputHistoryManager _manager;
        private readonly TextBox _targetBox;
        private readonly string _fieldKey;
        private readonly FlowLayoutPanel _itemPanel;
        private readonly Button _btnClear;

        private const int ItemHeight = 28;
        private const int MaxVisibleItems = 8;

        public HistoryDropDown(InputHistoryManager manager, TextBox targetBox, string fieldKey)
        {
            _manager = manager;
            _targetBox = targetBox;
            _fieldKey = fieldKey;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(40, 40, 40);
            Padding = new Padding(2);

            _itemPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(2),
                Margin = new Padding(0)
            };

            _btnClear = new Button
            {
                Text = "清空此列表",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(150, 150, 150),
                BackColor = Color.FromArgb(50, 50, 50),
                Font = new Font("Microsoft YaHei", 9F),
                Height = 26,
                Dock = DockStyle.Bottom,
                Cursor = Cursors.Hand
            };
            _btnClear.FlatAppearance.BorderSize = 0;
            _btnClear.Click += (s, e) =>
            {
                _manager.ClearField(_fieldKey);
                Close();
            };

            Controls.Add(_itemPanel);
            Controls.Add(_btnClear);

            Deactivate += (s, e) => Close();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        public void ShowDropdown()
        {
            // 最小宽度 180，避免文字被截断
            int dropdownWidth = Math.Max(_targetBox.Width + 4, 180);
            Width = dropdownWidth;

            BuildItems();
            if (_itemPanel.Controls.Count == 0) return;

            // 计算尺寸
            int visibleItems = Math.Min(_itemPanel.Controls.Count, MaxVisibleItems);
            int panelHeight = visibleItems * ItemHeight + 4;
            Height = panelHeight + _btnClear.Height + 2;

            // 定位在文本框下方，确保不超出屏幕
            Point screenPos = _targetBox.PointToScreen(Point.Empty);
            var screen = Screen.FromPoint(screenPos);
            int y = screenPos.Y + _targetBox.Height;
            if (y + Height > screen.WorkingArea.Bottom)
                y = screenPos.Y - Height; // 空间不够则弹到上面

            Location = new Point(screenPos.X, y);

            Show();
            _targetBox.Focus(); // 保持焦点在文本框，Esc 可关
        }

        private void BuildItems()
        {
            _itemPanel.Controls.Clear();

            var items = _manager.GetHistory(_fieldKey);
            if (items.Count == 0) return;

            foreach (string value in items)
            {
                int contentWidth = Width - 4; // 减掉 Padding

                var row = new Panel
                {
                    Height = ItemHeight,
                    Width = contentWidth,
                    BackColor = Color.FromArgb(40, 40, 40),
                    Margin = new Padding(0)
                };

                // 文本标签（点击填入）
                var lbl = new Label
                {
                    Text = value,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(220, 220, 220),
                    BackColor = Color.Transparent,
                    Font = new Font("Consolas", 10F),
                    Location = new Point(6, 0),
                    Size = new Size(contentWidth - 32, ItemHeight),
                    Cursor = Cursors.Hand
                };
                lbl.Click += (s, e) =>
                {
                    _targetBox.Text = value;
                    Close();
                };
                lbl.MouseEnter += (s, e) => row.BackColor = Color.FromArgb(60, 60, 60);
                lbl.MouseLeave += (s, e) => row.BackColor = Color.FromArgb(40, 40, 40);

                // 删除按钮 [×]
                var btnDel = new Button
                {
                    Text = "×",
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(180, 180, 180),
                    BackColor = Color.Transparent,
                    Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold),
                    Size = new Size(22, 20),
                    Location = new Point(row.Width - 28, 4),
                    Cursor = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                btnDel.FlatAppearance.BorderSize = 0;
                string capturedValue = value; // 闭包捕获
                btnDel.Click += (s, e) =>
                {
                    _manager.Remove(_fieldKey, capturedValue);
                    BuildItems(); // 重建面板
                    if (_itemPanel.Controls.Count == 0) Close();
                };
                btnDel.MouseEnter += (s, e) => btnDel.ForeColor = Color.FromArgb(255, 100, 100);
                btnDel.MouseLeave += (s, e) => btnDel.ForeColor = Color.FromArgb(180, 180, 180);

                row.Controls.Add(lbl);
                row.Controls.Add(btnDel);
                _itemPanel.Controls.Add(row);
            }
        }

        protected override bool ShowWithoutActivation => true;
    }
}
