# HANDOFF — 모바일 최적화 · 안정화

> 작성 2026-10-30 · 기준 브랜치 `KHD` (`de3432e` [MOD] 261030 모바일 최적화·안정화)
> 이 문서는 **이번 최적화·안정화 작업 하나**를 인계한다. 무엇을 왜 바꿨는지, 어떻게 확인하는지, 문제가 생기면 무엇을 되돌리는지, 남은 일이 무엇인지 정리한다. 전체 구조는 `HANDOFF.md`(요약은 8장 "최적화·안정화")를 본다.
> 표시: ✅ 확인됨 · ⚠️ 확인 필요 · ❓ 결정 필요

---

## 0. 한눈에 보기

| 항목 | 상태 |
|---|---|
| 런타임·에디터 코드 컴파일 (`dotnet build` Assembly-CSharp / -Editor) | ✅ 오류 0 · 경고 0 |
| 서브에이전트 코드 리뷰 지적 3건 반영 | ✅ |
| Unity 에디터 플레이 확인 | ⚠️ 안 함 (작업 중 Unity MCP 연결 안 됨) |
| 실기기 확인 (프레임·발열·저장) | ⚠️ 안 함 |
| 서브 캔버스의 그리는 순서 | ⚠️ 눈으로 확인 필요 (4장) |
| 스프라이트 아틀라스 / `homeTabs` / 던전 시간 = 앱 밖 시간 | ❓ 이번에 손대지 않음 (6장) |

**먼저 할 일:** Unity 에디터를 **저장하지 말고 껐다가 다시 열기**. 에디터가 열린 상태에서 `ProjectSettings.asset`·`Mobile_RPAsset.asset`을 디스크에서 고쳤기 때문에, 그대로 File > Save Project를 하면 메모리에 있던 옛 설정이 이번 변경을 덮어쓸 수 있다.

---

## 1. 왜 했나 — 점검에서 찾은 것

Main 씬의 방치 전투(`IdleBattle`)는 앱이 켜져 있는 내내 돈다. 점검 결과 비용은 대부분 그림이 아니라 **코드의 저장값 읽기·쓰기**에서 나왔다.

- **PlayerPrefs를 프레임마다 읽음 (가장 큼).**
  - 매 프레임 `IdleProgress.FinalHp` → `GachaProgress.OwnPercent` → `Equipment.TotalPercent`가 호출된다.
  - `TotalPercent`는 장비 15개마다 `Level(i)`를 부르고, `Level(i)`는 매번 `Migrate()` 확인과 `PlayerPrefs.GetInt`(키 문자열도 새로 생성)를 한다.
  - 그래서 **프레임당 약 70번 + 수확기를 던질 때마다 약 70번** 읽는다. 안드로이드에서 PlayerPrefs 읽기는 SharedPreferences를 부르는 JNI 호출이다.
  - `Formation.Get/Level`도 던질 때마다 3번씩 읽었다.
- **PlayerPrefs.Save()가 너무 잦음.** 전투 골드는 1초마다 `Wallet.Set`에서 Save를 불렀고, 강화 버튼을 꾹 누르면 초당 수십 번 Save가 불렸다. Save는 저장 파일 전체를 메인 스레드에서 다시 쓴다.
- **캔버스 하나에 모든 UI가 있음.** 전투가 매 프레임 움직이면 같은 캔버스의 강화 목록·하단바·재화 바까지 다시 묶였다.
- **설정:** 화면 꺼짐 방지가 없어서 기기 화면이 꺼지는 시간(30초~2분)이 절전 모드(3분)보다 먼저 왔고, 그때 전투가 멈췄다. 또 Render Scale 0.8 때문에 Main UI 전체가 80%로 그려져 흐렸다.

---

## 2. 바뀐 것 (파일별)

### 2.1 성능

