using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialChampion
    {
        public bool IsRemnant=>Kind==ChampionKind.Remnant;
        public bool Regrouping=>respite>0;
        public int KillStacks {get;private set;}
        public float KillHealthScale=>1+KillStacks*.18f;
        public float KillDamageScale=>1+KillStacks*.12f;
        public float KillPostureScale=>1+KillStacks*.10f;
        public bool Evading=>Mobility==ChampionMobility.SideSlip && mobilityAge>=.035f && mobilityAge<=.23f && Planar(transform.position-evasionOrigin).sqrMagnitude>.035f;
        float respite,pursuitAge,guardTime,guardCooldown;
        bool guardCounter;
        Vector3 evasionOrigin;
        public float RespiteRemaining=>respite;

        void SelectRemnantTarget()
        {
            TrialPlayer best=null;float highest=float.MinValue;
            foreach(var player in targets)
            {
                if(!player || !player.Alive || player.ChoosingBoon)continue;
                float score=12-Planar(player.transform.position-transform.position).magnitude*.5f;
                score+=(1-player.Health/player.MaxHealth)*7+(1-player.Stamina/player.MaxStamina)*3;
                if(player.CommandHeal || player.Action==FighterAction.Heal)score+=8;
                if(score>highest){highest=score;best=player;}
            }
            if(best)game.SetCombatTarget(best);
        }

        void ResetTactics()
        {respite=pursuitAge=guardTime=guardCooldown=0;guardCounter=false;KillStacks=0;fighter.BossGuard(false);}

        void BeginRespite()
        {
            respite=IsRemnant?1.0f:Kind==ChampionKind.Taffy?2.1f:1.65f;
            if(sequence%3==0)respite+=IsRemnant?.35f:.65f;
            cooldown=Mathf.Max(cooldown,respite);pursuitAge=0;orbitSide=-orbitSide;
            queuedMove=-1;guardTime=0;guardCounter=false;fighter.BossGuard(false);
        }

        bool TickRespite(float dt,Vector3 delta)
        {
            if(respite<=0)return false;
            // Let existing projectiles expire before counting the quiet interval.
            var partner=Partner();
            bool partnerFinishing=partner && partner.Alive && partner.Champion.Attacking;
            if(ActiveHazards==0 && !partnerFinishing)respite=Mathf.Max(0,respite-dt);
            cooldown=Mathf.Max(cooldown,respite+.08f);
            float distance=delta.magnitude;
            Vector3 radial=delta.normalized,tangent=Vector3.Cross(Vector3.up,radial)*orbitSide;
            Vector3 direction=distance<5.5f?-radial+tangent*.45f:tangent*.55f;
            if(distance>10)direction=Vector3.zero;
            SafeTacticalStep(direction,IsRemnant?3.6f:2.5f,dt);
            fighter.BossFace(delta,210,dt);return true;
        }

        void SafeTacticalStep(Vector3 direction,float speed,float dt)
        {
            direction=direction.normalized;
            Vector3 next=transform.position+direction*speed*dt;
            if(next.x<-21 || next.x>21 || next.z<4 || next.z>46 || Physics.SphereCast(transform.position+Vector3.up,.5f,direction,out _,1,1,QueryTriggerInteraction.Ignore))
            {orbitSide=-orbitSide;fighter.BossMotion(0);return;}
            fighter.BossMove(direction*speed,dt);fighter.BossMotion(direction.sqrMagnitude>.01f?1:0);
        }

        bool TickSwordDefense(float dt,TrialPlayer target,float distance)
        {
            guardCooldown=Mathf.Max(0,guardCooldown-dt);
            if(guardTime>0)
            {
                guardTime=Mathf.Max(0,guardTime-dt);fighter.BossFace(target.transform.position-transform.position,540,dt);fighter.BossMotion(0);
                if(guardTime<=0)
                {
                    fighter.BossGuard(false);
                    if(guardCounter && distance<5.5f){guardCounter=false;BeginMove(IsRemnant?1:0);}
                }
                return true;
            }
            if(Kind!=ChampionKind.Anon && !IsRemnant)return false;
            if(guardCooldown>0 || distance>5.2f || target.Invulnerable)return false;
            bool light=target.CommandLight || target.Action==FighterAction.Light && target.Progress<.32f;
            if(!light || target.CommandHeavy || target.Action==FighterAction.Heavy || target.Action==FighterAction.Charge)return false;
            if(Vector3.Angle(transform.forward,target.transform.position-transform.position)>80)return false;
            guardTime=IsRemnant?.38f:.55f;guardCooldown=IsRemnant?1.1f:1.85f;guardCounter=false;
            fighter.BossGuard(true);fighter.BossMotion(0);return true;
        }

        public bool TryGuardHit(TrialFighter attacker,bool unblockable)
        {
            if(!fighter.Guarding || guardTime<=0 || unblockable || !attacker || fighter.Vulnerable)return false;
            if(attacker.Action==FighterAction.Heavy || Vector3.Angle(transform.forward,attacker.transform.position-transform.position)>85)
            {guardTime=0;fighter.BossGuard(false);return false;}
            guardCounter=true;
            if(Kind==ChampionKind.Anon && attacker is TrialPlayer player)
            {player.ApplyCombatImpulse(Planar(player.transform.position-transform.position).normalized*12);game.Effects.SignatureGlyph(transform.position,Kind,2.3f,8);}
            game.Effects.Burst(transform.position+Vector3.up,TrialChampionCatalog.Color(Kind),16);
            game.Effects.Sound("parry",transform.position);return true;
        }

        public void AwardPlayerKill()
        {
            if(!IsRemnant || !fighter.Alive || !Mirror.NetworkServer.active)return;
            KillStacks++;fighter.ApplyKillGrowth();
            game.Hud.Notify("凑企鹅残躯 · 吞噬 "+KillStacks+" · 生命与战力增长",2.5f);
            game.Effects.Burst(transform.position+Vector3.up,TrialChampionCatalog.Color(Kind),45);
        }
    }
}
