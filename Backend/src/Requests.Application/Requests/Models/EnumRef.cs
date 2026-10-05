namespace Requests.Application.Requests;

/// <summary>
/// A reference-data value carried as both its numeric id and its human-readable name, so the
/// client can key logic on the id while displaying (or localizing) the name.
/// </summary>
public sealed record EnumRef(int Id, string Name);
