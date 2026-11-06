Shader "Tomorin/InkWash"
{
    Properties
    {
        _BaseColor("Ink tint", Color) = (1,1,1,1)
        _Dissolve("Dissolve", Range(0,1)) = 0
        _Feather("Feather ribbon", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Dissolve, _Feather;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings o; o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);o.color=input.color*_BaseColor;o.uv=input.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz*7+i.world.y*float2(2.3,4.1);
                float grain=Noise(p)*.65+Noise(p*2.7)*.35;
                float feather=smoothstep(.03,.28+grain*.18,i.uv.y);
                float erosion=smoothstep(_Dissolve-.12,_Dissolve+.2,grain);
                half4 c=i.color;
                c.rgb*=.65+grain*.35;
                c.a*=lerp(1,feather,_Feather)*erosion;
                return c;
            }
            ENDHLSL
        }
    }
}
