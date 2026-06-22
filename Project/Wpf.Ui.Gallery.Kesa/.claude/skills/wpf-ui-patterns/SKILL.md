# wpf-ui-patterns

生成 Wpf.Ui 组件使用代码片段，遵循 Gallery 现有模式。

## 导航

```csharp
// 注册页面（App.xaml.cs 或模块初始化）
services.AddSingleton<MyPage>();
services.AddSingleton<MyPageViewModel>();

// 导航（从 ViewModel 中）
_navigationService.Navigate(typeof(MyPage));
```

## 页面结构

```xaml
<Page x:Class="Wpf.Ui.Gallery.Views.Pages.MyPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:local="clr-namespace:Wpf.Ui.Gallery.Views.Pages">

    <StackPanel>
        <!-- Wpf.Ui 控件 -->
        <ui:Button Content="Test" Command="{Binding MyCommand}" />
        <ui:TextBox Text="{Binding MyProperty}" />
    </StackPanel>
</Page>
```

## ViewModel 模式

```csharp
public partial class MyPageViewModel : ViewModel
{
    [ObservableProperty]
    private string _myProperty = "default";

    [RelayCommand]
    private void OnDoSomething()
    {
        // 业务逻辑
    }
}
```

## 对话框/Snackbar

```csharp
// Snackbar（短暂通知）
_snackbarService.Show("操作成功", "数据已发送", ControlAppearance.Success);

// ContentDialog（确认对话框）
var result = await _contentDialogService.ShowAsync(
    new ContentDialogCreateResult
    {
        Title = "确认",
        Content = "确定要执行此操作？",
        PrimaryButtonText = "确定",
        CloseButtonText = "取消"
    });
```
