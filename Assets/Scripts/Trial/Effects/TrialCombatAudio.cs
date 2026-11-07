using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        readonly List<AudioClip> generatedSounds=new List<AudioClip>();
        readonly Dictionary<string,AudioClip[]> combatVariants=new Dictionary<string,AudioClip[]>();
        // Audio has its own random stream: cosmetic variation must not consume combat RNG.
        readonly System.Random audioRandom=new System.Random();
        sealed class SoundBag { public int[] order;public int cursor,last=-1; }
        readonly Dictionary<string,SoundBag> soundBags=new Dictionary<string,SoundBag>();
        readonly Dictionary<string,float> nextFlightSound=new Dictionary<string,float>();
        static readonly string[] ComboAudioKeys={"cutLight","cutHeavy","cutThrust","cutRise","cutSweep","cutDraw","cutSpin","cutAir","moveHilt","moveKick","skillCrimson","skillMoon","skillFire","skillSpiral","skillSakura","skillSand","skillRecall","impactBody","impactPierce"};
        static readonly HashSet<string> ComboAudioSet=new HashSet<string>(ComboAudioKeys);
        float nextFootstep;
        void LoadCombatAudio()
        {
            string[] keys={"swing","hit","impactHeavy","finish","parry","block","hurt","warning","dodge","heal","death","heavy","tideImpact","bubbleCast","bubblePop","tideCharge","heartbeat","footstep","charge","counter","landing"};
            foreach(string key in keys)
            {
                LoadSoundBank(key,3);
            }
            foreach(string key in ComboAudioKeys)LoadSoundBank(key,4);
        }
        void LoadSoundBank(string key,int count)
        {
            var clips=new List<AudioClip>(count);
            for(int i=0;i<count;i++)
            {
                var clip=Resources.Load<AudioClip>("Trial/Audio/"+key+"_"+i);
                if(clip)clips.Add(clip);
            }
            if(clips.Count==0)return;
            combatVariants[key]=clips.ToArray();sounds[key]=clips[0];soundBags.Remove(key);
        }
        AudioClip NextCombatClip(string key,AudioClip fallback)
        {
            if(!combatVariants.TryGetValue(key,out var variants) || variants.Length==0)return fallback;
            if(!soundBags.TryGetValue(key,out var bag))
            {
                bag=new SoundBag{order=new int[variants.Length],cursor=variants.Length};
                for(int i=0;i<bag.order.Length;i++)bag.order[i]=i;
                soundBags.Add(key,bag);
            }
            if(bag.cursor==bag.order.Length)
            {
                for(int i=bag.order.Length-1;i>0;i--)
                {int j=audioRandom.Next(i+1),swap=bag.order[i];bag.order[i]=bag.order[j];bag.order[j]=swap;}
                // Also avoid a repeat across the boundary between two shuffled rounds.
                if(bag.order.Length>1 && bag.order[0]==bag.last)
                {int j=audioRandom.Next(1,bag.order.Length),swap=bag.order[0];bag.order[0]=bag.order[j];bag.order[j]=swap;}
                bag.cursor=0;
            }
            bag.last=bag.order[bag.cursor++];return variants[bag.last];
        }
        public static string MoveSoundKey(int move,bool heavy)
        {
            if(TrialPlayer.IsBodyStrike(move))return move==TrialPlayer.HiltStrike || move==TrialPlayer.ElbowBreak?"moveHilt":"moveKick";
            return move switch
            {
                2 or 8 or 11 or 25 or TrialPlayer.BehindThrust or TrialPlayer.RollThrust or TrialPlayer.ThreadPierce or TrialPlayer.GatheredPierce or TrialPlayer.GuardNeedle or TrialPlayer.AirPierce or TrialPlayer.MoonChase=>"cutThrust",
                4 or 9 or 16 or 17 or TrialPlayer.CrossDraw or TrialPlayer.RetreatDraw or TrialPlayer.FallingDraw or TrialPlayer.SheathTurn or TrialPlayer.RollFlash or TrialPlayer.SwordRecallCut=>"cutDraw",
                10 or 15 or 18 or 23 or TrialPlayer.RisingBridge or TrialPlayer.LoftingDraw or TrialPlayer.ParryAscend or TrialPlayer.SwordRecallRise=>"cutRise",
                12 or 21 or 24 or TrialPlayer.WheelFinish or TrialPlayer.StormWheel or TrialPlayer.GuardBloom or TrialPlayer.AirWheel or TrialPlayer.OrbitCut or TrialPlayer.SpiralSword=>"cutSpin",
                1 or 7 or 22 or TrialPlayer.FlowingSweep or TrialPlayer.LowReap or TrialPlayer.ParryMirror=>"cutSweep",
                3 or TrialPlayer.AirBladeFirst or TrialPlayer.AirBladeSecond or TrialPlayer.AirBladeThird or TrialPlayer.AirStep or TrialPlayer.AirCross or TrialPlayer.AirFan or TrialPlayer.WallRebound or TrialPlayer.LowVault or TrialPlayer.VaultSweep or TrialPlayer.CloudRake or TrialPlayer.AerialCascade=>"cutAir",
                _=>heavy?"cutHeavy":"cutLight"
            };
        }
        public void MoveSound(int move,bool heavy,Vector3 point)=>Sound(MoveSoundKey(move,heavy),point);
        string ContactSoundKey(TrialCombatEvent cue,string fallback)
        {
            var actor=TrialNetworkManager.SessionActive?TrialNetworkManager.Current.PlayerById(cue.player):game.Player;
            if(!actor || actor.Action!=FighterAction.Light && actor.Action!=FighterAction.Heavy)return fallback;
            if(TrialPlayer.IsBodyStrike(actor.Combo))return "impactBody";
            return MoveSoundKey(actor.Combo,actor.Action==FighterAction.Heavy)=="cutThrust" && cue.cue!=TrialCombatCue.Kill?"impactPierce":fallback;
        }
        void TickCombatAudio()
        {
            if(game.Mode!=TrialMode.Playing || !game.Player.Alive || game.Player.Action!=FighterAction.Free && game.Player.Action!=FighterAction.Heal)return;
            if(game.Player.MoveBlend>.18f && Time.time>=nextFootstep)
            {
                nextFootstep=Time.time+(game.Player.Sprinting?.25f:.37f);
                Sound("footstep",game.Player.transform.position,Random.Range(.94f,1.08f));
            }
        }
    }
}
