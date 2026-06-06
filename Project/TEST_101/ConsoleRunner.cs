using System;
using System.Collections.Generic;

namespace TEST_101
{
    /// <summary>
    /// 终端运行入口 — 在命令行执行：
    ///   dotnet run -- chapters        ← 列出所有章节
    ///   dotnet run -- 01              ← 运行第 1 章
    ///   dotnet run -- 01 02 13        ← 运行多章
    ///   dotnet run -- all             ← 运行全部
    /// </summary>
    internal static class ConsoleRunner
    {
        static readonly Dictionary<string, (string title, Action action)> Chapters = new()
        {
            ["01"] = ("变量、类型与表达式",     CSharpMasterGuide.Ch01_TypesAndVariables),
            ["02"] = ("字符串处理",             CSharpMasterGuide.Ch02_Strings),
            ["03"] = ("控制流",                 CSharpMasterGuide.Ch03_ControlFlow),
            ["04"] = ("方法",                   CSharpMasterGuide.Ch04_Methods),
            ["05"] = ("类与对象",               CSharpMasterGuide.Ch05_ClassesAndObjects),
            ["06"] = ("继承与多态",             CSharpMasterGuide.Ch06_Inheritance),
            ["07"] = ("接口",                   CSharpMasterGuide.Ch07_Interfaces),
            ["08"] = ("枚举与结构体",           CSharpMasterGuide.Ch08_EnumsAndStructs),
            ["09"] = ("异常处理",               CSharpMasterGuide.Ch09_Exceptions),
            ["10"] = ("泛型",                   CSharpMasterGuide.Ch10_Generics),
            ["11"] = ("集合",                   CSharpMasterGuide.Ch11_Collections),
            ["12"] = ("委托/Lambda/事件",       CSharpMasterGuide.Ch12_DelegatesLambdaEvents),
            ["13"] = ("LINQ",                   CSharpMasterGuide.Ch13_LINQ),
            ["14"] = ("异步 async/await",       CSharpMasterGuide.Ch14_AsyncAwait),
            ["15"] = ("模式匹配",               CSharpMasterGuide.Ch15_PatternMatching),
            ["16"] = ("记录类型与元组",         CSharpMasterGuide.Ch16_RecordsAndTuples),
            ["17"] = ("可空类型与空安全",       CSharpMasterGuide.Ch17_NullableAndNullSafety),
            ["18"] = ("扩展方法",               CSharpMasterGuide.Ch18_ExtensionMethods),
            ["19"] = ("特性 Attribute",         CSharpMasterGuide.Ch19_Attributes),
            ["20"] = ("文件I/O与JSON",          CSharpMasterGuide.Ch20_FileIOAndJSON),
            ["21"] = ("多线程与并发",           CSharpMasterGuide.Ch21_ThreadingAndConcurrency),
        };

        /// <summary>
        /// 返回 true 表示已作为命令行工具处理（不需要启动 WinForms），
        /// 返回 false 表示没有参数，应启动正常窗体。
        /// </summary>
        public static bool TryRun(string[] args)
        {
            if (args.Length == 0) return false;

            string cmd = args[0].ToLower();

            if (cmd == "chapters" || cmd == "help" || cmd == "?")
            {
                Console.WriteLine();
                Console.WriteLine("  📚 C# 语法全攻略 — 可用章节");
                Console.WriteLine("  ─────────────────────────────────");
                foreach (var (key, (title, _)) in Chapters)
                    Console.WriteLine($"    {key}  {title}");
                Console.WriteLine("  ─────────────────────────────────");
                Console.WriteLine("    all  运行全部章节");
                Console.WriteLine();
                Console.WriteLine("  用法：dotnet run -- 01");
                Console.WriteLine("        dotnet run -- 01 05 13");
                Console.WriteLine("        dotnet run -- all");
                Console.WriteLine();
                return true;
            }

            // 收集要运行的章节
            var toRun = new List<(string key, string title, Action action)>();

            if (cmd == "all")
            {
                foreach (var (key, (title, action)) in Chapters)
                    toRun.Add((key, title, action));
            }
            else
            {
                foreach (string arg in args)
                {
                    string key = arg.ToLower().TrimStart('0').PadLeft(2, '0');
                    // 也接受不带前导零的输入：1 → 01
                    if (Chapters.TryGetValue(key, out var ch))
                        toRun.Add((key, ch.title, ch.action));
                    else if (Chapters.TryGetValue(arg, out ch))
                        toRun.Add((arg, ch.title, ch.action));
                    else
                        Console.WriteLine($"  ⚠ 未知章节: {arg}，用 chapters 查看可用列表");
                }
            }

            if (toRun.Count == 0)
            {
                Console.WriteLine("  没有可运行的章节。用 dotnet run -- chapters 查看列表。");
                return true;
            }

            int passed = 0, failed = 0;
            foreach (var (key, title, action) in toRun)
            {
                Console.WriteLine();
                Console.WriteLine($"════════ 运行第 {key} 章: {title} ════════");
                Console.WriteLine();
                try
                {
                    action();
                    Console.WriteLine($"\n  ✅ 第 {key} 章 — 通过");
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n  ❌ 第 {key} 章 — 失败: {ex.Message}");
                    failed++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"════════ 结果: {passed} 通过, {failed} 失败 ════════");
            Console.WriteLine();
            return true;
        }
    }
}
