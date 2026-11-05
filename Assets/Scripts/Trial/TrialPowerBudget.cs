using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial
{
    // Rendering budget only: never slow the authoritative LAN host's simulation.
    public sealed class TrialPowerBudget : MonoBehaviour
    {
        TrialGame game;
        float nextCheck;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go=new GameObject("Frame budget");DontDestroyOnLoad(go);go.AddComponent<TrialPowerBudget>();
        }
        void OnEnable(){SceneManager.sceneLoaded+=Loaded;game=FindFirstObjectByType<TrialGame>();}
        void OnDisable(){SceneManager.sceneLoaded-=Loaded;}
        void Loaded(Scene scene,LoadSceneMode mode){game=FindFirstObjectByType<TrialGame>();nextCheck=0;}
        public static int Budget(int requested,bool active,bool focused,bool session)
        {return session?Mathf.Max(60,requested):!focused?15:active?requested:Mathf.Min(30,requested);}
        void Update()
        {
            if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.5f;
            int requested=TrialPreferences.MobileFrameRate;
            if(requested==0)requested=Mathf.Max(60,Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
            bool active=game && game.Mode==TrialMode.Playing && !game.Player.ChoosingBoon;
            int target=Budget(requested,active,Application.isFocused,TrialNetworkManager.SessionActive);
            if(Application.targetFrameRate!=target)Application.targetFrameRate=target;
        }
    }
}
