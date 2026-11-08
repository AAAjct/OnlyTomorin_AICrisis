Shader "Trial/Black Coffin Chant"
{
    Properties
    {
        [PerRendererData] _MainTex ("Glyph atlas", 2D) = "white" {}
        [HideInInspector] _TitlePhase ("First phrase, time, voice pulse, final phrase", Vector) = (0,0,0,0)
        [HideInInspector] _StencilComp ("Stencil comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil write mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil read mask", Float) = 255
        [HideInInspector] _ColorMask ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ COFFIN_TITLE
            #include "UnityCG.cginc"
            #include "BlackCoffinFissures.hlsl"

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _TitlePhase;
            float4 _ClipRect;
            float _UIMaskSoftnessX,_UIMaskSoftnessY;

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                float4 title : TEXCOORD1;
                fixed4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 local : TEXCOORD1;
                float4 mask : TEXCOORD2;
                fixed4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                #if defined(COFFIN_TITLE)
                    if(_TitlePhase.y<0)
                    {
                        output.position=float4(2,2,2,1);
                        return output;
                    }
                #endif
                float4 position=input.position;
                output.local=float3(input.position.xy,0);
                #if defined(COFFIN_TITLE)
                    float column=input.title.x/460;
                    float height=input.title.w-.5;
                    float glyph=input.title.z;
                    // The middle dot belongs to the first phrase. The last two glyphs follow the final call.
                    float formed=lerp(_TitlePhase.x,_TitlePhase.w,step(5.5,glyph));
                    float loose=1-formed;
                    float hook=sin(column*4.4+1.1);
                    float seed=CoffinHash(float2(glyph,12));
                    float arch=18*(1-column*column)-13;
                    float shear=height*(column*26+hook*16)+(height*height-.08)*hook*57;
                    float voice=_TitlePhase.z;
                    float ripple=sin(column*9+height*8-(_TitlePhase.y-1)*8.8)*voice;
                    float offset=(seed-.5)*58;
                    position.x+=shear*(.52+1.16*loose)+loose*(column*54+height*offset+ripple*22);
                    position.y+=.60*arch+height*(10+7*sin(column*5))+loose*(offset+ripple*17);
                    float scale=lerp(.90,1,_TitlePhase.x)+.028*voice*(1-_TitlePhase.w);
                    position.xy*=lerp(1,scale,step(1,_TitlePhase.y));
                    output.local=float3(input.title.xy,formed);
                #endif
                output.position=UnityObjectToClipPos(position);
                output.uv=input.uv;
                output.color=input.color;
                #if defined(UNITY_UI_CLIP_RECT)
                    float2 pixelSize=output.position.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                    float4 rect=clamp(_ClipRect,-2e10,2e10);
                    output.mask=float4(input.position.xy*2-rect.xy-rect.zw,
                        .25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                #endif
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 color=(tex2D(_MainTex,input.uv)+_TextureSampleAdd)*input.color;
                #if defined(COFFIN_TITLE)
                    float2 p=input.local.xy/float2(460,80);
                    float formed=input.local.z;
                    float time=max(0,_TitlePhase.y-1);
                    half rim=saturate(fwidth(color.a)*1.35)*color.a;
                    clip(color.a-.003);
                    float grain=CoffinNoise(p*float2(12,8));
                    float lightPath=frac(p.x*.62-p.y*.31-time*.80+(grain-.5)*.18);
                    float stream=lerp(1-smoothstep(.035,.24,abs(lightPath-.52)),.24+.25*grain,formed);
                    float vein=p.x*6.9+p.y*.54+.24;
                    float fault=abs(frac(vein)-.5+(grain-.5)*.34);
                    half core=1-smoothstep(.012,.038,fault);
                    half glow=1-smoothstep(.038,.085,fault);
                    half3 shade=lerp(half3(.029,.004,.069),half3(.32,.058,.65),saturate(.12+grain*.18+stream*.76));
                    shade+=rim*half3(.34,.14,.67)+glow*(1-core)*half3(.33,.09,.62)*(.4+.6*stream);
                    color.rgb=lerp(shade,half3(.012,.001,.032),core*.90);
                    float pieces=smoothstep(.24,.50,grain+stream*.18);
                    color.a*=lerp(.38+.44*pieces,1,formed);
                #else
                float2 letter=input.local.xy*float2(.030,.024);
                float3 cracks=CoffinFissures(letter.x+letter.y*.45,frac(letter.y+.27),42,1.55);
                color.rgb=lerp(color.rgb,color.rgb*float3(.63,.27,.89),cracks.z*.62);
                color.rgb=lerp(color.rgb,color.rgb*float3(.78,.36,1),cracks.y);
                color.rgb=lerp(color.rgb,color.rgb*float3(.22,.018,.36),cracks.x);
                #endif
                #if defined(UNITY_UI_CLIP_RECT)
                    half2 clipMask=saturate((_ClipRect.zw-_ClipRect.xy-abs(input.mask.xy))*input.mask.zw);
                    color.a*=clipMask.x*clipMask.y;
                #endif
                return color;
            }
            ENDCG
        }
    }
}
