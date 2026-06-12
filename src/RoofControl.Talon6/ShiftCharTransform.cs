// <copyright file="ShiftCharTransform.cs" company="">
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

namespace RoofControl.Talon6;

/// <summary>
/// Talon6 hex digit shift encoding per INDI driver.
/// Hex nibbles 0-15 → ASCII: 0-9 map to '0'-'9', a-f map to ':' (0x3A) through '?' (0x3F).
/// </summary>
public static class ShiftCharTransform
{
    public static char EncodeNibble(int nibble)
    {
        if (nibble > 9)
            return (char)(0x37 + nibble); // 10 → ':', 15 → '?'
        return (char)(0x30 + nibble);     // 0 → '0', 9 → '9'
    }

    public static int DecodeNibble(char c)
    {
        if (c >= '0' && c <= '9')
            return c - '0';
        if (c >= ':' && c <= '?')
            return c - 0x37;
        return 0;
    }

    /// <summary>
    /// Encode a tick value as a 5-character shifted-hex string for &amp;Axxxxx#.
    /// </summary>
    public static string EncodeTicks(int ticks)
    {
        var hex = ticks.ToString("X5");
        var chars = new char[5];
        for (int i = 0; i < 5; i++)
            chars[i] = EncodeNibble(Convert.ToInt32(hex[i].ToString(), 16));
        return new string(chars);
    }
}
