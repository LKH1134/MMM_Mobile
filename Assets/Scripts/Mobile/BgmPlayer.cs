using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // 메인 BGM. Start(로딩 화면)가 끝나고 다음 씬이 처음 열리면 재생을 시작하고, 이후 씬이 바뀌어도 끊지 않고 반복한다.
    // 씬에 따로 둘 것은 없다 (처음 쓸 때 DontDestroyOnLoad 오브젝트를 만든다). 음량은 옵션 배경음 슬라이더(BgmVolumeSlider, 저장 키 InGameOption.BgmKey), 켜고 끄기는 OptionSwitch
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

        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(InGameOption.BgmKey, 1f));

        // 옵션 배경음 슬라이더(BgmVolumeSlider)가 부른다: 저장하고 바로 반영
        public static void SetVolume(float v)
        {
            PlayerPrefs.SetFloat(InGameOption.BgmKey, Mathf.Clamp01(v));
            SaveScheduler.MarkDirty();
            ApplyVolume();
        }

        // 옵션 배경음 스위치(OptionSwitch): 끄면 소리만 막고 슬라이더 음량은 그대로 둔다
        public static bool Enabled => OptionSwitch.IsOn(OptionSwitch.Kind.Bgm);

        public static void SetEnabled(bool on) => ApplyVolume();

        static void ApplyVolume()
        {
            if (source == null) return;
            source.volume = Volume;
            source.mute = !Enabled;
        }
    }
}
