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
        public bool HeavyHeld { get; private set; }
        public bool Dodge { get; private set; }
        public bool Guard { get; private set; }
        public bool Sprint { get; private set; }
        public bool Heal { get; private set; }
        public bool Interact { get; private set; }
        public bool Lock { get; private set; }
        public bool Pause { get; private set; }
        public bool Confirm { get; private set; }
        public bool Restart { get; private set; }
        public bool UsingGamepad {get;private set;}
        int suppressUntilFrame=-1;
        public void SuppressCombat(){suppressUntilFrame=Time.frameCount+2;Attack=Heavy=HeavyHeld=Dodge=Guard=Heal=Interact=Lock=false;}
        bool sprintToggled;
        bool toggleSprint;
        int bindingVersion=-1;
        public Vector2 TouchMove, TouchLook;
        public bool TouchGuard,TouchSprint,TouchHeavy;
        byte touchPressed;
        public void PressTouch(byte action){touchPressed|=action;}
        public void ClearTouch(){TouchMove=TouchLook=Vector2.zero;TouchGuard=TouchSprint=TouchHeavy=false;touchPressed=0;}
        public void ResetSprint(){sprintToggled=false;Sprint=false;ClearTouch();}
        void OnApplicationFocus(bool focused){if(!focused)ResetSprint();}
        InputAction move,look,attack,interact,sprint,heavy,dodge,guard,heal,target,pause,confirm;
        InputActionAsset actions;
        InputActionMap combat;
        public bool NetworkDirected { get; private set; }
        public float NetworkYaw { get; private set; }
        public void SetNetworkYaw(float yaw) { NetworkDirected=true; NetworkYaw=yaw; }
        public TrialControls CaptureNetwork(float yaw) => new TrialControls
        {
            move=Move, yaw=yaw,
            held=(byte)((Guard?1:0)|(Sprint?2:0)|(HeavyHeld?4:0)),
            pressed=(byte)((Attack?1:0)|(Heavy?2:0)|(Dodge?4:0)|(Heal?8:0)|(Lock?16:0)|(Interact?32:0))
        };
        public void ApplyNetwork(TrialControls value)
        {
            NetworkDirected=true;NetworkYaw=value.yaw;Move=value.move;Look=Vector2.zero;
            HeavyHeld=(value.held&4)!=0;Guard=(value.held&1)!=0;Sprint=(value.held&2)!=0;
            Attack=(value.pressed&1)!=0;Heavy=(value.pressed&2)!=0;Dodge=(value.pressed&4)!=0;
            Heal=(value.pressed&8)!=0;Lock=(value.pressed&16)!=0;Interact=(value.pressed&32)!=0;
        }

        public void Initialize(InputActionAsset source)
        {
            toggleSprint=TrialPreferences.ToggleSprint;
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
            foreach(var map in actions.actionMaps)foreach(var action in map.actions)action.performed+=TrackDevice;
            foreach(var action in combat.actions)action.performed+=TrackDevice;
            RefreshBindings();
        }
        void TrackDevice(InputAction.CallbackContext context)
        {
            if((context.action==move || context.action==look) && context.ReadValue<Vector2>().sqrMagnitude<.025f)return;
            UsingGamepad=context.control.device is Gamepad;
        }
        InputAction Button(string label,params string[] bindings)
        {var action=combat.AddAction(label,InputActionType.Button);foreach(var binding in bindings)action.AddBinding(binding);return action;}
        public void SetDeviceFilter(InputDevice[] devices)
        {actions.devices=devices;combat.devices=devices;}

        void RefreshBindings(){TrialBindings.Apply(actions,combat);bindingVersion=TrialBindings.Version;ResetSprint();}
        public void Read()
        {
            if(bindingVersion!=TrialBindings.Version)RefreshBindings();
            if(TrialSettingsPanel.InputBlocked || Time.frameCount<=suppressUntilFrame)
            {
                Move=Look=Vector2.zero;Attack=Heavy=HeavyHeld=Dodge=Guard=Heal=Interact=Lock=Pause=Confirm=Restart=false;ResetSprint();return;
            }
            Move=Vector2.ClampMagnitude(move.ReadValue<Vector2>()+TouchMove,1);
            Look=look.ReadValue<Vector2>()*(look.activeControl?.device is Gamepad?125*Time.unscaledDeltaTime:.105f);
            // Touch look is routed exclusively by the right-side UI pad. Pointer
            // bindings can also read the same finger as a mouse/touch delta.
            if(look.activeControl?.device is Touchscreen || Application.isMobilePlatform && look.activeControl?.device is Mouse)Look=Vector2.zero;
            Attack=attack.WasPressedThisFrame();HeavyHeld=heavy.IsPressed() || TouchHeavy;Heavy=heavy.WasPressedThisFrame();Dodge=dodge.WasPressedThisFrame();Guard=guard.IsPressed();
            // Mobile taps belong to UI controls, including taps reported as mouse clicks.
            // Apply the dedicated light-attack button below after filtering raw pointer input.
            if(attack.activeControl?.device is Touchscreen || Application.isMobilePlatform && attack.activeControl?.device is Mouse)Attack=false;
            if(toggleSprint!=TrialPreferences.ToggleSprint){toggleSprint=TrialPreferences.ToggleSprint;ResetSprint();}
            if(toggleSprint && sprint.WasPressedThisFrame())sprintToggled=!sprintToggled;
            Sprint=TouchSprint || (toggleSprint?sprintToggled:sprint.IsPressed());Heal=heal.WasPressedThisFrame();Interact=interact.WasPressedThisFrame();Lock=target.WasPressedThisFrame();Pause=pause.WasPressedThisFrame();Confirm=confirm.WasPressedThisFrame();Restart=Confirm;
            Look+=TouchLook;TouchLook=Vector2.zero;
            Attack|=(touchPressed&1)!=0;Heavy|=(touchPressed&2)!=0;Dodge|=(touchPressed&4)!=0;
            Heal|=(touchPressed&8)!=0;Lock|=(touchPressed&16)!=0;Interact|=(touchPressed&32)!=0;
            Pause|=(touchPressed&128)!=0;Guard|=TouchGuard;touchPressed=0;
        }
        void OnDestroy(){combat?.Dispose();if(actions){actions.Disable();Destroy(actions);}}
    }
}
