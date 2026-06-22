# add-wpf-sharp7-command

生成 S7Service 读取/写入代码片段。

## 用法

自然语言描述 PLC 地址和数据需求，Skill 生成对应代码。

## 模板

### 读取字节
```csharp
byte? val = _plc.ReadByte(area, byteAddress, dbNumber);
```

### 写入字节
```csharp
int result = _plc.WriteByte(area, byteAddress, value, dbNumber);
```

### 读取 DB 变量（解码为指定类型）
参考 `S7Service` 中的 `ReadDbValue` 模式，及 `SiemensDataTypes` 的 `TryResolve` 获取类型大小和对齐。

### 轮询注册
将地址添加到 `PollingScheduler.Config` 中，事件驱动更新 UI。

### 参考编号
- 区域常量：`S7Service.AreaI` (PE), `AreaQ` (PA), `AreaM` (MK), `AreaDB` (DB)
- 数据类型字典：`Models.SiemensDataTypes.Known`
