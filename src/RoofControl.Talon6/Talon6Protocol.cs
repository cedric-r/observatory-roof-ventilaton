// <copyright file="Talon6Protocol.cs" company="">
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

using RoofControl.Core.Models;

namespace RoofControl.Talon6;

/// <summary>
/// Talon6 serial protocol — binary frame commands.
/// Commands: &amp;G%#, &amp;S%#, &amp;O%#, &amp;P%#, &amp;A{5-hex}%#
/// Response: &amp;G + data-bytes + #  (23-25 bytes total, bit 7 set on data)
/// </summary>
public static class Talon6Protocol
{
    public const string QueryStatus = "&G%#";
    public const string StopMotion  = "&S%#";
    public const string OpenFully   = "&O%#";
    public const string Close       = "&P%#";

    public static string BuildGoToCommand(int ticks)
        => $"&A{ShiftCharTransform.EncodeTicks(ticks)}%#";

    public static int EncoderTicksMax { get; set; } = 50000;

    /// <summary>
    /// Parse a binary &amp;G response into <see cref="RoofStatus"/>.
    /// Frame: &amp;/G/{data}/{terminator}
    /// After masking data bytes with 0x7F:
    ///   [0] = upper nibble=state, lower nibble=action
    ///   [1..3] = position (3 bytes, 7-bit packed: [1]&lt;&lt;14 | [2]&lt;&lt;7 | [3])
    ///   [4..5] = voltage ((b1&amp;7)&lt;&lt;7 + b2) * 15 / 1024
    ///   [13] = sensors: bit3=ROP, bit4=RCL
    /// </summary>
    public static RoofStatus ParseStatusResponse(byte[] response)
    {
        if (response.Length < 5)
            return ErrorStatus($"Response too short: {response.Length} bytes");

        if (response[0] != (byte)'&' || response[1] != (byte)'G')
            return ErrorStatus($"Invalid header: '{(char)response[0]}{(char)response[1]}'");

        if (response[^1] != (byte)'#')
            return ErrorStatus("Response missing # terminator");

        // Data bytes between header and terminator (skip &G at start, # at end)
        var raw = new byte[response.Length - 3];
        for (int i = 0; i < raw.Length; i++)
            raw[i] = (byte)(response[2 + i] & 0x7F);

        var stateAction = raw[0];
        var stateNibble = (stateAction >> 4) & 0x0F;
        var actionNibble = stateAction & 0x0F;

        RoofState state = stateNibble switch
        {
            0 => RoofState.Open,
            1 => RoofState.Closed,
            2 => RoofState.Opening,
            3 => RoofState.Closing,
            _ => RoofState.Error
        };

        // Position: 3 bytes at [1..3], 7-bit packed
        var posHi  = raw.Length > 1 ? raw[1] : 0;
        var posMid = raw.Length > 2 ? raw[2] : 0;
        var posLo  = raw.Length > 3 ? raw[3] : 0;
        var positionTicks = (posHi << 14) | (posMid << 7) | posLo;

        // Voltage: bytes [4..5]
        var vHi = raw.Length > 4 ? raw[4] : 0;
        var vLo = raw.Length > 5 ? raw[5] : 0;
        var voltage = ((vHi & 0x07) << 7 | vLo) * 15.0 / 1024.0;

        // Limit switches at [13]: bit3=ROP, bit4=RCL
        var sensors = raw.Length > 13 ? raw[13] : (byte)0;
        var roofTotallyOpen   = (sensors & 0x08) != 0;
        var roofTotallyClosed = (sensors & 0x10) != 0;

        var positionPercent = EncoderTicksMax > 0
            ? Math.Round((double)positionTicks / EncoderTicksMax * 100.0, 1)
            : 0.0;

        string[] actions = [
            "None", "Open by user", "Close by user", "GoTo by user",
            "Calibrate", "Closed due to rain", "Power down", "Comms lost",
            "Internet lost", "Timeout", "Management", "Automation",
            "Motor stalled", "Emergency stop", "Ordered mount park", "Unknown"
        ];

        return new RoofStatus(
            State: state,
            PositionTicks: positionTicks,
            PositionPercent: positionPercent,
            PowerSupplyVoltage: Math.Round(voltage, 2),
            CloudWatcherRelayClosed: false,
            RoofTotallyOpen: roofTotallyOpen,
            RoofTotallyClosed: roofTotallyClosed,
            LastActionCode: actionNibble,
            LastActionDescription: actionNibble < actions.Length ? actions[actionNibble] : "Unknown"
        );
    }

    internal static RoofStatus ErrorStatus(string reason) => new(
        State: RoofState.Error, PositionTicks: 0, PositionPercent: 0,
        PowerSupplyVoltage: 0, CloudWatcherRelayClosed: false,
        RoofTotallyOpen: false, RoofTotallyClosed: false,
        LastActionCode: 0xFF, LastActionDescription: reason
    );
}
