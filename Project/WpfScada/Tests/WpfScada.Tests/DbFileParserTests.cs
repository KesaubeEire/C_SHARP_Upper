using WpfScada.Models.Plc;
using WpfScada.Services.Plc;

namespace WpfScada.Tests;

public sealed class DbFileParserTests
{
    private static readonly string ConfigDir = @"C:\Users\mz199\Downloads\Electric\临时配置文件";

    [Fact]
    public void Udt_轴控制_解析偏移量正确()
    {
        var udt = UdtFileParser.Parse(Path.Combine(ConfigDir, "轴控制.udt"));

        Assert.Equal("轴控制", udt.UdtName);

        // BOOL 段 #1：6 个 BOOL 打包在 WORD 0（bits 0-5）
        AssertVar(udt, "使能", 0, "BOOL", 1);
        AssertVar(udt, "复位", 0, "BOOL", 1);
        AssertVar(udt, "暂停", 0, "BOOL", 1);
        AssertVar(udt, "回原", 0, "BOOL", 1);
        AssertVar(udt, "点动＋", 0, "BOOL", 1);
        AssertVar(udt, "点动-", 0, "BOOL", 1);

        // BOOL 组结束 → +2（WORD 边界），LREAL 不做 8 对齐 → 偏移 2
        AssertVar(udt, "点动速度", 2, "LREAL", 8);

        // BOOL 段 #2：2 个 BOOL 在 WORD 10（bits 0-1）
        AssertVar(udt, "寸动+", 10, "BOOL", 1);
        AssertVar(udt, "寸动-", 10, "BOOL", 1);

        // +2 = 12
        AssertVar(udt, "寸动距离", 12, "LREAL", 8);
        AssertVar(udt, "寸动速度", 20, "LREAL", 8);

        // BOOL #3
        AssertVar(udt, "相对定位", 28, "BOOL", 1);
        // +2 = 30
        AssertVar(udt, "相对距离", 30, "LREAL", 8);
        AssertVar(udt, "相对速度", 38, "LREAL", 8);

        // BOOL #4
        AssertVar(udt, "绝对定位", 46, "BOOL", 1);
        // +2 = 48
        AssertVar(udt, "绝对位置", 48, "LREAL", 8);
        AssertVar(udt, "绝对速度", 56, "LREAL", 8);

        // BOOL 段 #5：6 个 BOOL 在 WORD 64（bits 0-5）
        AssertVar(udt, "轴使能中", 64, "BOOL", 1);
        AssertVar(udt, "轴暂停中", 64, "BOOL", 1);
        AssertVar(udt, "轴忙碌", 64, "BOOL", 1);
        AssertVar(udt, "回原完成", 64, "BOOL", 1);
        AssertVar(udt, "绝对定位完成", 64, "BOOL", 1);
        AssertVar(udt, "绝对定位中", 64, "BOOL", 1);
        // +2 = 66
        AssertVar(udt, "当前位置", 66, "LREAL", 8);
        AssertVar(udt, "当前速度", 74, "LREAL", 8);

        // 总大小 = 82
        Assert.Equal(82, udt.Variables[^1].Offset + udt.Variables[^1].Size);
    }

    [Fact]
    public void G_DB_解析含UDT引用()
    {
        var db = DbFileParser.Parse(Path.Combine(ConfigDir, "G.db"));

        Assert.Equal("G", db.DbName);
        Assert.Single(db.Variables);
        AssertVar(db, "圆盘", 0, "轴控制", 4);
    }

    [Fact]
    public void 映射DB_全部BOOL正确()
    {
        var db = DbFileParser.Parse(Path.Combine(ConfigDir, "映射DB.db"));

        Assert.Equal("映射DB", db.DbName);
        Assert.All(db.Variables, v => Assert.Equal("BOOL", v.DataType));
    }

    [Fact]
    public void 参数赋值DB_ARRAY偏移正确()
    {
        var db = DbFileParser.Parse(Path.Combine(ConfigDir, "参数赋值.db"));

        Assert.Equal("参数赋值", db.DbName);

        // 步进位置选择P : INT → 偏移 0，2 字节
        AssertVar(db, "步进位置选择P", 0, "INT", 2);

        // 步进点位坐标P : Array[1..10] of REAL → INT 结束于 2，不做 4 对齐 → 偏移 2
        AssertVar(db, "步进点位坐标P", 2, "Array[1..10] of Real", 40);

        // 步进自动速度P : REAL → 42
        AssertVar(db, "步进自动速度P", 42, "REAL", 4);

        // 伺服位置选择P : INT → 46
        AssertVar(db, "伺服位置选择P", 46, "INT", 2);

        // 伺服点位坐标P : Array[1..10] of REAL → 48
        AssertVar(db, "伺服点位坐标P", 48, "Array[1..10] of Real", 40);

        // 伺服自动速度P : REAL → 88
        AssertVar(db, "伺服自动速度P", 88, "REAL", 4);
    }

