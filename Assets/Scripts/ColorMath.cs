using System.Collections.Generic;
using UnityEngine;

public static class ColorMath
{
    public const float SnapEpsilon = 0.05f;
    public const float MatchEpsilon = 1e-3f;
    public const float MinBrightness = 1e-4f;

    public static MixResult Mix(IReadOnlyList<LightColor> beams)
    {
        if (beams == null || beams.Count == 0)
        {
            return new MixResult(Palette.W, "W", 0f);
        }

        float brightnessSum = 0f;
        LightColor normalizedSum = LightColor.Black;
        int count = 0;

        for (int i = 0; i < beams.Count; i++)
        {
            LightColor beam = beams[i];
            float total = beam.Total;

            if (total <= 1e-6f)
            {
                continue;
            }

            brightnessSum += total;
            normalizedSum += beam * (1f / total);
            count++;
        }

        if (count == 0)
        {
            return new MixResult(Palette.W, "W", 0f);
        }

        float averageBrightness = brightnessSum / count;
        LightColor mixedAtFull = normalizedSum * (1f / count);

        var (paletteId, paletteColor) = Palette.NearestOrWhite(mixedAtFull);
        return new MixResult(paletteColor, paletteId, averageBrightness);
    }

    public static float Distance(LightColor a, LightColor b)
    {
        float dr = a.R - b.R;
        float dg = a.G - b.G;
        float db = a.B - b.B;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    public static LightColor ApplyFilter(LightColor beam, LightColor filter)
    {
        float max = Mathf.Max(filter.R, Mathf.Max(filter.G, filter.B));

        if (max <= 1e-6f)
        {
            return LightColor.Black;
        }

        return new LightColor(
            filter.R >= max - 1e-6f ? beam.R : 0f,
            filter.G >= max - 1e-6f ? beam.G : 0f,
            filter.B >= max - 1e-6f ? beam.B : 0f);
    }

    public static bool Matches(MixResult mix, string targetColorId)
        => mix.Brightness > MinBrightness
           && mix.ColorId == targetColorId;
}
