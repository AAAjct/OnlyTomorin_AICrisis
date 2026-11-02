using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    // One input boundary: retain the project's Player map and add sword-specific actions.
    public sealed class TrialInput : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Attack { get; private set; }
        public bool Heavy { get; private set; }
        public bool Dodge { get; private set; }
        public bool Guard { get; private set; }
        public bool Sprint { get; private set; }
        public bool Heal { get; private set; }
        public bool Interact { get; private set; }
        public bool Lock { get; private set; }
        public bool Pause { get; private set; }
        public bool Confirm { get; private set; }
        public bool Restart { get; private set; }
        InputAction move,look,attack,interact,sprint,heavy,dodge,guard,heal,target,pause,confirm;
        InputActionAsset actions;
        InputActionMap combat;
        public bool NetworkDirected { get; private set; }
        public float NetworkYaw { get; private set; }
        public void SetNetworkYaw(float yaw) { NetworkDirected=true; NetworkYaw=yaw; }
        public TrialControls CaptureNetwork(float yaw) => new TrialControls
        {
            move=Move, yaw=yaw,
            held=(byte)((Guard?1:0)|(Sprint?2:0)),
            pressed=(byte)((Attack?1:0)|(Heavy?2:0)|(Dodge?4:0)|(Heal?8:0)|(Lock?16:0)|(Interact?32:0))
        };
        public void ApplyNetwork(TrialControls value)
        {
            NetworkDirected=true;NetworkYaw=value.yaw;Move=value.move;Look=Vector2.zero;
            Guard=(value.held&1)!=0;Sprint=(value.held&2)!=0;
            Attack=(value.pressed&1)!=0;Heavy=(value.pressed&2)!=0;Dodge=(value.pressed&4)!=0;
            Heal=(value.pressed&8)!=0;Lock=(value.pressed&16)!=0;Interact=(value.pressed&32)!=0;
        }

        public void Initialize(InputActionAsset source)
        {
            actions=Instantiate(source);
            move=actions.FindAction("Player/Move",true);look=actions.FindAction("Player/Look",true);
            attack=actions.FindAction("Player/Attack",true);interact=actions.FindAction("Player/Interact",true);sprint=actions.FindAction("Player/Sprint",true);
            attack.AddBinding("<Keyboard>/j");interact.AddBinding("<Gamepad>/buttonSouth");sprint.AddBinding("<Gamepad>/leftStickPress");
            combat=new InputActionMap("SwordCombat");
            heavy=Button("Heavy","<Keyboard>/q","<Gamepad>/rightShoulder");
            dodge=Button("Dodge","<Keyboard>/space","<Gamepad>/buttonEast");
            guard=Button("Guard","<Mouse>/rightButton","<Keyboard>/k","<Gamepad>/leftShoulder");
            heal=Button("Heal","<Keyboard>/r","<Gamepad>/dpad/up");
            target=Button("Lock","<Keyboard>/tab","<Mouse>/middleButton","<Gamepad>/rightStickPress");
            pause=Button("Pause","<Keyboard>/escape","<Gamepad>/start");
            confirm=Button("Confirm","<Keyboard>/enter","<Gamepad>/buttonSouth");
            actions.FindActionMap("Player",true).Enable();combat.Enable();
        }
        InputAction Button(string label,params string[] bindings)
        {var action=combat.AddAction(label,InputActionType.Button);foreach(var binding in bindings)action.AddBinding(binding);return action;}
        public void SetDeviceFilter(InputDevice[] devices)
        {actions.devices=devices;combat.devices=devices;}

        public void Read()
        {
            Move=move.ReadValue<Vector2>();
            Look=look.ReadValue<Vector2>()*(look.activeControl?.device is Gamepad?125*Time.unscaledDeltaTime:.105f);
            Attack=attack.WasPressedThisFrame();Heavy=heavy.WasPressedThisFrame();Dodge=dodge.WasPressedThisFrame();Guard=guard.IsPressed();
            Sprint=sprint.IsPressed();Heal=heal.WasPressedThisFrame();Interact=interact.WasPressedThisFrame();Lock=target.WasPressedThisFrame();Pause=pause.WasPressedThisFrame();Confirm=confirm.WasPressedThisFrame();Restart=Confirm;
        }
        void OnDestroy(){combat?.Dispose();if(actions){actions.Disable();Destroy(actions);}}
    }
}
