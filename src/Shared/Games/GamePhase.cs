using System.Text.Json.Serialization;

namespace Shared.Games;

/// <summary>Where a game is in its round cycle.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GamePhase>))]
public enum GamePhase
{
    Waiting,
    Playing,
    Ended,
}