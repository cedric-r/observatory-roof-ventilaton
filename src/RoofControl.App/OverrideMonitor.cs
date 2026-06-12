// <copyright file="OverrideMonitor.cs" company="">
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
