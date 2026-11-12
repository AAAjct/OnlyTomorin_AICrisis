using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TomorinTrial.Editor
{
    public static partial class TrialNetworkValidation
    {
        [MenuItem("Tools/Tomorin/Verify courtyard selection and combat feel (Play Mode)")]
        public static void RunCombatFeel()
        {
            if(!EditorApplication.isPlaying || routine!=null || !UnityEngine.Object.FindAnyObjectByType<InkMainMenu>() || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();runtimeErrors=0;reportName="CombatFeelValidation.txt";Status="Running combat feel";
            Application.logMessageReceived+=CaptureError;routine=CombatFeelChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static void ClickMenu(string field)
        {
            var menu=UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
            ((UnityEngine.UI.Button)typeof(InkMainMenu).GetField(field,Private).GetValue(menu)).onClick.Invoke();
        }
        static IEnumerator CombatFeelChecks()
        {
            Gamepad pad=null;Keyboard keys=null;GameObject cameraWall=null;
            var oldBackground=InputSystem.settings.backgroundBehavior;var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            try
            {
                var menu=UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
                Check(menu.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="单人游玩"),"Main menu labels solo play correctly");
                ClickMenu("solo");yield return .05f;
                var courtyard=(UnityEngine.UI.Button)typeof(InkMainMenu).GetField("courtyardRow",Private).GetValue(menu);
                Check(courtyard.gameObject.activeInHierarchy && courtyard.transform.GetSiblingIndex()==0 && courtyard.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("大肥鱼"),"Solo level list starts with Courtyard and explicitly names its boss");
                typeof(InkMainMenu).GetMethod("CloseLevelList",Private).Invoke(menu,null);
                ClickMenu("multiplayer");yield return .05f;
                var networkLevels=(RectTransform)typeof(InkMainMenu).GetField("networkLevels",Private).GetValue(menu);
                var networkRows=networkLevels.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
                Check(networkRows.gameObject.activeInHierarchy && networkRows.GetChild(0).name==TrialChampionCatalog.LevelName(TrialLevel.Arena) && networkRows.GetChild(1).name==courtyard.name,
                    "The network page embeds a scrollable list with Arena first and Courtyard second");
                ClickMenu("back");
                ClickMenu("solo");courtyard.onClick.Invoke();
                deadline=EditorApplication.timeSinceStartup+40;while(Waiting(UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready==true))yield return .03f;
                var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();var player=game.Player;
                Check(player.MaxStamina==TrialPlayer.BaseStamina && player.Stamina==TrialPlayer.BaseStamina,"Initial stamina uses the increased 420-point base");
                Check(player.ChoosingBoon && !game.GateOpen && game.DefeatedGuards==0 && game.Enemies.All(e=>e.Alive),"Solo Courtyard starts with guards, a sealed boss gate, and a three-choice boon prompt");
                yield return .06f;
                var buttons=(UnityEngine.UI.Button[])typeof(TrialHud).GetField("boonButtons",Private).GetValue(game.Hud);
                buttons[0].onClick.Invoke();
                Check(!player.ChoosingBoon && player.Boon!=TrialBoon.None,"Choosing a visible boon card applies the boon and releases combat input");
                Check(player.MaxStamina==(player.Boon==TrialBoon.Resolve?435:420),"Choosing a run boon preserves the new stamina base and adds Resolve's 15 points");
                Check(game.Enemies.Last().Boss.Boon!=TrialBossBoon.None,"Boss receives one modest random boon for the run");
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                pad=InputSystem.AddDevice<Gamepad>("Combat feel test pad");keys=InputSystem.AddDevice<Keyboard>("Combat feel test keys");
                game.Input.SetDeviceFilter(new InputDevice[]{pad,keys});
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return .12f;
                var hints=(UnityEngine.UI.Text[])typeof(TrialHud).GetField("skillKeys",Private).GetValue(game.Hud);
                Check(game.Input.UsingGamepad && hints[3].text=="B" && hints[1].text=="RB","Using a gamepad immediately switches dodge and heavy-attack UI hints");
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));yield return .12f;
                Check(!game.Input.UsingGamepad && hints[3].text==TrialBindings.Display("Dodge"),"Keyboard activity restores keyboard hints");
                InputSystem.QueueStateEvent(keys,new KeyboardState());yield return .1f;

                player.Restore(new Vector3(0,0,-21));game.CameraRig.Snap();
                Check(player.Stamina==player.MaxStamina && player.MaxStamina>=420,"Restoring a player refills the increased stamina pool");
                cameraWall=GameObject.CreatePrimitive(PrimitiveType.Cube);cameraWall.name="Temporary camera collision test";
                cameraWall.transform.position=player.transform.position+new Vector3(0,1.1f,-.8f);cameraWall.transform.localScale=new Vector3(4,4,.2f);Physics.SyncTransforms();
                game.CameraRig.Snap();yield return .06f;
                Check(player.GetComponentsInChildren<Renderer>().Any(r=>r.forceRenderingOff),"Camera proximity hides only the local model before the near plane enters its body");
                UnityEngine.Object.DestroyImmediate(cameraWall);cameraWall=null;Physics.SyncTransforms();game.CameraRig.Snap();yield return .12f;
                Check(player.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"Local model visibility returns after camera clearance");
                var guardEnemy=game.Enemies[0];guardEnemy.ReceiveHit(player,40,15);
                Check(guardEnemy.Recoil.sqrMagnitude>1 && guardEnemy.Action==FighterAction.Stagger,"A sword hit gives a guard directional recoil and a stagger reaction");
                guardEnemy.ReceiveHit(player,9999,40);
                Check(!guardEnemy.Alive && guardEnemy.MoveBlend==0 && guardEnemy.Recoil.sqrMagnitude>0,"A finishing blow preserves its direction for the accelerated knockdown pose");

                game.StartBossChallenge();player.ChooseBoon(player.OfferA);player.Restore(new Vector3(8,0,19));
                var boss=game.Enemies.Last();var brain=boss.Boss;
                var pot=boss.Visual.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(s=>s.sharedMesh.GetBlendShapeIndex("TiePen")>=0);
                Check(pot,"Replacement DeepSeek model exposes the authored TiePen blend shape");
                brain.ClearBoon();boss.ReceiveHit(player,boss.MaxHealth*.5f,0);yield return 1f;
                Check(brain.Enraged && pot.GetBlendShapeWeight(pot.sharedMesh.GetBlendShapeIndex("TiePen"))>90,"Second phase visibly raises TiePen to equip the iron pot");
                game.enabled=false;player.ResetRunBoon();boss.ResetFighter();brain.ClearBoon();game.SetCombatTarget(player);
                player.Restore(new Vector3(0,0,24));boss.Teleport(new Vector3(0,0,27));boss.ReceiveHit(player,1,0);brain.StartSkill(DeepFishSkill.TailSweep);
                float health=player.Health;
                while(brain.SkillProgress<.39f)brain.Tick(.02f);
                Check(player.Health==health,"A tail sweep does not hit the entire warning fan as soon as its active window opens");
                while(brain.Skill!=DeepFishSkill.None)brain.Tick(.02f);
                Check(player.Health<health,"The visible travelling tail blade hits when it crosses the target angle");
                boss.ResetFighter();player.Restore(new Vector3(0,0,24));boss.Teleport(new Vector3(0,0,27));boss.ReceiveHit(player,1,0);brain.StartSkill(DeepFishSkill.TailSweep);
                player.Restore(new Vector3(0,0,30));health=player.Health;
                while(brain.Skill!=DeepFishSkill.None)brain.Tick(.02f);
                Check(player.Health==health,"The region behind the front sweep remains a safe zone");
                boss.ResetFighter();player.Restore(new Vector3(0,0,24));boss.Teleport(new Vector3(0,0,27));boss.ReceiveHit(player,1,0);brain.StartSkill(DeepFishSkill.ReverseTail);
                player.Restore(new Vector3(8,0,24));while(brain.SkillProgress<.48f)brain.Tick(.02f);
                player.Restore(new Vector3(0,0,24));health=player.Health;while(brain.Skill!=DeepFishSkill.None)brain.Tick(.02f);
                Check(player.Health==health,"The reverse sweep covers the rear sector and leaves the front sector safe");

                boss.ResetFighter();player.Restore(new Vector3(0,0,22));boss.Teleport(new Vector3(0,0,27));player.ToggleLock();game.CameraRig.Snap();
                var input=game.gameObject.AddComponent<TrialInput>();float yaw=game.CameraRig.transform.eulerAngles.y;
                var before=player.transform.position;input.ApplyNetwork(new TrialControls{pressed=4,yaw=yaw});player.Tick(.02f,input);
                input.ApplyNetwork(new TrialControls{yaw=yaw});for(int i=0;i<5;i++)player.Tick(.02f,input);
                var backwards=-(Quaternion.Euler(0,yaw,0)*Vector3.forward);var displacement=player.transform.position-before;
                Check(displacement.magnitude>.8f && Vector3.Dot(displacement.normalized,backwards)>.98f,"A locked no-direction dodge travels backwards along the camera's ground-plane heading");
                health=player.Health;player.ReceiveHit(boss,40,30,true);
                Check(player.Health==health && player.SuccessfulDodges>0,"A timed dodge avoids the incoming boss hit");
                for(int i=0;i<10;i++)player.Tick(.02f,input);
                boss.Teleport(player.transform.position+Vector3.forward*3);float bossHealth=boss.Health;
                input.ApplyNetwork(new TrialControls{pressed=1,yaw=0});player.Tick(.02f,input);input.ApplyNetwork(default);
                Check(player.Action==FighterAction.Light && player.Combo==9,"The next light attack after a perfect dodge becomes the blink horizontal slash");
                for(int i=0;i<36;i++){player.Tick(.02f,input);game.Effects.TickTime();yield return .012f;}
                Check(Mathf.Abs((bossHealth-boss.Health)-42)<.5f,"Blink slash inflicts exactly 1.5 times an unmodified basic hit");
                Check(game.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.transform.parent.name.StartsWith("Blink afterimage")),"Blink slash generates baked silhouette afterimages");
                yield return .2f;
                boss.ResetFighter();brain.ClearBoon();player.Restore(new Vector3(0,0,24));boss.Teleport(new Vector3(0,0,27));boss.ReceiveHit(player,1,0);brain.StartSkill(DeepFishSkill.TidalCharge);
                input.ApplyNetwork(new TrialControls{held=1,yaw=0});player.Tick(0,input);health=player.Health;
                player.ReceiveHit(boss,40,30,true);
                Check(player.Health==health && player.ParryEmpowered && brain.Skill==DeepFishSkill.None,"Perfectly deflecting a charge cancels the boss attack and arms the player's chosen empowered follow-up");
                game.Effects.TickTime();Check(Time.timeScale<.5f,"Perfect guard triggers a short impact beat");
                Check(player.CombatFrozen && boss.CombatFrozen,"The blade clash applies a brief pose hold to both combatants");
                UnityEngine.Object.Destroy(input);Time.timeScale=1;game.enabled=true;game.ReturnToTitle();yield return .3f;
                Check(runtimeErrors==0,"Menu, boons, new model, camera, device hints and combat checks produce no runtime errors");
            }
            finally
            {
                if(cameraWall)UnityEngine.Object.DestroyImmediate(cameraWall);
                if(pad!=null)InputSystem.RemoveDevice(pad);if(keys!=null)InputSystem.RemoveDevice(keys);
                InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
                var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();if(game)game.enabled=true;
                Time.timeScale=1;
            }
        }
    }
}
