using System.Windows;
using MaterialDesignThemes.Wpf;

namespace TestWpf.Services;

public enum AppThemeMode { Dark, Light }

public static class ThemeManager
{
    public static AppThemeMode Current { get; private set; } = AppThemeMode.Dark;
    public static event Action<AppThemeMode>? ThemeChanged;

    public static void Toggle()
    {
        Current = Current == AppThemeMode.Dark ? AppThemeMode.Light : AppThemeMode.Dark;
        Apply(Current);
    }

    public static void Apply(AppThemeMode mode)
    {
        try
        {
            // MaterialDesign v5 API
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(mode == AppThemeMode.Dark ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }
        catch
        {
            // 降级：手动切换我们的主题字典
        }

        // 同步自定义资源
        string uri = mode == AppThemeMode.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
        var appResources = Application.Current.Resources.MergedDictionaries;
        for (int i = 0; i < appResources.Count; i++)
        {
            if (appResources[i].Source != null &&
                (appResources[i].Source.OriginalString.Contains("Themes/Dark") ||
                 appResources[i].Source.OriginalString.Contains("Themes/Light")))
            {
                appResources[i] = dict;
                ThemeChanged?.Invoke(mode);
                return;
            }
        }
        appResources.Insert(0, dict);
        ThemeChanged?.Invoke(mode);
    }
}
