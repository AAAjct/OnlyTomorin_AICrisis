using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialChampionBuilder
    {
        public const string PrefabFolder="Assets/Prefabs/Champions";
        const string MeshFolder="Assets/Art/Champions";
        static readonly string[] ModelPaths={"Assets/Art/Models/FeiBi/FeiBi.fbx","Assets/Art/Models/NuoNuo/NuoNuo.fbx","Assets/Art/Models/Anon/Anon.fbx","Assets/Art/Models/Taffy/Taffy.fbx","Assets/Art/Models/XiangQiE/香企鹅.fbx","Assets/Art/Models/YaYa/YaYa.fbx"};
        static readonly string[] BoneNames={"hips","spine","chest","head","upper_arm.L","lower_arm.L","hand.L","upper_arm.R","lower_arm.R","hand.R","upper_leg.L","lower_leg.L","foot.L","upper_leg.R","lower_leg.R","foot.R"};
        public static string PrefabPath(int kind)=>PrefabFolder+"/"+(ChampionKind)kind+"_Champion.prefab";

        [MenuItem("Tools/Tomorin/Champions/Repair YaYa skin and weapon materials")]
        public static void RepairPresentation()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before repairing assets.");
            var report=new System.Text.StringBuilder();
            foreach(var kind in new[]{ChampionKind.FeiBi,ChampionKind.NuoNuo,ChampionKind.YaYa}){BuildCharacter(kind,report);BakeAnimations(kind);}
            var gun=AssetDatabase.LoadAssetAtPath<Material>(MeshFolder+"/ChampionGun.mat");
            if(!gun){gun=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(gun,MeshFolder+"/ChampionGun.mat");}
            gun.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Low Poly Weapons VOL.1/Textures_Guns.png"));
            gun.SetColor("_BaseColor",Color.white);gun.SetFloat("_Metallic",.45f);gun.SetFloat("_Smoothness",.38f);EditorUtility.SetDirty(gun);
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath((int)ChampionKind.Taffy));
            try{var so=new SerializedObject(prefab.GetComponent<TrialChampionRig>());so.FindProperty("weaponMaterial").objectReferenceValue=gun;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath((int)ChampionKind.Taffy));}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            AssetDatabase.SaveAssets();Debug.Log(report.ToString());
        }

        [MenuItem("Tools/Tomorin/Champions/Build five boss stages and all animations")]
        public static void BuildAll()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before generating scenes.");
            Directory.CreateDirectory(PrefabFolder);Directory.CreateDirectory(MeshFolder);Directory.CreateDirectory("Assets/Anims/Champions");AssetDatabase.Refresh();
            var report=new System.Text.StringBuilder();
            for(int i=0;i<6;i++){BuildCharacter((ChampionKind)i,report);BakeAnimations((ChampionKind)i);}
            var source=EditorSceneManager.OpenScene(TrialBuilder.ScenePath,OpenSceneMode.Additive);
            try
            {
                var original=source.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<TrialGame>()).Single();
                for(int i=0;i<5;i++)
                {
                    TrialLevel level=(TrialLevel)((int)TrialLevel.Twins+i);
                    var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                    try
                    {
                        var root=new GameObject(TrialChampionCatalog.LevelName(level));SceneManager.MoveGameObjectToScene(root,scene);
                        var game=root.AddComponent<TrialGame>();EditorUtility.CopySerialized(original,game);
                        var so=new SerializedObject(game);so.FindProperty("initialLevel").enumValueIndex=(int)level;
                        var models=so.FindProperty("championModels");models.arraySize=6;
                        for(int k=0;k<6;k++)models.GetArrayElementAtIndex(k).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(k));
                        so.FindProperty("championGun").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Low Poly Weapons VOL.1/Prefabs/M4_8.prefab");so.ApplyModifiedPropertiesWithoutUndo();
                        var camera=new GameObject("Main Camera");SceneManager.MoveGameObjectToScene(camera,scene);camera.tag="MainCamera";camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();camera.transform.SetPositionAndRotation(new Vector3(0,3,3),Quaternion.Euler(17,0,0));
                        // Inspector-readable guide is also a convenient scene entry landmark.
                        var guide=new GameObject("48m arena · "+TrialChampionCatalog.Lesson(level));SceneManager.MoveGameObjectToScene(guide,scene);
                        EditorSceneManager.SaveScene(scene,TrialChampionCatalog.Scene(level));report.AppendLine("SCENE "+TrialChampionCatalog.Scene(level));
                    }
                    finally{EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally{EditorSceneManager.CloseScene(source,true);}
            var buildScenes=EditorBuildSettings.scenes.ToList();
            for(int i=0;i<5;i++){string path=TrialChampionCatalog.Scene((TrialLevel)((int)TrialLevel.Twins+i));if(!buildScenes.Any(s=>s.path==path))buildScenes.Add(new EditorBuildSettingsScene(path,true));}
            EditorBuildSettings.scenes=buildScenes.ToArray();
            TrialBuilder.Bake();AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/ChampionAssetReport.txt",report.ToString());
            Debug.Log("[Champions] Five stages, six animated prefabs and 150 champion clips generated. "+report);
        }

        static void BuildCharacter(ChampionKind kind,System.Text.StringBuilder report)
        {
            var root=new GameObject(kind+"_Champion");
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[(int)kind]);if(!source)throw new FileNotFoundException(ModelPaths[(int)kind]);
                var model=UnityEngine.Object.Instantiate(source,root.transform);model.name=kind+"_Model";
                foreach(var a in model.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(a);
                foreach(var c in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
                foreach(var camera in model.GetComponentsInChildren<Camera>())UnityEngine.Object.DestroyImmediate(camera);
                foreach(var light in model.GetComponentsInChildren<Light>())UnityEngine.Object.DestroyImmediate(light);
                var visible=model.GetComponentsInChildren<Renderer>().Where(r=>MeshOf(r) && MeshOf(r).vertexCount>0).ToArray();
                var bounds=visible[0].bounds;foreach(var r in visible)bounds.Encapsulate(r.bounds);
                model.transform.localScale*=1.9f/bounds.size.y;
                bounds=visible[0].bounds;foreach(var r in visible)bounds.Encapsulate(r.bounds);
                model.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                var bones=MapBones(model,kind,visible);
                if(bones.Any(b=>!b))throw new InvalidOperationException(kind+" missing mapped deform bones");
                if(kind==ChampionKind.FeiBi || kind==ChampionKind.NuoNuo || kind==ChampionKind.YaYa)
                    RepairSkin(model,bones,visible,kind,report);
                // Throw away unrelated duplicate armatures on static exports after binding the generated copy.
                var used=new HashSet<Transform>(model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s=>s.bones));
                foreach(var t in model.GetComponentsInChildren<Transform>().Reverse())
                    if(t!=model.transform && t.childCount==0 && !t.GetComponent<Renderer>() && !used.Contains(t) && !bones.Contains(t))UnityEngine.Object.DestroyImmediate(t.gameObject);
                var rig=root.AddComponent<TrialChampionRig>();rig.Configure(kind,model.transform,bones);
                if(kind==ChampionKind.Anon)TrialAnonSkirtRepair.Apply(root);
                if(kind==ChampionKind.Taffy)
                {var so=new SerializedObject(rig);so.FindProperty("weaponMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>(MeshFolder+"/ChampionGun.mat");so.ApplyModifiedPropertiesWithoutUndo();}
                rig.Pose(-1,0,0,false,0);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath((int)kind));
                report.AppendLine(kind+": "+root.GetComponentsInChildren<SkinnedMeshRenderer>().Count(s=>s.sharedMesh && s.sharedMesh.vertexCount>0)+" animated meshes; 16 driven joints");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static Mesh MeshOf(Renderer r)=>r is SkinnedMeshRenderer s?s.sharedMesh:r.GetComponent<MeshFilter>()?r.GetComponent<MeshFilter>().sharedMesh:null;
        static Transform[] MapBones(GameObject model,ChampionKind kind,Renderer[] visible)
        {
            var all=model.GetComponentsInChildren<Transform>();
            if(kind==ChampionKind.Anon || kind==ChampionKind.Taffy)
            {
                var weighted=visible.OfType<SkinnedMeshRenderer>().First(s=>s.bones.Length>0).bones;
                // The imported MMD names are lossy underscores. Stable source bone indices avoid ambiguous name lookups.
                int[] indices=kind==ChampionKind.Anon?new[]{0,1,2,65,8,9,13,23,24,28,43,44,45,47,48,49}:new[]{2,0,1,4,32,36,41,6,10,15,61,62,63,57,58,59};
                return indices.Select(i=>weighted[i]).ToArray();
            }
            var hips=all.Where(t=>t.name=="hips").ToArray();
            // These FBXs include several unrelated duplicate skeletons. Match the
            // visible palms, not the head centre (a chibi head joint is at its neck).
            var body=visible.First(r=>r.name.StartsWith("body",StringComparison.OrdinalIgnoreCase) || r.name=="yaya.002");
            var vertices=MeshOf(body).vertices.Select(v=>body.transform.TransformPoint(v)).ToArray();
            float minX=vertices.Min(v=>v.x),maxX=vertices.Max(v=>v.x);
            var left=vertices.Where(v=>v.x<Mathf.Lerp(minX,maxX,.1f)).ToArray();
            var right=vertices.Where(v=>v.x>Mathf.Lerp(minX,maxX,.9f)).ToArray();
            Vector3 leftPalm=left.Aggregate(Vector3.zero,(a,b)=>a+b)/left.Length,rightPalm=right.Aggregate(Vector3.zero,(a,b)=>a+b)/right.Length;
            Transform selected=hips.OrderBy(h=>
            {
                var children=h.GetComponentsInChildren<Transform>();var l=children.FirstOrDefault(t=>t.name=="hand.L");var r=children.FirstOrDefault(t=>t.name=="hand.R");
                if(!l || !r)return float.MaxValue;
                Vector3 dl=l.position-leftPalm,dr=r.position-rightPalm;
                return 4*(dl.y*dl.y+dr.y*dr.y)+dl.z*dl.z+dr.z*dr.z+.05f*(dl.x*dl.x+dr.x*dr.x);
            }).First();
            var branch=selected.GetComponentsInChildren<Transform>();
            return BoneNames.Select(n=>branch.FirstOrDefault(t=>t.name==n)).ToArray();
        }
        static void RepairSkin(GameObject model,Transform[] bones,Renderer[] visible,ChampionKind kind,System.Text.StringBuilder report)
        {
            // Originals contain skeletons but zero deformation weights. Generate smooth segment weights on a COPY.
            // Face/hair/hat meshes remain rigid on the head, preserving facial blend-shapes and silhouettes.
            int[] ends={1,2,3,3,5,6,6,8,9,9,11,12,12,14,15,15};
            string meshPath=MeshFolder+"/"+kind+"_Skinned.asset";bool first=true;
            foreach(var renderer in visible)
            {
                var mesh=UnityEngine.Object.Instantiate(MeshOf(renderer));
                mesh.name=first?System.IO.Path.GetFileNameWithoutExtension(meshPath):kind+"_"+renderer.name+"_Weighted";
                var verts=mesh.vertices;var weights=new BoneWeight[verts.Length];
                // YaYa's numbered objects hide the meaning of the original parts. Hair,
                // braids and ornaments must follow the head even below the shoulders.
                bool yaya=kind==ChampionKind.YaYa;
                bool rigid=renderer.name.ToLowerInvariant().Contains("head") || renderer.name.ToLowerInvariant().Contains("hair") || renderer.name=="hat" || renderer.name=="fa_jia"
                    || yaya && renderer.name!="yaya.002" && renderer.name!="yaya.013" && renderer.name!="yaya.015";
                for(int v=0;v<verts.Length;v++)
                {
                    Vector3 p=renderer.transform.TransformPoint(verts[v]);
                    int best=0,second=0;float d0=float.MaxValue,d1=float.MaxValue;
                    if(rigid || yaya && renderer.name=="yaya.002" && p.y>=bones[3].position.y-.025f*model.transform.lossyScale.y)
                    {weights[v]=new BoneWeight{boneIndex0=3,weight0=1};continue;}
                    // The skirt is a single garment, not a pair of legs. Keep its
                    // central panels together while sleeves still follow the arms.
                    if(yaya && (renderer.name=="yaya.013" || renderer.name=="yaya.015") && Mathf.Abs(p.x-bones[0].position.x)<.18f*model.transform.lossyScale.x)
                    {weights[v]=new BoneWeight{boneIndex0=1,weight0=1};continue;}
                    int arm=-1;
                    if(!rigid && (kind==ChampionKind.FeiBi || kind==ChampionKind.NuoNuo))
                    {
                        // Source exports have no weights. Do not blend palms with
                        // sleeves, torso or the nearby oversized chibi head.
                        for(int side=0;side<2;side++)
                        {
                            int upper=side==0?4:7,hand=upper+2;
                            Vector3 axis=(bones[hand].position-bones[upper].position).normalized;
                            float along=Vector3.Dot(p-bones[upper].position,axis);
                            float wrist=Vector3.Dot(bones[hand].position-bones[upper].position,axis);
                            if(along>wrist*.78f && Vector3.Distance(p,bones[hand].position)<wrist*.75f)
                            {arm=upper;break;}
                        }
                        if(arm>=0 && renderer.name.StartsWith("body",StringComparison.OrdinalIgnoreCase))
                        {weights[v]=new BoneWeight{boneIndex0=arm+2,weight0=1};continue;}
                    }
                    for(int k=0;k<16;k++)
                    {
                        if(arm>=0 && (k<arm || k>arm+2))continue;
                        Vector3 a=bones[k].position,b=bones[ends[k]].position;
                        if(k==3)b=a+Vector3.up*.40f;
                        if(k==6 || k==9)b=a+(a-bones[k-1].position).normalized*.12f;
                        if(k==12 || k==15)b=a+Vector3.forward*.13f;
                        Vector3 ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(.00001f,ab.sqrMagnitude));float d=(p-(a+ab*t)).sqrMagnitude;
                        // Keep upper-body skin away from coincident hand/leg helpers.
                        if(k>=10 && p.y>bones[2].position.y)d+=.25f;
                        if(d<d0){d1=d0;second=best;d0=d;best=k;}else if(d<d1){d1=d;second=k;}
                    }
                    float w0=1/Mathf.Pow(.012f+d0,2),w1=1/Mathf.Pow(.012f+d1,2);float total=w0+w1;
                    weights[v]=new BoneWeight{boneIndex0=best,boneIndex1=second,weight0=w0/total,weight1=w1/total};
                }
                mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*renderer.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
                var old=first?AssetDatabase.LoadAssetAtPath<Mesh>(meshPath):null;
                if(first && old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}
                else if(first)AssetDatabase.CreateAsset(mesh,meshPath);
                else
                {
                    var sub=AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().FirstOrDefault(m=>m.name==mesh.name);
                    if(sub){EditorUtility.CopySerialized(mesh,sub);UnityEngine.Object.DestroyImmediate(mesh);mesh=sub;}else AssetDatabase.AddObjectToAsset(mesh,meshPath);
                }
                first=false;
                // CopySerialized can leave an existing Mesh's native skin buffers
                // unchanged. Apply the generated weights to the persisted mesh too.
                mesh.boneWeights=weights;
                mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*renderer.transform.localToWorldMatrix).ToArray();
                EditorUtility.SetDirty(mesh);
                var go=renderer.gameObject;var materials=renderer.sharedMaterials;var skin=renderer as SkinnedMeshRenderer;
                if(!skin){UnityEngine.Object.DestroyImmediate(go.GetComponent<MeshFilter>());UnityEngine.Object.DestroyImmediate(renderer);skin=go.AddComponent<SkinnedMeshRenderer>();}
                skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=model.transform;skin.sharedMaterials=materials;
                // Mesh object axes differ from the model root on these static exports.
                var cullBounds=new Bounds(model.transform.InverseTransformPoint(go.transform.TransformPoint(verts[0])),Vector3.zero);
                foreach(var vertex in verts)cullBounds.Encapsulate(model.transform.InverseTransformPoint(go.transform.TransformPoint(vertex)));
                cullBounds.Expand(1.2f/model.transform.lossyScale.y);skin.localBounds=cullBounds;
                skin.updateWhenOffscreen=false;skin.quality=SkinQuality.Bone2;
                report.AppendLine("SKIN "+kind+"/"+go.name+": "+mesh.vertexCount+" vertices, source FBX preserved");
            }
        }

        static void BakeAnimations(ChampionKind kind)
        {
            string folder="Assets/Anims/Champions/"+kind;Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath((int)kind)));
            try
            {
                var rig=actor.GetComponent<TrialChampionRig>();
                var targets=new[]{rig.Model}.Concat(rig.DrivenBones).Distinct().ToArray();
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(folder+"/"+kind+".controller")??AnimatorController.CreateAnimatorControllerAtPath(folder+"/"+kind+".controller");
                foreach(var s in controller.layers[0].stateMachine.states.ToArray())controller.layers[0].stateMachine.RemoveState(s.state);
                var names=new[]{"Idle","Walk","Run","Jump","Flight","Guard","Stagger","Death","PhaseAwakening"};
                var ids=new[]{-1,-1,-1,-3,-4,-5,-6,-7,-8};
                for(int i=0;i<names.Length;i++)BakeClip(actor,rig,targets,controller,folder,names[i],ids[i],i==1?.55f:i==2?1:0,i==0?2:i==1?1:i==2?.7f:1.5f,i<3);
                for(int i=0;i<TrialChampionCatalog.SkillCount;i++)BakeClip(actor,rig,targets,controller,folder,"Skill"+(i+1).ToString("00"),i,0,TrialChampionCatalog.Move(kind,i).Duration,false);
                rig.ResetPose();actor.AddComponent<Animator>().runtimeAnimatorController=controller;
                const string previewFolder="Assets/Prefabs/AnimationPreviews";
                Directory.CreateDirectory(previewFolder);AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(actor,previewFolder+"/"+kind+"_AnimationPreview.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);}
        }
        static void BakeClip(GameObject actor,TrialChampionRig rig,Transform[] targets,AnimatorController controller,string folder,string name,int move,float movement,float duration,bool loop)
        {
            const int rate=30;int frames=Mathf.CeilToInt(duration*rate);var curves=new AnimationCurve[targets.Length,7];var prev=new Quaternion[targets.Length];
            for(int i=0;i<targets.Length;i++)for(int k=0;k<7;k++)curves[i,k]=new AnimationCurve();
            var data=TrialChampionCatalog.Move(rig.Kind,Mathf.Max(0,move));
            for(int f=0;f<=frames;f++)
            {
                float t=duration*f/frames,p=t/duration;
                if(move>=0){int beat=Mathf.Clamp(Mathf.FloorToInt((t-data.windup)/data.interval)+1,0,data.beats);float next=data.windup+beat*data.interval;p=beat>=data.beats?.65f+Mathf.Clamp01((t-(data.windup+data.interval*(data.beats-1)))/(data.recovery+.2f))*.35f:Mathf.Clamp01(1-(next-t)/Mathf.Min(data.windup,beat==0?data.windup:data.interval))*.38f;if(beat>0 && t-(data.windup+(beat-1)*data.interval)<.20f)p=.5f;}
                rig.Pose(move,p,movement,move==-6,t);
                for(int i=0;i<targets.Length;i++)
                {
                    var q=targets[i].localRotation;if(f>0 && Quaternion.Dot(prev[i],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);prev[i]=q;Vector3 v=targets[i].localPosition;float[] a={q.x,q.y,q.z,q.w,v.x,v.y,v.z};
                    for(int k=0;k<7;k++)curves[i,k].AddKey(t,a[k]);
                }
            }
            string path=folder+"/"+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}else clip.ClearCurves();clip.name=name;clip.frameRate=rate;
            string[] properties={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"};
            for(int i=0;i<targets.Length;i++)for(int k=0;k<7;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(targets[i],actor.transform),typeof(Transform),properties[k]),Simplify(curves[i,k],k<4?.0007f:.0002f));
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);controller.AddMotion(clip).name=name;
        }
        static AnimationCurve Simplify(AnimationCurve source,float tolerance)
        {
            var keys=source.keys;var keep=new SortedSet<int>{0,keys.Length-1};Reduce(keys,0,keys.Length-1,tolerance,keep);
            var result=new AnimationCurve(keep.Select(i=>new Keyframe(keys[i].time,keys[i].value)).ToArray());
            for(int i=0;i<result.length;i++){AnimationUtility.SetKeyLeftTangentMode(result,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(result,i,AnimationUtility.TangentMode.Linear);}return result;
        }
        static void Reduce(Keyframe[] keys,int first,int last,float tolerance,SortedSet<int> keep)
        {
            float error=tolerance;int split=-1;for(int i=first+1;i<last;i++){float expected=Mathf.Lerp(keys[first].value,keys[last].value,Mathf.InverseLerp(keys[first].time,keys[last].time,keys[i].time));float d=Mathf.Abs(keys[i].value-expected);if(d>error){error=d;split=i;}}
            if(split<0)return;keep.Add(split);Reduce(keys,first,split,tolerance,keep);Reduce(keys,split,last,tolerance,keep);
        }
    }
}
