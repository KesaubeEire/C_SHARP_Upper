using WpfScada.Services.Plc;

namespace WpfScada.Tests;

public sealed class S7ServiceTests
{
    [Fact]
    public void ReadByteWhenDisconnectedReturnsNullAndSetsLastError()
    {
        using var service = new S7Service();

        byte? value = service.ReadByte(S7Service.AreaI, 0);

        Assert.Null(value);
        Assert.False(string.IsNullOrWhiteSpace(service.LastError));
    }

    [Fact]
    public void ReadBytesWhenDisconnectedDoesNotFillFailedValuesWithZero()
    {
        using var service = new S7Service();

        var values = service.ReadBytes(S7Service.AreaI, [0, 1]);

        Assert.Empty(values);
        Assert.False(string.IsNullOrWhiteSpace(service.LastError));
    }

    [Fact]
    public void DisconnectClearsLastError()
    {
        using var service = new S7Service();
        _ = service.ReadByte(S7Service.AreaI, 0);
        Assert.False(string.IsNullOrWhiteSpace(service.LastError));

        service.Disconnect();

        Assert.Null(service.LastError);
    }

    [Fact]
    public void InvalidLocalBindConnectReturnsMinusOneAndSetsLastError()
    {
        using var service = new S7Service();

        int result = service.Connect("not-an-ip", "127.0.0.1", 102, 0, 1);

        Assert.Equal(-1, result);
        Assert.False(string.IsNullOrWhiteSpace(service.LastError));
    }
}
