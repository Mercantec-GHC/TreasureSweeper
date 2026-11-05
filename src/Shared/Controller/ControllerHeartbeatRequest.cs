namespace Shared.Controller;

public sealed record ControllerHeartbeatRequest
{
    public string? Pin { get; init; }
    public string? PlayerId { get; init; }
    public string? DeviceId { get; init; }
}