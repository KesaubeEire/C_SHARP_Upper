# WPF_S71200_SAMPLE

C# WPF 上位机，S7-1200 通信实战。

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（Windows only）
- Windows（WPF 依赖 Win32 平台）

## 本地构建 & 运行

```bash
cd Project/WPF_S71200_SAMPLE
dotnet restore
dotnet build
dotnet run --project WPF_S71200_SAMPLE.csproj
```

`dotnet restore` 会自动从 nuget.org 拉取所有依赖包，无需手动逐个安装。

## 依赖包

| 包名 | 用途 |
|------|------|
| LiveChartsCore.SkiaSharpView.WPF | 图表绘制 |
| MaterialDesignThemes | UI 主题 |
| Sharp7 | S7 协议通信 |

测试项目 `WpfTests` 额外依赖 xunit + coverlet，`dotnet test` 即可运行。
