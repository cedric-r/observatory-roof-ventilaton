// <copyright file="Talon6ProtocolTests.cs" company="">
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
using RoofControl.Talon6;

namespace RoofControl.Tests;

public class Talon6ProtocolTests
{
    private static byte[] MakeResponse(params int[] maskedBytes)
    {
        var resp = new byte[maskedBytes.Length + 3];
        resp[0] = (byte)'&';
        resp[1] = (byte)'G';
        for (int i = 0; i < maskedBytes.Length; i++)
            resp[2 + i] = (byte)(maskedBytes[i] | 0x80);
        resp[^1] = (byte)'#';
        return resp;
    }

    [Fact]
    public void ParseResponse_TooShort_ReturnsError()
    {
        Assert.Equal(RoofState.Error, Talon6Protocol.ParseStatusResponse(new byte[3]).State);
    }

    [Fact]
    public void ParseResponse_BadHeader_ReturnsError()
    {
        Assert.Equal(RoofState.Error, Talon6Protocol.ParseStatusResponse("XX#####"u8.ToArray()).State);
    }

    [Fact]
    public void ParseResponse_ClosedState()
    {
        var data = new int[21];
        data[0] = 0x10;  // state=1 (CLOSED), action=0
        data[4] = 0x06; data[5] = 0x19; // voltage
        data[13] = 0x10; // RCL bit 4 at data[13]

        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(data));
        Assert.Equal(RoofState.Closed, status.State);
        Assert.True(status.RoofTotallyClosed);
        Assert.Equal(0, status.PositionTicks);
        Assert.Equal(0.0, status.PositionPercent);
    }

    [Fact]
    public void ParseResponse_OpenState()
    {
        var data = new int[21];
        data[0] = 0x00;  // state=0 (OPEN)
        data[4] = 0x06; data[5] = 0x19; // voltage
        data[13] = 0x08; // ROP bit 3 at data[13]

        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(data));
        Assert.Equal(RoofState.Open, status.State);
        Assert.True(status.RoofTotallyOpen);
    }

    [Fact]
    public void ParseResponse_OpeningState()
    {
        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(
            0x20,  // state=2 (OPENING)
            0x00, 0x00, 0x00, 0x06, 0x19,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        ));
        Assert.Equal(RoofState.Opening, status.State);
    }

    [Fact]
    public void ParseResponse_ClosingState()
    {
        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(
            0x30,  // state=3 (CLOSING)
            0x00, 0x00, 0x00, 0x06, 0x19,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        ));
        Assert.Equal(RoofState.Closing, status.State);
    }

    [Fact]
    public void ParseResponse_ErrorState()
    {
        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(
            0x40,  // state=4 (ERROR)
            0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00
        ));
        Assert.Equal(RoofState.Error, status.State);
    }

    [Fact]
    public void ParseResponse_Voltage()
    {
        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(
            0x10, 0x00, 0x00, 0x00,
            0x06, 0x19,  // → 11.62V
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x10, 0x00
        ));
        Assert.Equal(11.62, status.PowerSupplyVoltage, 1);
    }

    [Fact]
    public void ParseResponse_Position()
    {
        Talon6Protocol.EncoderTicksMax = 50000;
        // position = (25 << 14) | (78 << 7) | 16 = 419600
        var status = Talon6Protocol.ParseStatusResponse(MakeResponse(
            0x10,
            0x19, 0x4E, 0x10,  // ≈3278 ticks after 7-bit packing
            0x06, 0x19,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x10, 0x00
        ));
        // (25 << 14) + (78 << 7) + 16 = 409600 + 9984 + 16 = 419600
        Assert.Equal(419600, status.PositionTicks);
    }

    [Fact]
    public void Constants_AreCorrect()
    {
        Assert.Equal("&G%#", Talon6Protocol.QueryStatus);
        Assert.Equal("&S%#", Talon6Protocol.StopMotion);
        Assert.Equal("&O%#", Talon6Protocol.OpenFully);
        Assert.Equal("&P%#", Talon6Protocol.Close);
    }

    [Fact]
    public void BuildGoToCommand_IncludesPercent()
    {
        var cmd = Talon6Protocol.BuildGoToCommand(25000);
        Assert.StartsWith("&A", cmd);
        Assert.EndsWith("%#", cmd);
    }
}
