Shader "Trial/Mirror Title"
{
    Properties
    {
        [PerRendererData] _MainTex ("Glyph atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                float4 shard : TEXCOORD1;
                fixed4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 shard : TEXCOORD1;
                float2 local : TEXCOORD2;
                fixed4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position=UnityObjectToClipPos(input.position);
                output.uv=input.uv;
                output.shard=input.shard;
                output.local=input.position.xy;
                output.color=input.color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                half alpha=(tex2D(_MainTex,input.uv)+_TextureSampleAdd).a*input.color.a;
                float3 edges=float3(input.shard.xy,1-input.shard.x-input.shard.y);
                float3 inside=smoothstep(0,max(fwidth(edges)*1.35,.001),edges);
                float bevel=1-min(inside.x,min(inside.y,inside.z));
                float glint=1-smoothstep(.012,.12,abs(frac(input.local.x*.003+input.local.y*.006-_Time.y*.85+input.shard.z*.14)-.5));
                float facet=saturate(input.shard.z*.83+input.shard.w*.28);
                half3 glass=lerp(half3(.105,.26,.40),half3(.66,.96,1),facet);
                glass=lerp(glass,half3(.90,1,1),saturate(bevel*.69+glint*.52));
                return fixed4(glass,alpha);
            }
            ENDCG
        }
    }
}
