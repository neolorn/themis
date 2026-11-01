using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Themis.Endpoints.Tests;

/// <summary>Keeps every entry any logger of the host writes, in order.</summary>
internal sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> entries = new();

    internal IReadOnlyList<LogEntry> Entries => [.. entries];

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(categoryName, entries);
    }

    public void Dispose()
    {
    }

    /// <summary>One entry.</summary>
    /// <param name="Category">The logger's category.</param>
    /// <param name="Message">The entry's message, formatted, with its exception where it has one.</param>
    internal sealed record LogEntry(string Category, string Message);

    private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new LogEntry(category, exception is null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}"));
        }
    }
}