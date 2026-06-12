using Microsoft.Extensions.Logging;

namespace RoofControl.Decision;

/// <summary>
/// Debounces transient weather or time transitions to prevent rapid toggling.
/// A condition must remain true for the entire hysteresis window before it triggers.
/// </summary>
public sealed class HysteresisTracker
{
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly TimeSpan _window;
    private DateTime? _windowStart;
    private bool _lastStableState;

    public HysteresisTracker(ILogger logger, string name, TimeSpan window)
    {
        _logger = logger;
        _name = name;
        _window = window;
    }

    /// <summary>
    /// Evaluate whether a condition has been true long enough to be considered stable.
    /// </summary>
    /// <param name="currentValue">The current reading of the condition.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>True when the condition has been continuously true for the hysteresis window.</returns>
    public bool Evaluate(bool currentValue, DateTime now)
    {
        if (!currentValue)
        {
            // Condition cleared — reset the timer
            _windowStart = null;
            _lastStableState = false;
            return false;
        }

        // Condition is true: start or continue the timer
        _windowStart ??= now;

        var elapsed = now - _windowStart.Value;
        if (elapsed >= _window && !_lastStableState)
        {
            _lastStableState = true;
            _logger.LogInformation(
                "Hysteresis [{Name}]: condition stable after {Elapsed}s, triggering",
                _name, elapsed.TotalSeconds);
        }

        return _lastStableState;
    }

    /// <summary>
    /// Reset the tracker to its initial state.
    /// </summary>
    public void Reset()
    {
        _windowStart = null;
        _lastStableState = false;
    }
}
