using System;

namespace Themis.Core;

/// <summary>A delegation's identifier, a UUID of version 7.</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct DelegationId(Guid Value);