| 파일 | 내용 |
|---|---|
| `Idle/Equipment.cs` | 레벨·조각·장착을 처음 한 번만 읽어 배열에 둔다(`EnsureLoaded`). 쓸 때는 배열과 PlayerPrefs를 같이 고친다(`SetLevel`·`SetCopies`·`SetEquipped`). `TotalPercent`는 레벨·장착이 바뀔 때만 다시 셈한다(NaN = 다시 셈). 키 문자열은 생성자에서 한 번만 만든다. 초기화는 `Equipment.ReloadAll()`. |
| `Idle/Formation.cs` | 같은 방식으로 칸·꼬마 레벨·조각을 캐시한다. 초기화는 `Formation.Reload()`(옛 세이브 옮기기 플래그도 같이 초기화). |
| `SaveScheduler.cs` (새 파일) | 숨긴 `DontDestroyOnLoad` 오브젝트다. `MarkDirty()`로 표시한 값을 **5초마다·`OnApplicationPause(true)`·`OnApplicationFocus(false)`·`OnApplicationQuit`** 때 `PlayerPrefs.Save()`한다. `Flush()`로 바로 저장할 수 있다. |
| `Wallet.cs` · `Idle/IdleProgress.cs` · `Idle/IdleAway.cs` · `UI/StageHud.cs` · `InGame/InGameOption.cs` | 자주 바뀌는 값(골드, 강화 레벨, 마지막 접속 시각과 초당 골드, 배속, 볼륨)은 Save 대신 `SaveScheduler.MarkDirty()`를 부른다. |
| `Idle/IdleBattle.cs` | Awake에서 `IdleBattle`·`EnemyArea`·`GgomaArea`에 `Canvas`를 붙인다(`AddSubCanvas`, 입력을 받는 `GraphicRaycaster`는 HUD 전투력 버튼이 있는 `IdleBattle`에만). 숫자 팝업은 `canvasRenderer.SetAlpha`로 흐려진다. 체력·전투력 글자는 값이 바뀔 때만 쓴다. 앱이 멈출 때와 끝날 때 `SaveScheduler.Flush()`를 부른다. |
| `Idle/HitEffects.cs` | 흐려짐을 `canvasRenderer.SetAlpha`로 바꿨다(정점을 다시 만들지 않음). |
| `Idle/UpgradePanel.cs` | `Wallet.Changed`(1초마다)는 `RefreshAfford`(버튼 활성·색만)로, `IdleProgress.Changed`는 전체 `Refresh`로 나눴다. |
| `UI/PowerSaveMode.cs` | 씬 이름을 매 프레임 읽지 않고 씬이 바뀔 때 `inMain`에 담아 둔다. |
| `InGame/BossHud.cs` | % 글자는 바뀔 때만 쓴다. |

### 2.2 리소스·설정

| 파일 | 내용 |
|---|---|
| `Assets/Settings/Mobile_RPAsset.asset` | HDR, 메인 라이트 그림자, 라이트 쿠키, 렌즈 플레어 2종, 지형 구멍, LOD 크로스페이드, 혼합 조명, 반사 프로브 블렌딩을 껐다. **Render Scale 0.8 → 1.** |
| `ProjectSettings/ProjectSettings.asset` | Optimized Frame Pacing(`androidUseSwappy`)과 `mipStripping`을 켰다. `m_StackTraceTypes`에서 Warning·Log는 None으로 바꿨다(Error·Assert·Exception은 Script Only 그대로). |
| `AppSettings.cs` | `Screen.sleepTimeout = SleepTimeout.NeverSleep`. |
| 텍스처 | 그대로다. `b7e2863`에서 이미 크기와 압축을 정리했다(Android 기본 = ASTC, 밉맵 꺼짐, Read/Write 꺼짐). |

### 2.3 안정화

| 파일 | 고친 것 · 실패 시나리오 |
|---|---|
| `Wallet.cs` | `Add`가 long 범위를 넘으면 음수가 되고 다시 0으로 잘려 **골드가 전부 사라지던 것**을 최댓값에서 멈추게 했다. |
| `Idle/IdleAway.cs` | 초당 골드를 `InvariantCulture`로 저장하고 읽는다(기기 언어가 바뀌면 값이 1만 배 틀어질 수 있었다). `Idle.LastSeen` 범위를 검사한다(망가진 값이면 `new DateTime`이 예외를 던져 `IdleBattle.Start`가 중간에 멈췄다). 오프라인 골드에 상한을 두고 NaN을 막았다. **마지막 접속 시각은 뒤로 가지 않는다**(시계를 되돌렸다 맞추며 앱 밖 보상을 반복해서 받는 것 방지). 다만 저장값이 지금보다 하루(`MaxClockSkew`) 넘게 앞서면 틀린 시계로 보고 버린다. |
| `DungeonTickets.cs` · `GachaProgress.cs` | 날짜가 '다른 날'일 때가 아니라 **'뒤의 날'일 때만** 충전한다(`DungeonTickets.IsNewDay`). 같은 하루 여유를 둔다. |
| `InGame/ResultPopup.cs` | `SceneLoader.Load`를 쓴다. 두 번 눌러도 한 번만 넘어가고 입장 횟수도 한 번만 쓴다. 로딩 팝업이 뜬다. |
| `StageProgress.cs` · `InGame/HarvestGame.cs` | `HasBoss(s)`(보스 행이 표에 있어야 보스 스테이지)를 추가해 `BossActive`·`Complete`·`FarmRound`에 같이 쓴다. 표가 틀려도 스테이지에 갇히지 않는다. 무한 스테이지의 `hpRate`·`rewardRate`는 double로 셈하고 1e30에서 멈춘다(float가 무한대가 되던 것). |
| `SkillProgress.cs` | 가격에 `9e18` 상한을 뒀다(x86에서 `long.MinValue`가 돼 공짜가 될 수 있었다). |
| `UI/PowerSaveMode.cs` | 앱으로 돌아오면 덮개를 걷는다(뒤에 있던 시간 때문에 바로 덮이거나 방치 정산 팝업을 가리던 것). |
| `UI/DataResetButton.cs` | `Formation.Reload()`·`Equipment.ReloadAll()`을 추가했다. |

