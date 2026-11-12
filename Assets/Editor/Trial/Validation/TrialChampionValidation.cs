using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialChampionValidation
    {
        static readonly List<string> results=new List<string>();
        static IEnumerator routine;
        static string failure;
        public static string Status {get;private set;}="Idle";
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);results.Add("PASS "+message);}
        [MenuItem("Tools/Tomorin/Champions/Validate assets and deformation")]
        public static void Assets()
        {
            results.Clear();
            for(int k=0;k<6;k++)
            {
                var kind=(ChampionKind)k;var asset=AssetDatabase.LoadAssetAtPath<GameObject>(TrialChampionBuilder.PrefabPath(k));
                Check(asset,kind+" prefab exists");var obj=UnityEngine.Object.Instantiate(asset);
                try
                {
                    var rig=obj.GetComponent<TrialChampionRig>();var skins=obj.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.sharedMesh && s.sharedMesh.vertexCount>0).ToArray();
                    Check(rig.DrivenBones.Length==16 && rig.DrivenBones.All(b=>b),kind+" maps all 16 deform joints");
                    Check(skins.Length>0 && skins.All(s=>s.bones.Length>0 && s.bones.Length==s.sharedMesh.bindposes.Length),kind+" all visible meshes have real bindings");
                    Check(skins.All(s=>s.sharedMaterials.All(m=>m && m.shader && m.shader.isSupported && m.shader.name!="Standard")),kind+" visible meshes have supported URP materials");
                    var skin=skins.OrderByDescending(s=>s.sharedMesh.vertexCount).First();var a=new Mesh();var b=new Mesh();
                    try
                    {
                        rig.Pose(-1,0,0,false,0);skin.BakeMesh(a);rig.Pose(0,.55f,0,false,.5f);skin.BakeMesh(b);
                        var av=a.vertices;var bv=b.vertices;float distance=0;for(int v=0;v<av.Length;v++)distance=Mathf.Max(distance,(av[v]-bv[v]).magnitude);
                        Check(distance>.002f,kind+" attack deforms actual vertices");
                        Check(bv.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),kind+" skin deformation remains finite");
                        // BakeMesh's default output already includes the imported
                        // renderer scale; applying TransformPoint would scale it twice.
                        if(kind==ChampionKind.FeiBi || kind==ChampionKind.NuoNuo || kind==ChampionKind.YaYa)
                            Check(bv.All(v=>skin.bounds.Contains(skin.transform.position+skin.transform.rotation*v)),kind+" repaired skin stays inside renderer culling bounds");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
                    var clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Anims/Champions/"+kind});
                    Check(clips.Length==25,kind+" has 25 exported motion clips");
                    float support=float.PositiveInfinity;float travel=0;Vector3 previous=Vector3.zero;
                    for(int f=0;f<8;f++)
                    {
                        rig.Pose(-1,0,1,false,f*.08f);
                        var left=rig.DrivenBones[12].position;var right=rig.DrivenBones[15].position;
                        float ground=Mathf.Min(left.y,right.y);if(f==0)support=ground;
                        Check(Mathf.Abs(ground-support)<.012f,kind+" gait keeps a planted support foot "+f);
                        if(f>0)travel=Mathf.Max(travel,Vector3.Distance(left,previous));previous=left;
                    }
                    Check(travel>.025f,kind+" authored gait lifts and advances the feet");
                    var mechanics=new HashSet<ChampionShape>();
                    for(int i=0;i<TrialChampionCatalog.SkillCount;i++){var move=TrialChampionCatalog.Move(kind,i);mechanics.Add(move.shape);Check(move.windup>=.28f && move.interval>=.28f && move.recovery>=.45f && !string.IsNullOrEmpty(move.counter),kind+" skill "+(i+1)+" fast readable timing and counterplay");}
                    Check(mechanics.Count>=8,kind+" has "+mechanics.Count+" different skill mechanisms");
                }
                finally{UnityEngine.Object.DestroyImmediate(obj);}
            }
            for(int i=0;i<5;i++){var level=(TrialLevel)((int)TrialLevel.Twins+i);Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(TrialChampionCatalog.Scene(level)),level+" scene exists");Check(EditorBuildSettings.scenes.Any(s=>s.enabled && s.path==TrialChampionCatalog.Scene(level)),level+" enabled in build");}
            Write("ChampionAssetValidation.txt");Status="Asset validation passed: "+results.Count;
        }
        [MenuItem("Tools/Tomorin/Champions/Validate all five stages (Play Mode)")]
        public static void Play()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            if(routine!=null)throw new InvalidOperationException("Validation already running");
            results.Clear();failure=null;Status="Running five stages";Application.logMessageReceived+=OnLog;routine=Run();EditorApplication.update+=Step;
        }
        static void OnLog(string log,string trace,LogType type){if(type==LogType.Exception || type==LogType.Error)failure=log;}
        static void Step()
        {
            try{if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended during validation");if(failure!=null)throw new Exception(failure);if(routine.MoveNext())return;Status="PASS "+results.Count(s=>s.StartsWith("PASS"))+" champion runtime checks";Finish();}
            catch(Exception ex){results.Add("FAIL "+ex);Status="FAIL "+ex.Message;Finish();Debug.LogError(Status);}
        }
        static void Finish(){EditorApplication.update-=Step;Application.logMessageReceived-=OnLog;routine=null;Time.timeScale=1;Write("ChampionRuntimeValidation.txt");}
        static void Write(string name){Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/"+name,DateTime.Now.ToString("s")+"\n"+string.Join("\n",results));}
        static IEnumerator Run()
        {
            for(int stage=0;stage<5;stage++)
            {
                TrialLevel level=(TrialLevel)((int)TrialLevel.Twins+stage);Status="Testing "+level;
                var load=SceneManager.LoadSceneAsync(TrialChampionCatalog.Scene(level));while(!load.isDone)yield return null;
                var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();while(!game || !game.Ready){yield return null;game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();}
                Check(game.Level==level && game.IsChampionStage && game.GateOpen,level+" initializes intended open boss stage");
                Check(game.Enemies.Count==(level==TrialLevel.Twins?2:1),level+" correct boss population");
                game.StartTrial();if(game.Player.ChoosingBoon)game.Player.ChooseBoon(game.Player.OfferA);
                int before=Time.frameCount;yield return null;yield return null;Check(Time.frameCount>before,level+" engine advances real frames");
                // Isolate contact tests from real input, keeping the real camera, HUD, colliders and rig loop alive.
                game.enabled=false;game.Input.enabled=false;
                var controls=new GameObject("Champion validation controls").AddComponent<TrialInput>();
                try
                {
                    foreach(var enemy in game.Enemies)
                    {
                        Check(enemy.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m && m.shader && m.shader.isSupported && m.shader.name!="Standard")),enemy.DisplayName+" model, weapons and skill lines have compatible materials");
                        if(enemy.Champion.Kind==ChampionKind.Taffy)
                        {
                            enemy.Champion.Rig.Pose(-1,0,0,false,0);
                            var gun=enemy.Champion.Rig.GetComponentsInChildren<Transform>().First(t=>t.name=="塔菲 · M4 imported weapon");
                            Check(Vector3.Dot(-gun.forward,enemy.transform.forward)>.99f,"Taffy imported -Z muzzle points along actual forward projectile direction");
                            Check(gun.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.GetTexture("_BaseMap")),"Taffy gun preserves its original atlas texture");
                        }
                        Check(enemy.MaxHealth==TrialChampionCatalog.Health(enemy.Champion.Kind),enemy.DisplayName+" intended health baseline");
                        for(int move=0;move<TrialChampionCatalog.SkillCount;move++)
                        {
                            Prepare(game,enemy);var brain=enemy.Champion;brain.BeginMove(move);
                            Check(brain.WindingUp && brain.MoveIndex==move,enemy.DisplayName+" move "+(move+1)+" telegraphs before contact");
                            float t=0;while(t<brain.Move.windup-brain.AimCommitTime+.025f){brain.Tick(.01f);t+=.01f;}
                            Vector3 locked=brain.Aim;game.Player.Teleport(new Vector3(19,0,8));brain.Tick(.02f);
                            Check((brain.Aim-locked).sqrMagnitude<.01f,enemy.DisplayName+" move "+(move+1)+" locks tracking before impact");
                            var shape=brain.Move.shape;bool marked=shape==ChampionShape.Leap || shape==ChampionShape.Dive || shape==ChampionShape.TwinPincer || shape==ChampionShape.Mark || shape==ChampionShape.Mine || shape==ChampionShape.Snare || shape==ChampionShape.Pillars;
                            Vector3 safe=shape==ChampionShape.Annulus || shape==ChampionShape.OrbitWhip?enemy.transform.position+Vector3.right*(brain.Move.inner*.5f):marked?brain.Aim+Vector3.right*(brain.Move.range+1):enemy.transform.position-brain.Heading*(brain.Move.range+2);
                            Check(!brain.Contains(safe),enemy.DisplayName+" move "+(move+1)+" has reachable safe geometry");
                            brain.Tick(.32f);
                            if(shape==ChampionShape.Bullets || shape==ChampionShape.Snipe || shape==ChampionShape.Flight || shape==ChampionShape.Ring || shape==ChampionShape.BladeWave || shape==ChampionShape.Boomerang || shape==ChampionShape.Mine || shape==ChampionShape.Snare || shape==ChampionShape.SilkLine || shape==ChampionShape.Rocket || shape==ChampionShape.Turret || shape==ChampionShape.Ricochet || shape==ChampionShape.Pillars || shape==ChampionShape.Fissure || shape==ChampionShape.Avalanche || shape==ChampionShape.Pellets || shape==ChampionShape.SlideVolley || shape==ChampionShape.OrbitWhip || shape==ChampionShape.SweepRay)Check(brain.ActiveHazards>0,enemy.DisplayName+" move "+(move+1)+" spawns bounded visible hazards");
                            brain.CancelAttack(true);Check(brain.ActiveHazards==0,enemy.DisplayName+" move "+(move+1)+" clears hazards on reset");
                        }
                        Prepare(game,enemy);int parries=game.Player.SuccessfulParries;float health=game.Player.Health;
                        controls.ApplyNetwork(new TrialControls{held=1});game.Player.Tick(0,controls);enemy.Champion.BeginMove(0);enemy.Champion.Tick(enemy.Champion.Move.windup+.005f);
                        if(enemy.Champion.Kind==ChampionKind.Taffy)enemy.Champion.Tick(.10f);
                        Check(game.Player.SuccessfulParries>parries && game.Player.Health==health,enemy.DisplayName+" real first attack can be deflected");
                        Prepare(game,enemy);controls.ApplyNetwork(new TrialControls{pressed=4});game.Player.Tick(0,controls);controls.ApplyNetwork(default);game.Player.Tick(.1f,controls);game.Player.Teleport(new Vector3(0,0,27));
                        int dodges=game.Player.SuccessfulDodges;health=game.Player.Health;enemy.Champion.BeginMove(0);enemy.Champion.Tick(enemy.Champion.Move.windup+.005f);if(enemy.Champion.Kind==ChampionKind.Taffy)enemy.Champion.Tick(.10f);
                        Check(game.Player.Health==health && game.Player.SuccessfulDodges>dodges,enemy.DisplayName+" dodge invulnerability defeats real contact");
                        Prepare(game,enemy);controls.ApplyNetwork(new TrialControls{held=1});game.Player.Tick(0,controls);health=game.Player.Health;
                        game.Player.ReceiveHazardHit(enemy,20,10,false,game.Player.transform.forward);
                        Check(game.Player.Health==health,enemy.DisplayName+" a frontal projectile is deflectable");
                        Prepare(game,enemy);controls.ApplyNetwork(new TrialControls{held=1});game.Player.Tick(0,controls);health=game.Player.Health;
                        game.Player.ReceiveHazardHit(enemy,20,10,false,-game.Player.transform.forward);
                        Check(game.Player.Health<health,enemy.DisplayName+" projectiles arriving from behind bypass a forward guard");
                        Prepare(game,enemy);enemy.Teleport(new Vector3(0,0,12));Vector3 start=enemy.transform.position;
                        for(int f=0;f<40;f++)enemy.Tick(.02f);
                        Check(Vector3.Distance(enemy.transform.position,start)>3,enemy.DisplayName+" closes distance with real mobile footwork");
                        Prepare(game,enemy);enemy.Teleport(new Vector3(0,0,13));typeof(TrialFighter).GetProperty("Health").SetValue(game.Player,80f);
                        controls.ApplyNetwork(new TrialControls{pressed=8});game.Player.Tick(0,controls);enemy.Tick(.24f);
                        Check(enemy.Champion.Mobility!=ChampionMobility.Grounded || enemy.Champion.Kind==ChampionKind.Taffy && enemy.Champion.MoveIndex==1,enemy.DisplayName+" reacts to committed healing with a chase or snipe");
                        Prepare(game,enemy);float ratio=enemy.Health/enemy.MaxHealth;enemy.ScaleForParty(3);Check(Mathf.Abs(enemy.Health/enemy.MaxHealth-ratio)<.001f && enemy.MaxHealth==TrialChampionCatalog.Health(enemy.Champion.Kind)*3,enemy.DisplayName+" party scaling preserves health ratio");enemy.ScaleForParty(1);
                        var state=enemy.Champion.CaptureNetwork();enemy.Champion.ApplyNetwork(state,0);Check(enemy.Champion.MoveIndex==state.move,enemy.DisplayName+" replication roundtrip");
                        int dive=-1,counter=-1,mine=-1;
                        for(int i=0;i<TrialChampionCatalog.SkillCount;i++){var shape=TrialChampionCatalog.Move(enemy.Champion.Kind,i).shape;if(shape==ChampionShape.Dive)dive=i;if(shape==ChampionShape.Counter)counter=i;if(shape==ChampionShape.Mine || shape==ChampionShape.Snare)mine=i;}
                        if(dive>=0)
                        {
                            Prepare(game,enemy);enemy.Champion.BeginMove(dive);enemy.Champion.Tick(enemy.Champion.Move.windup*.5f);
                            Check(enemy.transform.position.y>1.5f,enemy.DisplayName+" aerial strike moves the actual actor and hurtbox");
                            float foeHealth=enemy.Health;game.Player.Teleport(TrialChampion.Planar(enemy.transform.position));controls.ApplyNetwork(new TrialControls{pressed=1});game.Player.Tick(0,controls);controls.ApplyNetwork(default);game.Player.Tick(.20f,controls);
                            Check(enemy.Health==foeHealth,enemy.DisplayName+" ground sword cannot hit a high aerial hurtbox");
                        }
                        if(counter>=0)
                        {
                            Prepare(game,enemy);enemy.Champion.BeginMove(counter);controls.ApplyNetwork(new TrialControls{pressed=1});game.Player.Tick(0,controls);enemy.ReceiveHit(game.Player,28,21);
                            Check(!enemy.Champion.CounterReady && enemy.Health>enemy.MaxHealth-10,enemy.DisplayName+" stance catches light attacks and telegraphs a counter");
                            Prepare(game,enemy);enemy.Champion.BeginMove(counter);controls.ApplyNetwork(new TrialControls{pressed=2});game.Player.Tick(0,controls);enemy.ReceiveHit(game.Player,50,40);
                            Check(enemy.Vulnerable,enemy.DisplayName+" heavy attack breaks counter stance");
                        }
                        if(mine>=0)
                        {
                            Prepare(game,enemy);enemy.Champion.BeginMove(mine);enemy.Champion.Tick(enemy.Champion.Move.windup+.01f);Vector3 mark=enemy.Champion.Aim;int active=enemy.Champion.ActiveHazards;
                            enemy.Champion.CutHazards(mark-Vector3.forward,Vector3.forward,3,true);Check(active>0 && enemy.Champion.ActiveHazards==0,enemy.DisplayName+" deployed traps can be cut before detonation");
                        }
                    }
                    game.RestartTrial();if(game.Player.ChoosingBoon)game.Player.ChooseBoon(game.Player.OfferA);
                    if(level==TrialLevel.Twins)
                    {
                        game.Enemies[0].ReceiveHit(game.Player,99999,0,true);Check(game.Mode!=TrialMode.Victory,"Twins: defeating one boss does not finish the stage");
                        game.Enemies[1].Tick(.01f);Check(game.Enemies[1].Champion.Phase==3,"Twins: survivor enters unique final repertoire");
                        game.Enemies[1].ReceiveHit(game.Player,99999,0,true);Check(game.Mode==TrialMode.Victory,"Twins: both bosses must fall for victory");
                    }
                    game.RestartTrial();if(game.Player.ChoosingBoon)game.Player.ChooseBoon(game.Player.OfferA);game.enabled=true;game.Input.enabled=true;
                    game.Player.Teleport(new Vector3(0,0,23));game.CameraRig.Snap();foreach(var e in game.Enemies){e.Tick(.001f);e.HoldCombat(30);}
                    yield return null;yield return null;
                    TrialValidation.Capture("Logs/Trial/Champion_"+level+".png");
                    // Measure the actual rendered scene after warm-up, not the import/generation spike.
                    int frames=0;double total=0;float until=Time.realtimeSinceStartup+1;
                    while(Time.realtimeSinceStartup<until){total+=Time.unscaledDeltaTime;frames++;yield return null;}
                    results.Add("MEASURE "+level+" rendered average="+(1000*total/Math.Max(1,frames)).ToString("F2")+"ms; active renderers="+UnityEngine.Object.FindObjectsByType<Renderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy)+"; hazards="+game.Enemies.Sum(e=>e.Champion.ActiveHazards));
                    if(level==TrialLevel.Taffy)
                    {
                        game.enabled=false;var gunner=game.Enemies[0];Prepare(game,gunner);gunner.Champion.BeginMove(7);game.Player.Teleport(new Vector3(0,0,7));
                        frames=0;total=0;int peak=0;until=Time.realtimeSinceStartup+2;
                        while(Time.realtimeSinceStartup<until)
                        {
                            if(game.Player.Health<100)game.Player.Restore(new Vector3(0,0,7));
                            gunner.Champion.Tick(Mathf.Min(.04f,Time.unscaledDeltaTime));peak=Mathf.Max(peak,gunner.Champion.ActiveHazards);total+=Time.unscaledDeltaTime;frames++;yield return null;
                        }
                        results.Add("MEASURE Taffy active barrage="+(1000*total/Math.Max(1,frames)).ToString("F2")+"ms; peak pooled hazards="+peak+" / 48");Check(peak>0 && peak<=48,"Taffy active barrage stays within fixed projectile pool");game.enabled=true;
                    }
                    game.RestartTrial();Check(game.Enemies.All(e=>e.Alive && e.Champion.ActiveHazards==0),level+" retry resets every boss and projectile");
                }
                finally{if(controls)UnityEngine.Object.Destroy(controls.gameObject);}
            }
        }
        static void Prepare(TrialGame game,TrialEnemy enemy)
        {
            game.Player.ResetRunBoon();game.Player.Restore(new Vector3(0,0,27));game.Player.transform.rotation=Quaternion.Euler(0,180,0);
            enemy.ResetFighter();enemy.Teleport(new Vector3(0,0,25));enemy.Tick(.001f);enemy.transform.rotation=Quaternion.identity;
            Physics.SyncTransforms();
        }
    }
}
