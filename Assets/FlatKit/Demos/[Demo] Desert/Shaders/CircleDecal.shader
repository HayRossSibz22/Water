Shader "Dustyroom/Decal"
{
    Properties
    {
        [MainColor][HDR]_Color("_Color (default = 1,1,1,1)", Color) = (1,1,1,1)

        //[Header(Blending)]
        // https://docs.unity3d.com/ScriptReference/Rendering.BlendMode.html
        [Space]
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalSrcBlend("Src Blend", Int) = 5 // 5 = SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]_DecalDstBlend("Dst Blend", Int) = 10 // 10 = OneMinusSrcAlpha

        //[Header(Prevent Side Stretching(Compare projection direction with scene normal and Discard if needed))]
        [Space]
        [Toggle(_ProjectionAngleDiscardEnable)] _ProjectionAngleDiscardEnable("Angle Discard Enable", float) = 0
        _ProjectionAngleDiscardThreshold("     Threshold", range(-1,1)) = 0

        //====================================== below = usually can ignore in normal use case =====================================================================
        //[Header(Stencil Masking)]
        // https://docs.unity3d.com/ScriptReference/Rendering.CompareFunction.html
        [Space]
        _StencilRef("Stencil Reference", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("Stencil Compare", Float) = 0 //0 = disable

        //[Header(ZTest)]
        [Space]
        // https://docs.unity3d.com/ScriptReference/Rendering.CompareFunction.html
        // default need to be Disable, because we need to make sure decal render correctly even if camera goes into decal cube volume, although disable ZTest by default will prevent EarlyZ (bad for GPU performance)
        [Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("Depth Test", Float) = 0 //0 = disable

        //[Header(Cull)]
        [Space]
        // https://docs.unity3d.com/ScriptReference/Rendering.CullMode.html
        // default need to be Front, because we need to make sure decal render correctly even if camera goes into decal cube
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 1 //1 = Front
    }

    SubShader
    {
        // To avoid render order problems, Queue must >= 2501, which enters the transparent queue, 
        // in transparent queue Unity will always draw from back to front
        // https://github.com/ColinLeung-NiloCat/UnityURPUnlitScreenSpaceDecalShader/issues/6#issuecomment-615940985

        // https://docs.unity3d.com/Manual/SL-SubShaderTags.html
        // Queues up to 2500 (“Geometry+500”) are consided “opaque” and optimize the drawing order of the objects for best performance. 
        // Higher rendering queues are considered for “transparent objects” and sort objects by distance, 
        // starting rendering from the furthest ones and ending with the closest ones. 
        // Skyboxes are drawn in between all opaque and all transparent objects.
        // "Queue" = "Transparent-499" means "Queue" = "2501", which is almost equals "draw right before any transparent objects"

        // "DisableBatching" means disable "dynamic batching", not "srp batching"
        Tags
        {
            "RenderType" = "Overlay" "Queue" = "Transparent-499" "DisableBatching" = "True"
        }

        Pass
        {
            Stencil
            {
                Ref[_StencilRef]
                Comp[_StencilComp]
            }

            Cull[_Cull]
            ZTest[_ZTest]

            ZWrite off
            Blend[_DecalSrcBlend][_DecalDstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // make fog work
            #pragma multi_compile_fog

            // due to using ddx() & ddy()
            #pragma target 3.0

            #pragma shader_feature_local_fragment _ProjectionAngleDiscardEnable

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata {
                float3 positionOS : POSITION;
            };

            struct v2f {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 viewRayOS : TEXCOORD1; // xyz: viewRayOS, w: extra copy of positionVS.z 
                float4 cameraPosOSAndFogFactor : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _ProjectionAngleDiscardThreshold;
            half4 _Color;
            half2 _AlphaRemap;
            half _MulAlphaToRGB;
            CBUFFER_END

            v2f vert(appdata input) {
                v2f o;

                // VertexPositionInputs contains position in multiple spaces (world, view, homogeneous clip space, ndc)
                // Unity compiler will strip all unused references (say you don't use view space).
                // Therefore there is more flexibility at no additional cost with this struct.
                VertexPositionInputs vertexPositionInput = GetVertexPositionInputs(input.positionOS);

                o.positionCS = vertexPositionInput.positionCS;

                // regular unity fog
                #if _UnityFogEnable
                o.cameraPosOSAndFogFactor.a = ComputeFogFactor(o.positionCS.z);
                #else
                o.cameraPosOSAndFogFactor.a = 0;
                #endif

                // prepare depth texture's screen space UV
                o.screenPos = ComputeScreenPos(o.positionCS);

                // get "camera to vertex" ray in View space
                float3 viewRay = vertexPositionInput.positionVS;

                // [important note]
                //=========================================================
                // "viewRay z division" must do in the fragment shader, not vertex shader! (due to rasteriazation varying interpolation's perspective correction)
                // We skip the "viewRay z division" in vertex shader for now, and store the division value into varying o.viewRayOS.w first, 
                // we will do the division later when we enter fragment shader
                // viewRay /= viewRay.z; //skip the "viewRay z division" in vertex shader for now
                o.viewRayOS.w = viewRay.z; //store the division value to varying o.viewRayOS.w
                //=========================================================

                // unity's camera space is right hand coord(negativeZ pointing into screen), we want positive z ray in fragment shader, so negate it
                viewRay *= -1;

                // it is ok to write very expensive code in decal's vertex shader, 
                // it is just a unity cube(4*6 vertices) per decal only, won't affect GPU performance at all.
                float4x4 ViewToObjectMatrix = mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V);

                // transform everything to object space(decal space) in vertex shader first, so we can skip all matrix mul() in fragment shader
                o.viewRayOS.xyz = mul((float3x3)ViewToObjectMatrix, viewRay);
                o.cameraPosOSAndFogFactor.xyz = mul(ViewToObjectMatrix, float4(0, 0, 0, 1)).xyz;
                // hard code 0 or 1 can enable many compiler optimization

                return o;
            }

            // copied from URP12.1.2's ShaderVariablesFunctions.hlsl
            #if SHADER_LIBRARY_VERSION_MAJOR < 12
            float LinearDepthToEyeDepth(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                    return _ProjectionParams.z - (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
            #else
                    return _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * rawDepth;
            #endif
            }
            #endif

            half4 frag(v2f i) : SV_Target {
                // [important note]
                //========================================================================
                // now do "viewRay z division" that we skipped in vertex shader earlier.
                i.viewRayOS.xyz /= i.viewRayOS.w;
                //========================================================================

                float2 screenSpaceUV = i.screenPos.xy / i.screenPos.w;
                float sceneRawDepth = tex2D(_CameraDepthTexture, screenSpaceUV).r;

                float3 decalSpaceScenePos;

                // we have to support both orthographic and perspective camera projection
                // static uniform branch depends on unity_OrthoParams.w
                // (should we use UNITY_BRANCH here?) decided NO because https://forum.unity.com/threads/correct-use-of-unity_branch.476804/
                if (unity_OrthoParams.w) {
                    float sceneDepthVS = LinearDepthToEyeDepth(sceneRawDepth);

                    //***Used a few lines from Asset: Lux URP Essentials by forst***
                    // Edit: The copied Lux URP stopped working at some point, and no one even knew why it worked in the first place 
                    //----------------------------------------------------------------------------
                    float2 viewRayEndPosVS_xy = float2(
                        unity_OrthoParams.xy * (i.screenPos.xy - 0.5) * 2 /* to clip space */);
                    // Ortho near/far plane xy pos 
                    float4 vposOrtho = float4(viewRayEndPosVS_xy, -sceneDepthVS, 1); // Constructing a view space pos
                    float3 wposOrtho = mul(UNITY_MATRIX_I_V, vposOrtho).xyz; // Trans. view space to world space
                    //----------------------------------------------------------------------------

                    // transform world to object space(decal space)
                    decalSpaceScenePos = mul(GetWorldToObjectMatrix(), float4(wposOrtho, 1)).xyz;
                } else {
                    // if perspective camera, LinearEyeDepth will handle everything for user
                    // remember we can't use LinearEyeDepth for orthographic camera!
                    float sceneDepthVS = LinearEyeDepth(sceneRawDepth, _ZBufferParams);

                    // scene depth in any space = rayStartPos + rayDir * rayLength
                    // here all data in ObjectSpace(OS) or DecalSpace
                    // be careful, viewRayOS is not a unit vector, so don't normalize it, it is a direction vector which view space z's length is 1
                    decalSpaceScenePos = i.cameraPosOSAndFogFactor.xyz + i.viewRayOS.xyz * sceneDepthVS;
                }

                // convert unity cube's [-0.5,0.5] vertex pos range to [0,1] uv. Only works if you use a unity cube in mesh filter!
                float2 decalSpaceUV = decalSpaceScenePos.xy + 0.5;

                // discard logic
                //===================================================
                // discard "out of cube volume" pixels
                float shouldClip = 0;
                #if _ProjectionAngleDiscardEnable
                // also discard "scene normal not facing decal projector direction" pixels
                float3 decalSpaceHardNormal = normalize(cross(ddx(decalSpaceScenePos), ddy(decalSpaceScenePos)));//reconstruct scene hard normal using scene pos ddx&ddy

                // compare scene hard normal with decal projector's dir, decalSpaceHardNormal.z equals dot(decalForwardDir,sceneHardNormalDir)
                shouldClip = decalSpaceHardNormal.z > _ProjectionAngleDiscardThreshold ? 0 : 1;
                #endif
                // call discard
                // if ZWrite is Off, clip() is fast enough on mobile, because it won't write the DepthBuffer, so no GPU pipeline stall(confirmed by ARM staff).
                clip(0.5 - abs(decalSpaceScenePos) - shouldClip);
                //===================================================

                // Create a circular mask in col where RGB is 1 and A is the mask value
                float4 col = 1;
                col.a = smoothstep(0, 0.01, 1 - length(decalSpaceUV - 0.5) * 2);

                col *= _Color; // tint color

                col.rgb = MixFog(col.rgb, i.cameraPosOSAndFogFactor.a);
                return col;
            }
            ENDHLSL
        }
    }
}