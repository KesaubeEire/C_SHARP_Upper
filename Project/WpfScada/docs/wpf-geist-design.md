# WPF Geist 设计规范 (WPF UI 适配版)

> 将 Vercel Geist 设计系统的方法论映射到 WPF UI 框架的 XAML 实践。
> 适用目标：工业自动化上位机（PLC 监控面板、配方管理、IO 监视器等）。

---

## 1. 间距系统 — Spacing Grid

Geist 基础 4px 网格：**4, 8, 12, 16, 24, 32, 40, 64, 96**

### WPF Margin / Padding 速查

| 场景 | 推荐值 | 说明 |
|------|--------|------|
| 内联元素横向间距 | `Margin="0,0,8,0"` | 4px × 2 |
| 表单字段标签到控件 | `Margin="0,0,8,0"` | 4px × 2 |
| 卡片内边距 | `Padding="16"` | 4px × 4 |
| 状态条内边距 | `Padding="16,8"` | 4px × 4 (水平), 4px × 2 (垂直) |
| 段间距 | `Margin="0,16,0,0"` | 4px × 4 |
| 大段间距 | `Margin="0,24,0,0"` | 4px × 6 |
| 按钮组间距 | `Margin="0,0,8,0"` | 4px × 2 |
| 紧凑包装间距 | `Margin="0,4,0,0"` | 4px × 1 |
| 页面底部留白 | `Margin="0,0,0,24"` | 4px × 6 |

### XAML 示例

```xml
<!-- 标准卡片容器 -->
<Border Padding="16" ... >
    <StackPanel>
        <!-- 两段 16px 间距 -->
        <Section1 />
        <Section2 Margin="0,16,0,0" />
    </StackPanel>
</Border>

<!-- 水平按钮组 -->
<StackPanel Orientation="Horizontal">
    <ui:Button Margin="0,0,8,0" ... />
    <ui:Button Margin="0,0,8,0" ... />
    <ui:Button ... />
</StackPanel>
```

---

## 2. 排版 — Typography

### Geist → WPF UI FontTypography 映射

| Geist 类别 | 用途场景 | WPF UI FontTypography |
|-----------|---------|----------------------|
| `heading-72 ~ heading-48` | 大标题 / Hero 区 | `TitleLarge`, `Display` |
| `heading-40 ~ heading-24` | 区块标题 | `Subtitle` |
| `heading-20 ~ heading-16` | 卡片标题、区域名 | `BodyStrong` |
| `label-14` | 表单标签、导航、表头 | `Caption` 或 `Body` |
| `label-13`、`label-12` | 元数据、状态文字、时间戳 | `Caption` |
| `copy-16`、`copy-14` | 正文段落 | `Body` |
| `button-14` | 按钮文字 | 由 `ui:Button` 自动处理 |

### PLC 控件专有映射

| 场景 | 原写法（禁止） | 新写法（强制） |
|------|-------------|-------------|
| 大型数值显示（28px Bold） | `<TextBlock FontSize="28" FontWeight="Bold" />` | `<ui:TextBlock FontTypography="Title" />` |
| 卡片标题（16px SemiBold） | `<TextBlock FontSize="16" FontWeight="SemiBold" />` | `<ui:TextBlock FontTypography="Subtitle" />` |
| 标签名（12px） | `<TextBlock FontSize="12" />` | `<ui:TextBlock FontTypography="Caption" />` |
| 字段标签（13px） | `<TextBlock FontSize="13" />` | `<ui:TextBlock FontTypography="Body" />` |
| 状态文字 / 时间戳（11px） | `<TextBlock FontSize="11" />` | `<ui:TextBlock FontTypography="Caption" />` |

### 常用 `ui:TextBlock` 组合

```xml
<!-- 区域标题 -->
<ui:TextBlock FontTypography="Subtitle" Foreground="{DynamicResource TextFillColorPrimaryBrush}" />

<!-- 卡片标题 -->
<ui:TextBlock FontTypography="BodyStrong" Foreground="{DynamicResource TextFillColorPrimaryBrush}" />

<!-- 次要文字（描述、标签） -->
<ui:TextBlock FontTypography="Caption" Foreground="{DynamicResource TextFillColorSecondaryBrush}" />

<!-- 正文 -->
<ui:TextBlock FontTypography="Body" Foreground="{DynamicResource TextFillColorPrimaryBrush}" />

<!-- 使用 Appearance 简写次要文字（WPF UI 特有） -->
<ui:TextBlock Appearance="Secondary" FontTypography="Body" />
```

---

## 3. 组件尺寸

### Geist 32 / 40 / 48 体系

| 尺寸 | Geist 名称 | 适用 WPF 场景 | 控件 |
|------|-----------|-------------|------|
| **32px** | `small` | 内联按钮、紧凑操作、位字节面板行高、关联删除按钮 | `Height="32"` |
| **40px** | `medium`（默认） | 主按钮、输入框、选择器 | 默认（WPF UI 标准高度） |
| **48px** | `large` | 主操作按钮、Hero 区操作 | `Height="48"` |

