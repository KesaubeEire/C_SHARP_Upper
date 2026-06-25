# PollingScheduler → 响应式 Store 重构方案

## 现状问题

当前 `PollingScheduler` 是一个普通类，状态变更通过**原始事件**（`DataUpdated`、`PollingStarted`、`PollingStopped`）通知外部。

多个 ViewModel 必须：
1. 手动订阅事件
2. 在事件处理器中写状态同步代码（`IsPolling = true`、`PollStatusText = "轮询运行中"`）
3. 重复订阅/退订逻辑

每增加一个关心轮询状态的 ViewModel，就要重复一套模板。

## 目标

把 `PollingScheduler` 改造成**全局响应式 Store**，类似前端 Pinia / Zustand：

```
PollingStore（单例，ObservableObject）
├─ IsRunning          → 任何 XAML {Binding Scheduler.IsRunning} 自动响应
├─ LatencyMs          → 延迟数字自动刷新
├─ StatusText         → "运行中" / "已停止" / "连接失败"
├─ ConnectionQuality  → LED 颜色自动变
└─ 属性变化自动通知所有绑定/订阅者
```

## 改动方案

### 1. 新建 `PollingStore.cs`

继承 `ObservableObject`，用 `[ObservableProperty]` 声明状态：

```csharp
public partial class PollingStore : ObservableObject
{
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private long _latencyMs;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private LedQuality _quality = LedQuality.Disabled;
}
```

### 2. `PollingScheduler` 注入 `PollingStore`

不再自己持有 `IsConnected`/`LatencyMs` 字段，直接写入 Store：

```csharp
public class PollingScheduler
{
    private readonly PollingStore _store;

    public PollingScheduler(PollingStore store) { _store = store; }

    public void Start(...)
    {
        // ... 启动 timer
        _store.IsRunning = true;
        _store.StatusText = "轮询运行中";
        _store.Quality = LedQuality.Good;
    }
}
```

### 3. 删除手动事件订阅

`PpeConnectionSectionViewModel` 不再需要：
- `PollingStarted`/`PollingStopped` 事件订阅
- `OnPollingStarted()`/`OnPollingStopped()` 处理器
- `IsPolling`、`PollStatusText`、`PollQuality` 属性（直接从 Store 读取）

XAML 绑定改为指向 Store：
```xml
<!-- 现在 -->
IsEnabled="{Binding IsPolling}"
Quality="{Binding PollQuality}"

<!-- 改后 -->
IsEnabled="{Binding Store.IsRunning}"
Quality="{Binding Store.Quality}"
```

### 4. 依赖注入注册

```csharp
// App.xaml.cs
services.AddSingleton<PollingStore>();
services.AddSingleton<PollingScheduler>();  // 注入 Store
```

### 5. 消除的文件/代码

- `PollingScheduler.PollingStarted` / `PollingStopped` 事件 → 由 Store 属性变化替代
- `PpeConnectionSectionViewModel.OnPollingStarted()` / `OnPollingStopped()` → 删除
- `PpeConnectionSectionViewModel.IsPolling` / `PollStatusText` / `PollQuality` → 改为委托给 Store

## 收益

| 方面 | 当前（事件） | 改后（Store） |
|------|-------------|---------------|
| 新增消费者 | 写事件订阅 + 状态同步代码 | 注入 Store + XAML 绑定 |
| 状态源 | 分散在 N 个 ViewModel | 唯一可信源 |
| 调试 | 追事件链 | 一个对象看全部状态 |
| 测试 | mock 事件发射 | mock Store 属性 |

## 优先度建议

**建议下一轮做**。当前的 `PollingScheduler` + 事件方案已经能工作，Store 重构是"从能用到好用"的优化，不修也不影响功能。
