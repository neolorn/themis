namespace Themis.Types;

/// <summary>
/// A row of the type table: an approval type a host has published, and whether it is retired
/// now (<c>02</c> APR-TYPE-001). Its versions are rows of their own, never changed.
/// </summary>
internal sealed class TypeState
{
    public required string Key { get; set; }

    /// <summary>Whether a new request of the type is refused; a publish makes it current again.</summary>
    public bool Retired { get; set; }
}