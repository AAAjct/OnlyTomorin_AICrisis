using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TomorinTrial.Editor
{
    // Runs real frame/input/physics integration checks in the open scene, without a test package.
    public static partial class TrialValidation
    {
        public static string Status { get; private set; } = "Not run";
        static IEnumerator routine;
        static double resumeAt;
        static Keyboard keyboard;
        static InputSettings.BackgroundBehavior previousBackgroundBehavior;
        static InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
        static readonly List<string> passed=new List<string>();

        [MenuItem("Tools/Tomorin/Run gameplay smoke checks (Play Mode)")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Start AshenCourt in Play Mode first.");
            if(routine!=null)throw new InvalidOperationException("Validation is already running.");
            passed.Clear();Status="Running";
            previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;previousEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("Trial validation keyboard");
            routine=Checks();resumeAt=0;EditorApplication.update+=Step;
        }

        static void Step()
        {
            if(EditorApplication.timeSinceStartup<resumeAt)return;
            try
            {
                if(!routine.MoveNext()){Finish(null);return;}
                resumeAt=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:0);
            }
            catch(Exception e){Finish(e);}
        }

        static void Check(bool condition,string label)
        {if(!condition)throw new Exception(label);passed.Add(label);}
        static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
        static IEnumerator Checks(bool returnToMenu=true)
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            Check(game!=null && game.Ready,"Scene bootstrap, Tomorin reference and four enemies");
            Check(game.Enemies.Count==4,"Three guards and one boss instantiated");
            game.Input.SetDeviceFilter(new InputDevice[]{keyboard});game.RestartTrial();game.Player.ChooseBoon(game.Player.OfferA);
            int frame=Time.frameCount;yield return .15f;Check(Time.frameCount>frame+2,"Player loop advances in Play Mode");
            var origin=game.Player.transform.position;Keys(Key.W);yield return .70f;Keys();yield return .08f;
            Check(game.Player.transform.position.z-origin.z>1.7f,$"WASD moves Tomorin through the Input System (delta={game.Player.transform.position-origin}, mode={game.Mode})");
            Check(game.Player.transform.position.y>-.2f && game.Player.transform.position.y<.2f,"CharacterController stays on the stone floor");
            var enemy=game.Enemies[0];
            enemy.Teleport(game.Player.transform.position+game.Player.transform.forward*2.0f);enemy.Parried();
            float hp=enemy.Health;Keys(Key.J);yield return .06f;Keys();yield return .65f;
            Check(enemy.Health<hp,"Sword light attack has a working contact window");
            Check(game.transform.position.sqrMagnitude<.0001f,"Playing positional audio does not move the scene root");
            enemy.ResetFighter();enemy.Teleport(game.Player.transform.position+game.Player.transform.forward*2);enemy.Parried();
            hp=enemy.Health;yield return .75f;Keys(Key.Q);yield return .06f;Keys();yield return 1.12f;
            Check(enemy.Health<hp-45,$"Heavy attack consumes stamina and deals more damage (before={hp}, after={enemy.Health}, stamina={game.Player.Stamina}, action={game.Player.Action}, distance={Vector3.Distance(enemy.transform.position,game.Player.transform.position)})");
            enemy.ResetFighter();enemy.Teleport(new Vector3(11,0,35));game.Player.Teleport(new Vector3(0,0,-21));
            yield return 1.2f;Keys(Key.Space);yield return .15f;Keys();
            hp=game.Player.Health;game.Player.ReceiveHit(null,40,10,true);
            Check(Mathf.Approximately(hp,game.Player.Health),"Dodge invulnerability rejects an incoming hit");
            yield return .65f;enemy.ResetFighter();enemy.Teleport(game.Player.transform.position+game.Player.transform.forward*2);enemy.Parried();
            Keys(Key.K);yield return .08f;hp=game.Player.Health;int parries=game.Player.SuccessfulParries;
            game.Player.ReceiveHit(enemy,30,20);
            Check(game.Player.SuccessfulParries==parries+1 && game.Player.Health==hp && enemy.Vulnerable,"Timed guard parries and exposes the attacker");
            for(int i=0;i<80 && game.Player.GuardAge<.24f;i++)yield return .02f;
            game.Player.ReceiveHit(enemy,30,20);
            Check(game.Player.Health<hp && game.Player.Health>hp-10,"Held guard reduces damage after the parry window");
            Keys();yield return .1f;enemy.ResetFighter();enemy.Teleport(new Vector3(11,0,35));
            game.Player.ReceiveHit(null,65,10,true);yield return .6f;
            hp=game.Player.Health;int flasks=game.Player.Flasks;Keys(Key.R);yield return .06f;Keys();yield return 1.4f;
            Check(game.Player.Health>hp && game.Player.Flasks==flasks-1,"Drinking spirit dew restores health and spends one charge");
            game.Player.Teleport(game.World.ShrinePosition+Vector3.right);yield return .1f;Keys(Key.E);yield return .08f;Keys();yield return .20f;
            Check(game.Player.Flasks==3 && game.Player.Health==game.Player.MaxHealth && game.Checkpoint.x< -3,"Incense rest restores resources and sets the checkpoint");
            enemy.Teleport(game.Player.transform.position+game.Player.transform.forward*4);Keys(Key.Tab);yield return .08f;Keys();yield return .1f;
            Check(game.Player.Locked && game.Player.Target==enemy,"Target lock selects a living nearby enemy");
            game.Player.ReceiveHit(null,9999,0,true);yield return .15f;
            Check(game.Mode==TrialMode.Dead,"Lethal damage enters the death screen");
            game.Respawn();yield return .20f;
            Check(game.Player.Alive && Vector3.Distance(game.Player.transform.position,game.Checkpoint)<.2f && game.Enemies[0].Alive,"Respawn returns to incense and resets enemies");
            for(int i=0;i<3;i++)game.Enemies[i].ReceiveHit(game.Player,10000,100,true);
            Check(game.GateOpen && !game.World.Gate.activeSelf && game.DefeatedGuards==3,"Defeating three guards opens the boss seal");
            var boss=game.Enemies[3];boss.ReceiveHit(game.Player,boss.MaxHealth*.5f,10);
            Check(boss.PhaseTwo,"Boss switches to the second phase below 55% health");
            boss.ReceiveHit(game.Player,10000,100,true);yield return .1f;
            Check(game.Mode==TrialMode.Victory,"Defeating the boss completes the trial");
            game.RestartTrial();game.Player.ChooseBoon(game.Player.OfferA);Keys();yield return .12f;Keys(Key.Escape);yield return .12f;Keys();yield return .1f;
            Check(game.Mode==TrialMode.Paused && Time.timeScale==0,"Escape pauses gameplay and time");
            Keys(Key.Escape);yield return .12f;Keys();yield return .1f;
            Check(game.Mode==TrialMode.Playing && Time.timeScale>0,"Escape resumes gameplay");
            if(returnToMenu)game.ReturnToTitle();
        }

        static void Finish(Exception error)
        {
            EditorApplication.update-=Step;routine=null;
            if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();if(game && game.Ready)game.Input.SetDeviceFilter(null);
            InputSystem.settings.backgroundBehavior=previousBackgroundBehavior;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorBehavior;
            Status=error==null?"Passed "+passed.Count+" checks":"FAILED after "+passed.Count+": "+error.Message;
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/Validation.txt",DateTime.UtcNow.ToString("O")+"\n"+Status+"\n"+string.Join("\n",passed)+(error==null?"":"\n"+error));
            if(error==null)Debug.Log("[Tomorin validation] "+Status);else Debug.LogError("[Tomorin validation] "+Status);
        }

        // Offscreen camera captures normally omit overlay canvases. Move only our canvas to the
        // camera for this render and restore it immediately, preserving the actual game UI mode.
        public static void Capture(string path)
        {
            var camera=Camera.main;var hud=UnityEngine.Object.FindFirstObjectByType<TrialHud>();var canvas=hud.GetComponentInChildren<Canvas>();
            var previousMode=canvas.renderMode;var previousCamera=canvas.worldCamera;float previousPlane=canvas.planeDistance;
            var rt=new RenderTexture(1600,900,24);var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;Canvas.ForceUpdateCanvases();
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;canvas.planeDistance=previousPlane;
                camera.targetTexture=previousTarget;RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
