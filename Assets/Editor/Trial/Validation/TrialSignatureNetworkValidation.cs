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
        [MenuItem("Tools/Tomorin/Validate Crimson Sword Terrace over TCP (Play Mode)")]
        public static void RunSignatureNetwork()
        {
            if(!EditorApplication.isPlaying || routine!=null || !TrialNetworkManager.Current || TrialNetworkManager.SessionActive)throw new InvalidOperationException("Run InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();Status="Running signature TCP checks";reportName="SignatureNetworkValidation.txt";
            runtimeErrors=0;Application.logMessageReceived+=CaptureError;routine=SignatureNetworkChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static IEnumerator SignatureNetworkChecks()
        {
            var network=TrialNetworkManager.Current;var transport=(TelepathyTransport)network.transport;
            ushort oldPort=transport.port;var oldLevel=network.SelectedLevel;
            try
            {
                network.SelectLevel(TrialLevel.Anon);transport.port=18485;network.Host();
                incoming=new Unbatcher();lobby=sceneReceived=false;world=default;held=default;
                client=new Telepathy.Client(65536){NoDelay=true};
                client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="TerracePeer"});};
                client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(receivedLevel==TrialLevel.Anon,"TCP lobby selects the sword saint encounter");network.BeginTogether();
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
                Until();while(Waiting(network.Players.Count==2 && world.champions!=null && world.champions.Length==1))yield return .02f;
                game.Player.ChooseBoon(game.Player.OfferA);var remote=network.Players.First(p=>p!=game.Player);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                var boss=game.Enemies[0];var brain=boss.Champion;
                Check(!world.enemies[0].awake,"TCP clients can stage south of the opening line");
                game.Player.Teleport(new Vector3(-3,0,16));
                remote.Teleport(new Vector3(0,0,TrialWorld.TerraceEntryZ+.05f));
                Until();while(Waiting(world.enemies[0].awake && world.champions[0].move==0))yield return .02f;
                var snapshot=world.champions[0];
                Check(snapshot.terraceWindup==.68f && snapshot.hazards.Length==0,"TCP receives a grounded rush and its full windup");

                boss.HoldCombat(60);
                brain.CancelAttack(true);
                boss.Teleport(TrialWorld.TerraceBossSpawn);
                remote.Teleport(new Vector3(0,0,25));
                held=new TrialControls{held=1};Send(held);
                Until();while(Waiting(remote.Guarding && remote.GuardAge>=.5f))yield return .02f;
                game.SetCombatTarget(remote);
                brain.Tick(.5f);
                boss.Teleport(TrialWorld.TerraceBossSpawn);
                boss.transform.rotation=Quaternion.LookRotation(remote.transform.position-boss.transform.position);
                brain.Tick(.26f);
                Until();while(Waiting(world.champions[0].move==8))yield return .02f;
                Check(world.champions[0].terraceWindup==.76f,
                    "A remote held guard selects the sweep on the authority and replicates its complete tell");
                held=default;Send(held);
                brain.CancelAttack(true);
                boss.Teleport(TrialWorld.TerraceBossSpawn);
                remote.Teleport(new Vector3(0,0,25));
                boss.ReceiveHit(remote,boss.MaxHealth*.5f,0);
                brain.Tick(.08f);
                Until();while(Waiting(world.champions[0].terracePhase>0))yield return .02f;
                Check(world.champions[0].phase==1,"Transition clock reaches the peer before the phase changes");
                for(int i=0;i<22;i++)brain.Tick(.1f);
                Until();while(Waiting(world.champions[0].phase==2))yield return .02f;

                game.SetCombatTarget(remote);
                brain.BeginMove(10);
                game.SetCombatTarget(game.Player);
                Until();while(Waiting(world.champions[0].move==10))yield return .02f;
                Check(world.champions[0].terraceWindup==.46f,"TCP receives the distinct second-phase tell");
                for(int i=0;i<30;i++)brain.Tick(.02f);
                Until();while(Waiting(world.champions[0].move==11))yield return .02f;
                Check(world.champions[0].terraceRecovery==1.05f && world.champions[0].hazards.Length==0,
                    "TCP receives the fourth cut and its recoverable finish without persistent hazards");
                Check(runtimeErrors==0,"TCP terrace encounter completes without runtime errors");
            }
            finally
            {
                if(client!=null){client.Disconnect();client=null;}
                if(TrialNetworkManager.SessionActive)network.Leave();transport.port=oldPort;network.SelectLevel(oldLevel);
            }
        }
    }
}
