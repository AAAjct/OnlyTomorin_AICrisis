using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialWorld
    {
        public static readonly Vector3 TerraceCenter=new(0,0,25);
        public static readonly Vector3 TerraceCheckpoint=new(0,0,15.8f);
        public static readonly Vector3 TerraceBossSpawn=new(0,0,27.2f);
        public const float TerraceEntryZ=19;
        const float TerraceRadius=13;
        bool swordTerrace;
        Material terraceLights;
        static readonly Color TerraceIdleLight=new(.90f,.22f,.075f);
        static readonly Color TerracePhaseLight=new(1.60f,.32f,.24f);

        public static float TerraceWallDistance(Vector3 position)=>TerraceRadius-Vector2.Distance(
            new Vector2(position.x,position.z),new Vector2(TerraceCenter.x,TerraceCenter.z));

        internal float DuelWallDistance(Vector3 position)
        {
            if(swordTerrace)return TerraceWallDistance(position);
            Vector3 offset=position-TerraceCenter;
            return ChampionArenaHalfExtent-ChampionArenaWallThickness*.5f-Mathf.Max(Mathf.Abs(offset.x),Mathf.Abs(offset.z));
        }

        // The rectangular arena uses its physical walls and the fighter's CharacterController.
        internal Vector3 ClipDuelStep(Vector3 position,Vector3 step,float inset)=>
            swordTerrace?ClipTerraceStep(position,step,inset):step;

        internal static Vector3 ClipTerraceStep(Vector3 position,Vector3 step,float inset)
        {
            Vector3 start=Vector3.ProjectOnPlane(position-TerraceCenter,Vector3.up);
            float limit=TerraceRadius-inset;
            if((start+step).sqrMagnitude<=limit*limit || step.sqrMagnitude<.00001f)return step;
            Vector3 direction=step.normalized;
            float dot=Vector3.Dot(start,direction);
            float discriminant=dot*dot+limit*limit-start.sqrMagnitude;
            if(discriminant<0)return Vector3.zero;
            float travel=Mathf.Clamp(-dot+Mathf.Sqrt(discriminant),0,step.magnitude);
            return step*(travel/step.magnitude);
        }

        void BuildSwordTerrace()
        {
            swordTerrace=true;
            RenderSettings.fogColor=new Color(.23f,.31f,.31f);
            RenderSettings.fogDensity=.026f;
            RenderSettings.ambientSkyColor=new Color(.52f,.61f,.62f);
            RenderSettings.ambientEquatorColor=new Color(.31f,.38f,.37f);
            RenderSettings.ambientGroundColor=new Color(.20f,.22f,.19f);
            RenderSettings.skybox.SetColor("_HorizonColor",new Color(.42f,.54f,.53f));
            RenderSettings.skybox.SetColor("_ZenithColor",new Color(.18f,.27f,.30f));

            var stone=Material("Terrace · wet bluestone",new Color(.31f,.35f,.34f),.08f,.61f,true);
            var dark=Material("Terrace · weathered slate",new Color(.20f,.24f,.23f),.05f,.38f,true);
            var stripe=Material("Terrace · worn coping",new Color(.33f,.36f,.33f),.04f,.37f,true);
            var patch=Material("Terrace · patched bluestone",new Color(.28f,.32f,.31f),.05f,.50f,true);
            var rose=Material("Terrace · cinnabar threshold",new Color(.43f,.19f,.13f),.18f,.29f);
            terraceLights=Material("Terrace · red lantern paper",new Color(.56f,.12f,.055f),0,.25f,false,TerraceIdleLight);

            // A level collision plane sits flush with the paving. The circular rail owns the boundary.
            var floor=Box("Terrace · level collision",TerraceCenter+Vector3.down*.18f,new Vector3(26.8f,.36f,26.8f),stone);
            floor.GetComponent<Renderer>().enabled=false;
            Part("Terrace · continuous circular deck",PrimitiveType.Cylinder,Root,TerraceCenter+Vector3.down*.345f,new Vector3(26.7f,.34f,26.7f),dark);
            BuildTerracePaving(stone,patch,stripe);
            TerraceLine(ShrinePosition+Vector3.up*.012f,.48f,.032f,rose);

            const int count=64;
            const float railRadius=13.18f;
            float chord=2*railRadius*Mathf.Tan(Mathf.PI/count);
            for(int i=0;i<count;i++)
            {
                float angle=i*360f/count;
                var rotation=Quaternion.Euler(0,angle,0);
                var radial=rotation*Vector3.forward;
                Vector3 position=TerraceCenter+radial*railRadius;
                var wall=Box("Terrace · low perimeter",position+Vector3.up*.24f,new Vector3(chord+.05f,.48f,.36f),dark);
                wall.rotation=rotation;
                Box("Terrace · rail cap",position+Vector3.up*.51f,new Vector3(chord+.045f,.09f,.47f),stripe,false).rotation=rotation;
                // This upper extension keeps jump attacks in the arena without blocking the camera.
                var above=new GameObject("Terrace · perimeter jump limit");
                above.transform.SetParent(wall,false);
                above.layer=2;
                var collider=above.AddComponent<BoxCollider>();
                collider.center=new Vector3(0,7.9f,0);
                collider.size=new Vector3(1,15,1);
                if(i%4==0)
                    Box("Terrace · stone pier",position+Vector3.up*.51f,new Vector3(.43f,.84f,.52f),dark,false).rotation=rotation;
            }

            Box("Terrace · opening line",new Vector3(0,.012f,TerraceEntryZ),new Vector3(5.7f,.012f,.043f),rose,false);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Terrace · arrival inlay",new Vector3(side*.83f,.012f,16.45f),new Vector3(.037f,.012f,4.0f),stripe,false);
                for(int i=0;i<4;i++)
                {
                    float z=17+i*.48f;
                    Box("Terrace · entry ticks",new Vector3(side*(.98f+i*.14f),.013f,z),new Vector3(.26f,.012f,.034f),rose,false);
                }
            }
            Gate=new GameObject("Terrace · challenge available");
            Gate.transform.SetParent(Root,false);Gate.SetActive(false);
            ArenaEntrance=new GameObject("Terrace · duel active");
            ArenaEntrance.transform.SetParent(Root,false);ArenaEntrance.SetActive(false);

            BuildTerraceCourtyard(stone,dark);
            var key=Root.Find("Moonlight").GetComponent<Light>();
            key.color=new Color(.85f,.93f,1);key.intensity=1.65f;
            var fill=Root.Find("Amber rim").GetComponent<Light>();
            fill.color=new Color(.94f,.86f,.76f);fill.intensity=.62f;
        }

        void BuildTerraceCourtyard(Material stone,Material slate)
        {
            Box("Terrace · precinct foundation",new Vector3(0,-.60f,26),new Vector3(56,.38f,54),slate,false);
            Temple(new Vector3(0,-.42f,45),16,7);
            Temple(new Vector3(-19,-.42f,34),10,5.2f);
            Temple(new Vector3(19,-.42f,34),10,5.2f);
            Temple(new Vector3(-18,-.42f,17),9,4.5f);
            Temple(new Vector3(18,-.42f,17),9,4.5f);
            for(int i=0;i<7;i++)
            {
                float x=(i-3)*13;
                MeshPart("Distant ink peaks",cragMesh,new Vector3(x,-2,65+i%3*6),
                    new Vector3(16+i%3*3,16+(i*7%5)*3,15),slate);
            }

            // The gate sits behind the opening line. Its columns leave the central approach clear.
            var gate=new Vector3(0,0,17.1f);
            Roof(gate+Vector3.up*4.65f,9,3.2f,1.25f);
            Box("Terrace · gate crossbeam",gate+Vector3.up*4.33f,new Vector3(8.4f,.40f,.64f),DarkWood,false);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 post=gate+Vector3.right*(side*3.9f);
                Part("Terrace · cedar gate column",PrimitiveType.Cylinder,Root,post+Vector3.up*2.21f,
                    new Vector3(.44f,2.14f,.44f),DarkWood);
                Box("Terrace · column foot",post+Vector3.up*.19f,new Vector3(.78f,.38f,.78f),stone,false);
                TerraceLantern(gate+new Vector3(side*2.65f,4.32f,-.1f));
                TerraceLantern(new Vector3(side*5.8f,4.90f,42.8f));
                Tree(new Vector3(side*15.5f,-.38f,22),1.25f);
                Tree(new Vector3(side*11.8f,-.38f,40.5f),1.05f);
            }
        }

        void TerraceLantern(Vector3 beam)
        {
            Box("Terrace · lantern hanger",beam+Vector3.down*.22f,new Vector3(.045f,.44f,.045f),DarkWood,false);
            Part("Terrace · hanging lantern",PrimitiveType.Sphere,Root,beam+Vector3.down*.68f,
                new Vector3(.56f,.52f,.56f),terraceLights);
            Box("Terrace · lantern tassel",beam+Vector3.down*1.02f,new Vector3(.075f,.25f,.075f),Crimson,false);
        }

        void BuildTerracePaving(Material stone,Material patch,Material coping)
        {
            const float radius=13.22f;
            const float rowDepth=.92f;
            const float slabWidth=1.74f;
            var vertices=new System.Collections.Generic.List<Vector3>();
            var uv=new System.Collections.Generic.List<Vector2>();
            var main=new System.Collections.Generic.List<int>();
            var patches=new System.Collections.Generic.List<int>();
            var trim=new System.Collections.Generic.List<int>();
            for(int row=-14;row<=14;row++)
            {
                float south=Mathf.Max(-13.16f,(row-.5f)*rowDepth+.012f);
                float north=Mathf.Min(13.16f,(row+.5f)*rowDepth-.012f);
                float edge=Mathf.Max(Mathf.Abs(south),Mathf.Abs(north));
                float halfSpan=Mathf.Sqrt(radius*radius-edge*edge);
                for(int column=-8;column<=8;column++)
                {
                    float start=(column+(row%2==0?0:.5f))*slabWidth;
                    float west=Mathf.Max(-halfSpan,start+.015f);
                    float east=Mathf.Min(halfSpan,start+slabWidth-.015f);
                    if(east-west<.065f)continue;
                    int v=vertices.Count;
                    vertices.Add(TerraceCenter+new Vector3(west,.002f,south));
                    vertices.Add(TerraceCenter+new Vector3(west,.002f,north));
                    vertices.Add(TerraceCenter+new Vector3(east,.002f,north));
                    vertices.Add(TerraceCenter+new Vector3(east,.002f,south));
                    for(int i=0;i<4;i++)uv.Add(new Vector2(vertices[v+i].x,vertices[v+i].z)*.44f);
                    var triangles=(row*17+column*13)%19==0?trim:(row*7+column*3)%9==0?patches:main;
                    triangles.Add(v);triangles.Add(v+1);triangles.Add(v+2);
                    triangles.Add(v);triangles.Add(v+2);triangles.Add(v+3);
                }
            }
            var mesh=new Mesh{name="Terrace · staggered bluestone",subMeshCount=3};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
            mesh.SetTriangles(main,0);mesh.SetTriangles(patches,1);mesh.SetTriangles(trim,2);
            mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var tiles=new GameObject("Terrace · staggered bluestone paving");
            tiles.transform.SetParent(Root,false);
            tiles.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=tiles.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{stone,patch,coping};
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        void TerraceLine(Vector3 center,float radius,float width,Material material)
        {
            var line=new GameObject("Terrace · circular inlay").AddComponent<LineRenderer>();
            line.transform.SetParent(Root,false);line.sharedMaterial=material;
            line.alignment=LineAlignment.TransformZ;
            line.transform.rotation=Quaternion.Euler(90,0,0);
            line.shadowCastingMode=ShadowCastingMode.Off;
            line.positionCount=128;line.loop=true;line.widthMultiplier=width;
            for(int i=0;i<128;i++)
            {
                float a=i*Mathf.PI/64;
                line.SetPosition(i,center+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius));
            }
        }

        internal void SetTerracePhase(int phase,float transition)
        {
            if(!terraceLights)return;
            float t=Time.time%1.8f;
            float beat=Mathf.Max(Mathf.Clamp01(1-t/.19f),Mathf.Clamp01(1-Mathf.Abs(t-.31f)/.11f)*.72f);
            float strength=phase==2?InkMainMenu.ReducedMotion?.55f:.40f+.60f*beat:transition*.60f;
            terraceLights.SetColor("_EmissionColor",Color.Lerp(TerraceIdleLight,TerracePhaseLight,strength));
        }
    }
}
