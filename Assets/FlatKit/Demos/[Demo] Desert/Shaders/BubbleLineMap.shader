Shader "LineKit/Demos/BubbleLineMap"
{
    Properties
    {
        _SpecularSize("Specular Size", Range(-0.5, 0.5)) = -0.1

        [Space]
        [Enum(Both, 0, Back, 1, Front, 2)] _Cull("Render Face", Float) = 2

        // Workaround Unity bug "Shader 'Dustyroom/LineMap' doesn't have a texture property '_MainTex'"
        [HideInInspector] _MainTex("Main Texture", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            Name "LineMap"
            Tags
            {
                "LightMode" = "LineMap"
            }

            Cull [_Cull]

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram

            #pragma multi_compile_instancing

            half _SpecularSize;

            struct VertexInput {
                float3 positionOS: POSITION;
                float3 normalOS: NORMAL;
            };

            struct VertexOutput {
                float4 positionHCS: SV_POSITION;
                float3 positionOS: TEXCOORD0;
                float3 normalWS: TEXCOORD1;
            };

            VertexOutput VertexProgram(VertexInput v) {
                VertexOutput o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.positionOS = v.positionOS;
                o.normalWS = TransformObjectToWorldDir(v.normalOS);
                return o;
            }

            half4 FragmentProgram(VertexOutput i) : SV_TARGET {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                _SpecularSize = _SpecularSize + 0.5;

                const float3 positionWS = TransformObjectToWorld(i.positionOS);
                const float3 viewVectorWS = normalize(-GetWorldSpaceViewDir(positionWS));
                const float3 mainLightDirWS = -GetMainLight().direction;
                const float3 reflectedDirWS = reflect(mainLightDirWS, i.normalWS);
                const half nDotL1 = saturate(dot(reflectedDirWS, viewVectorWS) * 0.5 + 0.5);
                const half step1 = step(_SpecularSize, nDotL1);

                const half nDotL2 = saturate(dot(-reflectedDirWS, viewVectorWS) * 0.5 + 0.5);
                const half step2 = step(_SpecularSize / 6, nDotL2);

                const half mult = step1 * step2;

                if (mult) {
                    discard;
                }

                return 1;
            }
            ENDHLSL
        }
    }
}