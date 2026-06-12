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
