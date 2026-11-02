Shader "Tomorin/Ink Night Sky"
{
    Properties
    {
        _HorizonColor("Horizon",Color)=(.12,.21,.25,1)
        _ZenithColor("Zenith",Color)=(.012,.028,.055,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.direction=v.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                float t=smoothstep(-.04,.72,d.y);
                half3 color=lerp(_HorizonColor.rgb,_ZenithColor.rgb,t);
                float cloud=sin(d.x*22+d.z*13+sin(d.z*29)*.8)*sin(d.x*9-d.z*17);
                color+=cloud*.009*saturate(1-d.y);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
