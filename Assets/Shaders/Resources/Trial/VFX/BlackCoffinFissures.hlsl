#ifndef TRIAL_BLACK_COFFIN_FISSURES_INCLUDED
#define TRIAL_BLACK_COFFIN_FISSURES_INCLUDED

float CoffinHash(float2 p)
{
    float3 q=frac(float3(p.xyx)*.1031);
    q+=dot(q,q.yzx+33.33);
    return frac((q.x+q.y)*q.z);
}

float CoffinNoise(float2 p)
{
    float2 cell=floor(p);
    float2 f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(CoffinHash(cell),CoffinHash(cell+float2(1,0)),f.x),
                lerp(CoffinHash(cell+float2(0,1)),CoffinHash(cell+1),f.x),f.y);
}

float CoffinJag(float depth,float seed)
{
    float segment=floor(depth);
    return lerp(CoffinHash(float2(segment,seed)),CoffinHash(float2(segment+1,seed)),frac(depth))-.5;
}

// Each edge section grows a tapered fracture with two offshoots.
float3 CoffinFissures(float along,float depth,float seed,float growth,out float shear)
{
    float2 cell=float2(floor(along),seed);
    float root=.49+(CoffinHash(cell)-.5)*.39;
    float tip=lerp(.36,1.08,CoffinHash(cell+4.7))*growth;
    float tilt=CoffinHash(cell+11.8)-.5;
    float zig=CoffinJag(depth*8.3+root,cell.x+seed);
    float jag=CoffinJag(depth*24.7,cell.x-seed);
    float spine=root+tilt*depth*.49+zig*.21+jag*.095;
    float span=frac(along);
    float width=lerp(.026,.007,saturate(depth/max(tip,.01)))*(.87+jag*.26);
    float trunk=abs(span-spine)+(1-smoothstep(0,.10,tip-depth));
    float leftStart=lerp(.15,.32,CoffinHash(cell+18.9));
    float rightStart=lerp(.29,.53,CoffinHash(cell+32.6));
    float leftFork=max(0,depth-leftStart);
    float rightFork=max(0,depth-rightStart);
    float left=abs(span-spine+leftFork*(.67+jag*.51))+step(depth,leftStart)+smoothstep(.27,.41,leftFork);
    float right=abs(span-spine-rightFork*(.84+zig*.64))+step(depth,rightStart)+smoothstep(.22,.36,rightFork);
    float distance=min(trunk,min(left,right)*1.62+step(tip,depth));
    float side=span-spine;
    shear=sign(side)*smoothstep(0,.018,abs(side))*(1-smoothstep(.008,.18,trunk));
    float core=1-smoothstep(width*.58,width,distance);
    float rim=1-smoothstep(width*.9,width*2.35,distance);
    float haze=1-smoothstep(width,width*5.7,distance);
    return float3(core,rim,haze);
}

float3 CoffinFissures(float along,float depth,float seed,float growth)
{
    float shear;
    return CoffinFissures(along,depth,seed,growth,shear);
}

#endif
