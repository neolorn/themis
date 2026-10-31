using System;
using System.Threading;

namespace Observatory.Tests;

/// <summary>A clock a test moves; its timers run on the system's.</summary>
internal sealed class ClockFake(DateTimeOffset start) : TimeProvider
{
    private long ticks = start.UtcTicks;

    internal void Advance(TimeSpan by)
    {
        Interlocked.Add(ref ticks, by.Ticks);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(Interlocked.Read(ref ticks), TimeSpan.Zero);
    }
}