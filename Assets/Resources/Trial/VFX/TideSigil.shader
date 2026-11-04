Shader "Tomorin/Tide Sigil"
{
    Properties { _Tint("Warning colour",Color)=(1,.25,.12,1) _Charge("Impact progress",Range(0,1))=0 _Center("Centre",Vector)=(0,0,0,0) _Inner("Safe inner radius",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint,_Center;float _Charge,_Inner;
            CBUFFER_END
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(q),Hash(q+float2(1,0)),f.x),lerp(Hash(q+float2(0,1)),Hash(q+1),f.x),f.y);}
            half4 Frag(V i):SV_Target
            {
                float2 p=i.world.xz-_Center.xz;float r=length(p);
                clip(r-max(0,_Inner-.02));
                float n=Noise(i.world.xz*2.5+_Time.y*.2);
                float rings=pow(saturate(.5+.5*sin(r*14-_Time.y*3+n*2)),18);
                float veins=pow(saturate(1-abs(Noise(i.world.xz*5+float2(0,_Time.y*.12))-.48)*9),5);
                float ticks=pow(saturate(.5+.5*sin(atan2(p.x,p.y)*48)),18)*pow(saturate(.5+.5*sin(r*3)),24);
                float charge=smoothstep(.7,1,_Charge);
                float alpha=(.07+.08*n+veins*.11+rings*.14+ticks*.16)*(1+charge*.5);
                return half4(lerp(_Tint.rgb,float3(1,.88,.65),saturate(rings+ticks)*.45)*(1+charge*.45),alpha*_Tint.a);
            }
            ENDHLSL
        }
    }
}
