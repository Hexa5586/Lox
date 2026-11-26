Shader "Lox/FrostedAluminum"
{
    Properties
    {
        _Color ("Aluminum Color", Color) = (0.42, 0.44, 0.46, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.6
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 0.5
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

            // The edging mesh is made from boxes; double-sided rendering keeps
            // it robust at the mitered joints.
            Cull Off
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
            float _Metallic;
            float _Smoothness;
            float _SpecularStrength;
            float _ReflectionStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);

                float ndl = saturate(dot(n, l));
                float3 h = normalize(l + v);
                float ndh = saturate(dot(n, h));
                float gloss = lerp(10.0, 120.0, _Smoothness * _Smoothness);
                float spec = pow(ndh, gloss) * _SpecularStrength;

                float3 refl = reflect(-v, n);
                float4 envHDR = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, refl, lerp(6.0, 1.0, _Smoothness));
                float3 env = DecodeHDR(envHDR, unity_SpecCube0_HDR);

                float fresnel = pow(1.0 - saturate(dot(n, v)), 5.0);
                float3 base = _Color.rgb;
                float3 ambient = ShadeSH9(float4(n, 1.0));

                float diffuse = (1.0 - _Metallic) * 0.8;
                float3 color = base * (ambient * (1.0 - _Metallic) + _LightColor0.rgb * ndl * diffuse);
                color += _LightColor0.rgb * spec * base * lerp(0.35, 1.0, _Metallic);
                color += env * _ReflectionStrength * (base * _Metallic + 0.03) * (0.25 + fresnel);

                return fixed4(saturate(color), 1.0);
            }
            ENDCG
        }
    }
}


