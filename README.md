# RoofControl — Observatory Roof Controller

A .NET application that reads weather data from an AAG CloudWatcher (via HTTP), evaluates conditions (humidity, cloud coverage, ambient temperature, sky temperature, time of day), and controls a Talon6 roll-off roof to ventilate and dry the observatory interior during the day.

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     RoofControl.App                          │
│  ┌──────────┐  ┌──────────────┐  ┌─────────────────────────┐ │
│  │RoofWorker│  │SafetyWatchdog│  │StatePersistence         │ │
│  │(orchest.)│  │(fail-safe)   │  │(power-loss recovery)    │ │
│  └────┬─────┘  └──────┬───────┘  └─────────────────────────┘ │
│       │                │                                      │
├───────┴────────────────┴──────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────────┐ │
│  │DecisionEngine│  │WeatherReader │  │Talon6Controller     │ │
│  │(rule matrix) │  │(HTTP/parser) │  │(serial protocol)    │ │
│  └──────────────┘  └──────────────┘  └─────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

## Project Structure

```
RoofControl/
├── RoofControl.slnx
├── src/
│   ├── RoofControl.Core/           # Models, Interfaces, Configuration
│   ├── RoofControl.Weather/        # HTTP weather reader, key=value parser
│   ├── RoofControl.Talon6/         # Talon6 serial protocol & controller
│   ├── RoofControl.Decision/       # Decision engine, hysteresis, cloud classifier
│   └── RoofControl.App/            # Background workers, DI, Program.cs
├── tests/
│   └── RoofControl.Tests/          # xUnit tests (54 tests)
└── README.md
```

## Configuration

Configuration is in `appsettings.json`. Key sections:

| Section | Description |
|---------|-------------|
| `Timezone` | Local timezone for day/night detection |
| `Talon6` | Serial port settings, encoder ticks max, safety interlock |
| `WeatherSource` | CloudWatcher URL (`/cgi-bin/cgiLastData`), format, timeout |
| `Polling` | Weather/status/decision intervals in seconds |
| `RoofRules.Daytime` | Daytime window (`StartTime`/`EndTime`), open target %, conditions |
| `RoofRules.Daytime.OpenConditions` | Temp, sky temp, humidity, rain, wind thresholds |
| `Hysteresis` | Debounce delays to prevent rapid toggling |
| `FailSafe` | Max weather age, serial retry limits |
| `Serialization` | State file path for power-loss recovery |
| `Override` | File-based operator override flag path |

## How It Works

1. **Daytime** (configurable `StartTime`–`EndTime`): weather conditions are evaluated.
   - ALL must be true to open the roof:
     - Ambient temperature within `[MinAmbientTemp, MaxAmbientTemp]`
     - Humidity below `MaxHumidity`
     - No rain (ADC below `RainThreshold`, safety status 0)
     - Wind below `WindThreshold`
     - Sky temperature within `[SkyTempMin, SkyTempMax]` (clear skies)
     - CloudWatcher overall `safe` flag = 1
   - If conditions OK: open roof to `TargetOpenPercent` (e.g., 20%)
   - If conditions fail: close roof (with hysteresis debounce)

2. **Nighttime**: close roof (with configurable nightfall hysteresis).

3. **Safety watchdog**: independently monitors weather staleness, serial health, and roof error state. Initiates fail-safe close on trouble.

## Building

```bash
dotnet build RoofControl.slnx
```

## Testing

```bash
dotnet test RoofControl.slnx
```

## Running

```bash
dotnet run --project src/RoofControl.App --configuration Release
```

On first run, update `appsettings.json`:
1. Set `WeatherSource.Url` to your CloudWatcher IP
2. Set `Talon6.PortName` to your serial port
3. Adjust timezone and thresholds for your location

## Safety Features

| Hazard | Mitigation |
|--------|-----------|
| Stale weather data | Timestamp check; close after 90s (configurable) |
| Serial comms loss | Retry with backoff; close after N failures |
| Motor stall | On stall action code, abort immediately |
| Power-loss recovery | Query position on startup; persist state |
| Rain during opening | Checked before every GoTo decision |
| Transient conditions | Hysteresis timer before closing |
| Config validation | Validated at startup with explicit errors |
| ESC key | Closes roof and exits gracefully |
| Human override | File-based override flag suppresses automation |
| Serial concurrency | SemaphoreSlim queue with CancellationToken |
| Night-open hazard | Close on EndTime with hysteresis |
| Graceful shutdown | Stop roof motion, persist state, exit cleanly |
