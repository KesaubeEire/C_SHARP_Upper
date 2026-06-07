# 上位机监控系统 - 7 天速成指南

> 目标：让简历上的技能有代码支撑，面试能讲出来

---

## 📋 准备工作

### 1. 安装 NuGet 包

在 Visual Studio 的包管理器控制台运行：

```powershell
Install-Package System.IO.Ports
Install-Package ScottPlot.WinForms -Version 5.0.55
Install-Package Microsoft.Data.Sqlite -Version 9.0.6
Install-Package System.Data.SqlClient -Version 4.9.0
Install-Package EPPlus -Version 7.7.0
Install-Package System.Text.Json -Version 9.0.6
```

或者在项目文件 `TEST_101.csproj` 中添加：

```xml
<ItemGroup>
  <PackageReference Include="System.IO.Ports" Version="10.0.8" />
  <PackageReference Include="ScottPlot.WinForms" Version="5.0.55" />
  <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.6" />
  <PackageReference Include="System.Data.SqlClient" Version="4.9.0" />
  <PackageReference Include="EPPlus" Version="7.7.0" />
  <PackageReference Include="System.Text.Json" Version="9.0.6" />
</ItemGroup>
```

### 2. 安装 SQL Server（如果没有）

下载 SQL Server Express（免费版）：
```
https://www.microsoft.com/zh-cn/sql-server/sql-server-downloads
```

安装时选择"基本"安装，记住实例名称（通常是 `localhost\SQLEXPRESS`）。

### 3. 创建数据库

打开 SQL Server Management Studio (SSMS)，运行：

```sql
CREATE DATABASE MonitorDB;
GO
```

---

## 📁 项目文件结构

```
Project/TEST_101/
│
├── 📁 Core/                          # 核心基础设施
│   ├── EventBus.cs                   # 事件总线
│   └── DataPoint.cs                  # 数据模型
│
├── 📁 Storage/                       # 数据库模块
│   ├── DatabaseManager.cs            # 数据库管理器
│   ├── Models/
│   │   └── ProductionRecord.cs       # 数据模型
│   └── Repositories/
│       ├── ProductionRepository.cs   # 生产数据仓储
│       ├── AlarmRepository.cs        # 报警仓储
│       └── RecipeRepository.cs       # 配方仓储
│
├── 📁 Chart/                         # 实时曲线模块
│   ├── RealtimeChartControl.cs       # ScottPlot 控件
│   └── ChartDataManager.cs           # 数据管理器
│
├── 📁 Alarm/                         # 报警系统模块
│   ├── AlarmRule.cs                  # 报警规则
│   └── AlarmManager.cs               # 报警管理器
│
├── 📁 Recipe/                        # 配方管理模块
│   └── RecipeManager.cs              # 配方管理器
│
├── 📁 Report/                        # 报表模块
│   ├── StatisticsCalculator.cs       # 统计计算器
│   ├── ExcelExporter.cs              # Excel 导出
│   └── ReportGenerator.cs            # 报表生成器
│
├── ModbusForm.cs                     # 主窗体（需要整合）
├── ModbusProtocol.cs                 # Modbus 协议
├── ModbusTransport.cs                # 通讯层
└── Program.cs                        # 程序入口
```

---

## 📅 7 天计划

### Day 1：数据库集成

#### 1.1 添加引用

在 `ModbusForm.cs` 顶部添加：

```csharp
using TEST_101.Core;
using TEST_101.Storage;
using TEST_101.Storage.Models;
using TEST_101.Storage.Repositories;
```

#### 1.2 添加字段

在 `ModbusForm` 类中添加：

```csharp
// 数据库相关
private DatabaseManager _db = null!;
private ProductionRepository _productionRepo = null!;
private AlarmRepository _alarmRepo = null!;
private RecipeRepository _recipeRepo = null!;
```

#### 1.3 初始化数据库

在 `ModbusForm_Load` 方法中添加：

