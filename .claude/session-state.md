# 教学会话状态（断点恢复用）

最后更新：2026-06-13

## 当前进度

- ✅ 第1课：工业上位机系统全景架构 — 已学完
- ✅ 第2课：Modbus RTU 深度实践 — 已学完（概念已讲完，代码未落地）
- ⏭️ 下一步：开始写代码 或 第3课

## 教学偏好（NOTES.md 已记录）

1. 实战驱动，理论够用就行
2. 面试导向—每学一个东西，想"面试怎么讲"和"值多少钱"
3. 中文教学，术语保留英文方便面试
4. ✅ **所有课程 HTML 必须深色模式**
5. 喜欢先看全貌/架构图，再深入细节
6. 有不清楚的概念会追问，要沉淀到课程附录里

## 知识掌握情况

### 用户已掌握的
- C# 基础尚可，高级语法不太熟
- WinForms 基础使用没问题
- Modbus RTU 概念清楚：帧结构、CRC、串口、地址
- Modbus TCP 理论知道但实践不足
- 了解半双工/全双工/单工区别
- 理解生产者-消费者模式（概念上）
- 理解了 AutoResetEvent 的作用（等待/通知机制）
- 了解了 CAS 无锁算法概念
- 了解了退避策略（指数退避）
- 了解了 ManualResetEvent 与 AutoResetEvent 区别

### TEST_101 项目当前状态
- ModbusProtocol.cs：协议层完善，组装 PDU/RTU/TCP 帧，解析响应
- ModbusTransport.cs：传输层，串口/TCP 管理，事件驱动接收
- ModbusForm.cs：UI 层，按钮→发送→事件回调模式
- 缺少：业务逻辑层、数据层、命令队列、设备抽象

### 第2课待落地的代码
- ModbusPollingService.cs（生产者-消费者队列）
- DeviceState.cs（设备状态管理+退避）
- 改造 Transport：加 SendAndReadSync 同步读方法
- 改造 Form：集成轮询服务

## 已安装的 Skills

- Matt Pocock 29 个 skills（1. 已配置 Local markdown 问题追踪器；2. 默认 triage 标签；3. 单上下文领域文档配置）

## 记忆（需重建）

### dark-mode-teach-lessons
- 所有 /teach 课程 HTML 必须使用深色模式（背景 #0f0f1a，文字 #e0e0e0）
- 已经修正 0001，后续全部深色

## 教学文件位置

- `.claude/skills/teach/MISSION.md` — 学习目标
- `.claude/skills/teach/NOTES.md` — 教学偏好
- `.claude/skills/teach/lessons/0001-*.html` — 第1课
- `.claude/skills/teach/lessons/0002-*.html` — 第2课（含附录）
- `.claude/skills/teach/learning-records/0001-*.md` — 学习记录1
- `.claude/skills/teach/learning-records/0002-*.md` — 学习记录2

## 推荐恢复命令

在 Windows 上拉取后，输入以下命令继续：
```
/teach 继续学习 C# WinForms Modbus 上位机开发
```
