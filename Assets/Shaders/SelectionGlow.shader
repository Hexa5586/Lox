Shader "Lox/SelectionGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 0.4666667, 1)
        _OutlineWidth ("Outline Width (px at 1080p)", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        // Marks the device's rendered footprint in the stencil buffer so the
        // expanded pass below only draws outside the silhouette.
        Pass
        {
            Name "SilhouetteMask"
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            Stencil
            {
                Ref 2
                ReadMask 2
                WriteMask 2
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        // Expands the silhouette outwards by a constant number of screen pixels,
        // so the result is a 2D outline of the object in the current view.
        Pass
        {
            Name "SilhouetteOutline"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            Stencil
            {
                Ref 2
                ReadMask 2
                Comp NotEqual
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            fixed4 _Color;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;

                float4 clipPosition = UnityObjectToClipPos(v.vertex);
                float4 pivotPosition = UnityObjectToClipPos(float4(0, 0, 0, 1));

                float2 direction = clipPosition.xy / clipPosition.w - pivotPosition.xy / pivotPosition.w;
                float lengthSq = dot(direction, direction);
                direction = lengthSq > 1e-10 ? direction * rsqrt(lengthSq) : float2(1, 0);

                float pixelScale = _ScreenParams.y / 1080.0;
                clipPosition.xy += direction * (_OutlineWidth * pixelScale * 2.0 / _ScreenParams.xy) * clipPosition.w;

                o.pos = clipPosition;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