```csharp
private void ModbusForm_Load(object sender, EventArgs e)
{
    // 原有代码保持不变...
    _transport = new ModbusTransport(this, () => _isTcpMode);
    _transport.FrameReceived += OnFrameReceived;
    _transport.ErrorOccurred += OnError;
    _transport.ConnectionChanged += OnConnectionChanged;
    _history = new InputHistoryManager();
    InitUI();
    RefreshComPorts();

    // ===== 新增：初始化数据库 =====
    try
    {
        // 方式1：SQL Server（推荐，更贴近实际）
        _db = DatabaseManager.CreateSqlServer(
            server: @"localhost\SQLEXPRESS",  // 或 "localhost"
            database: "MonitorDB",
            user: "sa",
            password: "YourPassword123"       // 改成你的密码
        );

        // 方式2：SQLite（备选，无需安装数据库）
        // _db = DatabaseManager.CreateSQLite("monitor.db");

        _productionRepo = new ProductionRepository(_db);
        _alarmRepo = new AlarmRepository(_db);
        _recipeRepo = new RecipeRepository(_db);

        lb_status.Text = "数据库已连接";
    }
    catch (Exception ex)
    {
        MessageBox.Show($"数据库连接失败：{ex.Message}\n将使用无数据库模式", "警告",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
```

#### 1.4 存储数据

在 `OnFrameReceived` 方法中，解析完数据后添加：

```csharp
private void OnFrameReceived(byte[] buffer, bool isTcp)
{
    // 原有代码：解析数据
    byte funcCode = buffer.Length >= 2 ? buffer[1] : (byte)0;
    string hex = BitConverter.ToString(buffer).Replace("-", " ");
    ColorizeHexFrame(box_recv_hex, hex, funcCode, isTcp);
    box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 接收 → {hex}\r\n");

    byte[] pduBuf = isTcp && buffer.Length > ModbusProtocol.MBAP_HEADER_SIZE
        ? buffer.Skip(ModbusProtocol.MBAP_HEADER_SIZE).ToArray()
        : buffer;

    var result = ModbusProtocol.ParseResponse(pduBuf);
    FillGrid(result);

    // ===== 新增：存入数据库 =====
    if (_productionRepo != null && result.Registers.Count > 0)
    {
        try
        {
            foreach (var reg in result.Registers)
            {
                _productionRepo.Insert(new ProductionRecord
                {
                    Timestamp = DateTime.Now,
                    DeviceId = box_dev.Text.Trim(),
                    Address = reg.Index,
                    RawValue = reg.Value,
                    ActualValue = reg.Value * 0.1,  // 根据实际缩放系数调整
                    Unit = "rpm",
                    Name = $"寄存器[{reg.Index}]"
                });
            }
        }
        catch (Exception ex)
        {
            box_recv.AppendText($"[{DateTime.Now:HH:mm:ss}] 数据库存储失败: {ex.Message}\r\n");
        }
    }
}
```

#### 1.5 释放资源

在 `ModbusForm_FormClosing` 方法中添加：

```csharp
private void ModbusForm_FormClosing(object sender, FormClosingEventArgs e)
{
    _transport?.Dispose();
    _db?.Dispose();  // 新增：释放数据库连接
}
```

---

### Day 2：实时曲线

#### 2.1 添加引用

```csharp
using TEST_101.Chart;
```

#### 2.2 添加字段和控件

在 `ModbusForm` 类中添加：

```csharp
// 曲线相关
private RealtimeChartControl _chart = null!;
private ChartDataManager _chartManager = null!;
private Panel panelChart = null!;
```

在 `ModbusForm.Designer.cs` 的 `InitializeComponent()` 中添加：

```csharp
// 创建曲线面板
this.panelChart = new System.Windows.Forms.Panel();
this.panelChart.SuspendLayout();

// 
// panelChart
// 
this.panelChart.Dock = System.Windows.Forms.DockStyle.Bottom;
this.panelChart.Height = 250;
this.panelChart.Name = "panelChart";
this.panelChart.Padding = new System.Windows.Forms.Padding(5);
this.panelChart.ResumeLayout(false);

this.Controls.Add(this.panelChart);
```

#### 2.3 初始化曲线

在 `ModbusForm_Load` 中添加：

