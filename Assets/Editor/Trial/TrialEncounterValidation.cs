using System;
using System.Collections;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static partial class TrialNetworkValidation
    {
        static TrialLevel receivedLevel;
        static readonly System.Collections.Generic.List<Telepathy.Client> encounterPeers=new System.Collections.Generic.List<Telepathy.Client>();
        static void TickEncounterPeers(){foreach(var peer in encounterPeers)peer.Tick(1000);}
        static void ConnectExtraPeer(TrialNetworkManager network,int index)
        {
            var peer=new Telepathy.Client(16384){NoDelay=true};
            peer.OnConnected=()=>
            {
                var batch=new Batcher(16384);
                using(var writer=NetworkWriterPool.Get()){NetworkMessages.Pack(new TrialHello{name="Extra "+index},writer);batch.AddMessage(writer.ToArraySegment(),Time.realtimeSinceStartupAsDouble);}
                using(var writer=NetworkWriterPool.Get())while(batch.GetBatch(writer)){peer.Send(writer.ToArraySegment());writer.Reset();}
            };
            peer.OnData=_=>{};encounterPeers.Add(peer);peer.Connect("127.0.0.1",network.GamePort);
        }

        [MenuItem("Tools/Tomorin/Verify boss scaling and LAN arena (Play Mode)")]
        public static void RunEncounters()
        {
            if(!EditorApplication.isPlaying || routine!=null || !TrialNetworkManager.Current || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode, outside a network session.");
            results.Clear();events.Clear();Status="Running encounters";runtimeErrors=0;reportName="BossArenaValidation.txt";
            Application.logMessageReceived+=CaptureError;
            routine=EncounterChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }

        static void ConnectEncounterPeer(TrialNetworkManager network)
        {
            incoming=new Unbatcher();lobby=sceneReceived=false;world=default;held=default;
            client=new Telepathy.Client(16384){NoDelay=true};
            client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="DuelPeer"});};
            client.OnData=Receive;client.Connect("127.0.0.1",network.GamePort);
        }

        static IEnumerator EncounterChecks()
        {
            var network=TrialNetworkManager.Current;
            var transport=(TelepathyTransport)network.transport;
            ushort originalPort=transport.port;
            TrialLevel originalLevel=network.SelectedLevel;
            try
            {
                transport.port=18102;network.SelectLevel(TrialLevel.Arena);network.Host();
                Until();while(Waiting(network.Members.Length==1))yield return .02f;
                network.BeginTogether();yield return .05f;
                Check(UnityEngine.Object.FindFirstObjectByType<InkMainMenu>() && network.Status.Contains("至少"),"Arena cannot start without a second player");
                ConnectEncounterPeer(network);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(receivedLevel==TrialLevel.Arena,"Independent TCP peer receives the host's arena selection");
                network.BeginTogether();
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());
                Until();while(Waiting(network.Players.Count()==2 && world.players!=null))yield return .02f;
                var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
                var host=game.Player;var remote=network.Players.First(p=>p!=host);
                Check(game.IsArena && game.Enemies.All(e=>!e.gameObject.activeSelf) && world.level==TrialLevel.Arena,"Arena suppresses all PvE actors and replicates the selected rules");
                Check((host.transform.position-remote.transform.position).sqrMagnitude>60,"Duelists start at separated spawn points");
                Check(((UnityEngine.UI.Text)typeof(TrialHud).GetField("playerName",Private).GetValue(game.Hud)).text==network.NameOf(host),"The local vitality HUD displays the player's chosen network name");
                var plates=(System.Collections.IDictionary)typeof(TrialHud).GetField("nameplates",Private).GetValue(game.Hud);
                Check(plates.Count==1 && plates.Contains(remote) && !plates.Contains(host),"Overhead name and vitality widgets are created only for other players");
                Check(host.SpawnProtected && !host.ReceiveHit(remote,50,20,true),"Spawn protection rejects incoming PvP damage");
                host.Restore(new Vector3(0,0,26));host.transform.rotation=Quaternion.Euler(0,180,0);
                remote.Restore(new Vector3(0,0,24));
                host.ToggleLock();Check(host.Locked && host.DuelTarget==remote,"Arena lock-on selects another player");
                Send(new TrialControls{pressed=1,yaw=0});
                Until();while(Waiting(host.Health<host.MaxHealth))yield return .01f;
                Check(remote.Stamina<remote.MaxStamina,"A remote TCP sword input causes authoritative PvP damage and stamina cost");
                yield return .7f;
                host.Restore(new Vector3(0,0,26));host.transform.rotation=Quaternion.Euler(0,180,0);
                remote.Restore(new Vector3(0,0,24));
                var guard=game.gameObject.AddComponent<TrialInput>();guard.ApplyNetwork(new TrialControls{held=1});host.Tick(0,guard);
                int parries=host.SuccessfulParries;host.ReceiveHit(remote,30,20);
                Check(host.SuccessfulParries==parries+1 && remote.Action==FighterAction.Stagger,"A timed PvP parry staggers the attacking player");
                UnityEngine.Object.Destroy(guard);
                remote.Restore(new Vector3(0,0,24));remote.ReceiveHit(host,99999,100,true);
                Check(host.Kills==1 && remote.Deaths==1 && !remote.Alive,"A PvP kill credits the attacker and increments the victim's death count once");
                remote.ReceiveHit(host,99999,100,true);Check(host.Kills==1,"Repeated hits on a dead player cannot duplicate kill credit");
                Until();while(Waiting(world.players.Any(p=>p.kills==1) && world.players.Any(p=>p.deaths==1)))yield return .02f;
                Check(true,"Authoritative scores reach the independent TCP peer");
                Until();while(Waiting(remote.Alive && remote.SpawnProtected))yield return .03f;
                Check(remote.Health==remote.MaxHealth && remote.Deaths==1,"Arena death respawns with full resources, protection, and retained score");
                client.Disconnect();client=null;network.Leave();
                Until();while(Waiting(UnityEngine.Object.FindFirstObjectByType<InkMainMenu>()!=null))yield return .03f;
                yield return .2f;

                network=TrialNetworkManager.Current;((TelepathyTransport)network.transport).port=18102;
                network.SelectLevel(TrialLevel.BigFish);network.Host();ConnectEncounterPeer(network);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(receivedLevel==TrialLevel.BigFish,"Changing the host's level is reflected in the next room handshake");
                network.BeginTogether();
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());
                Until();while(Waiting(network.Players.Count()==2 && world.players!=null))yield return .02f;
                game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();host=game.Player;remote=network.Players.First(p=>p!=host);
                var boss=game.Enemies.Last();var brain=boss.Boss;
                Check(!game.IsArena && !game.GateOpen && game.DefeatedGuards==0 && host.transform.position.z< -18 && game.Enemies.All(e=>e.Alive),"Courtyard selection starts at the entrance with all guards alive and the boss gate sealed");
                Check(host.ChoosingBoon && remote.ChoosingBoon && host.OfferA!=host.OfferB && host.OfferB!=host.OfferC && host.OfferA!=host.OfferC,"Each co-op player receives three distinct run-boon choices");
                Check(!host.ChooseBoon(TrialBoon.None),"Invalid boon choices are rejected");
                host.ChooseBoon(host.OfferA);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                Check(remote.Boon!=TrialBoon.None,"Remote boon choice is validated and applied on the authority");
                Check(game.PartySize==2 && Mathf.Abs(boss.MaxHealth-2800)<.1f && boss.PostureLimit>=150,"Two players double boss health and increase stagger resistance");
                Check(!host.ReceiveHit(remote,50,20,true),"Co-op allies cannot damage each other");
                Check(world.enemies.Last().maxHealth==boss.MaxHealth && world.enemies.Last().postureLimit==boss.PostureLimit,"Scaled maximum health and posture are serialized to the peer");
                game.StartBossChallenge();host.ChooseBoon(host.OfferA);
                host.Restore(new Vector3(8,0,19));remote.Restore(new Vector3(-8,0,19));
                boss.ReceiveHit(host,boss.MaxHealth*.3f,0,true);float fraction=boss.Health/boss.MaxHealth;
                client.Disconnect();client=null;
                Until();while(Waiting(network.Members.Length==1 && game.PartySize==1))yield return .02f;
                Check(Mathf.Abs(boss.MaxHealth-1400)<.1f && Mathf.Abs(boss.Health/boss.MaxHealth-fraction)<.001f,"Disconnect scales boss down without healing its remaining health fraction");
                game.SetPartySize(4);
                Check(Mathf.Abs(boss.MaxHealth-5600)<.1f && Mathf.Abs(boss.Health/boss.MaxHealth-fraction)<.001f,"Four players give exactly four times solo boss health and preserve damage already dealt");
                game.SetPartySize(1);
                ConnectEncounterPeer(network);
                Until();while(Waiting(network.Members.Length==2 && game.PartySize==2))yield return .02f;
                Send(new ReadyMessage());
                Check(Mathf.Abs(boss.Health/boss.MaxHealth-fraction)<.001f,"A late join scales boss up without restoring lost health");
                Until();while(Waiting(network.Players.Count()==2))yield return .02f;
                Check(network.Players.Where(p=>p!=host).All(p=>p.transform.position.z>=18),"Late joiners spawn inside the sealed boss arena instead of being trapped outside the gate");
                for(int i=0;i<3;i++)ConnectExtraPeer(network,i);
                Until();while(Waiting(network.Members.Length==5 && network.Players.Count()==5))yield return .02f;
                Check(game.PartySize==5 && Mathf.Abs(boss.MaxHealth-7000)<.1f && network.maxConnections>100000,"Five real TCP participants exceed the old four-player limit and give exactly five times boss health");
                foreach(var extra in encounterPeers)extra.Disconnect();encounterPeers.Clear();
                Until();while(Waiting(network.Members.Length==2))yield return .02f;

                // Drive the actual encounter at fixed steps while network snapshots are held.
                game.enabled=false;
                CheckResponsiveness(game,boss);
                foreach(var skill in new[]{DeepFishSkill.ReverseTail,DeepFishSkill.RiptideRush,DeepFishSkill.DelayedSlam,DeepFishSkill.AbyssRain})
                {
                    boss.ResetFighter();host.ResetRunBoon();host.Restore(new Vector3(0,0,24));boss.Teleport(new Vector3(0,0,27));game.SetCombatTarget(host);
                    boss.ReceiveHit(host,1,0);brain.StartSkill(skill);
                    float health=host.Health;
                    for(int i=0;i<8;i++)brain.Tick(.02f);
                    Check(brain.WindingUp && host.Health==health,skill+" provides a visible, harmless windup");
                    for(int i=0;i<230 && brain.Skill!=DeepFishSkill.None;i++)brain.Tick(.02f);
                    Check(host.Health<health,skill+" resolves its telegraphed attack against a stationary target");
                    brain.CancelAttack();Check(brain.ActiveHazards==0,skill+" clears all hazards on cancellation");
                }
                boss.ResetFighter();brain.ClearBoon();host.Restore(new Vector3(0,0,23));boss.Teleport(new Vector3(0,0,27));boss.ReceiveHit(host,boss.MaxHealth*.8f,0);
                brain.Tick(.02f);Check(brain.Enraged && brain.Frenzied,"Boss enters awakening and low-health frenzy at the correct health thresholds");
                boss.ResetFighter();Check(!brain.Enraged && !brain.Frenzied && brain.ActiveHazards==0,"A reset clears frenzy, chained skills and hazards");
                game.SetCombatTarget(host);game.enabled=true;
                network.Leave();client.Disconnect();client=null;
                yield return .2f;
                Check(runtimeErrors==0,"Arena, boss, late-join and disconnect checks complete without runtime errors");
            }
            finally
            {
                foreach(var extra in encounterPeers)extra.Disconnect();encounterPeers.Clear();
                var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();if(game)game.enabled=true;
                if(TrialNetworkManager.Current)
                {
                    if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.Leave();
                    ((TelepathyTransport)TrialNetworkManager.Current.transport).port=originalPort;
                    TrialNetworkManager.Current.SelectLevel(originalLevel);
                }
            }
        }

        static void CheckResponsiveness(TrialGame game,TrialEnemy boss)
        {
            var host=game.Player;var brain=boss.Boss;
            boss.ResetFighter();brain.ClearBoon();host.ResetRunBoon();
            host.Restore(new Vector3(0,0,19));boss.Teleport(new Vector3(0,0,29));game.SetCombatTarget(host);
            boss.ReceiveHit(host,1,0);
            Vector3 start=boss.transform.position;
            for(int i=0;i<20;i++)brain.Tick(.02f);
            Check(Vector3.Distance(start,boss.transform.position)>.4f && boss.MoveBlend>.1f,"Boss approach moves the actor and increases its walking blend instead of cancelling it each frame");
            var rig=boss.GetComponent<TrialBossRig>();
            var leg=rig.DrivenBones.First(t=>t.name=="upper_leg.L");
            rig.Pose(DeepFishSkill.None,0,1,0,false);var idle=leg.localRotation;
            rig.Pose(DeepFishSkill.None,0,1,.2f,false);
            Check(Quaternion.Angle(idle,leg.localRotation)>10,"Walking blend visibly swings the real DeepSeek leg skeleton");

            foreach(int population in new[]{1,2,3})
            {
                game.SetPartySize(population);boss.ResetFighter();brain.ClearBoon();host.Restore(new Vector3(0,0,24));
                boss.Teleport(new Vector3(0,0,27));boss.ReceiveHit(host,boss.MaxHealth*.6f,0);
                brain.Tick(.01f);brain.CancelAttack();brain.StartSkill(DeepFishSkill.AbyssRain);
                float duration=brain.CaptureNetwork().duration;
                var tick=typeof(TrialBoss).GetMethod("TickSkill",Private);
                tick.Invoke(brain,new object[]{duration*.41f});
                int firstWave=brain.ActiveHazards;
                Check(firstWave>0,"Abyss rain retains its first wave with "+population+" player(s)");
                tick.Invoke(brain,new object[]{duration*.43f});
                // TickSkill leaves existing hazards alive, so the count reveals
                // whether another generation was actually spawned.
                int total=brain.ActiveHazards;
                Check(total==firstWave*(population==1?2:1),"Abyss rain second wave is "+(population==1?"retained solo":"suppressed in co-op")+" for population "+population);
                brain.CancelAttack();
            }
            game.SetPartySize(2);boss.ResetFighter();

            typeof(TrialHud).GetMethod("InitializeTouchControls",Private).Invoke(game.Hud,new object[]{true});
            Canvas.ForceUpdateCanvases();
            var pads=game.Hud.GetComponentsInChildren<TrialTouchControl>(true);
            var move=pads.First(p=>p.Stick);var look=pads.First(p=>p.Look);
            var rect=(RectTransform)move.transform;
            Check(rect.rect.width>=((RectTransform)rect.parent).rect.width*.49f,"Movement touch area spans the entire left half instead of the visible joystick only");
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var finger=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=11,position=point};
            game.Input.ClearTouch();move.OnPointerDown(finger);
            finger.position+=Vector2.right*200;move.OnDrag(finger);
            Check(game.Input.TouchMove.x>.5f && game.Input.TouchLook==Vector2.zero,"Dragging a captured movement finger outside the stick moves without turning the camera");
            var other=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=12,position=point,delta=new Vector2(20,0)};
            move.OnPointerUp(other);Check(game.Input.TouchMove.x>.5f,"An unrelated finger release cannot cancel movement");
            look.OnPointerDown(other);look.OnDrag(other);
            Check(game.Input.TouchLook.x>0 && game.Input.TouchMove.x>.5f,"A second finger can look while the first continues moving");
            move.OnPointerUp(finger);look.OnPointerUp(other);
            Check(game.Input.TouchMove==Vector2.zero && move.Knob.anchoredPosition==Vector2.zero,"Releasing the movement finger resets the movement vector and knob");
            game.Input.ClearTouch();
        }
    }
}
