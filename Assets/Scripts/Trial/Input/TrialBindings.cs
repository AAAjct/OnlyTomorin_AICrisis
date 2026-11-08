using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    // Stable preference keys are independent of the IDs of cloned/runtime actions.
    public static class TrialBindings
    {
        public sealed class Entry
        {
            public readonly string Id,Label,Action;
            public readonly string[] Defaults;
            public Entry(string id,string label,string action,string primary,string alternate,string pad)
            {Id=id;Label=label;Action=action;Defaults=new[]{primary,alternate,pad};}
        }
        internal static readonly Entry[] Spells={
            new Entry("Spell1","黑棺","SwordCombat/Spell1","<Keyboard>/1","<Keyboard>/numpad1",null),
            new Entry("Spell2","镜花水月","SwordCombat/Spell2","<Keyboard>/2","<Keyboard>/numpad2",null)
        };
        public static readonly Entry[] Entries={
            new Entry("Up","向前移动","Player/Move","<Keyboard>/w","<Keyboard>/upArrow",null),
            new Entry("Down","向后移动","Player/Move","<Keyboard>/s","<Keyboard>/downArrow",null),
            new Entry("Left","向左移动","Player/Move","<Keyboard>/a","<Keyboard>/leftArrow",null),
            new Entry("Right","向右移动","Player/Move","<Keyboard>/d","<Keyboard>/rightArrow",null),
            new Entry("Sprint","奔跑切换 / 按住","Player/Sprint","<Keyboard>/leftShift",null,"<Gamepad>/leftStickPress"),
            new Entry("Attack","轻攻击","Player/Attack","<Mouse>/leftButton","<Keyboard>/j","<Gamepad>/buttonWest"),
            new Entry("Heavy","重攻击","SwordCombat/Heavy","<Keyboard>/q",null,"<Gamepad>/rightShoulder"),
            new Entry("Guard","格挡","SwordCombat/Guard","<Mouse>/rightButton","<Keyboard>/k","<Gamepad>/leftShoulder"),
            new Entry("Dodge","闪避","SwordCombat/Dodge","<Keyboard>/space",null,"<Gamepad>/buttonEast"),
            new Entry("Jump","跳跃","SwordCombat/Jump","<Keyboard>/leftCtrl","<Keyboard>/rightCtrl","<Gamepad>/buttonNorth"),
            new Entry("Heal","使用回复药","SwordCombat/Heal","<Keyboard>/r",null,"<Gamepad>/dpad/up"),
            new Entry("Lock","锁定 / 解除锁定","SwordCombat/Lock","<Keyboard>/tab","<Mouse>/middleButton","<Gamepad>/rightStickPress"),
            new Entry("Interact","交互","Player/Interact","<Keyboard>/e",null,"<Gamepad>/buttonSouth"),
            new Entry("Pause","暂停菜单","SwordCombat/Pause","<Keyboard>/escape",null,"<Gamepad>/start"),
            new Entry("Upgrade","打开升级选项","SwordCombat/Upgrade","<Keyboard>/t",null,"<Gamepad>/dpad/down"),
            Spells[0],Spells[1]
        };
        public static int Version {get;private set;}
        static string Key(Entry entry,int slot)=>"Ink.Binding."+entry.Id+"."+slot;
        public static string Path(Entry entry,int slot)
        {
            var fallback=entry.Defaults[slot];
            if(fallback==null)return null;
            var saved=PlayerPrefs.GetString(Key(entry,slot),"");
            if(saved.Length!=0)return saved;
            if(entry==Spells[0] || entry==Spells[1])
            {
                // Older saves could already have assigned a spell's new default to another slot.
                foreach(var other in Entries)
                    for(int i=0;i<other.Defaults.Length;i++)
                        if(other.Defaults[i]!=null && string.Equals(PlayerPrefs.GetString(Key(other,i),""),fallback,StringComparison.OrdinalIgnoreCase))
                            return "";
            }
            return fallback;
        }
        public static string Display(Entry entry,int slot)
        {
            var path=Path(entry,slot);if(string.IsNullOrEmpty(path))return "—";
            if(path=="<Mouse>/leftButton")return "鼠标左键";if(path=="<Mouse>/rightButton")return "鼠标右键";if(path=="<Mouse>/middleButton")return "鼠标中键";
            if(slot==2)
            {
                bool ps=Gamepad.current!=null && (Gamepad.current.layout.Contains("Dual") || Gamepad.current.displayName.Contains("PlayStation"));
                switch(path)
                {
                    case "<Gamepad>/buttonWest":return ps?"□":"X";
                    case "<Gamepad>/buttonEast":return ps?"○":"B";
                    case "<Gamepad>/buttonSouth":return ps?"×":"A";
                    case "<Gamepad>/buttonNorth":return ps?"△":"Y";
                    case "<Gamepad>/rightShoulder":return ps?"R1":"RB";
                    case "<Gamepad>/leftShoulder":return ps?"L1":"LB";
                    case "<Gamepad>/rightStickPress":return "R3";
                    case "<Gamepad>/leftStickPress":return "L3";
                    case "<Gamepad>/dpad/up":return "↑";
                    case "<Gamepad>/start":return ps?"Options":"Menu";
                }
            }
            return InputControlPath.ToHumanReadableString(path,InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        public static string Display(string id,int slot=0)=>Display(Array.Find(Entries,e=>e.Id==id),slot);
        public static bool TrySet(Entry entry,int slot,string path,out string error)
        {
            error=null;
            bool pad=path!=null && path.StartsWith("<Gamepad>/",StringComparison.Ordinal);
            if(entry.Defaults[slot]==null || string.IsNullOrEmpty(path) || (slot==2?!pad:!(path.StartsWith("<Keyboard>/",StringComparison.Ordinal)||path.StartsWith("<Mouse>/",StringComparison.Ordinal))))
            {error="请选择此栏对应设备的按键。";return false;}
            if((path=="<Keyboard>/escape" || path=="<Keyboard>/enter" || path=="<Gamepad>/start") && path!=entry.Defaults[slot])
            {error="此按键保留用于菜单确认或返回。";return false;}
            foreach(var other in Entries)for(int i=0;i<3;i++)
                if(!(other==entry && i==slot) && string.Equals(Path(other,i),path,StringComparison.OrdinalIgnoreCase))
                {error="该按键已用于「"+other.Label+"」，请换一个按键。";return false;}
            PlayerPrefs.SetString(Key(entry,slot),path);PlayerPrefs.Save();Version++;return true;
        }
        public static void ResetDefaults()
        {foreach(var entry in Entries)for(int i=0;i<3;i++)PlayerPrefs.DeleteKey(Key(entry,i));PlayerPrefs.Save();Version++;}
        public static void Apply(InputActionAsset player,InputActionMap combat)
        {
            bool playerEnabled=player.FindActionMap("Player",true).enabled,combatEnabled=combat.enabled;
            player.Disable();combat.Disable();
            foreach(var entry in Entries)
            {
                var action=entry.Action.StartsWith("Player/")?player.FindAction(entry.Action,true):combat.FindAction(entry.Action.Substring(12),true);
                for(int slot=0;slot<3;slot++)if(entry.Defaults[slot]!=null)
                    for(int i=0;i<action.bindings.Count;i++)if(action.bindings[i].path==entry.Defaults[slot])action.ApplyBindingOverride(i,Path(entry,slot));
            }
            // Menu confirmation should not also fire an undisplayed third attack binding.
            var attack=player.FindAction("Player/Attack",true);
            for(int i=0;i<attack.bindings.Count;i++)if(attack.bindings[i].path=="<Keyboard>/enter")attack.ApplyBindingOverride(i,"");
            var interact=player.FindAction("Player/Interact",true);
            for(int i=0;i<interact.bindings.Count;i++)if(interact.bindings[i].path=="<Gamepad>/buttonNorth")interact.ApplyBindingOverride(i,"");
            if(playerEnabled)player.FindActionMap("Player",true).Enable();if(combatEnabled)combat.Enable();
        }
    }
}