```csharp
// ===== 新增：初始化实时曲线 =====
_chart = new RealtimeChartControl { Dock = DockStyle.Fill };
panelChart.Controls.Add(_chart);
_chartManager = new ChartDataManager(_chart);

// 配置通道（根据实际需求调整）
_chartManager.AddChannel(new ChannelConfig
{
    ChannelId = 1,
    Name = "伺服转速",
    DeviceId = "PLC-1",
    Address = 0,           // D100 寄存器地址
    Scale = 0.1,           // 缩放系数
    Offset = 0,
    Unit = "rpm",
    Color = Color.Red.ToArgb(),
    IsEnabled = true
});

_chartManager.AddChannel(new ChannelConfig
{
    ChannelId = 2,
    Name = "伺服转矩",
    DeviceId = "PLC-1",
    Address = 2,           // D102 寄存器地址
    Scale = 0.01,
    Offset = 0,
    Unit = "%",
    Color = Color.Blue.ToArgb(),
    IsEnabled = true
});
```

#### 2.4 发送数据到曲线

在 `OnFrameReceived` 方法中，数据库存储后添加：

```csharp
// ===== 新增：发送到实时曲线 =====
if (result.Registers.Count > 0)
{
    EventBus.Instance.Publish(new DataUpdatedEvent(
        DeviceId: box_dev.Text.Trim(),
        StartAddress: 0,
        Values: result.Registers.Select(r => r.Value).ToArray(),
        Timestamp: DateTime.Now
    ));
}
```

#### 2.5 添加曲线控制按钮

在 `InitUI()` 中添加：

```csharp
// ===== 新增：曲线控制按钮 =====
var btnChartPause = new Button
{
    Text = "⏸ 暂停曲线",
    Width = 80,
    Height = 25,
    Location = new Point(panelChart.Width - 90, 5)
};
btnChartPause.Click += (s, e) =>
{
    _chart.TogglePause();
    btnChartPause.Text = btnChartPause.Text.Contains("暂停") ? "▶ 恢复" : "⏸ 暂停曲线";
};
panelChart.Controls.Add(btnChartPause);
```

---

### Day 3：报警系统

#### 3.1 添加引用

```csharp
using TEST_101.Alarm;
```

#### 3.2 添加字段

```csharp
// 报警相关
private AlarmManager _alarmManager = null!;
```

#### 3.3 初始化报警

在 `ModbusForm_Load` 中添加：

```csharp
// ===== 新增：初始化报警系统 =====
if (_db != null)
{
    _alarmManager = new AlarmManager(_db);

    // 添加报警规则（根据实际需求调整）
    _alarmManager.AddRule(new AlarmRule
    {
        Name = "伺服过速报警",
        DeviceId = box_dev.Text.Trim(),
        Address = 0,
        Condition = AlarmCondition.GreaterThan,
        Threshold = 1500,        // 转速超过 1500 rpm
        Level = AlarmLevel.Fault,
        IsEnabled = true
    });

    _alarmManager.AddRule(new AlarmRule
    {
        Name = "伺服过载报警",
        DeviceId = box_dev.Text.Trim(),
        Address = 2,
        Condition = AlarmCondition.GreaterThan,
        Threshold = 80,          // 转矩超过 80%
        Level = AlarmLevel.Warning,
        IsEnabled = true
    });

    // 订阅报警事件
    _alarmManager.OnAlarmTriggered += OnAlarmTriggered;
}
```

#### 3.4 处理报警

添加报警处理方法：

```csharp
/// <summary>
/// 报警触发回调
/// </summary>
private void OnAlarmTriggered(AlarmRecord alarm)
{
    if (InvokeRequired)
    {
        Invoke(() => OnAlarmTriggered(alarm));
        return;
    }

    // 弹窗提醒
    var icon = alarm.Level switch
    {
        "Emergency" => MessageBoxIcon.Error,
        "Fault" => MessageBoxIcon.Warning,
        _ => MessageBoxIcon.Information
    };

    MessageBox.Show(
        $"⚠️ 报警触发\n\n" +
        $"规则：{alarm.RuleName}\n" +
        $"设备：{alarm.DeviceId}\n" +
        $"当前值：{alarm.CurrentValue:F2}\n" +
        $"阈值：{alarm.Threshold:F2}\n" +
        $"等级：{alarm.Level}",
        "报警提示",
        MessageBoxButtons.OK,
        icon);

    // 更新状态栏
    lb_status.Text = $"⚠️ 报警: {alarm.RuleName}";
    lb_status.ForeColor = Color.Red;
}
```

