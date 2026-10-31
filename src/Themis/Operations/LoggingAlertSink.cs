using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Themis.Core;

namespace Themis.Operations;

/// <summary>The library's own alert sink, used where the host registers none: it logs each condition at the error level (<c>03</c> APR-LIB-005; <c>04</c> APR-OPS-006).</summary>
internal sealed partial class LoggingAlertSink(ILogger<LoggingAlertSink> logger) : IAlertSink
{
    public ValueTask RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(details);
        Raised(condition, scope ?? "none", string.Join(", ", details.OrderBy(detail => detail.Key, StringComparer.Ordinal).Select(detail => $"{detail.Key} {detail.Value}")));
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string condition, string? scope, CancellationToken cancellationToken)
    {
        Cleared(condition, scope ?? "none");
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(EventId = 1, EventName = "AlertRaised", Level = LogLevel.Error, Message = "Alert raised: {Condition}, scope {Scope} ({Details}).")]
    private partial void Raised(string condition, string scope, string details);

    [LoggerMessage(EventId = 2, EventName = "AlertCleared", Level = LogLevel.Error, Message = "Alert cleared: {Condition}, scope {Scope}.")]
    private partial void Cleared(string condition, string scope);
}