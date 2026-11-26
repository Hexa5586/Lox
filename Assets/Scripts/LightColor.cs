using UnityEngine;

public readonly struct LightColor
{
    public readonly float R;
    public readonly float G;
    public readonly float B;

    public LightColor(float r, float g, float b)
    {
        R = r;
        G = g;
        B = b;
    }

    public float Total => R + G + B;

    public static LightColor Black => new LightColor(0f, 0f, 0f);

    public static LightColor operator +(LightColor a, LightColor b)
        => new LightColor(a.R + b.R, a.G + b.G, a.B + b.B);

    public static LightColor operator *(LightColor color, float scale)
        => new LightColor(color.R * scale, color.G * scale, color.B * scale);

    public bool ApproximatelyEquals(LightColor other, float epsilon = 1e-3f)
        => Mathf.Abs(R - other.R) <= epsilon
           && Mathf.Abs(G - other.G) <= epsilon
           && Mathf.Abs(B - other.B) <= epsilon;
}

public readonly struct MixResult
{
    public readonly LightColor Color;
    public readonly string ColorId;
    public readonly float Brightness;
    public readonly LightColor Final;

    public MixResult(LightColor color, string colorId, float brightness)
    {
        Color = color;
        ColorId = colorId;
        Brightness = brightness;
        Final = color * brightness;
    }
}
