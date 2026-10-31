namespace Themis.Operations;

/// <summary>
/// Registered by <c>AddThemisTesting</c> so that no hosted job runs in a test's host, whichever
/// of it and <c>AddThemis</c> is called first: a test runs the jobs on demand alone
/// (<c>03</c> APR-LIB-007).
/// </summary>
internal sealed class HostedJobsOff;