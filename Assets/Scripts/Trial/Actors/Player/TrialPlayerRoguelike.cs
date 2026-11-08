using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        readonly int[] affixes=new int[(int)TrialAffix.Count];
        public int RunLevel {get;private set;}=1;
        public int RunExperience {get;private set;}
        public int ExperienceRequired=>50+(RunLevel-1)*25;
        public int PendingRunChoices {get;private set;}
        public readonly TrialAffix[] RunOffers=new TrialAffix[3];
        public bool ChoosingRunAffix=>game.IsEndless && runChoiceOpen && PendingRunChoices>0;
        float leechAt,vacuumAt,waveAt,lightningAt,thornsAt,dodgeNovaAt,momentumUntil;
        int momentum;
        bool affixDamage,runChoiceOpen,hasRunOffers;
        public int AffixStacks(TrialAffix affix)=>game.IsEndless?affixes[(int)affix]:0;
        public void RestoreForBossRound(Vector3 respawnPoint)
        {
            int remaining=Flasks;var rotation=transform.rotation;
            // Survivors recover where they won; only fallen teammates need a respawn.
            var point=Alive?transform.position:respawnPoint;
            Restore(point);transform.rotation=rotation;Flasks=remaining;
        }
        public void ResetRoguelike()
        {System.Array.Clear(affixes,0,affixes.Length);RunLevel=1;RunExperience=PendingRunChoices=momentum=0;leechAt=vacuumAt=waveAt=lightningAt=thornsAt=dodgeNovaAt=momentumUntil=0;affixDamage=runChoiceOpen=hasRunOffers=false;}
        public void AwardRunExperience(int amount)
        {
            if(!game.IsEndless || !Alive)return;
            RunExperience+=Mathf.RoundToInt(amount*(1+.15f*AffixStacks(TrialAffix.Experience)));
            while(RunExperience>=ExperienceRequired)
            {RunExperience-=ExperienceRequired;RunLevel++;PendingRunChoices++;}

        }
        public void RequestRunUpgrade()
        {if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.RunUpgrade(-1);else OpenRunUpgrade();}
        public void RequestCloseRunUpgrade()
        {if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.RunUpgrade(-2);else CloseRunUpgrade();}
        public void RequestRunAffix(int slot)
        {if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.RunUpgrade(slot);else ChooseRunAffix(slot);}
        void CaptureRoguelike(ref TrialActorState state)
        {if(!game.IsEndless)return;state.runLevel=RunLevel;state.runExperience=RunExperience;state.pendingRunChoices=PendingRunChoices;state.runChoiceOpen=runChoiceOpen;state.runOffers=(TrialAffix[])RunOffers.Clone();state.runAffixes=(int[])affixes.Clone();}
        void ApplyRoguelike(TrialActorState state)
        {
            if(!game.IsEndless)return;
            bool wasOpen=ChoosingRunAffix;RunLevel=Mathf.Max(1,state.runLevel);RunExperience=state.runExperience;PendingRunChoices=state.pendingRunChoices;runChoiceOpen=state.runChoiceOpen;
            if(state.runOffers!=null)System.Array.Copy(state.runOffers,RunOffers,Mathf.Min(3,state.runOffers.Length));
            if(state.runAffixes!=null)System.Array.Copy(state.runAffixes,affixes,Mathf.Min(affixes.Length,state.runAffixes.Length));
            if(this==game.Player && wasOpen!=ChoosingRunAffix){game.Input.SuppressCombat();Cursor.lockState=ChoosingRunAffix?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=ChoosingRunAffix;}
        }
        public bool OpenRunUpgrade()
        {
            if(!game.IsEndless || !Alive || game.Mode!=TrialMode.Playing || PendingRunChoices<=0 || runChoiceOpen)return false;
            runChoiceOpen=true;
            StopLocomotion();PlanarVelocity=Vector3.zero;LocalMotion=Vector2.zero;Sprinting=false;TurnLean=0;
            if(!hasRunOffers)RollAffixes();
            if(this!=game.Player)return true;
            if(!TrialNetworkManager.SessionActive)Time.timeScale=0;game.Input.ResetSprint();game.Input.SuppressCombat();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        public void CloseRunUpgrade()
        {
            if(!ChoosingRunAffix)return;
            runChoiceOpen=false;
            RefreshRunChoiceControls();
        }
        void RollAffixes()
        {
            hasRunOffers=true;
            // Three distinct eligible entries; unlimited core stats keep the pool viable forever.
            for(int slot=0;slot<3;slot++)
            {
                int count=0;
                for(int i=0;i<(int)TrialAffix.Count;i++)
                {
                    var candidate=(TrialAffix)i;int cap=TrialAffixCatalog.MaxStacks(candidate);
                    if(cap>0 && affixes[i]>=cap)continue;
                    bool duplicate=false;for(int j=0;j<slot;j++)if(RunOffers[j]==candidate)duplicate=true;
                    if(!duplicate && Random.Range(0,++count)==0)RunOffers[slot]=candidate;
                }
            }
        }
        public bool ChooseRunAffix(int slot)
        {
            if(!ChoosingRunAffix || slot<0 || slot>=3)return false;
            var affix=RunOffers[slot];affixes[(int)affix]++;
            if(affix==TrialAffix.Vitality){MaxHealth+=25;Health=Mathf.Min(MaxHealth,Health+25);}
            if(affix==TrialAffix.Stamina){MaxStamina+=50;Stamina=Mathf.Min(MaxStamina,Stamina+50);}
            PendingRunChoices--;
            if(PendingRunChoices>0)RollAffixes();else runChoiceOpen=hasRunOffers=false;
            RefreshRunChoiceControls();
            if(this==game.Player)game.Hud.Notify("获得词条 · "+TrialAffixCatalog.Name(affix),2);
            return true;
        }
        void RefreshRunChoiceControls()
        {
            if(this!=game.Player)return;
            game.Input.ResetSprint();game.Input.SuppressCombat();
            Cursor.lockState=ChoosingRunAffix?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=ChoosingRunAffix;
            if(!TrialNetworkManager.SessionActive)Time.timeScale=ChoosingRunAffix?0:1;
        }
        public float ModifyRunDamage(TrialEnemy target,float damage,ref float posture)
        {
            if(!game.IsEndless || affixDamage)return damage;
            float scale=1+.1f*AffixStacks(TrialAffix.Edge);
            if(Health<MaxHealth*.5f)scale+=.15f*AffixStacks(TrialAffix.Berserker);
            if(target.Health<target.MaxHealth*.3f)scale+=.2f*AffixStacks(TrialAffix.Execution);
            if(target.IsBoss)scale+=.12f*AffixStacks(TrialAffix.EliteHunter);
            if(Time.time<momentumUntil)scale+=momentum*.03f*AffixStacks(TrialAffix.Momentum);
            if(Action==FighterAction.Heavy){scale+=.15f*AffixStacks(TrialAffix.HeavyImpact);posture*=1+.15f*AffixStacks(TrialAffix.HeavyImpact);}
            if(Random.value<.08f*AffixStacks(TrialAffix.Critical))scale*=1.6f;
            posture*=1+.15f*AffixStacks(TrialAffix.Breaker);return damage*scale;
        }
        public void OnRunHit(TrialEnemy target,float dealt)
        {
            if(!game.IsEndless || affixDamage || dealt<=0)return;
            if(Time.time>=leechAt){HealRun(AffixStacks(TrialAffix.Leech));leechAt=Time.time+.25f;}
            int fire=AffixStacks(TrialAffix.Burn);if(fire>0 && target.Alive)target.ApplyRunBurn(this,fire*5);
            int frost=AffixStacks(TrialAffix.Frost);if(frost>0 && target.Alive)target.ApplyRunSlow(.18f*frost);
            bool light=Action==FighterAction.Light && Combo<=3;
            int pull=AffixStacks(TrialAffix.Vacuum);
            if(light && pull>0 && Time.time>=vacuumAt)
            {
                vacuumAt=Time.time+1;
                foreach(var enemy in game.Enemies)if(enemy.Alive && !enemy.IsBoss && enemy!=target)
                {var delta=target.transform.position-enemy.transform.position;delta.y=0;if(delta.sqrMagnitude<36)enemy.PullForRun(delta.normalized*Mathf.Min(delta.magnitude,1.5f+.5f*pull));}
                game.Effects.Burst(target.transform.position+Vector3.up,new Color(.35f,.9f,1),24);
            }
            int wave=AffixStacks(TrialAffix.Shockwave);
            if(light && wave>0 && Time.time>=waveAt){waveAt=Time.time+1.2f;RunAreaDamage(target.transform.position,3.5f,12*wave,target);}
            int lightning=AffixStacks(TrialAffix.ChainLightning);
            if(lightning>0 && Time.time>=lightningAt)
            {
                lightningAt=Time.time+1;int chains=0;
                foreach(var enemy in game.Enemies)if(enemy!=target && enemy.Alive && (enemy.transform.position-target.transform.position).sqrMagnitude<49)
                {DealAffixDamage(enemy,14*lightning);game.Effects.Burst(enemy.transform.position+Vector3.up,new Color(.5f,.7f,1),10);if(++chains>=3)break;}
            }
        }
        public void OnRunKill(TrialEnemy enemy)
        {
            if(!game.IsEndless)return;
            HealRun(4*AffixStacks(TrialAffix.BloodHarvest));RestoreCombatStamina(25*AffixStacks(TrialAffix.SecondWind));
            momentum=Time.time<momentumUntil?Mathf.Min(10,momentum+1):1;momentumUntil=Time.time+5;
            if(!affixDamage && AffixStacks(TrialAffix.KillBlast)>0)RunAreaDamage(enemy.transform.position,4,24*AffixStacks(TrialAffix.KillBlast),enemy);
        }
        internal void DealRunEffectDamage(TrialEnemy enemy,float damage)=>DealAffixDamage(enemy,damage);
        void DealAffixDamage(TrialEnemy enemy,float damage)
        {bool previous=affixDamage;affixDamage=true;try{enemy.ReceiveHit(this,damage,0);}finally{affixDamage=previous;}}
        void RunAreaDamage(Vector3 point,float radius,float damage,TrialEnemy ignore=null)
        {
            foreach(var enemy in game.Enemies)if(enemy!=ignore && enemy.Alive && (enemy.transform.position-point).sqrMagnitude<radius*radius)DealAffixDamage(enemy,damage);
            game.Effects.Burst(point+Vector3.up,new Color(.45f,.8f,1),20);
        }
        void HealRun(float amount){if(Alive && amount>0)Health=Mathf.Min(MaxHealth,Health+amount);}
        void TickRoguelike(float dt){if(game.IsEndless)HealRun(.6f*AffixStacks(TrialAffix.Regeneration)*dt);}
        float RunDefense(float damage)=>damage*(1-Mathf.Min(.48f,.06f*AffixStacks(TrialAffix.Armor)))*(Health<MaxHealth*.3f?1-Mathf.Min(.6f,.12f*AffixStacks(TrialAffix.LastBastion)):1);
        void RunParry(){HealRun(8*AffixStacks(TrialAffix.ParryHeal));}
        void RunDodge(){int stacks=AffixStacks(TrialAffix.DodgeNova);if(stacks>0 && Time.time>=dodgeNovaAt){dodgeNovaAt=Time.time+2;RunAreaDamage(transform.position,4,16*stacks);}}
        void RunRetaliate(TrialFighter attacker)
        {if(attacker is TrialEnemy enemy && enemy.Alive && AffixStacks(TrialAffix.Thorns)>0 && Time.time>=thornsAt){thornsAt=Time.time+.5f;DealAffixDamage(enemy,18*AffixStacks(TrialAffix.Thorns));}}
    }
}
