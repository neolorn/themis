using System;

namespace Themis.Core;

/// <summary>An event's identifier, a UUID of version 7.</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct ApprovalEventId(Guid Value);