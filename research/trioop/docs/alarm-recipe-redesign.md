# 报警与配方模块重写 — 设计文档

> 参考项目：`Wpf.Ui.Gallery.Kesa` (WPF MVVM 架构)
> 重写日期：2026-06-28

## 概述

对 Trioop PLC Monitor 的报警和配方模块进行了全面重写，参照 WPF 参考项目的功能设计和交互模式，在 Web (React + Express) 上实现了功能等价的功能。

---

## 报警系统

### 参考文件 (WPF)

| 文件 | 对应功能 |
|------|---------|
| `Models/Plc/AlarmItem.cs` | 报警数据模型 |
| `Services/Plc/AlarmService.cs` | 报警引擎服务 |
| `ViewModels/Pages/Plc/AlarmViewModel.cs` | 报警 ViewModel / 前端逻辑 |
| `Views/Pages/Plc/AlarmPage.xaml` | 报警页面 UI |
| `Controls/Plc/AlarmStatCards.xaml` | 统计卡片 |
| `Controls/Plc/AlarmToolbar.xaml` | 工具栏 |
| `Controls/Plc/AlarmFilterBar.xaml` | 过滤栏 |
| `Controls/Plc/AlarmDetailPanel.xaml` | 报警详情面板 |
| `Models/Plc/AlarmItem.cs` — AlarmConditionType | 报警条件类型枚举 |

### 核心功能

#### 1. 报警数据模型 (`shared/types.ts`)

- `AlarmSeverity` 枚举: Info / Warning / Critical / Emergency
- `AlarmConditionType` 枚举: High / HighHigh / Low / LowLow / NotEqual / RateOfChange / Digital
- `AlarmItem` 接口: 时间戳、严重度、类型、变量名、描述、区域、当前值、阈值、死区、活动/确认/搁置状态、操作人、备注
- `AlarmRule` 接口: 变量键、数据类型、描述、严重度、条件类型、阈值、死区、OnDelay、OffDelay、区域、启用
- `AlarmStatistics` 接口: 总活动、未确认、搁置、今日、本小时、Emergency、Critical

#### 2. 报警引擎 (`server/alarmEngine.ts`)

**规则管理**:
- CRUD (addRule / removeRule / updateRule / getRules)
- JSON 持久化 (`data/alarm-rules.json`)
- 从 default-rules.json 同步（自动创建）
- CSV 导出/导入 (`exportRulesCsv` / `importRulesCsv`)

**报警生命周期** (ISA 18.2):
1. **触发检查**: `checkAlarms(data)` 每次数据更新时调用
2. **死区防抖**: `checkWithDeadband()` — 触发后需越过死区才恢复
3. **OnDelay**: 条件持续满足超过延时后才触发报警
4. **OffDelay**: 条件不满足超过延时后才清除报警
5. **确认** (`acknowledgeAlarm` / `acknowledgeAll`): 标记已确认
6. **搁置** (`shelveAlarm` / `unshelveAlarm`): 带到期自动恢复
7. **恢复**: 条件不满足 + OffDelay 后自动清除
8. **备注**: `addComment`
9. **清除全部**: `clearAll`
10. **统计**: `getStatistics()` — 实时计算

**持久化**: 报警历史存 `data/alarm-history.json`，最多保留 MAX_PERSIST(5000) 条

#### 3. 报警 API (`server/index.ts`)

| Method | Route | 功能 |
|--------|-------|------|
| GET | `/api/alarm/rules` | 获取规则列表 |
| POST | `/api/alarm/rules` | 添加规则 |
| PUT | `/api/alarm/rules/:variableKey` | 更新规则 |
| DELETE | `/api/alarm/rules/:variableKey` | 删除规则 |
| GET | `/api/alarm/rules/export` | 导出规则 CSV |
| POST | `/api/alarm/rules/import` | 导入规则 CSV |
| GET | `/api/alarm/active` | 活动报警 |
| GET | `/api/alarm/shelved` | 已搁置报警 |
| GET | `/api/alarm/history` | 全部报警历史 |
| GET | `/api/alarm/statistics` | 统计 |
| GET | `/api/alarm/export` | 导出报警 CSV |
| POST | `/api/alarm/ack` | 确认(单条/全部) |
| POST | `/api/alarm/ack/:id` | 确认单条 |
| POST | `/api/alarm/shelve/:id` | 搁置 |
| POST | `/api/alarm/unshelve/:id` | 取消搁置 |
| POST | `/api/alarm/comment/:id` | 添加备注 |
| POST | `/api/alarm/clear` | 清除全部 |

