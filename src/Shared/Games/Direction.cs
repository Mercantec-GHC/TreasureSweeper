using System.Text.Json.Serialization;

namespace Shared.Games;

[JsonConverter(typeof(JsonStringEnumConverter<Direction>))]
public enum Direction { Up, Right, Down, Left }