---

## 3. 꼭 지킬 규칙 (다음 작업자)

- **장비·꼬마·강화·지갑 값은 반드시 그 클래스를 거쳐 쓴다.** `Equipment`·`Formation`·`IdleProgress`·`Wallet`은 메모리 캐시다. PlayerPrefs를 직접 고치거나 지우면 화면과 전투에 반영되지 않는다. 직접 고쳤다면 다음을 부른다.
  - `Wallet.Reload()`
  - `IdleProgress.ReloadLevels()`
  - `Formation.Reload()`
  - `Equipment.ReloadAll()`
  - 플레이를 시작할 때는 `SubsystemRegistration`에서 자동으로 불린다.
- **자주 바뀌는 새 저장값은 `SaveScheduler.MarkDirty()`를 쓴다.** 드물고 중요한 값(뽑기·결제·합성·편성 등)은 지금처럼 `PlayerPrefs.Save()`를 바로 불러도 된다. 그때 밀린 값도 같이 저장된다.
- 강제 종료(OOM 등)로 앱이 pause 없이 죽으면 **최대 5초 치** 골드·강화가 저장되지 않을 수 있다. 두 값이 같은 Save에 들어가므로 "돈만 빠지고 레벨은 안 오름" 같은 반쪽 저장은 없다.
- 전투 그림을 새로 만들 때 흐려짐은 `color.a` 대신 `canvasRenderer.SetAlpha`로 한다. 꺼낼 때는 `SetAlpha(1)`로 되돌린다.

---

## 4. 확인 방법

### 4.1 에디터 (먼저)
1. 에디터를 껐다 켜고, Project Settings > Player > Android에서 Optimized Frame Pacing이 켜져 있는지, `Mobile_RPAsset`의 Render Scale이 1인지 확인한다.
2. **새 스크립트 등록:** `SaveScheduler.cs`의 `.meta`는 직접 만들어 커밋했다(GUID `961a812484c741bbb0d590f6399206f4`). Console에 에러가 없어야 하고, 플레이하면 Hierarchy에 숨은 `SaveScheduler` 오브젝트가 생긴다(HideInHierarchy라 DontDestroyOnLoad 씬에서도 안 보일 수 있음 — `Object.FindFirstObjectByType<SaveScheduler>()`로 확인). 머지 직후처럼 새 스크립트가 컴파일에서 빠지면 `HANDOFF.md` 8장 "머지 뒤 새 스크립트가 컴파일에서 빠짐"을 본다.
3. **그리는 순서 (⚠️ 가장 중요):** Main을 플레이해서 숫자 팝업, 타격 이펙트, 수확기가 **옵션 버튼·재화 바(`WalletBar`)·강화 목록(`UpgradePanel`)보다 위로 그려지지 않는지** 본다. 중첩 캔버스는 원래 계층 순서대로 그려지는 것으로 알고 있지만 이번에 눈으로 확인하지 못했다. 문제가 있으면 `IdleBattle.Awake` 끝의 `AddSubCanvas` 세 줄을 지우면 예전과 같아진다(성능 이득은 사라짐).
4. HUD 전투력 버튼을 누르면 통계 창이 열고 닫히는지 본다(서브 캔버스의 `GraphicRaycaster`).
5. Profiler(CPU·GC Alloc)로 Main 전투를 몇 초 잡는다. `PlayerPrefs.GetInt`·`GetString`이 프레임마다 보이지 않아야 하고, `Canvas.SendWillRenderCanvases`·`Canvas.BuildBatch`가 줄어야 한다.
6. **저장:** 강화 버튼을 꾹 눌러 레벨을 올린 뒤 5초 안에 플레이를 멈춘다. 에디터 정지는 `OnApplicationQuit`을 부르므로 다시 플레이했을 때 레벨과 골드가 남아 있어야 한다.
7. 던전 1 → 결과 팝업에서 "한 번 더하기"를 빠르게 두 번 눌러도 입장 횟수가 1만 줄어야 한다.

