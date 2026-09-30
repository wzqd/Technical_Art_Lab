Shader "TechArtLab/003/CardDrag"
{
    Properties
    {
        _BaseMap ("Card",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION; float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv; return output;
            }
            half4 Frag(Varyings input):SV_Target
            { return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_Color; }
            ENDHLSL
        }
    }
}
