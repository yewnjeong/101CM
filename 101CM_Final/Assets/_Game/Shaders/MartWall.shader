Shader "_Game/MartWall"
{
    // 벽 높이(월드 Y) 기준 2색 + 몰딩 선. 아래 브라운 / 몰딩 / 위 노랑
    Properties
    {
        _TopColor("Top (Yellow)", Color) = (0.996, 0.941, 0.714, 1)
        _BottomColor("Bottom (Brown)", Color) = (0.278, 0.184, 0.149, 1)
        _TrimColor("Trim (Molding)", Color) = (0.961, 0.910, 0.804, 1)
        _SplitHeight("Split Height", Float) = 1.5
        _TrimHeight("Trim Height", Float) = 0.08
        _FloorY("Floor Y", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _TrimColor;
                float _SplitHeight;
                float _TrimHeight;
                float _FloorY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fogFactor : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float h = i.positionWS.y - _FloorY;
                half3 albedo = h < _SplitHeight ? _BottomColor.rgb : (h < _SplitHeight + _TrimHeight ? _TrimColor.rgb : _TopColor.rgb);
                float3 n = normalize(i.normalWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light ml = GetMainLight(sc);
                half3 col = albedo * SampleSH(n);
                col += albedo * ml.color * saturate(dot(n, ml.direction)) * ml.shadowAttenuation * ml.distanceAttenuation;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
