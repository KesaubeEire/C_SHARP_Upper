#r "WPF_S71200_SAMPLE.csproj"
using TestWpf.Models;
using TestWpf.Services;
using System.IO;

var files = new[] {
    @"C:\Users\admin\Documents\Automation\A.db",
    @"C:\Users\admin\Documents\Automation\B.db",
    @"C:\Users\admin\Documents\Automation\C.db",
    @"C:\Users\admin\Documents\Automation\F.db",
    @"C:\Users\admin\Documents\Automation\G.db",
    @"C:\Users\admin\Documents\Automation\H.db",
    @"C:\Users\admin\Documents\Automation\J.db",
    @"C:\Users\admin\Documents\Automation\手动.db",
    @"C:\Users\admin\Documents\Automation\星三角数据类型.udt",
    @"C:\Users\admin\Documents\Automation\轴控制.udt",
};

foreach (var f in files) 
{
    Console.WriteLine($"=== {Path.GetFileName(f)} ===");
    try
    {
        if (f.EndsWith(".udt", StringComparison.OrdinalIgnoreCase))
        {
            var u = UdtFileParser.Parse(f);
            Console.WriteLine($"  UDT: {u.UdtName}");
            Console.WriteLine($"  Error: {u.ParseError ?? "(ok)"}");
            Console.WriteLine($"  HasUnknown: {u.HasUnknownType}");
            Console.WriteLine($"  Vars: {u.Variables.Count}");
            foreach (var v in u.Variables.Take(3))
                Console.WriteLine($"    {v}");
        }
        else
        {
            var d = DbFileParser.Parse(f);
            Console.WriteLine($"  DB: {d.DbName}, Number: {d.DbNumber}");
            Console.WriteLine($"  Error: {d.ParseError ?? "(ok)"}");
            Console.WriteLine($"  HasUnknown: {d.HasUnknownType}");
            Console.WriteLine($"  Vars: {d.Variables.Count}");
            foreach (var v in d.Variables.Take(5))
                Console.WriteLine($"    {v}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  CRASH: {ex.Message}");
    }
    Console.WriteLine();
}
