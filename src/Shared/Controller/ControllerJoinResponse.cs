using System.Text.Json.Serialization;

namespace Shared.Controller;

/// <summary>Reply to a controller join. Field names are fixed by the controller firmware (#10).</summary>
public sealed record ControllerJoinResponse(
    bool Ok,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? PlayerId = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] int? Number = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] bool? Reconnected = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? Error = null,
    [property: JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)] string? Message = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Light = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Color = null
)
{
    public static ControllerJoinResponse Joined(string playerId, string name, int number, string color, bool reconnected) =>
        new(true, playerId, name, number, reconnected ? true : null, Color: color);

    public static ControllerJoinResponse Fail(string error) => new(false, Error: error);

    public static ControllerJoinResponse Success() => new(true);

    public static ControllerJoinResponse Rejected(string message) => new(false, Message: message);

	public static ControllerJoinResponse Scanned(bool onMine, int count = 0) =>
		new(true, Light: onMine ? ControllerLight.Red : ControllerLight.Off, Count: count);
}

public static class ControllerLight
{
    public const string Off = "off";
    public const string Red = "red";
}