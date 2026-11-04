using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        readonly List<AudioClip> generatedSounds=new List<AudioClip>();
        readonly Dictionary<string,AudioClip[]> combatVariants=new Dictionary<string,AudioClip[]>();
        float nextFootstep;
        void LoadCombatAudio()
        {
            string[] keys={"swing","hit","impactHeavy","finish","parry","block","hurt","warning","dodge","heal","death","heavy","tideImpact","bubbleCast","bubblePop","tideCharge","heartbeat","footstep","charge","counter","landing"};
            foreach(string key in keys)
            {
                var clips=new AudioClip[3];
                for(int i=0;i<clips.Length;i++)clips[i]=Resources.Load<AudioClip>("Trial/Audio/"+key+"_"+i);
                if(clips[0]){combatVariants[key]=clips;sounds[key]=clips[0];}
            }
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
