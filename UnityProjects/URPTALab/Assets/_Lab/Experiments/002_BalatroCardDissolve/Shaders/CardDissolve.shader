Shader "TechArtLab/002/CardDissolve"
{
    Properties
    {
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _PixelSize, _BurnColor1, _BurnColor2, _PatternOffset;
                float _Dissolve, _FieldTime, _EdgeWidth, _TintStrength;
                float _Shadow, _ShadowOpacity, _ViewMode;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            // Capture-derived field. Local UV is top-left based, unlike this mesh's UV.
            // Divide BOTH axes by max(width,height): preserve isotropic logical pixels.
            float DissolveField(float2 localUV, float d, float threshold)
            {
                float2 size = max(_PixelSize.xy, 1.0);
                float largest = max(size.x, size.y);
                float2 gridUV = floor(localUV * size) / largest;
                // One stable offset per playback, never new random values per frame.
                // Leave gridUV unchanged so the border bias stays attached to the card.
                float2 p = (gridUV - 0.5) * 2.3 * largest + _PatternOffset.xy;
                float t = _FieldTime * 10.0 + 2003.0;
                float2 p1 = p + 50.0 * float2(sin(-t/143.6340), cos(-t/99.4324));
                float2 p2 = p + 50.0 * float2(cos(t/53.1532), cos(t/61.4532));
                float2 p3 = p + 50.0 * float2(sin(-t/87.53218), sin(-t/49.0000));
                float field = (1.0 + cos(length(p1)/19.483)
                    + sin(length(p2)/33.155) * cos(p2.y/15.73)
                    + cos(length(p3)/27.193) * sin(p3.x/21.92)) * 0.5;
                float result = 0.5 + 0.5 * cos(threshold/82.612 + (field-0.5)*3.14);
                float2 border = max(gridUV - 0.8, 0.0) + max(0.2 - gridUV, 0.0);
                return result - (border.x + border.y) * (5.0 + 5.0*d) * d;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 card = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float d = saturate(_Dissolve);
                float threshold = d*d*(3.0-2.0*d)*1.02-0.01;
                float result = DissolveField(float2(input.uv.x, 1.0-input.uv.y), d, threshold);
                // Source writes alpha=0 instead of discard. Keep original texture alpha.
                float visible = d < 0.001 ? 1.0 : (result > threshold ? 1.0 : 0.0);
                if (_Shadow > 0.5)
                    return float4(0,0,0, card.a * visible * _ShadowOpacity);
                if (_ViewMode > 1.5)
                    return float4(saturate(result).xxx, card.a);
                if (_ViewMode > 0.5)
                    return float4(1,1,1,card.a * visible);

                if (d > 0.01)
                {
                    if (_BurnColor2.a > 0.01) card.rgb = lerp(card.rgb, _BurnColor2.rgb, _TintStrength*d);
                    else if (_BurnColor1.a > 0.01) card.rgb = lerp(card.rgb, _BurnColor1.rgb, _TintStrength*d);
                }
                float band = max(0.0, 0.5-abs(threshold-0.5)) * _EdgeWidth;
                if (d >= 0.001 && card.a > 0.01 && _BurnColor1.a > 0.01
                    && result > threshold && result < threshold + 0.8*band)
                {
                    if (result < threshold + 0.5*band) card = _BurnColor1;
                    else if (_BurnColor2.a > 0.01) card = _BurnColor2;
                }
                card.a *= visible;
                return card;
            }
            ENDHLSL
        }
    }
}
