Shader "Tomorin/Tide Energy"
{
    Properties { _BaseColor("Energy tint",Color)=(.08,.65,1,.8) _Mode("Shell (1) or ribbon (0)",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float2 uv:TEXCOORD2;float4 color:COLOR;};
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _Mode;
            CBUFFER_END
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.color=i.color;return o;}
            half4 Frag(V i):SV_Target
            {
                float t=_Time.y;
                float braid=pow(saturate(.5+.5*sin(i.uv.x*65+i.uv.y*22-t*9)),5);
                float micro=pow(saturate(.5+.5*sin(i.uv.x*117-i.uv.y*51+t*6)),12);
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(GetWorldSpaceViewDir(i.world))))),2.5);
                float edge=pow(saturate(sin(i.uv.y*PI)),.7);
                float intensity=lerp(edge*(.35+braid*.65+micro*.3),.08+rim*.9+braid*.3,_Mode);
                float4 tint=lerp(i.color*_BaseColor,_BaseColor,_Mode);
                return half4(lerp(tint.rgb,tint.rgb*.4+float3(.6,.8,1),braid*.3)*1.6,tint.a*intensity);
            }
            ENDHLSL
        }
    }
}
