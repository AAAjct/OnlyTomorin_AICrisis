Shader "Trial/Spell Surface"
{
    Properties
    {
        [HDR] _Body ("Body", Color) = (0.03, 0.08, 0.16, 0.6)
        [HDR] _Glow ("Glow", Color) = (0.25, 1, 1.3, 1)
        _Seed ("Phase", Float) = 0
        _Glass ("Transparency", Range(0,1)) = 1
        _Fade ("Fade", Range(0,1)) = 1
        [HideInInspector] _Coffin ("Coffin surface", Float) = 0
        [HideInInspector] _Rise ("Coffin height", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BlackCoffinFissures.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Body, _Glow;
                float _Seed, _Glass, _Fade, _Coffin, _Rise;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 local : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.local=input.positionOS.xyz;
                output.uv=input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 face=abs(input.local)*2;
                float nearest=min(max(face.x,face.y),min(max(face.x,face.z),max(face.y,face.z)));
                float edge=smoothstep(.93,.995,nearest);
                float2 uv=input.uv;
                float time=_Time.y+_Seed;
                if(_Coffin>.5)
                {
                    float2 stone=uv*float2(3.7,4.8*_Rise);
                    float grain=CoffinNoise(stone+_Seed);
                    float3 cracks=CoffinFissures(stone.x+.24,frac(stone.y*.26+.13),_Seed+9,1.2);
                    float lip=pow(saturate(uv.y),11)*(1-smoothstep(.45,1,_Rise));
                    float energy=saturate(edge*.60+cracks.y*.61+cracks.z*.15+lip*.44);
                    half3 stoneColor=lerp(_Body.rgb*(.52+grain),_Glow.rgb,energy);
                    stoneColor=lerp(stoneColor,_Body.rgb*.18,cracks.x);
                    return half4(stoneColor,_Body.a);
                }
                float flow=uv.y+uv.x*.27-time*.34+.034*sin(uv.x*29+time*3);
                float bands=pow(.5+.5*sin(flow*43),8);
                float etched=smoothstep(.94,1,cos((uv.x*2.4-uv.y*.45+sin(uv.y*20)*.026)*22));
                float sweep=pow(.5+.5*sin((uv.y+uv.x*.2)*10-time*2.6),10);
                float accent=saturate(edge*.95+bands*.28+sweep*.37+etched*.12);
                half3 rgb=lerp(_Body.rgb,_Glow.rgb,accent);
                half opacity=lerp(_Body.a,_Body.a*(.18+.82*accent),_Glass);
                return half4(rgb,opacity*_Fade);
            }
            ENDHLSL
        }
    }
}
