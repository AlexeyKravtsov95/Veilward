Shader "Veilward/InkBattleSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = .005
        _Ghost("Occlusion tint", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _DepthTest("Depth test", Float) = 4
        [Toggle] _DepthWrite("Depth write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompare("Stencil compare", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil pass", Float) = 0
        _StencilWriteMask("Stencil write mask", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask [_StencilWriteMask]
                Comp [_StencilCompare]
                Pass [_StencilPass]
            }
            ZTest [_DepthTest]
            ZWrite [_DepthWrite]
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Cutoff;
            half _Ghost;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 artwork = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(artwork.rgb, half3(.3h, .59h, .11h));
                artwork.rgb = lerp(artwork.rgb, _Tint.rgb * lerp(.48h, 1.h, luminance), _Ghost);
                artwork *= input.color;
                artwork.a *= _Tint.a;
                clip(artwork.a - _Cutoff);
                return artwork;
            }
            ENDHLSL
        }
    }
}