    [Fact]
    public void A_DB_偏移量正确()
    {
        var db = DbFileParser.Parse(Path.Combine(ConfigDir, "A.db"));
        Assert.Equal("A", db.DbName);

        // 7 个 BOOL 打包在 WORD 0（bits 0-6）
        AssertVar(db, "按钮_正转", 0, "BOOL", 1);
        // BOOL 组结束 → +2 = 2
        AssertVar(db, "设定频率", 2, "REAL", 4);
        // REAL 接 REAL → 6
        AssertVar(db, "VDF_频率", 6, "REAL", 4);
        // 5 个 BOOL 打包在 WORD 10（bits 0-4）
        AssertVar(db, "VDF_运行", 10, "BOOL", 1);
    }

    [Fact]
    public void F_DB_轴控制偏移量正确()
    {
        var db = DbFileParser.Parse(Path.Combine(ConfigDir, "F.db"));
        Assert.Equal("F", db.DbName);

        // BOOL 段 #1：4 个 BOOL 在 WORD 0（bits 0-3）
        AssertVar(db, "轴使能", 0, "BOOL", 1);
        // +2 = 2
        AssertVar(db, "点动速度", 2, "REAL", 4);
        // BOOL 段 #2：3 个 BOOL 在 WORD 6（bits 0-2）
        AssertVar(db, "回原点", 6, "BOOL", 1);
        // +2 = 8
        AssertVar(db, "相对定位距离", 8, "REAL", 4);
        // REAL 接 REAL → 12
        AssertVar(db, "相对定位速度", 12, "REAL", 4);
        // BOOL 段 #3：2 个 BOOL 在 WORD 16（bits 0-1）
        AssertVar(db, "寸动+", 16, "BOOL", 1);
        // +2 = 18
        AssertVar(db, "寸动距离", 18, "REAL", 4);
        // REAL 接 REAL → 22
        AssertVar(db, "寸动速度", 22, "REAL", 4);
        // BOOL #4：1 个 BOOL → WORD 26
        AssertVar(db, "绝对定位执行", 26, "BOOL", 1);
        // +2 = 28
        AssertVar(db, "绝对定位位置", 28, "REAL", 4);
        // REAL 接 REAL → 32
        AssertVar(db, "绝对定位速度", 32, "REAL", 4);
        // BOOL #5：1 个 BOOL → WORD 36
        AssertVar(db, "回原完成", 36, "BOOL", 1);
        // +2 = 38
        AssertVar(db, "位置当前值", 38, "REAL", 4);
        AssertVar(db, "速度当前值", 42, "REAL", 4);
    }

    [Fact]
    public void S7Service_DecodeLReal_大端正确()
    {
        var buf = new byte[] { 0x40, 0x34, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        double result = S7Service.DecodeValue(buf, "LREAL");

        Assert.Equal(20.0, result, precision: 6);
    }

    [Fact]
    public void S7Service_DecodeReal_大端正确()
    {
        var buf = new byte[] { 0x42, 0x48, 0x00, 0x00 };

        double result = S7Service.DecodeValue(buf, "REAL");

        Assert.Equal(50.0, result, precision: 4);
    }

    [Fact]
    public void S7Service_GetDataTypeSize_LREAL返回8()
    {
        Assert.Equal(8, S7Service.GetDataTypeSize("LREAL"));
        Assert.Equal(8, S7Service.GetDataTypeSize("lreal"));
        Assert.Equal(8, S7Service.GetDataTypeSize("LReal"));
    }

    private static void AssertVar(DbStructure db, string name, int offset, string type, int size)
    {
        var v = Assert.Single(db.Variables, x => x.Name == name);
        AssertVar(v, offset, type, size);
    }

    private static void AssertVar(UdtStructure udt, string name, int offset, string type, int size)
    {
        var v = Assert.Single(udt.Variables, x => x.Name == name);
        AssertVar(v, offset, type, size);
    }

    private static void AssertVar(DbVariable v, int offset, string type, int size)
    {
        Assert.Equal(offset, v.Offset);
        Assert.Equal(type, v.DataType);
        Assert.Equal(size, v.Size);
    }
}