---

### Day 4：配方管理

#### 4.1 添加引用

```csharp
using TEST_101.Recipe;
```

#### 4.2 添加字段

```csharp
// 配方相关
private RecipeManager _recipeManager = null!;
```

#### 4.3 初始化配方管理器

在 `ModbusForm_Load` 中添加：

```csharp
// ===== 新增：初始化配方管理 =====
if (_db != null)
{
    _recipeManager = new RecipeManager(_db);

    // 订阅配方下发事件
    _recipeManager.OnRecipeDownloaded += (name, count) =>
    {
        MessageBox.Show($"配方 [{name}] 已下发\n写入 {count} 个参数", "成功",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    };
}
```

#### 4.4 配方操作按钮

在 `InitUI()` 中添加配方按钮：

```csharp
// ===== 新增：配方按钮 =====
var btnRecipeSave = new Button
{
    Text = "💾 保存配方",
    Width = 90,
    Height = 30,
    Location = new Point(10, 10),
    BackColor = Color.FromArgb(60, 140, 60),
    ForeColor = Color.White
};
btnRecipeSave.Click += BtnRecipeSave_Click;
this.Controls.Add(btnRecipeSave);

var btnRecipeLoad = new Button
{
    Text = "📋 加载配方",
    Width = 90,
    Height = 30,
    Location = new Point(110, 10),
    BackColor = Color.FromArgb(60, 100, 180),
    ForeColor = Color.White
};
btnRecipeLoad.Click += BtnRecipeLoad_Click;
this.Controls.Add(btnRecipeLoad);
```

#### 4.5 配方操作方法

```csharp
/// <summary>
/// 保存配方
/// </summary>
private void BtnRecipeSave_Click(object? sender, EventArgs e)
{
    if (_recipeManager == null)
    {
        MessageBox.Show("配方管理器未初始化", "错误");
        return;
    }

    var name = Microsoft.VisualBasic.Interaction.InputBox(
        "请输入配方名称：", "保存配方", "产品A-标准");

    if (string.IsNullOrWhiteSpace(name)) return;

    try
    {
        _recipeManager.SaveRecipe(name, "从上位机保存", new List<RecipeParameter>
        {
            new RecipeParameter
            {
                Name = "伺服转速",
                Address = 0,
                Value = 1000,
                Scale = 0.1,
                Offset = 0,
                Unit = "rpm"
            },
            new RecipeParameter
            {
                Name = "变频器频率",
                Address = 100,
                Value = 50,
                Scale = 1.0,
                Offset = 0,
                Unit = "Hz"
            }
        });

        MessageBox.Show($"配方 [{name}] 已保存", "成功");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"保存失败: {ex.Message}", "错误");
    }
}

/// <summary>
/// 加载配方
/// </summary>
private void BtnRecipeLoad_Click(object? sender, EventArgs e)
{
    if (_recipeManager == null)
    {
        MessageBox.Show("配方管理器未初始化", "错误");
        return;
    }

    var recipes = _recipeManager.GetAllRecipes();
    if (recipes.Count == 0)
    {
        MessageBox.Show("暂无配方", "提示");
        return;
    }

    // 简单实现：选择第一个配方
    var recipe = recipes[0];
    var parameters = _recipeManager.GetRecipeParameters(recipe.Name);

    if (parameters != null)
    {
        var msg = $"配方 [{recipe.Name}] 参数：\n\n";
        foreach (var p in parameters)
        {
            msg += $"  {p.Name}: {p.Value} {p.Unit}\n";
        }
        MessageBox.Show(msg, "配方详情");
    }
}
```

---

### Day 5：Excel 导出

#### 5.1 添加引用

```csharp
using TEST_101.Report;
```

#### 5.2 添加字段

```csharp
// 报表相关
private ReportGenerator _reportGenerator = null!;
```

#### 5.3 初始化报表生成器

在 `ModbusForm_Load` 中添加：

```csharp
// ===== 新增：初始化报表生成器 =====
if (_db != null)
{
    _reportGenerator = new ReportGenerator(_db);
}
```

