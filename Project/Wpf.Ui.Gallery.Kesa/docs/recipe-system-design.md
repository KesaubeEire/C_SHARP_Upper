# 配方管理系统设计与实现说明

> 最后更新：2026-06-26
> 范围：仅配方模块（Models / Services / ViewModels / Views），不影响其他页面

---

## 一、改造动机

对标开源上位机（DotNetCore SCADA、SwjFramework、NormalizingApp 等）的配方管理功能，
弥补原配方模块的不足：

| 原功能 | 原状态 | 改造后 |
|--------|--------|--------|
| 参数平铺无分组 | ❌ | ✅ 参数组（RecipeGroup） |
| 无元数据（产品代码/操作人） | ❌ | ✅ ProductCode / Author / Status |
| 无版本历史 | ❌ | ✅ 自动快照 + 版本浏览/恢复 |
| 无导入导出 | ❌ | ✅ CSV 导入/导出（支持 Tab/逗号分隔、自动检测编码） |
| 无 PLC 通信 | ❌ | ✅ 一键下载/上传（Sharp7） |
| 无搜索过滤 | ❌ | ✅ 按名称/产品代码/标签搜索 |
| ViewModel 未继承基类 | ❌ | ✅ 继承 `ViewModel` + `INavigationAware` |

---

## 二、数据模型

```
RecipeRecord
├── Id / Name / Description
├── ProductCode / Author             ← 新增
├── Status (Draft / Active / Archived)  ← 新增
├── CreatedAt / ModifiedAt / Version
└── Groups[]                         ← 新增（替代平铺 Parameters）
      ├── RecipeGroup
      │   ├── Name / Description
      │   └── Parameters[]
      │         ├── Name / Value / Unit / Address
      │         ├── DataType (PlcDataType 枚举)  ← 新增（替代字符串）
      │         ├── Scale / Offset
      │         ├── MinValue / MaxValue
      │         └── RawValue          ← 计算属性（PLC 原始值）
      └── RecipeGroup ...
```

### PlcDataType 枚举

`Real` / `Int` / `DInt` / `UInt` / `UDInt` / `Word` / `DWord` / `Byte` / `USInt` / `SInt` / `Bool`

### 文件清单

| 文件 | 类型 |
|------|------|
| `Models/Plc/PlcDataType.cs` | **新建**（PLC 数据类型枚举） |
| `Models/Plc/RecipeStatus.cs` | **新建**（配方状态枚举） |
| `Models/Plc/RecipeGroup.cs` | **新建**（参数组模型） |
| `Models/Plc/RecipeVersionSnapshot.cs` | **新建**（版本快照元数据） |
| `Models/Plc/RecipeParameter.cs` | 修改（+DataType 枚举 + `PlcDataTypeStr` 桥接旧 JSON） |
| `Models/Plc/RecipeRecord.cs` | 修改（+Groups / ProductCode / Author / Status） |
| `Helpers/IntGreaterThanZeroToVisibilityConverter.cs` | **新建** |

### 向后兼容

旧 JSON 格式只有 `Parameters` 无 `Groups`：
- `RecipeRecord.Parameters` setter 检测旧格式并自动迁移到 Groups
- 第一个 Group 命名为"参数组1"，包含所有旧参数
- 保存时自动使用新格式（Groups）

---

## 三、服务层架构

```
View (RecipePage.xaml)
  └─ ViewModel (RecipeViewModel: ViewModel + INavigationAware)
       └─ RecipeService (CRUD + 版本 + CSV + PLC)
            ├─ JSON 文件持久化 (%APPDATA%/Kesa_PLC_TEST/recipes/)
            ├─ 版本快照 (%APPDATA%/Kesa_PLC_TEST/recipes/_versions/)
            ├─ CSV 导入/导出（自动检测 Tab/逗号分隔、UTF-8/GBK 编码）
            └─ S7Service (Sharp7) → PLC DB 块读写
```

### 持久化路径

所有运行时数据统一存储在 **Roaming AppData**：

