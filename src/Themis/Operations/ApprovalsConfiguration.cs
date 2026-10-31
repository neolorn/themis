using Microsoft.Extensions.Configuration;

namespace Themis.Operations;

/// <summary>The configuration section <c>Themis</c> of the configuration given to <c>AddThemis</c>, which the startup checks read raw (<c>04</c> APR-OPS-001).</summary>
/// <param name="Section">The section.</param>
internal sealed record ApprovalsConfiguration(IConfigurationSection Section);