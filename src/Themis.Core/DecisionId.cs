using System;

namespace Themis.Core;

/// <summary>A decision's identifier, a UUID of version 7.</summary>
/// <param name="Value">The UUID.</param>
public readonly record struct DecisionId(Guid Value);