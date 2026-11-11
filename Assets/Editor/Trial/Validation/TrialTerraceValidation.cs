using System;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialTerraceValidation
    {
        const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly MethodInfo ClearHold=typeof(TrialFighter).GetMethod("ClearCombatHold",All);
        static readonly FieldInfo Cooldown=typeof(TrialChampion).GetField("cooldown",All);

        [MenuItem("Tools/Tomorin/Validate Crimson Sword Terrace (Play Mode)")]
        public static string Run()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
            if(!EditorApplication.isPlaying || !game || !game.Ready || game.Level!=TrialLevel.Anon || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Run CrimsonSwordTerrace in standalone Play Mode.");
            var results=new List<string>();
            void Check(bool ok,string label)
            {
                if(!ok)throw new InvalidOperationException(label);
                results.Add("PASS "+label);
            }

            var player=game.Player;
            var boss=game.Enemies[0];
            var brain=boss.Champion;
            bool wasEnabled=game.enabled;
            game.enabled=false;
            var input=game.gameObject.AddComponent<TrialInput>();
            input.enabled=false;
            game.StartTrial();
            player.ResetRunBoon();
            void Step(float dt=.01f)
            {
                ClearHold.Invoke(boss,null);
                ClearHold.Invoke(player,null);
                boss.Tick(dt);
            }
            void Advance(float time){for(int i=0;i<Mathf.RoundToInt(time*100);i++)Step();}
            void Until(Func<bool> condition,string label)
            {
                for(int i=0;i<400 && !condition();i++)Step();
                Check(condition(),label);
            }
            void Guard(float age)
            {
                player.transform.rotation=Quaternion.LookRotation(TrialChampion.Planar(boss.transform.position-player.transform.position));
                typeof(TrialFighter).GetProperty("Guarding",All).SetValue(player,true);
                typeof(TrialPlayer).GetProperty("GuardAge",All).SetValue(player,age);
            }
            void Enter()
            {
                game.RestartTrial();
                player.ResetRunBoon();
                player.Teleport(new Vector3(0,0,TrialWorld.TerraceEntryZ+.01f));
                Physics.SyncTransforms();
                Step();
            }
            void PlaceDuel(Vector3 origin,Vector3 direction,float distance)
            {
                boss.Teleport(origin);
                boss.transform.rotation=Quaternion.LookRotation(direction);
                player.Teleport(origin+direction*distance);
                Physics.SyncTransforms();
            }
            void PrepareNeutral(float distance)
            {
                Enter();
                brain.CancelAttack();
                PlaceDuel(TrialWorld.TerraceCenter,Vector3.forward,distance);
            }
            void ObserveAction(FighterAction action)
                =>typeof(TrialFighter).GetProperty("Action",All).SetValue(player,action);
            void Decide(string label)
            {
                Cooldown.SetValue(brain,0f);
                Until(()=>brain.Attacking,label);
            }

            try
            {
                game.RestartTrial();
                player.ResetRunBoon();
                Check(brain.TerraceDuel && brain.Rig.WeaponRoot && !brain.Rig.LeftWeapon,"The dedicated scene runs the single-sword encounter");
                Check(Mathf.Approximately(boss.MaxHealth,1800*TrialDifficultySettings.HealthScale(game.Difficulty)),
                    "The solo terrace duel uses the expanded health pool");
                Check(Vector3.Distance(player.transform.position,TrialWorld.TerraceCheckpoint)<.03f,"Retry spawns south of the opening line");
                Step();Check(!boss.Awakened && !brain.Attacking,"South staging strip leaves the player in control");
                Check(!boss.ReceiveHit(player,100,50),"The encounter cannot be attacked before entering");
                for(int i=0;i<16;i++)
                {
                    Vector3 radial=Quaternion.Euler(0,i*22.5f,0)*Vector3.forward;
                    Check(Physics.Raycast(TrialWorld.TerraceCenter+radial*11+Vector3.up*.25f,radial,out var rail,3,1) &&
                        Mathf.Abs(TrialWorld.TerraceWallDistance(rail.point))<.15f,"Continuous rail at sector "+i);
                }

                Enter();
                Until(()=>brain.Attacking,"Crossing the entry line starts a visible attack");
                Check(brain.MoveIndex==0 && !brain.Introducing,"The first attack is the grounded dash");
                Advance(.28f);Vector3 locked=brain.Heading;
                player.Teleport(new Vector3(4,0,19));
                Advance(.39f);
                Check(Vector3.Angle(locked,brain.Heading)<.1f && Mathf.Abs(boss.transform.position.x)<.05f,
                    "Dash travel and aim do not bend after the lock");
                Advance(.18f);
                Check(brain.Recovering && brain.ActiveHazards==0 && player.Health==player.MaxHealth,
                    "A committed lateral exit avoids the dash and its follow-up");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,26.1f));
                brain.BeginMove(1);Advance(.40f);
                Check(Mathf.Abs(boss.transform.position.x)>.24f && brain.WindingUp,
                    "The close opener sidesteps even at point-blank range");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,26.8f));
                brain.BeginMove(1);Advance(.06f);
                float interruptedAge=brain.Age;
                typeof(TrialFighter).GetProperty("Action",All).SetValue(player,FighterAction.Light);
                Check(boss.ReceiveHit(player,34,26) && boss.Flinching && !boss.Vulnerable,
                    "A light hit before commitment causes brief hitstun without a posture break");
                Advance(.09f);
                Check(brain.Age==interruptedAge && boss.transform.position.z<TrialWorld.TerraceCenter.z-.06f,
                    "Hitstun pauses the attack clock and moves the boss away from the blow");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,26.8f));
                brain.BeginMove(1);Advance(.04f);
                typeof(TrialFighter).GetProperty("Action",All).SetValue(player,FighterAction.Light);
                for(int i=0;i<3;i++)boss.ReceiveHit(player,25,20);
                Check(brain.MoveIndex==5 && boss.Guarding,"Repeated close light strikes draw a readable blade guard");
                Advance(.32f);
                float bracedHealth=boss.Health;
                Check(!boss.ReceiveHit(player,25,20) && boss.Health==bracedHealth &&
                    brain.MoveIndex==6 && player.Action==FighterAction.Stagger,
                    "Another light attack is turned aside into a traveling riposte");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,26.8f));
                brain.BeginMove(5);Advance(.18f);
                typeof(TrialFighter).GetProperty("Action",All).SetValue(player,FighterAction.Heavy);
                Check(boss.ReceiveHit(player,72,22) && !brain.Attacking && boss.Flinching,
                    "A heavy strike breaks the blade guard and interrupts the boss");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,26.8f));
                player.transform.rotation=Quaternion.identity;
                brain.BeginMove(2);Advance(.20f);
                int inputParries=player.SuccessfulParries;
                input.ApplyNetwork(new TrialControls{pressed=TrialControls.GuardPress|1});
                player.Tick(.01f,input);
                input.ApplyNetwork(default);
                for(int i=0;i<16;i++){player.Tick(.01f,input);Step();}
                Check(player.SuccessfulParries==inputParries+1 && player.Health==player.MaxHealth,
                    "A one-frame guard tap wins over a simultaneous light input and deflects the boss");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,30));
                brain.BeginMove(7);Advance(.65f);
                Check(player.Health<player.MaxHealth && player.Action==FighterAction.Stagger,"The chase thrust connects once");
                float thrustHealth=player.Health;
                float contactZ=player.transform.position.z, bossContactZ=boss.transform.position.z;
                Advance(.10f);
                float midPushZ=player.transform.position.z;
                Advance(.16f);
                Check(midPushZ-contactZ>.35f && player.transform.position.z-midPushZ>.55f,
                    "The thrust pushes the defender across successive contact frames");
                Check(Mathf.Abs(boss.transform.position.z-bossContactZ-(player.transform.position.z-contactZ))<.12f &&
                    player.Health==thrustHealth,"The attacker follows the push without dealing damage again");

                Enter();boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,30));
                brain.BeginMove(7);Guard(0);Advance(.65f);
                contactZ=player.transform.position.z;
                Advance(.25f);
                Check(player.Health==player.MaxHealth && Mathf.Abs(player.transform.position.z-contactZ)<.05f,
                    "A precision deflect prevents the sustained thrust push");

                Enter();boss.Teleport(new Vector3(10.3f,0,25));player.Teleport(new Vector3(12.3f,0,25));
                brain.BeginMove(7);Advance(.94f);
                Check(player.Health<player.MaxHealth && player.transform.position.x>12.4f &&
                    TrialWorld.TerraceWallDistance(player.transform.position)>=.37f,
                    "A thrust can pin against the rail without pushing through it");

                Enter();
                int parries=player.SuccessfulParries;
                for(int i=1;i<=3;i++)
                {
                    for(int tick=0;tick<210 && player.SuccessfulParries<parries+i;tick++){Guard(0);Step();}
                    Check(player.SuccessfulParries==parries+i,"Precision contact "+i+" registers once");
                    if(i<3)Check(!boss.Vulnerable && boss.Posture==18*i,"Early contact keeps the next attack and accumulates posture");
                }
                Check(boss.Vulnerable && Mathf.Abs(boss.ActionDuration-1.65f)<.01f && !brain.Attacking,
                    "Three contacts guarantee a full stagger with no residual attacks");
                Check(player.Health==player.MaxHealth && player.Stamina==player.MaxStamina,"Perfect defense costs no life or stamina");
                ClearHold.Invoke(player,null);
                player.Teleport(boss.transform.position-brain.Heading*1.7f);
                player.transform.rotation=Quaternion.LookRotation(brain.Heading);
                input.ApplyNetwork(new TrialControls{pressed=2});
                player.Tick(.01f,input);
                Check(player.Action==FighterAction.Heavy && player.Combo==14,"Heavy input after a posture break starts the execution");
                input.ApplyNetwork(default);

                Enter();player.Teleport(new Vector3(0,0,25));brain.BeginMove(2);Guard(.5f);
                float stamina=player.Stamina;
                Advance(.35f);
                Check(player.Health==player.MaxHealth && Mathf.Abs(stamina-player.Stamina-player.MaxStamina*.18f)<.01f,
                    "Ordinary guard absorbs the hit and pays 18 percent stamina");

                Enter();player.Teleport(new Vector3(0,0,25));brain.BeginMove(8);Guard(0);Advance(.77f);
                Check(player.Health<player.MaxHealth && player.Action==FighterAction.Stagger && player.ActionDuration>=.60f,
                    "Sweep bypasses precision guard and knocks down once");

                Enter();player.Teleport(new Vector3(0,0,25));brain.BeginMove(1);
                Advance(.45f);
                Check(player.Action==FighterAction.Stagger && player.ActionDuration<=.23f && player.Health<player.MaxHealth,
                    "A close hit has short, recoverable stun");
                ClearHold.Invoke(player,null);
                input.ApplyNetwork(new TrialControls{held=1});
                player.Tick(.24f,input);
                player.Tick(.01f,input);
                Check(player.Action==FighterAction.Free && player.Guarding,"Held guard is accepted before the next cut");
                typeof(TrialPlayer).GetField("hurtUntil",All).SetValue(player,0f);
                float hp=player.Health;
                for(int i=0;i<100 && !brain.Recovering;i++){Guard(.4f);Step();}
                Check(brain.Recovering && player.Health==hp,"Holding guard after the first hit completes the round without a second hit");

                Enter();player.Teleport(new Vector3(0,0,25));brain.BeginMove(1);
                Advance(.40f);
                player.Teleport(boss.transform.position+Quaternion.AngleAxis(62,Vector3.up)*brain.Heading*2.5f);
                Until(()=>brain.MoveIndex==4,"One side cut answers a lateral exit within its turn budget");
                Advance(.25f);
                player.Teleport(boss.transform.position-brain.Heading*2.4f);
                Until(()=>brain.Recovering,"Passing behind the locked side cut ends the round");
                Check(player.Health==player.MaxHealth,"Neither locked attack follows the player around the boss");

                Enter();boss.Teleport(new Vector3(10.3f,0,25));player.Teleport(new Vector3(12.5f,0,25));
                brain.BeginMove(2);Advance(.48f);
                Check(brain.Recovering,"A wall-side finish retains its recovery");
                Until(()=>!brain.Attacking,"Wall-side recovery ends without an additional attack");
                var nearWall=boss.transform.position;
                Advance(.30f);
                Check(!brain.Attacking && Mathf.Abs(boss.transform.position.z-nearWall.z)>.9f,
                    "The boss moves along the rail and leaves a route back inside");

                Enter();player.Teleport(new Vector3(0,0,25));brain.BeginMove(9);Advance(.23f);
                typeof(TrialFighter).GetProperty("Action",All).SetValue(player,FighterAction.Heavy);
                Check(boss.ReceiveHit(player,72,22) && boss.Vulnerable && !brain.Attacking,
                    "A heavy hit interrupts the exposed charge");

                Enter();player.Teleport(new Vector3(0,0,25));brain.BeginMove(2);
                boss.ReceiveHit(player,boss.MaxHealth*.5f,0);
                Until(()=>brain.Recovering,"Reaching half health preserves the current recovery");
                Check(!brain.TerraceChangingPhase,"Phase transition waits out the punish window");
                Until(()=>brain.TerraceChangingPhase,"Phase transition begins after recovery");
                Check(!boss.ReceiveHit(player,50,10),"Only the transition protects the boss from damage");
                var snapshot=brain.CaptureNetwork();
                using(var writer=NetworkWriterPool.Get())
                {
                    writer.Write(snapshot);
                    using var reader=NetworkReaderPool.Get(writer.ToArraySegment());
                    var copy=reader.Read<TrialChampionState>();
                    Check(copy.terracePhase==snapshot.terracePhase && copy.hazards.Length==0,"Transition clock round-trips through Mirror");
                }
                Until(()=>brain.Phase==2,"The encounter reaches phase two");
                // The manual simulation advances the boss clock without advancing the player's hit immunity clock.
                typeof(TrialPlayer).GetField("hurtUntil",All).SetValue(player,0f);
                player.Teleport(boss.transform.position+boss.transform.forward*1.9f);
                brain.BeginMove(1);
                Until(()=>brain.MoveIndex==10,"The phase-two close exchange leads naturally into the downbeat");
                brain.BeginMove(10);Advance(.12f);
                snapshot=brain.CaptureNetwork();
                using(var writer=NetworkWriterPool.Get())
                {
                    writer.Write(snapshot);
                    using var reader=NetworkReaderPool.Get(writer.ToArraySegment());
                    var copy=reader.Read<TrialChampionState>();
                    Check(copy.move==10 && copy.terraceWindup==snapshot.terraceWindup && copy.terraceRecovery==snapshot.terraceRecovery,
                        "Phase two attack clocks round-trip through Mirror");
                }
                Until(()=>brain.MoveIndex==11,"A player remaining in front can receive the explicit fourth cut");
                Until(()=>brain.Recovering,"The fourth cut reaches a fixed recovery");
                brain.BeginMove(10);
                typeof(TrialChampion).GetField("terraceStreak",All).SetValue(brain,2);
                typeof(TrialPlayer).GetField("hurtUntil",All).SetValue(player,0f);
                for(int i=0;i<60 && !boss.Vulnerable;i++){Guard(0);Step();}
                Check(boss.Vulnerable && !brain.Attacking,"The third perfect contact cancels the fourth cut in phase two");

                boss.Teleport(TrialWorld.TerraceCenter);player.Teleport(new Vector3(0,0,28));
                brain.BeginMove(12);Advance(.73f);
                Check(Mathf.Abs(boss.transform.position.x)>.9f,"The wheel cut takes a diagonal step through the swing");
                float wheelEndZ=boss.transform.position.z;
                Advance(.32f);
                Check(boss.transform.position.z<wheelEndZ-.7f && brain.Recovering,
                    "The wheel cut steps back and leaves a punish window");

                PrepareNeutral(5.4f);
                ObserveAction(FighterAction.Heal);
                Decide("Mid-range decision runs on the encounter tick");
                Check(brain.MoveIndex==0,"The boss does not react on the first frame of a heal");
                PrepareNeutral(5.4f);
                ObserveAction(FighterAction.Heal);
                Advance(.21f);
                PlaceDuel(TrialWorld.TerraceCenter,Vector3.forward,5.4f);
                Decide("The boss selects a move after observing the heal");
                Check(brain.MoveIndex==7,"A committed mid-range heal draws a pursuit thrust");

                PrepareNeutral(2.6f);
                Guard(.5f);
                Advance(.47f);
                PlaceDuel(TrialWorld.TerraceCenter,Vector3.forward,2.6f);
                Decide("A sustained guard is evaluated as an opener");
                Check(brain.MoveIndex==8,"Sustained guard in the open draws the dodgeable sweep");
                brain.CancelAttack();
                Decide("A canceled sweep leaves other options available");
                Check(brain.MoveIndex!=8,"An interrupted sweep still spends its cooldown");

                PrepareNeutral(2.6f);
                Vector3 railDuel=new Vector3(8.75f,0,25);
                PlaceDuel(railDuel,Vector3.right,2.6f);
                Guard(.5f);
                Advance(.47f);
                PlaceDuel(railDuel,Vector3.right,2.6f);
                Decide("Guard is evaluated with the player by the rail");
                Check(brain.MoveIndex!=8 && brain.MoveIndex!=7,"Pinned players are spared the sweep and driving thrust");

                PrepareNeutral(2.45f);
                for(int i=0;i<2;i++)
                {
                    brain.BeginMove(0);
                    Advance(.07f);
                    ObserveAction(FighterAction.Dodge);
                    Advance(.04f);
                    brain.CancelAttack();
                    ObserveAction(FighterAction.Free);
                    Step();
                }
                PlaceDuel(TrialWorld.TerraceCenter,Vector3.forward,2.45f);
                Decide("Early rolls affect the next neutral decision");
                Check(brain.MoveIndex==3,"Repeated early rolls draw the delayed cut as an opener");
                PrepareNeutral(2.45f);
                Decide("The boss chooses again after retry");
                Check(brain.MoveIndex==1,"Retry clears the early-roll read and recent attack history");
                brain.CancelAttack();
                Decide("Another opener is available at the same distance");
                Check(brain.MoveIndex!=1,"Recent openings are deprioritized at equal spacing");

                PrepareNeutral(2f);
                brain.BeginMove(1);
                Advance(.31f);
                typeof(TrialPlayer).GetField("dodgeDirection",All).SetValue(player,brain.Heading);
                ObserveAction(FighterAction.Dodge);
                player.Teleport(boss.transform.position+brain.Heading*4.2f);
                Until(()=>brain.MoveIndex==7,"A reachable backstep branches from the close cut into a pursuit thrust");

                PrepareNeutral(2f);
                brain.BeginMove(1);
                Advance(.31f);
                typeof(TrialPlayer).GetField("dodgeDirection",All).SetValue(player,brain.Heading);
                ObserveAction(FighterAction.Dodge);
                player.Teleport(boss.transform.position+brain.Heading*7.4f);
                Until(()=>brain.Recovering,"A backstep beyond the branch's reach leaves the boss in recovery");
                Check(brain.MoveIndex==1,"The boss does not extend a missed attack past its follow-up reach");

                Debug.Log($"Crimson Sword Terrace passed {results.Count} checks.");
                return string.Join("\n",results);
            }
            finally
            {
                UnityEngine.Object.Destroy(input);
                game.RestartTrial();
                player.ResetRunBoon();
                game.enabled=wasEnabled;
            }
        }
    }
}
