# C# 编码约定（摘自 .editorconfig 策略）

## 命名规则

| 作用域 | 样式 | 示例 |
|--------|------|------|
| 私有字段 | `_camelCase` | `_clientLock`, `_busy` |
| 参数 | `camelCase` | `byteAddress`, `areaCode` |
| 本地变量 | `camelCase` | `result`, `norm` |
| 接口 | `I` + PascalCase | `IDataProvider` |
| 类型参数 | `T` + PascalCase | `TResult` |
| 公开/保护成员 (属性、方法、事件) | PascalCase | `StartPolling`, `DataUpdated` |
| 类型 (class/struct/enum) | PascalCase | `S7Service`, `PollingConfig` |
| 常量 | PascalCase | `AreaI`, `DefaultPort` |

## 代码风格

- **File-scoped namespaces**: `namespace TestWpf;` (不带大括号)
- **var 使用**: 内置类型（int, bool 等）和类型明显时用 var；其他情况显式类型
- **表达式体**: 单行表达式允许使用 `=>` 语法
- **模式匹配**: 优先于 `is` + 强制转换 / `as` + null 检查
- **空值处理**: 优先 `??`、`?.` 而非 if 判断

## 静态分析策略

- **TreatWarningsAsErrors**: 所有代码风格警告视为构建错误
- 配置在 `Directory.Build.props`：Meziantou.Analyzer + SonarAnalyzer.CSharp 两个第三方分析器
- `EnforceCodeStyleInBuild`: 在构建时强制执行代码风格
- `CS1591` (缺少 XML 文档注释) 被抑制，无需在每个成员上添加文档注释
- `CA1852` / `MA0053` (密封类) 仅 suggestion，不影响构建
