using System.ComponentModel.DataAnnotations;

namespace Themis.Operations;

/// <summary>The keys <c>Themis:Database:...</c> (<c>06</c> section 4).</summary>
internal sealed class DatabaseOptions
{
    [Range(0, 10)]
    public int Retries { get; set; } = 3;
}