### XAML 示例

```xml
<!-- 小型操作按钮（位面板的读取/写入） -->
<ui:Button Height="32" Appearance="Primary" Content="读取" />

<!-- 默认尺寸按钮 -->
<ui:Button Appearance="Primary" Content="连接" />

<!-- 大号按钮 -->
<ui:Button Height="48" Appearance="Success" Content="保存配方" />
```

---

## 4. 圆角 — Corner Radii

| 值 | Geist 等效 | 适用场景 |
|----|-----------|---------|
| **4** | —（紧凑） | 位块（BitBlock）、Tag、内嵌小元素 |
| **6** | `rounded-sm` | 按钮、输入框、小控件、状态条 |
| **8** | `rounded-md`（适配） | 卡片容器、面板、页面块、ControlExample |
| **12** | `rounded-lg` | 浮层、菜单、模态框 |
| **16** | — | 全屏面板 |
| **9999** | `rounded-full` | 圆形头像、胶囊（Pill） |

> 注意：Geist 原版 `rounded` 是 sm=6, md=12, lg=16。WPF UI 标准卡片的 CornerRadius 是 8，所以我们在容器层面用 **8** 来匹配 WPF UI 的风格。

### XAML 示例

```xml
<!-- 标准卡片（8） -->
<Border CornerRadius="8" ... />

<!-- 按钮 / 输入框（6） -->
<Border CornerRadius="6" ... />

<!-- 位块 / 内嵌（4） -->
<Border CornerRadius="4" ... />
```

---

## 5. 颜色 — Color Tokens

### Geist Step → WPF UI DynamicResource 映射

| Geist Step | 含义 | WPF UI Brush Key |
|-----------|------|-----------------|
| 100 | 默认背景 | `CardBackground` / `ApplicationBackgroundBrush` |
| 200 | Hover 背景 | `CardBackgroundFillColorSecondaryBrush` |
| 400 | 默认边框 | `ControlElevationBorderBrush` |
| 700 | 实心填充 / 主色 | `SystemAccentColorPrimaryBrush` |
| 900 | 次要文字 | `TextFillColorSecondaryBrush` |
| 1000 | 主要文字 | `TextFillColorPrimaryBrush` |
| — | 禁用文字 | `TextFillColorDisabledBrush` |
| — | 第三级文字 | `TextFillColorTertiaryBrush` |

### PLC 状态色（固定含义）

| 状态 | WPF UI Brush Key | 说明 |
|------|-----------------|------|
| 绿色/正常 | `SystemFillColorSuccessBrush` | 连接成功、轮询运行、设备正常 |
| 红色/错误 | `SystemFillColorCriticalBrush` | 连接失败、写入错误、报警 |
| 黄色/警告 | `SystemFillColorCautionBrush` | 警告状态、异常 |
| 蓝色/提示 | `SystemFillColorAttentionBrush` | 信息提示、进行中 |
| 灰色/禁用 | `TextFillColorDisabledBrush` | 未连接、关闭、禁用 |
| 中性 | `SystemFillColorNeutralBrush` | 就绪、待机 |

### 硬编码颜色禁止规则

```xml
<!-- ❌ 禁止 -->
<TextBlock Foreground="#FF3A3A3A" />
<Ellipse Fill="#27AE60" />

<!-- ✅ 正确 -->
<ui:TextBlock Foreground="{DynamicResource TextFillColorSecondaryBrush}" />
<plc:LedIndicator Quality="Good" />
```

```csharp
// ❌ 禁止
btn.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
indicator.Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219));

// ✅ 正确
btn.Foreground = (Brush)Application.Current.FindResource("SystemFillColorSuccessBrush");

// ✅ 兜底模式（当资源缺失时用 fallback）
public static Brush GetResourceBrush(string key, Color fallback)
    => Application.Current.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
```

---

## 6. 图标系统 — Icon Pattern

### emoji → WPF UI SymbolIcon 对照表

| emoji | 含义 | SymbolRegular 常量 |
|-------|------|-------------------|
| 📂 | 打开/导入 | `SymbolRegular.FolderOpen24` |
| ▶ | 启动/运行 | `SymbolRegular.Play24` |
| ■ | 停止 | `SymbolRegular.Stop24` |
| ✕ | 删除/关闭 | `SymbolRegular.Dismiss24` |
| 💾 | 保存 | `SymbolRegular.Save24` |
| 🔒 | 锁定 / 写模式关 | `SymbolRegular.LockClosed24` |
| 🔓 | 解锁 / 写模式开 | `SymbolRegular.LockOpen24` |
| ⟳ | 刷新 | `SymbolRegular.ArrowSync24` |
| ＋ | 添加 | `SymbolRegular.Add24` |
| － | 删除 | `SymbolRegular.Subtract24` |
| 📡 | 信号/通信 | `SymbolRegular.Wifi424` |
| 📄 | 文档/空状态 | `SymbolRegular.Document24` |

