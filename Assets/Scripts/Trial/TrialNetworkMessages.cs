using Mirror;
using UnityEngine;

namespace TomorinTrial
{
    public enum TrialLevel : byte { BigFish, Arena, Twins, Anon, Taffy, Penguin, YaYa, Remnant }
    public enum TrialDifficulty : byte { Normal, Easy, Hard }
    public static class TrialDifficultySettings
    {
        public static TrialDifficulty Solo { get; set; } = TrialDifficulty.Normal;
        public static string Label(TrialDifficulty difficulty)=>difficulty==TrialDifficulty.Easy?"简单":difficulty==TrialDifficulty.Hard?"困难":"正常";
        public static float HealthScale(TrialDifficulty difficulty)=>difficulty==TrialDifficulty.Easy?.75f:difficulty==TrialDifficulty.Hard?1.3f:1;
        public static float DamageScale(TrialDifficulty difficulty)=>difficulty==TrialDifficulty.Easy?.7f:difficulty==TrialDifficulty.Hard?1.3f:1;
    }
    public struct TrialHello : NetworkMessage { public string name; }
    public struct TrialRetryChallenge : NetworkMessage { }
    public struct TrialReturnToRoom : NetworkMessage { }
    public struct TrialChooseBoon : NetworkMessage { public TrialBoon boon; }
    public struct TrialLobby : NetworkMessage { public int self; public int[] ids; public string[] names; public TrialLevel level; public TrialDifficulty difficulty; }
    public struct TrialControls : NetworkMessage
    {
        public Vector2 move;
        public float yaw;
        public byte held, pressed;
    }
    public enum TrialCombatCue : byte { Parry, Block, GuardBreak, Death, Hit, HeavyHit, Kill, PerfectDodge, BlinkSlash, Reversal, Hurt, SkillImpact, Dodge, Heal, ChargeReady, CutThrough, HeavyLand, FinisherHit, CrimsonBlade, MoonBlade, BlueSand, SakuraScatter, SandImpact, FireDragon, SpiralSword, ThrustTrace, RisingCut, CrescentCut, EmpoweredStep }
    // Transient feedback is separate from snapshots: several contacts may occur
    // between snapshots, and each belongs to the player who received the hit.
    public struct TrialCombatEvent : NetworkMessage
    {
        public int player;
        public TrialCombatCue cue;
        public Vector3 position, forward;
        public Vector3 end;
        public float amount;
    }
    public struct TrialActorState
    {
        public int id;
        public Vector3 position;
        public Vector3 dodgeDirection;
        public Vector3 recoil;
        public float yaw, health, maxHealth, stamina, posture, postureLimit, actionTime, duration, movement, protection;
        public int kills, deaths;
        public TrialBoon boon,offerA,offerB,offerC;
        public float maxStamina;
        public float bossResolve,bossFlinch;
        public float swordCharge,parryReady,dodgeFollowup;
        public byte action, combo, flasks;
        public bool guard, awake,dragonLanded;
    }
    public struct TrialHazardState { public Vector3 position; public float radius; public bool active, wave; }
    public struct TrialBossState
    {
        public byte skill;
        public float age, duration;
        public Vector3 aim, origin, heading;
        public bool enraged;
        public bool frenzied;
        public int variant;
        public TrialBossBoon boon;
        public Vector3[] marks;
        public TrialHazardState[] hazards;
    }
    public struct TrialWorldState : NetworkMessage
    {
        public TrialActorState[] players, enemies;
        public TrialBossState boss;
        public TrialChampionState[] champions;
        public bool gate, victory, arena, defeat, waitingForBoons;
        public int guards;
        public float seconds;
        public TrialLevel level;
        public TrialDifficulty difficulty;
        public int partySize;
    }
}
