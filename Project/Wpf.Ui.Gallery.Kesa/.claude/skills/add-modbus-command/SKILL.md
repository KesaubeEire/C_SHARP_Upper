# add-modbus-command

生成 Modbus 读取/写入代码片段。

## 用法

自然语言描述 Modbus 地址和数据需求，Skill 生成对应代码。

## 模板

### Modbus 寻址格式

| 类型 | 前缀 | 地址范围 | 数据单位 |
|------|------|----------|----------|
| 线圈 | 0x | 000001–065536 | 位 (bool) |
| 离散输入 | 1x | 100001–165536 | 位 (bool) |
| 输入寄存器 | 3x | 300001–365536 | 字 (16-bit) |
| 保持寄存器 | 4x | 400001–465536 | 字 (16-bit) |

### ModbusProtocol — 帧组装
```csharp
// 读保持寄存器 (功能码 0x03)
byte[] frame = ModbusProtocol.BuildReadRequest(
    slaveId: 1,
    functionCode: 0x03,
    startAddress: 0,
    quantity: 10);

// 写单个线圈 (功能码 0x05)
byte[] frame = ModbusProtocol.BuildWriteRequest(
    slaveId: 1,
    functionCode: 0x05,
    address: 100,
    value: true);
```

### ModbusProtocol — 响应解析
```csharp
// 解析读寄存器响应
ushort[] values = ModbusProtocol.ParseReadResponse(response, functionCode: 0x03);
// values 数组包含读到的寄存器值
```

### ModbusTransport — 连接管理
```csharp
// RTU (串口)
var transport = new ModbusTransport();
transport.Connect("COM3", 9600, 8, StopBits.One, Parity.None);

// TCP
transport.Connect("192.168.1.100", 502);
```

### 约定

- 协议层（ModbusProtocol）**不含 IO**，只做帧组装/解析
- 传输层（ModbusTransport）封装串口/TCP 细节
- 所有异步方法使用 `Async` 后缀
- CRC 校验自动附加/验证
