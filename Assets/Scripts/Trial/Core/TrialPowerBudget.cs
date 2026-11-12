using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial
{
    // Honor the selected combat cap. LAN sessions do not receive idle/background throttling.
    public sealed class TrialPowerBudget : MonoBehaviour
    {
        TrialGame game;
        float nextCheck;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go=new GameObject("Frame budget");DontDestroyOnLoad(go);go.AddComponent<TrialPowerBudget>();
        }
        void OnEnable(){SceneManager.sceneLoaded+=Loaded;game=FindAnyObjectByType<TrialGame>();}
        void OnDisable(){SceneManager.sceneLoaded-=Loaded;}
        void Loaded(Scene scene,LoadSceneMode mode){game=FindAnyObjectByType<TrialGame>();nextCheck=0;}
        public static int Budget(int requested,bool active,bool focused,bool session)
        {
            int limit=requested<=0?-1:requested;
            return session?limit:!focused?15:active?limit:limit<0?30:Mathf.Min(30,limit);
        }
        void Update()
        {
            if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.5f;
            int requested=TrialPreferences.MobileFrameRate;
            bool active=game && game.Mode==TrialMode.Playing && !game.Player.ChoosingBoon;
            bool session=TrialNetworkManager.SessionActive;
            // Desktop VSync overrides targetFrameRate; suspend it while solo is
            // idle so menu/background caps can actually reduce GPU work.
            bool sync=TrialPreferences.VSyncEnabled;
            bool idle=!session && (!Application.isFocused || !active);
            int syncCount=sync && !idle?1:0;if(QualitySettings.vSyncCount!=syncCount)QualitySettings.vSyncCount=syncCount;
            int target=sync && !idle && !Application.isMobilePlatform?-1:Budget(requested,active,Application.isFocused,session);
            if(Application.targetFrameRate!=target)Application.targetFrameRate=target;
        }
    }
}
