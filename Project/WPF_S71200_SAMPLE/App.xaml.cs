using System.Windows;
using TestWpf.Services;

namespace TestWpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 启动时初始化 MaterialDesign 主题（暗色）
        ThemeManager.Apply(AppThemeMode.Dark);
    }
}
