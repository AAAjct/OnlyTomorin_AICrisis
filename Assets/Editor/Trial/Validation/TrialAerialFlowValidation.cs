using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    // Observe actual LateUpdate poses and frame-to-frame displacement, not just route IDs.
    public static class TrialAerialFlowValidation
    {
        public static string Status {get;private set;}="Not run";
        static IEnumerator routine;
        static readonly List<string> results=new List<string>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string message){if(!value)throw new Exception(message);results.Add(message);}
        [MenuItem("Tools/Tomorin/Verify rendered aerial landing flow (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Open an offline trial in Play Mode first.");
            results.Clear();Status="Running";routine=Frames();EditorApplication.update+=Step;
        }
        static void Step()
        {
            try{if(routine.MoveNext())return;Status="PASS "+results.Count;Finish();}
            catch(Exception e){Status="FAIL "+e;Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Step;(routine as IDisposable)?.Dispose();routine=null;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/AerialFlowValidation.txt",Status+"\n"+string.Join("\n",results));
        }
        static IEnumerator Frames()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();
            Check(game && game.Ready && !TrialNetworkManager.SessionActive,"Offline trial initialized");
            var p=game.Player;bool gameEnabled=game.enabled;float scale=Time.timeScale;
            game.StartTrial();if(p.ChoosingBoon)p.ChooseBoon(p.OfferA);game.enabled=false;Time.timeScale=1;
            var input=game.gameObject.AddComponent<TrialInput>();
            var power=UnityEngine.Object.FindAnyObjectByType<TrialPowerBudget>();bool powerEnabled=power && power.enabled;
            if(power)power.enabled=false;
            int cap=Application.targetFrameRate,sync=QualitySettings.vSyncCount;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            var enemies=new List<Vector3>();foreach(var e in game.Enemies){enemies.Add(e.transform.position);e.Teleport(new Vector3(35,0,40));}
            var attack=typeof(TrialPlayer).GetMethod("StartAttack",Private);
            var emitted=typeof(TrialPlayer).GetField("comboBladeEmissions",Private);
            var landed=typeof(TrialPlayer).GetField("swordLanded",Private);
            var rig=p.GetComponent<TrialRig>();
            var torso=(Transform)typeof(TrialRig).GetField("spine",Private).GetValue(rig);
            try
            {
                for(int route=0;route<3;route++)
                {
                    p.Restore(new Vector3(0,0,20));Physics.SyncTransforms();
                    attack.Invoke(p,new object[]{false,TrialPlayer.AirStep});
                    float warm=0;int frame=-1;
                    while(warm<.34f)
                    {
                        while(frame==Time.frameCount)yield return null;frame=Time.frameCount;
                        float dt=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.04f);
                        input.ApplyNetwork(new TrialControls());p.Tick(dt,input);warm+=dt;
                    }
                    attack.Invoke(p,new object[]{false,TrialPlayer.AirFan});
                    float age=0,lastBlade=-1,bladeY=0,descent=-1,contact=-1,pursuit=-1;
                    float maxRootAngle=0,maxSwordAngle=0,maxHandStep=0,firstPursuitDistance=0;
                    bool divePressed=false,lightPressed=false;int idle=0,observed=0,edgeFrames=0;
                    var rootPose=torso.localRotation;var swordPose=Quaternion.Inverse(p.transform.rotation)*p.Weapon.rotation;var handPose=p.transform.InverseTransformPoint(rig.RightHand.position);
                    while(age<2.4f)
                    {
                        while(frame==Time.frameCount)yield return null;frame=Time.frameCount;
                        float dt=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.04f);byte key=0;
                        if(route>0 && !divePressed && p.Combo==TrialPlayer.AirFan && p.Progress>=.70f)
                        {key=route==1?(byte)1:(byte)2;divePressed=true;}
                        else if(!lightPressed && (route==0?(bool)landed.GetValue(p):TrialPlayer.IsDive(p.Combo)))
                        {key=1;lightPressed=true;}
                        int previous=p.Combo;bool alreadyPursuing=p.Combo==TrialPlayer.SakuraFollowup;
                        var point=p.transform.position;
                        input.ApplyNetwork(new TrialControls{pressed=key});p.Tick(dt,input);age+=dt;
                        if(p.Combo==TrialPlayer.AirFan && (byte)emitted.GetValue(p)==3 && lastBlade<0){lastBlade=age;bladeY=p.transform.position.y;}
                        if(lastBlade>=0 && descent<0 && p.transform.position.y<bladeY-.02f)descent=age-lastBlade;
                        if((bool)landed.GetValue(p) && contact<0)contact=age;
                        if(p.Combo==TrialPlayer.SakuraFollowup && pursuit<0)pursuit=age;
                        if(route>0 && contact>=0 && pursuit<0 && p.Action==FighterAction.Free)idle++;
                        if(alreadyPursuing && firstPursuitDistance==0)firstPursuitDistance=Vector3.ProjectOnPlane(p.transform.position-point,Vector3.up).magnitude;
                        if(previous!=p.Combo)edgeFrames=4;
                        int before=Time.frameCount;while(before==Time.frameCount)yield return null;
                        var rootNow=torso.localRotation;var swordNow=Quaternion.Inverse(p.transform.rotation)*p.Weapon.rotation;var handNow=p.transform.InverseTransformPoint(rig.RightHand.position);
                        if(edgeFrames-->0){maxRootAngle=Mathf.Max(maxRootAngle,Quaternion.Angle(rootPose,rootNow));maxSwordAngle=Mathf.Max(maxSwordAngle,Quaternion.Angle(swordPose,swordNow));maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(handPose,handNow));}
                        rootPose=rootNow;swordPose=swordNow;handPose=handNow;observed++;
                        if(Vector3.Distance(p.Weapon.position,rig.RightHand.position)>.01f)throw new Exception("Sword detached from rendered hand");
                        if(p.Combo==TrialPlayer.SakuraFollowup && p.Progress>.55f)break;
                    }
                    Check(observed>20 && lastBlade>=0,"Route "+route+": all three blades observed in real rendered frames");
                    Check(descent>=0 && descent<=.10f,"Route "+route+": descent follows final blade in "+descent.ToString("0.000")+" s");
                    Check(contact>=0 && pursuit>=contact && pursuit-contact<=.08f,"Route "+route+": landing-to-sakura gap "+(pursuit-contact).ToString("0.000")+" s");
                    Check(idle==0,"Route "+route+": zero idle frames between landing and buffered sakura");
                    Check(firstPursuitDistance>.002f,"Route "+route+": first sakura tick moves "+firstPursuitDistance.ToString("0.000")+" m");
                    Check(maxRootAngle<45 && maxSwordAngle<90 && maxHandStep<.55f,"Route "+route+": pose boundary max torso "+maxRootAngle.ToString("0.0")+" deg / sword "+maxSwordAngle.ToString("0.0")+" deg / hand "+maxHandStep.ToString("0.000")+" m");
                    Check((int)typeof(TrialPlayer).GetField("swordEmissions",Private).GetValue(p)==1,"Route "+route+": exactly one sakura scatter");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(input);p.Restore(new Vector3(0,0,20));
                for(int i=0;i<enemies.Count;i++)if(game.Enemies[i])game.Enemies[i].Teleport(enemies[i]);
                game.enabled=gameEnabled;if(power)power.enabled=powerEnabled;Application.targetFrameRate=cap;QualitySettings.vSyncCount=sync;Time.timeScale=scale;
            }
        }
    }
}
