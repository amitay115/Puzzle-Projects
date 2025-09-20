Shader "Custom/URP_OutlineOnly"
{
    Properties
    {
        [HDR]_OutlineColor("Outline Color", Color) = (0,1,1,1)
        _ThicknessWorld("Outline Thickness (world units)", Float) = 0.02
        _Alpha("Alpha", Range(0,1)) = 1
        _HDRIntensity("HDR Intensity", Float) = 3
    }
    SubShader
    {
        Tags{ "RenderType"="Transparent" "Queue"="Transparent+10" }

        Pass
        {
            Name "OUTLINE"
            Tags{ "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual          // 🔑 מצייר אחרי המודל: עובר רק היכן שאנחנו קרובים/קרובים-יותר מהרקע
            Blend SrcAlpha OneMinusSrcAlpha
            Offset 0, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionHCS:SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _ThicknessWorld;
                float  _Alpha;
                float  _HDRIntensity;
            CBUFFER_END

            V vert(A IN)
            {
                V O;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = normalize(TransformObjectToWorldNormal(IN.normalOS));
                posWS += nWS * _ThicknessWorld;
                O.positionHCS = TransformWorldToHClip(posWS);
                return O;
            }

            half4 frag(V IN):SV_Target
            {
                float3 rgb = _OutlineColor.rgb * _HDRIntensity;
                return half4(rgb, _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}