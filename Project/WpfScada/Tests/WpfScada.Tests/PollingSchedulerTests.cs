using Microsoft.Extensions.Logging;
using WpfScada.Models.Plc;
using WpfScada.Services.Plc;

namespace WpfScada.Tests;

public sealed class PollingSchedulerTests
{
    [Fact]
    public void TickRecordsFastAreaReadFailureWithoutPublishingZero()
    {
        var store = new PollingStore();
        var client = new FakePlcClient { IsConnected = true, LastError = "read failed" };
        client.EnqueueRead(null);

        var scheduler = CreateScheduler(store, client);
        scheduler.Config.Fast.PollIAddr = "0";

        scheduler.TickForTests();

        Assert.Equal(1, store.ConsecutiveFailures);
        Assert.Contains("I0-0", store.LastError);
        Assert.Null(scheduler.GetValue("I0"));
        Assert.Single(client.ReadCalls);
    }

    [Fact]
    public void TickResetsFailureStateAfterSuccessfulRetry()
    {
        var store = new PollingStore();
        var client = new FakePlcClient { IsConnected = true, LastError = "first failure" };
        client.EnqueueRead(null);
        client.EnqueueRead([42]);

        var scheduler = CreateScheduler(store, client);
        scheduler.Config.Fast.PollIAddr = "0";

        scheduler.TickForTests();
        scheduler.TickForTests();

        Assert.Equal(0, store.ConsecutiveFailures);
        Assert.Null(store.LastError);
        Assert.NotNull(store.LastSuccessAt);
        Assert.Equal((byte?)42, scheduler.GetValue("I0"));
        Assert.Equal(2, client.ReadCalls.Count);
    }

    [Fact]
    public void TickSkipsPollingDuringBackoffWindow()
    {
        var store = new PollingStore
        {
            ConsecutiveFailures = 1,
            TotalTicks = 0,
        };
        var client = new FakePlcClient { IsConnected = true };

        var scheduler = CreateScheduler(store, client);
        scheduler.Config.Fast.PollIAddr = "0";

        scheduler.TickForTests();

        Assert.Equal(1, store.TotalTicks);
        Assert.Empty(client.ReadCalls);
    }

    [Fact]
    public void TickTracksLongCycle()
    {
        var store = new PollingStore();
        var client = new FakePlcClient { IsConnected = true, ReadDelay = TimeSpan.FromMilliseconds(15) };
        client.EnqueueRead([7]);

        var scheduler = CreateScheduler(store, client);
        scheduler.Config.FastInterval = 1;
        scheduler.Config.Fast.PollIAddr = "0";

        scheduler.TickForTests();

        Assert.True(store.LastDurationMs >= 10);
        Assert.Equal(1, store.LongCycleCount);
        Assert.Equal((byte?)7, scheduler.GetValue("I0"));
    }

    [Fact]
    public void TickRecordsDbReadFailure()
    {
        var store = new PollingStore();
        var client = new FakePlcClient { IsConnected = true, LastError = "db failed", AlwaysFailReads = true };

        var scheduler = CreateScheduler(store, client);
        scheduler.Config.DbItems.Add(new DbPollItem
        {
            DbNumber = 1,
            Offset = 6,
            DataType = "REAL",
            Enabled = true,
        });

        scheduler.TickForTests();

        Assert.Equal(1, store.ConsecutiveFailures);
        Assert.Contains("DB1[6]", store.LastError);
        Assert.Equal("错误: db failed", scheduler.Config.DbItems[0].Status);
    }

    private static PollingScheduler CreateScheduler(PollingStore store, FakePlcClient client)
    {
        var scheduler = new PollingScheduler(new TestLogger<PollingScheduler>(), store);
        scheduler.AttachForTests(client);
        return scheduler;
    }

    private sealed class FakePlcClient : IPlcClient
    {
        private readonly Queue<byte[]?> _reads = new();

        public bool IsConnected { get; init; }
        public string? LastError { get; set; }
        public TimeSpan ReadDelay { get; init; }
        public bool AlwaysFailReads { get; init; }
        public List<(int Area, int Start, int Count, int DbNumber)> ReadCalls { get; } = [];

        public void EnqueueRead(byte[]? value) => _reads.Enqueue(value);

        public byte[]? ReadBytesRaw(int area, int start, int count, int dbNumber = 0)
        {
            if (ReadDelay > TimeSpan.Zero)
                Thread.Sleep(ReadDelay);

            ReadCalls.Add((area, start, count, dbNumber));
            if (AlwaysFailReads)
                return null;

            return _reads.Count > 0 ? _reads.Dequeue() : new byte[count];
        }

        public bool WriteByte(int area, int byteAddress, byte value, int dbNumber = 0) => true;
    }
}

internal sealed class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
    }
}
