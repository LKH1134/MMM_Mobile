using UnityEngine;

namespace MoreMush.Mobile
{
    // PlayerPrefs.Save()는 안드로이드에서 저장 파일 전체를 메인 스레드에서 다시 쓴다 → 자주 바뀌는 값(전투 골드 1초마다,
    // 강화 버튼 꾹 누르기 초당 수십 번, 마지막 접속 시각)은 MarkDirty만 하고, 여기서 FlushInterval초마다·앱이 뒤로 갈 때·끝날 때 한 번에 저장한다.
    // 뽑기·합성·편성처럼 드물고 중요한 쓰기는 지금처럼 바로 PlayerPrefs.Save()를 불러도 된다 (그때 밀린 값도 같이 저장된다)
    public class SaveScheduler : MonoBehaviour
    {
        const float FlushInterval = 5f;

        static bool dirty;
        static SaveScheduler instance;
        float flushIn = FlushInterval;

        public static void MarkDirty() => dirty = true;

        // 밀린 값을 지금 저장한다
        public static void Flush()
        {
            if (!dirty) return;
            dirty = false;
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            dirty = false;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(SaveScheduler)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SaveScheduler>();
        }

        void Update()
        {
            flushIn -= Time.unscaledDeltaTime;
            if (flushIn > 0f) return;
            flushIn = FlushInterval;
            Flush();
        }

        // 안드로이드는 뒤로 간 앱을 OnApplicationQuit 없이 끝낼 수 있다 → 뒤로 갈 때 반드시 저장
        void OnApplicationPause(bool paused) { if (paused) Flush(); }
        void OnApplicationFocus(bool focused) { if (!focused) Flush(); }
        void OnApplicationQuit() => Flush();
    }
}
