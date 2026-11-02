using Mirror;
using UnityEngine;

namespace TomorinTrial
{
    public struct TrialHello : NetworkMessage { public string name; }
    public struct TrialLobby : NetworkMessage { public int self; public int[] ids; public string[] names; }
    public struct TrialControls : NetworkMessage
    {
        public Vector2 move;
        public float yaw;
        public byte held, pressed;
    }
    public struct TrialActorState
    {
        public int id;
        public Vector3 position;
        public float yaw, health, stamina, posture, actionTime, duration, movement;
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
        public TrialHazardState[] hazards;
    }
    public struct TrialWorldState : NetworkMessage
    {
        public TrialActorState[] players, enemies;
        public TrialBossState boss;
        public bool gate, victory;
        public int guards;
        public float seconds;
    }
}