#### 4. 前端组件 (`src/components/AlarmPanel.tsx`)

布局分区（从上到下）:
1. **统计卡片** — 7 列 CSS Grid: 总报警/活动/未确认/今日/本小时/Emergency/Critical
2. **工具栏** — 刷新/全部确认(flyout)/批量搁置(flyout: 30min/1h/8h/永久)/导出CSV/规则管理切换/清除
3. **规则管理面板** — 可折叠，规则表格 + 编辑表单(11个字段) + 导入/导出CSV
4. **标签切换** — 活动报警/报警历史/报警规则
5. **过滤栏** — 文本搜索/严重度下拉/区域/日期范围/显示搁置checkbox/重置
6. **报警表格** — 状态彩色药丸/时间/严重度彩色标签/变量/描述/区域/操作(确认flyout/搁置flyout)
7. **状态条** — 状态文本 + 条目计数

---

## 配方系统

### 参考文件 (WPF)

| 文件 | 对应功能 |
|------|---------|
| `Models/Plc/RecipeParameter.cs` | 配方参数模型 |
| `Models/Plc/RecipeGroup.cs` | 配方参数组模型 |
| `Models/Plc/RecipeRecord.cs` | 配方记录模型 |
| `Models/Plc/RecipeStatus.cs` | 配方状态枚举 |
| `Models/Plc/RecipeVersionSnapshot.cs` | 版本快照模型 |
| `Services/Plc/RecipeService.cs` | 配方服务 |
| `ViewModels/Pages/Plc/RecipeViewModel.cs` | 配方 ViewModel |
| `Views/Pages/Plc/RecipePage.xaml` | 配方页面 UI |

### 核心功能

#### 1. 配方数据模型 (`shared/types.ts`)

- `RecipeStatus` 枚举: Draft / Active / Archived
- `RecipeParameter`: 名称、值、单位、地址、缩放、偏移、最小值/最大值、数据类型、DB号
- `RecipeGroup`: 组名、描述、参数列表、参数计数
- `RecipeRecord`: ID、名称、描述、产品代码、作者、状态、时间戳、版本、标签、分类、默认DB、分组
- `RecipeMeta`: 配方列表元数据（快速读取，不完整反序列化）
- `RecipeVersionSnapshot`: 版本快照元数据

#### 2. 配方服务 (`server/recipeManager.ts`)

**CRUD**:
- `getAllRecipes()` — 快速 JSON 元数据读取（只解析头字段）
- `loadRecipe(id)` — 完整加载
- `saveRecipe(recipe)` — 保存 + 自动 +1 版本 + 创建版本快照
- `deleteRecipe(id)` — 删除配方 + 版本历史
- `copyRecipe(sourceId, newName)` — 深拷贝（含分组和参数）

**版本管理**:
- 每次保存自动创建版本快照 (`data/recipes/_versions/<id>/v{N}.json`)
- `getVersionHistory(recipeId)` — 版本历史列表
- `loadRecipeVersion(recipeId, version)` — 加载指定版本
- `restoreVersion(recipeId, version)` — 恢复版本（创建新版本）

**CSV 导出/导入**:
- `exportToCsv(recipe)` — 导出所有参数为 CSV
- `importFromCsv(csvText, targetGroup?)` — 从 CSV 导入参数
- 兼容 tab 分隔和逗号分隔
- 自动检测 UTF-8 BOM / ANSI 编码（Node 端已通过 Buffer 处理）

#### 3. 配方 API (`server/index.ts`)

