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
        [MenuItem("Tools/Tomorin/Champions/Validate all stages over real TCP (Play Mode)")]
        public static void RunChampions()
        {
            if(!EditorApplication.isPlaying || routine!=null || !TrialNetworkManager.Current || TrialNetworkManager.SessionActive)throw new InvalidOperationException("Run InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();Status="Running champion TCP checks";reportName="ChampionNetworkValidation.txt";
            runtimeErrors=0;Application.logMessageReceived+=CaptureError;routine=ChampionNetworkChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static IEnumerator ChampionNetworkChecks()
        {
            var network=TrialNetworkManager.Current;var transport=(TelepathyTransport)network.transport;ushort oldPort=transport.port;string oldName=network.PlayerName;
            try
            {
                for(int stage=0;stage<5;stage++)
                {
                    network=TrialNetworkManager.Current;transport=(TelepathyTransport)network.transport;
                    var level=(TrialLevel)((int)TrialLevel.Twins+stage);network.SelectLevel(level);transport.port=(ushort)(18110+stage);network.PlayerName="ChampionHost";network.Host();
                    Check(NetworkServer.active,level+" TCP host starts");incoming=new Unbatcher();lobby=sceneReceived=false;world=default;held=default;
                    client=new Telepathy.Client(65536){NoDelay=true};client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="ChampionPeer"});};client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                    Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                    Check(receivedLevel==level,level+" lobby communicates selected stage");network.BeginTogether();
                    Until();while(Waiting(sceneReceived && UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                    Send(new ReadyMessage());var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();
                    Until();while(Waiting(network.Players.Count==2 && world.champions!=null && world.champions.Length==game.Enemies.Count))yield return .02f;
                    Check(game.Level==level && game.gameObject.scene.path==TrialChampionCatalog.Scene(level),level+" host loads intended scene");
                    Check(world.level==level && world.partySize==2,level+" peer receives authoritative stage and population");
                    game.Player.ChooseBoon(game.Player.OfferA);var remote=network.Players.First(p=>p!=game.Player);Send(new TrialChooseBoon{boon=remote.OfferA});
                    Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                    Check(game.Enemies.All(e=>Mathf.Abs(e.MaxHealth-TrialChampionCatalog.Health(e.Champion.Kind)*2)<.1f),level+" boss health scales to both peers");
                    foreach(var e in game.Enemies)e.HoldCombat(30);
                    var boss=game.Enemies.Last();int index=level==TrialLevel.Twins?12:level==TrialLevel.Anon?9:level==TrialLevel.Taffy?10:level==TrialLevel.Penguin?8:10;
                    boss.Champion.BeginMove(index);
                    Until();while(Waiting(world.champions!=null && world.champions.Last().move==index))yield return .02f;
                    Check(world.champions.Last().heading.sqrMagnitude>.5f,level+" TCP snapshot contains live unique skill and aim");
                    // Advance the host-owned clock while its normal combat tick is held, then observe actual serialized hazards.
                    boss.Champion.Tick(boss.Champion.Move.windup+.01f);
                    if(level!=TrialLevel.Twins)
                    {
                        Until();while(Waiting(world.champions.Last().hazards!=null && world.champions.Last().hazards.Length>0))yield return .02f;
                        Check(world.champions.Last().hazards.All(h=>h.active && h.kind<=12),level+" typed hazard payload survives real TCP framing");
                    }
                    boss.Champion.CancelAttack(true);
                    Until();while(Waiting(world.champions.Last().hazards!=null && world.champions.Last().hazards.Length==0))yield return .02f;
                    Check(true,level+" peer receives projectile and trap cleanup");
                    client.Disconnect();client=null;network.Leave();
                    Until();while(Waiting(SceneManager.GetActiveScene().path==TrialNetworkManager.MenuScene))yield return .02f;
                    yield return .2f;
                    Until();while(Waiting(TrialNetworkManager.Current && !TrialNetworkManager.Current.Busy))yield return .02f;
                }
                Check(runtimeErrors==0,"Five TCP stage sessions finish without runtime errors");
            }
            finally
            {var current=TrialNetworkManager.Current;if(current){if(TrialNetworkManager.SessionActive)current.Leave();((TelepathyTransport)current.transport).port=oldPort;current.PlayerName=oldName;}}
        }
    }
}
