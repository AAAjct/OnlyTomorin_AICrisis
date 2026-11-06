using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialPlayer
    {
        float swordThrowGuardAge=100;
        Vector3 swordReturnStart,swordReturnOrigin;
        public bool SwordGuardReturning=>Combo==SwordGuardReturn && Action==FighterAction.Heavy;
        public Vector3 SwordReturnOrigin=>swordReturnOrigin;
        ParryFlight LiveThrownSword
        {
            get{for(int i=parryFlights.Count-1;i>=0;i--)if(parryFlights[i].cue==TrialCombatCue.SpiralSword)return parryFlights[i];return null;}
        }
        bool AutoSwordThrowGuard=>Alive && (Action==FighterAction.Heavy && Combo==SpiralSword || Action==FighterAction.Free && (LiveThrownSword!=null || Time.time<replicaSwordUntil));
        void BeginSwordThrowGuard(){swordThrowGuardAge=0;GuardAge=0;Guarding=Stamina>1;}
        void TickSwordThrowGuard(float dt)
        {
            if(!AutoSwordThrowGuard)return;
            swordThrowGuardAge=guardPressedThisTick?0:swordThrowGuardAge+dt;
            Guarding=Stamina>1;GuardAge=swordThrowGuardAge;
        }
        bool TrySwordGuardReturn()
        {
            var flight=LiveThrownSword;
            if(flight==null)return false;
            Vector3 before=transform.position,blade=flight.point,destination=blade-Vector3.up;
            swordReturnOrigin=flight.origin;
            if(Physics.Raycast(destination+Vector3.up*2,Vector3.down,out var floor,6,1,QueryTriggerInteraction.Ignore))destination.y=floor.point.y;
            else destination.y=before.y;
            var offset=destination-before;float distance=offset.magnitude;
            if(distance>.001f && Physics.CapsuleCast(before+Vector3.up*(motor.radius+.05f),before+Vector3.up*(motor.height-motor.radius),motor.radius,offset.normalized,out var wall,distance,1,QueryTriggerInteraction.Ignore))destination=before+offset.normalized*Mathf.Max(0,wall.distance-.05f);
            game.Effects.Afterimage(this);Teleport(destination);swordReturnStart=transform.position;
            parryFlights.RemoveAll(f=>f.cue==TrialCombatCue.SpiralSword);replicaSwordUntil=0;
            parryReadyUntil=parryDodgeUntil=blinkReadyUntil=0;
            ClearCombatHold();game.Effects.ResumeSwordReturn();ClearAttackInputs();attackGuardLatch=true;chainUntil=weaveUntil=0;weavePending=false;
            airborneCombo=false;verticalSpeed=-2;PlanarVelocity=combatImpulse=Vector3.zero;chaseTarget=null;
            Combo=SwordGuardReturn;BeginSwordArt();struck.Clear();swordEmissions=1;
            swordHeading=Vector3.ProjectOnPlane(swordReturnOrigin-swordReturnStart,Vector3.up).normalized;
            if(swordHeading.sqrMagnitude<.01f)swordHeading=-transform.forward;
            Turn(swordHeading,36000,.02f);Begin(FighterAction.Heavy,.36f);
            if(Weapon)Weapon.gameObject.SetActive(true);
            EmitSwordCue(TrialCombatCue.SwordRecall,before+Vector3.up,swordHeading,swordReturnStart+Vector3.up,1);
            EmitSwordCue(TrialCombatCue.SwordGuardReturn,swordReturnStart+Vector3.up,swordHeading,swordReturnOrigin+Vector3.up,1);
            return true;
        }
        bool TickSwordGuardReturn()
        {
            if(!SwordGuardReturning)return false;
            Vector3 before=transform.position;
            float progress=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.65f,Progress));
            bool moving=swordMotionProgress<1;swordMotionProgress=progress;
            Vector3 destination=Vector3.Lerp(swordReturnStart,swordReturnOrigin,progress);
            // Sweep world geometry explicitly: the slash passes through enemy
            // hitboxes, while a wall still stops both movement and damage.
            Vector3 offset=destination-before;float distance=offset.magnitude;
            if(distance>.001f && Physics.CapsuleCast(before+Vector3.up*(motor.radius+.05f),before+Vector3.up*(motor.height-motor.radius),motor.radius,offset.normalized,out var wall,distance,1,QueryTriggerInteraction.Ignore))destination=before+offset.normalized*Mathf.Max(0,wall.distance-.05f);
            Teleport(destination);
            if(moving || Progress<=.72f)SwordSegment(before+Vector3.up,transform.position+Vector3.up,.85f,95,70,struck);
            if(trail)trail.emitting=Progress<.7f;
            return true;
        }
    }
}
