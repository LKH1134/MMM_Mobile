using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // 성능 벤치마크. 정해진 시나리오(스테이지 × 스킬 레벨)마다 InGame 씬을 새로 열어, 사람 대신 자동 조작으로 판을 돌리며
    // 프레임 시간·CPU(메인·렌더 스레드)·GPU·메모리·드로우 콜을 매 프레임 잰다. 저장값은 건드리지 않는다:
    //   스킬 레벨은 SkillProgress.SimLevels, 판 결과(지갑·스테이지·결과 팝업)는 HarvestGame.Simulating으로 건너뛴다.
    //   보스는 죽지 않게(BossHpMul) 해서 판 내내 같은 부하를 준다. 제한 시간도 측정이 끝날 때까지 늘린다.
    //   배속은 시나리오의 speed로 고정한다 (HUD 배속 버튼과 같은 Time.timeScale. 측정 시간은 배속과 상관없이 실제 초).
    // 결과: Application.persistentDataPath/Benchmark/<시각>_summary.csv (시나리오별 요약) · _timeline.csv (1초 단위).
    //   안드로이드: /sdcard/Android/data/<패키지>/files/Benchmark/ — 끝나면 화면에도 요약을 띄운다 (눌러서 닫으면 SkillTree 씬으로).
    // 시작: Benchmark.Start() — 에디터 메뉴 MoreMush/Debug/Run Benchmark, 개발 빌드 옵션 팝업의 벤치마크 버튼,
    //   또는 실행 인자 "-benchmark" (선택: "-benchmark-seconds=60", "-benchmark-repeat=3"). 안드로이드는
    //   adb shell am start -n <패키지>/com.unity3d.player.UnityPlayerGameActivity -e unity "-benchmark"
    //   Firebase Test Lab Game Loop: TEST_LOOP 신호로 켜지면(매니페스트는 빌드 때 GameLoopManifest가 붙인다) 바로 시작하고,
    //   끝나면 요약·1초 기록을 Firebase 결과 파일(신호의 data URI)에 쓰고 앱을 끈다. 시나리오 번호는 쓰지 않는다 (늘 전부)
    // 정확한 CPU·GPU 시간은 Player Settings의 Frame Timing Stats가 켜져 있어야 나온다 (꺼져 있으면 빈칸).
    // 드로우 콜 같은 렌더 카운터는 개발 빌드에서만 값이 나오는 기기가 있다
    public static class Benchmark
    {
        public class Scenario
        {
            public string name;
            public int stage;
            public bool boss;
            public float skill;   // 모든 스킬 레벨 = 최대 레벨 × 이 값 (0 = 하나도 안 배움, 1 = 전부 최대)
            public string[] maxAbilities;   // 이 능력(SkillAbility 키)을 가진 스킬은 skill과 상관없이 최대 레벨
            public float speed = 1f;        // 배속 (StageHud 배속 버튼과 같은 Time.timeScale)
        }

        // 기본 시나리오: 가벼운 판 → 무거운 판. 스킬 최대 = 분열·분신술·수확기 수가 가장 많은 경우
        public static readonly Scenario[] Default =
        {
            new Scenario { name = "일반 1스테이지 · 스킬 없음", stage = 1, skill = 0f },
            new Scenario { name = "일반 25스테이지 · 스킬 절반", stage = 25, skill = 0.5f },
            new Scenario { name = "일반 49스테이지 · 스킬 최대", stage = 49, skill = 1f },
            new Scenario { name = "보스 분신(40) · 스킬 최대", stage = 40, boss = true, skill = 1f },
            new Scenario { name = "보스 이동(50) · 스킬 최대", stage = 50, boss = true, skill = 1f },
            // 분열 스킬만 다 찍고 배속을 바꿔 가며: 같은 판에서 배속이 프레임에 주는 영향을 본다
            new Scenario { name = "일반 25스테이지 · 분열 최대 · 1배속", stage = 25, maxAbilities = new[] { SkillAbility.SplitBuff }, speed = 1f },
            new Scenario { name = "일반 25스테이지 · 분열 최대 · 4배속", stage = 25, maxAbilities = new[] { SkillAbility.SplitBuff }, speed = 4f },
        };

        public const string InGameScene = "InGame";
        public const string ReturnScene = "SkillTree";

        public static bool Running => runner != null;
        public static bool GameLoop { get; private set; }   // Firebase Game Loop로 켜졌다
        public static string LastSummaryPath { get; private set; }

        static BenchmarkRunner runner;

        // measureSeconds: 시나리오마다 재는 시간, warmupSeconds: 재기 전에 버리는 시간 (씬을 연 직후의 튐), repeat: 전체를 몇 번
        public static void Start(float measureSeconds = 30f, float warmupSeconds = 3f, int repeat = 1, Scenario[] scenarios = null)
        {
            if (runner != null) return;
            var go = new GameObject("[Benchmark]");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<BenchmarkRunner>();
            runner.Begin(scenarios ?? Default, Mathf.Max(1f, measureSeconds), Mathf.Max(0f, warmupSeconds), Mathf.Max(1, repeat));
        }

        internal static void Finished(string summaryPath)
        {
            LastSummaryPath = summaryPath;
            runner = null;
        }

        const string TestLoopAction = "com.google.intent.action.TEST_LOOP";

        // 실행 인자 "-benchmark"나 Game Loop 신호로 켜면 첫 씬이 뜬 뒤 바로 시작한다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartFromCommandLine()
        {
            var args = new List<string>(System.Environment.GetCommandLineArgs());
            string extra = Intent(i => i.Call<string>("getStringExtra", "unity"));
            if (!string.IsNullOrEmpty(extra)) args.Add(extra);
            GameLoop = Intent(i => i.Call<string>("getAction")) == TestLoopAction;
            bool on = GameLoop;
            float seconds = 30f;
            int repeat = 1;
            foreach (var raw in args)
            {
                foreach (var a in raw.Split(' '))   // 안드로이드는 "unity" 인자 하나에 공백으로 붙어 올 수 있다
                {
                    if (a == "-benchmark") on = true;
                    else if (a.StartsWith("-benchmark-seconds=")) float.TryParse(a.Substring(19), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
                    else if (a.StartsWith("-benchmark-repeat=")) int.TryParse(a.Substring(18), out repeat);
                }
            }
            if (on) Start(seconds, 3f, repeat);
        }

        // 안드로이드: 앱을 켠 intent에서 값을 읽는다 (am start -e unity "<인자>", Game Loop 신호 등). 다른 플랫폼·실패면 null
        static string Intent(System.Func<AndroidJavaObject, string> read)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    return read(intent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Benchmark] 실행 intent를 못 읽었다: " + e.Message);
            }
#endif
            return null;
        }

        // Game Loop: Firebase가 넘긴 결과 파일(intent data URI)에 쓴다. Firebase 콘솔의 결과 파일로 받는다
        internal static void WriteGameLoopResult(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                using (var uri = intent.Call<AndroidJavaObject>("getData"))
                {
                    if (uri == null) { Debug.LogWarning("[Benchmark] Game Loop 결과 파일 URI가 없다"); return; }
                    using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                    using (var pfd = resolver.Call<AndroidJavaObject>("openFileDescriptor", uri, "w"))
                    using (var fd = pfd.Call<AndroidJavaObject>("getFileDescriptor"))
                    using (var output = new AndroidJavaObject("java.io.FileOutputStream", fd))
                    {
                        output.Call("write", Encoding.UTF8.GetBytes(text));
                        output.Call("flush");
                        output.Call("close");
                        pfd.Call("close");
                    }
                }
                Debug.Log("[Benchmark] Game Loop 결과 파일에 썼다");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Benchmark] Game Loop 결과 파일에 못 썼다: " + e.Message);
            }
