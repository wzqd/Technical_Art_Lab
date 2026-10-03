            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SignedField); SAMPLER(sampler_SignedField);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _PixelSize, _BurnColor1, _BurnColor2, _PatternOffset;
                float _Dissolve, _FieldTime, _EdgeWidth, _TintStrength;
                float _Shadow, _ShadowOpacity, _ViewMode;
                float4 _MousePixels, _ViewportSize;
                float _Hover, _Strength, _ScreenScale;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                #if defined(CARD_HOVER)
                float4 clip=output.positionCS;
                float2 ndc=clip.xy/clip.w; ndc.y*=_ProjectionParams.x;
                float2 pixels=(ndc*.5+.5)*_ViewportSize.xy;
                float mid=length(pixels-.5*_ViewportSize.xy)/max(length(_ViewportSize.xy),1);
                float2 offset=(pixels-_MousePixels.xy)/max(_ScreenScale,1);
                float delta=.2*(-.03-.3*max(0,.3-mid))*_Hover*dot(offset,offset)/max(2-mid,.1);
                float warpedW=max(clip.w+delta*_Strength,.2);
                // This is a screen-space warp: keep the original z/w depth.
                // Changing w alone can push corners through the near clip plane
                // (especially with reversed Z and a large camera far distance).
                clip.z*=warpedW/clip.w;
                clip.w=warpedW;
                output.positionCS=clip;
                #endif
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 card = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float d = saturate(_Dissolve);
                float threshold = d*d*(3.0-2.0*d)*1.02-0.01;
                float signedField = SAMPLE_TEXTURE2D(_SignedField, sampler_SignedField, float2(input.uv.x,1-input.uv.y)).r;
                float result = signedField + threshold;
                // Source writes alpha=0 instead of discard. Keep original texture alpha.
                float visible = d < 0.001 ? 1.0 : (signedField > 0 ? 1.0 : 0.0);
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
                    && signedField > 0 && signedField < 0.8*band)
                {
                    if (signedField < 0.5*band) card = _BurnColor1;
                    else if (_BurnColor2.a > 0.01) card = _BurnColor2;
                }
                card.a *= visible;
                return card;
            }
