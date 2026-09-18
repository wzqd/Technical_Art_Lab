Shader "TechArtLab/001/CardHover"
{
    Properties
    {
        _BaseMap ("Card texture", 2D) = "white" {}
        _Hover ("Hover", Range(0,1)) = 0
        _Strength ("Strength", Range(0,3)) = 1
        _ScreenScale ("Distance normalization (pixels)", Float) = 250
        _MousePixels ("Mouse (bottom-left pixels)", Vector) = (0,0,0,0)
        _ViewportSize ("Viewport size", Vector) = (1920,1080,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Card"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _MousePixels, _ViewportSize;
                float _Hover, _Strength, _ScreenScale;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 clip = TransformObjectToHClip(input.positionOS);
                // Unity reproduction choice: use unwarped viewport pixels, bottom-left origin.
                // Original GLSL uses vertex_position.xy; its input coordinate contract is unknown.
                float2 ndc = clip.xy / clip.w;
                ndc.y *= _ProjectionParams.x;
                float2 pixels = (ndc * 0.5 + 0.5) * _ViewportSize.xy;
                float midDistance = length(pixels - 0.5 * _ViewportSize.xy) / length(_ViewportSize.xy);
                float2 mouseOffset = (pixels - _MousePixels.xy) / max(_ScreenScale, 1.0);
                float deltaW = 0.2 * (-0.03 - 0.3 * max(0.0, 0.3 - midDistance))
                    * _Hover * dot(mouseOffset, mouseOffset) / max(2.0 - midDistance, 0.1);
                // At strength=0 or hover=0 this is the original clip position.
                // Orthographic demo has w=1. Clamp is a safety choice, not original behavior.
                clip.w = max(clip.w + deltaW * _Strength, 0.2);
                output.positionCS = clip;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Minimal: the supplied fragment shader's dissolve=0, shadow=false branch.
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            }
            ENDHLSL
        }
    }
}
