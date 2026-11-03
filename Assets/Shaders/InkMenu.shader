Shader "Tomorin/Ink Menu"
{
    Properties
    {
        _MainTex("Artwork",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Motion("Motion",Float)=1
        _Pointer("Pointer",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            sampler2D _MainTex;float4 _Color,_Pointer;float _Motion;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            float4 frag(v2f i):SV_Target
            {
                float t=_Time.y*_Motion;
                float mask=smoothstep(.4,.85,i.uv.x);
                float2 uv=i.uv;
                // Keep the painted background fixed; only the mist changes over time.
                float4 col=tex2D(_MainTex,saturate(uv))*i.color;
                float mist=pow(saturate(sin(uv.x*8+uv.y*5+t*.12)*.5+.5),5)*mask*(1-smoothstep(.1,.65,uv.y));
                col.rgb=lerp(col.rgb,float3(.94,.92,.87),mist*.075*_Motion);
                return col;
            }
            ENDHLSL
        }
    }
}