> ⚠️ **重要：XAML 中不要加 `SymbolRegular.` 前缀！**
> 标准 `Enum.Parse` 无法解析 `SymbolRegular.Play24`（短类型名前缀），XAML 中只需写 `Play24`。
> C# 代码中才需要完整写法：`SymbolRegular.Play24`。
>
> 完整列表见 [Fluent System Icons](https://github.com/microsoft/fluentui-system-icons)。
> 可选后缀：`24`（默认）、`20`、`16`、`32`、`48`。

### XAML 示例

```xml
<!-- ✅ 正确：Button + SymbolIcon -->
<ui:Button Appearance="Primary" Content="连接">
    <ui:Button.Icon>
        <ui:SymbolIcon Symbol="PlugDisconnected24" />
    </ui:Button.Icon>
</ui:Button>

<!-- ✅ 正确：只有图标没有文字 -->
<ui:Button Appearance="Secondary" ToolTip="删除">
    <ui:Button.Icon>
        <ui:SymbolIcon Symbol="Dismiss24" />
    </ui:Button.Icon>
</ui:Button>

<!-- ❌ 禁止：emoji 作为按钮内容 -->
<ui:Button Content="📂 导入 .db" />
```

---

## 7. 卡片容器模式

### 标准卡片模板

```xml
<Border
    Padding="16"
    Background="{DynamicResource CardBackground}"
    BorderBrush="{DynamicResource ControlElevationBorderBrush}"
    BorderThickness="1"
    CornerRadius="8">
    <!-- 卡片内容 -->
</Border>
```

### 状态条模板（紧凑卡片）

```xml
<Border
    Padding="16,8"
    Background="{DynamicResource CardBackground}"
    BorderBrush="{DynamicResource ControlElevationBorderBrush}"
    BorderThickness="1"
    CornerRadius="8">
    <StackPanel Orientation="Horizontal">
        <plc:LedIndicator Width="10" Height="10" Quality="..." />
        <ui:TextBlock Margin="6,0,0,0" FontTypography="Caption" Text="..." />
    </StackPanel>
</Border>
```

### 侧边栏段模板

```xml
<!-- 一个功能区块 -->
<StackPanel>
    <ui:TextBlock
        Margin="0,0,0,8"
        FontTypography="BodyStrong"
        Foreground="{DynamicResource TextFillColorPrimaryBrush}"
        Text="区块标题" />
    <!-- 区块内容（表单、按钮组等） -->
    <ui:TextBox PlaceholderText="..." />
    <ui:Button Appearance="Primary" Content="操作" />
</StackPanel>
```

---

## 8. 状态指示灯模式

统一使用 `plc:LedIndicator` 控件，通过 `Quality` 枚举驱动颜色：

```xml
<plc:LedIndicator
    Width="10"
    Height="10"
    Quality="Good"           <!-- Good / Bad / Warning / Info / Disabled -->
    ToolTipText="正常运行" />
```

- `Good` → 绿色（SystemFillColorSuccessBrush）
- `Bad` → 红色（SystemFillColorCriticalBrush）
- `Warning` → 黄色（SystemFillColorCautionBrush）
- `Info` → 蓝色（SystemFillColorAttentionBrush）
- `Disabled` → 灰色（TextFillColorDisabledBrush）

---

## 9. 按钮语义配色

参考 Geist primary / secondary / error 与 WPF UI Appearance 的对应：

| 操作类型 | Appearance | 场景 |
|---------|-----------|------|
| 主要操作 | `Primary` | 连接、启动、保存、读取 |
| 次要操作 | `Secondary` | 断开、停止、取消、刷新 |
| 成功/确认 | `Success` | 保存成功、确认操作 |
| 危险/删除 | `Danger` | 删除、清除、断开 |
| 警告 | `Caution` | 写模式切换、危险操作 |

---

## 10. XAML 命名空间规范

```xml
<!-- 标准控件前缀 -->
xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"

<!-- PLC 自定义控件 -->
xmlns:plc="clr-namespace:WpfScada.Controls.Plc"

<!-- ViewModel 设计时数据 -->
xmlns:vm="clr-namespace:WpfScada.ViewModels.Pages.Plc"
```

---

## 11. 页面 / UserControl 根元素模板

```xml
<!-- Page -->
<Page
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    ui:Design.Background="{DynamicResource ApplicationBackgroundBrush}"
    ui:Design.Foreground="{DynamicResource TextFillColorPrimaryBrush}"
    Foreground="{DynamicResource TextFillColorPrimaryBrush}"
    mc:Ignorable="d">

    <Grid Margin="0,0,0,24">
    <!-- 页面内容 -->
    </Grid>
</Page>
```

```xml
<!-- UserControl -->
<UserControl
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    ui:Design.Background="{DynamicResource ApplicationBackgroundBrush}"
    ui:Design.Foreground="{DynamicResource TextFillColorPrimaryBrush}"
    mc:Ignorable="d">

    <Border Padding="16" CornerRadius="8" ...>
    </Border>
</UserControl>
```

---

## 变更日志

| 日期 | 版本 | 修改内容 |
|------|------|---------|
| 2026-06-24 | 1.0 | 初始版本 |
