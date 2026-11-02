using Mirror;
using UnityEngine;

namespace TomorinTrial
{
    public enum TrialLevel : byte { BigFish, Arena }
    public struct TrialHello : NetworkMessage { public string name; }
    public struct TrialRetryChallenge : NetworkMessage { }
    public struct TrialChooseBoon : NetworkMessage { public TrialBoon boon; }
    public struct TrialLobby : NetworkMessage { public int self; public int[] ids; public string[] names; public TrialLevel level; }
    public struct TrialControls : NetworkMessage
    {
        public Vector2 move;
        public float yaw;
        public byte held, pressed;
    }
    public enum TrialCombatCue : byte { Parry, Block, GuardBreak, Death, Hit, HeavyHit, Kill, PerfectDodge, BlinkSlash, Reversal }
    // Transient feedback is separate from snapshots: several contacts may occur
    // between snapshots, and each belongs to the player who received the hit.
    public struct TrialCombatEvent : NetworkMessage
    {
        public int player;
        public TrialCombatCue cue;
        public Vector3 position, forward;
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
        public byte action, combo, flasks;
        public bool guard, awake;
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
        public bool gate, victory, arena;
        public int guards;
        public float seconds;
        public TrialLevel level;
        public int partySize;
    }
}
