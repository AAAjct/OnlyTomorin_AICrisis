using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TomorinTrial
{
    // Original temple kit, terrain, textures and weapons; no downloaded asset dependencies.
    public sealed partial class TrialWorld
    {
        public Material Stone,DarkStone,DarkWood,Crimson,Gold,Steel,Armor,Glow,RedGlow,Leaf,Water;
        public Transform Root { get; private set; }
        public GameObject Gate { get; private set; }
        public GameObject ArenaEntrance { get; private set; }
        public Vector3 ShrinePosition => swordTerrace?new Vector3(-3.2f,0,15.1f):championArena?new Vector3(-19,0,5):new Vector3(-5.7f,0,-18);
        bool championArena;
        readonly List<Object> owned=new List<Object>();
        readonly System.Random random=new System.Random(316);
        Mesh cragMesh,foliageMesh;
        Shader surfaceShader,skyShader;

        public void Build(Transform parent,Shader surface,Shader skySource,TrialLevel level=TrialLevel.BigFish)
        {
            surfaceShader=surface;skyShader=skySource;
            Root=new GameObject("雾庭 · Temple of the last ember").transform;Root.SetParent(parent,false);
            Stone=Material("Rainwashed basalt",new Color(.24f,.29f,.30f),.05f,.35f,true);
            DarkStone=Material("Black slate",new Color(.075f,.11f,.13f),.12f,.4f,true);
            DarkWood=Material("Charred cedar",new Color(.065f,.040f,.036f),.05f,.25f,true);
            Crimson=Material("Vermilion lacquer",new Color(.28f,.035f,.025f),.18f,.5f,true);
            Gold=Material("Antique brass",new Color(.52f,.32f,.12f),.7f,.65f);
            Steel=Material("Folded steel",new Color(.56f,.66f,.73f),.85f,.82f);
            Armor=Material("Obsidian lamellar",new Color(.075f,.09f,.11f),.65f,.6f,true);
            Glow=Material("Incense gold",new Color(1,.67f,.20f),0,.2f,false,new Color(3.3f,1.5f,.32f));
            RedGlow=Material("Blood ember",new Color(.9f,.055f,.025f),0,.2f,false,new Color(3.8f,.12f,.025f));
            Leaf=Material("Autumn maple",new Color(.52f,.065f,.025f),.02f,.25f,true);
            Water=Material("Still jade",new Color(.035f,.13f,.14f),.55f,.92f);
            cragMesh=CragMesh();foliageMesh=FoliageMesh();owned.Add(cragMesh);owned.Add(foliageMesh);
            Lighting();
            if(TrialChampionCatalog.IsChampion(level) || level==TrialLevel.Endless || level==TrialLevel.BossRush){championArena=true;BuildChampionArena(level);return;}
            Box("Foundation",new Vector3(0,-.50f,6),new Vector3(29,.8f,66),DarkStone);
            for(int x=-6;x<=6;x++) for(int z=-12;z<=18;z++)
            {
                float shade=(float)random.NextDouble();
                var tile=Box("Stone paving",new Vector3(x*2,-.075f,z*2),new Vector3(1.96f,.15f,1.96f),shade>.22f?Stone:DarkStone,false);
                tile.localEulerAngles=new Vector3(0,shade>.7f?90:0,0);
            }
            Box("Walkable court",new Vector3(0,-.14f,6),new Vector3(26,.2f,64),Stone);
            for(int side=-1;side<=1;side+=2)
            {
                PerimeterWall("Perimeter wall",new Vector3(side*13.4f,1.1f,6),new Vector3(.65f,2.5f,65));
                Box("Wall cap",new Vector3(side*13.4f,2.4f,6),new Vector3(.95f,.2f,65),Gold,false);
                for(int z=-24;z<=36;z+=5)
                {
                    Box("Wall buttress",new Vector3(side*13.1f,1.4f,z),new Vector3(.9f,3,.9f),Stone);
                    Box("Jade reflecting pool",new Vector3(side*10.5f,.015f,z+1),new Vector3(3.3f,.06f,3.5f),Water,false);
                    if(z%10== -4 || z==6 || z==16 || z==26) Tree(new Vector3(side*10.3f,0,z),.8f+(float)random.NextDouble()*.4f);
                    Lantern(new Vector3(side*8.5f,0,z+2),z%10==0);
                }
            }
            PerimeterWall("Rear boundary",new Vector3(0,1.2f,38.5f),new Vector3(28,3,.8f));
            PerimeterWall("Entry boundary",new Vector3(0,1,-26),new Vector3(28,2,.7f));
            // Silver inlay leads the eye from the checkpoint to the duelling circle.
            for(int z=-24;z<18;z+=2)
            {
                Box("Processional inlay",new Vector3(-2.15f,.011f,z),new Vector3(.035f,.024f,1.9f),Gold,false);
                Box("Processional inlay",new Vector3(2.15f,.011f,z),new Vector3(.035f,.024f,1.9f),Gold,false);
            }
            Ring(new Vector3(0,.025f,25),9.4f,.07f,Gold);
            Ring(new Vector3(0,.029f,25),8.9f,.035f,Gold);
            Ring(new Vector3(0,.03f,25),3.1f,.055f,Gold);
            Ring(new Vector3(0,.031f,25),1.6f,.035f,Gold);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var marker=Box("Twelve seals",new Vector3(Mathf.Sin(a)*8.3f,.035f,25+Mathf.Cos(a)*8.3f),new Vector3(.12f,.04f,.6f),Gold,false);
                marker.localEulerAngles=new Vector3(0,i*30,0);
            }
            Temple(new Vector3(0,0,35),9.5f,5.8f);
            Temple(new Vector3(-10,0,10),3.7f,4.3f);
            Temple(new Vector3(10,0,-10),3.7f,4.3f);
            Torii(new Vector3(0,0,-12),6.3f);
            Torii(new Vector3(0,0,16),8.6f);
            Gate=Box("封印 · 击败三名剑卫解除",new Vector3(0,1.5f,16),new Vector3(7.5f,3,.14f),RedGlow).gameObject;
            // Narrow bars are translucent-looking through their spacing, without a screen-filling solid wall.
            Gate.GetComponent<Renderer>().enabled=false;
            for(int i=-7;i<=7;i++)
            {var bar=Part("Seal threads",PrimitiveType.Cube,Gate.transform,new Vector3(i*.065f,0,0),new Vector3(.006f,1,1),i%2==0?RedGlow:Gold);}
            for(int side=-1;side<=1;side+=2)Fence("庭门侧栏",new Vector3(side*8.7f,1.45f,16),9.9f,0);
            for(int i=0;i<64;i++)
            {
                float angle=i*360f/64;if(angle>160 && angle<200)continue;
                float radians=angle*Mathf.Deg2Rad;
                Fence("斗场环栏",new Vector3(Mathf.Sin(radians)*9.7f,1.45f,25+Mathf.Cos(radians)*9.7f),1.02f,angle);
            }
            ArenaEntrance=new GameObject("斗场封门");ArenaEntrance.transform.SetParent(Root,false);
            for(int i=29;i<=35;i++)
            {
                float angle=i*360f/64,radians=angle*Mathf.Deg2Rad;
                var section=Fence("封门环栏",new Vector3(Mathf.Sin(radians)*9.7f,1.45f,25+Mathf.Cos(radians)*9.7f),1.02f,angle);
                section.SetParent(ArenaEntrance.transform,true);
            }
            ArenaEntrance.SetActive(false);
            Shrine();
            for(int i=0;i<26;i++)
            {
                float x=(i%2==0?-1:1)*(16+random.Next(8));float z=-20+random.Next(70);
                var mountain=MeshPart("Distant ink peaks",cragMesh,new Vector3(x,-1,z),new Vector3(random.Next(8,14),random.Next(12,23),random.Next(8,14)),DarkStone);
                mountain.localEulerAngles=new Vector3(0,random.Next(180),0);
            }
            Part("Moon",PrimitiveType.Sphere,Root,new Vector3(-35,42,100),Vector3.one*11,Material("Cold moon",new Color(.38f,.62f,.68f),0,0,false,new Color(.55f,.85f,.92f)));
            // Lantern light and scene fog provide distance without an expensive volumetric renderer.
            BatchStaticGeometry();
        }

        Transform Fence(string label,Vector3 point,float length,float yaw)
        {
            var fence=Box(label,point,new Vector3(length,2.9f,.28f),DarkWood);
            fence.GetComponent<Renderer>().enabled=false;fence.localRotation=Quaternion.Euler(0,yaw,0);
            Part("下横梁",PrimitiveType.Cube,fence,new Vector3(0,-.34f,0),new Vector3(1,.055f,1.35f),Gold);
            Part("上横梁",PrimitiveType.Cube,fence,new Vector3(0,.37f,0),new Vector3(1,.06f,1.35f),Gold);
            int count=Mathf.Max(2,Mathf.CeilToInt(length/.42f));
            for(int j=0;j<=count;j++)Part("栏杆",PrimitiveType.Cube,fence,new Vector3((float)j/count-.5f,0,0),new Vector3(.09f/length,1,1),Crimson);
            return fence;
        }

        Material Material(string label,Color color,float metallic,float smooth,bool texture=false,Color? emission=null)
        {
            var m=new Material(surfaceShader){name=label};
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            if(texture)
            {
                var t=new Texture2D(64,64,TextureFormat.RGBA32,true){name=label+" original noise",wrapMode=TextureWrapMode.Repeat};
                var pixels=new Color[4096];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float n=.7f+Mathf.PerlinNoise(x*.19f,y*.19f)*.4f+(float)random.NextDouble()*.12f;
                    if((x+y*3)%31==0)n*=.82f;pixels[y*64+x]=new Color(n,n,n,1);
                }
                t.SetPixels(pixels);t.Apply(true,true);m.SetTexture("_BaseMap",t);m.SetTextureScale("_BaseMap",Vector2.one*2);owned.Add(t);
            }
            if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
            owned.Add(m);return m;
        }

        void Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.24f,.33f,.39f);
            RenderSettings.ambientEquatorColor=new Color(.13f,.19f,.22f);
            RenderSettings.ambientGroundColor=new Color(.075f,.095f,.11f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.10f,.18f,.22f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.018f;
            var sky=new Material(skyShader);sky.name="Ink and mist sky";owned.Add(sky);RenderSettings.skybox=sky;
            var sun=new GameObject("Moonlight").AddComponent<Light>();sun.transform.SetParent(Root);sun.type=LightType.Directional;sun.color=new Color(.60f,.78f,1);sun.intensity=1.8f;sun.transform.rotation=Quaternion.Euler(42,-32,0);sun.shadows=LightShadows.Soft;
            var fill=new GameObject("Amber rim").AddComponent<Light>();fill.transform.SetParent(Root);fill.type=LightType.Directional;fill.color=new Color(1,.58f,.32f);fill.intensity=.65f;fill.transform.rotation=Quaternion.Euler(18,155,0);
            var volume=new GameObject("Night atmosphere").AddComponent<Volume>();volume.transform.SetParent(Root);volume.isGlobal=true;volume.priority=1;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;owned.Add(profile);
            var bloom=profile.Add<Bloom>();bloom.active=!Application.isMobilePlatform && !TrialGraphicsBudget.Performance;bloom.intensity.Override(.45f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.65f);owned.Add(bloom);
            var vignette=profile.Add<Vignette>();vignette.intensity.Override(.28f);vignette.smoothness.Override(.5f);owned.Add(vignette);
            var tonemap=profile.Add<Tonemapping>();tonemap.mode.Override(TonemappingMode.ACES);owned.Add(tonemap);
            var color=profile.Add<ColorAdjustments>();color.postExposure.Override(.2f);color.contrast.Override(12);color.saturation.Override(-8);owned.Add(color);
        }

        Transform Box(string label,Vector3 p,Vector3 size,Material m,bool collide=true)
        {return Part(label,PrimitiveType.Cube,Root,p,size,m,collide);}

        void PerimeterWall(string label,Vector3 position,Vector3 size)
        {
            // Keep the wall's footprint and mesh, but close the space above it.
            const float top=16f,bottom=-1f;
            var collider=Box(label,position,size,DarkStone).GetComponent<BoxCollider>();
            collider.size=new Vector3(1,(top-bottom)/size.y,1);
            collider.center=Vector3.up*((.5f*(top+bottom)-position.y)/size.y);
        }

        public static Transform Part(string label,PrimitiveType primitive,Transform parent,Vector3 p,Vector3 size,Material material,bool collide=false)
        {
            var t=GameObject.CreatePrimitive(primitive).transform;t.name=label;t.SetParent(parent,false);t.localPosition=p;t.localScale=size;
            var renderer=t.GetComponent<Renderer>();renderer.sharedMaterial=material;
            if(!collide) {var c=t.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);}
            return t;
        }

        void Ring(Vector3 center,float radius,float thickness,Material m)
        {
            var go=new GameObject("Circular inlay");go.transform.SetParent(Root,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=m;line.positionCount=129;line.widthMultiplier=thickness;line.useWorldSpace=true;line.shadowCastingMode=ShadowCastingMode.Off;
            for(int i=0;i<=128;i++){float a=i*Mathf.PI/64;line.SetPosition(i,center+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius));}
        }

        void Temple(Vector3 center,float width,float height)
        {
            Box("Temple plinth",center+Vector3.up*.20f,new Vector3(width+1,.4f,5),DarkStone);
            Box("Temple rear wall",center+new Vector3(0,height*.45f,1.7f),new Vector3(width,height*.8f,.25f),DarkWood);
            for(int i=0;i<5;i++)
            {
                float x=(i-2)*width*.235f;
                Box("Vermilion column",center+new Vector3(x,height*.45f,-1.2f),new Vector3(.27f,height*.8f,.27f),Crimson);
                Box("Column foot",center+new Vector3(x,.35f,-1.2f),new Vector3(.48f,.35f,.48f),Stone);
                if(i<4)for(int j=0;j<5;j++)Box("Lattice door",center+new Vector3(x+width*.12f+(j-2)*width*.035f,height*.39f,1.55f),new Vector3(.045f,height*.65f,.08f),Gold,false);
            }
            Box("Lintel",center+new Vector3(0,height*.83f,-1.2f),new Vector3(width+.4f,.3f,.5f),Crimson);
            Roof(center+Vector3.up*(height*.83f),width+2,6,1.6f);
            Roof(center+Vector3.up*(height+1),width*.65f+2,4,1.3f);
            for(int i=-1;i<=1;i++)Box("Stone steps",center+new Vector3(0,.08f*(i+1),-2.75f+i*.3f),new Vector3(width*.55f,.16f,.65f),Stone);
            Lantern(center+new Vector3(-width*.39f,.2f,-1.6f),true);Lantern(center+new Vector3(width*.39f,.2f,-1.6f),true);
        }

        void Roof(Vector3 pos,float width,float depth,float rise)
        {
            var go=new GameObject("Swept tiled roof");go.transform.SetParent(Root,false);go.transform.localPosition=pos;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float[] r={1,.78f,.40f,.07f};float[] y={.22f,0,.65f,1};
            for(int j=0;j<4;j++)for(int i=0;i<4;i++)
            {vertices.Add(new Vector3((i==0||i==3?-1:1)*width*.5f*r[j],y[j]*rise,(i<2?-1:1)*depth*.5f*r[j]));uv.Add(new Vector2(i/3f,j/3f));}
            for(int j=0;j<3;j++)for(int i=0;i<4;i++)
            {int a=j*4+i,b=j*4+(i+1)%4,c=(j+1)*4+i,d=(j+1)*4+(i+1)%4;triangles.AddRange(new[]{a,c,b,b,c,d});}
            var mesh=new Mesh(){name="Original curved roof"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=DarkStone;
            for(int side=-1;side<=1;side+=2)
            {
                Box("Roof brass eave",pos+new Vector3(0,.22f*rise,side*depth*.5f),new Vector3(width,.09f,.09f),Gold,false);
                Box("Ridge ornament",pos+new Vector3(side*width*.09f,rise+.20f,0),new Vector3(.12f,.40f,.15f),Gold,false);
            }
            Box("Roof ridge",pos+Vector3.up*rise,new Vector3(width*.2f,.12f,.12f),Gold,false);
        }

        void Torii(Vector3 pos,float width)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Box("Gate post",pos+new Vector3(side*(width*.5f-.4f),2.1f,0),new Vector3(.46f,4.2f,.46f),Crimson);
                Box("Gate pedestal",pos+new Vector3(side*(width*.5f-.4f),.25f,0),new Vector3(.8f,.5f,.8f),Stone);
            }
            Box("Gate lintel",pos+new Vector3(0,3.7f,0),new Vector3(width+.4f,.25f,.5f),Crimson);
            Roof(pos+new Vector3(0,4.3f,0),width+1.5f,2, .7f);
            Box("Inscribed tablet",pos+new Vector3(0,3.95f,-.3f),new Vector3(1.1f,.65f,.12f),DarkWood,false);
            for(int i=-1;i<=1;i++)Box("Tablet strokes",pos+new Vector3(i*.23f,3.95f,-.38f),new Vector3(.07f,.36f,.035f),Gold,false);
        }

        void Tree(Vector3 pos,float scale)
        {
            var trunk=Part("Ancient maple",PrimitiveType.Cylinder,Root,pos+Vector3.up*1.5f*scale,new Vector3(.35f,1.5f,.35f)*scale,DarkWood);
            trunk.localEulerAngles=new Vector3(7,0,12);
            for(int i=0;i<8;i++)
            {
                float a=i*2.4f;Vector3 offset=new Vector3(Mathf.Sin(a)*1.45f,3.0f+(float)random.NextDouble(),Mathf.Cos(a)*1.45f)*scale;
                var branch=Part("Twisted branch",PrimitiveType.Cylinder,Root,pos+offset*.65f,new Vector3(.12f,1.3f,.12f)*scale,DarkWood);branch.localRotation=Quaternion.FromToRotation(Vector3.up,offset-new Vector3(0,1.5f*scale,0));
                var canopy=MeshPart("Maple leaves",foliageMesh,pos+offset,new Vector3(2.5f,1.2f,2.2f)*scale,Leaf);canopy.localRotation=Quaternion.Euler(random.Next(20),random.Next(180),random.Next(20));
            }
        }

        void Lantern(Vector3 pos,bool lit)
        {
            Box("Lantern base",pos+Vector3.up*.14f,new Vector3(.65f,.28f,.65f),Stone);
            Box("Lantern stem",pos+Vector3.up*.72f,new Vector3(.20f,.95f,.20f),DarkStone);
            Box("Lantern chamber",pos+Vector3.up*1.30f,new Vector3(.47f,.45f,.47f),Glow,false);
            Box("Lantern roof",pos+Vector3.up*1.59f,new Vector3(.78f,.17f,.78f),DarkStone,false);
            for(int i=0;i<4;i++)Box("Lantern strut",pos+new Vector3(i<2?-.25f:.25f,1.30f,i%2==0?-.25f:.25f),new Vector3(.06f,.48f,.06f),DarkWood,false);
            if(lit)
            {var light=new GameObject("Lantern light").AddComponent<Light>();light.transform.SetParent(Root);light.transform.position=pos+Vector3.up*1.5f;light.type=LightType.Point;light.color=new Color(1,.50f,.19f);light.intensity=3;light.range=5;light.shadows=LightShadows.None;}
        }

        void Shrine()
        {
            Vector3 p=ShrinePosition;
            Box("Checkpoint plinth",p+Vector3.up*.15f,new Vector3(2,.3f,2),DarkStone);
            var bowl=Part("Incense brazier",PrimitiveType.Cylinder,Root,p+Vector3.up*.60f,new Vector3(.9f,.18f,.9f),Gold);
            Part("Last ember",PrimitiveType.Sphere,Root,p+Vector3.up*.95f,new Vector3(.25f,.55f,.25f),Glow);
            Ring(p+Vector3.up*.02f,1.6f,.045f,Gold);
            var light=new GameObject("Checkpoint light").AddComponent<Light>();light.transform.SetParent(Root);light.transform.position=p+Vector3.up*1.3f;light.color=new Color(1,.65f,.28f);light.intensity=5;light.range=7;
        }

        void BatchStaticGeometry()
        {
            // Keep mobile batches local so off-camera courtyard sections can be
            // culled instead of submitting the entire level for each material.
            var groups=new Dictionary<(Material material,Vector2Int cell,ShadowCastingMode shadows),List<CombineInstance>>();
            var originals=new List<MeshRenderer>();
            foreach(var renderer in Root.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.transform.IsChildOf(Gate.transform) || renderer.transform.IsChildOf(ArenaEntrance.transform) || !renderer.enabled)continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh || filter.sharedMesh.subMeshCount!=1)continue;
                var center=Root.InverseTransformPoint(renderer.bounds.center);
                var cell=new Vector2Int(Mathf.FloorToInt(center.x/12),Mathf.FloorToInt(center.z/12));
                // Ground slabs receive shadows but cannot cast a useful silhouette;
                // distant mountains also do not need to enter every shadow cascade.
                var shadows=renderer.bounds.max.y<.2f || renderer.name=="Distant ink peaks"?ShadowCastingMode.Off:renderer.shadowCastingMode;
                var key=(renderer.sharedMaterial,cell,shadows);
                if(!groups.TryGetValue(key,out var list)){list=new List<CombineInstance>();groups.Add(key,list);}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=Root.worldToLocalMatrix*renderer.transform.localToWorldMatrix});originals.Add(renderer);
            }
            foreach(var entry in groups)
            {
                var mesh=new Mesh{name="Temple batch · "+entry.Key.material.name+" · "+entry.Key.cell,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray());mesh.UploadMeshData(true);owned.Add(mesh);
                var go=new GameObject(mesh.name);go.transform.SetParent(Root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=entry.Key.material;renderer.shadowCastingMode=entry.Key.shadows;
            }
            foreach(var renderer in originals)
            {renderer.enabled=false;Object.Destroy(renderer.GetComponent<MeshFilter>());Object.Destroy(renderer);}
        }

        Transform MeshPart(string label,Mesh mesh,Vector3 point,Vector3 size,Material material)
        {
            var t=new GameObject(label).transform;t.SetParent(Root,false);t.localPosition=point;t.localScale=size;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;
        }

        Mesh CragMesh()
        {
            const int sides=9;var rings=new Vector3[4,sides];
            for(int j=0;j<4;j++)for(int i=0;i<sides;i++)
            {float angle=i*Mathf.PI*2/sides;float r=(j==3?.09f:j==0?.65f:.48f)*( .8f+(float)random.NextDouble()*.4f);rings[j,i]=new Vector3(Mathf.Cos(angle)*r+(j==3?.18f:0),j*.3f+(j==0?0:(float)random.NextDouble()*.12f),Mathf.Sin(angle)*r);}
            var vertices=new List<Vector3>();
            for(int j=0;j<3;j++)for(int i=0;i<sides;i++)
            {int n=(i+1)%sides;vertices.AddRange(new[]{rings[j,i],rings[j+1,i],rings[j,n],rings[j,n],rings[j+1,i],rings[j+1,n]});}
            for(int i=0;i<sides;i++)vertices.AddRange(new[]{rings[3,i],new Vector3(.18f,1.08f,0),rings[3,(i+1)%sides]});
            return FlatMesh("Original jagged mountain",vertices);
        }

        Mesh FoliageMesh()
        {
            var vertices=new List<Vector3>();
            for(int i=0;i<95;i++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2,rad=Mathf.Sqrt((float)random.NextDouble())*.52f;
                var p=new Vector3(Mathf.Cos(angle)*rad,((float)random.NextDouble()-.5f)*.35f,Mathf.Sin(angle)*rad);
                float size=.055f+(float)random.NextDouble()*.11f;
                var a=p+new Vector3(-size,0,-size*.6f);var b=p+new Vector3(size,0,-size*.6f);var c=p+new Vector3(0,0,size);var d=p+new Vector3(0,size*.6f,0);
                vertices.AddRange(new[]{a,d,b,b,d,c,c,d,a,a,b,c});
            }
            return FlatMesh("Original angular maple foliage",vertices);
        }

        static Mesh FlatMesh(string label,List<Vector3> vertices)
        {
            var mesh=new Mesh{name=label};var triangles=new int[vertices.Count];var uv=new Vector2[vertices.Count];
            for(int i=0;i<vertices.Count;i++){triangles[i]=i;uv[i]=new Vector2(vertices[i].x,vertices[i].z);}
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        public static Transform Sword(Transform parent,Material steel,Material gold,Material light,bool enemy,TrialOutfit sword=TrialOutfit.MingShuang)
        {
            string name=enemy?"Ashforged longsword":sword==TrialOutfit.MingShuang?"鸣霜 · Ming Shuang":"初始剑";
            var root=new GameObject(name).transform;root.SetParent(parent,false);
            root.localScale=Vector3.one/Mathf.Max(.001f,parent.lossyScale.x);
            var model=!enemy && sword==TrialOutfit.MingShuang?Resources.Load<GameObject>("Trial/Models/MingShuang/MingShuang"):null;
            if(model)
            {
                var visual=Object.Instantiate(model,root,false);
                visual.name="MingShuang";
            }
            else
            {
                Part("Wrapped hilt",PrimitiveType.Cylinder,root,new Vector3(0,-.06f,0),new Vector3(.044f,.13f,.044f),gold);
                Part("Tsuba",PrimitiveType.Cube,root,new Vector3(0,.10f,0),new Vector3(.22f,.04f,.09f),gold);
                Part("Tempered blade",PrimitiveType.Cube,root,new Vector3(0,.61f,0),new Vector3(.058f,1.0f,.025f),steel);
                var tip=Part("Blade point",PrimitiveType.Cube,root,new Vector3(.012f,1.12f,0),new Vector3(.034f,.10f,.018f),steel);tip.localRotation=Quaternion.Euler(0,0,-15);
                Part("Blade glint",PrimitiveType.Cube,root,new Vector3(-.028f,.61f,0),new Vector3(.006f,.99f,.026f),light);
                for(int i=0;i<5;i++){var wrap=Part("Hilt binding",PrimitiveType.Cube,root,new Vector3(0,-.16f+i*.043f,0),new Vector3(.055f,.013f,.052f),steel);wrap.localEulerAngles=new Vector3(0,0,35);}
            }
            var trailObj=new GameObject("Sword wake");trailObj.transform.SetParent(root,false);trailObj.transform.localPosition=new Vector3(0,.92f,0);
            var trail=trailObj.AddComponent<TrailRenderer>();trail.sharedMaterial=light;trail.time=.15f;trail.minVertexDistance=.05f;trail.startWidth=.5f;trail.endWidth=0;trail.emitting=false;trail.shadowCastingMode=ShadowCastingMode.Off;
            trail.startColor=enemy?new Color(1,.17f,.02f,.7f):new Color(1,.85f,.45f,.65f);trail.endColor=new Color(1,.5f,.1f,0);
            return root;
        }

        public static Transform SwordScabbard(Transform parent)
        {
            var model=Resources.Load<GameObject>("Trial/Models/MingShuang/MingShuangScabbard");
            if(!model)return null;
            // Drive the socket in sword space and preserve the FBX import rotation.
            var socket=new GameObject("MingShuangScabbardSocket").transform;
            socket.SetParent(parent,false);
            var sheath=Object.Instantiate(model,socket,false);
            TrialFighter.DisableShadows(sheath.transform);
            return socket;
        }

        public void Dispose(){foreach(var o in owned) if(o)Object.Destroy(o);owned.Clear();}
    }
}
