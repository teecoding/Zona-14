namespace Content.Shared._Zona14.Humanoid;

public static class StalkerHairPalette
{
    public const float MaxSaturation = 0.55f;
    public const float MinValue = 0.05f;
    public const float MaxValue = 0.95f;

    public static Color Clamp(Color color)
    {
        var hsv = Color.ToHsv(color);
        hsv.Y = Math.Clamp(hsv.Y, 0f, MaxSaturation);
        hsv.Z = Math.Clamp(hsv.Z, MinValue, MaxValue);
        return Color.FromHsv(hsv);
    }
}
