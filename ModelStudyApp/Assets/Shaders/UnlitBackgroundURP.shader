Shader "Custom/UnlitBackgroundURP"
{
    Properties{
        _MainTex("Background", 2D) = "white" {}
        [HDR]_Tint("Tint", Color) = (1,1,1,1)
        _HDRIntensity("HDR Intensity", Float) = 1.5
        _Alpha("Alpha", Range(0,1)) = 1
    }
    SubShader{
        Tags{ "Queue"="Background" "RenderType"="Opaque" } // מצויר ראשון
        Pass{
            Name "Background"
            Tags{ "LightMode"="UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{ float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V{ float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Tint;
                float _HDRIntensity, _Alpha;
            CBUFFER_END
            V vert(A IN){ V O; O.positionHCS = TransformObjectToHClip(IN.positionOS.xyz); O.uv = TRANSFORM_TEX(IN.uv,_MainTex); return O; }
            half4 frag(V IN):SV_Target{
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                c.rgb *= _Tint.rgb * _HDRIntensity; c.a *= _Alpha * _Tint.a; return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}