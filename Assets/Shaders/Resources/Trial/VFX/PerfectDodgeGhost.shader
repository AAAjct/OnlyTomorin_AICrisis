Shader "Trial/Perfect Dodge Ghost"
{
    Properties
    {
        _BaseColor ("Ink tint", Color) = (.009,.006,.014,.8)
        _Dissolve ("Ink erosion", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Dissolve;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash(float2 coordinate)
            {
                float3 p=frac(coordinate.xyx*.1031);
                p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }

            float Noise(float2 coordinate)
            {
                float2 cell=floor(coordinate);
                float2 f=frac(coordinate);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                            lerp(Hash(cell+float2(0,1)),Hash(cell+float2(1,1)),f.x),f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim=pow(1-saturate(dot(normalize(input.normalWS),view)),2.6);
                float2 paper=float2(input.positionWS.x+input.positionWS.z*.73,input.positionWS.y);
                float grain=Noise(paper*float2(9,14))*.64+Noise(paper*float2(22,47))*.36;
                float ink=smoothstep(_Dissolve-.08,_Dissolve+.12,grain);
                half fringe=smoothstep(_Dissolve-.14,_Dissolve-.05,grain)*(1-ink)*saturate(_Dissolve*8);
                half3 wash=_BaseColor.rgb*(.48+.52*grain);
                half3 graphite=wash+half3(.043,.043,.047)*(rim*.72+fringe*.42);
                half opacity=_BaseColor.a*(ink*(.68+rim*.26)+fringe*.32);
                return half4(graphite,opacity);
            }
            ENDHLSL
        }
    }
}
