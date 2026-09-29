// Escuridão fora do campo de visão, no estilo Darkwood.
// A malha é montada pelo VisaoDarkwood: alfa 0 no buraco que o jogador enxerga,
// alfa cheio fora dele. ZTest Always porque ela precisa cobrir tudo que está
// fora da visão, inclusive o que é mais alto que o plano onde ela é desenhada.
Shader "Ecos/Escuridao"
{
    Properties
    {
        _Color ("Cor", Color) = (0.008, 0.010, 0.014, 0.9)
        _Grao ("Grão", Range(0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Escuridao"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Grao;
            CBUFFER_END
            // clarão de relâmpago (global, 0..1): levanta a escuridão da tela inteira
            half _EcosRelampago;

            struct Entrada { float4 posicao : POSITION; half4 cor : COLOR; };
            struct Saida { float4 posicao : SV_POSITION; half4 cor : COLOR; };

            Saida vert (Entrada i)
            {
                Saida o;
                o.posicao = TransformObjectToHClip(i.posicao.xyz);
                o.cor = i.cor;
                return o;
            }

            half4 frag (Saida i) : SV_Target
            {
                // grão que mexe: o escuro não é uma chapa, é algo que ferve
                float n = frac(sin(dot(floor(i.posicao.xy * 0.5), float2(12.9898, 78.233)) + _Time.y * 11.0) * 43758.5453);
                half a = i.cor.a * _Color.a * (1.0 - _Grao + _Grao * n) * (1.0 - 0.8 * saturate(_EcosRelampago));
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