#### 5.4 导出按钮

在 `InitUI()` 中添加：

```csharp
// ===== 新增：导出按钮 =====
var btnExport = new Button
{
    Text = "📊 导出报表",
    Width = 90,
    Height = 30,
    Location = new Point(210, 10),
    BackColor = Color.FromArgb(180, 100, 60),
    ForeColor = Color.White
};
btnExport.Click += BtnExport_Click;
this.Controls.Add(btnExport);
```

#### 5.5 导出方法

```csharp
/// <summary>
/// 导出 Excel 报表
/// </summary>
private void BtnExport_Click(object? sender, EventArgs e)
{
    if (_reportGenerator == null)
    {
        MessageBox.Show("报表生成器未初始化", "错误");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "Excel 文件|*.xlsx",
        FileName = $"生产报表_{DateTime.Now:yyyyMMdd}.xlsx"
    };

    if (dialog.ShowDialog() == DialogResult.OK)
    {
        try
        {
            _reportGenerator.GenerateDailyReport(DateTime.Now, dialog.FileName);
            MessageBox.Show($"报表已导出到：\n{dialog.FileName}", "成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 打开文件
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dialog.FileName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误");
        }
    }
}
```

---

### Day 6：整合测试

#### 6.1 完整的 ModbusForm_Load

```csharp
private void ModbusForm_Load(object sender, EventArgs e)
{
    // 1. 初始化通讯
    _transport = new ModbusTransport(this, () => _isTcpMode);
    _transport.FrameReceived += OnFrameReceived;
    _transport.ErrorOccurred += OnError;
    _transport.ConnectionChanged += OnConnectionChanged;
    _history = new InputHistoryManager();

    InitUI();
    RefreshComPorts();

    // 2. 初始化数据库
    try
    {
        _db = DatabaseManager.CreateSqlServer(@"localhost\SQLEXPRESS", "MonitorDB");
        // _db = DatabaseManager.CreateSQLite("monitor.db");

        _productionRepo = new ProductionRepository(_db);
        _alarmRepo = new AlarmRepository(_db);
        _recipeRepo = new RecipeRepository(_db);
        _recipeManager = new RecipeManager(_db);
        _reportGenerator = new ReportGenerator(_db);

        lb_status.Text = "数据库已连接";
    }
    catch (Exception ex)
    {
        MessageBox.Show($"数据库连接失败：{ex.Message}", "警告");
    }

    // 3. 初始化实时曲线
    _chart = new RealtimeChartControl { Dock = DockStyle.Fill };
    panelChart.Controls.Add(_chart);
    _chartManager = new ChartDataManager(_chart);
    _chartManager.AddChannel(new ChannelConfig
    {
        ChannelId = 1, Name = "伺服转速", DeviceId = "PLC-1",
        Address = 0, Scale = 0.1, Unit = "rpm",
        Color = Color.Red.ToArgb(), IsEnabled = true
    });

    // 4. 初始化报警
    if (_db != null)
    {
        _alarmManager = new AlarmManager(_db);
        _alarmManager.AddRule(new AlarmRule
        {
            Name = "伺服过速", DeviceId = "PLC-1", Address = 0,
            Condition = AlarmCondition.GreaterThan, Threshold = 1500,
            Level = AlarmLevel.Fault, IsEnabled = true
        });
        _alarmManager.OnAlarmTriggered += OnAlarmTriggered;
    }
}
```

#### 6.2 测试清单

- [ ] 程序能正常启动
- [ ] 数据库连接成功
- [ ] Modbus 读取正常
- [ ] 数据能存入数据库
- [ ] 实时曲线能显示
- [ ] 报警能触发
- [ ] 配方能保存和加载
- [ ] Excel 能导出

---

### Day 7：面试准备

#### 7.1 高频问题及答案

**Q1: 你的上位机架构是怎样的？**

> 采用分层架构：
> - **UI 层**：WinForms 界面，负责数据显示和用户交互
> - **业务逻辑层**：报警检测、配方管理、数据统计
> - **数据访问层**：Repository 模式，封装数据库操作
> - **通讯层**：Modbus 协议实现，支持 RTU/TCP
> - **事件总线**：模块间解耦通信
>
> 这样的好处是各层职责清晰，方便维护和扩展。

