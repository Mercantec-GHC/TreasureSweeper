
using System.Text.Json.Serialization;
namespace Shared.Games;

/// <summary>
/// A player as shown on the shared screen.
/// Values use the Domain TileContent bit layout. Scanned tiles include their content and count;
/// unscanned tiles are 0; null = outside the board.
/// </summary>
public sealed record PlayerResponse(
    string Id,
    int Number,
    string Name,
    string Color,
    string Symbol,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PositionResponse? Position = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Direction? Facing = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int?>? View = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Score = null
);