// 101CM 무지개 젤리(구미베어) 셰이더: 버텍스 컬러 + 반투명 + 강한 하이라이트 + 가장자리 밝게
Shader "101CM/Gummy"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Alpha ("Opacity", Range(0,1)) = 0.82
        _RimAlpha ("Edge Opacity", Range(0,1)) = 0.97
        _Gloss ("Gloss", Range(8,256)) = 90
        _SpecStrength ("Highlight", Range(0,3)) = 1.4
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2.5
        _RimStrength ("Rim Strength", Range(0,2)) = 0.55
        _Glow ("Inner Glow", Range(0,1)) = 0.35
        _Wrap ("Light Wrap", Range(0,1)) = 0.6
        _Saturation ("Saturation", Range(0,2)) = 1.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        // 1) 깊이만 먼저 써서 자기 자신끼리 겹쳐 보이는 문제 방지
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos:POSITION; };
            struct V { float4 pos:SV_POSITION; };
            V vert(A i){ V o; o.pos=TransformObjectToHClip(i.pos.xyz); return o; }
            half4 frag(V i):SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "Gummy"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half _Alpha; half _RimAlpha; half _Gloss; half _SpecStrength;
                half4 _RimColor; half _RimPower; half _RimStrength; half _Glow; half _Wrap; half _Saturation;
            CBUFFER_END
            struct A { float4 pos:POSITION; float3 nrm:NORMAL; float4 col:COLOR; };
            struct V { float4 pos:SV_POSITION; float3 n:TEXCOORD0; float3 w:TEXCOORD1; half4 col:COLOR; };
            V vert(A i)
            {
                V o; VertexPositionInputs p=GetVertexPositionInputs(i.pos.xyz);
                o.pos=p.positionCS; o.w=p.positionWS; o.n=TransformObjectToWorldNormal(i.nrm); o.col=i.col; return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.n); float3 v=normalize(GetWorldSpaceViewDir(i.w));
                Light L=GetMainLight();
                half3 base=SRGBToLinear(i.col.rgb)*_BaseColor.rgb;  // 버텍스 컬러는 sRGB로 저장됨
                base=lerp(dot(base,half3(0.299,0.587,0.114)).xxx,base,_Saturation);
                half nl=dot(n,L.direction);
                half wrap=saturate((nl+_Wrap)/(1+_Wrap));
                half3 amb=SampleSH(n);
                half3 col=base*(wrap*L.color*0.8+amb*0.45+_Glow);
                float3 h=normalize(L.direction+v);
                half spec=pow(saturate(dot(n,h)),_Gloss)*_SpecStrength;
                half spec2=pow(saturate(dot(n,h)),_Gloss*0.15)*0.12;
                half fres=pow(1-saturate(dot(n,v)),_RimPower);
                col+= (spec+spec2)*L.color + fres*_RimStrength*_RimColor.rgb*0.35 + fres*base*0.5;
                half a=lerp(_Alpha,_RimAlpha,fres);
                a=saturate(a+spec);
                return half4(col,a*_BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
