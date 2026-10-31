using System;

namespace Themis.Core;

/// <summary>A request's identifier, a UUID of version 7.</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct RequestId(Guid Value);