**Q2: 数据库为什么用 SQL Server？**

> 工业上位机需要：
> 1. **多客户端并发**：多台上位机同时写入，SQLite 的文件锁扛不住
> 2. **数据量大**：一台设备每秒采集一次，一天就是 8.6 万条
> 3. **系统集成**：要对接 MES、ERP，需要统一的数据库平台
> 4. **运维需求**：需要定时备份、权限管理、审计日志
>
> 开发阶段用 SQLite 方便测试，生产环境切到 SQL Server。

**Q3: 实时曲线如何高性能刷新？**

> 三个关键点：
> 1. **数据队列**：用 ConcurrentQueue 缓存数据，线程安全
> 2. **定时刷新**：100ms 刷新一次，避免每次都刷新
> 3. **限制数据点**：最多保留 500 个点，超出自动丢弃旧数据
>
> ScottPlot 库本身性能很好，支持百万级数据点。

**Q4: 报警如何防止抖动？**

> 使用冷却时间机制：
> - 同一条规则 5 秒内不重复报警
> - 用 ConcurrentDictionary 记录上次报警时间
> - 触发报警前先检查是否在冷却期
>
> 这样可以避免信号波动导致的误报。

**Q5: 多线程如何管理？**

> - 通讯层：每个设备独立线程，避免阻塞 UI
> - 数据存储：用队列缓冲，批量写入数据库
> - UI 更新：用 Invoke 封送回 UI 线程
> - 线程安全：ConcurrentDictionary、ConcurrentQueue

**Q6: 你遇到过什么技术难题？**

> 准备一个"踩坑"故事，例如：
>
> "在做串口通讯时，遇到数据丢失的问题。调试发现是串口缓冲区溢出，因为数据量大的时候，读取速度跟不上。我加了流控机制（XON/XOFF），并且用队列缓冲数据，异步处理，解决了这个问题。"

#### 7.2 简历项目描述模板

**项目名称：** XXX 监控系统

**技术栈：** C#/.NET + WinForms + SQL Server + Modbus RTU/TCP

**项目职责：**
- 负责上位机程序开发，实现多设备数据采集、实时监控、报警管理
- 使用 Modbus 协议与 PLC 通讯，实现寄存器读写
- 设计数据库表结构，实现生产数据存储和历史查询
- 开发实时曲线功能，使用 ScottPlot 库实现高性能绑图
- 实现报警系统，支持阈值配置、报警记录、声音提醒
- 开发配方管理功能，支持配方下发到 PLC
- 使用 EPPlus 库实现生产报表导出

**技术亮点：**
- 采用事件总线实现模块间解耦
- 使用异步编程避免 UI 卡顿
- 报警冷却机制防止抖动
- 批量写入优化数据库性能

---

## 🔧 常见问题

### Q1: SQL Server 连接失败

检查：
1. SQL Server 服务是否启动
2. 防火墙是否放行 1433 端口
3. 连接字符串是否正确
4. 用户名密码是否正确

### Q2: ScottPlot 命名空间冲突

在文件顶部添加：
```csharp
using Color = System.Drawing.Color;
```

### Q3: EPPlus 许可证错误

在使用前添加：
```csharp
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
```

### Q4: 跨线程访问 UI 控件

使用 Invoke：
```csharp
if (InvokeRequired)
{
    Invoke(() => UpdateUI());
    return;
}
```

---

## 📚 参考资源

- **ScottPlot 文档**：https://scottplot.net/
- **EPPlus 文档**：https://epplussoftware.com/
- **Modbus 协议**：https://www.modbus.org/
- **SQL Server 文档**：https://docs.microsoft.com/zh-cn/sql/

---

## ✅ 完成检查

7 天后，你应该能做到：

- [ ] 程序能连接 SQL Server
- [ ] Modbus 数据能存入数据库
- [ ] 实时曲线能显示数据趋势
- [ ] 报警系统能检测异常
- [ ] 配方能保存和下发
- [ ] 报表能导出 Excel
- [ ] 每个功能面试能讲清楚

---

*最后更新：2026-06-08*
