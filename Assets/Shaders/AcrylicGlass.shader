Shader "Lox/AcrylicGlass"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (1, 1, 1, 1)
        _BaseAlpha ("Base Alpha", Range(0, 0.5)) = 0.01
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4
        _FresnelAlpha ("Fresnel Alpha", Range(0, 1)) = 0.22
        _EdgeCoreWidth ("Edge Core Width", Range(0.0005, 0.05)) = 0.004
        _EdgeHaloWidth ("Edge Halo Width", Range(0.001, 0.1)) = 0.014
        _EdgeAlpha ("Edge Alpha", Range(0, 1)) = 0.1
        _EdgeGlow ("Edge Glow", Range(0, 4)) = 0.38
        _ThicknessWidth ("Thickness Band Width", Range(0.001, 0.1)) = 0.03
        _ThicknessAlpha ("Thickness Band Alpha", Range(0, 0.5)) = 0.04
        _ThicknessGlow ("Thickness Band Glow", Range(0, 2)) = 0.09
        _Glossiness ("Glossiness", Range(1, 512)) = 48
        _SpecularStrength ("Specular Strength", Range(0, 8)) = 1.4
        _ReflectionStrength ("Environment Reflection", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        // Body: a nearly colorless tint plus soft fresnel and a bright seam
        // along the cube edges, which is where acrylic catches light.
        Pass
        {
            Name "AcrylicSurface"
            Tags { "LightMode" = "ForwardBase" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 localPos : TEXCOORD2;
            };

            fixed4 _Color;
            float _BaseAlpha;
            float _FresnelPower;
            float _FresnelAlpha;
            float _EdgeCoreWidth;
            float _EdgeHaloWidth;
            float _EdgeAlpha;
            float _ThicknessWidth;
            float _ThicknessAlpha;
            float _ThicknessGlow;
            float _ReflectionStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.localPos = v.vertex.xyz;
                return o;
            }

            float EdgeDistance(float3 localPos)
            {
                // The built-in cube mesh spans [-0.5, 0.5]. The distance to the
                // nearest edge of the current face is the middle value of the
                // three per-axis distances (the smallest is always zero on a face).
                float3 d = 0.5 - abs(localPos);
                float lo = min(d.x, min(d.y, d.z));
                float hi = max(d.x, max(d.y, d.z));
                float mid = d.x + d.y + d.z - lo - hi;
                return max(mid, 0.0);
            }

            float EdgeMask(float3 localPos, float width)
            {
                return 1.0 - smoothstep(0.0, width, EdgeDistance(localPos));
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.worldNormal) * (facing > 0.0 ? 1.0 : -1.0);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float ndv = saturate(dot(n, v));
                float fresnel = pow(1.0 - ndv, _FresnelPower);

                float edgeDistance = EdgeDistance(i.localPos);
                float core = 1.0 - smoothstep(0.0, _EdgeCoreWidth, edgeDistance);
                float halo = 1.0 - smoothstep(0.0, _EdgeHaloWidth, edgeDistance);
                float band = 1.0 - smoothstep(0.0, _ThicknessWidth, edgeDistance);

                float3 refl = reflect(-v, n);
                float4 envHDR = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, refl, 4.0);
                float3 env = DecodeHDR(envHDR, unity_SpecCube0_HDR);

                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;
                float3 color = _Color.rgb * (ambient + 0.04);
                color += env * _ReflectionStrength * (0.04 + fresnel * 0.7);
                color += _Color.rgb * (core * 0.18 + halo * 0.06 + band * _ThicknessGlow);

                float alpha = _BaseAlpha;
                alpha += fresnel * _FresnelAlpha;
                alpha += core * _EdgeAlpha + halo * _EdgeAlpha * 0.45;
                alpha += band * _ThicknessAlpha;
                alpha = saturate(alpha);

                return fixed4(color, alpha);
            }
            ENDCG
        }

        // Gloss: additive highlight pass so the reflections stay visible even
        // though the body itself is almost fully transparent.
        Pass
        {
            Name "AcrylicHighlight"
            Tags { "LightMode" = "ForwardBase" }

            Blend One One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 localPos : TEXCOORD2;
            };

            fixed4 _Color;
            float _EdgeCoreWidth;
            float _EdgeHaloWidth;
            float _EdgeGlow;
            float _ThicknessWidth;
            float _ThicknessGlow;
            float _Glossiness;
            float _SpecularStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.localPos = v.vertex.xyz;
                return o;
            }

            float EdgeDistance(float3 localPos)
            {
                float3 d = 0.5 - abs(localPos);
                float lo = min(d.x, min(d.y, d.z));
                float hi = max(d.x, max(d.y, d.z));
                float mid = d.x + d.y + d.z - lo - hi;
                return max(mid, 0.0);
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.worldNormal) * (facing > 0.0 ? 1.0 : -1.0);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 h = normalize(l + v);
                float ndh = saturate(dot(n, h));
                float spec = pow(ndh, _Glossiness);

                float edgeDistance = EdgeDistance(i.localPos);
                float core = 1.0 - smoothstep(0.0, _EdgeCoreWidth, edgeDistance);
                float halo = 1.0 - smoothstep(0.0, _EdgeHaloWidth, edgeDistance);
                float band = 1.0 - smoothstep(0.0, _ThicknessWidth, edgeDistance);

                float3 color = _LightColor0.rgb * spec * _SpecularStrength;
                color += _Color.rgb * (core * _EdgeGlow + halo * _EdgeGlow * 0.3 + band * _ThicknessGlow * 0.5);

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}

