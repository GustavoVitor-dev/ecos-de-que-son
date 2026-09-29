// Silhueta de copa e galho no primeiro plano (CopaParalaxe).
//
// Precisa ser desenhada DEPOIS da escuridão do cone (Ecos/Escuridao, fila
// Transparent+100): a copa está entre a câmera e tudo, inclusive o escuro.
// Com o Unlit do URP a fila voltava pra 3000 e a escuridão apagava a copa.
Shader "Ecos/Silhueta"
{
    Properties
    {
        _BaseMap ("Silhueta", 2D) = "white" {}
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+200" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Silhueta"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Entrada { float4 posicao : POSITION; float2 uv : TEXCOORD0; };
            struct Saida { float4 posicao : SV_POSITION; float2 uv : TEXCOORD0; };

            Saida vert (Entrada i)
            {
                Saida o;
                o.posicao = TransformObjectToHClip(i.posicao.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag (Saida i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                return c;
            }
            ENDHLSL
        }
    }
}
