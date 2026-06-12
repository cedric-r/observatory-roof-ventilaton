using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RoofControl.Core.Models;

namespace RoofControl.App;

/// <summary>
/// Persists roof state and hysteresis recovery data to a JSON file.
/// Used to restore last-known state across restarts (power-loss recovery).
/// </summary>
public sealed class StatePersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<StatePersistence> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public StatePersistence(ILogger<StatePersistence> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;
    }

    public async Task<PersistedState> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("No state file found at {Path}, using defaults", _filePath);
                return new PersistedState();
            }

            var json = await File.ReadAllTextAsync(_filePath, ct);
            var state = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
            if (state is null)
            {
                _logger.LogWarning("Failed to deserialize state file, using defaults");
                return new PersistedState();
            }

            _logger.LogInformation("Loaded state: position={PosTicks} ticks, weather={WeatherOk}",
                state.LastKnownPositionTicks, state.LastWeatherOK);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading state file");
            return new PersistedState();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(PersistedState state, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
            _logger.LogDebug("State saved to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving state file");
        }
        finally
        {
            _lock.Release();
        }
    }
}

public class PersistedState
{
    public string? LastKnownState { get; set; } = "Closed";
    public int LastKnownPositionTicks { get; set; } = 0;
    public bool LastWeatherOK { get; set; } = true;
    public DateTime? LastDecisionTime { get; set; }
}
