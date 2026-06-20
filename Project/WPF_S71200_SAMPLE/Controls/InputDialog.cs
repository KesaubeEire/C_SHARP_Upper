using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TestWpf.Controls;

public partial class InputDialog : Window
{
    public string InputText { get; private set; } = "";

    public InputDialog(string prompt, string defaultValue = "")
    {
        Width = 360; Height = 160;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        FontFamily = new FontFamily("Consolas");
        Title = "输入 DB 编号";

        var grid = new Grid { Margin = new Thickness(16, 16, 16, 16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var lbl = new TextBlock
        {
            Text = prompt,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
        };
        Grid.SetRow(lbl, 0);
        grid.Children.Add(lbl);

        var input = new TextBox
        {
            Text = defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x4A)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(input, 1);
        grid.Children.Add(input);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okBtn = new Button
        {
            Content = "确定",
            Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(16, 6, 16, 6),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        okBtn.Click += (_, _) => { InputText = input.Text; DialogResult = true; Close(); };

        var cancelBtn = new Button
        {
            Content = "取消",
            Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(16, 6, 16, 6),
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand
        };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        Grid.SetRow(btnPanel, 2);
        grid.Children.Add(btnPanel);

        Content = grid;
        input.Focus();
        Owner = Application.Current.MainWindow;
    }
}
