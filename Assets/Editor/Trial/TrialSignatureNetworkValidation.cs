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
        [MenuItem("Tools/Tomorin/Validate sword garden over TCP (Play Mode)")]
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
                client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="SwordGardenPeer"});};
                client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(receivedLevel==TrialLevel.Anon,"TCP lobby selects the sword saint encounter");network.BeginTogether();
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
                Until();while(Waiting(network.Players.Count==2 && world.champions!=null && world.champions.Length==1))yield return .02f;
                game.Player.ChooseBoon(game.Player.OfferA);var remote=network.Players.First(p=>p!=game.Player);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                var boss=game.Enemies[0];var brain=boss.Champion;
                Until();while(Waiting(world.champions[0].introAge>.1f && !world.champions[0].introPlayed))yield return .02f;
                Check(!world.champions[0].introPlayed,"Real TCP peer receives the landing / sword-draw timeline");
                Until();while(Waiting(world.champions[0].introPlayed))yield return .02f;
                Check(world.players.All(p=>p.maxStamina>=420),"All real TCP actors use the increased stamina maximum");
                boss.HoldCombat(60);brain.CancelAttack(true);game.Player.Restore(new Vector3(-15,0,8));remote.Restore(new Vector3(0,0,22));boss.Teleport(new Vector3(0,0,29));
                brain.BeginMove(4);brain.Tick(.92f);
                Until();while(Waiting(world.champions[0].hazards.Count(h=>h.kind==(byte)ChampionHazardKind.SwordRain)==15))yield return .02f;
                var snapshot=world.champions[0];
                Check(snapshot.hazards.Count(h=>h.kind==(byte)ChampionHazardKind.StarEdge)==5,"Real TCP peer receives every pentagram edge");
                Check(snapshot.hazards.Where(h=>h.kind==(byte)ChampionHazardKind.SwordRain).All(h=>h.aux>=.9f && h.radius>.5f),"Real TCP peer receives sword warning delay and collision radius");
                brain.Tick(.8f);
                Until();while(Waiting(world.champions[0].move==10 && world.champions[0].hazards.Length>0))yield return .02f;
                Check(world.champions[0].move==10,"TCP snapshots preserve simultaneous close combat and persistent sword hazards");
                brain.CancelAttack(true);typeof(TrialPlayer).GetProperty("Action").SetValue(remote,FighterAction.Light);boss.ReceiveHit(remote,1,0);
                Until();while(Waiting(world.enemies[0].bossFlinch>0))yield return .02f;
                Check(world.enemies[0].bossFlinch>=.12f,"A real remote player hit broadcasts the boss short flinch");
                brain.CancelAttack(true);
                Until();while(Waiting(world.champions[0].hazards.Length==0))yield return .02f;
                Check(world.champions[0].hazards.Length==0,"Hazard cancellation reaches the TCP peer");
                Check(runtimeErrors==0,"TCP sword-garden encounter completes without runtime errors");
            }
            finally
            {
                if(client!=null){client.Disconnect();client=null;}
                if(TrialNetworkManager.SessionActive)network.Leave();transport.port=oldPort;network.SelectLevel(oldLevel);
            }
        }
    }
}