### 4.2 실기기
1. 3분 동안 손대지 않으면 화면이 꺼지지 않고 절전 덮개가 뜨는지 본다.
2. 홈 버튼으로 앱을 내리고 1분 넘게 기다린 뒤 돌아왔을 때 방치 정산 팝업이 보이는지 본다(절전 덮개가 가리지 않음).
3. 앱을 내린 상태에서 최근 앱 목록으로 강제 종료한 뒤 다시 켰을 때 골드·강화가 유지되는지 본다(pause 때 저장).
4. 벤치마크(`HANDOFF.md` 8장)나 Firebase Test Lab으로 전·후 프레임과 발열을 비교한다. 이번에는 숫자를 재지 못했다.

---

## 5. 되돌리기

| 문제 | 되돌릴 곳 |
|---|---|
| 전투가 다른 UI 위로 그려짐 | `IdleBattle.Awake`의 `AddSubCanvas` 세 줄 삭제 |
| 저장이 늦어서 문제 | `Wallet.Set`·`IdleProgress.TryUpgrade`의 `SaveScheduler.MarkDirty()`를 `PlayerPrefs.Save()`로 |
| UI가 너무 무거움(채움 비용) | `Mobile_RPAsset` Render Scale을 0.9 등으로 (UI가 조금 흐려짐) |
| 시계 보호 때문에 정상 사용자가 막힘 | `IdleAway.MaxClockSkew`(1일)를 줄이거나, `IdleAway.MarkSeen`·`DungeonTickets.IsNewDay`를 예전처럼 "지금 시각 / 다른 날" 비교로 |
| 전체 | `git revert de3432e` |

---

## 6. 손대지 않은 것 · 다음 후보

1. ❓ **스프라이트 아틀라스.** Sprite Packer가 꺼져 있다(`EditorSettings` `m_SpritePackerMode: 0`). 작은 UI·아이콘·전투 그림(`Resources/Art/Dream/Icons`·`UI`·`Window`·`Characters/Critters`·`FX`·`Harvesters`)을 Sprite Atlas V2로 묶으면 드로우 콜이 줄어든다. 배경·배너처럼 큰 그림은 넣지 않는다. 에디터에서 결과를 보며 해야 해서 이번에는 하지 않았다.
2. ❓ **`IdleBattle.homeTabs`가 Main 씬에서 비어 있다.** 그래서 전투가 늘 '홈'으로 동작한다. 탭을 열어도 전투가 숨지 않고 계속 그려지며(가려질 뿐), 탭에서 돌아와도 방치 정산 팝업이 뜨지 않는다. 의도한 것인지 정해야 한다. 연결하면 탭이 열린 동안 서브 캔버스를 꺼(`canvas.enabled = false`) 그리기를 아예 멈출 수 있다.
3. ❓ **InGame(던전)에 있던 시간도 앱 밖 시간으로 센다.** `IdleAway.MarkSeen`은 Main의 `IdleBattle`만 부르기 때문에, 던전을 1분 넘게 하고 돌아오면 방치 보상도 같이 받는다. 의도가 아니면 InGame에서도 `MarkSeen`을 부르면 된다.
4. 남은 작은 할당 정리(효과 작음):
   - 숫자 팝업 문자열(타격마다 `FormatNumber`)
   - `GachaResultPopup`이 뽑을 때마다 칸을 새로 만드는 것(풀로 바꾸기)
   - `HarvestField`의 `IReadOnlyList` foreach 박싱
   - `GachaPanel`이 1초마다 Refresh하는 것
5. 절전 모드 중에도 전투 그림을 계속 움직인다. 카메라는 꺼지지만 캔버스 갱신은 남는다. `PowerSaveMode.Active`일 때 전투의 꾸밈 애니메이션을 건너뛰면 전지를 더 아낄 수 있다.
6. 숫자 팝업에 폴백 폰트 글리프가 섞이면 `TMP_SubMeshUI`는 `SetAlpha`를 따르지 않아 흐려지지 않는다. 지금 폰트(LINESeedKR)로는 해당 없다.
