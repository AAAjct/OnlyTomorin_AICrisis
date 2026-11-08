Shader "Trial/Black Coffin Pressure"
{
    Properties
    {
        [HideInInspector] _ChantPhase ("Progress, time, motion, title", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "BlackCoffinFissures.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ChantPhase;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float progress=_ChantPhase.x;
                float time=_ChantPhase.y;
                float motion=_ChantPhase.z;
                float onset=smoothstep(0,.032,progress);
                float pressure=onset*lerp(.55,1,smoothstep(.09,.91,progress));
                float breath=.94+.06*cos(time*2.55);

                float2 center=uv-float2(.50,.53);
                float radius=length(center*float2(1.10,1));
                float perimeter=smoothstep(.19,.66,radius);
                float2 fold=float2(sin(center.y*12+time*1.15),sin(center.x*10-time*1.28));
                float2 domain=uv+fold*perimeter*motion*.040;
                float2 edge=min(domain,1-domain);
                float2 shear=0;
                float3 cracks=0;
                // Bend the fracture field with the same pressure waves as the scene.
                if(edge.x<.42)
                {
                    bool left=uv.x<.5;
                    float depth=max(0,edge.x)/(.33+.052*sin(domain.y*12+2));
                    cracks=CoffinFissures(domain.y*(left?2.65:3.30)+(left?.34:.08),depth,left?28:14,1.12,shear.y);
                }
                if(edge.y<.42)
                {
                    bool upper=uv.y<.5;
                    float depth=max(0,edge.y)/(.34+.047*cos(domain.x*14));
                    cracks=max(cracks,CoffinFissures(domain.x*(upper?3.85:3.05)+(upper?.71:.44),depth,upper?12:64,1.13,shear.x));
                }

                float twist=.15*sin(radius*8-time*1.6)+.085;
                float2 tangent=float2(-center.y,center.x);
                float2 compression=(center*.17+tangent*twist+fold*.044)*perimeter;
                float2 displacement=(compression+shear*.027)*pressure*motion*breath;
                half4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv-displacement));

                float ink=CoffinNoise(domain*float2(5.5,3.2)+float2(time*.09,-time*.15));
                float threads=CoffinNoise(domain*float2(52,2.1)-float2(time*.13,time*.26));
                float shadow=perimeter*(.60+.30*ink+.10*threads)*breath;
                half luminance=dot(scene.rgb,half3(.2126,.7152,.0722));
                half3 color=lerp(scene.rgb,luminance*half3(.76,.58,1),pressure*(.30+.43*perimeter));
                color*=1-pressure*(.18+.53*shadow);
                color+=half3(.029,.002,.057)*pressure*shadow;
                color=lerp(color,half3(.008,.001,.019),pressure*cracks.x*.96);
                float rim=cracks.y*(1-cracks.x)*(.46+.54*ink);
                color+=pressure*(rim*half3(.25,.022,.49)+cracks.z*half3(.038,.002,.081))*breath;
                float titleField=1-smoothstep(.02,.19,abs(center.y+(ink-.5)*.037));
                color=lerp(color,half3(.013,.002,.028),_ChantPhase.w*(.25+titleField*.60));
                return half4(color,scene.a);
            }
            ENDHLSL
        }
    }
}
