using System.Windows;

namespace TestWpf.Services;

public enum ThemeMode { Dark, Light }

public static class ThemeManager
{
    public static ThemeMode Current { get; private set; } = ThemeMode.Dark;
    public static event Action<ThemeMode>? ThemeChanged;

    public static void Toggle()
    {
        Current = Current == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        Apply(Current);
    }

    public static void Apply(ThemeMode mode)
    {
        string uri = mode == ThemeMode.Dark
            ? "Themes/Dark.xaml"
            : "Themes/Light.xaml";

        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

        // 替换 Application 级合并字典中的主题资源
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

        // 没找到已有主题资源，追加
        appResources.Insert(0, dict);
        ThemeChanged?.Invoke(mode);
    }
}
