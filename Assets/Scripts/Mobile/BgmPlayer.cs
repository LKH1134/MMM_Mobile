using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // 메인 BGM. Start(로딩 화면)가 끝나고 다음 씬이 처음 열리면 재생을 시작하고, 이후 씬이 바뀌어도 끊지 않고 반복한다.
    // 씬에 따로 둘 것은 없다 (처음 쓸 때 DontDestroyOnLoad 오브젝트를 만든다). 음량은 옵션 배경음 슬라이더(InGameOption.BgmKey)
    public static class BgmPlayer
    {
        const string ClipPath = "Audio/Bgm/GlowingWoodland";
        const string LoadingScene = "Start";

        static AudioSource source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook() => SceneManager.sceneLoaded += OnSceneLoaded;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == LoadingScene) return;
            Play();
        }

        public static void Play()
        {
            if (source == null)
            {
                var clip = Resources.Load<AudioClip>(ClipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[BgmPlayer] 소리 없음: Resources/{ClipPath}");
                    return;
                }
                var go = new GameObject("BgmPlayer");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 0;
            }
            ApplyVolume();
            if (!source.isPlaying) source.Play();
        }

        // 옵션 슬라이더가 바뀌면 부른다
        public static void ApplyVolume()
        {
            if (source != null) source.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(InGameOption.BgmKey, 1f));
        }
    }
}
