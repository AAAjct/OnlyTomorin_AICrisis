Shader "Trial/Mirror Model"
{
    Properties
    {
        _BaseMap ("Model texture", 2D) = "white" {}
        _BaseColor ("Model tint", Color) = (1,1,1,1)
        _ShatterAge ("Shatter progress", Range(0,1)) = 0
        _VioletGlass ("Obsidian glass", Range(0,1)) = 0
        _ShardScatter ("Outward scatter", Float) = 1
        _CoffinPrism ("Textured obsidian prism", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _ShatterAge, _VioletGlass, _ShardScatter, _CoffinPrism;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 shard : TEXCOORD1;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half facet : TEXCOORD3;
            };

            float3 Rotate(float3 v,float3 axis,float angle)
            {
                float s,c;
                sincos(angle,s,c);
                return v*c+cross(axis,v)*s+axis*dot(axis,v)*(1-c);
            }

            Varyings Vert(Attributes input)
            {
                float progress=_ShatterAge;
                Varyings output=(Varyings)0;
                if(progress>=1)
                {
                    output.positionCS=float4(2,2,2,1);
                    return output;
                }
                float seed=input.shard.w*2.399963;
                float3 pivot=input.shard.xyz;
                float3 axis=normalize(float3(cos(seed),.58,sin(seed)));
                float3 outward=normalize(pivot+axis*.18);
                float angle=progress*(3.1+1.9*sin(seed));
                float burst=1-exp(-lerp(3.8,8,_VioletGlass)*progress);
                if(_CoffinPrism>.5)
                {
                    outward=normalize(float3(pivot.x+axis.x*.25,pivot.y*.38+.18,pivot.z+axis.z*.25));
                    angle=progress*(5.8+3.4*sin(seed));
                }
                float3 shift=outward*(.7*burst+1.5*progress)*_ShardScatter+float3(0,1.4*progress-3.35*progress*progress,0);
                float chipSize=1;
                if(_VioletGlass>.5 && _CoffinPrism<.5)chipSize=lerp(1,.65,smoothstep(.10,.38,progress));
                float3 displaced=pivot+Rotate(input.positionOS.xyz-pivot,axis,angle)*chipSize+shift;
                output.positionWS=TransformObjectToWorld(displaced);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(Rotate(input.normalOS,axis,angle));
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                output.color=input.color;
                output.facet=frac(input.shard.w*.618034);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_BaseColor*input.color;
                half3 normal=normalize(input.normalWS);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 light=GetMainLight().direction;
                half rim=pow(1-saturate(abs(dot(normal,view))),3);
                half glint=pow(saturate(abs(dot(normal,normalize(view+light)))),24);
                half illumination=.66+.34*saturate(dot(normal,light));
                half3 rgb=albedo.rgb*illumination+half3(.32,.80,1)*rim*.53+half3(1,1.2,1.4)*glint*.84;
                if(_VioletGlass>.5)
                {
                    half tone=dot(albedo.rgb,half3(.30,.59,.11));
                    half facet=pow(saturate(abs(dot(normal,normalize(view+half3(-.42,.33,.20))))),10);
                    half3 glass=half3(.047,.006,.085)*(tone*.50+input.facet*.65+.20)*illumination;
                    rgb=glass+half3(.54,.07,1.0)*rim*.85+half3(.76,.22,1.45)*(glint*.84+facet*.27);
                    if(_CoffinPrism>.5)
                    {
                        half3 model=albedo.rgb*half3(.27,.11,.38)+tone*half3(.08,.025,.13);
                        rgb=(glass+model)*(.55+.45*illumination)
                            +half3(.46,.075,.91)*rim*.53
                            +half3(.70,.24,1.24)*(glint*.68+facet*.19);
                    }
                }
                half opacity=albedo.a*(1-smoothstep(.62,1,_ShatterAge));
                clip(opacity-.035);
                return half4(rgb,opacity);
            }
            ENDHLSL
        }
    }
}
