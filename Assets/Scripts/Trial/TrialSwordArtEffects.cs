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
        }
        readonly SwordArtVisual[] swordArts=new SwordArtVisual[40];
        int nextSwordArt;
        Mesh dragonHeadMesh;
        ParticleSystem dragonFlames;
        Material flameMaterial;
        Texture2D flameTexture;
        Texture2D swordGlowTexture;
        Material swordGlowMaterial;
        static readonly Color SakuraPink=new Color(1,.44f,.68f);
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
            art.nextEmber=0;art.head.gameObject.SetActive(cue.cue==TrialCombatCue.FireDragon);
            art.owner=TrialNetworkManager.SessionActive?TrialNetworkManager.Current.PlayerById(cue.player):game.Player;
            art.surface.enabled=true;art.detail.enabled=false;
            art.core.enabled=art.glow.enabled=art.filament.enabled=true;
            UpdateArt(art);return art;
        }
        bool PlaySwordArtCue(TrialCombatEvent cue,bool local)
        {
            if(cue.cue==TrialCombatCue.CrimsonBlade || cue.cue==TrialCombatCue.MoonBlade)
            {Art(cue,TrialSwordFlight.Lifetime(cue.cue));Sound("swing",cue.position,.95f);return true;}
            if(cue.cue==TrialCombatCue.FireDragon || cue.cue==TrialCombatCue.SpiralSword)
            {
                Art(cue,TrialParryFlight.Duration(cue.cue,cue.amount),true);
                Sound(cue.cue==TrialCombatCue.FireDragon?"finish":"swing",cue.position,cue.cue==TrialCombatCue.FireDragon?.72f:.85f);
                if(local){game.CameraRig.Kick(cue.cue==TrialCombatCue.FireDragon?.28f:.10f);Rumble(.25f,.45f,.1f);}return true;
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

        void UpdateArt(SwordArtVisual art)
        {
            var cue=art.cue;float t=Mathf.Clamp01(art.age/art.duration),fade=1-Mathf.SmoothStep(0,1,t);
            Vector3 forward=cue.forward.sqrMagnitude>.01f?cue.forward.normalized:Vector3.forward,right=Vector3.Cross(Vector3.up,forward).normalized;
            Color tint=SakuraPink;float width=.045f;
            bool dragon=cue.cue==TrialCombatCue.FireDragon,spiral=cue.cue==TrialCombatCue.SpiralSword;
            bool wave=cue.cue==TrialCombatCue.CrimsonBlade || cue.cue==TrialCombatCue.MoonBlade;
            Vector3 hand=art.owner?art.owner.transform.position+Vector3.up:cue.position;
            if(art.flight && art.age>0)
            {
                Vector3 from=TrialParryFlight.Point(cue.cue,cue.position,cue.end,hand,Mathf.Max(0,t-Time.deltaTime/art.duration),cue.amount);
                Vector3 to=TrialParryFlight.Point(cue.cue,cue.position,cue.end,hand,t,cue.amount),step=to-from;
                float radius=dragon?Mathf.Lerp(.8f,1.3f,cue.amount):.52f;
                if(step.sqrMagnitude>.00001f && Physics.SphereCast(from,radius,step.normalized,out _,step.magnitude,1,QueryTriggerInteraction.Ignore))
                {art.root.gameObject.SetActive(false);if(art.owner && art.owner.Weapon)art.owner.Weapon.gameObject.SetActive(true);return;}
            }
            if(dragon)tint=new Color(1,.32f,.055f);
            else if(spiral)tint=new Color(.42f,.78f,1);
            else if(cue.cue==TrialCombatCue.CrimsonBlade)tint=new Color(.82f,.06f,.19f);
            else if(cue.cue==TrialCombatCue.RisingCut || cue.cue==TrialCombatCue.CrescentCut)tint=new Color(1,.83f,.60f);
            float scale=dragon?Mathf.Lerp(.75f,1.35f,cue.amount):1;
            for(int i=0;i<65;i++)
            {
                float u=i/64f;Vector3 point,across;
                if(wave)
                {
                    float angle=Mathf.Lerp(-1.48f,1.48f,u),radius=cue.cue==TrialCombatCue.MoonBlade?1.3f:1.1f;
                    Vector3 radial=right*Mathf.Sin(angle)*radius+forward*Mathf.Cos(angle)*.55f;
                    point=cue.position+forward*(TrialSwordFlight.Speed(cue.cue)*art.age)+radial;
                    if(cue.cue==TrialCombatCue.CrimsonBlade)point+=Vector3.up*(Mathf.Sin(angle)*.36f);
                    across=forward*(.25f*Mathf.Sin(u*Mathf.PI));
                }
                else if(art.flight)
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
                    Vector3 axis=cue.cue==TrialCombatCue.RisingCut?(right+Vector3.up*.65f).normalized:right;
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
            art.mesh.vertices=art.vertices;art.mesh.colors=art.colors;art.mesh.triangles=art.triangles;art.mesh.RecalculateBounds();
            if(wave)
            {
                Vector3 step=forward*TrialSwordFlight.Speed(cue.cue)*Time.deltaTime;
                Vector3 centre=cue.position+forward*(TrialSwordFlight.Speed(cue.cue)*art.age);
                if(step.sqrMagnitude>0 && Physics.SphereCast(centre-step,TrialSwordFlight.Radius(cue.cue),forward,out _,step.magnitude,1,QueryTriggerInteraction.Ignore)){art.root.gameObject.SetActive(false);return;}
            }
            if(dragon)width=.17f*scale;
            if(cue.cue==TrialCombatCue.EmpoweredStep){width=.025f;tint=new Color(.5f,.85f,1);}
            PaintLine(art.core,art.points,Color.Lerp(tint,Color.white,.75f),width,art.flight?1:fade);
            PaintLine(art.glow,art.points,new Color(tint.r,tint.g,tint.b,.22f),width*4,fade);
            for(int i=0;i<65;i++)art.points[i]+=Vector3.up*(Mathf.Sin(i*.42f-art.age*18)*.05f)+right*.07f;
            PaintLine(art.filament,art.points,tint,width*.35f,fade);
            if(dragon)DragonHead(art,t,hand,scale,fade);
            if(spiral)FlyingSword(art,t,hand,fade);
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
            if(art.age>=art.nextEmber){art.nextEmber=art.age+.025f;EmitDragonFlames(head,forward,scale);}
        }
        void BuildDragonHead()
        {
            var vertices=new Vector3[32];var colors=new Color[32];var triangles=new int[144];
            for(int ring=0;ring<4;ring++)for(int j=0;j<8;j++)
            {
                float angle=j*Mathf.PI*.25f,radius=ring==0?.12f:ring==1?.34f:ring==2?.27f:.11f;
                int v=ring*8+j;vertices[v]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.72f+.025f,Mathf.Lerp(-.58f,.72f,ring/3f));
                colors[v]=j<4?new Color(1,.56f,.08f,.86f):new Color(.96f,.15f,.02f,.8f);
                if(ring<3){int k=(ring*8+j)*6,a=v,b=ring*8+(j+1)%8;triangles[k]=a;triangles[k+1]=b;triangles[k+2]=a+8;triangles[k+3]=b;triangles[k+4]=b+8;triangles[k+5]=a+8;}
            }
            dragonHeadMesh=new Mesh{name="Flame dragon sculpted muzzle"};dragonHeadMesh.vertices=vertices;dragonHeadMesh.colors=colors;dragonHeadMesh.triangles=triangles;dragonHeadMesh.RecalculateNormals();
        }
        void EmitDragonFlames(Vector3 head,Vector3 forward,float scale)
        {
            if(!dragonFlames)
            {
                flameTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Soft flame falloff",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}flameTexture.SetPixels(pixels);flameTexture.Apply();
                flameMaterial=new Material(particles);flameMaterial.SetTexture("_BaseMap",flameTexture);
                var obj=new GameObject("Dragon flame plumes and embers");obj.transform.SetParent(transform,false);dragonFlames=obj.AddComponent<ParticleSystem>();dragonFlames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=dragonFlames.main;main.playOnAwake=false;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.7f);main.startSize=new ParticleSystem.MinMaxCurve(.14f,.46f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=500;
                var emission=dragonFlames.emission;emission.enabled=false;var shape=dragonFlames.shape;shape.enabled=false;
                var color=dragonFlames.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.9f,.35f),0),new GradientColorKey(new Color(1,.28f,.025f),.35f),new GradientColorKey(new Color(.5f,.04f,.01f),1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.65f,.4f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=dragonFlames.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.25f,1),new Keyframe(1,0)));
                var renderer=dragonFlames.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=flameMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            }
            dragonFlames.Play();int count=Application.isMobilePlatform?4:9;
            for(int j=0;j<count;j++)dragonFlames.Emit(new ParticleSystem.EmitParams{position=head+Random.insideUnitSphere*.35f*scale-forward*Random.Range(0,.9f),velocity=-forward*Random.Range(1,3)+Vector3.up*Random.Range(.3f,1.5f)+Random.insideUnitSphere*.8f,startColor=Color.white,startSize=Random.Range(.16f,.5f)*scale},1);
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
                bool reclaim=art.owner.Guarding || art.owner.Action==FighterAction.Dodge || (art.owner.Action==FighterAction.Light || art.owner.Action==FighterAction.Heavy) && art.owner.Combo!=TrialPlayer.SpiralSword;
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
            }
        }
        public void ClearSwordArts(TrialPlayer owner)
        {
            foreach(var art in swordArts)if(art!=null && art.owner==owner)art.root.gameObject.SetActive(false);
            if(owner.Weapon)owner.Weapon.gameObject.SetActive(true);
        }
        void DisposeSwordArtEffects()
        {foreach(var art in swordArts)if(art!=null && art.mesh)Destroy(art.mesh);if(dragonHeadMesh)Destroy(dragonHeadMesh);if(flameMaterial)Destroy(flameMaterial);if(flameTexture)Destroy(flameTexture);if(swordGlowMaterial)Destroy(swordGlowMaterial);if(swordGlowTexture)Destroy(swordGlowTexture);}
    }
}