| Method | Route | 功能 |
|--------|-------|------|
| GET | `/api/recipe` | 配方列表 |
| GET | `/api/recipe/:id` | 获取单个配方 |
| POST | `/api/recipe` | 创建配方 |
| PUT | `/api/recipe/:id` | 更新配方 |
| DELETE | `/api/recipe/:id` | 删除配方 |
| POST | `/api/recipe/:id/copy` | 复制配方 |
| GET | `/api/recipe/:id/versions` | 版本历史 |
| GET | `/api/recipe/:id/versions/:version` | 加载指定版本 |
| POST | `/api/recipe/:id/restore/:version` | 恢复版本 |
| GET | `/api/recipe/:id/export-csv` | 导出 CSV |
| POST | `/api/recipe/:id/import-csv` | 导入 CSV |
| POST | `/api/recipe/:id/apply` | 下载到 PLC |
| POST | `/api/recipe/snapshot` | 从当前 PLC 值创建配方 |

#### 4. 前端组件 (`src/components/RecipePanel.tsx`)

**左栏 (配方列表, 300px)**:
- 标题栏「配方列表」
- 搜索框（搜索名称/描述/产品代码/标签）
- 分类下拉过滤
- 「新建配方」按钮
- 配方列表项: 名称 + 分类标签 + 版本号 / 产品代码 + 作者 + 参数数 + 状态标签 / 修改时间
- 底部: 复制按钮 + 刷新按钮 + 配方计数
- **版本历史折叠面板**: 版本列表(v1/v2/...) + 快照时间 +「恢复」按钮

**右栏 (配方编辑器)**:
- Header: 配方名称 + 版本号 + 删除按钮
- **元数据区域**:
  - 行1: 配方名称 / 产品代码
  - 行2: 操作人 / 配方状态(草稿/使用中/已归档) / 默认DB
  - 行3: 描述 / 标签(逗号分隔)
- **参数组 Tab 栏**: 水平 Tab（组名+参数数） + 添加组 + 删除组
- **参数工具栏**: 搜索框 / PLC下载 / PLC上传 / +参数 / −参数 / CSV导入 / CSV导出
- **参数 DataGrid** (可编辑 <input>): # / 参数名 / 值 / 单位 / 地址 / 缩放 / 偏移 / 数据类型(下拉) / DB
- **Footer**: PLC连接状态灯 + 状态文本 +「保存配方」按钮

---

## 文件变更清单

| 文件 | 变更 | 说明 |
|------|------|------|
| `shared/types.ts` | 修改 | 添加 Alarm + Recipe 类型定义 |
| `server/alarmEngine.ts` | 重写 | 完整报警引擎 (\~400行) |
| `server/recipeManager.ts` | 重写 | 完整配方服务 (\~350行) |
| `server/index.ts` | 修改 | 扩展报警+配方路由 (\~+200行) |
| `src/components/AlarmPanel.tsx` | 重写 | 报警面板 (\~550行) |
| `src/components/RecipePanel.tsx` | 重写 | 配方面板 (\~500行) |
| `src/App.tsx` | 修改 | 调整 RecipePanel 调用 |
| `src/App.css` | 修改 | 添加报警+配方样式 (\~400行) |
| `vite.config.ts` | 修改 | 添加 @altara 到 external |
| `pnpm-workspace.yaml` | 修正 | 添加 packages 字段修复 pnpm install |

---

## 已知问题

1. **中文编码**: 终端测试时 GBK 编码会报错，服务端已正确处理 UTF-8
2. **CSV 导出文件名的编码**: `Content-Disposition` header 中中文文件名需使用 `filename*=UTF-8''` 格式
3. **报警引擎只在服务端轮询时自动触发**, 前端需通过 SSE 或轮询获取报警数据
4. **前端报警面板的 SSE 数据驱动**尚未集成（当前用 3 秒轮询刷新报警列表和统计）
5. **配方 PLC 上传功能**需要在 S7 连接建立后才能工作，当前 UI 按钮已预留
