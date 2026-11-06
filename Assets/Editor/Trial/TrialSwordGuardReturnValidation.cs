using System;
using Mirror;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static partial class TrialComboFlowValidation
    {
        static void SwordGuardReturnChecks()
        {
            var foe=game.Enemies[game.Enemies.Count-1];
            foreach(float fps in new[]{30f,60f,144f})
            {
                step=1/fps;Reset();Vector3 origin=p.transform.position;
                Move(TrialPlayer.SpiralSword,true);
                Check(p.Guarding && p.GuardAge<.2f,"Throw starts in automatic guard at "+fps+" FPS");
                Advance(.30f);Vector3 blade=p.OrbitSwordPosition;
                Check(p.Guarding && p.SwordRecallReady,"Automatic guard persists while the physical sword flies at "+fps+" FPS");
                foe.ResetFighter();foe.Teleport(p.transform.position+p.transform.forward*2);Physics.SyncTransforms();
                float hp=p.Health;int parries=p.SuccessfulParries;
                p.ReceiveHit(foe,25,15);
                Check(p.Health==hp && p.SuccessfulParries==parries+1 && p.SwordGuardReturning,"Actual perfect guard automatically selects the return slash at "+fps+" FPS");
                Check(Vector3.ProjectOnPlane(p.transform.position-blade,Vector3.up).magnitude<.5f,"Perfect guard instantly teleports to the actual blade at "+fps+" FPS");
                Check(!p.SwordRecallReady && p.Weapon.gameObject.activeSelf && !p.ParryEmpowered,"Counter reclaims the sword and consumes the enhanced choice once at "+fps+" FPS");
                Check(Vector3.Distance(p.SwordReturnOrigin,origin)<.05f,"Counter remembers the throw's ground origin at "+fps+" FPS");
                var target=game.Enemies[0];target.ResetFighter();
                target.ConfigureRunStrength(5,1);target.Teleport(Vector3.Lerp(p.transform.position,origin,.55f));Physics.SyncTransforms();
                float enemyHealth=target.Health;
                Advance(.25f);
                Check(Vector3.ProjectOnPlane(p.transform.position-origin,Vector3.up).magnitude<.15f,"Return slash reaches the recorded origin quickly at "+fps+" FPS");
                Check(target.Health<enemyHealth && !p.CombatFrozen,"Swept return slash hits without interrupting its dash at "+fps+" FPS");
                float after=target.Health;Advance(.03f);
                Check(target.Health==after,"Return slash does not repeatedly damage a swept target at "+fps+" FPS");
                Advance(.15f);Check(!p.SwordGuardReturning,"Return slash has a finite recovery at "+fps+" FPS");
            }
            step=1f/60;
            Reset();Move(TrialPlayer.SpiralSword,true);Advance(.17f);game.Effects.ResumeSwordReturn();
            var flowTarget=game.Enemies[0];flowTarget.ResetFighter();flowTarget.ConfigureRunStrength(5,1);flowTarget.Teleport(p.transform.position+Vector3.forward*2);Physics.SyncTransforms();
            float flowHealth=flowTarget.Health;input.ApplyNetwork(new TrialControls());p.Tick(.15f,input);
            Check(flowTarget.Health<flowHealth && !p.CombatFrozen,"Outgoing thrown-sword contact does not freeze the thrower's movement");
            Reset();Move(TrialPlayer.SpiralSword,true);Advance(.55f);
            foe.ResetFighter();foe.Teleport(p.transform.position+Vector3.forward*2);Physics.SyncTransforms();
            Vector3 before=p.transform.position;int oldParries=p.SuccessfulParries;
            p.ReceiveHit(foe,20,10);
            Check(!p.SwordGuardReturning && p.SuccessfulParries==oldParries && Vector3.Distance(before,p.transform.position)<.01f,"An ordinary late block does not teleport");
            Reset();Move(TrialPlayer.SpiralSword,true);
            int recoveryFrames=0;while(p.Action==FighterAction.Heavy && recoveryFrames++<180)Tick(step);
            Check(p.Action==FighterAction.Free && p.Guarding,"Automatic guard has no gap on the first cast-recovery frame");
            Advance(.07f);
            Check(p.Action==FighterAction.Free && p.Guarding,"Automatic guard survives the cast's recovery while the sword remains airborne");
            Tick(.001f,0,1);foe.Teleport(p.transform.position+p.transform.forward*2);Physics.SyncTransforms();
            p.ReceiveHit(foe,20,10);
            Check(p.SwordGuardReturning,"A fresh manual guard renews the perfect window during sword flight");
            Reset();Move(TrialPlayer.SpiralSword,true);Advance(.30f);
            before=p.transform.position;foe.ResetFighter();foe.Teleport(before-Vector3.forward*2);Physics.SyncTransforms();
            p.ReceiveHit(foe,20,10);
            Check(!p.SwordGuardReturning && Vector3.Distance(before,p.transform.position)<.01f,"A rear hit cannot trigger the perfect-guard teleport");
            Reset();Move(TrialPlayer.SpiralSword,true);Advance(.30f);
            foe.ResetFighter();foe.Teleport(p.transform.position+Vector3.forward*2);Physics.SyncTransforms();
            before=p.transform.position;p.ReceiveHit(foe,20,10,true);
            Check(!p.SwordGuardReturning && Vector3.Distance(before,p.transform.position)<.01f,"An undeflectable attack cannot trigger the counter");
            Reset();Move(TrialPlayer.SpiralSword,true);Advance(.30f);
            before=p.transform.position;Vector3 delta=Vector3.ProjectOnPlane(p.OrbitSwordPosition-before,Vector3.up);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position=before+delta*.5f+Vector3.up*1.5f;wall.transform.rotation=Quaternion.LookRotation(delta.normalized);wall.transform.localScale=new Vector3(8,3,.2f);
                foe.ResetFighter();foe.Teleport(before+Vector3.forward*.8f);Physics.SyncTransforms();p.ReceiveHit(foe,20,10);
                Check(p.SwordGuardReturning && Vector3.ProjectOnPlane(p.transform.position-before,Vector3.up).magnitude<delta.magnitude*.5f,"Perfect-guard teleport stops at world geometry");
            }
            finally{UnityEngine.Object.DestroyImmediate(wall);}
            var state=p.CaptureNetwork(7);
            using(var writer=NetworkWriterPool.Get())
            {writer.Write(state);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))state=reader.Read<TrialActorState>();}
            Check(state.combo==TrialPlayer.SwordGuardReturn && state.swordReturnOrigin==p.SwordReturnOrigin,"Mirror snapshots preserve the automatic return and its origin");
            p.Restore(new Vector3(0,0,20));p.ApplyNetwork(state,.001f);
            Check(Vector3.Distance(p.transform.position,state.position)<.001f,"The first replicated counter snapshot snaps rather than slowly interpolating");
            Reset();Check(!p.Guarding && !p.SwordGuardReturning && !p.SwordRecallReady,"Restore clears automatic throw defence and counter state");
        }
    }
}
