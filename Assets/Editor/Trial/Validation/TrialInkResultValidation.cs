using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static class TrialInkResultValidation
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}

        [MenuItem("Tools/Tomorin/Verify ink verdict artwork and split")]
        public static void Run()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();
            Require(EditorApplication.isPlaying && game && game.Ready,"Open a trial in Play Mode first.");
            var hud=game.Hud;bool enabled=hud.enabled;bool gameEnabled=game.enabled;
            bool reduced=InkMainMenu.ReducedMotion;float timeScale=Time.timeScale;
            var render=hud.GetType().GetMethod("RenderInkResult",Private);
            var resultClock=typeof(TrialGame).GetField("resultStartedAt",Private);
            var halves=Field<RawImage[]>(hud,"resultHalves");
            var materials=Field<Material[]>(hud,"resultInkMaterials");
            string folder="Logs/Trial/InkVerdict";Directory.CreateDirectory(folder);
            try
            {
                game.StartTrial();game.Player.ResetRunBoon();
                typeof(TrialHud).GetMethod("Update",Private).Invoke(hud,null);
                game.enabled=false;hud.enabled=false;Time.timeScale=0;
                InkMainMenu.SetReducedMotion(false);
                Require(materials.All(m=>m && m.shader.isSupported),"Ink shaders must be supported.");
                var verdict=Field<CanvasGroup>(hud,"resultInk").transform;
                Require(!verdict.Find("Diffusing paper light") && !verdict.Find("Vermilion seal"),"Verdict must display only glyph effects, without a paper background or seal caption.");
                Require(!ShaderUtil.GetShaderMessages(materials[0].shader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"Ink shader has compile errors.");
                foreach(bool victory in new[]{true,false})
                {
                    game.Respawn();game.Player.ResetRunBoon();
                    typeof(TrialHud).GetMethod("Update",Private).Invoke(hud,null);
                    if(victory)foreach(var enemy in game.Enemies.ToArray())enemy.ReceiveHit(game.Player,100000,0,true);
                    else game.Player.ReceiveHit(null,100000,0,true);
                    typeof(TrialHud).GetMethod("Update",Private).Invoke(hud,null);
                    Require(Time.timeScale==.5f && !verdict.gameObject.activeSelf,"Slow motion starts before the glyph appears.");
                    resultClock.SetValue(game,Time.unscaledTime-.5f);
                    typeof(TrialHud).GetMethod("Update",Private).Invoke(hud,null);
                    Time.timeScale=0;
                    Require(Field<Text>(hud,"resultCharacter").text==(victory?"斩":"菜"),"Actual victory/death must select the correct calligraphy.");
                    Require(halves.All(h=>h.enabled && h.texture),"Both calligraphy textures must load.");
                    Require(!Field<Text>(hud,"resultCharacter").enabled,"The generated brushwork must replace the fallback font.");
                    Require(!Field<CanvasGroup>(hud,"resultInk").blocksRaycasts,"Verdict must allow retry clicks.");
                    foreach(float age in new[]{.24f,.60f,1.18f,2.35f})
                    {
                        render.Invoke(hud,new object[]{age});
                        if(age==.60f)Require(halves.All(h=>h.rectTransform.anchoredPosition.sqrMagnitude==0),"Ink must remain whole before the cut.");
                        if(age>1)Require(halves[0].rectTransform.anchoredPosition.x<0 && halves[1].rectTransform.anchoredPosition.x>0,"Cut must separate real glyph halves.");
                        if(age==.24f || age==2.35f)
                        {
                            bool red=victory==(age==.24f);
                            Require(materials.Take(2).All(m=>Mathf.Approximately(m.GetFloat("_Red"),red?1:0)),"Both halves must use the outcome's correct starting and ending colors.");
                            Require((Field<Text>(hud,"resultCharacter").color.r>.5f)==red,"Fallback text must follow the same color direction.");
                        }
                        Capture(hud,game.CameraRig.View,folder+"/"+(victory?"victory":"defeat")+"-"+age.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png");
                    }
                }
                InkMainMenu.SetReducedMotion(true);render.Invoke(hud,new object[]{2.35f});
                Require(Mathf.Abs(halves[0].rectTransform.anchoredPosition.x)<=4.01f && Quaternion.Angle(halves[0].rectTransform.localRotation,Quaternion.identity)<.01f,"Reduced motion must avoid large displacement and rotation.");
                hud.ShowInkResult(true);
                typeof(TrialGame).GetProperty("Mode").SetValue(game,TrialMode.Victory);
                typeof(TrialHud).GetField("resultMode",Private).SetValue(hud,TrialMode.Victory);
                typeof(TrialHud).GetField("resultStarted",Private).SetValue(hud,Time.unscaledTime-.47f);
                typeof(TrialHud).GetMethod("UpdateInkResult",Private).Invoke(hud,null);
                Require(materials[0].GetFloat("_Reveal")>.99f,"The faster reveal completes even while timeScale is zero.");
                typeof(TrialHud).GetField("resultStarted",Private).SetValue(hud,Time.unscaledTime-2.65f);
                typeof(TrialHud).GetMethod("UpdateInkResult",Private).Invoke(hud,null);
                Require(!Field<CanvasGroup>(hud,"resultInk").gameObject.activeSelf,"Verdict must hide after fading out.");
                File.WriteAllText(folder+"/Validation.txt","PASS: slow-motion lead-in; both artwork resources; supported shader; whole ink before cut; independent halves; red-to-black victory; black-to-red defeat; reduced motion; zero-timescale playback; fade-out; click-through. Eight rendered frames exported.");
                Debug.Log("Ink verdict validation PASS; captures in "+folder);
            }
            finally
            {
                InkMainMenu.SetReducedMotion(reduced);Time.timeScale=timeScale;
                hud.enabled=enabled;game.enabled=gameEnabled;game.ReturnToTitle();
            }
        }

        static void Capture(TrialHud hud,Camera camera,string path)
        {
            var canvas=hud.GetComponent<Canvas>();if(!canvas)canvas=hud.GetComponentInChildren<Canvas>();
            var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;
                Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                Require(texture.GetPixels32().Count(p=>p.r>180 && p.b>160 && p.g<65)<12,"Rendered frame must have no shader-error magenta.");
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=target;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
