using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TEST_101
{
    /// <summary>
    /// 🔬 C# 高级语法演示 — 基于当前 Modbus 项目的实际代码
    /// 四个主题：委托、事件、Lambda、反射
    /// 每个概念都从"本项目用到的地方"切入，再深入解释
    /// </summary>
    public static class CSharpConceptsDemo
    {
        // ================================================================
        //  第一部分：委托 (Delegate)
        //  ================================================================

        /*
         *  💡 核心类比：委托 = "招聘广告"
         *
         *  传统思维：你打电话给王师傅 → 王师傅来修
         *  委托思维：你贴一张"招空调维修工"的广告 →
         *          任何人只要会修空调，就能填这个位置
         *
         *  关键：调用方不需要知道"具体是谁"，只需要知道"他能干什么"
         */

        /// <summary>
        /// ★ 概念1.1：最原始的 delegate 关键字写法
        /// 定义一个"招聘广告"：接受两个 int，返回 int 的方法
        /// </summary>
        private delegate int Calculator(int a, int b);

        /// <summary>
        /// ★ 概念1.2：本项目中你已经在用的委托
        ///
        /// 看 ModbusTransport.cs 构造函数：
        ///   public ModbusTransport(Control uiControl, Func＜bool＞ isTcpModeCallback)
        ///
        /// Func＜bool＞ 就是一个"无参数、返回 bool"的委托！
        /// 翻译成人话：你传进来一个"能判断当前是不是 TCP 模式"的方法，
        /// Transport 不需要知道这个方法怎么实现，只需要"叫它一声，它回答 true/false"
        /// </summary>

        // ★ 这个属性由 ShowLearnDialog 在调用前注入真实状态
        //    避免写死 false 误导读者
        public static Func<bool>? CurrentIsTcpMode { get; set; }

        public static void Demo01_Delegates_FuncAndAction()
        {
            /*
             * 📖 内置委托速查表：
             *
             * Func<T>           = 无参，返回 T        → "你问他一个问题，他给你答案"
             * Func<T1, T2>      = 1 个参数，返回 T2
             * Func<T1,...,T16,TResult> = 多参数
             *
             * Action            = 无参，无返回        → "我叫你干活，干完拉倒"
             * Action<T>         = 1 个参数，无返回
             * Action<T1..T16>   = 多参数，无返回
             *
             * Predicate<T>      = 1 个参数，返回 bool → "我问你对不对，你回答是/否"
             */

            // ======== 例 1：Func — "我问你答" ========
            // 这行代码的意思是：
            // "我招一个叫 isEven 的工人，他的工作是：给一个 int，回答它是不是偶数"
            Func<int, bool> isEven = num => num % 2 == 0;

            bool result1 = isEven(42);
            Console.WriteLine($"42 是偶数吗？{result1}");  // → True
            // 控制台输出：42 是偶数吗？True


            // ======== 例 2：Action — "叫你去干活" ========
            Action<string> log = message =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            };

            log("这是通过委托记录的日志");  // → [14:32:05] 这是通过委托记录的日志


            // ======== 例 3：对比 —— "委托的参数也可以是委托" ========
            // 这就是 ModbusTransport 做的事情：
            //   构造函数接受一个 Func<bool>，内部用它来获取"当前模式"
            //   构造函数接受一个 Control，内部用它来做 Invoke
            // ★ 这里用注入的真实状态，而不是写死 false
            Func<bool> isCurrentlyTcp = CurrentIsTcpMode ?? (() => false);
            Console.WriteLine($"当前是 TCP 模式吗？{isCurrentlyTcp()}");
            Console.WriteLine($"  （此值来自 ModbusForm 的 _isTcpMode 字段，不是写死的）");


            // ======== 例 4：把"招聘广告"换成"具体的人" ========
            // 定义一个广告：要找一个"int→int"的转换器
            Func<int, int> converter;

            // 方案 A：雇一个叫 DoubleIt 的人
            converter = DoubleIt;
            Console.WriteLine($"DoubleIt(5) = {converter(5)}");  // → 10

            // 方案 B：解雇 DoubleIt，雇一个叫 TripleIt 的人
            //         ——接口没变，干活的人换了
            converter = TripleIt;
            Console.WriteLine($"TripleIt(5) = {converter(5)}");  // → 15
        }

        private static int DoubleIt(int x) => x * 2;
        private static int TripleIt(int x) => x * 3;

        // ================================================================
        //  第二部分：Lambda 表达式 (Lambda Expression)
        //  ================================================================

        /*
         *  💡 核心类比：Lambda = "便利贴"
         *
         *  如果这个方法只用一次、逻辑很简单，
         *  不值得专门起个名字、写在文件的另一个角落——
         *  直接写在使用的地方，像贴一张便利贴。
         *
         *  你项目中大量使用 lambda：
         *    btn.Click += (s, e) => { ... };           ← 便利贴
         *    _transport.FrameReceived += OnFrameReceived;  ← 正规方法
         */

        public static void Demo02_Lambda_ThreeWays()
        {
            // ==== 三种写法，效果完全一样 ====

            int[] numbers = { 1, 2, 3, 4, 5 };

            // 写法 1：完整的 lambda（参数类型明确）
            var evens1 = Array.FindAll(numbers, (int n) => { return n % 2 == 0; });

            // 写法 2：省略类型和花括号（C# 能自动推断）
            var evens2 = Array.FindAll(numbers, n => n % 2 == 0);

            // 写法 3：传统命名方法（老派写法）
            var evens3 = Array.FindAll(numbers, IsEven);

            Console.WriteLine($"写法1: {string.Join(",", evens1)}");  // → 2,4
            Console.WriteLine($"写法2: {string.Join(",", evens2)}");  // → 2,4
            Console.WriteLine($"写法3: {string.Join(",", evens3)}");  // → 2,4


            // ==== Lambda 进阶：闭包 (Closure) — 捕获外部变量 ====

            int threshold = 3;  // ← lambda 内部可以直接用这个变量！
            // (threshold,s,e) 这三个参数保持原样——threshold 叫"闭包变量"
            var aboveThreshold = Array.FindAll(numbers, n => n > threshold);
            Console.WriteLine($"大于 {threshold} 的数: {string.Join(",", aboveThreshold)}");
        }

        private static bool IsEven(int n) => n % 2 == 0;

        // ================================================================
        //  第三部分：事件 (Event)
        //  ================================================================

        /*
         *  💡 核心类比：事件 = "公众号订阅"
         *
         *  公众号（事件发布者）：我发了新文章
         *  订阅者（事件处理者）：我收到了通知
         *
         *  关键三条规则：
         *  1. 发布者不知道谁订阅了它（解耦）
         *  2. 订阅者可以随时取消（取消关注）
         *  3. 只有发布者自己能"发文章"（触发事件）
         *
         *  你项目中：
         *    发布者 = ModbusTransport（设备返回了数据）
         *    订阅者 = ModbusForm（我要更新 UI）
         *    事件   = FrameReceived
         */

        // ---- 为演示创建一个迷你版发布者 ----
        private class Sensor
        {
            // ★ 这就是事件的定义：
            //    public event Action<float> TemperatureChanged;
            //
            //    翻译成人话：
            //    "我(Sensor)有一个叫 TemperatureChanged 的事件
            //     任何人只要有一个接受 float 的方法，就能订阅
            //     当温度变化时，我会调用所有订阅者的方法"

            public event Action<float>? TemperatureChanged;

            private float _temperature;

            public float Temperature
            {
                get => _temperature;
                set
                {
                    _temperature = value;
                    // ★ 触发事件：通知所有订阅者
                    TemperatureChanged?.Invoke(value);
                    //                ^^^^^^ 这个 ? 的意思是"如果有人订阅才触发，没人就跳过"
                }
            }
        }

        // ---- 模拟一个显示屏（订阅者） ----
        private class Display
        {
            public string Name { get; }

            public Display(string name)
            {
                Name = name;
            }

            public void OnTemperatureChanged(float newTemp)
            {
                Console.WriteLine($"[{Name}] 收到通知：温度变为 {newTemp:F1}°C");
            }
        }

        public static void Demo03_Events_PubSub()
        {
            var sensor = new Sensor();

            // 创建两个显示屏
            var display1 = new Display("车间东侧显示屏");
            var display2 = new Display("车间西侧显示屏");

            // ★ 订阅事件（关注公众号）
            // TemperatureChanged += OnTemperatureChanged;
            sensor.TemperatureChanged += display1.OnTemperatureChanged;
            sensor.TemperatureChanged += display2.OnTemperatureChanged;

            Console.WriteLine("--- 温度变化，两个显示屏都收到 ---");
            sensor.Temperature = 25.5f;
            // 输出：
            // [车间东侧显示屏] 收到通知：温度变为 25.5°C
            // [车间西侧显示屏] 收到通知：温度变为 25.5°C

            // ★ 取消订阅（取关）
            Console.WriteLine("\n--- 取关西侧显示屏后 ---");
            sensor.TemperatureChanged -= display2.OnTemperatureChanged;

            sensor.Temperature = 30.0f;
            // 只剩东侧显示屏收到
            // 输出：[车间东侧显示屏] 收到通知：温度变为 30.0°C


            // ★★★ 对应到你项目中的实际代码 ★★★
            Console.WriteLine("\n--- 对应项目代码 ---");
            Console.WriteLine("ModbusTransport.cs 第 34 行：");
            Console.WriteLine("  public event Action<byte[], bool>? FrameReceived;");
            Console.WriteLine("  ↑ 发布者定好一个'协议'：我会传给你 byte[] 和 bool");
            Console.WriteLine();
            Console.WriteLine("ModbusForm.cs ModbusForm_Load：");
            Console.WriteLine("  _transport.FrameReceived += OnFrameReceived;");
            Console.WriteLine("  ↑ 订阅者说：我准备好了，数据一来就调我的 OnFrameReceived");
            Console.WriteLine();
            Console.WriteLine("ModbusTransport.cs SafeInvoke 里：");
            Console.WriteLine("  FrameReceived?.Invoke(buffer, false);");
            Console.WriteLine("  ↑ 发布者触发：所有订阅者，数据到了！");
        }

        // ================================================================
        //  第四部分：反射 (Reflection)
        //  ================================================================

        /*
         *  💡 核心类比：反射 = "X 光机"
         *
         *  你拿到一个编译好的 DLL（黑盒子），
         *  反射让你能看透这个盒子：
         *    - 里面有哪些类？
         *    - 每个类有哪些方法？方法的参数是什么？
         *    - 有哪些字段？是 public 还是 private？
         *    - 甚至能动态调用 private 方法
         */

        /// <summary>
        /// ★ 直接扫描我们项目中的 ModbusProtocol 类，展示反射的实际用途
        /// </summary>
        public static string Demo04_Reflection_ScanOurProtocol()
        {
            var lines = new List<string>();

            // ★ 第一步：拿到"类型对象"——这是反射的入口
            Type protocolType = typeof(ModbusProtocol);

            lines.Add("═══════════════════════════════════════");
            lines.Add("   🔬 X 光扫描：ModbusProtocol 类");
            lines.Add("═══════════════════════════════════════");
            lines.Add($"类名: {protocolType.Name}");
            lines.Add($"命名空间: {protocolType.Namespace}");
            lines.Add($"是静态类吗？{protocolType.IsAbstract && protocolType.IsSealed}");
            lines.Add("");

            // ★ 第二步：列出所有常量
            lines.Add("── 常量 ──");
            foreach (var field in protocolType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && !field.IsInitOnly) // IsLiteral = const
                {
                    lines.Add($"  const {field.FieldType.Name} {field.Name} = {field.GetValue(null)}");
                }
            }
            lines.Add("");

            // ★ 第三步：列出所有 public 方法及其参数
            lines.Add("── Public 方法 ──");
            foreach (var method in protocolType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                         .Where(m => !m.IsSpecialName)) // 排除 get_/set_ 属性访问器
            {
                var parameters = string.Join(", ",
                    method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                lines.Add($"  {method.ReturnType.Name} {method.Name}({parameters})");
            }
            lines.Add("");

            // ★ 第四步：列出解析结果类型的属性
            lines.Add("── ModbusParseResult 的属性 ──");
            Type parseResultType = typeof(ModbusParseResult);
            foreach (var prop in parseResultType.GetProperties())
            {
                lines.Add($"  {prop.PropertyType.Name} {prop.Name}");
            }

            // ★★★ 进阶：用反射动态调用一个方法 ★★★
            lines.Add("");
            lines.Add("── 动态调用演示 ──");

            var calcCrcMethod = protocolType.GetMethod("CalcCRC",
                BindingFlags.Public | BindingFlags.Static);

            if (calcCrcMethod != null)
            {
                byte[] testData = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
                // ★ 这就是反射的核心：用字符串名字来调用方法
                byte[] crc = (byte[])calcCrcMethod.Invoke(null, new object[] { testData })!;
                lines.Add($"  CalcCRC(01030000000A) = {crc[0]:X2} {crc[1]:X2}");
            }

            // 只返回字符串，不内部 Console.WriteLine（调用方会打印）
            return string.Join("\n", lines);
        }

        // ================================================================
        //  第五部分：综合案例 — 四个概念在一个场景里协同工作
        //  ================================================================

        /*
         *  场景：Modbus 数据过滤器
         *
         *  需求：收到 Modbus 寄存器数据后，允许用户自定义过滤规则
         *
         *  用到的概念：
         *    委托  → 过滤规则本身就是一个委托
         *    Lambda → 用 lambda 定义过滤规则
         *    事件  → 过滤完成后通知
         *    反射  → 动态列出可用的过滤规则
         */

        /// <summary>
        /// 综合演示：Modbus 数据过滤器
        /// </summary>
        public class ModbusDataFilter
        {
            // ★ 委托：定义一个"过滤规则"——收一个 ushort，判它是否通过
            public delegate bool FilterRule(ushort value);

            // ★ 事件：过滤完成后触发
            public event Action<int, int>? FilterCompleted;

            // ★ 委托字段：当前生效的过滤规则
            private FilterRule? _activeFilter;

            /// <summary>设置过滤规则（委托注入）★</summary>
            public void SetFilter(FilterRule rule)
            {
                _activeFilter = rule;
            }

            /// <summary>对寄存器列表执行过滤（Lambda ⚡）★</summary>
            public List<ushort> Apply(List<ushort> registers)
            {
                if (_activeFilter == null) return registers;

                // ★ 这里用到了 Lambda + LINQ：
                //   Where(v => _activeFilter(v)) 等价于
                //   "遍历每个值，问 _activeFilter 这个委托：它通过吗？"
                var result = registers.Where(v => _activeFilter(v)).ToList();

                // ★ 触发事件：通知"过滤完了"
                FilterCompleted?.Invoke(registers.Count, result.Count);

                return result;
            }

            // ★ 反射：列出我们预置的所有过滤规则（当作"工具箱"展示）★
            public static List<(string Name, FilterRule Rule)> GetBuiltInFilters()
            {
                var filters = new List<(string, FilterRule)>();

                // 遍历当前类的所有静态方法，找出返回 FilterRule 或与其签名兼容的方法
                var methods = typeof(ModbusDataFilter).GetMethods(
                    BindingFlags.Public | BindingFlags.Static);

                foreach (var m in methods)
                {
                    // 检查这个方法是否匹配 FilterRule 的签名
                    if (m.ReturnType == typeof(bool) &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(ushort) &&
                        m.Name.StartsWith("Filter"))
                    {
                        // ★ 用反射把一个方法包装成委托
                        var rule = (FilterRule)Delegate.CreateDelegate(
                            typeof(FilterRule), m);
                        filters.Add((m.Name, rule));
                    }
                }

                // 再加一个 Lambda 直接定义的规则（不用反射，直接写）
                filters.Add(("Lambda: 只保留 0", v => v == 0));

                return filters;
            }

            // ---- 以下是一些预置过滤规则 ----

            /// <summary>过滤规则：值大于 0</summary>
            public static bool FilterAboveZero(ushort v) => v > 0;

            /// <summary>过滤规则：值是偶数</summary>
            public static bool FilterEven(ushort v) => v % 2 == 0;

            /// <summary>过滤规则：值在 100~1000 之间</summary>
            public static bool FilterMidRange(ushort v) => v >= 100 && v <= 1000;
        }

        public static void Demo05_AllTogether()
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("   🧩 综合案例：Modbus 数据过滤器");
            Console.WriteLine("═══════════════════════════════════════");

            var filter = new ModbusDataFilter();

            // ----- 第 1 步：用反射列出所有可用规则 -----
            Console.WriteLine("\n① 反射：列出内置过滤规则");
            var builtInFilters = ModbusDataFilter.GetBuiltInFilters();
            foreach (var (name, _) in builtInFilters)
                Console.WriteLine($"  可用规则: {name}");

            // ----- 第 2 步：用委托 + Lambda 设置规则 -----
            Console.WriteLine("\n② 委托：设置过滤规则为 '值大于 0'");
            // ★ 三种等价写法，效果完全一样：
            // filter.SetFilter(ModbusDataFilter.FilterAboveZero);  // 方法组
            filter.SetFilter(v => v > 0);                            // ★ Lambda

            // ----- 第 3 步：订阅事件 -----
            Console.WriteLine("③ 事件：订阅过滤完成通知");
            // ★ Lambda 再次出现：定义一个"收到通知后干什么"
            filter.FilterCompleted += (total, passed) =>
            {
                Console.WriteLine($"  📢 过滤完成：{total} 个寄存器 → {passed} 个通过");
            };

            // ----- 第 4 步：执行 -----
            Console.WriteLine("\n④ 执行过滤");
            var data = new List<ushort> { 0, 150, 0, 250, 0, 500, 1200 };
            Console.WriteLine($"  原始数据: [{string.Join(", ", data)}]");

            var filtered = filter.Apply(data);
            Console.WriteLine($"  过滤结果: [{string.Join(", ", filtered)}]");
            // 输出: [150, 250, 500, 1200]
        }

        // ================================================================
        //  第六部分：对比总结 — 什么时候用哪个
        //  ================================================================

        /// <summary>
        /// 打印一张速查表
        /// </summary>
        public static string GetQuickReference()
        {
            return @"
╔══════════╦═══════════════════════════════════════════════════════════╗
║  概念    ║  一句话理解                                               ║
╠══════════╬═══════════════════════════════════════════════════════════╣
║  委托    ║  把方法当作参数传来传去                                   ║
║ Delegate ║  「你不需要知道是谁，只需要知道他能不能干这个活」         ║
╠══════════╬═══════════════════════════════════════════════════════════╣
║  Lambda  ║  不值得起名字的小方法，就地写                             ║
║          ║  n => n > 0  翻译成人话：「给我一个数，回答是否大于0」    ║
╠══════════╬═══════════════════════════════════════════════════════════╣
║  事件    ║  公众号 + 订阅者 模式                                     ║
║  Event   ║  发布者只管发，订阅者只管收，互不依赖                     ║
╠══════════╬═══════════════════════════════════════════════════════════╣
║  反射    ║  X 光机                                                   ║
║Reflection║  运行时看透一个 DLL/类内部的所有结构，甚至调用私有方法   ║
╚══════════╩═══════════════════════════════════════════════════════════╝

📌 你现在项目中实际用的：
  ┌────────────────────┬─────────────────────────────────────┐
  │ 概念               │ 项目中的位置                        │
  ├────────────────────┼─────────────────────────────────────┤
  │ 委托 (Func<bool>)  │ ModbusTransport 构造函数参数        │
  │ Lambda             │ btn.Click += (s,e) => { ... }       │
  │ 事件               │ FrameReceived / ConnectionChanged    │
  │ 反射               │ （本项目暂时没用到，但上面有示例）  │
  └────────────────────┴─────────────────────────────────────┘
";
        }
    }
}
