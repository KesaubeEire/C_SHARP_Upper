# 报警与配方系统 — 测试标准与流程

## 1. 测试层级

| 层级 | 范围 | 工具 |
|------|------|------|
| L1 单元测试 | 纯函数：alarmEngine.ts, recipeManager.ts 每个导出函数 | vitest |
| L2 API 测试 | Express 路由：所有 `/api/recipe/*` 和 `/api/alarm/*` 端点 | vitest + supertest |
| L3 编码测试 | CSV 导入导出：UTF-8 BOM、中文、特殊字符、分隔符 | vitest |
| L4 端到端测试 | 前端页面加载、CRUD 操作流程、CSV 上传下载 | 人工浏览器验证 |

## 2. 单元测试覆盖标准（L1）

### recipeManager.ts — 必须覆盖：
- `getAllRecipes()`: 空目录、含文件、损坏文件不崩溃
- `loadRecipe()`: 存在/不存在/损坏
- `saveRecipe()`: 新建保存、版本号自增、modifiedAt 更新、自动快照
- `deleteRecipe()`: 删除存在/不存在的配方
- `copyRecipe()`: 完整复制、新 ID、初始版本=1
- `getVersionHistory()`: 无版本/有版本
- `loadRecipeVersion()`: 存在/不存在
- `restoreVersion()`: 恢复元数据/参数组
- `exportToCsv()`: 含逗号/引号/换行的值正确转义
- `importFromCsv()`: 标准 CSV、Tab 分隔、带 BOM、空文件、中文
- `readCsvFileWithAutoDetect()`: 有 BOM/无 BOM

### alarmEngine.ts — 必须覆盖：
- 规则 CRUD: `addRule/removeRule/updateRule/getRules`
- `checkAlarms()`: 触发、死区、OnDelay、OffDelay、恢复
- `acknowledgeAlarm/acknowledgeAll`
- `shelveAlarm/unshelveAlarm`: 搁置到期自动恢复
- `addComment`
- `clearAll/getAlarms/getActiveAlarms/getShelvedAlarms/getAlarmHistory`
- `getStatistics`: 各计数器正确
- CSV: `exportAlarmsCsv/exportRulesCsv/importRulesCsv`
- 条件逻辑: High/Low/NotEqual/Digital/RateOfChange + 向后兼容 condition

## 3. API 集成测试标准（L2）

每个端点测试：
- 正常请求 → 200 + 正确 JSON 结构
- 参数缺失 → 400 + error 字段
- 资源不存在 → 404 + error 字段
- 空数据 → 不会崩溃

## 4. 编码测试标准（L3）

- CSV 含中文 → 保存后重新导入，中文不变
- CSV 含 `"` `,` `\n` → 正确转义与还原
- Tab 分隔文件 → 自动检测
- UTF-8 BOM → 自动去除
- 数字精度 → 浮点不丢失

## 5. 验收标准

- L1/L2/L3 全部通过：✅ 继续
- 发现 Bug：先修后补测
- L4 前端验证：CRUD 正常、CSV 下载无乱码、参数编辑保存正常