```
%APPDATA%\Kesa_PLC_TEST\
├── recipes/                  ← 配方 JSON 文件
│   └── _versions/{id}/      ← 版本历史快照
├── kesa_config.json          ← 应用配置
├── alarms.json               ← 报警记录
├── rules.json                ← 轮询规则
└── default-rules.json        ← 默认轮询规则
```

不依赖 `bin/` 目录，`dotnet clean` / `dotnet build` 不会丢失数据。

### RecipeService API

| 方法 | 说明 |
|------|------|
| `GetAllRecipes()` | 列表（返回 `RecipeMeta` 轻量摘要） |
| `LoadRecipe(id)` | 加载单个配方（完整数据） |
| `SaveRecipe(recipe)` | 保存（自动版本快照 + 版本号+1） |
| `DeleteRecipe(id)` | 删除（含版本历史目录） |
| `CopyRecipe(sourceId, newName)` | 复制配方（深拷贝参数） |
| `GetVersionHistory(recipeId)` | 版本历史列表 |
| `LoadRecipeVersion(recipeId, version)` | 加载历史版本 |
| `RestoreVersion(recipeId, version)` | 恢复历史版本（快照→覆盖→新版本） |
| `ExportToCsv(recipe)` | 导出 CSV（UTF-8 BOM） |
| `ImportFromCsv(csvText)` | 从 CSV 导入（自动检测 Tab/逗号分隔） |
| `DownloadToPlc(recipe, defaultDb)` | 下载到 PLC DB 块 |
| `UploadFromPlc(recipe, defaultDb)` | 从 PLC DB 块上传 |

### CSV 编码处理

- **导出**：`File.WriteAllText(..., Encoding.UTF8)` → 写入 UTF-8 BOM，Excel 正确识别
- **导入**：自动检测编码
  - 有 UTF-8 BOM（`0xEF BB BF`）→ UTF-8 解码
  - 无 BOM → Win32 `MultiByteToWideChar(CP_ACP)` 解码（中文 Windows = GBK）
- **分隔符**：根据表头行自动检测 Tab（`\t`）或逗号（`,`）

---

## 四、ViewModel 设计

继承 `ViewModel` 基类（`INavigationAware`），注入 `RecipeService` + `S7Service`。

### 状态属性

| 属性 | 用途 |
|------|------|
| `Recipes` / `FilteredRecipes` | 配方列表 + 过滤 |
| `RecipeSearchText` / `SelectedCategoryFilter` | 搜索 + 分类过滤 |
| `SelectedRecipe` | 当前选中的配方摘要 |
| `CurrentRecipeName / Description / Category / Tags` | 配方头字段 |
| `CurrentProductCode / CurrentAuthor / CurrentStatus` | 元数据字段（新增） |
| `CurrentVersion` | 版本号 |
| `HasRecipeSelected` | 是否有选中配方 |
| `DefaultDbNumber` | PLC 默认 DB |
| `RecipeGroups` / `SelectedGroup` | 参数组标签栏（新增） |
| `CurrentGroupParameters` / `SelectedParameter` | 当前组参数网格（新增） |
| `HasGroups` / `HasGroupSelected` | 组状态 |
| `ParameterSearchText` | 组内参数搜索 |
| `StatusText` / `PlcStatusText` / `IsPlcConnected` | PLC 状态 |
| `VersionHistoryItems` / `IsVersionHistoryVisible` / `SelectedVersion` | 版本历史面板（新增） |

### 命令

| 命令 | 触发条件 |
|------|---------|
| `NewRecipe / SaveRecipe / DeleteRecipe` | 基础 CRUD |
| `CopyRecipe / RefreshRecipeList` | 复制 / 刷新 |
| `AddGroup / RemoveGroup` | 参数组管理（新增） |
| `AddParameter / RemoveParameter / DuplicateParameter` | 参数管理 |
| `ExportCsv / ImportCsv` | CSV 导入导出 |
| `DownloadToPlc / UploadFromPlc` | PLC 上下载（需 `HasRecipeSelected`） |
| `ShowVersionHistory / RestoreVersion` | 版本历史（新增） |
| `ReloadCurrentRecipe` | 从磁盘重新加载（新增） |

