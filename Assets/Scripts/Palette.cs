using System.Collections.Generic;

public static class Palette
{
    public static readonly LightColor W = new LightColor(1f / 3f, 1f / 3f, 1f / 3f);

    private static readonly string[] Ids =
    {
        "R", "G", "B", "C", "M", "Y",
        "R+C", "R+M", "R+Y", "G+C", "G+M", "G+Y",
        "B+C", "B+M", "B+Y",
        "W"
    };

    private static readonly LightColor[] Colors =
    {
        new LightColor(1f, 0f, 0f),
        new LightColor(0f, 1f, 0f),
        new LightColor(0f, 0f, 1f),
        new LightColor(0f, 0.5f, 0.5f),
        new LightColor(0.5f, 0f, 0.5f),
        new LightColor(0.5f, 0.5f, 0f),
        new LightColor(0.5f, 0.25f, 0.25f),
        new LightColor(0.75f, 0f, 0.25f),
        new LightColor(0.75f, 0.25f, 0f),
        new LightColor(0f, 0.75f, 0.25f),
        new LightColor(0.25f, 0.5f, 0.25f),
        new LightColor(0.25f, 0.75f, 0f),
        new LightColor(0f, 0.25f, 0.75f),
        new LightColor(0.25f, 0f, 0.75f),
        new LightColor(0.25f, 0.25f, 0.5f),
        W
    };

    private static readonly Dictionary<string, LightColor> ById = BuildLookup();

    public static IEnumerable<KeyValuePair<string, LightColor>> All
    {
        get
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                yield return new KeyValuePair<string, LightColor>(Ids[i], Colors[i]);
            }
        }
    }

    public static bool TryGet(string id, out LightColor color)
    {
        if (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out color))
        {
            return true;
        }

        color = W;
        return false;
    }

    public static LightColor Get(string id)
    {
        return TryGet(id, out LightColor color) ? color : W;
    }

    public static (string id, LightColor color) NearestOrWhite(LightColor color)
    {
        string bestId = null;
        LightColor bestColor = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < Ids.Length; i++)
        {
            float distance = ColorMath.Distance(color, Colors[i]);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestId = Ids[i];
                bestColor = Colors[i];
            }
        }

        if (bestId != null && bestDistance <= ColorMath.SnapEpsilon)
        {
            return (bestId, bestColor);
        }

        return ("W", W);
    }

    private static Dictionary<string, LightColor> BuildLookup()
    {
        var lookup = new Dictionary<string, LightColor>(Ids.Length);

        for (int i = 0; i < Ids.Length; i++)
        {
            lookup[Ids[i]] = Colors[i];
        }

        return lookup;
    }
}
