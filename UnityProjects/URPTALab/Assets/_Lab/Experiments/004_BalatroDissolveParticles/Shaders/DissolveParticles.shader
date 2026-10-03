Shader "TechArtLab/004/DissolveParticles"
{
    Properties
    {
        _SignedField ("Signed field", 2D) = "white" {}
        _BaseMap ("Card texture", 2D) = "white" {}
        _Dissolve ("Dissolve (0 visible, 1 hidden)", Range(0,1)) = 0
        _FieldTime ("Field time", Float) = 766.55188
        _PatternOffset ("Seeded field offset", Vector) = (0,0,0,0)
        _PixelSize ("Logical card pixels", Vector) = (71,95,0,0)
        _BurnColor1 ("Inner edge", Color) = (0.215686,0.258824,0.266667,1)
        _BurnColor2 ("Outer edge (alpha 0 disables)", Color) = (0.992157,0.635294,0,1)
        _EdgeWidth ("Edge width multiplier", Range(0,2)) = 1
        _TintStrength ("Card tint", Range(0,1)) = 0.6
        _Shadow ("Shadow draw", Float) = 0
        _ShadowOpacity ("Shadow opacity", Range(0,1)) = 0.3
        _ViewMode ("0 Color / 1 Mask / 2 Field", Float) = 0
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
            #include "DissolveCardPass.hlsl"
            ENDHLSL
        }
    }
}
