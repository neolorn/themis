using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Tests;

/// <summary>An alert sink that keeps every raise and clear, in the order called.</summary>
internal sealed class AlertSinkRecorder : IAlertSink
{
    private readonly Lock gate = new();

    private readonly List<AlertCall> calls = [];

    internal AlertCall[] Calls
    {
        get
        {
            lock (gate)
            {
                return [.. calls];
            }
        }
    }

    public ValueTask RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
    {
        Add(new AlertCall(true, condition, scope, new Dictionary<string, string>(details)));
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string condition, string? scope, CancellationToken cancellationToken)
    {
        Add(new AlertCall(false, condition, scope, new Dictionary<string, string>()));
        return ValueTask.CompletedTask;
    }

    private void Add(AlertCall call)
    {
        lock (gate)
        {
            calls.Add(call);
        }
    }
}

/// <summary>A raise, or a clear where <paramref name="Raised"/> is false, of a condition in a scope.</summary>
internal sealed record AlertCall(bool Raised, string Condition, string? Scope, IReadOnlyDictionary<string, string> Details);