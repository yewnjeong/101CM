// 창고 전신거울 면. JellyMirror가 거울 면 기준으로 대칭인 카메라로 그린 화면(_ReflectionTex)을
// 메인 카메라의 화면 위치로 받아 좌우를 뒤집어 그린다. 거울 판 모양과 상관없이 맞게 비친다.
// 반사 카메라가 꺼져 있을 때(멀리 있거나 안 보일 때, 에디터)는 은회색 단색.
Shader "CM101/Mirror"
{
    Properties
    {
        [NoScaleOffset] _ReflectionTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.94, 0.97, 1, 1)
        _OffColor ("Off Color", Color) = (0.66, 0.70, 0.76, 1)
        _MirrorOn ("Mirror On", Float) = 0
        _EdgeShade ("Edge Shade", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Mirror"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _OffColor;
                float _MirrorOn;
                half _EdgeShade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = i.screenPos.xy / i.screenPos.w;
                suv.x = 1.0 - suv.x; // 반사 카메라는 정상 카메라라서 거울상은 좌우만 뒤집으면 된다
                half3 refl = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, suv).rgb * _Tint.rgb;
                float2 d = i.uv - 0.5;
                half edge = saturate(dot(d, d) * 2.0) * _EdgeShade; // 가장자리를 아주 살짝 어둡게
                half3 col = lerp(_OffColor.rgb, refl, saturate(_MirrorOn)) * (1.0 - edge);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _OffColor;
                float _MirrorOn;
                half _EdgeShade;
            CBUFFER_END
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
