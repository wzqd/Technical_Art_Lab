Shader "TechArtLab/008/LoFiDiffuse"
{
    Properties
    {
        _BaseColor ("Base color", Color) = (0.7,0.7,0.7,1)
        _Ambient ("Ambient floor", Range(0,1)) = 0.32
        _DebugView ("Debug: beauty / albedo / normals / silhouette", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // URP 17 Forward+ must select the clustered main-light data path.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Ambient;
                half _DebugView;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                if (_DebugView > 2.5) return half4(1,1,1,1);
                if (_DebugView > 1.5) return half4(normalize(i.normalWS)*.5+.5,1);
                if (_DebugView > .5) return half4(_BaseColor.rgb,1);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // World-space unit normal and direction towards the light.
                // A continuous Lambert term deliberately excludes toon bands/specular noise.
                half diffuse = saturate(dot(normalize(i.normalWS), light.direction));
                half3 illumination = _Ambient.xxx + (1 - _Ambient) * diffuse * light.color
                    * light.distanceAttenuation * light.shadowAttenuation;
                return half4(_BaseColor.rgb * illumination, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            // Match the forward pass layout: borrowing Lit's different CBUFFER breaks batching.
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Ambient;
                half _DebugView;
            CBUFFER_END
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float4 clip = TransformWorldToHClip(ApplyShadowBias(p, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
                #endif
                return clip;
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
