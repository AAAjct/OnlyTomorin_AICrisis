using Mirror;
using UnityEngine;

namespace TomorinTrial
{
    public enum TrialLevel : byte { BigFish, Arena, Twins, Anon, Taffy, Penguin, YaYa, Remnant, Endless, BossRush }
    public enum TrialDifficulty : byte { Normal, Easy, Hard }
    public static class TrialDifficultySettings
    {
        public static TrialDifficulty Solo { get; set; } = TrialDifficulty.Normal;
        public static string Label(TrialDifficulty difficulty)=>difficulty==TrialDifficulty.Easy?"简单":difficulty==TrialDifficulty.Hard?"困难":"正常";
        public static float HealthScale(TrialDifficulty difficulty)=>difficulty==TrialDifficulty.Easy?.75f:difficulty==TrialDifficulty.Hard?1.3f:1;
        public static float DamageScale(TrialDifficulty difficulty)=>difficulty==TrialDifficulty.Easy?.7f:difficulty==TrialDifficulty.Hard?1.3f:1;
    }
    public struct TrialHello : NetworkMessage { public string name; public TrialOutfit outfit; }
    public struct TrialRetryChallenge : NetworkMessage { }
    public struct TrialReturnToRoom : NetworkMessage { }
    public struct TrialRunUpgrade : NetworkMessage { public int slot; } // -2 closes the offers; -1 opens them; 0..2 selects one.
    public struct TrialChooseBoon : NetworkMessage { public TrialBoon boon; }
    public struct TrialLobby : NetworkMessage { public int self; public int[] ids; public string[] names; public TrialOutfit[] outfits; public TrialLevel level; public TrialDifficulty difficulty; public int capacity; public string roomName; }
    public struct TrialControls : NetworkMessage
    {
        public const byte GuardPress=1<<7;
        public Vector2 move;
        public float yaw;
        public byte held, pressed, spells;
    }
    public enum TrialCombatCue : byte { Parry, Block, GuardBreak, Death, Hit, HeavyHit, Kill, PerfectDodge, BlinkSlash, Reversal, Hurt, SkillImpact, Dodge, Heal, ChargeReady, CutThrough, HeavyLand, FinisherHit, CrimsonBlade, MoonBlade, BlueSand, SakuraScatter, SandImpact, ThunderDragon, SpiralSword, ThrustTrace, RisingCut, CrescentCut, EmpoweredStep, SwordLeap, SwordTurn, SwordDescent, SwordLanding, RollFlash, WeaveSigil, MartialStroke, SwordRecall, SwordGuardReturn, SwordFlowHit, MirrorCast, MirrorBreak, ThunderChain, BlackCoffin, MirrorScreenBreak, MirrorPrismCut, PerfectBlock, TerraceParry, TerraceBlock, RunFrostOrbCast, RunFrostOrbImpact }
    // Transient feedback is separate from snapshots: several contacts may occur
    // between snapshots, and each belongs to the player who received the hit.
    public struct TrialCombatEvent : NetworkMessage
    {
        // Contact cues use amount for these flags; sword art cues carry their own payload.
        public const float ContactOnlyImpact=-1;
        public const float BlinkSlashImpact=-2;
        internal const float BlinkSlashHitStop=.04f;
        public int player, target;
        public TrialCombatCue cue;
        public Vector3 position, forward;
        public Vector3 end;
        public float amount;
        public byte style;
    }
    public struct TrialActorState
    {
        public int id;
        public byte enemyKind;
        public int runLevel,runExperience,pendingRunChoices;
        public bool runChoiceOpen;
        public float runFrostRemaining;
        public bool runSlamStunned;
        public TrialAffix[] runOffers;
        public int[] runAffixes;
        public Vector3 position;
        public Vector3 dodgeDirection;
        public Vector3 recoil;
        public float yaw, health, maxHealth, stamina, posture, postureLimit, actionTime, duration, movement, protection;
        public int kills, deaths;
        public ushort spellEnergy,mirrorBlink;
        public float chantRemaining,mirrorCounter;
        public TrialBoon boon,offerA,offerB,offerC;
        public float maxStamina;
        public float respawnRemaining;
        public float bossResolve,bossFlinch;
        public float swordCharge,parryReady,dodgeFollowup;
        public float swordLanding;
        public bool landingSakuraFlow,airVolleyDescending;
        public float comboGrace,weaveGrace,guardComboDelay,orbitSwordTime;
        public Vector3 orbitSwordPosition,swordReturnOrigin;
        public bool comboHeavy,weaveHeavy;
        public byte weaveFrom;
        public byte action, combo, flasks;
        public bool guard, awake,dragonLanded,headSevered,spellExecuted;
    }
    public struct TrialFrostOrbState { public int id; public Vector3 position, direction; }
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
        public TrialBossState[] runBosses;
        public TrialFrostOrbState[] frostOrbs;
        public int runWave,runKills,runBossesDefeated;
        public TrialChampionState[] champions;
        public bool gate, victory, arena, defeat, waitingForBoons;
        public int guards;
        public float seconds;
        public TrialLevel level;
        public TrialDifficulty difficulty;
        public int partySize;
    }
}
