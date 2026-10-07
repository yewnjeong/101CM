Shader "_Game/JellyLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _Saturation ("Saturation Boost", Range(0,2)) = 1.15
        _Wrap ("Soft Light Wrap", Range(0,1)) = 0.6
        _ShadowColor ("Shadow Tint", Color) = (0.78,0.62,0.9,1)
        _Glow ("Inner Glow", Range(0,1)) = 0.35
        _SSSColor ("Translucency Color", Color) = (1,0.85,0.8,1)
        _SSSStrength ("Translucency", Range(0,2)) = 0.6
        _SSSDistortion ("Translucency Distortion", Range(0,1)) = 0.4
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        _SpecColor2 ("Specular Color", Color) = (1,1,1,1)
        _Gloss ("Specular Sharpness", Range(8,512)) = 160
        _SpecStrength ("Specular Strength", Range(0,3)) = 1.2
        _SoftSpec ("Soft Sheen", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; half _BumpScale; half _Saturation; half _Wrap; half4 _ShadowColor;
            half _Glow; half4 _SSSColor; half _SSSStrength; half _SSSDistortion; half4 _RimColor; half _RimPower; half _RimStrength;
            half4 _SpecColor2; half _Gloss; half _SpecStrength; half _SoftSpec;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float3 normalWS:TEXCOORD2; float4 tangentWS:TEXCOORD3; float fog:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, o);
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = n.normalWS; o.tangentWS = float4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half3 Saturate3(half3 c, half s){ half l = dot(c, half3(0.299,0.587,0.114)); return max(0, lerp(l.xxx, c, s)); }
            half3 JellyLight(Light light, half3 albedo, half3 N, half3 V)
            {
                half3 L = light.direction;
                half atten = light.distanceAttenuation * light.shadowAttenuation;
                half wrap = saturate((dot(N, L) + _Wrap) / (1 + _Wrap));
                half3 diff = albedo * lerp(_ShadowColor.rgb, 1, wrap * lerp(0.5, 1, atten)) * wrap;
                half3 H = normalize(L + V);
                half nh = saturate(dot(N, H));
                half spec = pow(nh, _Gloss) * _SpecStrength * atten;
                half sheen = pow(nh, 12) * _SoftSpec * atten;
                half3 Lt = normalize(L + N * _SSSDistortion);
                half sss = pow(saturate(dot(V, -Lt)), 3) * _SSSStrength;
                return light.color * (diff + (spec + sheen) * _SpecColor2.rgb + sss * _SSSColor.rgb * albedo);
            }
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
                albedo = Saturate3(albedo, _Saturation);
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float3 bit = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3 N = normalize(TransformTangentToWorld(nTS, half3x3(IN.tangentWS.xyz, bit, IN.normalWS)));
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                Light mainL = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half3 col = JellyLight(mainL, albedo, N, V);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                for (uint i = 0; i < cnt; i++) { Light l = GetAdditionalLight(i, IN.positionWS); col += JellyLight(l, albedo, N, V) * 0.6; }
                #endif
                half ndv = saturate(dot(N, V));
                col += SampleSH(N) * albedo * 0.55;
                col += albedo * _Glow * (0.4 + 0.6 * ndv);
                half rim = pow(1 - ndv, _RimPower) * _RimStrength;
                col = lerp(col, _RimColor.rgb * (albedo * 0.5 + 0.6), rim);
                col = MixFog(col, IN.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex sv
            #pragma fragment sf
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 sv(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - p);
                #else
                float3 ld = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, ld));
                #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 sf() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex dv
            #pragma fragment df
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 dv(A i) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(i); return TransformObjectToHClip(i.positionOS.xyz); }
            half df() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex nv
            #pragma fragment nf
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V2 { float4 pos:SV_POSITION; float3 n:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V2 nv(A i) { V2 o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o); o.pos = TransformObjectToHClip(i.positionOS.xyz); o.n = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 nf(V2 i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); return half4(NormalizeNormalPerPixel(i.n), 0); }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
