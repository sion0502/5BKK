Shader "WraithBound/ShadowGhostFog"
{
    Properties
    {
        _MainTex ("Soft Mask", 2D) = "white" {}
        _Color ("Fog Color", Color) = (0.02, 0.02, 0.03, 0.92)
        _NoiseScale ("Noise Scale", Float) = 2.4
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.45
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.55
        _Density ("Density", Range(0.1, 3)) = 1.35
        _Scroll ("Scroll Speed", Vector) = (0.07, 0.04, -0.05, 0.06)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ShadowGhostFog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _NoiseScale;
                float _NoiseStrength;
                float _EdgeSoftness;
                float _Density;
                float4 _Scroll;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += Noise(p) * a;
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return v;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.worldPos = world;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(1.0 - length(centered));
                radial = pow(radial, max(0.05, _EdgeSoftness));

                float t = _Time.y;
                float2 driftA = float2(_Scroll.x, _Scroll.y) * t;
                float2 driftB = float2(_Scroll.z, _Scroll.w) * t;
                float n1 = Fbm(input.worldPos.xz * _NoiseScale + driftA);
                float n2 = Fbm(input.worldPos.xy * (_NoiseScale * 0.8) - driftB);
                float n = saturate(n1 * 0.65 + n2 * 0.35);
                float cloudy = lerp(1.0, n, _NoiseStrength);

                float alpha = mask * radial * cloudy * _Color.a * input.color.a * _Density;
                alpha = saturate(alpha);
                // Keep the body near-black so walls only ghost through faintly.
                half3 rgb = _Color.rgb * (0.55 + n * 0.25);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
