Shader "Tomorin/UI/Ink Verdict"
{
    Properties
    {
        [PerRendererData] _MainTex("Ink artwork",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Reveal("Ink diffusion",Range(0,1))=1
        _Split("Incision",Range(0,1))=0
        _Red("Cinnabar diffusion",Range(0,1))=0
        _Side("Cut side",Float)=-1
        _Layer("Layer: ink, paper, blade",Float)=0
        _Opacity("Opacity",Range(0,1))=1
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False"}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            sampler2D _MainTex;
            float4 _Color;
            float _Reveal,_Split,_Red,_Side,_Layer,_Opacity;
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv,p=uv-.5;
                float n=noise(uv*29)*.65+noise(uv*83)*.35;
                float seam=p.x+.20*p.y;
                if(_Layer>.5 && _Layer<1.5)
                {
                    float radius=length(p*float2(1,1.08));
                    float paper=1-smoothstep(.18,.5,radius+(n-.5)*.065);
                    return float4(float3(.93,.90,.83)*i.color.rgb,paper*.86*_Reveal*i.color.a);
                }
                if(_Layer>1.5)
                {
                    float blade=1-smoothstep(.001,.006,abs(seam));
                    float tip=smoothstep(.94-_Reveal*.90,.99-_Reveal*.90,uv.y);
                    float ends=smoothstep(.05,.16,uv.y)*(1-smoothstep(.86,.96,uv.y));
                    return float4(float3(1,.90,.73)*i.color.rgb,blade*tip*ends*_Reveal*_Opacity*i.color.a);
                }
                // Complementary masks slice the actual brushwork into two independently moving halves.
                float halfMask=saturate(.5+_Side*seam*1400-_Split*.75);
                float4 artwork=tex2D(_MainTex,uv);
                float source=artwork.a;
                float wet=tex2D(_MainTex,uv+float2(.003,0)).a+tex2D(_MainTex,uv-float2(.003,0)).a;
                wet+=tex2D(_MainTex,uv+float2(0,.003)).a+tex2D(_MainTex,uv-float2(0,.003)).a;
                float ink=saturate(source+wet*.12*(1-_Reveal));
                float arrival=length(p)*.58+n*.38;
                float bloom=smoothstep(arrival,arrival+.20,_Reveal*1.15);
                float dye=smoothstep(abs(seam)*1.2+n*.15,abs(seam)*1.2+n*.15+.30,_Red*1.25)*step(.001,_Red);
                float density=lerp(.07,0,source)+artwork.r*.32;
                float3 black=float3(.018,.023,.022)+density;
                float3 red=lerp(float3(.38,.012,.018),float3(.78,.035,.025),n*.6+source*.4);
                return float4(lerp(black,red,dye)*i.color.rgb,ink*bloom*halfMask*i.color.a);
            }
            ENDHLSL
        }
    }
}
