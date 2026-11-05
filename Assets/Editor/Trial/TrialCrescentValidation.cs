using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialCrescentValidation
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> checks=new List<string>();
        static int errors;
        static T Field<T>(object art,string name)=>(T)art.GetType().GetField(name).GetValue(art);
        static void Set(object art,string name,object value)=>art.GetType().GetField(name).SetValue(art,value);
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        static bool Finite(Vector3 p)=>float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        static void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert || message.Contains("MinMaxAABB"))errors++;}

        [MenuItem("Tools/Tomorin/Verify black scarlet crescents")]
        public static void Run()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            if(!EditorApplication.isPlaying || !game || !game.Ready || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Run an offline arena first.");
            checks.Clear();errors=0;Application.logMessageReceived+=OnLog;
            var effects=game.Effects;var make=typeof(TrialEffects).GetMethod("Art",Private);
            var update=typeof(TrialEffects).GetMethod("UpdateArt",Private);
            var created=new List<object>();GameObject wall=null;bool ticking=game.enabled;
            object Create(TrialCombatCue cue,Vector3 forward)
            {
                var art=make.Invoke(effects,new object[]{new TrialCombatEvent{cue=cue,position=new Vector3(0,12,0),forward=forward},1.36f,false});
                created.Add(art);return art;
            }
            void At(object art,float age){Set(art,"age",age);update.Invoke(effects,new[]{art});}
            try
            {
                game.enabled=false;
                foreach(var cue in new[]{TrialCombatCue.MoonBlade,TrialCombatCue.CrimsonBlade})
                foreach(float fps in new[]{30f,60f,144f})
                {
                    Vector3 forward=Quaternion.Euler(0,fps,0)*Vector3.forward;
                    var art=Create(cue,forward);
                    for(float age=0;age<1.1f;age+=1/fps)
                    {
                        At(art,age);var mesh=Field<Mesh>(art,"mesh");var vertices=mesh.vertices;
                        Check(vertices.All(Finite) && Field<Mesh>(art,"wakeMesh").vertices.All(Finite),cue+" finite vertices at "+fps+" FPS / "+age.ToString("F3"));
                        Vector3 centre=new Vector3(0,12,0)+forward*TrialSwordFlight.Speed(cue)*age;
                        var radii=Enumerable.Range(0,65).Select(i=>(vertices[i*2+1]-centre).magnitude).ToArray();
                        if(radii.Max()-radii.Min()>.0001f)throw new Exception("Leading edge is not circular");
                        var bounds=mesh.bounds;bounds.Expand(.001f);
                        if(!vertices.All(v=>bounds.Contains(v)))throw new Exception("Mesh bounds exclude a vertex");
                    }
                    At(art,.4f); // Explicitly inspect history at a populated age.
                    var wake=Field<Mesh>(art,"wakeMesh");var history=wake.vertices;
                    float length=history.Max(v=>Vector3.Dot(v,forward))-history.Min(v=>Vector3.Dot(v,forward));
                    Check(length>4,"Flight wake spans more than four metres: "+cue+" / "+fps+" FPS");
                    var colors=Field<Mesh>(art,"mesh").colors;
                    Check(colors[64].r<.01f && colors[64].a>.9f,"Crescent has a visible near-black body");
                    var material=Field<LineRenderer>(art,"core").sharedMaterial;
                    Color emission=material.GetColor("_BaseColor");
                    Check(emission.r>emission.g*20,"Leading edge emits scarlet");
                    At(art,1.12f);Check(Field<Transform>(art,"root").gameObject.activeSelf,"Wake survives the flight lifetime");
                    At(art,1.37f);Check(!Field<Transform>(art,"root").gameObject.activeSelf,"Wake expires within its bounded lifetime");
                }
                var blocked=Create(TrialCombatCue.MoonBlade,Vector3.forward);
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,12,4);
                wall.transform.localScale=new Vector3(8,5,.2f);Physics.SyncTransforms();
                At(blocked,.5f);float stopped=Field<float>(blocked,"waveStopAge");
                Check(stopped>=0 && stopped*TrialSwordFlight.Speed(TrialCombatCue.MoonBlade)<4,"A long frame cannot move the crescent through a wall");
                UnityEngine.Object.DestroyImmediate(wall);wall=null;
                var invalid=Create(TrialCombatCue.MoonBlade,Vector3.forward);
                Field<Vector3[]>(invalid,"vertices")[0]=new Vector3(float.NaN,0,0);
                bool uploaded=(bool)typeof(TrialEffects).GetMethod("UploadSwordMesh",Private).Invoke(effects,new[]{invalid});
                Check(!uploaded && !Field<Transform>(invalid,"root").gameObject.activeSelf,"Invalid vertices are rejected before entering Unity mesh bounds calculation");
                for(int i=0;i<45;i++)
                {
                    var art=Create(i%2==0?TrialCombatCue.MoonBlade:TrialCombatCue.RisingCut,Vector3.forward);
                    Check(Field<MeshRenderer>(art,"wakeSurface")==null || Field<MeshRenderer>(art,"wakeSurface").enabled==(i%2==0),"Pooled wake visibility resets for move "+i);
                }
                Check(errors==0,"No assertions, MinMaxAABB errors or exceptions");
                Directory.CreateDirectory("Logs/Trial");
                File.WriteAllText("Logs/Trial/CrescentValidation.txt","PASS "+checks.Count+"\n"+string.Join("\n",checks));
                Debug.Log("Crescent validation PASS "+checks.Count);
            }
            finally
            {
                if(wall)UnityEngine.Object.DestroyImmediate(wall);
                foreach(var art in created)Field<Transform>(art,"root").gameObject.SetActive(false);
                game.enabled=ticking;Application.logMessageReceived-=OnLog;
            }
        }
    }
}
