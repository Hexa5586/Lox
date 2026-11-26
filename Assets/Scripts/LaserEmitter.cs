using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LaserEmitter : MonoBehaviour
{
    private const int MaxBounces = 12;
    private const float FaceBound = 0.43f;
    private const float SurfaceOffset = 0.0005f;
    private const float SplitThreshold = 1e-5f;

    [SerializeField] private string colorId;
    [SerializeField] private string beamId;

    private LightColor beamColor;
    private Quaternion baseRotation;
    private Vector2 baseAim;
    private Vector2 configuredAim;
    private bool hasConfiguredAim;
    private LineRenderer beam;
    private Material beamMaterialTemplate;
    private Material beamGlowTemplate;
    private LaserMirror[] mirrors;
    private LaserConcaveMirror[] concaveMirrors;
    private LaserWall[] walls;
    private LaserLens[] lenses;
    private LaserFilter[] filters;
    private LaserPrism[] prisms;
    private LaserReceiver[] receivers;
    private LaserLock[] locks;
    private LaserLock laserLock;
    private readonly List<Vector3> segmentStarts = new List<Vector3>(8);
    private readonly List<Vector3> segmentEnds = new List<Vector3>(8);
    private readonly List<LightColor> segmentColors = new List<LightColor>(8);
    private readonly List<LineRenderer> beamSegments = new List<LineRenderer>(8);
    private readonly List<LineRenderer> glowSegments = new List<LineRenderer>(8);

    private const float GlowWidthScale = 3.0f;
    private const float GlowAlpha = 0.28f;

    public string ColorId => colorId;

    public void Configure(string id, string beamName = null, Vector2? initialAim = null)
    {
        colorId = id;
        beamId = string.IsNullOrEmpty(beamName) ? id : beamName;
        beamColor = ResolveColor();

        hasConfiguredAim = initialAim.HasValue
            && initialAim.Value.sqrMagnitude > 0.000001f;
        configuredAim = hasConfiguredAim
            ? initialAim.Value.normalized
            : Vector2.zero;
    }

    public void RebaseAim()
    {
        baseRotation = transform.localRotation;

        if (hasConfiguredAim)
        {
            baseAim = configuredAim;
            return;
        }

        Vector2 outward = new Vector2(transform.localPosition.x, transform.localPosition.y);
        baseAim = outward.sqrMagnitude > 0.000001f ? -outward.normalized : Vector2.down;
    }

    private void Awake()
    {
        beamColor = ResolveColor();
        laserLock = GetComponent<LaserLock>();
        RebaseAim();

        var assets = Resources.Load<LevelAssets>("LevelAssets");
        beamGlowTemplate = assets == null ? null : assets.FindMaterial("BeamGlow");

        string beamName = string.IsNullOrEmpty(beamId) ? colorId : beamId;
        var beamTransform = transform.parent != null
            ? transform.parent.Find($"Beam_{beamName}")
            : null;

        if (beamTransform != null)
        {
            beam = beamTransform.GetComponent<LineRenderer>();

            if (beam != null)
            {
                if (beam.sharedMaterial != null)
                {
                    beamMaterialTemplate = new Material(beam.sharedMaterial);
                    beam.sharedMaterial = new Material(beamMaterialTemplate);
                }

                beamSegments.Add(beam);
                glowSegments.Add(CreateGlow(beam.transform, beam));
            }
        }

        if (transform.parent != null)
        {
            // The level loader builds every emitter in the same frame. Including inactive
            // objects here makes the cache complete even when the emitters are activated
            // one after another; all of them are active by the first Update.
            mirrors = transform.parent.GetComponentsInChildren<LaserMirror>(true);
            concaveMirrors = transform.parent.GetComponentsInChildren<LaserConcaveMirror>(true);
            walls = transform.parent.GetComponentsInChildren<LaserWall>(true);
            lenses = transform.parent.GetComponentsInChildren<LaserLens>(true);
            filters = transform.parent.GetComponentsInChildren<LaserFilter>(true);
            prisms = transform.parent.GetComponentsInChildren<LaserPrism>(true);
            receivers = transform.parent.GetComponentsInChildren<LaserReceiver>(true);
            locks = transform.parent.GetComponentsInChildren<LaserLock>(true);
        }
        else
        {
            mirrors = new LaserMirror[0];
            concaveMirrors = new LaserConcaveMirror[0];
            walls = new LaserWall[0];
            lenses = new LaserLens[0];
            filters = new LaserFilter[0];
            prisms = new LaserPrism[0];
            receivers = new LaserReceiver[0];
            locks = new LaserLock[0];
        }
    }

    private void Update()
    {
        if (laserLock != null && laserLock.IsBlocking)
        {
            HideBeam();
            return;
        }

        Quaternion delta = transform.localRotation * Quaternion.Inverse(baseRotation);
        Vector3 aim3 = delta * new Vector3(baseAim.x, baseAim.y, 0f);
        Vector2 aim = new Vector2(aim3.x, aim3.y);

        if (aim.sqrMagnitude < 0.000001f)
        {
            HideBeam();
            return;
        }

        aim.Normalize();

        Vector2 origin = (Vector2)transform.localPosition + aim * transform.localScale.y;

        segmentStarts.Clear();
        segmentEnds.Clear();
        segmentColors.Clear();

        TraceBeam(origin, aim, beamColor, 0, null);
        RenderBeam();
    }

    /// <summary>
    /// Traces one beam path. A prism splits a composite beam into independent R/G/B
    /// component paths, each of which continues with its own refractive index.
    /// </summary>
    private void TraceBeam(
        Vector2 origin,
        Vector2 aim,
        LightColor currentColor,
        int bounces,
        LaserPrism insidePrism)
    {
        while (bounces < MaxBounces)
        {
            bounces++;

            float nearest = FaceExitDistance(origin, aim);
            HitKind kind = HitKind.Boundary;
            Vector2 normal = Vector2.up;
            Vector2 lensDirection = Vector2.zero;
            LightColor nextColor = currentColor;
            LaserReceiver hitReceiver = null;
            LaserPrism hitPrism = null;
            Vector2 prismNormal = Vector2.up;

            if (insidePrism != null)
            {
                if (!insidePrism.TryIntersect(origin, aim, out nearest, out prismNormal))
                {
                    return;
                }

                kind = HitKind.Prism;
                hitPrism = insidePrism;
            }
            else
            {
                if (mirrors != null)
                {
                    foreach (LaserMirror mirror in mirrors)
                    {
                        if (mirror.TryIntersect(origin, aim, out float distance, out Vector2 hitNormal)
                            && distance < nearest)
                        {
                            nearest = distance;
                            kind = HitKind.Mirror;
                            normal = hitNormal;
                        }
                    }
                }

                if (concaveMirrors != null)
                {
                    foreach (LaserConcaveMirror concaveMirror in concaveMirrors)
                    {
                        if (concaveMirror.TryIntersect(
                                origin,
                                aim,
                                out float distance,
                                out Vector2 hitNormal,
                                out bool reflective)
                            && distance < nearest)
                        {
                            nearest = distance;
                            kind = reflective ? HitKind.Mirror : HitKind.Wall;
                            normal = hitNormal;
                        }
                    }
                }

                if (walls != null)
                {
                    foreach (LaserWall wall in walls)
                    {
                        if (wall.TryIntersect(origin, aim, out float distance) && distance < nearest)
                        {
                            nearest = distance;
                            kind = HitKind.Wall;
                        }
                    }
                }

                if (lenses != null)
                {
                    foreach (LaserLens lens in lenses)
                    {
                        if (lens.TryRefract(origin, aim, out float distance, out Vector2 outgoing)
                            && distance < nearest)
                        {
                            nearest = distance;
                            kind = HitKind.Lens;
                            lensDirection = outgoing;
                        }
                    }
                }

                if (filters != null)
                {
                    foreach (LaserFilter filter in filters)
                    {
                        if (filter.TryFilter(
                                origin,
                                aim,
                                currentColor,
                                out float distance,
                                out LightColor outgoing,
                                out bool blocked)
                            && distance < nearest)
                        {
                            nearest = distance;
                            kind = blocked ? HitKind.FilterBlocked : HitKind.Filter;
                            nextColor = outgoing;
                        }
                    }
                }

                if (prisms != null)
                {
                    foreach (LaserPrism prism in prisms)
                    {
                        if (prism.TryIntersect(origin, aim, out float distance, out Vector2 hitNormal)
                            && distance < nearest)
                        {
                            nearest = distance;
                            kind = HitKind.Prism;
                            hitPrism = prism;
                            prismNormal = hitNormal;
                        }
                    }
                }

                if (locks != null)
                {
                    foreach (LaserLock faceLock in locks)
                    {
                        if (faceLock != null
                            && faceLock.TryIntersect(origin, aim, out float distance)
                            && distance < nearest)
                        {
                            nearest = distance;
                            kind = HitKind.Lock;
                        }
                    }
                }

                if (receivers != null)
                {
                    foreach (LaserReceiver receiver in receivers)
                    {
                        Vector2 center = receiver.HitCenter;
                        float distance = RayCircle(origin, aim, center, receiver.HitRadius);

                        if (distance > 0f && distance < nearest)
                        {
                            nearest = distance;
                            kind = HitKind.Receiver;
                            hitReceiver = receiver;
                        }
                    }
                }
            }

            Vector2 end = origin + aim * nearest;
            AddSegment(origin, end, currentColor);

            if (kind == HitKind.Prism && hitPrism != null)
            {
                if (insidePrism == null)
                {
                    SplitIntoComponents(hitPrism, end, aim, prismNormal, currentColor, bounces);
                }
                else
                {
                    ExitPrism(hitPrism, end, aim, prismNormal, currentColor, bounces);
                }

                return;
            }

            if (kind == HitKind.Receiver)
            {
                if (hitReceiver != null)
                {
                    hitReceiver.AddBeam(currentColor);
                }

                return;
            }

            if (kind == HitKind.Boundary || kind == HitKind.Wall || kind == HitKind.FilterBlocked || kind == HitKind.Lock)
            {
                return;
            }

            if (kind == HitKind.Filter)
            {
                currentColor = nextColor;
                origin = end + aim * SurfaceOffset;
                continue;
            }

            if (kind == HitKind.Lens)
            {
                aim = lensDirection.normalized;
                origin = end + aim * SurfaceOffset;
                continue;
            }

            aim = Vector2.Reflect(aim, normal).normalized;
            origin = end + aim * SurfaceOffset;
        }
    }

    private void SplitIntoComponents(
        LaserPrism prism,
        Vector2 entryPoint,
        Vector2 direction,
        Vector2 outwardNormal,
        LightColor color,
        int bounces)
    {
        for (int component = LaserPrism.ComponentR; component <= LaserPrism.ComponentB; component++)
        {
            LightColor componentColor = LaserPrism.ComponentColor(color, component);

            if (componentColor.Total <= SplitThreshold)
            {
                continue;
            }

            float eta = 1f / prism.IndexFor(component);

            if (!LaserPrism.Refract(direction, outwardNormal, eta, out Vector2 refracted))
            {
                continue;
            }

            TraceBeam(
                entryPoint + refracted * SurfaceOffset,
                refracted,
                componentColor,
                bounces,
                prism);
        }
    }

    private void ExitPrism(
        LaserPrism prism,
        Vector2 exitPoint,
        Vector2 direction,
        Vector2 outwardNormal,
        LightColor color,
        int bounces)
    {
        int component = LaserPrism.ComponentIndex(color);
        float eta = prism.IndexFor(component);

        if (!LaserPrism.Refract(direction, -outwardNormal, eta, out Vector2 refracted))
        {
            Vector2 reflected = Vector2.Reflect(direction, outwardNormal).normalized;
            TraceBeam(
                exitPoint + reflected * SurfaceOffset,
                reflected,
                color,
                bounces,
                prism);
            return;
        }

        TraceBeam(
            exitPoint + refracted * SurfaceOffset,
            refracted,
            color,
            bounces,
            null);
    }

    private void AddSegment(Vector2 start, Vector2 end, LightColor color)
    {
        segmentStarts.Add(new Vector3(start.x, start.y, 0.05f));
        segmentEnds.Add(new Vector3(end.x, end.y, 0.05f));
        segmentColors.Add(color);
    }

    private void RenderBeam()
    {
        int segmentCount = segmentColors.Count;

        for (int i = 0; i < segmentCount; i++)
        {
            LineRenderer segment = GetBeamSegment(i);

            if (segment == null)
            {
                return;
            }

            segment.positionCount = 2;
            segment.SetPosition(0, segmentStarts[i]);
            segment.SetPosition(1, segmentEnds[i]);

            if (segment.sharedMaterial != null)
            {
                segment.sharedMaterial.SetColor("_Color", DisplayColor(segmentColors[i]));
            }

            if (i < glowSegments.Count && glowSegments[i] != null)
            {
                LineRenderer glow = glowSegments[i];
                glow.positionCount = 2;
                glow.SetPosition(0, segmentStarts[i]);
                glow.SetPosition(1, segmentEnds[i]);

                if (glow.sharedMaterial != null)
                {
                    Color glowColor = DisplayColor(segmentColors[i]);
                    glowColor.a = GlowAlpha;
                    glow.sharedMaterial.SetColor("_Color", glowColor);
                }
            }
        }

        if (beamSegments.Count > segmentCount)
        {
            for (int i = segmentCount; i < beamSegments.Count; i++)
            {
                beamSegments[i].positionCount = 0;

                if (i < glowSegments.Count && glowSegments[i] != null)
                {
                    glowSegments[i].positionCount = 0;
                }
            }
        }
    }

    private void HideBeam()
    {
        for (int i = 0; i < beamSegments.Count; i++)
        {
            beamSegments[i].positionCount = 0;
        }

        for (int i = 0; i < glowSegments.Count; i++)
        {
            if (glowSegments[i] != null)
            {
                glowSegments[i].positionCount = 0;
            }
        }
    }

    private LineRenderer GetBeamSegment(int index)
    {
        if (beam == null)
        {
            return null;
        }

        while (beamSegments.Count <= index)
        {
            var segmentObject = new GameObject("Segment_" + beamSegments.Count);
            Transform segmentTransform = segmentObject.transform;
            segmentTransform.SetParent(beam.transform, false);
            segmentTransform.localPosition = Vector3.zero;
            segmentTransform.localRotation = Quaternion.identity;
            segmentTransform.localScale = Vector3.one;

            var segment = segmentObject.AddComponent<LineRenderer>();
            segment.useWorldSpace = false;
            segment.loop = false;
            segment.alignment = beam.alignment;
            segment.textureMode = beam.textureMode;
            segment.startWidth = beam.startWidth;
            segment.endWidth = beam.endWidth;
            segment.widthMultiplier = beam.widthMultiplier;
            segment.numCapVertices = beam.numCapVertices;
            segment.numCornerVertices = beam.numCornerVertices;
            segment.startColor = Color.white;
            segment.endColor = Color.white;
            segment.shadowCastingMode = beam.shadowCastingMode;
            segment.receiveShadows = false;
            segment.positionCount = 0;
            segment.sharedMaterial = beamMaterialTemplate != null
                ? new Material(beamMaterialTemplate)
                : null;
            beamSegments.Add(segment);
            glowSegments.Add(CreateGlow(segmentTransform, segment));
        }

        return beamSegments[index];
    }

    private LineRenderer CreateGlow(Transform parent, LineRenderer source)
    {
        if (beamGlowTemplate == null || parent == null || source == null)
        {
            return null;
        }

        var glowObject = new GameObject("Glow");
        Transform glowTransform = glowObject.transform;
        glowTransform.SetParent(parent, false);
        glowTransform.localPosition = Vector3.zero;
        glowTransform.localRotation = Quaternion.identity;
        glowTransform.localScale = Vector3.one;

        var glow = glowObject.AddComponent<LineRenderer>();
        glow.useWorldSpace = false;
        glow.loop = false;
        glow.alignment = source.alignment;
        glow.textureMode = source.textureMode;
        glow.startWidth = source.startWidth;
        glow.endWidth = source.endWidth;
        glow.widthMultiplier = source.widthMultiplier * GlowWidthScale;
        glow.numCapVertices = 0;
        glow.numCornerVertices = 0;
        glow.startColor = Color.white;
        glow.endColor = Color.white;
        glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glow.receiveShadows = false;
        glow.positionCount = 0;
        glow.sharedMaterial = new Material(beamGlowTemplate);
        return glow;
    }

    private static Color DisplayColor(LightColor color)
    {
        float max = Mathf.Max(color.R, Mathf.Max(color.G, color.B));

        if (max <= 1e-6f)
        {
            return Color.black;
        }

        return new Color(color.R / max, color.G / max, color.B / max, 1f);
    }

    private LightColor ResolveColor()
    {
        if (Palette.TryGet(colorId, out LightColor color))
        {
            return color;
        }

        Debug.LogWarning($"[LaserEmitter] Unknown color id: {colorId}");
        return Palette.W;
    }

    private static float RayCircle(
        Vector2 origin,
        Vector2 direction,
        Vector2 center,
        float radius)
    {
        Vector2 toCenter = center - origin;
        float projection = Vector2.Dot(toCenter, direction);

        if (projection <= 0f)
        {
            return -1f;
        }

        float perpendicularSq = toCenter.sqrMagnitude - projection * projection;
        float radiusSq = radius * radius;

        if (perpendicularSq > radiusSq)
        {
            return -1f;
        }

        float offset = Mathf.Sqrt(radiusSq - perpendicularSq);
        float near = projection - offset;

        return near > 0f ? near : projection + offset;
    }

    private static float FaceExitDistance(Vector2 origin, Vector2 direction)
    {
        float distance = float.PositiveInfinity;

        if (Mathf.Abs(direction.x) > 0.00001f)
        {
            float tx = ((direction.x > 0f ? FaceBound : -FaceBound) - origin.x) / direction.x;
            if (tx > 0f)
            {
                distance = Mathf.Min(distance, tx);
            }
        }

        if (Mathf.Abs(direction.y) > 0.00001f)
        {
            float ty = ((direction.y > 0f ? FaceBound : -FaceBound) - origin.y) / direction.y;
            if (ty > 0f)
            {
                distance = Mathf.Min(distance, ty);
            }
        }

        return float.IsInfinity(distance) ? 0f : distance;
    }

    private enum HitKind
    {
        Boundary,
        Wall,
        Mirror,
        Lens,
        Filter,
        FilterBlocked,
        Prism,
        Lock,
        Receiver
    }
}