### 文件

| 文件 | 类型 |
|------|------|
| `ViewModels/Pages/Plc/RecipeViewModel.cs` | 重写（继承 `ViewModel` 基类） |

---

## 五、UI 布局

```
┌───────────── 300px ────────────┬──┬────────────────── * ──────────────────┐
│        配方列表                  │  │          配方编辑器                   │
│  [搜索框]                       │  │  名称: [____]  产品: [____]          │
│  [分类过滤器]                    │  │  操作人: [____]  状态:[▼]  DB:[__]  │
│  [+新建配方]                    │  │  描述: [________] 标签: [________]    │
│  ┌─────────────────────────┐   │  │                                     │
│  │ 配方A       类别  v3    │   │  │  参数组: [加热段] [保压段] [冷却段] [+组] │
│  │ PC-001  操作人          │   │  │  ┌──────┬────┬──┬──┬────┬──┬──┐    │
│  │ 06-26 14:30             │   │  │  │参数名│ 值 │单位│地址│缩放│偏移│DB│
│  │ 配方B       类别  v1   │   │  │  ├──────┼────┼──┼──┼────┼──┼──┤    │
│  │ ...                     │   │  │  │滑台  │150 │°C│ 0│1.0 │ 0│ 6│    │
│  └─────────────────────────┘   │  │  └──────┴────┴──┴──┴────┴──┴──┘    │
│  [3 个配方] [复制] [刷新]     │  │  [↑下载] [上传] [+参数] [CSV↓][CSV↑]  │
│  [▼ 版本历史]                  │  │  [复制参数]                            │
│  │ v3  06-26 14:30           │  │  ●已连接  就绪               [保存配方] │
│  │ v2  06-26 13:00           │  │                                      │
│  │ v1  06-26 10:00           │  │                                      │
│  └─────────────────────────┘   │  │                                      │
└────────────────────────────────┴──┴──────────────────────────────────────┘
```

### 左侧面板

- 配方列表（搜索、分类过滤、新建）
- 列表项显示：名称 + 分类标签 + 版本 + 产品代码 + 操作人 + 修改时间
- 底部：配方计数、复制、刷新
- **版本历史**（可折叠）：点击展开显示版本列表 + "恢复此版本" 按钮

### 右侧面板 — 配方编辑器

- **元数据**：名称 / 产品代码 / 操作人 / 状态（Draft/Active/Archived）/ 默认 DB / 描述 / 标签
- **参数组标签栏**：水平标签组，显示组名 + 参数计数，可添加/删除组
- **参数工具栏**：搜索 / PLC 下载/上传 / 参数增删 / CSV 导入导出
- **参数 DataGrid**：Name / Value / Unit / Address / Scale / Offset / DataType / DB
- **底部状态栏**：PLC 连接指示 + 状态文本 + "保存配方" 按钮

### 文件

| 文件 | 类型 |
|------|------|
| `Views/Pages/Plc/RecipePage.xaml` | 重写（双栏 + 组标签 + DataGrid + 版本历史面板） |
| `Views/Pages/Plc/RecipePage.xaml.cs` | 未改（`[GalleryPage]` 保留） |

---

## 六、未包含/待定

以下功能本次未实现，但已预留接口：

- **Excel 导入导出**（当前是 CSV，可改用 MiniExcel NuGet 包）
- **配方执行状态机**（步骤顺序执行 + 条件跳转）
- **执行实时监控**（LiveCharts 趋势曲线显示实际值 vs 目标值）
- **权限控制**（操作员只读 vs 工程师可编辑）
- **审计日志**（操作记录持久化）
- **S7 DB 映射自动生成**（从 RecipeParameter 自动生成 DB 结构）
- **"应用配方" 一键操作**（保存 + 下载到 PLC + 状态标记 Active）
