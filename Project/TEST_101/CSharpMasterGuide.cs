// ╔══════════════════════════════════════════════════════════════════════════╗
// ║                                                                        ║
// ║          🎓 C# 语法全攻略 — 从入门到进阶的系统化学习指南              ║
// ║                                                                        ║
// ║   本文件覆盖 C# 所有核心语法，每个概念都结合你的 Modbus 项目来讲解    ║
// ║   运行方法：在 ModbusForm 里加一个按钮，调用对应章节的 Run 方法       ║
// ║                                                                        ║
// ║   21 章，按学习顺序排列，建议从第 1 章开始顺序阅读                    ║
// ║                                                                        ║
// ╚══════════════════════════════════════════════════════════════════════════╝
//
//  📑 目录
//  ────────────────────────────────────────────────────────────────────────
//   第 1 章  变量、类型与表达式          ← 一切的基础
//   第 2 章  字符串处理                  ← 你天天在用，但可能只用了 10%
//   第 3 章  控制流                      ← if / switch / 循环
//   第 4 章  方法                        ← 函数的方方面面
//   第 5 章  类与对象                    ← OOP 起点
//   第 6 章  继承与多态                  ← OOP 核心
//   第 7 章  接口                        ← OOP 最灵活的武器
//   第 8 章  枚举与结构体                ← 值类型的两员大将
//   第 9 章  异常处理                    ← 你的项目到处 try/catch
//   第 10 章 泛型                        ← 你已经在用 List<T>，知道原理吗？
//   第 11 章 常用集合                    ← Array / List / Dictionary / Queue
//   第 12 章 委托、Lambda 与事件        ← 你的 CSharpConceptsDemo 的升级版
//   第 13 章 LINQ                        ← 数据查询的瑞士军刀
//   第 14 章 异步编程 async/await        ← 你的 TCP 收发能简化很多
//   第 15 章 模式匹配                    ← C# 现代语法的核心
//   第 16 章 记录类型与元组              ← 轻量级数据载体
//   第 17 章 可空类型与空安全            ← 那些问号 ? 的真正含义
//   第 18 章 扩展方法                    ← 给别人的类"加方法"
//   第 19 章 特性 (Attribute)            ← 代码里的"标签"
//   第 20 章 文件 I/O 与 JSON 序列化     ← 你的 InputHistoryManager 原理
//   第 21 章 多线程与并发                ← lock / Task / CancellationToken
//  ────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TEST_101
{
    /// <summary>
    /// C# 语法全攻略 — 21 章系统化学习。
    /// 每个章节都是独立的静态方法，可以直接运行看输出。
    /// </summary>
    public static class CSharpMasterGuide
    {
        // 辅助方法：章节标题
        private static void Title(string chapter, string topic)
        {
            Console.WriteLine();
            Console.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  {chapter,-56}║");
            Console.WriteLine($"║  {topic,-56}║");
            Console.WriteLine($"╚═══════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        private static void SubTitle(string text)
        {
            Console.WriteLine($"  ── {text} ──");
        }

        private static void Note(string text)
        {
            Console.WriteLine($"    💡 {text}");
        }

        private static void ProjectLink(string text)
        {
            Console.WriteLine($"    📌 项目对应：{text}");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 1 章：变量、类型与表达式
        // ══════════════════════════════════════════════════════════════════

        public static void Ch01_TypesAndVariables()
        {
            Title("第 1 章", "变量、类型与表达式");

            // ── 1.1 基本数据类型 ──
            SubTitle("1.1 基本数据类型");
            /*
             *  C# 的类型系统就像 Modbus 寄存器的分类：
             *
             *  ┌───────────────┬──────────┬──────────────────────┐
             *  │ C# 类型       │ 大小     │ 对应 Modbus 概念     │
             *  ├───────────────┼──────────┼──────────────────────┤
             *  │ byte          │ 1 字节   │ 单个字节 0x01        │
             *  │ ushort        │ 2 字节   │ 一个寄存器 0x000A    │
             *  │ int           │ 4 字节   │ 两个寄存器拼起来     │
             *  │ float         │ 4 字节   │ 32位浮点（两个寄存器）│
             *  │ double        │ 8 字节   │ 64位浮点             │
             *  │ bool          │ 1 字节   │ 一个线圈 ON/OFF      │
             *  │ char          │ 2 字节   │ Unicode 字符         │
             *  │ string        │ 不定长   │ 一串文本             │
             *  └───────────────┴──────────┴──────────────────────┘
             */

            // 整数类型
            byte funcCode = 0x03;              // 你的功能码就是 byte
            ushort registerValue = 12345;      // Modbus 寄存器就是 ushort
            int bigNumber = 1_000_000;         // 下划线分隔符，提高可读性
            long veryBig = 9_999_999_999L;     // L 后缀表示 long

            // 浮点类型
            float temperature = 25.5f;         // f 后缀表示 float
            double precise = 3.14159265358979;  // 默认小数是 double
            decimal money = 19.99m;            // m 后缀表示 decimal（金融精度）

            // 其他
            bool isTcpMode = true;
            char separator = ' ';
            string deviceName = "Modbus设备#1";

            Console.WriteLine($"  byte:   {funcCode} (0x{funcCode:X2})");
            Console.WriteLine($"  ushort: {registerValue}");
            Console.WriteLine($"  int:    {bigNumber}");
            Console.WriteLine($"  float:  {temperature}");
            Console.WriteLine($"  double: {precise}");
            Console.WriteLine($"  bool:   {isTcpMode}");
            Console.WriteLine($"  string: {deviceName}");

            ProjectLink("ModbusProtocol.cs 里的 byte[] pdu = new byte[6] 就是在操作字节");

            // ── 1.2 var 与类型推断 ──
            SubTitle("1.2 var — 让编译器帮你推断类型");
            /*
             *  var 不是"没有类型"，而是"让编译器根据右边推断类型"
             *  就像你说"那个修空调的"——听的人知道你指的是"王师傅"
             */

            var port = "COM3";          // 编译器推断为 string
            var baudRate = 9600;        // 编译器推断为 int
            var frame = new byte[] { 0x01, 0x03, 0x00, 0x00 }; // byte[]

            Console.WriteLine($"  var port 的类型是: {port.GetType().Name}");     // String
            Console.WriteLine($"  var baudRate 的类型是: {baudRate.GetType().Name}"); // Int32

            // ── 1.3 常量 const 与只读 readonly ──
            SubTitle("1.3 const 与 readonly 的区别");
            /*
             *  const  = 编译时就确定的值，永远不变（像数学常数 π）
             *  readonly = 运行时赋值一次，之后不变（像你的身份证号）
             *
             *  你的 ModbusProtocol.cs 里就用了 const：
             *    public const int MBAP_HEADER_SIZE = 7;
             *    public const int MAX_REGISTERS_PER_READ = 125;
             */

            const double Pi = 3.14159;  // 编译时确定
            // readonly DateTime _startTime = DateTime.Now;
            // ↑ readonly 只能用在类的字段上，不能用在方法内的局部变量
            // 正确用法见下方示例：
            Console.WriteLine($"  const Pi = {Pi}");
            Console.WriteLine($"  (readonly 只能在类字段中使用，这里只是演示语法)");

            // ── 1.4 类型转换 ──
            SubTitle("1.4 类型转换：隐式、显式、安全转换");
            /*
             *  三种转换方式：
             *
             *  1. 隐式转换（自动，不会丢数据）：小 → 大
             *     int → long，float → double
             *
             *  2. 显式转换（手动，可能丢数据）：大 → 小
             *     double → int，long → int
             *     用 (目标类型) 语法
             *
             *  3. 安全转换（try-parse 模式）
             *     int.TryParse()，失败返回 false 而不是炸掉
             */

            // 隐式：ushort → int（安全，不丢数据）
            ushort regVal = 50000;
            int intVal = regVal;   // 自动转换
            Console.WriteLine($"  隐式: ushort {regVal} → int {intVal}");

            // 显式：double → int（可能丢小数）
            double temp = 25.7;
            int truncated = (int)temp;  // 结果是 25，小数丢了
            Console.WriteLine($"  显式: double {temp} → int {truncated}");

            // 你的项目中的实际转换（ModbusForm.cs 第 410 行）
            // byte.Parse(box_dev.Text.Trim())  —— 字符串→byte
            // ushort.Parse(addrText)            —— 字符串→ushort
            // Convert.ToUInt16("FF", 16)        —— 十六进制字符串→ushort

            // 安全转换：TryParse
            string userInput = "abc";
            if (int.TryParse(userInput, out int parsed))
                Console.WriteLine($"  解析成功: {parsed}");
            else
                Console.WriteLine($"  解析失败: \"{userInput}\" 不是有效数字");

            userInput = "42";
            if (int.TryParse(userInput, out parsed))
                Console.WriteLine($"  解析成功: {parsed}");

            ProjectLink("ModbusForm.cs btn_read_Click 里的 ushort.Parse() 可以换成 TryParse 更安全");

            // ── 1.5 运算符 ──
            SubTitle("1.5 运算符速查");
            /*
             *  算术：  +  -  *  /  %（取余）
             *  比较：  ==  !=  <  >  <=  >=
             *  逻辑：  &&（与）  ||（或）  !（非）
             *  位运算：&（与）  |（或）  ^（异或）  ~（取反）  <<（左移）  >>（右移）
             *  赋值：  =  +=  -=  *=  /=  %=
             *  其他：  ??（空合并）  ?.（空条件）  ?：（三元）
             */

            // 位运算 —— 你天天在用！Modbus 协议全是字节位操作
            byte address = 0x01;
            byte highByte = (byte)(100 >> 8);    // 右移 8 位取高字节
            byte lowByte = (byte)(100 & 0xFF);   // AND 0xFF 取低字节
            Console.WriteLine($"  100 的高字节: 0x{highByte:X2}, 低字节: 0x{lowByte:X2}");

            // 异常码判断 —— 你项目里的写法
            byte func = 0x83;  // 带异常标志的功能码
            bool isError = (func & 0x80) != 0;  // 第 7 位为 1 表示异常
            Console.WriteLine($"  功能码 0x{func:X2} 是异常码吗？{isError}");

            // 空合并运算符 ??
            string? maybeNull = null;
            string result = maybeNull ?? "默认值";  // 左边为 null 就用右边
            Console.WriteLine($"  null ?? \"默认值\" = \"{result}\"");

            // 三元运算符
            int temp2 = 35;
            string status = temp2 > 30 ? "高温" : "正常";
            Console.WriteLine($"  {temp2}°C → {status}");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 2 章：字符串处理
        // ══════════════════════════════════════════════════════════════════

        public static void Ch02_Strings()
        {
            Title("第 2 章", "字符串处理");

            // ── 2.1 字符串基础 ──
            SubTitle("2.1 创建与拼接");

            // 普通字符串
            string portName = "COM3";

            // 逐字字符串 @（不转义反斜杠）
            string path = @"C:\Users\mz199\Downloads\Code";

            // 字符串插值 $（最常用！）
            int baud = 9600;
            string msg = $"已打开 {portName}，{baud} 波特率";
            Console.WriteLine($"  插值: {msg}");

            // 你的项目里大量使用 $ 插值：
            // ModbusForm.cs:  lb_status.Text = $"检测到 {ports.Length} 个 COM 口";
            // ModbusForm.cs:  $"已打开 {drop_com.Text}，{drop_baud.Text} 波特率"

            // ── 2.2 常用方法 ──
            SubTitle("2.2 常用字符串方法");

            string hex = "01 03 00 00 00 0A";
            Console.WriteLine($"  原始:     \"{hex}\"");
            Console.WriteLine($"  Trim():   \"{hex.Trim()}\"");         // 去首尾空格
            Console.WriteLine($"  Replace:  \"{hex.Replace(" ", "-")}\""); // 替换
            Console.WriteLine($"  Split:    [{string.Join(", ", hex.Split(' '))}]"); // 分割
            Console.WriteLine($"  Contains: {hex.Contains("03")}");    // 是否包含
            Console.WriteLine($"  StartsWith: {hex.StartsWith("01")}"); // 是否开头
            Console.WriteLine($"  IndexOf:  {hex.IndexOf("00")}");     // 首次出现位置
            Console.WriteLine($"  ToUpper:  \"{hex.ToUpper()}\"");     // 转大写
            Console.WriteLine($"  Length:   {hex.Length}");             // 长度

            // 你项目里的实际用法：
            // ModbusForm.cs:
            //   string funcStr = drop_func.Text.Substring(0, 2);  // 截取前两个字符
            //   byte funcCode = byte.Parse(funcStr);
            //   addrText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)

            // ── 2.3 格式化 ──
            SubTitle("2.3 格式化：数字、日期、对齐");

            // 数字格式化
            ushort value = 255;
            Console.WriteLine($"  十六进制: 0x{value:X4}");      // 0x00FF
            Console.WriteLine($"  十进制:   {value:D5}");         // 00255
            Console.WriteLine($"  二进制:   {Convert.ToString(value, 2).PadLeft(16, '0')}");

            // 日期格式化
            DateTime now = DateTime.Now;
            Console.WriteLine($"  时间戳:   [{now:HH:mm:ss}]");     // 你的日志格式
            Console.WriteLine($"  完整日期: {now:yyyy-MM-dd HH:mm:ss}");

            // 对齐
            Console.WriteLine($"  右对齐:   [{value,10}]");   // 宽度 10，右对齐
            Console.WriteLine($"  左对齐:   [{value,-10}]");  // 宽度 10，左对齐

            // ── 2.4 StringBuilder ──
            SubTitle("2.4 StringBuilder — 大量拼接时用它");
            /*
             *  string 是不可变的——每次 += 都创建新对象。
             *  如果要在循环里拼接几百次，用 StringBuilder 快几十倍。
             *
             *  你的项目里 ShowLearnDialog() 就用了：
             *    var output = new System.Text.StringBuilder();
             */

            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++)
                sb.AppendLine($"  [{i}] 第 {i} 行日志");

            Console.WriteLine(sb.ToString());

            // ── 2.5 字符串与字节互转 ──
            SubTitle("2.5 字符串 ↔ 字节数组");

            // 十六进制字符串 → 字节数组（你经常需要）
            string hexStr = "01030000000A";

            // 方式一：逐对转换
            byte[] bytes = Enumerable.Range(0, hexStr.Length / 2)
                .Select(i => Convert.ToByte(hexStr.Substring(i * 2, 2), 16))
                .ToArray();
            Console.WriteLine($"  方式一: {BitConverter.ToString(bytes)}");

            // 方式二：.NET 5+ 内置方法（更简洁）
            bytes = Convert.FromHexString(hexStr);
            Console.WriteLine($"  方式二: {BitConverter.ToString(bytes)}");

            // 字节数组 → 十六进制字符串
            byte[] frame = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            string hexResult = BitConverter.ToString(frame).Replace("-", " ");
            Console.WriteLine($"  字节 → 十六进制: {hexResult}");

            ProjectLink("ModbusForm.cs 里 ColorizeHexFrame 用的就是 BitConverter.ToString");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 3 章：控制流
        // ══════════════════════════════════════════════════════════════════

        public static void Ch03_ControlFlow()
        {
            Title("第 3 章", "控制流：条件、循环、跳转");

            // ── 3.1 if / else if / else ──
            SubTitle("3.1 条件判断");

            byte funcCode = 0x03;
            if ((funcCode & 0x80) != 0)
                Console.WriteLine("  这是异常响应");
            else if (funcCode == 0x03 || funcCode == 0x04)
                Console.WriteLine("  这是读寄存器响应");
            else if (funcCode == 0x01 || funcCode == 0x02)
                Console.WriteLine("  这是读线圈响应");
            else
                Console.WriteLine("  其他功能码");

            // ── 3.2 switch 语句 ──
            SubTitle("3.2 switch 语句（传统写法）");

            switch (funcCode)
            {
                case 0x01:
                    Console.WriteLine("  01 读线圈");
                    break;
                case 0x02:
                    Console.WriteLine("  02 读离散输入");
                    break;
                case 0x03:
                    Console.WriteLine("  03 读保持寄存器");
                    break;
                case 0x04:
                    Console.WriteLine("  04 读输入寄存器");
                    break;
                default:
                    Console.WriteLine("  其他");
                    break;
            }

            // ── 3.3 switch 表达式（C# 8+ 现代写法）──
            SubTitle("3.3 switch 表达式 — 更简洁的写法");
            /*
             *  你项目里已经在用了！
             *  ModbusForm.cs 第 560 行：
             *    return funcCode switch
             *    {
             *        0x01 => Color.FromArgb(220, 238, 255),
             *        0x02 => Color.FromArgb(220, 255, 225),
             *        ...
             *    };
             */

            string funcName = funcCode switch
            {
                0x01 => "读线圈",
                0x02 => "读离散输入",
                0x03 => "读保持寄存器",
                0x04 => "读输入寄存器",
                0x05 => "写单线圈",
                0x06 => "写单寄存器",
                _ => $"未知(0x{funcCode:X2})"  // _ 是默认分支
            };
            Console.WriteLine($"  功能码 0x{funcCode:X2} = {funcName}");

            // ── 3.4 for 循环 ──
            SubTitle("3.4 for 循环");

            // 遍历 Modbus 寄存器值（模拟）
            ushort[] registers = { 100, 200, 300, 400, 500 };
            for (int i = 0; i < registers.Length; i++)
                Console.WriteLine($"  寄存器[{i}] = {registers[i]}");

            // ── 3.5 foreach 循环 ──
            SubTitle("3.5 foreach — 遍历集合");
            /*
             *  你项目里到处在用：
             *    foreach (string port in ports) drop_com.Items.Add(port);
             *    foreach (var bit in result.Bits) grid_result.Rows.Add(...);
             */

            foreach (ushort reg in registers)
                Console.WriteLine($"  寄存器值: {reg}");

            // ── 3.6 while 与 do-while ──
            SubTitle("3.6 while 循环");

            // TCP 接收循环的简化版
            int bytesReceived = 0;
            int expectedBytes = 6;
            while (bytesReceived < expectedBytes)
            {
                bytesReceived += 2; // 模拟每次收到 2 字节
                Console.WriteLine($"  已收 {bytesReceived}/{expectedBytes} 字节");
            }
            Console.WriteLine("  接收完成！");

            // ── 3.7 break / continue / return ──
            SubTitle("3.7 break / continue / return");
            /*
             *  break    = 立即退出当前循环
             *  continue = 跳过本次迭代，继续下一次
             *  return   = 退出整个方法
             */

            // break 示例：找到第一个异常值就停
            int[] values = { 10, 20, 30, -1, 40, 50 };
            Console.Write("  扫描异常值: ");
            foreach (int v in values)
            {
                if (v < 0)
                {
                    Console.WriteLine($"发现异常值 {v}，停止扫描");
                    break;
                }
                Console.Write($"{v} → ");
            }

            // continue 示例：跳过偶数
            Console.Write("  只打印奇数: ");
            for (int i = 1; i <= 10; i++)
            {
                if (i % 2 == 0) continue;  // 跳过偶数
                Console.Write($"{i} ");
            }
            Console.WriteLine();

            // ── 3.8 循环标签与嵌套 ──
            SubTitle("3.8 嵌套循环中的控制");

            // 模拟：遍历多个设备的多个寄存器
            string[] devices = { "温度传感器", "压力传感器", "转速传感器" };
            ushort[][] allData = {
                new ushort[] { 250, 251, 252 },
                new ushort[] { 101, 102, 103 },
                new ushort[] { 1400, 1450, 1500 }
            };

            // 用 goto 模拟多层跳出（不推荐，用函数封装更好）
            for (int dev = 0; dev < devices.Length; dev++)
            {
                for (int reg = 0; reg < allData[dev].Length; reg++)
                {
                    if (allData[dev][reg] > 1000)
                    {
                        Console.WriteLine($"  找到超限值: {devices[dev]}[{reg}] = {allData[dev][reg]}");
                        goto Found;  // 跳出所有嵌套循环
                    }
                }
            }
        Found:
            Console.WriteLine("  扫描结束");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 4 章：方法
        // ══════════════════════════════════════════════════════════════════

        public static void Ch04_Methods()
        {
            Title("第 4 章", "方法（函数）的方方面面");

            // ── 4.1 方法签名 ──
            SubTitle("4.1 方法的基本结构");
            /*
             *  返回类型  方法名(参数列表)
             *  {
             *      方法体;
             *      return 返回值;
             *  }
             *
             *  你项目里的典型方法：
             *    public static byte[] CalcCRC(byte[] data)        ← 返回 byte[]
             *    public static bool VerifyCRC(byte[] frame)       ← 返回 bool
             *    public void OpenSerial(string portName, ...)     ← 无返回值
             */

            // ── 4.2 参数的四种传递方式 ──
            SubTitle("4.2 参数传递：值/ref/out/in");

            // 值传递（默认）—— 副本，不影响原变量
            int original = 10;
            PassByValue(original);
            Console.WriteLine($"  值传递后: original = {original}"); // 还是 10

            // ref —— 传引用，方法内修改会影响原变量
            int refVal = 10;
            PassByRef(ref refVal);
            Console.WriteLine($"  ref 传递后: refVal = {refVal}"); // 变成 20

            // out —— 方法负责赋值（必须赋值）
            PassByOut(out int outVal);
            Console.WriteLine($"  out 传递后: outVal = {outVal}"); // 方法内赋的值

            // TryParse 就是 out 模式的典型应用
            // int.TryParse("42", out int result)  → result = 42

            // ── 4.3 默认参数与命名参数 ──
            SubTitle("4.3 默认参数与命名参数");

            // 调用带默认参数的方法
            PrintLog("串口已打开");
            PrintLog("CRC 校验失败", "ERROR");
            PrintLog("收到数据帧", "DEBUG", showTimestamp: false);

            // 命名参数 —— 跳过中间的可选参数
            BuildFrame(devAddr: 1, funcCode: 0x03, startAddr: 0, count: 10);

            // ── 4.4 方法重载 ──
            SubTitle("4.4 方法重载 — 同名不同参数");
            /*
             *  同一个方法名，参数不同（类型或数量），就是重载。
             *  就像"打电话"——可以打手机，也可以打座机，动作一样，工具不同。
             *
             *  你项目里的例子：
             *    Convert.ToUInt16(string) vs Convert.ToUInt16(string, int)
             *    第一个按十进制转换，第二个指定进制
             */

            Console.WriteLine($"  Add(3, 5) = {Add(3, 5)}");
            Console.WriteLine($"  Add(3.5, 2.1) = {Add(3.5, 2.1)}");
            Console.WriteLine($"  Add(\"Hello\", \"World\") = {Add("Hello", "World")}");

            // ── 4.5 表达式体方法 ──
            SubTitle("4.5 表达式体方法 => （简写语法）");
            /*
             *  当方法只有一行时，可以用 => 简写：
             *    int DoubleIt(int x) => x * 2;         // 等价于 { return x * 2; }
             *
             *  你项目里大量使用：
             *    public bool IsSerialOpen => _sp.IsOpen;     // 表达式体属性
             *    private static int DoubleIt(int x) => x * 2; // 表达式体方法
             */

            Console.WriteLine($"  Square(7) = {Square(7)}");
            Console.WriteLine($"  IsEven(4) = {IsEven(4)}");

            // ── 4.6 局部方法 ──
            SubTitle("4.6 局部方法 — 方法里面定义方法");

            // C# 8+ 可以在方法内部定义方法
            void LocalHelper(string name)
            {
                Console.WriteLine($"  [局部方法] 欢迎 {name}！");
            }
            LocalHelper("Modbus 调试助手");

            // ── 4.7 params 可变参数 ──
            SubTitle("4.7 params — 接受任意数量的参数");

            Console.WriteLine($"  Sum = {Sum(1, 2, 3)}");
            Console.WriteLine($"  Sum = {Sum(10, 20, 30, 40, 50)}");

            // ── 4.8 元组返回 ──
            SubTitle("4.8 返回多个值：元组");
            /*
             *  你项目里已经在用了：
             *    var (frame, fc) = _transport.SendReadRequest(...);
             *    这就是解构元组！
             */

            var (addr, data, crc) = ParseModbusFrame("010302000ACRC");
            Console.WriteLine($"  解析结果: 地址={addr}, 数据={data}, CRC={crc}");

            // 不需要所有字段时，用 _ 丢弃
            var (address2, _, _) = ParseModbusFrame("010302000ACRC");
            Console.WriteLine($"  只取地址: {address2}");
        }

        // 第 4 章的辅助方法
        static void PassByValue(int x) { x = 20; }
        static void PassByRef(ref int x) { x = 20; }
        static void PassByOut(out int x) { x = 42; }  // 必须赋值

        static void PrintLog(string message, string level = "INFO", bool showTimestamp = true)
        {
            string ts = showTimestamp ? $"[{DateTime.Now:HH:mm:ss}] " : "";
            Console.WriteLine($"  {ts}[{level}] {message}");
        }

        static void BuildFrame(byte devAddr, byte funcCode, ushort startAddr, ushort count)
        {
            Console.WriteLine($"  帧: 地址={devAddr} 功能码=0x{funcCode:X2} 起始={startAddr} 数量={count}");
        }

        static int Add(int a, int b) => a + b;
        static double Add(double a, double b) => a + b;       // 重载
        static string Add(string a, string b) => a + " " + b; // 再重载

        static int Square(int x) => x * x;
        static bool IsEven(int x) => x % 2 == 0;

        static int Sum(params int[] numbers) => numbers.Sum();

        static (byte addr, string data, string crc) ParseModbusFrame(string hex)
        {
            return ((byte)1, "000A", "ABCD");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 5 章：类与对象
        // ══════════════════════════════════════════════════════════════════

        public static void Ch05_ClassesAndObjects()
        {
            Title("第 5 章", "类与对象 — OOP 起点");

            // ── 5.1 类的基本结构 ──
            SubTitle("5.1 类 = 属性 + 方法 + 事件");
            /*
             *  类就像"模具"，对象就像"产品"。
             *
             *  你的项目中：
             *    类 = ModbusTransport, ModbusProtocol, InputHistoryManager
             *    对象 = _transport, _history （在 ModbusForm 里 new 出来的）
             */

            // 创建一个 ModbusDevice 对象
            var device = new ModbusDevice(1, "温度传感器", "COM3");
            Console.WriteLine($"  设备: {device}");
            device.SetRegisters(new ushort[] { 250, 300, 150 });
            Console.WriteLine($"  平均温度: {device.GetAverageValue():F1}");

            // ── 5.2 访问修饰符 ──
            SubTitle("5.2 访问修饰符：谁能看到什么");
            /*
             *  ┌──────────────┬──────────────────────────────────────────┐
             *  │ 修饰符       │ 可见范围                                 │
             *  ├──────────────┼──────────────────────────────────────────┤
             *  │ public       │ 任何地方都能访问                         │
             *  │ private      │ 只有自己类内部能访问（默认）             │
             *  │ protected    │ 自己 + 子类能访问                        │
             *  │ internal     │ 同一个程序集（项目）内能访问             │
             *  └──────────────┴──────────────────────────────────────────┘
             *
             *  你项目中的例子：
             *    ModbusProtocol 里所有方法都是 public static
             *    ModbusTransport 里 _sp、_tcpClient 是 private
             *    ModbusForm 里 _transport 是 private
             */

            // ── 5.3 属性（Property）详解 ──
            SubTitle("5.3 属性 — 有保护的字段");
            /*
             *  属性 = get（读取） + set（设置）+ 可选逻辑
             *
             *  你的 CSharpConceptsDemo.cs 里 Sensor 类：
             *    public float Temperature
             *    {
             *        get => _temperature;
             *        set
             *        {
             *            _temperature = value;
             *            TemperatureChanged?.Invoke(value);  ← 设置时触发事件
             *        }
             *    }
             */

            var sensor = new TemperatureSensor();
            sensor.Temperature = 25.5f;  // 触发 set
            Console.WriteLine($"  当前温度: {sensor.Temperature}"); // 触发 get
            sensor.Temperature = 99.9f;

            // ── 5.4 构造函数与析构 ──
            SubTitle("5.4 构造函数");

            // 你的 ModbusTransport 构造函数：
            //   public ModbusTransport(Control uiControl, Func<bool> isTcpModeCallback)
            //   {
            //       _uiControl = uiControl ?? throw new ArgumentNullException(nameof(uiControl));
            //       _isTcpMode = isTcpModeCallback ?? throw new ArgumentNullException(nameof(isTcpModeCallback));
            //   }
            // ↑ ?? throw 是一种简洁的"空值防护"写法

            // ── 5.5 静态成员 ──
            SubTitle("5.5 static — 属于类而非对象");
            /*
             *  static = 不需要 new 就能用，全局只有一份
             *
             *  你的 ModbusProtocol 就是纯静态类：
             *    public static class ModbusProtocol
             *    {
             *        public const int MBAP_HEADER_SIZE = 7;     // 静态常量
             *        public static byte[] CalcCRC(byte[] data)  // 静态方法
             *        public static ModbusParseResult ParseResponse(byte[] buffer)
             *    }
             */

            Console.WriteLine($"  MBAP 头大小: {ModbusProtocol.MBAP_HEADER_SIZE}");
            byte[] testData = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            byte[] crc = ModbusProtocol.CalcCRC(testData);
            Console.WriteLine($"  CRC: {crc[0]:X2} {crc[1]:X2}");
        }

        // 第 5 章的辅助类
        class ModbusDevice
        {
            public byte Address { get; }
            public string Name { get; }
            public string Port { get; }

            private ushort[] _registers = Array.Empty<ushort>();

            // 构造函数
            public ModbusDevice(byte address, string name, string port)
            {
                Address = address;
                Name = name;
                Port = port;
            }

            public void SetRegisters(ushort[] values) => _registers = values;

            public double GetAverageValue() =>
                _registers.Length == 0 ? 0 : _registers.Average(v => (double)v);

            // 重写 ToString
            public override string ToString() =>
                $"[地址{Address}] {Name} @ {Port}";
        }

        class TemperatureSensor
        {
            private float _temperature;
            private readonly List<float> _history = new();

            public float Temperature
            {
                get => _temperature;
                set
                {
                    _temperature = value;
                    _history.Add(value);
                    Console.WriteLine($"    [setter] 温度变为 {value:F1}，已记录 (历史共 {_history.Count} 条)");
                }
            }

            // 只读属性（没有 set）
            public int RecordCount => _history.Count;
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 6 章：继承与多态
        // ══════════════════════════════════════════════════════════════════

        public static void Ch06_Inheritance()
        {
            Title("第 6 章", "继承与多态");

            // ── 6.1 继承 ──
            SubTitle("6.1 继承 = 复用 + 扩展");
            /*
             *  继承就像"子承父业"：
             *    父类（基类）定义通用行为
             *    子类（派生类）继承通用行为，再加自己的特色
             *
             *  ┌──────────────────┐
             *  │  CommunicationBase │  ← 父类：连接、断开、发送
             *  ├──────────────────┤
             *  │ SerialConnection  │  ← 子类：串口特有逻辑
             *  │ TcpConnection     │  ← 子类：TCP 特有逻辑
             *  └──────────────────┘
             */

            var serial = new SerialConnection("COM3", 9600);
            var tcp = new TcpConnection("192.168.1.100", 502);

            serial.Connect();
            tcp.Connect();

            // ── 6.2 多态 ──
            SubTitle("6.2 多态 — 同一个方法，不同行为");
            /*
             *  父类引用可以指向子类对象，调用的是子类的方法。
             *  就像"交通工具.前进()"——汽车是开，船是航行，飞机是飞行。
             */

            CommunicationBase[] connections = { serial, tcp };
            foreach (var conn in connections)
            {
                conn.SendData(new byte[] { 0x01, 0x03 });  // 多态调用
            }

            // ── 6.3 虚方法与重写 ──
            SubTitle("6.3 virtual / override / sealed");
            /*
             *  virtual  = 父类说"这个方法子类可以改"
             *  override = 子类说"我改了"
             *  sealed   = 子类说"我的后代不能再改了"
             *
             *  没有 virtual 的方法，子类用 new 关键字"隐藏"（但不多态）
             */

            // ── 6.4 抽象类 ──
            SubTitle("6.4 abstract — 不能实例化的基类");
            /*
             *  abstract class = 有"未完成"方法的类，不能直接 new
             *  子类必须实现所有 abstract 方法才能被使用
             *
             *  比如：CommunicationBase 可以定义 abstract Connect()
             *  但具体怎么连，由 SerialConnection 和 TcpConnection 各自决定
             */

            // ── 6.5 base 关键字 ──
            SubTitle("6.5 base — 调用父类的实现");
            /*
             *  子类重写方法时，可以用 base.XXX() 调用父类的版本
             *  就像"先按老爸的方式做，再加自己的改进"
             */

            var advanced = new AdvancedSerialConnection("COM5", 115200);
            advanced.Connect();  // 会先调父类 Connect，再执行子类额外逻辑
        }

        // 第 6 章辅助类
        abstract class CommunicationBase
        {
            public string Endpoint { get; protected set; }
            public bool IsConnected { get; protected set; }

            protected CommunicationBase(string endpoint)
            {
                Endpoint = endpoint;
            }

            // 普通方法
            public virtual void Connect()
            {
                Console.WriteLine($"  [基类] 正在连接 {Endpoint}...");
                IsConnected = true;
            }

            public virtual void Disconnect()
            {
                Console.WriteLine($"  [基类] 断开 {Endpoint}");
                IsConnected = false;
            }

            // 抽象方法 —— 子类必须实现
            public abstract void SendData(byte[] data);

            // 虚方法 —— 子类可以重写
            public virtual string GetStatus() =>
                IsConnected ? $"已连接 {Endpoint}" : "未连接";
        }

        class SerialConnection : CommunicationBase
        {
            public int BaudRate { get; }

            public SerialConnection(string port, int baud)
                : base(port)  // 调用父类构造函数
            {
                BaudRate = baud;
            }

            public override void SendData(byte[] data)
            {
                Console.WriteLine($"  [串口] 发送 {data.Length} 字节到 {Endpoint} @ {BaudRate}");
            }

            public override string GetStatus() =>
                $"串口 {Endpoint} @ {BaudRate} - {(IsConnected ? "已连接" : "未连接")}";
        }

        class TcpConnection : CommunicationBase
        {
            public int Port { get; }

            public TcpConnection(string ip, int port)
                : base(ip)
            {
                Port = port;
            }

            public override void SendData(byte[] data)
            {
                Console.WriteLine($"  [TCP] 发送 {data.Length} 字节到 {Endpoint}:{Port}");
            }
        }

        class AdvancedSerialConnection : SerialConnection
        {
            public AdvancedSerialConnection(string port, int baud)
                : base(port, baud) { }

            public override void Connect()
            {
                base.Connect();  // 先执行父类的连接逻辑
                Console.WriteLine($"  [高级串口] 自动检测波特率，设置流控...");
            }
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 7 章：接口
        // ══════════════════════════════════════════════════════════════════

        public static void Ch07_Interfaces()
        {
            Title("第 7 章", "接口 — OOP 最灵活的武器");

            // ── 7.1 什么是接口 ──
            SubTitle("7.1 接口 = 约定（只有声明，没有实现）");
            /*
             *  接口就像"合同"：
             *    "不管你是什么设备，只要你实现了 IConnectable，
             *     你就必须能 Connect()、Disconnect()、Send()。"
             *
             *  ┌─────────────────────────────────────────────┐
             *  │             interface IConnectable           │
             *  │  ┌─────────────────────────────────────┐    │
             *  │  │ void Connect()                       │    │
             *  │  │ void Disconnect()                    │    │
             *  │  │ void Send(byte[] data)               │    │
             *  │  │ bool IsConnected { get; }            │    │
             *  │  └─────────────────────────────────────┘    │
             *  └──────────┬──────────────────┬───────────────┘
             *             │                  │
             *    ┌────────┴───────┐  ┌──────┴───────┐
             *    │ SerialDevice   │  │ TcpDevice    │
             *    │ (实现所有方法) │  │ (实现所有方法)│
             *    └────────────────┘  └──────────────┘
             */

            // 用接口类型来引用不同实现
            IConnectable serial = new ModbusSerialDevice("COM3", 9600);
            IConnectable tcp = new ModbusTcpDevice("192.168.1.100", 502);

            serial.Connect();
            tcp.Connect();

            // 多态：统一接口，不同实现
            IConnectable[] devices = { serial, tcp };
            foreach (var dev in devices)
                Console.WriteLine($"  设备状态: {(dev.IsConnected ? "在线" : "离线")}");

            // ── 7.2 接口 vs 抽象类 ──
            SubTitle("7.2 接口 vs 抽象类 — 什么时候用哪个");
            /*
             *  ┌─────────────┬─────────────────┬─────────────────┐
             *  │             │ 接口             │ 抽象类           │
             *  ├─────────────┼─────────────────┼─────────────────┤
             *  │ 能否有实现  │ ✗ 不能（传统）  │ ✓ 可以有方法体  │
             *  │ 多继承      │ ✓ 可实现多个    │ ✗ 只能继承一个  │
             *  │ 字段        │ ✗ 不能有字段    │ ✓ 可以有字段    │
             *  │ 构造函数    │ ✗ 不能          │ ✓ 可以          │
             *  │ 用途        │ "能做什么"      │ "是什么"        │
             *  └─────────────┴─────────────────┴─────────────────┘
             *
             *  经验法则：
             *    "IS-A" 关系 → 继承（Dog IS-A Animal）
             *    "CAN-DO" 关系 → 接口（SerialDevice CAN-DO Connect）
             */

            // ── 7.3 一个类实现多个接口 ──
            SubTitle("7.3 多接口实现");

            var smartDevice = new SmartModbusDevice();
            smartDevice.Connect();           // IConnectable
            smartDevice.Log("正在读取...");   // ILogger
            smartDevice.Dispose();           // IDisposable

            // 可以当作任何接口来用
            ((ILogger)smartDevice).Log("通过接口引用调用");

            // ── 7.4 默认接口方法（C# 8+）──
            SubTitle("7.4 默认接口方法（C# 8+）");
            /*
             *  C# 8 开始，接口方法可以有默认实现！
             *  这缩小了接口和抽象类的差距。
             */
        }

        // 第 7 章辅助接口与类
        interface IConnectable
        {
            bool IsConnected { get; }
            void Connect();
            void Disconnect();
            void Send(byte[] data);
        }

        interface ILogger
        {
            void Log(string message);
        }

        interface IDiagnostic
        {
            // 默认接口方法（C# 8+）
            string GetDiagnostics() => "诊断信息不可用";
        }

        class ModbusSerialDevice : IConnectable
        {
            private readonly string _port;
            private readonly int _baud;

            public bool IsConnected { get; private set; }

            public ModbusSerialDevice(string port, int baud)
            {
                _port = port;
                _baud = baud;
            }

            public void Connect()
            {
                Console.WriteLine($"  [串口] 连接 {_port} @ {_baud}");
                IsConnected = true;
            }

            public void Disconnect() { IsConnected = false; }
            public void Send(byte[] data) =>
                Console.WriteLine($"  [串口] 发送 {data.Length} 字节");
        }

        class ModbusTcpDevice : IConnectable
        {
            private readonly string _ip;
            private readonly int _port;
            public bool IsConnected { get; private set; }

            public ModbusTcpDevice(string ip, int port)
            {
                _ip = ip;
                _port = port;
            }

            public void Connect()
            {
                Console.WriteLine($"  [TCP] 连接 {_ip}:{_port}");
                IsConnected = true;
            }

            public void Disconnect() { IsConnected = false; }
            public void Send(byte[] data) =>
                Console.WriteLine($"  [TCP] 发送 {data.Length} 字节");
        }

        class SmartModbusDevice : IConnectable, ILogger, IDisposable
        {
            public bool IsConnected { get; private set; }

            public void Connect()
            {
                Console.WriteLine("  [智能设备] 连接...");
                IsConnected = true;
            }

            public void Disconnect() { IsConnected = false; }
            public void Send(byte[] data) =>
                Console.WriteLine($"  [智能设备] 发送 {data.Length} 字节");

            public void Log(string message) =>
                Console.WriteLine($"  [LOG] {DateTime.Now:HH:mm:ss} {message}");

            public void Dispose()
            {
                Disconnect();
                Console.WriteLine("  [智能设备] 已释放资源");
            }
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 8 章：枚举与结构体
        // ══════════════════════════════════════════════════════════════════

        public static void Ch08_EnumsAndStructs()
        {
            Title("第 8 章", "枚举与结构体");

            // ── 8.1 枚举 enum ──
            SubTitle("8.1 枚举 — 给数字起名字");
            /*
             *  枚举让代码更可读。
             *  写 funcCode == 3  没人知道 3 是什么
             *  写 funcCode == FuncCode.ReadHoldingRegisters  一目了然
             */

            var code = ModbusFunctionCode.ReadHoldingRegisters;
            Console.WriteLine($"  功能码: {code}");
            Console.WriteLine($"  数值:   {(byte)code}");  // 3
            Console.WriteLine($"  名称:   {code.ToString()}"); // "ReadHoldingRegisters"

            // 枚举可以用在 switch 里
            switch (code)
            {
                case ModbusFunctionCode.ReadCoils:
                    Console.WriteLine("  读线圈"); break;
                case ModbusFunctionCode.ReadHoldingRegisters:
                    Console.WriteLine("  读保持寄存器"); break;
            }

            // [Flags] 枚举 — 可以组合
            var options = ConnectionOptions.AutoReconnect | ConnectionOptions.LogTraffic;
            Console.WriteLine($"  选项: {options}");  // "AutoReconnect, LogTraffic"
            Console.WriteLine($"  包含自动重连？{options.HasFlag(ConnectionOptions.AutoReconnect)}");

            // ── 8.2 结构体 struct ──
            SubTitle("8.2 结构体 — 轻量级值类型");
            /*
             *  struct vs class：
             *    struct 是值类型（存在栈上，赋值时复制）
             *    class  是引用类型（存在堆上，赋值时传引用）
             *
             *  适合用 struct 的场景：
             *    - 小型数据（坐标、颜色、寄存器值）
             *    - 不需要继承
             *    - 创建量大，需要减少 GC 压力
             *
             *  C# 内置的 struct：int, float, bool, DateTime, Guid...
             */

            var point = new ModbusRegister(0, 255);
            var point2 = point;  // 复制！不是引用
            point2.Value = 500;

            Console.WriteLine($"  原始: {point}");   // 还是 255
            Console.WriteLine($"  复制: {point2}");  // 500
            // 如果是 class，两个都会变成 500

            // ── 8.3 record struct（C# 10+）──
            SubTitle("8.3 record struct（C# 10+）");
            /*
             *  record struct = 值类型 + 自动生成 Equals/ToString
             *  非常适合小型不可变数据
             */

            var reg = new RegisterInfo(0, 255, "温度值");
            Console.WriteLine($"  {reg}");  // 自动生成的 ToString

            // with 表达式 —— 创建副本并修改部分字段
            var reg2 = reg with { Value = 300 };
            Console.WriteLine($"  修改后: {reg2}");
        }

        // 第 8 章辅助类型

        enum ModbusFunctionCode : byte
        {
            ReadCoils = 0x01,
            ReadDiscreteInputs = 0x02,
            ReadHoldingRegisters = 0x03,
            ReadInputRegisters = 0x04,
            WriteSingleCoil = 0x05,
            WriteSingleRegister = 0x06,
            WriteMultipleCoils = 0x0F,
            WriteMultipleRegisters = 0x10
        }

        [Flags]
        enum ConnectionOptions
        {
            None = 0,
            AutoReconnect = 1,
            LogTraffic = 2,
            ValidateCRC = 4,
            ShowHex = 8,
            All = AutoReconnect | LogTraffic | ValidateCRC | ShowHex
        }

        struct ModbusRegister
        {
            public int Index { get; set; }
            public ushort Value { get; set; }

            public ModbusRegister(int index, ushort value)
            {
                Index = index;
                Value = value;
            }

            public override string ToString() => $"[{Index}] = {Value}";
        }

        record struct RegisterInfo(int Index, ushort Value, string Description);


        // ══════════════════════════════════════════════════════════════════
        //  第 9 章：异常处理
        // ══════════════════════════════════════════════════════════════════

        public static void Ch09_Exceptions()
        {
            Title("第 9 章", "异常处理");

            // ── 9.1 try / catch / finally ──
            SubTitle("9.1 try / catch / finally");
            /*
             *  try      = "试试这段代码"
             *  catch    = "如果出错，这样处理"
             *  finally  = "不管成功还是失败，最后都要执行"
             *
             *  你的项目中到处在用：
             *    ModbusForm.cs:
             *      try { sp.Open(); }
             *      catch (Exception ex) { MessageBox.Show("打开串口失败：" + ex.Message); }
             *
             *    InputHistoryManager.cs:
             *      try { File.WriteAllText(...); }
             *      catch { /* 静默失败 *​/ }
             */

            // 基本用法
            try
            {
                int.Parse("不是数字");
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"  格式错误: {ex.Message}");
            }

            // 多个 catch — 从具体到宽泛
            try
            {
                string? nullStr = null;
                int len = nullStr!.Length;
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine($"  空引用: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  其他异常: {ex.Message}");
            }

            // finally — 无论如何都执行
            try
            {
                Console.WriteLine("  尝试操作...");
                // 假设操作失败了
                throw new IOException("模拟 IO 错误");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"  捕获: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("  finally: 清理资源（关闭文件、释放端口等）");
            }

            // ── 9.2 自定义异常 ──
            SubTitle("9.2 自定义异常");

            try
            {
                ValidateModbusCount(0x03, 200);
            }
            catch (ModbusCountExceededException ex)
            {
                Console.WriteLine($"  自定义异常: {ex.Message}");
                Console.WriteLine($"    功能码: 0x{ex.FuncCode:X2}, 请求: {ex.Requested}, 最大: {ex.MaxAllowed}");
            }

            // ── 9.3 when 条件过滤 ──
            SubTitle("9.3 catch ... when — 条件捕获");

            try
            {
                throw new HttpRequestException("连接超时", null, System.Net.HttpStatusCode.GatewayTimeout);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
            {
                Console.WriteLine($"  网关超时: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"  其他 HTTP 错误: {ex.Message}");
            }

            // ── 9.4 异常处理最佳实践 ──
            SubTitle("9.4 最佳实践");
            /*
             *  ✗ 不要这样：
             *    catch (Exception ex) { }  // 吞掉所有异常，出了 bug 完全不知道
             *
             *  ✗ 不要这样：
             *    catch (Exception ex) { throw ex; }  // 会丢失调用栈信息
             *
             *  ✓ 应该这样：
             *    catch (Exception ex) { throw; }  // 保留原始调用栈
             *
             *  ✓ 或者这样（你的 InputHistoryManager 的做法）：
             *    catch { /* 静默失败，但你知道这里可能出错 *​/ }
             *
             *  ✓ 最好这样：
             *    catch (SpecificException ex) { /* 只捕获你能处理的 *​/ }
             */
        }

        // 第 9 章辅助
        class ModbusCountExceededException : Exception
        {
            public byte FuncCode { get; }
            public int Requested { get; }
            public int MaxAllowed { get; }

            public ModbusCountExceededException(byte funcCode, int requested, int maxAllowed)
                : base($"功能码 0x{funcCode:X2} 请求 {requested} 个，超出最大限制 {maxAllowed}")
            {
                FuncCode = funcCode;
                Requested = requested;
                MaxAllowed = maxAllowed;
            }
        }

        static void ValidateModbusCount(byte funcCode, int count)
        {
            int max = funcCode switch
            {
                0x01 or 0x02 => 2000,
                0x03 or 0x04 => 125,
                _ => int.MaxValue
            };
            if (count > max)
                throw new ModbusCountExceededException(funcCode, count, max);
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 10 章：泛型
        // ══════════════════════════════════════════════════════════════════

        public static void Ch10_Generics()
        {
            Title("第 10 章", "泛型 — 写一次，适用所有类型");

            // ── 10.1 为什么需要泛型 ──
            SubTitle("10.1 没有泛型的世界");
            /*
             *  没有泛型时，你得为每种类型写一个：
             *    IntList, StringList, ByteList...
             *  或者用 object（丢失类型安全，要强制转换）
             *
             *  泛型 = "类型参数化"
             *  List<T> = 一个"容器蓝图"，T 是占位符
             *    List<int>    → T 被替换为 int
             *    List<string> → T 被替换为 string
             *    List<ushort> → T 被替换为 ushort
             *
             *  你项目中已经在用泛型：
             *    List<BitResult>       → ModbusParseResult.Bits
             *    List<RegisterResult>  → ModbusParseResult.Registers
             *    List<string>          → InputHistoryManager 内部存储
             *    Dictionary<string, List<string>>  → _history 字段
             */

            // ── 10.2 泛型方法 ──
            SubTitle("10.2 泛型方法");

            // 交换任意类型的两个变量
            int a = 10, b = 20;
            Swap(ref a, ref b);
            Console.WriteLine($"  交换后: a={a}, b={b}");

            string s1 = "Hello", s2 = "World";
            Swap(ref s1, ref s2);
            Console.WriteLine($"  交换后: s1={s1}, s2={s2}");

            // ── 10.3 泛型类 ──
            SubTitle("10.3 泛型类");

            // 一个泛型的"结果包装器"
            var success = Result<int>.Ok(200);
            var failure = Result<int>.Fail("连接超时");

            Console.WriteLine($"  成功: {success}");
            Console.WriteLine($"  失败: {failure}");

            // 泛型类可以装任何类型
            var strResult = Result<string>.Ok("数据已收到");
            Console.WriteLine($"  字符串结果: {strResult}");

            // ── 10.4 泛型约束 ──
            SubTitle("10.4 泛型约束 — 限定 T 的范围");
            /*
             *  where T : struct        → T 必须是值类型
             *  where T : class         → T 必须是引用类型
             *  where T : new()         → T 必须有无参构造函数
             *  where T : ISomeInterface → T 必须实现某接口
             *  where T : BaseClass     → T 必须继承某基类
             */

            // 有约束的泛型方法
            var list = CreateListWithDefault<int>(5);
            Console.WriteLine($"  创建了 {list.Count} 个默认 int: [{string.Join(", ", list)}]");

            // ── 10.5 泛型的实际场景 ──
            SubTitle("10.5 在你项目中的应用思路");
            /*
             *  你项目里的 ModbusParseResult 可以用泛型重构：
             *
             *  现在：
             *    public List<BitResult> Bits { get; set; }
             *    public List<RegisterResult> Registers { get; set; }
             *
             *  泛型版：
             *    public class ModbusResult<T> { public List<T> Data { get; set; } }
             *    ModbusResult<BitResult> → 线圈结果
             *    ModbusResult<RegisterResult> → 寄存器结果
             */
        }

        // 第 10 章辅助
        static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        class Result<T>
        {
            public bool IsSuccess { get; }
            public T? Value { get; }
            public string? Error { get; }

            private Result(bool isSuccess, T? value, string? error)
            {
                IsSuccess = isSuccess;
                Value = value;
                Error = error;
            }

            public static Result<T> Ok(T value) => new(true, value, null);
            public static Result<T> Fail(string error) => new(false, default, error);

            public override string ToString() =>
                IsSuccess ? $"OK: {Value}" : $"Error: {Error}";
        }

        static List<T> CreateListWithDefault<T>(int count) where T : struct
        {
            var list = new List<T>();
            for (int i = 0; i < count; i++)
                list.Add(default);  // default(T) = 类型的默认值
            return list;
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 11 章：常用集合
        // ══════════════════════════════════════════════════════════════════

        public static void Ch11_Collections()
        {
            Title("第 11 章", "常用集合类型");

            // ── 11.1 数组 ──
            SubTitle("11.1 数组 Array — 固定长度");
            /*
             *  你在 ModbusProtocol 里大量使用数组：
             *    byte[] pdu = new byte[6];
             *    byte[] frame = new byte[pdu.Length + 2];
             */

            byte[] buffer = { 0x01, 0x03, 0x02, 0x00, 0x0A };
            Console.WriteLine($"  数组长度: {buffer.Length}");
            Console.WriteLine($"  第一个: 0x{buffer[0]:X2}");
            Console.WriteLine($"  最后一个: 0x{buffer[^1]:X2}");  // 索引运算符 ^

            // 数组切片（Range 运算符 ..）
            byte[] data = buffer[2..^0];  // 从第 2 个到末尾
            Console.WriteLine($"  切片 [2..]: {BitConverter.ToString(data)}");

            // ── 11.2 List<T> ──
            SubTitle("11.2 List<T> — 动态数组");
            /*
             *  你项目里：
             *    List<BitResult> Bits = new();
             *    List<RegisterResult> Registers = new();
             */

            var registers = new List<ushort>();
            registers.Add(100);
            registers.Add(200);
            registers.AddRange(new ushort[] { 300, 400, 500 });

            Console.WriteLine($"  数量: {registers.Count}");
            Console.WriteLine($"  第一个: {registers[0]}");
            Console.WriteLine($"  包含 300: {registers.Contains(300)}");
            Console.WriteLine($"  索引: {registers.IndexOf(300)}");

            registers.RemoveAt(0);  // 移除第一个
            registers.RemoveAll(x => x > 350);  // 移除所有 > 350 的
            Console.WriteLine($"  移除后: [{string.Join(", ", registers)}]");

            // ── 11.3 Dictionary<TKey, TValue> ──
            SubTitle("11.3 Dictionary — 键值对");
            /*
             *  你的 InputHistoryManager 用的就是：
             *    Dictionary<string, List<string>> _history;
             */

            var errorMap = new Dictionary<byte, string>
            {
                [0x01] = "非法功能码",
                [0x02] = "非法数据地址",
                [0x03] = "非法数据值",
                [0x04] = "从站设备故障"
            };

            // 查找
            Console.WriteLine($"  错误 0x02: {errorMap[0x02]}");

            // 安全查找
            if (errorMap.TryGetValue(0x05, out string? errMsg))
                Console.WriteLine($"  0x05: {errMsg}");
            else
                Console.WriteLine("  0x05: 未知错误码");

            // 遍历
            foreach (var (code, name) in errorMap)
                Console.WriteLine($"  0x{code:X2} → {name}");

            // ── 11.4 Queue<T> 与 Stack<T> ──
            SubTitle("11.4 Queue（队列）与 Stack（栈）");

            // Queue = 先进先出（像排队买票）
            var frameQueue = new Queue<byte[]>();
            frameQueue.Enqueue(new byte[] { 0x01, 0x03 });  // 入队
            frameQueue.Enqueue(new byte[] { 0x02, 0x04 });
            var first = frameQueue.Dequeue();  // 出队（先入先出）
            Console.WriteLine($"  Queue 先出: {BitConverter.ToString(first)}");

            // Stack = 后进先出（像叠盘子）
            var history = new Stack<string>();
            history.Push("读寄存器 #1");
            history.Push("读寄存器 #2");
            Console.WriteLine($"  Stack 弹出: {history.Pop()}");  // 后入先出
            Console.WriteLine($"  Stack 顶部: {history.Peek()}"); // 看一眼不取出

            // ── 11.5 HashSet<T> ──
            SubTitle("11.5 HashSet — 不重复集合");

            var activeDevices = new HashSet<int> { 1, 2, 3 };
            activeDevices.Add(2);  // 重复，不会添加
            activeDevices.Add(4);
            Console.WriteLine($"  设备数: {activeDevices.Count}"); // 4
            Console.WriteLine($"  包含设备 3: {activeDevices.Contains(3)}");

            // 集合运算
            var setA = new HashSet<int> { 1, 2, 3, 4, 5 };
            var setB = new HashSet<int> { 3, 4, 5, 6, 7 };
            setA.IntersectWith(setB);  // 交集
            Console.WriteLine($"  交集: [{string.Join(", ", setA)}]"); // 3, 4, 5

            // ── 11.6 集合初始化器 ──
            SubTitle("11.6 集合初始化器与展开运算符");

            // C# 12 集合表达式
            List<int> numbers = [1, 2, 3, 4, 5];  // 新语法
            int[] moreNumbers = [.. numbers, 6, 7, 8];  // 展开 + 追加
            Console.WriteLine($"  展开后: [{string.Join(", ", moreNumbers)}]");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 12 章：委托、Lambda 与事件
        // ══════════════════════════════════════════════════════════════════

        public static void Ch12_DelegatesLambdaEvents()
        {
            Title("第 12 章", "委托、Lambda 与事件（进阶版）");

            // ── 12.1 委托的本质 ──
            SubTitle("12.1 委托 = 类型安全的函数指针");
            /*
             *  委托就是一个"方法的类型"。
             *  就像 int 是"整数的类型"一样。
             *
             *  Func<int, bool> 是一个类型，表示"接受 int 返回 bool 的方法"
             *  Action<string> 是一个类型，表示"接受 string 无返回值的方法"
             *
             *  你的 ModbusTransport 构造函数：
             *    Func<bool> isTcpModeCallback  ← 参数类型就是委托
             */

            // ── 12.2 委托的组合 ──
            SubTitle("12.2 委托的组合 — 多播委托");

            Action<string> log = msg => Console.WriteLine($"  [控制台] {msg}");
            log += msg => Debug.WriteLine($"  [调试] {msg}");  // 追加第二个
            log += msg => { /* 可以继续追加 */ };

            log("这条消息被两个处理器接收");

            // 减少处理器
            log -= msg => Debug.WriteLine($"  [调试] {msg}");

            // ── 12.3 Lambda 的多种形式 ──
            SubTitle("12.3 Lambda 完全指南");
            /*
             *  Lambda 从简到繁：
             *    x => x + 1                       ← 最简
             *    (x, y) => x + y                  ← 多参数
             *    x => { return x + 1; }           ← 语句体
             *    (int x) => x + 1                 ← 显式类型
             *    () => Console.WriteLine("hi")    ← 无参数
             */

            // 在你项目中的 Lambda 用法汇总：
            // 1. 事件处理
            //    btn.Click += (s, e) => { ... };
            // 2. LINQ 查询
            //    registers.Where(v => v > 100).ToList();
            // 3. 回调
            //    new ModbusTransport(this, () => _isTcpMode);
            // 4. 闭包
            //    int threshold = 3;
            //    var above = numbers.Where(n => n > threshold);

            // ── 12.4 闭包详解 ──
            SubTitle("12.4 闭包 — Lambda 捕获外部变量");
            /*
             *  Lambda 可以"记住"它被创建时的环境变量。
             *  这叫闭包（Closure）。
             */

            int counter = 0;
            Action increment = () => counter++;  // 捕获了 counter

            increment();
            increment();
            increment();
            Console.WriteLine($"  闭包捕获: counter = {counter}"); // 3

            // 陷阱：循环中的闭包
            Console.WriteLine("  闭包陷阱:");
            var actions = new List<Action>();
            for (int i = 0; i < 3; i++)
            {
                int captured = i;  // 每次循环创建新变量
                actions.Add(() => Console.Write($" {captured}"));
            }
            foreach (var act in actions) act();
            Console.WriteLine();  // 0 1 2（正确）

            // ── 12.5 事件进阶 ──
            SubTitle("12.5 事件进阶");
            /*
             *  event 关键字给委托加了保护：
             *    - 外部只能 += 和 -=，不能直接 = 或 Invoke()
             *    - 只有声明 event 的类内部才能 Invoke
             *
             *  你项目中的事件定义：
             *    public event Action<byte[], bool>? FrameReceived;
             *    public event Action<string>? ErrorOccurred;
             *    public event Action<bool, string>? ConnectionChanged;
             */

            // ── 12.6 EventHandler 标准模式 ──
            SubTitle("12.6 EventHandler — .NET 标准事件模式");

            var alarm = new AlarmSystem();
            alarm.OnTemperatureExceeded += (sender, args) =>
            {
                Console.WriteLine($"  🚨 温度报警！当前 {args.Temperature:F1}°C，阈值 {args.Threshold:F1}°C");
            };
            alarm.CheckTemperature(85.0f, 80.0f);
        }

        // 第 12 章辅助
        class TemperatureEventArgs : EventArgs
        {
            public float Temperature { get; }
            public float Threshold { get; }

            public TemperatureEventArgs(float temp, float threshold)
            {
                Temperature = temp;
                Threshold = threshold;
            }
        }

        class AlarmSystem
        {
            // 标准事件模式：EventHandler<TEventArgs>
            public event EventHandler<TemperatureEventArgs>? OnTemperatureExceeded;

            public void CheckTemperature(float current, float threshold)
            {
                if (current > threshold)
                    OnTemperatureExceeded?.Invoke(this,
                        new TemperatureEventArgs(current, threshold));
            }
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 13 章：LINQ
        // ══════════════════════════════════════════════════════════════════

        public static void Ch13_LINQ()
        {
            Title("第 13 章", "LINQ — 数据查询的瑞士军刀");

            // ── 13.1 什么是 LINQ ──
            SubTitle("13.1 LINQ = Language Integrated Query");
            /*
             *  LINQ 让你用统一的语法查询各种数据源：
             *    数组、列表、数据库、XML、JSON...
             *
             *  两种写法：
             *    1. 方法语法（链式调用）—— 推荐，你项目里在用
             *    2. 查询语法（SQL 风格）—— 偶尔更清晰
             */

            ushort[] registers = { 0, 150, 0, 250, 0, 500, 1200, 300, 0, 450 };

            // ── 13.2 Where — 过滤 ──
            SubTitle("13.2 Where — 过滤");

            var nonZero = registers.Where(v => v != 0).ToArray();
            Console.WriteLine($"  非零值: [{string.Join(", ", nonZero)}]");

            var inRange = registers.Where(v => v >= 100 && v <= 500);
            Console.WriteLine($"  100~500: [{string.Join(", ", inRange)}]");

            // ── 13.3 Select — 转换 ──
            SubTitle("13.3 Select — 映射/转换");

            var hexStrings = registers.Select(v => $"0x{v:X4}");
            Console.WriteLine($"  十六进制: [{string.Join(", ", hexStrings)}]");

            // 带索引
            var indexed = registers.Select((v, i) => $"[{i}]={v}");
            Console.WriteLine($"  带索引: [{string.Join(", ", indexed.Take(5))}]");

            // ── 13.4 聚合 ──
            SubTitle("13.4 聚合：Count / Sum / Min / Max / Average");

            Console.WriteLine($"  总数: {registers.Length}");
            Console.WriteLine($"  非零数量: {registers.Count(v => v != 0)}");
            Console.WriteLine($"  总和: {registers.Select(v => (int)v).Sum()}");
            Console.WriteLine($"  最小: {registers.Min()}");
            Console.WriteLine($"  最大: {registers.Max()}");
            Console.WriteLine($"  平均: {registers.Select(v => (double)v).Average():F1}");

            // ── 13.5 排序 ──
            SubTitle("13.5 排序：OrderBy / OrderByDescending");

            var sorted = registers.Where(v => v > 0).OrderBy(v => v);
            Console.WriteLine($"  升序: [{string.Join(", ", sorted)}]");

            var desc = registers.OrderByDescending(v => v).Take(3);
            Console.WriteLine($"  前三大: [{string.Join(", ", desc)}]");

            // ── 13.6 First / Last / Single ──
            SubTitle("13.6 查找元素");

            var first2 = registers.First(v => v > 200);      // 第一个 >200 的
            var last2 = registers.Last(v => v > 200);        // 最后一个 >200 的
            var orDefault = registers.FirstOrDefault(v => v > 9999); // 找不到返回 0

            Console.WriteLine($"  第一个 >200: {first2}");
            Console.WriteLine($"  最后一个 >200: {last2}");
            Console.WriteLine($"  找不到返回默认: {orDefault}");

            // ── 13.7 分组 ──
            SubTitle("13.7 GroupBy — 分组统计");

            var grouped = registers.GroupBy(v => v == 0 ? "零值" : "有效值");
            foreach (var group in grouped)
                Console.WriteLine($"  {group.Key}: {group.Count()} 个");

            // ── 13.8 Any / All / Contains ──
            SubTitle("13.8 判断：Any / All / Contains");

            Console.WriteLine($"  有超限值吗？{registers.Any(v => v > 1000)}");
            Console.WriteLine($"  全部非零吗？{registers.All(v => v != 0)}");
            Console.WriteLine($"  包含 500 吗？{registers.Any(v => v == 500)}");

            // ── 13.9 SelectMany — 展平 ──
            SubTitle("13.9 SelectMany — 展平嵌套集合");

            var allFrames = new List<byte[]>
            {
                new byte[] { 0x01, 0x03, 0x02 },
                new byte[] { 0x02, 0x04, 0x02 },
                new byte[] { 0x03, 0x01, 0x01 }
            };

            var allBytes = allFrames.SelectMany(f => f);  // 展平
            Console.WriteLine($"  所有字节: {BitConverter.ToString(allBytes.ToArray())}");

            // ── 13.10 Zip — 配对 ──
            SubTitle("13.10 Zip — 两个序列配对");

            string[] names = { "温度", "压力", "转速" };
            ushort[] values = { 250, 101, 1400 };

            var pairs = names.Zip(values, (name, val) => $"{name}={val}");
            Console.WriteLine($"  配对: [{string.Join(", ", pairs)}]");

            // ── 13.11 查询语法 ──
            SubTitle("13.11 查询语法（SQL 风格）");

            // 方法语法
            var methodStyle = registers
                .Where(v => v > 0)
                .OrderByDescending(v => v)
                .Take(3)
                .Select(v => $"0x{v:X4}");

            // 查询语法（等价）
            var queryStyle = (from v in registers
                              where v > 0
                              orderby v descending
                              select $"0x{v:X4}").Take(3);

            Console.WriteLine($"  方法语法: [{string.Join(", ", methodStyle)}]");
            Console.WriteLine($"  查询语法: [{string.Join(", ", queryStyle)}]");

            // ── 13.12 ToDictionary / ToLookup ──
            SubTitle("13.12 转换为字典");

            var regDict = registers
                .Select((v, i) => new { Index = i, Value = v })
                .Where(x => x.Value > 0)
                .ToDictionary(x => x.Index, x => x.Value);

            foreach (var (idx, val) in regDict.Take(3))
                Console.WriteLine($"  寄存器[{idx}] = {val}");

            // ── 13.13 Aggregate — 自定义聚合 ──
            SubTitle("13.13 Aggregate — 自定义归约");

            // 把所有寄存器值拼成一个十六进制字符串
            string hexStr = registers.Aggregate("",
                (acc, val) => acc + $"{val:X4} ");
            Console.WriteLine($"  聚合: {hexStr.Trim()}");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 14 章：异步编程 async/await
        // ══════════════════════════════════════════════════════════════════

        public static void Ch14_AsyncAwait()
        {
            Title("第 14 章", "异步编程 async/await");

            // ── 14.1 为什么需要异步 ──
            SubTitle("14.1 为什么需要异步？");
            /*
             *  同步：做一件事 → 等它完成 → 做下一件
             *    就像你站在微波炉前干等，什么都不做
             *
             *  异步：发出请求 → 去做别的 → 请求完成后回来处理
             *    就像按了微波炉按钮后去看手机，响了再去拿
             *
             *  你项目中的痛点：
             *    - TCP 连接是阻塞的：ConnectTcp() 会卡住 UI
             *    - 串口接收用 Thread.Sleep(50) 等缓冲区
             *    - 用 Task.Run + SafeInvoke 手动管理线程
             *
             *  async/await 可以大幅简化这些代码。
             */

            // ── 14.2 基本语法 ──
            SubTitle("14.2 基本语法");
            /*
             *  async = "这个方法包含异步操作"
             *  await = "在这里暂停，等结果回来再继续"
             *
             *  关键点：await 不会阻塞线程！
             *  它只是"注册一个回调"，然后把控制权交还给调用者。
             */

            // 调用异步方法
            DemoAsync().Wait();  // .Wait() 只是为了在同步环境里演示，实际不要这么用
        }

        static async Task DemoAsync()
        {
            SubTitle("14.3 Task — 代表一个异步操作");
            /*
             *  Task    = "一个还没有完成的工作"
             *  Task<T> = "一个还没有完成的工作，完成后会有结果 T"
             *
             *  await task = "等它完成，然后拿结果"
             */

            // 模拟异步操作（如网络请求、文件读写）
            Console.WriteLine("  开始下载...");
            string result = await DownloadDataAsync("192.168.1.100");
            Console.WriteLine($"  下载完成: {result}");

            // ── 14.4 异步方法的返回类型 ──
            SubTitle("14.4 返回类型");
            /*
             *  async Task      → 无返回值的异步方法
             *  async Task<T>   → 有返回值 T 的异步方法
             *  async void      → 只用于事件处理，其他地方别用！
             *
             *  对应到你项目中：
             *    现在:  void ConnectTcp(string ip, int port)  // 同步，会卡 UI
             *    改进:  async Task ConnectTcpAsync(string ip, int port)  // 异步，不卡
             */

            // ── 14.5 当你的代码改写成 async ──
            SubTitle("14.5 对照：你项目中的代码如何改写");

            Console.WriteLine("  【现在】ModbusTransport.ConnectTcp():");
            Console.WriteLine("    _tcpClient.Connect(ip, port);  ← 阻塞 UI 线程");
            Console.WriteLine("    Task.Run(TcpReceiveLoop);      ← 手动开后台线程");
            Console.WriteLine();
            Console.WriteLine("  【改进】ConnectTcpAsync():");
            Console.WriteLine("    await _tcpClient.ConnectAsync(ip, port);  ← 不阻塞");
            Console.WriteLine("    _ = TcpReceiveLoopAsync();               ← 异步接收");

            // ── 14.6 异步串口操作 ──
            SubTitle("14.6 异步版串口读写");

            var data = await ReadModbusResponseAsync();
            Console.WriteLine($"  收到: {BitConverter.ToString(data)}");

            // ── 14.7 并行执行 ──
            SubTitle("14.7 Task.WhenAll — 并行执行多个任务");
            /*
             *  如果有多个独立的异步操作，可以同时发出去，一起等。
             *  就像同时给 3 个设备发读请求，不用一个一个等。
             */

            Console.WriteLine("  同时读取 3 个设备...");
            var tasks = new[]
            {
                ReadDeviceAsync("温度传感器"),
                ReadDeviceAsync("压力传感器"),
                ReadDeviceAsync("转速传感器")
            };

            string[] results = await Task.WhenAll(tasks);
            foreach (var r in results)
                Console.WriteLine($"  结果: {r}");

            // ── 14.8 Task.WhenAny — 谁先完成用谁 ──
            SubTitle("14.8 Task.WhenAny — 竞速");

            var fastest = await Task.WhenAny(
                ReadDeviceAsync("设备A (模拟慢)"),
                ReadDeviceAsync("设备B (模拟快)")
            );
            Console.WriteLine($"  最快响应: {await fastest}");

            // ── 14.9 CancellationToken — 取消操作 ──
            SubTitle("14.9 CancellationToken — 取消长时间操作");
            /*
             *  用户点了"断开"按钮，正在等待的异步操作应该立即取消。
             *
             *  你项目中：
             *    用户点"断开"→ 调用 DisconnectTcp()
             *    但 TcpReceiveLoop 里的 _tcpStream.Read() 还在阻塞
             *    用 CancellationToken 可以优雅地取消它
             */

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1)); // 1 秒后自动取消

            try
            {
                await LongRunningOperationAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  操作已取消（超时）");
            }

            // ── 14.10 ConfigureAwait ──
            SubTitle("14.10 ConfigureAwait(false)");
            /*
             *  在 WinForms 里，await 之后默认会回到 UI 线程。
             *  如果你不需要操作 UI，用 ConfigureAwait(false) 可以提高性能。
             *
             *  await SomeAsync().ConfigureAwait(false);
             *  // 之后的代码不在 UI 线程上，不能操作控件
             *
             *  一般在库代码里用，UI 代码里不用。
             */

            // ── 14.11 async/await 最佳实践 ──
            SubTitle("14.11 最佳实践");
            /*
             *  ✗ 不要这样：
             *    var task = SomeAsync();
             *    task.Wait();       // 死锁风险！
             *    task.Result;       // 同上
             *
             *  ✗ 不要这样：
             *    async void SomeMethod()  // 除了事件处理，不要用 void
             *
             *  ✓ 应该这样：
             *    await SomeAsync();        // 直接 await
             *
             *  ✓ 或者这样（Fire-and-forget）：
             *    _ = SomeAsync();          // 明确表示不等待结果
             */
        }

        // 第 14 章辅助方法
        static async Task<string> DownloadDataAsync(string ip)
        {
            await Task.Delay(100); // 模拟网络延迟
            return $"来自 {ip} 的数据 (01 03 02 00 0A)";
        }

        static async Task<byte[]> ReadModbusResponseAsync()
        {
            await Task.Delay(50); // 模拟串口等待
            return new byte[] { 0x01, 0x03, 0x02, 0x00, 0x0A };
        }

        static async Task<string> ReadDeviceAsync(string deviceName)
        {
            // 模拟不同的响应时间
            int delay = deviceName.Contains("快") ? 50 : 200;
            await Task.Delay(delay);
            return $"{deviceName}: 250";
        }

        static async Task LongRunningOperationAsync(CancellationToken token)
        {
            for (int i = 0; i < 100; i++)
            {
                token.ThrowIfCancellationRequested();  // 检查是否取消
                await Task.Delay(50, token);
                Console.Write($"\r  进度: {i + 1}%");
            }
            Console.WriteLine();
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 15 章：模式匹配
        // ══════════════════════════════════════════════════════════════════

        public static void Ch15_PatternMatching()
        {
            Title("第 15 章", "模式匹配 — C# 现代语法核心");

            // ── 15.1 类型模式 ──
            SubTitle("15.1 类型模式 is / switch");

            object[] objects = { 42, "hello", 3.14, new byte[] { 0x01 }, null };

            foreach (var obj in objects)
            {
                string desc = obj switch
                {
                    int i when i > 0 => $"正整数 {i}",
                    int i => $"非正整数 {i}",
                    string s => $"字符串 \"{s}\"",
                    double d => $"浮点数 {d}",
                    byte[] b => $"字节数组 [{BitConverter.ToString(b)}]",
                    null => "空值",
                    _ => $"未知类型 {obj.GetType().Name}"
                };
                Console.WriteLine($"  {desc}");
            }

            // ── 15.2 属性模式 ──
            SubTitle("15.2 属性模式 — 检查对象的属性");

            var results = new ModbusParseResult[]
            {
                new() { IsError = true, ErrorMessage = "CRC 校验失败" },
                new() { IsError = false, Registers = { new RegisterResult { Index = 0, Value = 250 } } },
                new() { IsError = false, Bits = { new BitResult { Index = 0, IsOn = true } } }
            };

            foreach (var result in results)
            {
                string desc = result switch
                {
                    { IsError: true, ErrorMessage: var msg } => $"❌ 错误: {msg}",
                    { Registers.Count: > 0 } => $"📊 {result.Registers.Count} 个寄存器",
                    { Bits.Count: > 0 } => $"🔌 {result.Bits.Count} 个位",
                    _ => "空数据"
                };
                Console.WriteLine($"  {desc}");
            }

            // ── 15.3 元组模式 ──
            SubTitle("15.3 元组模式 — 同时匹配多个值");

            byte funcCode = 0x03;
            bool isError = false;
            string desc2 = (funcCode, isError) switch
            {
                (0x01, false) => "读线圈成功",
                (0x03, false) => "读保持寄存器成功",
                (_, true) => "操作失败",
                _ => "其他"
            };
            Console.WriteLine($"  结果: {desc2}");

            // ── 15.4 位置模式 ──
            SubTitle("15.4 位置模式（需要 Deconstruct）");

            // ── 15.5 常量模式与逻辑模式 ──
            SubTitle("15.5 逻辑模式：and / or / not");

            byte errorCode = 0x02;
            string severity = errorCode switch
            {
                0x01 or 0x02 => "协议错误",
                0x03 => "数据错误",
                0x04 => "设备错误",
                >= 0x80 => "保留错误码",
                _ => "未知"
            };
            Console.WriteLine($"  错误 0x{errorCode:X2}: {severity}");

            // not 模式
            int value = 255;
            if (value is not 0 and not > 1000)
                Console.WriteLine($"  {value} 在有效范围内");

            // ── 15.6 switch 表达式 vs if-else ──
            SubTitle("15.6 什么时候用模式匹配");
            /*
             *  当你需要根据"值的形状"做不同处理时，用模式匹配。
             *  比传统的 if-else 链更清晰、更安全（编译器会检查是否覆盖所有情况）。
             *
             *  你项目中 GetFuncCodeColor 已经在用 switch 表达式了。
             *  但还可以用模式匹配做更多事，比如 ParseResponse 的分支。
             */
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 16 章：记录类型与元组
        // ══════════════════════════════════════════════════════════════════

        public static void Ch16_RecordsAndTuples()
        {
            Title("第 16 章", "记录类型与元组");

            // ── 16.1 元组 ──
            SubTitle("16.1 元组 — 临时打包多个值");
            /*
             *  元组 = 轻量级的"临时数据容器"
             *  不需要定义类，直接打包。
             *
             *  你项目中已经在用：
             *    public (byte[] frame, byte funcCode) SendReadRequest(...) { ... }
             *    var (frame, fc) = _transport.SendReadRequest(...);
             */

            // 创建元组
            var point = (X: 100, Y: 200);
            Console.WriteLine($"  坐标: ({point.X}, {point.Y})");

            // 解构
            var (x, y) = point;
            Console.WriteLine($"  解构: x={x}, y={y}");

            // 方法返回元组
            var (min, max, avg) = AnalyzeRegisters(new ushort[] { 100, 200, 300, 400, 500 });
            Console.WriteLine($"  分析: 最小={min}, 最大={max}, 平均={avg}");

            // ── 16.2 record class（C# 9+）──
            SubTitle("16.2 record — 不可变数据类型");
            /*
             *  record 自动给你生成：
             *    - Equals / GetHashCode（值比较，不是引用比较）
             *    - ToString
             *    - 解构
             *    - with 表达式（创建副本并修改）
             */

            var device1 = new DeviceRecord(1, "温度传感器", "COM3");
            var device2 = new DeviceRecord(1, "温度传感器", "COM3");

            // 值比较！两个类内容相同就相等
            Console.WriteLine($"  device1 == device2: {device1 == device2}"); // True
            // 普通 class 的话是 False（引用不同）

            Console.WriteLine($"  ToString: {device1}");

            // with 表达式 —— 创建副本，只改部分字段
            var device3 = device1 with { Port = "COM5" };
            Console.WriteLine($"  修改后: {device3}");

            // 解构
            var (addr, name, port) = device1;
            Console.WriteLine($"  解构: 地址={addr}, 名称={name}, 端口={port}");

            // ── 16.3 什么时候用 record vs class ──
            SubTitle("16.3 record vs class vs struct");
            /*
             *  ┌─────────────┬──────────────────────────────────────────┐
             *  │ 类型        │ 适合场景                                 │
             *  ├─────────────┼──────────────────────────────────────────┤
             *  │ class       │ 需要可变状态、继承、引用语义             │
             *  │ record      │ 数据传输对象 (DTO)、不可变数据、值比较  │
             *  │ struct      │ 小型值类型、高频创建、无继承             │
             *  │ record struct│ 不可变的小型值类型                      │
             *  └─────────────┴──────────────────────────────────────────┘
             *
             *  你的 ModbusParseResult 适合用 record 吗？
             *  答：不太适合，因为它有 List 属性，需要可变。
             *  但 ModbusParseResult 的子项 BitResult / RegisterResult 可以用 record。
             */
        }

        record DeviceRecord(byte Address, string Name, string Port);

        static (ushort min, ushort max, double avg) AnalyzeRegisters(ushort[] registers)
        {
            return (registers.Min(), registers.Max(), (ushort)registers.Select(v => (int)v).Average());
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 17 章：可空类型与空安全
        // ══════════════════════════════════════════════════════════════════

        public static void Ch17_NullableAndNullSafety()
        {
            Title("第 17 章", "可空类型与空安全");

            // ── 17.1 值类型的可空版本 ──
            SubTitle("17.1 Nullable<T> — 让值类型也能为 null");
            /*
             *  int 不能为 null（它总是有值）
             *  int? 可以是 null（用 ? 后缀）
             *
             *  使用场景：
             *    - 数据库字段可能为空
             *    - 用户还没输入
             *    - Modbus 设备没响应
             */

            int? temperature = null;
            Console.WriteLine($"  温度: {temperature ?? -1}");  // 用 ?? 提供默认值

            temperature = 25;
            Console.WriteLine($"  温度: {temperature}");

            // HasValue / Value
            if (temperature.HasValue)
                Console.WriteLine($"  有值: {temperature.Value}");

            // ── 17.2 引用类型的可空 ──
            SubTitle("17.2 引用类型与 nullable 引用类型");
            /*
             *  你的 .csproj 里启用了：
             *    <Nullable>enable</Nullable>
             *
             *  这意味着编译器会检查你是否处理了 null 的情况。
             *
             *  string   → 不能为 null（编译器会警告）
             *  string?  → 可以为 null
             */

            string nonNull = "必须有值";
            string? maybeNull = null;

            // ── 17.3 空安全运算符 ──
            SubTitle("17.3 空安全运算符：? ?? ?. ?[]");

            // ?. 空条件运算符 —— 如果左边为 null，整个表达式返回 null
            string? name = null;
            int? length = name?.Length;  // null，不会抛异常
            Console.WriteLine($"  name?.Length = {length ?? 0}");

            // ?? 空合并运算符 —— 左边为 null 就用右边
            string displayName = name ?? "未命名";
            Console.WriteLine($"  name ?? \"未命名\" = \"{displayName}\"");

            // ??= 空合并赋值 —— 只在左边为 null 时赋值
            string? label = null;
            label ??= "默认标签";
            Console.WriteLine($"  label ??= → \"{label}\"");

            // 你项目中常见的空安全写法：
            // _transport?.Dispose();     ← 如果 _transport 为 null 就不调用
            // FrameReceived?.Invoke()    ← 如果没人订阅就不触发
            // new SerialPort()           ← 确保不为 null

            // ── 17.4 ! 空断言运算符 ──
            SubTitle("17.4 ! 空断言 — 告诉编译器「我确定不为 null」");
            /*
             *  var sp = _sp!;  // 告诉编译器：我确定 _sp 不是 null
             *  ⚠ 危险：如果真的为 null，运行时会抛 NullReferenceException
             *  只在你 100% 确定不为 null 时使用。
             */

            // ── 17.5 null 检查的新写法 ──
            SubTitle("17.5 现代 null 检查写法");

            object? obj = "hello";

            // 传统写法
            if (obj != null)
                Console.WriteLine($"  传统: {obj}");

            // is not null（推荐）
            if (obj is not null)
                Console.WriteLine($"  is not null: {obj}");

            // 模式匹配
            if (obj is string s)
                Console.WriteLine($"  is string: {s}");
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 18 章：扩展方法
        // ══════════════════════════════════════════════════════════════════

        public static void Ch18_ExtensionMethods()
        {
            Title("第 18 章", "扩展方法 — 给别人的类「加方法」");

            // ── 18.1 什么是扩展方法 ──
            SubTitle("18.1 扩展方法 = 静态方法，但看起来像实例方法");
            /*
             *  你不能修改别人的类（比如 string, byte[]），但你想给它加方法。
             *  扩展方法让你做到这一点！
             *
             *  语法：第一个参数前加 this，表示"这个方法扩展了什么类型"
             *    public static string ToHex(this byte[] bytes)
             *    ↑ 表示给 byte[] 类型加了一个 ToHex() 方法
             *
             *  LINQ 就是用扩展方法实现的：
             *    Where(), Select(), OrderBy() 都是 IEnumerable<T> 的扩展方法
             */

            // 使用扩展方法
            byte[] frame = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD };
            Console.WriteLine($"  ToHex:     {frame.ToHex()}");
            Console.WriteLine($"  ToHexSpaced: {frame.ToHexSpaced()}");
            Console.WriteLine($"  Describe:  {frame.Describe()}");

            ushort value = 12345;
            Console.WriteLine($"  ToHexStr:  {value.ToHexStr()}");

            // ── 18.2 链式调用 ──
            SubTitle("18.2 链式调用（Fluent API）");
            /*
             *  如果每个扩展方法都返回 this 或相关类型，就可以链式调用：
             *    result = data.Filter(v => v > 0).Transform(v => v * 2).ToList();
             */

            // ── 18.3 实际应用 ──
            SubTitle("18.3 在你项目中的应用");
            /*
             *  可以给 byte[] 加扩展方法，简化你的协议代码：
             *
             *  现在：
             *    BitConverter.ToString(frame).Replace("-", " ")
             *
             *  扩展方法后：
             *    frame.ToHexSpaced()
             *
             *  现在：
             *    ModbusProtocol.VerifyCRC(buffer)
             *
             *  扩展方法后：
             *    buffer.VerifyCRC()
             */
        }

        // 第 18 章 — 扩展方法定义（必须在静态类中）
        // 你可以把这些放到一个单独的 ByteExtensions.cs 文件中

        /// <summary>字节数组 → 紧凑十六进制字符串</summary>
        public static string ToHex(this byte[] bytes) =>
            Convert.ToHexString(bytes);

        /// <summary>字节数组 → 空格分隔的十六进制字符串</summary>
        public static string ToHexSpaced(this byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", " ");

        /// <summary>字节数组 → Modbus 帧描述</summary>
        public static string Describe(this byte[] bytes)
        {
            if (bytes.Length < 2) return "太短";
            return $"地址=0x{bytes[0]:X2} 功能码=0x{bytes[1]:X2} 数据长度={bytes.Length - 2}";
        }

        /// <summary>ushort → "0x00FF" 格式</summary>
        public static string ToHexStr(this ushort value) => $"0x{value:X4}";


        // ══════════════════════════════════════════════════════════════════
        //  第 19 章：特性 (Attribute)
        // ══════════════════════════════════════════════════════════════════

        public static void Ch19_Attributes()
        {
            Title("第 19 章", "特性 (Attribute) — 代码里的「标签」");

            // ── 19.1 什么是特性 ──
            SubTitle("19.1 特性 = 给代码贴标签");
            /*
             *  特性不会改变代码的行为，但能提供"元信息"。
             *  就像快递包裹上的"易碎"标签——不影响包裹内容，
             *  但提醒处理的人要小心。
             *
             *  你已经见过的特性：
             *    [STAThread]           ← Program.cs 里，标记主线程为单线程单元
             *    [Serializable]        ← 标记类可序列化
             *    [Flags]              ← 标记枚举是位标志
             *    [Obsolete]           ← 标记方法已过时
             */

            // ── 19.2 常用内置特性 ──
            SubTitle("19.2 常用内置特性");

            // [Obsolete] — 标记已过时的方法
            OldMethod();

            // [Description] — 给元素加描述
            var desc = typeof(ModbusFunctionCode)
                .GetField(ModbusFunctionCode.ReadHoldingRegisters.ToString())?
                .GetCustomAttribute<DescriptionAttribute>()?
                .Description;
            Console.WriteLine($"  ReadHoldingRegisters 描述: {desc ?? "无"}");

            // ── 19.3 自定义特性 ──
            SubTitle("19.3 自定义特性");

            // 用反射读取自定义特性
            var type = typeof(CustomDevice);
            var attr = type.GetCustomAttribute<DeviceInfoAttribute>();
            if (attr != null)
            {
                Console.WriteLine($"  设备名称: {attr.Name}");
                Console.WriteLine($"  设备版本: {attr.Version}");
                Console.WriteLine($"  支持的功能码: {string.Join(", ", attr.SupportedFunctions)}");
            }

            // ── 19.4 特性的实际应用 ──
            SubTitle("19.4 实际应用场景");
            /*
             *  特性在框架和库中大量使用：
             *    [JsonPropertyName("temp")]  ← JSON 序列化时指定字段名
             *    [Required]                  ← 数据验证
             *    [TestMethod]                ← 单元测试标记
             *    [DllImport("user32.dll")]   ← P/Invoke 调用 Windows API
             *    [DefaultValue(9600)]        ← 属性的默认值
             */
        }

        // 第 19 章辅助
        [Obsolete("请使用 NewMethod() 代替")]
        static void OldMethod()
        {
            Console.WriteLine("  [过时方法] 这个方法已过时，编译器会发出警告");
        }

        [AttributeUsage(AttributeTargets.Class)]
        class DeviceInfoAttribute : Attribute
        {
            public string Name { get; }
            public string Version { get; }
            public byte[] SupportedFunctions { get; }

            public DeviceInfoAttribute(string name, string version, params byte[] functions)
            {
                Name = name;
                Version = version;
                SupportedFunctions = functions;
            }
        }

        [DeviceInfo("Modbus RTU 网关", "2.1.0", 0x01, 0x02, 0x03, 0x04)]
        class CustomDevice { }


        // ══════════════════════════════════════════════════════════════════
        //  第 20 章：文件 I/O 与 JSON 序列化
        // ══════════════════════════════════════════════════════════════════

        public static void Ch20_FileIOAndJSON()
        {
            Title("第 20 章", "文件 I/O 与 JSON 序列化");

            // ── 20.1 文件读写 ──
            SubTitle("20.1 文件读写基础");
            /*
             *  File.ReadAllText     → 一次性读取整个文件为字符串
             *  File.WriteAllText    → 一次性写入字符串到文件
             *  File.ReadAllLines    → 按行读取为 string[]
             *  File.Exists          → 检查文件是否存在
             *  File.Delete          → 删除文件
             *
             *  你的 InputHistoryManager 里用了：
             *    File.ReadAllText(_filePath)
             *    File.WriteAllText(_filePath, json)
             */

            // 安全的文件读写模式（你的 InputHistoryManager 的做法）
            string testPath = Path.Combine(Path.GetTempPath(), "csharp_guide_test.json");
            try
            {
                // 写
                File.WriteAllText(testPath, "{\"test\": true}");
                Console.WriteLine($"  写入: {testPath}");

                // 读
                string content = File.ReadAllText(testPath);
                Console.WriteLine($"  读取: {content}");

                // 清理
                File.Delete(testPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  错误: {ex.Message}");
            }

            // ── 20.2 Path 工具类 ──
            SubTitle("20.2 Path — 路径操作");

            Console.WriteLine($"  Combine:     {Path.Combine("C:\\data", "history.json")}");
            Console.WriteLine($"  Extension:   {Path.GetExtension("test.json")}");
            Console.WriteLine($"  FileName:    {Path.GetFileName("C:\\data\\test.json")}");
            Console.WriteLine($"  Directory:   {Path.GetDirectoryName("C:\\data\\test.json")}");
            Console.WriteLine($"  TempPath:    {Path.GetTempPath()}");
            Console.WriteLine($"  RandomFile:  {Path.GetTempFileName()}");

            // ── 20.3 目录操作 ──
            SubTitle("20.3 Directory 操作");

            string testDir = Path.Combine(Path.GetTempPath(), "csharp_guide_demo");
            Directory.CreateDirectory(testDir);
            Console.WriteLine($"  创建目录: {testDir}");
            Console.WriteLine($"  存在: {Directory.Exists(testDir)}");
            Directory.Delete(testDir);
            Console.WriteLine($"  删除后存在: {Directory.Exists(testDir)}");

            // ── 20.4 JSON 序列化 ──
            SubTitle("20.4 System.Text.Json — JSON 序列化");
            /*
             *  你的 InputHistoryManager 就在用：
             *    JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
             *    JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
             */

            // 序列化：对象 → JSON 字符串
            var data = new Dictionary<string, List<string>>
            {
                ["dev_addr"] = new() { "1", "2", "3" },
                ["start_addr"] = new() { "0", "100", "0x64" }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            Console.WriteLine("  序列化结果:");
            Console.WriteLine($"  {json}");

            // 反序列化：JSON 字符串 → 对象
            var restored = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            Console.WriteLine($"\n  反序列化: dev_addr = [{string.Join(", ", restored!["dev_addr"])}]");

            // ── 20.5 自定义 JSON 行为 ──
            SubTitle("20.5 JSON 选项");

            var device = new JsonDevice
            {
                DeviceAddress = 1,
                DeviceName = "温度传感器",
                RegisterValues = new ushort[] { 250, 300, 150 }
            };

            // 自定义命名策略（camelCase）
            var camelOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            string camelJson = JsonSerializer.Serialize(device, camelOptions);
            Console.WriteLine("  camelCase:");
            Console.WriteLine($"  {camelJson}");
        }

        class JsonDevice
        {
            public int DeviceAddress { get; set; }
            public string DeviceName { get; set; } = "";
            public ushort[] RegisterValues { get; set; } = Array.Empty<ushort>();
        }


        // ══════════════════════════════════════════════════════════════════
        //  第 21 章：多线程与并发
        // ══════════════════════════════════════════════════════════════════

        public static void Ch21_ThreadingAndConcurrency()
        {
            Title("第 21 章", "多线程与并发");

            // ── 21.1 线程基础 ──
            SubTitle("21.1 线程 = 同时做多件事");
            /*
             *  你的项目中就有多个线程：
             *    - UI 线程：处理按钮点击、更新控件
             *    - 串口线程：SerialPort.DataReceived 事件在后台线程触发
             *    - TCP 线程：Task.Run(TcpReceiveLoop) 在后台运行
             *
             *  所以你需要 SafeInvoke 把数据"搬运"回 UI 线程
             */

            // ── 21.2 lock — 互斥锁 ──
            SubTitle("21.2 lock — 防止多线程同时修改数据");
            /*
             *  你的 InputHistoryManager 就用了 lock：
             *    private readonly object _lock = new();
             *    public void Add(string fieldKey, string value)
             *    {
             *        lock (_lock) { ... }
             *    }
             *
             *  lock 保证同一时刻只有一个线程能进入代码块。
             *  就像厕所的门锁——一个人进去锁上门，其他人等在外面。
             */

            // 模拟多线程安全操作
            var counter = new ThreadSafeCounter();
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 1000; j++)
                        counter.Increment();
                });

            Task.WaitAll(tasks);
            Console.WriteLine($"  最终计数: {counter.Count} (期望: 10000)");

            // ── 21.3 Interlocked — 原子操作 ──
            SubTitle("21.3 Interlocked — 更高效的原子操作");
            /*
             *  对于简单的计数器，Interlocked 比 lock 更高效：
             *    Interlocked.Increment(ref _count);
             *    Interlocked.CompareExchange(ref _value, newValue, oldValue);
             */

            // ── 21.4 SemaphoreSlim — 限流 ──
            SubTitle("21.4 SemaphoreSlim — 限制并发数");
            /*
             *  如果你有 100 个设备要同时读，但串口只有 1 个，
             *  你需要用信号量限制同时只有 1 个操作在进行。
             */

            // ── 21.5 ConcurrentDictionary ──
            SubTitle("21.5 线程安全集合");
            /*
             *  .NET 提供了一组线程安全集合：
             *    ConcurrentDictionary<TKey, TValue>  ← 线程安全的 Dictionary
             *    ConcurrentQueue<T>                  ← 线程安全的 Queue
             *    ConcurrentBag<T>                    ← 线程安全的无序集合
             *
             *  不需要手动 lock，内部已经处理好了。
             */

            var concurrentDict = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            concurrentDict.TryAdd("温度", 250);
            concurrentDict.AddOrUpdate("温度", 1, (key, old) => old + 1);
            Console.WriteLine($"  并发字典: 温度={concurrentDict["温度"]}");

            // ── 21.6 Task.Run 与线程池 ──
            SubTitle("21.6 Task.Run — 使用线程池");
            /*
             *  Task.Run(() => { ... }) 把工作交给线程池。
             *  线程池会自动管理线程的创建和回收。
             *
             *  你项目中的用法：
             *    Task.Run(TcpReceiveLoop);  // 把 TCP 接收循环放到后台
             *
             *  注意：
             *    - 不要在 UI 事件处理中直接用 Task.Run 操作 UI 控件
             *    - 长时间运行的操作（如无限循环）应该用专门的 Task
             */

            // ── 21.7 Task.Delay vs Thread.Sleep ──
            SubTitle("21.7 Task.Delay vs Thread.Sleep");
            /*
             *  Thread.Sleep(100)   → 阻塞当前线程 100ms（浪费线程资源）
             *  await Task.Delay(100) → 不阻塞，100ms 后恢复（推荐）
             *
             *  你项目中的：
             *    Sp_DataReceived 里 Thread.Sleep(50) ← 可以用异步替代
             *    但 DataReceived 事件是同步回调，所以 Thread.Sleep 是合理的
             */

            // ── 21.8 死锁与避免 ──
            SubTitle("21.8 死锁 — 互相等待");
            /*
             *  线程 A 拿着锁 1，等待锁 2
             *  线程 B 拿着锁 2，等待锁 1
             *  → 两个线程永远等下去 = 死锁
             *
             *  避免方法：
             *    1. 总是按相同顺序获取锁
             *    2. 用 lock 的超时版本（Monitor.TryEnter）
             *    3. 尽量减少锁的嵌套
             *    4. 用 async/await 代替手动线程管理
             */

            Console.WriteLine("  ⚠ 死锁是多线程最难调试的 bug，尽量用 async/await 避免");
        }

        // 第 21 章辅助
        class ThreadSafeCounter
        {
            private int _count;
            private readonly object _lock = new();

            public int Count => _count;  // 读取不需要锁

            public void Increment()
            {
                lock (_lock)
                {
                    _count++;
                }
            }
        }
    }
}
