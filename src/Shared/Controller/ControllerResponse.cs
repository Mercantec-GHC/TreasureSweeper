using System.Text.Json.Serialization;

namespace Shared.Controller;

/// <summary>Reply to every controller request. Field names are fixed by the controller firmware (#10).</summary>
public sealed record ControllerResponse(
    bool Ok,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? PlayerId = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] int? Number = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] bool? Reconnected = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? Error = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? Message = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Event = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Color = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Facing = null
)
{
    public static ControllerResponse Joined(string playerId, string name, int number, string color, bool reconnected) =>
        new(true, playerId, name, number, reconnected ? true : null, Color: color);

    public static ControllerResponse Fail(string error) => new(false, Error: error);

    public static ControllerResponse Success() => new(true);

    public static ControllerResponse Rejected(string message) => new(false, Message: message);

    public static ControllerResponse Scanned(bool onMine, int count = 0) =>
        new(true, Event: onMine ? ControllerEvent.MineAhead : ControllerEvent.None, Count: count);

    public static ControllerResponse Dug(string @event) => new(true, Event: @event);

    public static ControllerResponse Stepped(string @event) => new(true, Event: @event);
}

/// <summary>What happened to the player. The firmware decides how to show it.</summary>
public static class ControllerEvent
{
    public const string None = "none";
    public const string MineAhead = "mineAhead";
    public const string Treasure = "treasure";
    public const string PointMine = "pointMine";
    public const string FreezeMine = "freezeMine";
}