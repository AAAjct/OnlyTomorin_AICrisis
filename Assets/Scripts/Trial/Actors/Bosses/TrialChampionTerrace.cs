using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialChampion
    {
        internal enum TerraceCut { Dash, Pair, Return, Delay, Side, Brace, Riposte, Chase, Sweep, Charge, Downbeat, FinalThrust, Wheel }
        enum TerraceOutcome { Miss, Evaded, Hit, Blocked, Deflected }

        readonly struct TerraceAttack
        {
            public readonly ChampionMove Move;
            public readonly float Active, Lock, Travel, HalfWidth, Hurt;
            public TerraceAttack(string name, string counter, float windup, float active, float recovery,
                float aimLock, float reach, float angle, float travel, float damage, float hurt=.23f, float halfWidth=0, bool peril=false)
            {
                Move=new ChampionMove(name,counter,halfWidth>0?ChampionShape.Thrust:ChampionShape.Cut,
                    peril?ChampionDefense.Dodge:ChampionDefense.Deflect,windup,active,recovery,reach,0,angle,damage,1);
                Active=active; Lock=aimLock; Travel=travel; HalfWidth=halfWidth; Hurt=hurt;
            }
        }

        static readonly TerraceAttack[] terraceAttacks=
        {
            new("踏线挑斩","读落脚拼刀 / 锁线后横移",.68f,.14f,.65f,.26f,2.2f,65,6.7f,14),
            new("对拍双斩 · 平切","看左肩下沉 / 侧闪",.36f,.11f,.65f,.16f,2.4f,95,.9f,11),
            new("对拍双斩 · 快返","读右肘贴身",.34f,.12f,.70f,.12f,2.3f,85,.4f,13),
            new("悬拍落刃","悬刀时等沉肩 / 横移",.72f,.13f,.95f,.36f,2.7f,55,.65f,19,.32f),
            new("换肩切","等横跨锁向后穿身",.48f,.12f,.75f,.23f,2.3f,85,.55f,15,.26f),
            new("架刃回手 · 架势","停轻击 / 重击破架或绕侧",.52f,0,.60f,.12f,2.3f,100,0,0),
            new("架刃回手 · 反斩","看沉肩再拼刀 / 侧闪",.43f,.12f,.72f,.16f,2.35f,92,1.25f,18),
            new("赶步贯刺","横移离开刺线 / 拼刀",.64f,.34f,.90f,.25f,2.4f,0,5.4f,18,.38f,.55f),
            new("绊足弧","低刀危险 · 穿滚 / 离开扇区",.76f,.15f,1,.33f,3.2f,145,.25f,23,.65f,peril:true),
            new("折刃蓄斩","重击或两轻可截断 / 沉肩后防御",.86f,.14f,1.05f,.62f,2.7f,115,1.1f,24,.38f),
            new("双落点 · 竖斩","读双点脚步 / 侧闪",.46f,.11f,.85f,.24f,2.4f,65,.45f,13),
            new("双落点 · 平刺","收肘后侧闪 / 拼刀",.45f,.30f,1.05f,.18f,2.5f,0,.85f,19,.35f,.55f),
            new("掠身回旋","读低身跨步 / 拼刀或退开",.53f,.19f,.84f,.25f,2.65f,155,1.7f,17,.28f)
        };

        public bool TerraceDuel=>Kind==ChampionKind.Anon && game;
        internal float DuelWallDistance(Vector3 point)=>game.World.DuelWallDistance(point);
        public bool TerraceChangingPhase=>TerraceDuel && phaseAge>0;
        internal float TerraceSideDirection=>terraceSideSign?1:-1;
        internal int TerraceClashCount=>terraceStreak;
        public float TerracePhaseProgress=>Mathf.Clamp01(phaseAge/2.10f);
        internal bool TerraceActive=>TerraceDuel && Attacking && Age>=terraceWindup && Age<TerraceEnd && Cut!=TerraceCut.Brace;
        internal float TerraceHurtTime=>TerraceMove.Hurt;
        internal bool TerraceDrivingThrust=>TerraceActive && (Cut==TerraceCut.Chase || Cut==TerraceCut.FinalThrust);
        internal bool TerraceCanFlinch=>!Attacking || Recovering || WindingUp && Age<TerraceMove.Lock && Cut!=TerraceCut.Brace;
        internal float TerraceGuardCost=>Move.damage>=18?.24f:.18f;
        TerraceAttack TerraceMove=>terraceAttacks[Mathf.Clamp(MoveIndex,0,terraceAttacks.Length-1)];
        TerraceCut Cut=>(TerraceCut)MoveIndex;
        float TerraceEnd=>terraceWindup+TerraceMove.Active;
        bool TerraceLightPressure=>terraceLights>=3 && terraceClock-terraceLastLight<=2;

        TrialPlayer terraceTarget, terraceDriveTarget;
        float terraceClock, terraceWindup, terraceRecovery, terracePressureAt;
        float terraceRoundStart, terraceTravel, terraceTravelApplied, terraceRetreatUntil, terraceLastLight=-10;
        float terraceStaggerSlide;
        int terraceHits, terraceStreak, terraceLights, terraceChargeHits, terraceOpener=-1, terraceLastOpener=-1;
        int terraceRound;
        TerraceOutcome terraceOutcome;
        bool terraceGuardBroken, terraceSideSign;
        Vector3 terraceInitialHeading, terraceStep, terraceSide, terraceRecoil;

        void ResetTerrace()
        {
            ResetTerraceDecisions();
            terraceTarget=terraceDriveTarget=null;
            terraceRecoil=Vector3.zero;
            terraceClock=terraceWindup=terraceRecovery=terracePressureAt=0;
            terraceRoundStart=terraceTravel=terraceTravelApplied=terraceStaggerSlide=0;
            terraceHits=terraceStreak=terraceChargeHits=terraceRound=0;
            terraceOpener=terraceLastOpener=-1;
            terraceOutcome=TerraceOutcome.Miss;
            terraceGuardBroken=terraceSideSign=false;
            game.World.SetTerracePhase(1,0);
        }

        void TickTerrace(float dt)
        {
            terraceClock+=dt;
            if(!fighter.Awakened){HideWarnings();return;}
            TickTerraceRecoil(dt);
            if(fighter.Vulnerable)
            {
                if(terraceStaggerSlide>0)
                {
                    float step=Mathf.Min(terraceStaggerSlide,2.6f*dt);
                    Vector3 slide=game.World.ClipDuelStep(transform.position,-transform.forward*step,.55f);
                    fighter.BossMove(slide/Mathf.Max(.001f,dt),dt);
                    terraceStaggerSlide-=step;
                }
                if(fighter.ActionTime>=fighter.ActionDuration)fighter.BossBegin(FighterAction.Free,0);
                return;
            }

            var target=Attacking?terraceTarget:game.CombatTarget;
            if(!target || !target.Alive)
            {
                CancelAttack();
                return;
            }

            Vector3 delta=Planar(target.transform.position-transform.position);
            float distance=delta.magnitude;
            ObserveTerrace(target,delta,dt);
            if(terraceClock-terracePressureAt>3)fighter.DecayTerracePosture(10*dt);
            if(Attacking){TickTerraceCut(dt,delta);return;}
            if(terraceRound==0 && distance<=8.5f)
            {
                BeginTerraceRound(TerraceCut.Dash,target);
                return;
            }

            if(Phase==1 && fighter.Health<=fighter.MaxHealth*.5f)
            {
                if(phaseAge==0)
                {
                    fighter.BossBegin(FighterAction.Free,0);
                    game.Hud.Notify("爱音 · 双落点",2.1f);
                }
                fighter.BossMotion(0);
                float before=phaseAge;
                phaseAge+=dt;
                if((before<.45f && phaseAge>=.45f) || (before<.78f && phaseAge>=.78f))
                    game.Effects.Sound("block",transform.position,.76f);
                if(phaseAge>=2.10f){Phase=2;phaseAge=0;cooldown=.18f;}
                return;
            }

            if(terraceRetreatUntil>terraceClock)
            {
                StepTerraceBody(terraceSide*(4.5f*dt),dt);
                fighter.BossFace(delta,520,dt);
                fighter.BossMotion(1);
                return;
            }

            fighter.BossFace(delta,690,dt);
            cooldown-=dt;
            if(cooldown<=0 && terraceClock>=terraceNextDecision)
            {
                terraceNextDecision=terraceClock+.09f;
                var next=ChooseTerraceOpener(target,delta,distance);
                if(next.HasValue){BeginTerraceRound(next.Value,target);return;}
            }

            MoveTerraceNeutral(target,delta,distance,dt);
        }

        bool TerracePlayerPinned(TrialPlayer target)=>DuelWallDistance(target.transform.position)<2;

        void BeginTerraceRound(TerraceCut move,TrialPlayer target)
        {
            terraceRound++;
            terraceHits=terraceStreak=terraceChargeHits=0;
            terraceRoundStart=terraceClock;
            terraceTarget=target;
            terraceOpener=(int)move;
            terraceLastOpener=terraceOpener;
            BeginTerraceCut(move,false);
        }

        void BeginTerraceCut(TerraceCut cut,bool followup)
        {
            Vector3 previousHeading=heading;
            MoveIndex=(int)cut;
            Age=0; beat=0; AttacksStarted++;
            terraceWindup=TerraceMove.Move.windup+(cut==TerraceCut.Pair && !followup?.08f:0);
            terraceRecovery=Move.recovery;
            terraceOutcome=TerraceOutcome.Miss;
            terraceGuardBroken=false;
            terraceDriveTarget=null;
            contacted.Clear();
            terraceTravel=terraceTravelApplied=0;
            origin=transform.position;
            aim=terraceTarget.transform.position;
            terraceInitialHeading=followup?previousHeading:Planar(aim-origin).normalized;
            if(terraceInitialHeading.sqrMagnitude<.01f)terraceInitialHeading=transform.forward;
            heading=terraceInitialHeading;
            terraceStep=heading;
            terraceSideSign=cut switch
            {
                TerraceCut.Wheel or TerraceCut.Side=>TerraceOpenSide(heading)>0,
                TerraceCut.Pair=>terraceRound%2==0,
                TerraceCut.Return=>terraceRound%2!=0,
                _=>Vector3.SignedAngle(heading,Planar(aim-origin),Vector3.up)>=0
            };
            fighter.BossBegin(FighterAction.Heavy,TerraceEnd+terraceRecovery);
            fighter.BossMotion(0);
            fighter.BossGuard(cut==TerraceCut.Brace);
            RememberTerraceCut(cut);
            if(cut==TerraceCut.Brace)terraceLights=0;
            PlayTerraceTell();
            DrawTerraceCut();
        }

        void PlayTerraceTell()
        {
            string cue=Cut==TerraceCut.Sweep?"heavy":Cut==TerraceCut.Brace || Cut==TerraceCut.Delay?"block":
                Cut==TerraceCut.Charge?"charge":Cut==TerraceCut.Return?"swing":"warning";
            float pitch=Cut==TerraceCut.Delay?.65f:Cut==TerraceCut.Sweep?.72f:Cut==TerraceCut.Downbeat?1.40f:1;
            game.Effects.Sound(cue,transform.position,pitch);
        }

        void TickTerraceCut(float dt,Vector3 delta)
        {
            float before=Age;
            Age+=dt;
            if(before<TerraceMove.Lock)
            {
                float max=Cut==TerraceCut.Side?45:Cut==TerraceCut.FinalThrust?25:terraceOpener==MoveIndex?95:35;
                Vector3 lead=(Cut==TerraceCut.Dash || Cut==TerraceCut.Chase) && duel.Action==FighterAction.Free
                    ?Vector3.ClampMagnitude(terraceTarget.PlanarVelocity*.08f,.45f):Vector3.zero;
                Vector3 aimDelta=delta+lead;
                if(aimDelta.sqrMagnitude>.005f)
                    heading=Vector3.RotateTowards(terraceInitialHeading,aimDelta.normalized,max*Mathf.Deg2Rad,0);
                if(Age>=TerraceMove.Lock)CommitTerraceCut(delta);
            }
            if(before<terraceWindup)
            {
                fighter.BossFace(heading,3000,dt);
                if(Cut==TerraceCut.Dash && before<.58f && Age>=.58f ||
                    Cut==TerraceCut.Delay && before<.53f && Age>=.53f ||
                    Cut==TerraceCut.Downbeat && before<.14f && Age>=.14f)
                    game.Effects.Sound("landing",transform.position,1.2f);
            }
            MoveTerraceCut(dt);
            if(Cut!=TerraceCut.Brace && Age>=terraceWindup && before<TerraceEnd)
            {
                if(beat==0)
                {
                    beat=1; terraceHits++;
                    game.Effects.Sound("swing",transform.position,1.1f);
                }
                int cut=MoveIndex;
                if(Age<TerraceEnd)ContactTerrace();
                if(MoveIndex!=cut)return;
                if(TerraceDrivingThrust && !fighter.CombatFrozen)
                {
                    if(terraceDriveTarget)
                        DriveTerraceThrust(Age-Mathf.Max(before,terraceWindup));
                    else if(terraceOutcome==TerraceOutcome.Miss)
                    {
                        float glide=Mathf.InverseLerp(terraceWindup,terraceWindup+.16f,Age)-
                            Mathf.InverseLerp(terraceWindup,terraceWindup+.16f,before);
                        StepTerraceBody(heading*(glide*.32f),dt);
                    }
                }
            }
            if(before<TerraceEnd && Age>=TerraceEnd)
            {
                if(terraceOutcome!=TerraceOutcome.Deflected)terraceStreak=0;
                fighter.BossGuard(false);
                if(TryTerraceFollowup())return;
            }
            if(Cut!=TerraceCut.Brace)
            {
                float start=TerraceEnd+.055f, end=start+.25f;
                float retreat=Mathf.InverseLerp(start,end,Age)-Mathf.InverseLerp(start,end,before);
                float stepBack=Cut==TerraceCut.Wheel?1.12f:terraceOutcome==TerraceOutcome.Deflected?.48f:.28f;
                StepTerraceBody(-heading*(retreat*stepBack),dt);
            }
            if(Age>=TerraceEnd+terraceRecovery)
            {
                EndTerraceRound();
                return;
            }
            DrawTerraceCut();
        }

        void CommitTerraceCut(Vector3 delta)
        {
            var move=TerraceMove;
            float distance=Vector3.Dot(delta,heading);
            float spacing=Cut==TerraceCut.Dash?1.60f:Cut==TerraceCut.Chase?1.80f:1.35f;
            float step=Mathf.Clamp(distance-spacing,0,move.Travel);
            if(Cut!=TerraceCut.Brace)
                step=Mathf.Max(step,Mathf.Clamp(distance-.90f,0,Mathf.Min(.40f,move.Travel)));
            Vector3 offset=heading*(Cut==TerraceCut.Brace?-.38f:step);
            float sidestep=Cut switch
            {
                TerraceCut.Wheel=>1.45f,
                TerraceCut.Side=>1.2f,
                TerraceCut.Pair=>.46f,
                TerraceCut.Return=>.55f,
                _=>0
            };
            offset+=Vector3.Cross(Vector3.up,terraceInitialHeading)*(TerraceSideDirection*sidestep);
            Vector3 travel=game.World.ClipDuelStep(transform.position,offset,.65f);
            terraceStep=travel.normalized;
            terraceTravel=travel.magnitude;
            if(terraceTravel>.15f && (Cut==TerraceCut.Dash || Cut==TerraceCut.Chase))
                game.Effects.DodgeWake(transform.position,terraceStep,new Color(.69f,.50f,.63f,.45f));
        }

        void MoveTerraceCut(float dt)
        {
            if(terraceTravelApplied>=terraceTravel || Age<=TerraceMove.Lock || terraceOutcome==TerraceOutcome.Deflected)return;
            float end=Cut==TerraceCut.Dash?.58f:Cut==TerraceCut.Chase?.57f:Cut==TerraceCut.FinalThrust
                ?terraceWindup:terraceWindup+TerraceMove.Active*.50f;
            float progress=Mathf.InverseLerp(TerraceMove.Lock,end,Age);
            float desired=terraceTravel*Mathf.SmoothStep(0,1,progress);
            StepTerraceBody(terraceStep*(desired-terraceTravelApplied),dt);
            terraceTravelApplied=desired;
            fighter.BossMotion(progress<1?1:0);
        }

        void StepTerraceBody(Vector3 step,float dt)
        {
            if(dt<=0 || step.sqrMagnitude<.000001f)return;
            fighter.BossMove(game.World.ClipDuelStep(transform.position,step,.55f)/dt,dt);
        }

        internal void TickTerraceRecoil(float dt)
        {
            if(terraceRecoil.sqrMagnitude<=.01f)return;
            StepTerraceBody(terraceRecoil*dt,dt);
            terraceRecoil=Vector3.MoveTowards(terraceRecoil,Vector3.zero,19*dt);
        }

        void DriveTerraceThrust(float dt)
        {
            if(dt<=0 || !terraceDriveTarget || !terraceDriveTarget.Alive ||
                terraceDriveTarget.Action!=FighterAction.Stagger || !TerraceContains(terraceDriveTarget.transform.position))
                return;
            Vector3 step=heading*((Cut==TerraceCut.Chase?5.4f:4.8f)*dt);
            step=game.World.ClipDuelStep(transform.position,step,.55f);
            // Use the defender's actual controller travel so the blade stops at a wall with them.
            float moved=terraceDriveTarget.TerracePush(step);
            StepTerraceBody(heading*moved,dt);
        }

        void ContactTerrace()
        {
            foreach(var target in Targets)
            {
                if(!target || !target.Alive || contacted.Contains(target) ||
                    !target.OverlapsAttackHeight(transform.position.y,transform.position.y+(Cut==TerraceCut.Sweep?.55f:1.85f)) ||
                    !TerraceContains(target.transform.position) ||
                    Physics.Linecast(transform.position+Vector3.up,target.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))continue;
                contacted.Add(target);
                int cut=MoveIndex;
                if(target==terraceTarget)terraceOutcome=TerraceOutcome.Evaded;
                bool landed=target.ReceiveHit(fighter,Move.damage*(target.MaxHealth/100),0,Move.Unblockable);
                if(MoveIndex!=cut || fighter.Vulnerable)return;
                if(landed && target==terraceTarget)
                {
                    if(!terraceGuardBroken)terraceOutcome=TerraceOutcome.Hit;
                    if(TerraceDrivingThrust && target.Alive && Mathf.Abs(target.transform.position.y-transform.position.y)<.65f)
                        terraceDriveTarget=target;
                }
            }
        }

        bool TerraceContains(Vector3 point)
        {
            Vector3 delta=Planar(point-transform.position);
            if(TerraceMove.HalfWidth==0)
                return delta.sqrMagnitude<=Move.range*Move.range && Vector3.Angle(heading,delta)<=Move.angle*.5f;
            float along=Vector3.Dot(delta,heading);
            return along>=0 && along<=Move.range && Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,heading)))<=TerraceMove.HalfWidth;
        }

        void EndTerraceRound()
        {
            if(terraceTarget && TerracePlayerPinned(terraceTarget))
            {
                Vector3 radial=Planar(transform.position-TrialWorld.TerraceCenter).normalized;
                Vector3 tangent=Vector3.Cross(Vector3.up,radial);
                if(Vector3.Dot(tangent,transform.position-terraceTarget.transform.position)<0)tangent=-tangent;
                terraceSide=(tangent-radial*.28f).normalized;
                terraceRetreatUntil=terraceClock+.45f;
            }
            CancelAttack();
            fighter.BossBegin(FighterAction.Free,0);
            cooldown=terraceOutcome<TerraceOutcome.Hit?.27f:Phase==2?.14f:.20f;
        }

        internal void TerraceBlock(TrialPlayer defender,bool broken)
        {
            if(defender!=terraceTarget)return;
            terraceOutcome=TerraceOutcome.Blocked;
            terraceGuardBroken=broken;
            if(!broken)terraceGuardRead=Mathf.Min(3,terraceGuardRead+1);
        }

        internal void TerraceDeflect(TrialPlayer defender)
        {
            if(!TerraceActive)return;
            duel.ObserveParry(defender);
            if(defender==terraceTarget){terraceOutcome=TerraceOutcome.Deflected; terraceStreak++;}
            if(TerraceDrivingThrust)
            {
                terraceDriveTarget=null;
                defender.ApplyCombatImpulse(heading*2.7f);
            }
            AddTerracePosture(18,terraceStreak>=3);
        }

        internal void TerraceStrikeRecoil(Vector3 direction,bool heavy)
        {
            if(TerraceCanFlinch)
                terraceRecoil=direction*(heavy?3.1f:1.85f);
        }

        internal bool TerraceReceiveStrike(TrialFighter attacker,float posture)
        {
            if(fighter.Vulnerable || !fighter.Alive || posture<=0)return false;
            bool heavy=attacker && attacker.Action==FighterAction.Heavy;
            bool light=attacker && attacker.Action==FighterAction.Light;
            if(light && attacker==terraceObserved)
            {
                terraceLights=terraceClock-terraceLastLight<=2?terraceLights+1:1;
                terraceLastLight=terraceClock;
            }
            Vector3 away=attacker?Planar(attacker.transform.position-transform.position):Vector3.zero;
            if(Attacking && Cut==TerraceCut.Brace && WindingUp)
            {
                if(heavy)
                {
                    CancelAttack();
                    fighter.BossBegin(FighterAction.Free,0);
                }
                else if(light && Age>=.12f && away.sqrMagnitude<9 && Vector3.Angle(heading,away)<70)
                {
                    if(attacker is TrialPlayer player)player.TerraceBladeBounced(away.normalized);
                    BeginTerraceCut(TerraceCut.Riposte,true);
                    Vector3 contact=transform.position+Vector3.up+away.normalized*.85f;
                    game.Effects.Burst(contact,new Color(1.8f,.93f,.30f),30);
                    game.Effects.Sound("block",contact,.88f);
                    return true;
                }
            }
            if(light && TerraceLightPressure && attacker==terraceTarget && away.sqrMagnitude<7.84f &&
                Attacking && (Cut==TerraceCut.Pair || Cut==TerraceCut.Return) && Age<TerraceMove.Lock &&
                TerraceReady(TerraceCut.Brace))
                BeginTerraceCut(TerraceCut.Brace,true);
            if(Attacking && Cut==TerraceCut.Charge && Age<.62f && (heavy || (light && ++terraceChargeHits>=2)))
            {
                CancelAttack();
                fighter.BossBegin(FighterAction.Stagger,.75f);
                return false;
            }
            AddTerracePosture(heavy?22:light?(TerraceActive || TerraceLightPressure?5:8):posture*.4f);
            return false;
        }

        void AddTerracePosture(float amount,bool force=false)
        {
            terracePressureAt=terraceClock;
            if(!fighter.AddTerracePosture(amount,force))return;
            CancelAttack();
            terraceStaggerSlide=.36f;
            fighter.BossBegin(FighterAction.Stagger,1.65f);
            game.Effects.Sound("landing",transform.position,.75f);
            if(game.Player.LockTarget==fighter)
            {
                game.Hud.Notify("脱拍失衡 · 近身重击可处决",1.65f);
                game.CameraRig.CombatFrame(fighter,.55f,.55f);
            }
        }

        float TerracePose=>Age<terraceWindup?.38f*Mathf.Clamp01(Age/terraceWindup):
            Age<TerraceEnd?Mathf.Lerp(.38f,.65f,Mathf.InverseLerp(terraceWindup,TerraceEnd,Age)):
            Mathf.Lerp(.65f,1,Mathf.InverseLerp(TerraceEnd,TerraceEnd+terraceRecovery,Age));

        void DrawTerraceCut()
        {
            HideWarnings();
            if(!Attacking)return;
            bool ground=Cut==TerraceCut.Sweep;
            if(!WindingUp || Age<TerraceMove.Lock || !ground && Cut!=TerraceCut.Chase && Cut!=TerraceCut.FinalThrust)return;
            var line=warning;
            line.enabled=true;
            line.startColor=line.endColor=ground?new Color(1,.24f,.14f,.82f):new Color(1,.82f,.57f,.56f);
            line.widthMultiplier=ground?.062f:.035f;
            Vector3 center=transform.position+Vector3.up*.042f;
            if(TerraceMove.HalfWidth>0)
            {
                Vector3 side=Vector3.Cross(Vector3.up,heading)*TerraceMove.HalfWidth;
                Vector3 end=center+heading*Move.range;
                line.positionCount=5;
                line.SetPosition(0,center-side);line.SetPosition(1,end-side);line.SetPosition(2,end+side);
                line.SetPosition(3,center+side);line.SetPosition(4,center-side);
            }
            else
            {
                const int count=25;
                line.positionCount=count;
                for(int i=0;i<count;i++)
                {
                    float angle=Mathf.Lerp(-Move.angle*.5f,Move.angle*.5f,i/(count-1f));
                    line.SetPosition(i,center+Quaternion.AngleAxis(angle,Vector3.up)*heading*Move.range);
                }
            }
        }
    }
}
