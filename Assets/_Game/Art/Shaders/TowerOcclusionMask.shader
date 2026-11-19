Shader "Veilward/TowerOcclusionMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            ZTest LEqual ZWrite Off ColorMask 0 Cull Back
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
