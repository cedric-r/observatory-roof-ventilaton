using RoofControl.Core.Models;

namespace RoofControl.Core.Interfaces;

public interface IRoofController
{
    Task<RoofStatus> GetStatusAsync(CancellationToken ct);
    Task GoToPositionAsync(double percent, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
    Task OpenFullyAsync(CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
}
