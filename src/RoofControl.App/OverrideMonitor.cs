using Microsoft.Extensions.Logging;

namespace RoofControl.App;

/// <summary>
/// Monitors a file-based override flag. When the file exists, automated roof commands are suppressed.
/// Monitoring continues for logging/alerts, but no motor commands are issued.
/// </summary>
public sealed class OverrideMonitor
{
    private readonly ILogger<OverrideMonitor> _logger;
    private readonly string _filePath;
    private bool _lastState;

    public OverrideMonitor(ILogger<OverrideMonitor> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    /// <summary>
    /// Check whether operator override is active.
    /// </summary>
    public bool IsOverridden()
    {
        var exists = File.Exists(_filePath);
        if (exists != _lastState)
        {
            if (exists)
                _logger.LogWarning("Operator override ACTIVE — file detected at {Path}", _filePath);
            else
                _logger.LogInformation("Operator override removed — resuming automated control");
            _lastState = exists;
        }
        return exists;
    }
}
