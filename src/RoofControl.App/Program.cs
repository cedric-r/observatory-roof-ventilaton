// <copyright file="Program.cs" company="">
// Copyright (c) 2026 Cedric Raguenaud
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoofControl.App;
using RoofControl.Core.Configuration;
using RoofControl.Core.Interfaces;
using RoofControl.Decision;
using RoofControl.Talon6;
using RoofControl.Weather;
using Serilog;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("ROOFCONTROL_")
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine("logs", "roofcontrol-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("RoofControl starting");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            // Bind and validate configuration
            var config = BindConfiguration(context.Configuration, services);
            services.AddSingleton(config);

            // HTTP client for weather fetching
            services.AddHttpClient(nameof(HttpWeatherReader), client =>
            {
                client.Timeout = TimeSpan.FromSeconds(config.WeatherSource.TimeoutSeconds);
            });
            services.AddSingleton<IWeatherReader>(sp =>
            {
                var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var client = clientFactory.CreateClient(nameof(HttpWeatherReader));
                return new HttpWeatherReader(
                    client,
                    sp.GetRequiredService<ILogger<HttpWeatherReader>>(),
                    config.WeatherSource.Url,
                    config.WeatherSource.Format);
            });

            // Talon6 controller (singleton — shared by RoofWorker and SafetyWatchdog)
            services.AddSingleton<IRoofController>(sp =>
            {
                var talon = config.Talon6;
                return new Talon6Controller(
                    sp.GetRequiredService<ILogger<Talon6Controller>>(),
                    talon.PortName,
                    talon.BaudRate,
                    talon.EncoderTicksMax,
                    talon.SafetyEnabled,
                    talon.ReadTimeoutSeconds,
                    talon.WriteTimeoutSeconds);
            });

            // Decision engine
            services.AddSingleton<IDecisionEngine>(sp =>
            {
                var rules = config.RoofRules.Daytime;
                var conditions = rules.OpenConditions;
                return new DecisionEngine(
                    sp.GetRequiredService<ILogger<DecisionEngine>>(),
                    config.Timezone,
                    rules.StartTime,
                    rules.EndTime,
                    rules.TargetOpenPercent,
                    rules.NightfallHysteresisSeconds,
                    conditions.MinAmbientTemp,
                    conditions.MaxAmbientTemp,
                    conditions.SkyTempMin,
                    conditions.SkyTempMax,
                    conditions.MaxHumidity,
                    conditions.RainThreshold,
                    conditions.RainSafetyThreshold,
                    conditions.WindThreshold,
                    config.Hysteresis.CloseDelaySeconds,
                    ignoreCloudWatcherSafe: conditions.IgnoreCloudWatcherSafe);
            });

            // State persistence
            services.AddSingleton(sp =>
                new StatePersistence(
                    sp.GetRequiredService<ILogger<StatePersistence>>(),
                    config.Serialization.StateFilePath));

            // Override monitor
            services.AddSingleton(sp =>
                new OverrideMonitor(
                    sp.GetRequiredService<ILogger<OverrideMonitor>>(),
                    config.Override.FilePath));

            // ESC key monitor — closes roof on ESC press
            services.AddHostedService<EscShutdownMonitor>();

            // Background services
            services.AddHostedService<RoofWorker>();
            services.AddHostedService(sp =>
            {
                var controller = sp.GetRequiredService<IRoofController>();
                var weatherReader = sp.GetRequiredService<IWeatherReader>();
                var logger = sp.GetRequiredService<ILogger<SafetyWatchdog>>();
                return new SafetyWatchdog(
                    logger,
                    controller,
                    weatherReader,
                    TimeSpan.FromSeconds(config.FailSafe.MaxWeatherAgeSeconds),
                    config.FailSafe.MaxRetries,
                    config.FailSafe.BaseDelayMs);
            });
        })
        .Build();

    // Graceful shutdown: stop roof motion, persist state
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    var loggerShutdown = host.Services.GetRequiredService<ILogger<Program>>();

    lifetime.ApplicationStopping.Register(async () =>
    {
        loggerShutdown.LogInformation("Application stopping — closing roof");

        var controller = host.Services.GetRequiredService<IRoofController>();
        try
        {
            // Close the roof (safety-aware Park command) — this also handles
            // stopping any in-progress motion before transitioning to closed.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await controller.CloseAsync(cts.Token);
            loggerShutdown.LogInformation("Roof closed");
        }
        catch (Exception ex)
        {
            loggerShutdown.LogWarning(ex, "Error closing roof during shutdown");
        }

        // Persist current state
        try
        {
            var status = await controller.GetStatusAsync(CancellationToken.None);
            var statePersistence = host.Services.GetRequiredService<StatePersistence>();
            await statePersistence.SaveAsync(new PersistedState
            {
                LastKnownState = status.State.ToString(),
                LastKnownPositionTicks = status.PositionTicks
            });
            loggerShutdown.LogInformation("State persisted for next startup");
        }
        catch (Exception ex)
        {
            loggerShutdown.LogWarning(ex, "Error persisting state during shutdown");
        }
    });

    // Also handle Ctrl+C via Console.CancelKeyPress
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        loggerShutdown.LogInformation("Ctrl+C pressed — initiating shutdown");
        lifetime.StopApplication();
    };

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static RoofControlConfig BindConfiguration(IConfiguration configuration, IServiceCollection services)
{
    var config = new RoofControlConfig();
    configuration.Bind(config);

    // Validate using data annotations
    var context = new System.ComponentModel.DataAnnotations.ValidationContext(config);
    var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
    if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(config, context, results, validateAllProperties: true))
    {
        var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
        throw new InvalidOperationException($"Configuration validation failed: {errors}");
    }

    // Custom cross-field validation
    ValidateConfig(config);

    return config;
}

static void ValidateConfig(RoofControlConfig config)
{
    var rules = config.RoofRules.Daytime;

    if (rules.OpenConditions.MinAmbientTemp >= rules.OpenConditions.MaxAmbientTemp)
        throw new InvalidOperationException(
            $"MinAmbientTemp ({rules.OpenConditions.MinAmbientTemp}) must be < MaxAmbientTemp ({rules.OpenConditions.MaxAmbientTemp})");

    if (rules.StartTime == rules.EndTime)
        throw new InvalidOperationException($"StartTime ({rules.StartTime}) must not equal EndTime ({rules.EndTime})");

    if (string.IsNullOrEmpty(config.Talon6.PortName))
        throw new InvalidOperationException("Talon6 PortName must not be empty");
}
