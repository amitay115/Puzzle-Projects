Shader "Custom/UnlitOutlineURP"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0, 1, 1, 1)
        _OutlineThickness ("Outline Thickness (world units)", Float) = 0.005
        _ZBias ("Z Bias", Float) = -1.0
        _Alpha ("Alpha", Range(0,1)) = 1.0
        _HDRIntensity ("HDR Intensity (Bloom Boost)", Float) = 4.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        LOD 100

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Offset [_ZBias], [_ZBias]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _ZBias;
                float  _Alpha;
                float  _HDRIntensity;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = normalize(TransformObjectToWorldNormal(IN.normalOS));
                posWS += nWS * _OutlineThickness;

                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 rgb = _OutlineColor.rgb * _HDRIntensity; // דחיפה ל-HDR
                return half4(rgb, _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
