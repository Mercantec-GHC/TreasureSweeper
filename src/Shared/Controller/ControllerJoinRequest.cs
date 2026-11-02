namespace Shared.Controller;

/// <summary>Sent by an Oplà controller to join a game.</summary>
public sealed record ControllerJoinRequest
{
    public string? Pin { get; init; }
    public string? Name { get; init; }
    public string? DeviceId { get; init; }
}