using System;
using System.Threading;

namespace Themis.Endpoints.Tests;

/// <summary>A clock a test sets and moves; its timers run on the system's.</summary>
internal sealed class ClockFake(DateTimeOffset start) : TimeProvider
{
    private long ticks = start.UtcTicks;

    internal DateTimeOffset Now
    {
        get => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        set => Interlocked.Exchange(ref ticks, value.UtcTicks);
    }

    internal void Advance(TimeSpan by)
    {
        Interlocked.Add(ref ticks, by.Ticks);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}