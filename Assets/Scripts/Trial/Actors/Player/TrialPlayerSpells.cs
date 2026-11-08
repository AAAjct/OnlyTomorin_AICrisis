using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    internal enum TrialCoffinStage : byte { Rise, BreakShell, ShatterEnemy, DamageBoss }

    public sealed partial class TrialPlayer
    {
        public const int SpellCount=2,MaxSpellEnergy=300;
        const int KillEnergy=75,ClashEnergy=45,ParryEnergy=60,DodgeEnergy=36;
        internal const float CoffinRiseDuration=.72f,CoffinSealDuration=1.02f;
        internal const float MirrorGlassDuration=.68f;
        internal const float CoffinBossDamage=.12f;
        const float MirrorBossDamage=.05f;
        internal const byte MirrorExecutionStyle=2,MirrorBossImpactStyle=3;
        const float MirrorCounterDuration=2f,MirrorSightDot=.5735764f; // 55 degrees to either side.
        internal const string MirrorPhrase="碎裂吧，镜花水月";
        static readonly int[] SpellCosts={MaxSpellEnergy,MaxSpellEnergy/2};
        internal static int SpellCost(int slot)=>SpellCosts[slot];

        public int SpellEnergy { get; private set; }
        public bool Chanting => Alive && chantRemaining>0;
        internal float ChantTime => game.Effects.CoffinChantDuration-chantRemaining;
        internal bool MirrorCounterReady => Alive && Time.time<mirrorCounterUntil;
        float chantRemaining;
        float mirrorCounterUntil;
        float mirrorStageRemaining;
        bool mirrorGlassBroken;
        ushort mirrorBlink;
        static readonly float[] MirrorBlinkAngles={0,30,-30,58,-58,78,-78};
        readonly List<TrialEnemy> mirrorTargets=new List<TrialEnemy>();

        internal void AwardSpellEnergyForKill()=>AwardSpellEnergy(KillEnergy);
        void AwardSpellEnergy(int amount)
        {
            if(chantRemaining>0)return;
            SpellEnergy=Mathf.Min(MaxSpellEnergy,SpellEnergy+amount);
        }

        void CastSpells(byte slots)
        {
            if(slots==0 || SpellControlled || Action==FighterAction.Stagger)return;
            for(int slot=0;slot<SpellCount;slot++)
            {
                if((slots&(1<<slot))==0 || SpellEnergy<SpellCosts[slot] || (slot==0?Chanting:mirrorStageRemaining>0))continue;
                SpellEnergy-=SpellCosts[slot];
                if(slot==0)chantRemaining=game.Effects.CoffinChantDuration;
                else ReleaseMirror();
                return;
            }
        }

        internal void InterruptForSpell()
        {
            ClearAttackInputs();
            ClearDodgeLink();
            chainUntil=weaveUntil=0;
            weavePending=false;
            bufferedDodge=bufferedJump=bufferedHeal=-10;
            PlanarVelocity=combatImpulse=Vector3.zero;
            LocalMotion=Vector2.zero;
            MoveBlend=0;
            Sprinting=false;
        }

        void TickCoffinChant()
        {
            if(chantRemaining<=0)return;
            // The voice plays in real time, including during combat hit-stop.
            chantRemaining=Mathf.Max(0,chantRemaining-Time.unscaledDeltaTime);
            if(chantRemaining==0)ReleaseCoffins();
        }

        void ReleaseCoffins()
        {
            int sealedCount=0;
            // A completed chant seals eligible enemies across the current map.
            foreach(var enemy in game.Enemies)
                if(enemy && enemy.SealInCoffin(this,sealedCount*.055f))
                {
                    sealedCount++;
                    PlayCoffinCue(enemy,TrialCoffinStage.Rise);
                }
        }

        internal void PlayCoffinCue(TrialEnemy enemy,TrialCoffinStage stage)
        {
            Vector3 size=enemy.Boss?new Vector3(5.7f,4.6f,5.7f):enemy.IsBoss?new Vector3(2.6f,3.35f,2.6f):Vector3.zero;
            SpellCue(TrialCombatCue.BlackCoffin,enemy.transform.position,end:size,target:enemy,style:(byte)stage);
        }

        void ReleaseMirror()
        {
            mirrorCounterUntil=Time.time+MirrorCounterDuration;
            mirrorStageRemaining=game.Effects.MirrorLeadIn;
            mirrorGlassBroken=false;
            Vector3 eyes=transform.position+Vector3.up*1.4f;
            Vector3 forward=transform.forward;
            SpellCue(TrialCombatCue.MirrorCast,eyes);
            mirrorTargets.Clear();
            foreach(var enemy in game.Enemies)
            {
                if(!enemy)continue;
                bool eligible=enemy.IsBoss?enemy.CanBeSpellDamaged:enemy.CanBeSpellExecuted && enemy.Health*3<=enemy.MaxHealth*2;
                if(!eligible)continue;
                Vector3 center=enemy.transform.position+Vector3.up;
                Vector3 bearing=Vector3.ProjectOnPlane(center-eyes,Vector3.up).normalized;
                if((bearing!=Vector3.zero && Vector3.Dot(forward,bearing)<MirrorSightDot) ||
                    Physics.Linecast(eyes,center,1,QueryTriggerInteraction.Ignore))continue;
                mirrorTargets.Add(enemy);
            }
        }

        void TickMirrorRelease()
        {
            if(mirrorStageRemaining<=0)return;
            mirrorStageRemaining=Mathf.Max(0,mirrorStageRemaining-Time.unscaledDeltaTime);
            if(mirrorStageRemaining>0 || mirrorGlassBroken)return;
            mirrorGlassBroken=true;
            // Keep the current glass animation available to finish before accepting a new cast.
            mirrorStageRemaining=MirrorGlassDuration+.05f;
            SpellCue(TrialCombatCue.MirrorScreenBreak,transform.position+Vector3.up*1.4f);

            for(int i=0;i<mirrorTargets.Count;i++)
            {
                var enemy=mirrorTargets[i];
                if(enemy && enemy.IsBoss)
                {
                    mirrorTargets[i]=null;
                    if(enemy.DamageWithSpell(this,MirrorBossDamage,24,awardSpellEnergy:true))
                        SpellCue(TrialCombatCue.MirrorBreak,enemy.transform.position+Vector3.up*1.25f,target:enemy,style:MirrorBossImpactStyle);
                }
                else if(enemy && enemy.ExecuteWithSpell())
                    SpellCue(TrialCombatCue.MirrorBreak,enemy.transform.position+Vector3.up,target:enemy,style:MirrorExecutionStyle);
                else mirrorTargets[i]=null;
            }
            foreach(var enemy in mirrorTargets)if(enemy)game.OnEnemyDied(enemy,this);
            mirrorTargets.Clear();
        }

        bool TryMirrorCounter(TrialFighter attacker)
        {
            if(!MirrorCounterReady || !attacker || attacker==this)return false;
            Vector3 behind=Vector3.ProjectOnPlane(-attacker.transform.forward,Vector3.up).normalized;
            Vector3 center=attacker.transform.position;
            float distance=(attacker is TrialEnemy enemy?enemy.BodyRadius:.28f)+motor.radius+.65f;
            foreach(float angle in MirrorBlinkAngles)
            {
                Vector3 destination=center+Quaternion.AngleAxis(angle,Vector3.up)*behind*distance;
                if(!Physics.Raycast(destination+Vector3.up*1.1f,Vector3.down,out var floor,4,1,QueryTriggerInteraction.Ignore) ||
                    Vector3.Angle(floor.normal,Vector3.up)>motor.slopeLimit)continue;
                destination=floor.point+Vector3.up*.04f;
                float radius=motor.radius+motor.skinWidth;
                Vector3 bottom=destination+Vector3.up*(radius+.04f);
                Vector3 top=destination+Vector3.up*(motor.height-radius);
                if(Physics.CheckCapsule(bottom,top,radius,1,QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(center+Vector3.up,destination+Vector3.up,1,QueryTriggerInteraction.Ignore))continue;

                unchecked{mirrorBlink++;}
                Vector3 origin=transform.position;
                InterruptForSpell();
                Teleport(destination);
                Turn(center-destination,360,1);
                Begin(FighterAction.Free,0);
                EmitSwordCue(TrialCombatCue.EmpoweredStep,origin+Vector3.up,transform.forward,destination+Vector3.up,1);
                return true;
            }
            return false;
        }

        void SpellCue(TrialCombatCue cue,Vector3 point,Vector3 end=default,TrialFighter target=null,byte style=0)
        {
            int id=target?game.SpellTargetId(target):0;
            if(Mirror.NetworkServer.active)
                TrialNetworkManager.Current.BroadcastSwordCombat(this,cue,point,transform.forward,end,0,style,id);
            else game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=point,forward=transform.forward,end=end,style=style,target=id},this==game.Player);
        }

        void CaptureSpells(ref TrialActorState state)
        {
            state.spellEnergy=(ushort)SpellEnergy;
            state.chantRemaining=chantRemaining;
            state.mirrorCounter=Mathf.Max(0,mirrorCounterUntil-Time.time);
            state.mirrorBlink=mirrorBlink;
        }

        void ApplySpells(TrialActorState state)
        {
            SpellEnergy=state.spellEnergy;
            chantRemaining=state.chantRemaining;
            mirrorCounterUntil=Time.time+state.mirrorCounter;
            mirrorBlink=state.mirrorBlink;
        }

        internal void ResetSpellProgress()=>SpellEnergy=0;

        void ResetSpells(bool lostLife)
        {
            chantRemaining=0;
            mirrorCounterUntil=0;
            mirrorStageRemaining=0;
            mirrorTargets.Clear();
            if(this==game.Player && game.Hud)game.Hud.ClearMirrorScreen();
            ClearSpellControl();
            if(lostLife)ResetSpellProgress();
        }
    }
}
