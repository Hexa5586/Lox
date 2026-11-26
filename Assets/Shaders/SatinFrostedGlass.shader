Shader "Lox/SatinFrostedGlass"
{
    Properties
    {
        _Color ("Color", Color) = (0.7333333, 0.7333333, 0.7333333, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.35
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.35
        _Sheen ("Fresnel Sheen", Range(0, 1)) = 0.1
        _Grain ("Frost Grain", Range(0, 0.2)) = 0.035
        _GrainScale ("Frost Grain Scale", Range(4, 120)) = 20
        _Brightness ("Brightness", Range(0, 2)) = 0.78
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            Cull Back
            ZWrite On

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
            };

            fixed4 _Color;
            float _Smoothness;
            float _SpecularStrength;
            float _Wrap;
            float _Sheen;
            float _Grain;
            float _GrainScale;
            float _Brightness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float FrostHash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth value noise keeps the larger frosted grain from looking
            // like square pixels.
            float FrostNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = FrostHash(cell + float3(0.0, 0.0, 0.0));
                float n100 = FrostHash(cell + float3(1.0, 0.0, 0.0));
                float n010 = FrostHash(cell + float3(0.0, 1.0, 0.0));
                float n110 = FrostHash(cell + float3(1.0, 1.0, 0.0));
                float n001 = FrostHash(cell + float3(0.0, 0.0, 1.0));
                float n101 = FrostHash(cell + float3(1.0, 0.0, 1.0));
                float n011 = FrostHash(cell + float3(0.0, 1.0, 1.0));
                float n111 = FrostHash(cell + float3(1.0, 1.0, 1.0));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);

                float ndl = dot(n, l);
                float diffuse = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                float grain = FrostNoise(i.worldPos * _GrainScale) - 0.5;
                diffuse = saturate(diffuse + grain * _Grain);

                float3 ambient = ShadeSH9(float4(n, 1.0));
                float3 base = _Color.rgb;
                float3 color = base * (ambient + _LightColor0.rgb * diffuse * 0.9);

                // Satin frosted glass keeps a broad, low-intensity highlight instead
                // of the tight highlight of a polished surface.
                float3 h = normalize(l + v);
                float ndh = saturate(dot(n, h));
                float gloss = lerp(6.0, 80.0, _Smoothness * _Smoothness);
                float spec = pow(ndh, gloss) * _SpecularStrength;
                float fresnel = pow(1.0 - saturate(dot(n, v)), 5.0) * _Sheen;

                color += _LightColor0.rgb * (spec + fresnel) * (0.5 + 0.5 * base);
                return fixed4(saturate(color * _Brightness), 1.0);
            }
            ENDCG
        }
    }
}


