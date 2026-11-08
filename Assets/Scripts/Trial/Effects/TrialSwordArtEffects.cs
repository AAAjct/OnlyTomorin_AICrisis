using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        // Bounded pools share materials and geometry buffers. No per-frame object creation.
        sealed class SwordArtVisual
        {
            public Transform root;
            public LineRenderer core,glow,filament,detail;
            public Mesh mesh;
            public MeshRenderer surface;
            public Vector3[] vertices=new Vector3[130],points=new Vector3[65];
            public Color[] colors=new Color[130];
            public int[] triangles=new int[384];
            public TrialCombatEvent cue;
            public float age,duration;
            public TrialPlayer owner;
            public bool flight;
            public Transform head;
            public float nextEmber;
            public float waveStopAge,wavePreviousAge;
            public Mesh wakeMesh;
            public MeshRenderer wakeSurface;
            public Vector3[] wakeVertices;
            public Color[] wakeColors;
        }
        readonly SwordArtVisual[] swordArts=new SwordArtVisual[40];
        int nextSwordArt;
        Mesh dragonHeadMesh;
        Texture2D swordGlowTexture;
        Material swordGlowMaterial;
        Material crimsonEdgeMaterial,crimsonGlowMaterial;
        static readonly Color CrimsonWave=new Color(1,.012f,.045f);
        static readonly Color SakuraPink=new Color(1,.44f,.68f);
        static readonly Color ThunderGold=new Color(1,.80f,.14f);
        static readonly Vector3[] DragonOutline={new Vector3(-.18f,.1f,-.7f),new Vector3(-.52f,.65f,-.8f),new Vector3(-.28f,.27f,-.24f),new Vector3(-.25f,.16f,.23f),new Vector3(-.13f,.1f,.75f),new Vector3(.13f,.1f,.75f),new Vector3(.25f,.16f,.23f),new Vector3(.28f,.27f,-.24f),new Vector3(.52f,.65f,-.8f),new Vector3(.18f,.1f,-.7f),new Vector3(.3f,-.16f,-.1f),new Vector3(.16f,-.20f,.56f),new Vector3(-.16f,-.20f,.56f),new Vector3(-.3f,-.16f,-.1f),new Vector3(-.25f,.05f,.30f),new Vector3(-.72f,.14f,.45f),new Vector3(-1,.28f,.18f),new Vector3(-.72f,.14f,.45f),new Vector3(-.25f,.05f,.30f),new Vector3(.25f,.05f,.30f),new Vector3(.72f,.14f,.45f),new Vector3(1,.28f,.18f)};

        SwordArtVisual Art(TrialCombatEvent cue,float duration,bool flight=false)
        {
            int index=nextSwordArt++%swordArts.Length;var art=swordArts[index];
            if(art==null)
            {
                var root=new GameObject("Sword art ribbon "+index).transform;root.SetParent(transform,false);
                art=new SwordArtVisual{root=root};
                art.core=SwordLine(root,"Pearl edge");art.glow=SwordLine(root,"Soft aura");art.filament=SwordLine(root,"Trailing filament");art.detail=SwordLine(root,"Silhouette details");
                if(!swordGlowMaterial)
                {
                    swordGlowTexture=new Texture2D(1,32,TextureFormat.RGBA32,false){name="Sword aura soft edge",wrapMode=TextureWrapMode.Clamp};
                    var pixels=new Color[32];for(int k=0;k<32;k++){float edge=Mathf.Abs((k-15.5f)/15.5f);pixels[k]=new Color(1,1,1,Mathf.Pow(1-edge,2));}swordGlowTexture.SetPixels(pixels);swordGlowTexture.Apply();
                    swordGlowMaterial=new Material(particles);swordGlowMaterial.SetTexture("_BaseMap",swordGlowTexture);swordGlowMaterial.SetInt("_DstBlend",(int)BlendMode.One);
                }
                art.glow.sharedMaterial=swordGlowMaterial;
                art.mesh=new Mesh{name="Sword art curved surface "+index};art.mesh.MarkDynamic();
                root.gameObject.AddComponent<MeshFilter>().sharedMesh=art.mesh;art.surface=root.gameObject.AddComponent<MeshRenderer>();art.surface.sharedMaterial=particles;art.surface.shadowCastingMode=ShadowCastingMode.Off;art.surface.receiveShadows=false;
                for(int i=0;i<64;i++){int j=i*6,v=i*2;art.triangles[j]=v;art.triangles[j+1]=v+1;art.triangles[j+2]=v+2;art.triangles[j+3]=v+1;art.triangles[j+4]=v+3;art.triangles[j+5]=v+2;}
                art.mesh.vertices=art.vertices;art.mesh.triangles=art.triangles;
                var uv=new Vector2[130];for(int i=0;i<65;i++){uv[i*2]=new Vector2(i/64f,0);uv[i*2+1]=new Vector2(i/64f,1);}art.mesh.uv=uv;
                foreach(var line in new[]{art.core,art.glow,art.filament,art.detail}){line.useWorldSpace=true;line.positionCount=65;line.numCornerVertices=3;line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,.8f),new Keyframe(.75f,1),new Keyframe(1,0));}
                art.head=new GameObject("Sculpted dragon head").transform;art.head.SetParent(root,false);
                if(!dragonHeadMesh)BuildDragonHead();
                art.head.gameObject.AddComponent<MeshFilter>().sharedMesh=dragonHeadMesh;
                var face=art.head.gameObject.AddComponent<MeshRenderer>();face.sharedMaterial=particles;face.shadowCastingMode=ShadowCastingMode.Off;
                swordArts[index]=art;
            }
            if(art.owner && art.owner.Weapon)art.owner.Weapon.gameObject.SetActive(true);
            art.root.gameObject.SetActive(true);art.root.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            art.cue=cue;art.age=0;art.duration=duration;art.flight=flight;
            art.waveStopAge=-1;art.wavePreviousAge=0;
            if(art.wakeSurface)art.wakeSurface.enabled=false;
            art.nextEmber=0;art.head.gameObject.SetActive(cue.cue==TrialCombatCue.ThunderDragon);
            art.owner=TrialNetworkManager.SessionActive?TrialNetworkManager.Current.PlayerById(cue.player):game.Player;
            art.surface.enabled=true;art.detail.enabled=false;
            art.core.enabled=art.glow.enabled=art.filament.enabled=true;
            bool inkCut=cue.cue==TrialCombatCue.RisingCut || cue.cue==TrialCombatCue.CrescentCut || cue.cue==TrialCombatCue.RollFlash || cue.cue==TrialCombatCue.WeaveSigil || cue.cue==TrialCombatCue.MartialStroke;
            bool crimson=inkCut || cue.cue==TrialCombatCue.CrimsonBlade || cue.cue==TrialCombatCue.MoonBlade || IsSwordMotionCue(cue.cue);
            art.surface.sharedMaterial=crimson?InkMaterial:particles;
            if(crimson && !crimsonEdgeMaterial)
            {
                // HDR unlit red drives bloom without washing the edge into white or pink.
                crimsonEdgeMaterial=new Material(particles){name="Scarlet crescent emissive edge"};
                crimsonEdgeMaterial.SetColor("_BaseColor",new Color(4.6f,.035f,.12f,1));
                crimsonEdgeMaterial.SetInt("_DstBlend",(int)BlendMode.One);
                crimsonGlowMaterial=new Material(swordGlowMaterial){name="Scarlet crescent soft radiance"};
                crimsonGlowMaterial.SetColor("_BaseColor",new Color(3.2f,.015f,.07f,1));
            }
            // Slots are reused by all sword arts: restore their original materials as well.
            art.core.sharedMaterial=art.filament.sharedMaterial=crimson?crimsonEdgeMaterial:particles;
            art.glow.sharedMaterial=crimson?crimsonGlowMaterial:swordGlowMaterial;
            art.detail.sharedMaterial=crimson?crimsonGlowMaterial:particles;
            if(cue.cue!=TrialCombatCue.ThunderDragon && (crimson || cue.style>0))ApplySwordPalette(art);
            UpdateArt(art);return art;
        }
        bool PlaySwordArtCue(TrialCombatEvent cue,bool local)
        {
            if(cue.cue==TrialCombatCue.SwordGuardReturn)
            {
                Sound("parry",cue.position,.95f);Sound("swing",cue.position,1.15f);
                Burst(cue.position,new Color(.35f,.8f,1),32);
                SlashEffect(cue.position,cue.forward,new Color(.4f,.85f,1),1.8f);
                cue.cue=TrialCombatCue.ThrustTrace;Art(cue,.24f);
                if(local){game.CameraRig.Kick(.15f);Rumble(.3f,.55f,.08f);}
                return true;
            }
            if(cue.cue==TrialCombatCue.SwordRecall || cue.cue==TrialCombatCue.SpiralSword)
            {
                var owner=TrialNetworkManager.SessionActive?TrialNetworkManager.Current.PlayerById(cue.player):game.Player;
                foreach(var old in swordArts)
                    if(old!=null && old.owner==owner && old.cue.cue==TrialCombatCue.SpiralSword)old.root.gameObject.SetActive(false);
                if(owner && owner.Weapon)owner.Weapon.gameObject.SetActive(true);
                if(cue.cue==TrialCombatCue.SwordRecall)
                {
                    cue.cue=TrialCombatCue.EmpoweredStep;Art(cue,.22f);Sound("skillRecall",cue.end);
                    if(local)Rumble(.22f,.65f,.075f);
                    return true;
                }
            }
            if(PlayMartialCue(cue,local))return true;
            if(PlaySwordMotionCue(cue,local))return true;
            if(cue.cue==TrialCombatCue.WeaveSigil){Art(cue,.42f);Burst(cue.position,StyleColor(cue.style),18);return true;}
            if(cue.cue==TrialCombatCue.RollFlash)
            {
                Art(cue,.24f);InkBloom(cue.position,-cue.forward,1.2f);Sound("cutDraw",cue.position);
                if(local){game.CameraRig.Kick(.16f);Rumble(.24f,.45f,.07f);}return true;
            }
            if(cue.cue==TrialCombatCue.CrimsonBlade || cue.cue==TrialCombatCue.MoonBlade)
            {Art(cue,TrialSwordFlight.Lifetime(cue.cue)+CrescentTrailSeconds);Sound(cue.cue==TrialCombatCue.MoonBlade?"skillMoon":"skillCrimson",cue.position);return true;}
            if(cue.cue==TrialCombatCue.ThunderDragon || cue.cue==TrialCombatCue.SpiralSword)
            {
                Art(cue,TrialParryFlight.Duration(cue.cue,cue.amount),true);
                bool thunder=cue.cue==TrialCombatCue.ThunderDragon;
                Sound(thunder?"spellThunder":"skillSpiral",cue.position);
                if(local)
                {
                    if(thunder)game.Hud.Notify("破道之六十三 · 雷吼炮 · 雷龙",1.4f);
                    game.CameraRig.Kick(thunder?.28f:.10f);
                    if(thunder)Rumble(.74f,.84f,.17f);
                    else Rumble(.25f,.45f,.10f);
                }
                return true;
            }
            if(cue.cue==TrialCombatCue.RisingCut || cue.cue==TrialCombatCue.CrescentCut || cue.cue==TrialCombatCue.ThrustTrace || cue.cue==TrialCombatCue.EmpoweredStep)
            {Art(cue,cue.cue==TrialCombatCue.EmpoweredStep?.45f:.28f);return true;}
            return false;
        }
        void SakuraCorridor(Vector3 from,Vector3 end,Vector3 forward)
        {
            EmitPetals(from,end);
            Art(new TrialCombatEvent{cue=TrialCombatCue.SakuraScatter,position=from,end=end,forward=forward},.85f);
            for(int i=0;i<4;i++)SakuraCut(Vector3.Lerp(from,end,(i+1)/5f),forward,i);
        }
        void PaintLine(LineRenderer line,Vector3[] points,Color tint,float width,float fade)
        {line.SetPositions(points);tint.a*=fade;line.startColor=line.endColor=tint;line.widthMultiplier=width;}

        const float CrescentTrailSeconds=.26f;
        const int CrescentWakeRows=9;
        static float CrescentEnvelope(float u)
        {
            // Float sin(PI) can be negative: fractional powers need a clamped base.
            return u<=0 || u>=1?0:Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.75f);
        }
        static bool Finite(Vector3 p)=>float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        bool UploadSwordMesh(SwordArtVisual art)
        {
            foreach(var p in art.vertices)if(!Finite(p)){art.root.gameObject.SetActive(false);return false;}
            art.mesh.vertices=art.vertices;art.mesh.colors=art.colors;art.mesh.RecalculateBounds();return true;
        }
        void InitializeCrescentWake(SwordArtVisual art)
        {
            if(art.wakeMesh)return;
            var obj=new GameObject("Black scarlet flight wake");obj.transform.SetParent(art.root,false);
            art.wakeMesh=new Mesh{name="Crescent swept flight history"};art.wakeMesh.MarkDynamic();
            obj.AddComponent<MeshFilter>().sharedMesh=art.wakeMesh;
            art.wakeSurface=obj.AddComponent<MeshRenderer>();art.wakeSurface.sharedMaterial=InkMaterial;
            art.wakeSurface.shadowCastingMode=ShadowCastingMode.Off;art.wakeSurface.receiveShadows=false;
            art.wakeVertices=new Vector3[65*CrescentWakeRows];art.wakeColors=new Color[art.wakeVertices.Length];
            var indices=new int[(CrescentWakeRows-1)*64*6];
            for(int row=0;row<CrescentWakeRows-1;row++)for(int i=0;i<64;i++)
            {
                int v=row*65+i,j=(row*64+i)*6;
                indices[j]=v;indices[j+1]=v+65;indices[j+2]=v+1;
                indices[j+3]=v+1;indices[j+4]=v+65;indices[j+5]=v+66;
            }
            art.wakeMesh.vertices=art.wakeVertices;art.wakeMesh.triangles=indices;
            var uv=new Vector2[art.wakeVertices.Length];
            for(int row=0;row<CrescentWakeRows;row++)for(int i=0;i<65;i++)uv[row*65+i]=new Vector2(i/64f,1-row/(float)(CrescentWakeRows-1));
            art.wakeMesh.uv=uv;
        }
        void UpdateCrescentArt(SwordArtVisual art)
        {
            var cue=art.cue;
            Vector3 forward=cue.forward.sqrMagnitude>.01f?cue.forward.normalized:Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            if(right.sqrMagnitude<.01f)right=Vector3.right;
            // Equal orthogonal axes retain a circular edge on the tilted air slash.
            Vector3 axis=cue.cue==TrialCombatCue.CrimsonBlade?(right+Vector3.Cross(forward,right)*.32f).normalized:right;
            float radius=cue.cue==TrialCombatCue.MoonBlade?1.6f:1.4f;
            float speed=TrialSwordFlight.Speed(cue.cue),lifetime=TrialSwordFlight.Lifetime(cue.cue);
            float headAge=Mathf.Min(art.age,lifetime);
            if(art.waveStopAge<0 && headAge>art.wavePreviousAge)
            {
                Vector3 from=cue.position+forward*(speed*art.wavePreviousAge);
                float travel=speed*(headAge-art.wavePreviousAge);
                if(Physics.SphereCast(from,TrialSwordFlight.Radius(cue.cue),forward,out var hit,travel,1,QueryTriggerInteraction.Ignore))
                    art.waveStopAge=art.wavePreviousAge+hit.distance/speed;
                else if(art.age>=lifetime)art.waveStopAge=lifetime;
            }
            if(art.waveStopAge>=0)headAge=Mathf.Min(headAge,art.waveStopAge);
            art.wavePreviousAge=headAge;
            float fade=art.waveStopAge<0?1:1-Mathf.SmoothStep(0,1,(art.age-art.waveStopAge)/CrescentTrailSeconds);
            if(fade<=0){art.root.gameObject.SetActive(false);return;}
            Vector3 centre=cue.position+forward*(speed*headAge);
            for(int i=0;i<65;i++)
            {
                float u=i/64f,angle=Mathf.Lerp(-1.65f,1.65f,u),envelope=CrescentEnvelope(u);
                Vector3 radial=axis*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                Vector3 point=centre+radial*radius;
                art.points[i]=point;
                float bleed=.8f+Mathf.PerlinNoise(u*18,headAge*1.2f)*.32f;
                art.vertices[i*2]=point-radial*(.60f*envelope*bleed);art.vertices[i*2+1]=point;
                // The near-black alpha-blended body remains visible inside the red rim.
                art.colors[i*2]=new Color(.003f,.0002f,.001f,.98f*fade*envelope);
                art.colors[i*2+1]=new Color(.012f,.001f,.003f,.98f*fade*envelope);
            }
            if(!UploadSwordMesh(art))return;
            PaintLine(art.core,art.points,new Color(1,1,1,.98f),.038f,fade);
            PaintLine(art.glow,art.points,new Color(1,1,1,.27f),.20f,fade);
            if(art.age>=art.nextEmber)
            {art.nextEmber=art.age+.055f;InkBloom(centre-forward*.35f,-forward*.6f,.55f);}
            InitializeCrescentWake(art);art.wakeSurface.enabled=true;
            for(int row=0;row<CrescentWakeRows;row++)
            {
                float history=row/(float)(CrescentWakeRows-1),sampleAge=Mathf.Max(0,headAge-history*CrescentTrailSeconds);
                Vector3 past=cue.position+forward*(speed*sampleAge);
                for(int i=0;i<65;i++)
                {
                    float u=i/64f,angle=Mathf.Lerp(-1.65f,1.65f,u),envelope=CrescentEnvelope(u);
                    Vector3 radial=axis*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                    int v=row*65+i;
                    float bleed=Mathf.PerlinNoise(u*21,history*8+headAge)*.17f*history;
                    art.wakeVertices[v]=past+radial*(radius*(1-history*.22f)-(.25f+bleed)*envelope);
                    float alpha=Mathf.Pow(1-history,2)*envelope*fade*.70f;
                    art.wakeColors[v]=new Color(.006f,.002f,.009f,alpha);
                }
            }
            foreach(var p in art.wakeVertices)if(!Finite(p)){art.root.gameObject.SetActive(false);return;}
            art.wakeMesh.vertices=art.wakeVertices;art.wakeMesh.colors=art.wakeColors;art.wakeMesh.RecalculateBounds();
            // Both tip streaks trace past flight positions, rather than offset arcs.
            for(int side=0;side<2;side++)
            {
                float angle=side==0?-1.48f:1.48f;
                Vector3 radial=axis*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                for(int i=0;i<65;i++)
                {
                    float history=1-i/64f,sampleAge=Mathf.Max(0,headAge-history*CrescentTrailSeconds);
                    art.points[i]=cue.position+forward*(speed*sampleAge)+radial*radius*(1-history*.22f);
                }
                var line=side==0?art.filament:art.detail;line.enabled=true;line.positionCount=65;
                PaintLine(line,art.points,new Color(1,1,1,.30f),.018f,fade);
            }
        }

        void UpdateArt(SwordArtVisual art)
        {
            if(art.cue.cue==TrialCombatCue.MartialStroke){UpdateMartialStroke(art);return;}
            if(art.cue.cue==TrialCombatCue.WeaveSigil){UpdateWeaveSigil(art);return;}
            if(IsSwordMotionCue(art.cue.cue)){UpdateSwordMotionArt(art);return;}
            if(art.cue.cue==TrialCombatCue.CrimsonBlade || art.cue.cue==TrialCombatCue.MoonBlade){UpdateCrescentArt(art);return;}
            if(art.cue.cue==TrialCombatCue.RisingCut || art.cue.cue==TrialCombatCue.CrescentCut || art.cue.cue==TrialCombatCue.RollFlash){UpdateInkCut(art);return;}
            var cue=art.cue;float t=Mathf.Clamp01(art.age/art.duration),fade=1-Mathf.SmoothStep(0,1,t);
            Vector3 forward=cue.forward.sqrMagnitude>.01f?cue.forward.normalized:Vector3.forward,right=Vector3.Cross(Vector3.up,forward).normalized;
            Color tint=cue.style>0?StyleColor(cue.style):SakuraPink;float width=.045f;
            bool dragon=cue.cue==TrialCombatCue.ThunderDragon,spiral=cue.cue==TrialCombatCue.SpiralSword;
            Vector3 hand=art.owner?art.owner.transform.position+Vector3.up:cue.position;
            if(art.flight && art.age>0)
            {
                Vector3 from=TrialParryFlight.Point(cue.cue,cue.position,cue.end,hand,Mathf.Max(0,t-Time.deltaTime/art.duration),cue.amount);
                Vector3 to=TrialParryFlight.Point(cue.cue,cue.position,cue.end,hand,t,cue.amount),step=to-from;
                float radius=dragon?Mathf.Lerp(.8f,1.3f,cue.amount):.52f;
                if(step.sqrMagnitude>.00001f && Physics.SphereCast(from,radius,step.normalized,out _,step.magnitude,1,QueryTriggerInteraction.Ignore))
                {art.root.gameObject.SetActive(false);if(art.owner && art.owner.Weapon)art.owner.Weapon.gameObject.SetActive(true);return;}
            }
            if(dragon)tint=ThunderGold;
            else if(spiral)tint=new Color(.42f,.78f,1);
            else if(cue.style==0 && (cue.cue==TrialCombatCue.RisingCut || cue.cue==TrialCombatCue.CrescentCut))tint=new Color(1,.83f,.60f);
            float scale=dragon?Mathf.Lerp(.75f,1.35f,cue.amount):1;
            for(int i=0;i<65;i++)
            {
                float u=i/64f;Vector3 point,across;
                if(art.flight)
                {
                    float trail=dragon?.40f:.23f,age=Mathf.Max(0,t-(1-u)*trail);
                    point=TrialParryFlight.Point(cue.cue,cue.position,cue.end,hand,age,cue.amount);
                    across=(Vector3.up*Mathf.Cos(u*9+art.age*16)+right*Mathf.Sin(u*9+art.age*16))*(dragon?.42f:.10f)*scale*Mathf.Sin(u*Mathf.PI*.82f);
                    if(dragon)point+=Vector3.up*(Mathf.Sin(u*12-art.age*22)*.11f*scale);
                }
                else if(cue.cue==TrialCombatCue.SakuraScatter)
                {
                    float angle=u*Mathf.PI*6+art.age*2.2f;
                    point=Vector3.Lerp(cue.position,cue.end,u)+(right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*(.22f+Mathf.Sin(u*Mathf.PI)*.52f);
                    across=(right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*.18f;
                }
                else if(cue.cue==TrialCombatCue.RisingCut || cue.cue==TrialCombatCue.CrescentCut)
                {
                    float angle=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI*.5f,u);
                    Vector3 axis=cue.cue==TrialCombatCue.RisingCut?(cue.amount<0?Vector3.down:(right+Vector3.up*.65f).normalized):right;
                    Vector3 radial=axis*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                    point=cue.position+radial*(2.5f+t*.22f);across=radial*(.30f*Mathf.Sin(u*Mathf.PI));
                }
                else
                {
                    point=Vector3.Lerp(cue.position,cue.end,u);
                    across=right*Mathf.Sin(u*Mathf.PI)*.08f;
                }
                art.points[i]=point;art.vertices[i*2]=point-across;art.vertices[i*2+1]=point+across;
                Color inner=tint;inner.a=fade*(dragon?.65f:.25f)*Mathf.Sin(u*Mathf.PI);
                art.colors[i*2]=inner;inner.a=0;art.colors[i*2+1]=inner;

            }
            if(!UploadSwordMesh(art))return;
            if(dragon)width=.17f*scale;
            if(cue.cue==TrialCombatCue.EmpoweredStep){width=.025f;tint=new Color(.5f,.85f,1);}
            PaintLine(art.core,art.points,Color.Lerp(tint,Color.white,.75f),width,art.flight?1:fade);
            PaintLine(art.glow,art.points,new Color(tint.r,tint.g,tint.b,.22f),width*4,fade);
            if(dragon)
            {
                float frame=InkMainMenu.ReducedMotion?0:Mathf.Floor(art.age*42);
                for(int i=0;i<65;i++)
                {
                    float spread=Mathf.Sin(i/64f*Mathf.PI)*scale;
                    float seed=i*2.79f;
                    Vector3 jitter=right*(Mathf.PerlinNoise(seed,frame*.52f)-.5f)+
                        Vector3.up*(Mathf.PerlinNoise(seed+17,frame*.65f)-.5f);
                    art.points[i]+=jitter*(1.25f*spread)+right*.07f;
                }
            }
            else for(int i=0;i<65;i++)art.points[i]+=Vector3.up*(Mathf.Sin(i*.42f-art.age*18)*.05f)+right*.07f;
            PaintLine(art.filament,art.points,tint,width*.35f,fade);
            if(dragon)DragonHead(art,t,hand,scale,fade);
            if(spiral)FlyingSword(art,t,hand,fade);
        }
        void UpdateInkCut(SwordArtVisual art)
        {
            var cue=art.cue;float t=Mathf.Clamp01(art.age/art.duration),fade=1-Mathf.SmoothStep(0,1,t);
            Vector3 forward=cue.forward.sqrMagnitude>.01f?cue.forward.normalized:Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            bool flash=cue.cue==TrialCombatCue.RollFlash || cue.cue==TrialCombatCue.WeaveSigil || cue.cue==TrialCombatCue.MartialStroke;
            Vector3 axis=cue.cue==TrialCombatCue.RisingCut?(right+Vector3.up*.65f).normalized:right;
            float radius=flash?2.8f:2.5f;
            for(int i=0;i<65;i++)
            {
                float u=i/64f,envelope=CrescentEnvelope(u);
                // Flash starts low behind the right hip and sweeps into the front.
                float angle=flash?Mathf.Lerp(2.4f,-.22f,u):Mathf.Lerp(-1.65f,1.65f,u);
                Vector3 radial=axis*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                Vector3 point=cue.position+radial*radius;
                if(flash)point+=Vector3.up*Mathf.Lerp(-.65f,.12f,u);
                art.points[i]=point;art.vertices[i*2+1]=point;
                float feather=.75f+Mathf.PerlinNoise(u*16,1.3f)*.4f;
                art.vertices[i*2]=point-radial*(flash?.62f:.42f)*envelope*feather;
                art.colors[i*2]=new Color(.003f,.001f,.006f,fade*.95f*envelope);
                art.colors[i*2+1]=new Color(.012f,.001f,.004f,fade*.98f*envelope);
            }
            if(!UploadSwordMesh(art))return;
            PaintLine(art.core,art.points,Color.white,flash?.052f:.032f,fade);
            PaintLine(art.glow,art.points,new Color(1,1,1,.30f),flash?.22f:.15f,fade);
            art.filament.enabled=art.cue.style>0;art.detail.enabled=false;
            if(art.cue.style>0)
            {
                for(int i=0;i<65;i++)
                {
                    float u=i/64f,wave=Mathf.Sin(u*Mathf.PI*(art.cue.style==4?6:art.cue.style==2?10:3)-t*5);
                    art.points[i]+=-forward*.12f+Vector3.up*wave*(art.cue.style==4?.26f:.10f);
                }
                PaintLine(art.filament,art.points,Color.white,.012f,fade*.7f);
            }
            if(art.age>=art.nextEmber)
            {art.nextEmber=art.age+.08f;InkBloom(cue.position+forward*1.8f,-forward*.4f,flash?1:.55f);}
        }
        void DragonHead(SwordArtVisual art,float t,Vector3 hand,float scale,float fade)
        {
            Vector3 head=TrialParryFlight.Point(art.cue.cue,art.cue.position,art.cue.end,hand,t,art.cue.amount);
            Vector3 forward=(art.cue.end-art.cue.position).normalized,right=Vector3.Cross(Vector3.up,forward).normalized;
            art.head.SetPositionAndRotation(head,Quaternion.LookRotation(forward));art.head.localScale=Vector3.one*scale;
            // Angular snout, open jaw, swept horns and curling whiskers form one dragon silhouette.
            var shape=DragonOutline;
            art.detail.enabled=true;art.detail.positionCount=shape.Length;
            for(int i=0;i<shape.Length;i++)art.detail.SetPosition(i,head+(right*shape[i].x+Vector3.up*shape[i].y+forward*shape[i].z)*scale);
            art.detail.startColor=art.detail.endColor=new Color(1,.79f,.24f,Mathf.Max(.2f,fade));art.detail.widthMultiplier=.075f*scale;
            if(art.age>=art.nextEmber)
            {
                art.nextEmber=art.age+.085f;
                Burst(head,ThunderGold,6);
            }
        }
        void BuildDragonHead()
        {
            var vertices=new Vector3[32];var colors=new Color[32];var triangles=new int[144];
            for(int ring=0;ring<4;ring++)for(int j=0;j<8;j++)
            {
                float angle=j*Mathf.PI*.25f,radius=ring==0?.12f:ring==1?.34f:ring==2?.27f:.11f;
                int v=ring*8+j;vertices[v]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.72f+.025f,Mathf.Lerp(-.58f,.72f,ring/3f));
                colors[v]=j<4?new Color(1,.94f,.52f,.86f):new Color(1,.69f,.12f,.8f);
                if(ring<3){int k=(ring*8+j)*6,a=v,b=ring*8+(j+1)%8;triangles[k]=a;triangles[k+1]=b;triangles[k+2]=a+8;triangles[k+3]=b;triangles[k+4]=b+8;triangles[k+5]=a+8;}
            }
            dragonHeadMesh=new Mesh{name="Thunder dragon sculpted muzzle"};dragonHeadMesh.vertices=vertices;dragonHeadMesh.colors=colors;dragonHeadMesh.triangles=triangles;dragonHeadMesh.RecalculateNormals();
        }
        void FlyingSword(SwordArtVisual art,float t,Vector3 hand,float fade)
        {
            var cue=art.cue;Vector3 centre=TrialParryFlight.Point(cue.cue,cue.position,cue.end,hand,t,cue.amount);
            Vector3 axis=Quaternion.AngleAxis(t*1800,Vector3.up)*(cue.end-cue.position).normalized;
            art.detail.enabled=true;art.detail.positionCount=7;
            Vector3 cross=Vector3.Cross(Vector3.up,axis).normalized;
            art.detail.SetPosition(0,centre-axis*.45f);art.detail.SetPosition(1,centre+axis*.95f);art.detail.SetPosition(2,centre+axis*.55f+cross*.035f);
            art.detail.SetPosition(3,centre-axis*.12f);art.detail.SetPosition(4,centre-axis*.12f+cross*.16f);art.detail.SetPosition(5,centre-axis*.12f-cross*.16f);art.detail.SetPosition(6,centre-axis*.45f);
            art.detail.widthMultiplier=.065f;art.detail.startColor=art.detail.endColor=new Color(.88f,.96f,1);
            if(art.owner && art.owner.Weapon)
            {
                bool reclaim=art.owner.Guarding && art.owner.Combo!=TrialPlayer.SpiralSword || art.owner.Action==FighterAction.Dodge || (art.owner.Action==FighterAction.Light || art.owner.Action==FighterAction.Heavy) && art.owner.Combo!=TrialPlayer.SpiralSword;
                art.owner.Weapon.gameObject.SetActive(reclaim || t>.97f);
            }
        }
        void TickSwordArtEffects()
        {
            foreach(var art in swordArts)
            {
                if(art==null || !art.root.gameObject.activeSelf)continue;
                art.age+=Time.deltaTime;
                if(art.age>=art.duration || art.owner && !art.owner.Alive)
                {art.root.gameObject.SetActive(false);if(art.owner && art.owner.Weapon)art.owner.Weapon.gameObject.SetActive(true);continue;}
                UpdateArt(art);
                if(TrialGraphicsBudget.Performance)art.glow.enabled=false;
            }
        }
        public void ClearSwordArts(TrialPlayer owner)
        {
            foreach(var art in swordArts)if(art!=null && art.owner==owner)art.root.gameObject.SetActive(false);
            if(owner.Weapon)owner.Weapon.gameObject.SetActive(true);
        }
        void DisposeSwordArtEffects()
        {DisposeSwordPalette();foreach(var art in swordArts)if(art!=null){if(art.mesh)Destroy(art.mesh);if(art.wakeMesh)Destroy(art.wakeMesh);}if(dragonHeadMesh)Destroy(dragonHeadMesh);if(swordGlowMaterial)Destroy(swordGlowMaterial);if(swordGlowTexture)Destroy(swordGlowTexture);if(crimsonEdgeMaterial)Destroy(crimsonEdgeMaterial);if(crimsonGlowMaterial)Destroy(crimsonGlowMaterial);}
    }
}