#endif
        }

        internal static Dictionary<int, int> LevelsFor(Scenario scenario)
        {
            var levels = new Dictionary<int, int>();
            foreach (var s in GameData.Skills.All)
            {
                int max = SkillProgress.MaxLevel(s);
                int level = Mathf.Clamp(Mathf.RoundToInt(max * scenario.skill), 0, max);
                if (scenario.maxAbilities != null && System.Array.IndexOf(scenario.maxAbilities, s.ability) >= 0) level = max;
                if (level > 0) levels[s.index] = level;
            }
            return levels;
        }
    }

    // 벤치마크 진행 (씬을 넘어 살아남는다). 자동 조작·측정·저장·결과 표시
    public class BenchmarkRunner : MonoBehaviour
    {
        Benchmark.Scenario[] scenarios;
        float measure, warmup;
        int repeat;

        HarvestField field;
        HarvestGame game;
        StageHud hud;
        bool piloting;
        float speed = 1f;
        float errTimer, err;
        const float PilotSkill = 0.3f;   // 바 오차 (BalanceBot SKILL과 같은 뜻). 낮을수록 수확기를 잘 받아 콤보·분열이 많이 터진다

        readonly List<Result> results = new List<Result>();
        readonly StringBuilder timeline = new StringBuilder();
        string summaryPath, summaryText, error;
        bool showResult;
        Dictionary<int, int> previousSim;
        float previousTimeScale;
        int previousTargetFrameRate;
        bool previousRunInBackground;

        class Result
        {
            public string name;
            public int round, frames;
            public float speed;
            public float seconds;
            public readonly List<float> frameMs = new List<float>();
            public readonly List<float> mainMs = new List<float>();
            public readonly List<float> renderMs = new List<float>();
            public readonly List<float> gpuMs = new List<float>();
            public long memStart, memMax, memEnd, sysMax, monoMax, gcTotal;
            public double drawCalls, setPass, batches, mushrooms, harvesters;
            public int drawSamples;
        }

        public void Begin(Benchmark.Scenario[] list, float measureSeconds, float warmupSeconds, int repeatCount)
        {
            scenarios = list;
            measure = measureSeconds;
            warmup = warmupSeconds;
            repeat = repeatCount;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            previousSim = SkillProgress.SimLevels;
            previousTimeScale = Time.timeScale;
            previousTargetFrameRate = Application.targetFrameRate;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;   // 에디터 창이 포커스를 잃어도 멈추지 않게
            Debug.Log($"[Benchmark] 시작: 시나리오 {scenarios.Length}개 × {repeat}회, 측정 {measure}초 (예열 {warmup}초), 목표 {Application.targetFrameRate}fps");
            timeline.AppendLine("round,scenario,second,fps,frameMs,mainMs,renderMs,gpuMs,totalUsedMB,systemUsedMB,gcAllocKB,mushrooms,harvesters");

            for (int round = 1; round <= repeat; round++)
                foreach (var scenario in scenarios)
                {
                    yield return RunScenario(scenario, round);
                    if (error != null) break;
                }

            piloting = false;
            SkillProgress.SimLevels = previousSim;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            Application.targetFrameRate = previousTargetFrameRate;
            Application.runInBackground = previousRunInBackground;
            summaryPath = Save();
            Benchmark.Finished(summaryPath);
            if (Benchmark.GameLoop)
            {
                // Firebase는 앱이 스스로 꺼져야 테스트가 끝난 것으로 본다
                Benchmark.WriteGameLoopResult(summaryText + System.Environment.NewLine + timeline);
                Application.Quit();
                yield break;
            }
            showResult = true;
        }

        IEnumerator RunScenario(Benchmark.Scenario s, int round)
        {
            // 스킬 레벨은 씬을 열기 전에 (HarvestGame.Awake가 수치를 정한다)
            SkillProgress.SimLevels = Benchmark.LevelsFor(s);
            speed = Mathf.Max(0.01f, s.speed);
            piloting = false;
            var load = SceneManager.LoadSceneAsync(Benchmark.InGameScene);
            if (load == null) { error = $"씬을 열 수 없다: {Benchmark.InGameScene}"; yield break; }
            while (!load.isDone) yield return null;
            yield return null;   // Awake·Start가 다 돈 뒤

            field = FindFirstObjectByType<HarvestField>();
            game = FindFirstObjectByType<HarvestGame>();
            hud = FindFirstObjectByType<StageHud>();
            var effects = FindFirstObjectByType<SkillEffects>();
            var gimmicks = FindFirstObjectByType<BossGimmicks>();
            var input = FindFirstObjectByType<FieldInput>();
            if (field == null || game == null || hud == null) { error = "InGame 씬에서 판 오브젝트를 못 찾았다"; yield break; }

            // 판 준비: 측정기와 같은 순서. 보스는 죽지 않게, 시간은 측정이 끝날 때까지
            game.Simulating = true;
            if (input != null) input.Interactable = false;
            bool boss = s.boss && StageProgress.Get(s.stage) is StageData sd && sd.boss != 0;
            game.Prepare(s.stage, boss, StageProgress.LastNormalScore, StageProgress.LastNormalGold);
            if (boss) game.Stats.BossHpMul = Mathf.Max(1f, 1e9f / Mathf.Max(1f, game.Stats.BossHp));
            field.Restart();
            if (effects != null) effects.Restart();
            if (gimmicks != null) gimmicks.Restart();
            hud.ResetTimer((warmup + measure + 30f) * speed);   // 제한 시간은 게임 시간이라 배속만큼 빨리 준다
            hud.ForceSpeed(speed);   // 시간 배율과 HUD 배속 아이콘을 같이 (저장은 안 한다)
            errTimer = 0f;
            piloting = true;
            // 분신 보스는 체력 66%·33%에서 분신을 부르는데 죽지 않는 보스는 거기까지 안 깎인다 — 30%로 깎아 분신 둘을 띄운 채로 잰다
            if (boss && game.Stats.Boss.gimmick == BossGimmick.Split)
                foreach (var m in field.Mushrooms)
                    if (m.IsBoss && m.Alive)
                    {
                        // DealDamage가 보스 피해 배율을 곱하므로 나눠 둔다 (안 나누면 스킬 최대에서 한 방에 죽는다)
                        float mul = Mathf.Max(0.01f, game.Stats.BossDamageMul);
                        field.DealDamage(m, Mathf.FloorToInt(m.MaxHp * 0.7f / mul), null);
                        break;
                    }

            for (float t = 0f; t < warmup; t += Time.unscaledDeltaTime) yield return null;

            var r = new Result { name = s.name, round = round, speed = speed };
            using (var rec = new Recorders())
            {
                r.memStart = rec.TotalUsed;
                float elapsed = 0f, secondTimer = 0f;
                int secondFrames = 0;
                float secondMs = 0f;
                long secondGc = 0;
                int second = 0;
                while (elapsed < measure)
                {
                    yield return null;
                    if (game.Finished) break;   // 늘려 둔 시간이 그래도 끝나면 거기까지
                    float dt = Time.unscaledDeltaTime;
                    elapsed += dt;
                    rec.Sample(out float main, out float render, out float gpu);
                    r.frameMs.Add(dt * 1000f);
                    if (main > 0f) r.mainMs.Add(main);
                    if (render > 0f) r.renderMs.Add(render);
                    if (gpu > 0f) r.gpuMs.Add(gpu);
                    long mem = rec.TotalUsed;
                    r.memMax = System.Math.Max(r.memMax, mem);
                    r.sysMax = System.Math.Max(r.sysMax, rec.SystemUsed);
                    r.monoMax = System.Math.Max(r.monoMax, Profiler.GetMonoUsedSizeLong());
                    long gc = rec.GcAllocInFrame;
                    r.gcTotal += gc;
                    if (rec.DrawCalls > 0)
                    {
                        r.drawCalls += rec.DrawCalls;
                        r.setPass += rec.SetPass;
                        r.batches += rec.Batches;
                        r.drawSamples++;
                    }
                    r.mushrooms += field.Mushrooms.Count;
                    r.harvesters += field.Harvesters.Count;

                    secondFrames++;
                    secondMs += dt * 1000f;
                    secondGc += gc;
                    secondTimer += dt;
                    if (secondTimer >= 1f)
                    {
                        second++;
                        timeline.AppendLine(string.Join(",", round, Csv(s.name), second, F(secondFrames / secondTimer), F(secondMs / secondFrames),
                            F(main), F(render), F(gpu), F(mem / 1048576f), F(rec.SystemUsed / 1048576f), F(secondGc / 1024f),
                            field.Mushrooms.Count, field.Harvesters.Count));
                        secondTimer = 0f;
                        secondFrames = 0;
                        secondMs = 0f;
                        secondGc = 0;
                    }
                }
                r.seconds = elapsed;
                r.frames = r.frameMs.Count;
                r.memEnd = rec.TotalUsed;
            }
            piloting = false;
            results.Add(r);
            Debug.Log($"[Benchmark] {round}회 {s.name}: {Line(r)}");
        }

        // 자동 조작: 던지기 전이면 위로(조금 비스듬히) 던지고, 그 뒤로는 바를 수확기가 떨어질 자리로 (0.5초마다 바뀌는 오차)
        void Update()
        {
            if (!piloting || field == null || !field.Running) return;
            if (Time.timeScale != speed && Time.timeScale > 0f && hud != null) hud.ForceSpeed(speed);   // 배속 버튼이 눌려도 시나리오 배속으로
            if (field.MainReady)
            {
                field.LaunchMain(new Vector2(Random.Range(-0.3f, 0.3f), 1f));
                return;
            }
            if (field.Bar == null) return;
            float dt = Time.deltaTime;
            errTimer -= dt;
            if (errTimer <= 0f)
            {
                errTimer = 0.5f;
                err = Random.Range(-1f, 1f) * 2f * PilotSkill * field.Bar.GetRect(field.Rect).width * 0.5f;
            }
            if (BalanceBot.AimX(field, out float x)) field.MoveBarTo(x + err, 5000f * dt);
        }

        // ── 저장 ──
        string Save()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "Benchmark");
            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string summary = System.IO.Path.Combine(dir, stamp + "_summary.csv");
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("round,scenario,speed,seconds,frames,avgFps,low1Fps,avgFrameMs,p95FrameMs,maxFrameMs,jankPct,avgMainMs,p95MainMs,avgRenderMs,avgGpuMs," +
                              "memStartMB,memMaxMB,memEndMB,systemMaxMB,monoMaxMB,gcAllocKBPerSec,avgDrawCalls,avgSetPass,avgBatches,avgMushrooms,avgHarvesters," +
                              "device,os,cpu,cores,ramMB,gpu,screen,targetFps,frameTimingStats,debugBuild,unity");
                string device = string.Join(",", Csv(SystemInfo.deviceModel), Csv(SystemInfo.operatingSystem), Csv(SystemInfo.processorType), SystemInfo.processorCount,
                    SystemInfo.systemMemorySize, Csv(SystemInfo.graphicsDeviceName), $"{Screen.width}x{Screen.height}", Application.targetFrameRate,
                    FrameTimingManager.IsFeatureEnabled() ? 1 : 0, Debug.isDebugBuild ? 1 : 0, Application.unityVersion);
                foreach (var r in results)
                {
                    float avgMs = Avg(r.frameMs);
                    sb.AppendLine(string.Join(",", r.round, Csv(r.name), F(r.speed), F(r.seconds), r.frames, F(avgMs > 0f ? 1000f / avgMs : 0f), F(Low1Fps(r)),
                        F(avgMs), F(Percentile(r.frameMs, 0.95f)), F(Max(r.frameMs)), F(JankPct(r)), F(Avg(r.mainMs)), F(Percentile(r.mainMs, 0.95f)),
                        F(Avg(r.renderMs)), F(Avg(r.gpuMs)), F(r.memStart / 1048576f), F(r.memMax / 1048576f), F(r.memEnd / 1048576f),
                        F(r.sysMax / 1048576f), F(r.monoMax / 1048576f), F(r.seconds > 0f ? r.gcTotal / 1024f / r.seconds : 0f),
                        F(r.drawSamples > 0 ? (float)(r.drawCalls / r.drawSamples) : 0f), F(r.drawSamples > 0 ? (float)(r.setPass / r.drawSamples) : 0f),
                        F(r.drawSamples > 0 ? (float)(r.batches / r.drawSamples) : 0f), F(r.frames > 0 ? (float)(r.mushrooms / r.frames) : 0f),
                        F(r.frames > 0 ? (float)(r.harvesters / r.frames) : 0f), device));
                }
                summaryText = sb.ToString();
                System.IO.File.WriteAllText(summary, summaryText, new UTF8Encoding(true));   // BOM: 엑셀에서 한글이 안 깨지게
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, stamp + "_timeline.csv"), timeline.ToString(), new UTF8Encoding(true));
                Debug.Log($"[Benchmark] 끝: {summary}");
                return summary;
            }
            catch (System.Exception e)
            {
                error = "저장 실패: " + e.Message;
                Debug.LogError("[Benchmark] " + error);
                return null;
            }
        }

        // ── 결과 화면 (개발용이라 OnGUI) ──
        void OnGUI()
        {
            if (!showResult) return;
            float scale = Screen.width / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = 1080f, h = Screen.height / scale;
            GUI.Box(new Rect(0, 0, w, h), GUIContent.none);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true, richText = true };
            var sb = new StringBuilder("<b>벤치마크 결과</b>\n\n");
            if (error != null) sb.Append("<color=#ff8080>").Append(error).Append("</color>\n\n");
            foreach (var r in results) sb.Append("<b>").Append(r.round > 1 || repeat > 1 ? $"[{r.round}] " : "").Append(r.name).Append("</b>\n").Append(Line(r)).Append("\n\n");
            sb.Append(summaryPath != null ? "저장: " + summaryPath : "저장 안 됨").Append("\n\n화면을 누르면 닫는다");
            GUI.Label(new Rect(40, 40, w - 80, h - 80), sb.ToString(), style);
            if (Event.current.type == EventType.MouseUp)
            {
                showResult = false;
                if (Application.CanStreamedLevelBeLoaded(Benchmark.ReturnScene)) SceneManager.LoadScene(Benchmark.ReturnScene);
                Destroy(gameObject);
            }
        }

        static string Line(Result r)
        {
            float avgMs = Avg(r.frameMs);
            string cpu = r.mainMs.Count > 0 ? $" · 메인 {Avg(r.mainMs):0.0}ms" : "";
            string gpu = r.gpuMs.Count > 0 ? $" · GPU {Avg(r.gpuMs):0.0}ms" : "";
            return $"평균 {(avgMs > 0f ? 1000f / avgMs : 0f):0.0}fps · 하위1% {Low1Fps(r):0.0}fps · 끊김 {JankPct(r):0.0}%{cpu}{gpu} · 메모리 최대 {r.memMax / 1048576f:0}MB · GC {(r.seconds > 0f ? r.gcTotal / 1024f / r.seconds : 0f):0.0}KB/s";
        }

        // 하위 1%: 가장 느린 1% 프레임들의 평균 시간으로 본 fps
        static float Low1Fps(Result r)
        {
            if (r.frameMs.Count == 0) return 0f;
            var sorted = new List<float>(r.frameMs);
            sorted.Sort();
            int n = Mathf.Max(1, sorted.Count / 100);
            float sum = 0f;
            for (int i = sorted.Count - n; i < sorted.Count; i++) sum += sorted[i];
            return sum > 0f ? 1000f / (sum / n) : 0f;
        }

        // 끊김: 목표 프레임 시간의 1.5배를 넘긴 프레임 비율
        static float JankPct(Result r)
        {
            if (r.frameMs.Count == 0) return 0f;
            float target = Application.targetFrameRate > 0 ? 1000f / Application.targetFrameRate : 1000f / 60f;
            int jank = 0;
            foreach (var ms in r.frameMs) if (ms > target * 1.5f) jank++;
            return 100f * jank / r.frameMs.Count;
        }

        static float Avg(List<float> v)
        {
            if (v.Count == 0) return 0f;
            double sum = 0;
            foreach (var x in v) sum += x;
            return (float)(sum / v.Count);
        }

        static float Max(List<float> v)
        {
            float m = 0f;
            foreach (var x in v) m = Mathf.Max(m, x);
            return m;
        }

        static float Percentile(List<float> v, float p)
        {
            if (v.Count == 0) return 0f;
            var sorted = new List<float>(v);
            sorted.Sort();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        // 프레임마다 읽는 카운터 묶음. 기기·빌드에 없는 카운터는 0
        sealed class Recorders : System.IDisposable
        {
            ProfilerRecorder totalUsed, systemUsed, gcAlloc, drawCalls, setPass, batches, mainThread;
            readonly FrameTiming[] timings = new FrameTiming[1];

            public Recorders()
            {
                totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
                systemUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
                gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            }

            public long TotalUsed => totalUsed.Valid && totalUsed.LastValue > 0 ? totalUsed.LastValue : Profiler.GetTotalAllocatedMemoryLong();
            public long SystemUsed => systemUsed.Valid ? systemUsed.LastValue : 0;
            public long GcAllocInFrame => gcAlloc.Valid ? gcAlloc.LastValue : 0;
            public long DrawCalls => drawCalls.Valid ? drawCalls.LastValue : 0;
            public long SetPass => setPass.Valid ? setPass.LastValue : 0;
            public long Batches => batches.Valid ? batches.LastValue : 0;

            // 직전 프레임의 CPU 메인·렌더 스레드, GPU 시간(ms). Frame Timing Stats가 꺼져 있으면 메인만 프로파일러 카운터로 (없으면 0)
            public void Sample(out float main, out float render, out float gpu)
            {
                main = render = gpu = 0f;
                if (FrameTimingManager.IsFeatureEnabled())
                {
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        main = (float)timings[0].cpuMainThreadFrameTime;
                        render = (float)timings[0].cpuRenderThreadFrameTime;
                        gpu = (float)timings[0].gpuFrameTime;
                        return;
                    }
                }
                if (mainThread.Valid && mainThread.LastValue > 0) main = mainThread.LastValue / 1e6f;
            }

            public void Dispose()
            {
                totalUsed.Dispose();
                systemUsed.Dispose();
                gcAlloc.Dispose();
                drawCalls.Dispose();
                setPass.Dispose();
                batches.Dispose();
                mainThread.Dispose();
            }
        }
    }
}
