using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        Vector3 martialRight;
        float martialFloor;
        bool martialFinished;

        public static float SwordStaminaCost(bool heavy,int move)
        {
            if(move==SwordRecallRise)return 14;
            if(move==ParryMirror || move==SwordRecallCut)return 12;
            if(move==ParryAscend)return 8;
            if(move==LoftingDraw || move==3 || move==AirStep || move==LowVault)return 5;
            if(move==AerialCascade)return 9;
            if(move==WallRebound)return 6;
            if(IsMartialAir(move) || move==AirCross || move==AirWheel || move==AirPierce)return heavy?10:7;
            if(IsMartialMove(move))return IsBodyStrike(move)?6:heavy?14:9;
            return move==14?32:heavy?22:12;
        }
        static float MartialDuration(int move)=>move==SwordRecallRise?.78f:move==SwordRecallCut?.64f:move==ParryMirror?.82f:move==ParryAscend?.76f:move==HiltStrike?.38f:move==HeelTurn?.55f:move==LowVault?.58f:move==VaultSweep?.66f:move==OrbitCut?.70f:move==FallingDraw?.72f:move==ElbowBreak?.42f:move==ScissorKick?.68f:move==SheathTurn?.62f:.72f;

        void BeginMartialArt()
        {
            martialRight=Vector3.Cross(Vector3.up,swordHeading);martialFloor=launchFloor;martialFinished=false;
            // Launch from the current floor, or carry the current suspended height into another jump.
            if(Combo==SwordRecallRise || Combo==ParryAscend || Combo==LoftingDraw || Combo==LowVault || Combo==CloudRake)
            {
                if(!airborneCombo || transform.position.y<launchFloor+.2f)launchFloor=transform.position.y;
                launchHeight=Combo==SwordRecallRise || Combo==ParryAscend?2.2f:Combo==LowVault?1.05f:1.7f;airborneCombo=true;
            }
            if(Combo==VaultSweep || Combo==ScissorKick)
            {
                if(!airborneCombo){launchFloor=transform.position.y;launchHeight=1.05f;}
                airborneCombo=true;
            }
            martialFloor=launchFloor;
        }
        bool TickMartialMotion()
        {
            if(!IsMartialMove(Combo))return false;
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.58f,Progress));
            float old=swordMotionProgress;swordMotionProgress=t;
            // A visible crescent step around the target replaces another straight blink/lunge.
            if(Combo==SwordRecallCut)return true;
            if(Combo==ParryMirror)
            {
                Vector3 Before(float p)=>martialRight*(Mathf.Sin(p*Mathf.PI*2)*1.25f)+swordHeading*(p*1.2f);
                motor.Move(Before(t)-Before(old));
            }
            else if(Combo==OrbitCut || Combo==SheathTurn)
            {
                float side=Combo==OrbitCut?1:-1;
                Vector3 Before(float p)=>martialRight*(side*Mathf.Sin(p*Mathf.PI*.85f)*1.5f)+swordHeading*(p*.7f);
                motor.Move(Before(t)-Before(old));
            }
            else
            {
                float distance=Combo==LowVault?1.8f:Combo==VaultSweep?1.1f:Combo==HiltStrike || Combo==ElbowBreak?.65f:Combo==HeelTurn?.45f:Combo==ScissorKick?.9f:.75f;
                float step=(t-old)*distance;
                if(chaseTarget && chaseTarget.Alive)
                {
                    float body=chaseTarget is TrialEnemy foe?foe.BodyRadius:.28f;
                    step=Mathf.Min(step,Mathf.Max(0,Vector3.ProjectOnPlane(chaseTarget.transform.position-transform.position,Vector3.up).magnitude-body-1.0f));
                }
                motor.Move(swordHeading*step);
            }
            return true;
        }
        bool TickMartialGravity()
        {
            if(Combo!=FallingDraw || Action!=FighterAction.Light && Action!=FighterAction.Heavy)return false;
            float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.65f,Progress));
            motor.Move(Vector3.up*(Mathf.Lerp(swordStart.y,martialFloor,fall)-transform.position.y));
            verticalSpeed=-2;airborneCombo=false;return true;
        }
        void TickMartialArts()
        {
            if(!IsMartialMove(Combo))return;
            if((Combo==SwordRecallRise || Combo==ParryAscend || Combo==LoftingDraw) && Progress>=.08f && (swordMotionCues&1)==0)
            {swordMotionCues|=1;EmitSwordCue(TrialCombatCue.SwordLeap,swordStart,swordHeading,transform.position,1);}
            if(Combo==LowVault)
            {
                if(Progress>=.10f && swordEmissions==0)
                {swordEmissions++;EmitSwordCue(TrialCombatCue.MartialStroke,transform.position,swordHeading,transform.position,Combo);}
                return;
            }
            int beats=Combo==ParryMirror || Combo==ScissorKick || Combo==CloudRake?2:1;
            while(swordEmissions<beats && Progress>=.26f+swordEmissions*.28f)
            {
                swordEmissions++;
                struck.Clear();
                foreach(var enemy in game.Opponents(this))
                {
                    if(!enemy || !enemy.Alive)continue;
                    Vector3 delta=enemy.transform.position-transform.position;float height=Mathf.Abs(delta.y);delta.y=0;
                    float body=enemy is TrialEnemy foe?foe.BodyRadius:.28f;
                    float reach=Combo==HiltStrike || Combo==ElbowBreak?2.0f:Combo==HeelTurn?2.7f:3.3f;
                    if(height>2.5f || delta.magnitude>reach+body || Vector3.Angle(transform.forward,delta)>(Combo==OrbitCut || Combo==VaultSweep?145:85))continue;
                    if(Physics.Linecast(transform.position+Vector3.up,enemy.transform.position+Vector3.up,1,QueryTriggerInteraction.Ignore))continue;
                    float damage=Combo==SwordRecallCut?48:Combo==ParryMirror?32:Combo==SwordRecallRise || Combo==ParryAscend?52:Combo==HiltStrike?16:Combo==HeelTurn?20:Combo==ElbowBreak?24:Combo==ScissorKick?18:Combo==CloudRake?24:Combo==FallingDraw?48:36;
                    SwordDamage(enemy,damage,Combo==SwordRecallRise || Combo==ParryAscend?45:IsBodyStrike(Combo)?36:28,Combo==FallingDraw);
                }
                EmitSwordCue(TrialCombatCue.MartialStroke,transform.position,swordHeading,transform.position+transform.forward*2+(Combo==ParryMirror?Vector3.up*(swordEmissions==1?1:-1):Vector3.zero),Combo);
                if(Combo==ParryMirror || Combo==ParryAscend)game.Effects.Afterimage(this);
            }
            if(Combo==FallingDraw && Progress>=.67f && !martialFinished)
            {
                martialFinished=true;EmitSwordCue(TrialCombatCue.SwordLanding,transform.position,swordHeading,transform.position,.8f);
            }
        }
    }
}
