using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Themis.Tests;

/// <summary>Keeps every entry the library's loggers write, in order, or every logger's where asked.</summary>
internal sealed class LogCapture(bool everyCategory = false) : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> entries = new();

    /// <summary>The entries written so far by a logger whose category is the library's, or by any logger where asked, each with its exception.</summary>
    internal IReadOnlyList<LogEntry> Entries => [.. entries];

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(categoryName, everyCategory, entries);
    }

    public void Dispose()
    {
    }

    /// <summary>One entry.</summary>
    /// <param name="Category">The logger's category.</param>
    /// <param name="Level">The entry's level.</param>
    /// <param name="Message">The entry's message, formatted.</param>
    internal sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class Logger(string category, bool everyCategory, ConcurrentQueue<LogEntry> entries) : ILogger
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
            if (everyCategory || category.StartsWith("Themis.", StringComparison.Ordinal))
            {
                entries.Enqueue(new LogEntry(category, logLevel, exception is null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}"));
            }
        }
    }
}