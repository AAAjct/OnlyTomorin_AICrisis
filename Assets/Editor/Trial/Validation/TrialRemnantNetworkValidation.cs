using System;
using System.Collections;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static partial class TrialNetworkValidation
    {
        [MenuItem("Tools/Tomorin/Validate Remnant growth over TCP (Play Mode)")]
        public static void RunRemnantNetwork()
        {
            if(!EditorApplication.isPlaying || routine!=null || !TrialNetworkManager.Current || TrialNetworkManager.SessionActive)throw new InvalidOperationException("Run InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();Status="Running Remnant TCP checks";reportName="RemnantNetworkValidation.txt";
            runtimeErrors=0;Application.logMessageReceived+=CaptureError;routine=RemnantChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static IEnumerator RemnantChecks()
        {
            var network=TrialNetworkManager.Current;var transport=(TelepathyTransport)network.transport;ushort oldPort=transport.port;
            var oldLevel=network.SelectedLevel;
            try
            {
                network.SelectLevel(TrialLevel.Remnant);transport.port=18481;network.Host();
                incoming=new Unbatcher();lobby=sceneReceived=false;world=default;held=default;
                client=new Telepathy.Client(65536){NoDelay=true};client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="RemnantPeer"});};client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(receivedLevel==TrialLevel.Remnant,"New level is selectable and serialized in the TCP lobby");network.BeginTogether();
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
                Until();while(Waiting(network.Players.Count==2 && world.champions!=null && world.champions.Length==1))yield return .02f;
                game.Player.ChooseBoon(game.Player.OfferA);var remote=network.Players.First(p=>p!=game.Player);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                var boss=game.Enemies[0];var brain=boss.Champion;boss.HoldCombat(60);game.Player.Restore(new Vector3(-15,0,8));
                Check(boss.MaxHealth==14400,"Two-player base health is authoritative");
                for(int kill=1;kill<=2;kill++)
                {
                    remote.Restore(new Vector3(0,0,25));remote.ReceiveHit(boss,10000,0,true);
                    Check(!remote.Alive && brain.KillStacks==kill,"Boss lethal hit awards exactly one growth stack "+kill);
                    remote.ReceiveHit(boss,10000,0,true);Check(brain.KillStacks==kill,"Repeated hits on a dead player cannot add stacks");
                    float max=14400*(1+kill*.18f);
                    Check(Mathf.Abs(boss.MaxHealth-max)<.1f,"Kill "+kill+" increases maximum health by 18% of party-scaled base");
                    Until();while(Waiting(world.champions[0].kills==kill && Mathf.Abs(world.enemies[0].maxHealth-max)<.1f))yield return .02f;
                    Check(world.champions[0].kills==kill,"Real TCP peer receives kill stacks and increased health "+kill);
                }
                remote.Restore(new Vector3(0,0,25));remote.ReceiveHit(null,10000,0,true);
                Check(brain.KillStacks==2,"Environmental death does not credit boss growth");
                remote.Restore(new Vector3(0,0,25));float before=remote.Health;remote.ReceiveHit(boss,20,0,true);
                Check(Mathf.Abs(before-remote.Health-20*game.EnemyDamageScale*1.24f)<.05f,"Two kill stacks actually increase outgoing damage by 24%");
                game.SetPartySize(3);Check(Mathf.Abs(boss.MaxHealth-7200*3*1.36f)<.1f && brain.KillStacks==2,"Party rescaling preserves earned growth");game.SetPartySize(2);
                using(var writer=NetworkWriterPool.Get())
                {
                    var state=brain.CaptureNetwork();state.guardTime=.3f;state.respite=1.2f;writer.Write(state);
                    using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))
                    {var copy=reader.Read<TrialChampionState>();Check(copy.kills==2 && copy.guardTime==.3f && copy.respite==1.2f,"Mirror roundtrip preserves defenses, respite and growth");}
                }
                boss.ResetFighter();Check(brain.KillStacks==0 && boss.MaxHealth==14400 && boss.Health==14400,"Retry resets growth, health and posture scaling");
                Until();while(Waiting(world.champions[0].kills==0 && world.enemies[0].maxHealth==14400))yield return .02f;
                Check(runtimeErrors==0,"TCP Remnant encounter completes without runtime errors");
            }
            finally
            {if(client!=null){client.Disconnect();client=null;}if(TrialNetworkManager.SessionActive)network.Leave();transport.port=oldPort;network.SelectLevel(oldLevel);}
        }
    }
}
