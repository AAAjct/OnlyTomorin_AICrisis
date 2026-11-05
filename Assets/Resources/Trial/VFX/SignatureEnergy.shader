Shader "Tomorin/Signature Silk Light"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) _BaseMap("Detail",2D)="white"{} }
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
            struct A { float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct V { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            V Vert(A i){V o;o.vertex=TransformObjectToHClip(i.vertex.xyz);o.uv=i.uv;o.color=i.color*_BaseColor;return o;}
            half4 Frag(V i):SV_Target
            {
                float edge=saturate(1-abs(i.uv.y*2-1));
                float flow=.82+.18*sin(i.uv.x*18-_Time.y*7);
                float core=pow(edge,12);
                float glow=pow(edge,1.8)*flow;
                return half4(lerp(i.color.rgb*1.7,float3(1,.94,.98)*2,core*.45),i.color.a*glow);
            }
            ENDHLSL
        }
    }
}
