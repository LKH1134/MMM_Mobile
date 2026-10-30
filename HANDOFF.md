# HANDOFF — MoreMustMush 모바일 포팅

> 작성 2026-10-28 · **갱신 2026-10-30** · 기준 브랜치 `InGame` (`dabec53`까지)
> itch.io에 PC 웹빌드로 출시한 **MoreMustMush**를 모바일(세로)로 옮기는 Unity 프로젝트입니다. 이 문서는 지금까지의 구조와 다음 작업자가 알아야 할 것을 정리합니다. 팀 공유용 요약은 claude.ai의 "MoreMustMush 모바일 인계문서"에 있습니다.
>
> **2026-10-29~30 방향 전환:** 게임은 **방치형**이 되었고 처음 화면은 `Main` 씬입니다. 기존 판 플레이(`InGame` 씬)는 10-30 한 번 지웠다가 KHD(`3183125`)가 **던전 1로 되살렸습니다**(Main 던전 탭 → InGame → 결과 팝업 → Main, 4장). **`SkillTree` 씬은 제거**(`Backup/SkillTree/`에 백업, 5장). **`WorldTree` 씬도 10-30 늦게 제거**(`Backup/WorldTree/`에 백업, 하단바 세계수 = "아직 개발중인 기능입니다." 알림). 방치형 화면은 **3장 3.1**, 기획 논의는 `HANDOFF_IDLE_PIVOT.md`, 방치형 수치는 `BALANCE_IDLE.md`·`밸런스설계/`를 보세요.

> **2026-10-30 최적화·안정화 (KHD `de3432e`):** PlayerPrefs 캐시(`Equipment`·`Formation`), 저장 모으기(`SaveScheduler`), 전투 서브 캔버스, URP·Player 설정, 저장·시계·넘침 안정화. 요약은 8장, **자세한 인계·확인 방법·되돌리기는 `HANDOFF_OPTIMIZATION.md`**. 에디터·실기기 확인 전입니다.

> **2026-10-30 해상도 대응:** 모든 씬의 UI는 `Canvas/SafeArea/Stage`(`StageFrame`) 아래에 있습니다. **높이 2340 고정, 폭 1080~1316 가변, 남는 공간은 검정.** 새 UI는 폭 1080에서 잘리지 않게 앵커로 맞추세요(3.2).

> **2026-10-30 데이터 표 정리:** 새 통합 표는 `Assets/Data/Tables/GameBalance.xlsx`입니다(적·보스·꼬마·업그레이드·스테이지·리소스·`info`). 기존 엑셀 5개와 대응 `.bytes`는 각각 `Tables/Legacy/`로 이동했습니다. **이번 범위는 엑셀 정리 + Unity 임포트·검증까지이며, 방치형 전투·강화·편성의 하드코딩 수치를 새 표로 전환하지 않았습니다.** 자세한 내용은 6장과 엑셀 `info`.

---

## 1. 한눈에 보기

| 항목 | 상태 |
|---|---|
| **Main 방치형 전투** (꼬마 5칸이 수확기 투척, 웨이브 5 + 보스, 패배 시 같은 스테이지 처음부터) | ✅ 동작 (3.1) |
| Main 강화 목록 (공격력·체력·재생·공격 속도·치명타 + 상급 공격력·일반 적 피해량) / 방치 정산 팝업 / 앱 밖 골드 | ✅ 동작 (성장 곡선 10-30 재설계, `BALANCE_IDLE.md`) |
| Main 하단 탭: 편성(꼬마 편성·장비) · 던전 · 뽑기(결과 연출 `GachaResultPopup`) | ✅ 동작. 던전은 **던전 1만 열림**(→ InGame, **하루 2회·한국 00시 충전**), 2~5는 잠김. 뽑기는 종류마다 **하루 1회 무료 11회**(광고 없음) |
| 던전·뽑기 탭 그림 (배너·카드, Codex 생성) | ✅ 2026-10-30 (`Assets/Art/Generated/Dungeon`·`Gacha`) |
| 해상도 대응 (`StageFrame`·`FitWidth`, 16:9~20:9·노치, 남는 쪽 검정) | ✅ 2026-10-30 에디터에서 측정 (3.2). 실기기 재확인 필요 |
| Android 뒤로가기 (팝업 닫기 → 홈 → 종료 확인 / InGame = 설정) | ✅ 2026-10-30 로직 확인. 실제 키 입력은 실기기 확인 필요 |
| Google Play 대상 API 36 / 세로 고정 | ✅ 2026-10-30 (8장 "출시 준비"). 서명 키·AAB는 아직 |
| 재화: 웨이브 적 = 골드, 보스 = 균사석 + 골드 / Main 재화 바 상시 표시 | ✅ 2026-10-30 (테스트 균사석 50,000 지급 제거·회수) |
| 씬 전환 로딩 팝업 (`SceneLoader`) / 팝업 하나만 (`PopupGate`) / 팝업 층 (`PopupLayer`) | ✅ 2026-10-30 |
| 절전 모드 (`PowerSaveMode`, Main에서 3분 무입력 → 검은 덮개·10fps) | ✅ 2026-10-30 (KHD) |
| InGame = **던전 1** (판 하나 → 결과 "한 번 더하기 · 나가기") + 첫 입장 튜토리얼 | ✅ 2026-10-30 복원 (KHD). 아래 InGame 줄은 판 플레이 규칙 기록 |
| InGame 핵심 플레이 (수확기·바·군락지·타이머·점수·콤보) | ✅ 동작 |
| 배속 1/2/4, 결과·옵션 팝업 | ✅ 동작 (결과 팝업은 10-30 간소화: 번 골드만) |
| ~~스킬 트리 화면~~ | 🗑️ **씬 제거 (10-30, KHD)** — `Backup/SkillTree/`. 스킬 표·`SkillProgress`는 남음 (5장) |
| 스킬 해금 조건 = 선으로 이어진 이전 노드 1레벨 이상 하나뿐 | (스킬 트리 제거로 지금 쓰지 않음) |
| 스킬 효과 전부 (스탯·바 버프·소환·확률 스킬·장치·디버프·보스) | ✅ 동작 (플레이 모드에서 코드로 흉내 내 확인) |
| 스테이지 50개(표) + 51부터 무한 생성, 보스 5종 (기믹 4종) | ✅ 동작 (무한 스테이지 2026-10-28, 4.5) |
| 일반 스테이지 | ✅ 목표 없음 — 판을 마치면 늘 다음 스테이지 (2026-10-28) |
| 보스 HUD (이름 + 본체 체력 바) | ✅ 2026-10-28 |
| 성능 벤치마크 (FPS·CPU·GPU·메모리) + Firebase Test Lab Game Loop | ✅ 2026-10-28 (8장) |
| 데이터 초기화 | 🗑️ 10-30 설정 팝업에서 버튼 제거 (`DataResetButton` 스크립트만 남음) |
| ~~WorldTree 씬~~ | 🗑️ **10-30 제거** — `Backup/WorldTree/`(README에 되살리는 법). 하단바 세계수 = `ComingSoonButton` 알림 |
| 지갑 (골드·균사석 저장, Main 재화 바 표시) | ✅ 동작 |
| 스킬 가격 (배우기 때 차감) | ✅ 동작 |
| 밸런스 (가격·스테이지 보상·보스 체력) | ⚠️ M2(10-27)에서 50보스 88.3분까지 맞췄으나, 그 측정은 일반 스테이지 목표가 있던 규칙 기준 — 목표 삭제·해금 조건 변경(10-28) 뒤 다시 재야 함 (6.2) |
| 밸런스 측정기 `BalanceBot` | ✅ 헤드리스 캠페인·보스 반복 측정 |
| 사운드 | ❌ 슬라이더 값만 저장 |
| 실기기 터치 테스트 | ⚠️ 안 함 (스킬 트리 스크롤·바 조작 모두 에디터에서만. 벤치마크만 Firebase 실기기에서 돌려 봄) |
| 아트 | ⚠️ 임시. 등급·황금·버프·장치·보스는 색으로만 구분 |

**환경:** Unity 6000.3.19f1 · URP · Input System (`InputSystemUIInputModule`) · TextMesh Pro · Unity MCP (`.mcp.json`, `http://localhost:8080/mcp`) · 이미지 생성은 Codex CLI(`codex exec`, `image_generation` 기능)
**저장소:** `https://github.com/LKH1134/MMM_Mobile` (`main`. 작업 브랜치: `InGame`(방치형 통합, 이 문서 기준) · `KHD`(장비·UI·밸런스, 10-30 `3183125`까지 InGame에 머지됨) · `LKH`(10-28까지). 원격에 `Admob`·`UI_SafeArea` 브랜치도 있음 — 이 문서는 다루지 않음)
**커밋 규칙:** `[태그] YYMMDD 설명` (예: `[ADD] 261026 스테이지·보스 표 및 보스 기믹`)

---

## 2. 커밋

### 2026-10-29 ~ 10-30 (`InGame` 브랜치, 방치형 전환)

| 커밋 | 날짜 | 내용 |
|---|---|---|
| `e3f4710`·`35fbb31`·`8371051` | 10-29 | Main 방치형 전투·강화, 꼬마 편성·편성 해제 (LKH) |
| `bcc39f5`~`13ecfcc` | 10-29 | KHD: UI 리워크(SafeArea·하단바·패널 프리팹), 하단 탭(`TabGroup`·`MainNavigation`·`MainTabButton`), **장비 초안**(`Equipment`·`EquipmentPanel`·`CombatPower`·`IdleAway`·`IdleReportPopup`) |
| `90598e0` | 10-30 | KHD → InGame 머지 (폰트 아틀라스 충돌은 InGame 쪽 유지) |
| `3c45137` | 10-30 | Start → **Main** 연결, 전투 배경 임시 이미지 |
| `d762fb8` | 10-30 | Main 옵션 버튼 복구, **InGame 씬으로 가는 UI 연결 해제** |
| `b2b2f54` | 10-30 | 옵션 팝업이 전투 뒤로 숨는 문제(`UiPopup`이 열릴 때 자기 캔버스로 맨 위) |
| `dcd8d5a` | 10-30 | **씬 전환 로딩 팝업** `SceneLoader` |
| `5e91ce7` | 10-30 | 세계수 홈·던전 버튼이 팝업을 잠깐 띄우던 문제 |
| `34cb9c0` | 10-30 | 전투 레이어(꼬마·수확기·버섯)를 `SafeArea` 안 배경 바로 위로 (패널·팝업 위로 보이던 문제) |
| `f0d8b0c` | 10-30 | 테스트 균사석 지급 제거·회수, **Main 재화 바(`WalletBar`) 상시 표시** |
| `e7c6336` | 10-30 | 던전·뽑기 페이지를 전투 아래 영역(스크롤)으로, 탭이 열리면 강화 목록 숨김, **`PopupGate`(팝업 하나만)**, 스킬트리 하단바 연결, **`InGame.unity` 삭제·빌드 제외** |
| `9e89056` | 10-30 | 던전 목록 "던전 1~5" + "해금되지 않음" 덮개 |
| `7538439`~`260fb33` | 10-30 | 편성 화면: 장착 장비 아이콘(`EquippedGearView`), 임시 흰 박스 → 게임 UI 리소스, '동료'→'꼬마', 꼬마 이름 대조 띠, 장비 이름 겹침 |
| `a009228`·`50a12da` | 10-30 | 꼬마 고르기 팝업 정리(위 빈 UI 삭제, 아래 버튼 줄 숨김, 하단바 위로), **하단바 나무 판 배경** |
| `c3b9dc7`·`50137ed` | 10-30 | **Main 배경 한 장(`bg_main.png`, Codex 생성)**, 편성 프리팹 TMP 저장값 정리 |
| `5a152c4` | 10-30 | 꼬마 배치(앞 3 · 뒤 2 지그재그), HANDOFF 갱신 |
| `c6b2c82` | 10-30 | **방치형 통합 표 `GameBalance.xlsx`** + 검사기(`GameBalanceValidator`), 기존 표 5개는 `Tables/Legacy/` |
| `d1d8b1e`·`c74f65d` | 10-30 | 간판(`header_plaque`) 나뭇잎 제거·수정 |
| `3183125` | 10-30 | **KHD:** 성장 곡선 재설계(골드·강화 비용 지수 항, 치명타 0.1%/Lv, 공속 상한 제거), 강화 2종(상급 공격력·일반 적 피해량)과 단계 제한, **던전 1 → InGame 연결·InGame 씬 복원**, InGame 결과 팝업 간소화·첫 입장 튜토리얼, 뽑기 결과 연출, **절전 모드**, `PopupLayer`, `TabGroup` 탭별 `hide`, 편성·던전·뽑기 패널을 화면 위까지, 설정 창 정리, 세계수 뒤로가기 → Main, **SkillTree 씬·스크립트 제거**(`Backup/SkillTree/`), 골드 획득량 강화 제거, `밸런스설계/` 폴더 |
| `a61e7f0` | 10-30 | KHD → InGame 머지 (충돌 없음) |
| `bdc30df` | 10-30 | HANDOFF 정리 (KHD 머지 반영) |
| `4a783a7` | 10-30 | **KHD:** 방치 전투 최적화 — `Wallet` 잔액·`IdleProgress` 강화 레벨을 메모리에 둠(`Wallet.Reload`·`IdleProgress.ReloadLevels`), 적 공격력은 스테이지 바뀔 때만 계산, 보스 HUD 초 글자는 초가 바뀔 때만 |
| `b647911` | 10-30 | **KHD:** **꼬마 강화(합성)** — 레벨·조각(장비와 같은 방식), 레벨마다 공격 배율 +5%, 꼬마 고르기 아래 정보 칸 `CharacterDetail`(편성·합성·일괄 합성), 예전 보유 장수 세이브 옮김 |
| `034c20f` | 10-30 | **KHD:** 뽑기 카드 오른쪽 열 개편 `GachaCardView` — 뽑기 레벨·경험치 막대, 광고 소환(초록) · 11회/35회(보라, 재화 아이콘·가격) |
| `a902a76` | 10-30 | **KHD:** 타격 이펙트 `HitEffects` (MoreMustMush `FX/hit_0~4`, `Resources/Art/Dream/FX`) |
| `dc7de93` | 10-30 | **KHD:** 편성 3×2(꼬마 5 + **펫 칸**), 하단 꼬마/펫 탭(`Down`) 삭제, 꼬마 고르기 X 닫기 버튼 |
| `cd69bd5` | 10-30 | KHD → InGame 머지 (충돌 없음) |
| `cfbfc2c`·`d3b5fa2` | 10-30 | **던전·뽑기 탭 그림**(Codex 생성 배너·카드), 배너를 화면 위에 고정하고 목록 판을 그 위로 |
| `4baa8d1`·`854549e` | 10-30 | 회사명·게임명 `soomyouth`/`moremustmush` 저장(브랜치 전환 때 되돌아가던 것), 앱 아이콘 v2 |
| `c2feb81` | 10-30 | **던전 입장 횟수**: 하루 2회, 한국 00시 충전(`DungeonTickets`·`DungeonEntry`·`DungeonResetTimer`), 결과 "한 번 더하기"도 1회 사용 |
| `5e58cd0`·`fe15987` | 10-30 | 설정 팝업 게임 종료·데이터 초기화 버튼 제거, 꼬마 고르기 `Outside` 크기 정리 |
| `278ebce` | 10-30 | **해상도 대응**: `StageFrame`(디자인 영역)·`FitWidth`, 모든 씬 UI를 Stage 아래로, 고정 폭 UI 앵커 재정비 (3.2) |
| `339b0e4` | 10-30 | 광고 11회 뽑기 → **하루 1회 무료 뽑기**(한국 00시) |
| `b7e2863` | 10-30 | **대상 API 36 고정·세로 고정**, 안 쓰는 리소스 삭제(폰트 4종이 WorldTree의 낡은 머티리얼 오버라이드 때문에 빌드에 끌려 들어가던 것 등), 텍스처 크기·압축 정리 |
| `72c6a1d` | 10-30 | 설정 도움말 버튼 제거, **세계수 탭 = "개발중" 알림**(`ComingSoonButton`), **WorldTree 씬 제거**(`Backup/WorldTree/`), 인게임 "로비로 돌아가기" → Main |
| `dabec53` | 10-30 | **Android 뒤로가기**(`BackButton`·`MainBackHandler`) |

### 2026-10-26 ~ 10-28 (`LKH` 브랜치, 판 플레이 시절)

그 전 커밋은 `git log`로 보세요.

| 커밋 | 날짜 | 내용 |
|---|---|---|
| `6f65608` | 10-27 | 콤보 규칙(수확 +1 / 바닥 0, 점수용 상한 20·계수 0.05), 보스 판 황금 제외, 일반 스테이지 목표(→ 10-28 삭제), 보스 패배 패널티 제거·재도전/파밍, 첫 클리어 균사석, 보스 체력 재맞춤, `AGENT_WORKLOG.md` |
| `e3ee606` | 10-28 | **일반 스테이지 목표 삭제**(판을 마치면 늘 다음), 결과 팝업 "다음 스테이지 / 스킬트리로", HUD "스테이지 n" |
| `6a62d2d` | 10-28 | 금액 단위 '억' Jua에 추가 |
| `2c5aad9` | 10-28 | **50스테이지 뒤 무한 스테이지** (10개마다 보스 재사용) |
| `b6a3536` | 10-28 | 51스테이지부터 이름이 비던 문제 |
| `3ed657f` | 10-28 | **성능 벤치마크** + 목표 60fps |
| `cf30e51` | 10-28 | Firebase Test Lab Game Loop + 안드로이드 패키지 이름 |
| `ec2fb57` | 10-28 | Game Loop intent-filter 위치 (Firebase 인식) |
| `25f525f` | 10-28 | 벤치마크 시나리오 배속·특정 스킬 최대 옵션 |
| `22c4f33` | 10-28 | 벤치마크 배속 시나리오의 HUD 배속 아이콘 |
| `ceb7ede` | 10-28 | **보스 HUD** (이름 + 본체 체력 바), 보스 머리 위 작은 바 제거 |
| `d005fbd` | 10-28 | 버프 글자 표시 제거 |
| `7159ba7` | 10-28 | **스킬 트리 가지별 페이지** + 가로 3칸 자동 배치(`SkillTreeLayout`) |
| `51bbacc` | 10-28 | 스킬 트리 연결 선 좌우 균형 |
| `c926b09` | 10-28 | **해금 조건 = 이전 노드(link) 1레벨 이상만** (requires 제거) |
| `ea9c2b2` | 10-28 | Skill 표에 `tier` 칸 |

**작업 트리에 늘 남는 수정:** `Assets/Settings/DefaultVolumeProfile.asset`·`Mobile_RPAsset.asset`·`UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ProjectSettings.asset`은 Unity가 열 때 스스로 다시 쓰는 파일이라 계속 "수정됨"으로 뜹니다. 일부러 바꾼 게 아니면 커밋에 넣지 마세요.
`LiberationSans SDF - Fallback.asset`은 TextMesh Pro가 실행 중에 스스로 고치는 파일이라 계속 "수정됨"으로 뜹니다. 동작과는 무관합니다.
`Assets/_Recovery/`는 Unity를 강제 종료할 때 생긴 씬 백업입니다. 커밋하지 않았고 지워도 됩니다.

---

## 3. 씬 흐름

빌드 씬: `Start` · `Main` · `InGame` (`SkillTree`·`WorldTree`는 10-30 삭제, `Backup/`에 백업. `InGame`은 던전 1 "입장하기"로 들어가고 결과 팝업에서 `Main`으로 돌아온다)

`Start`(퍼센트 로딩, `StartLoading.nextScene = Main`) → **`Main`(방치형, 상주)**
`Main` 던전 탭 → 던전 1 "입장하기"(`DungeonEntry`, 입장 횟수 1 사용) → **`InGame`**(판 하나) → 결과 팝업 "한 번 더하기"(입장 횟수 1 사용, 없으면 막힘) / "나가기"(→ `Main`). 인게임 설정 "로비로 돌아가기" → `Main`

- 하단바(`BottomBar` 프리팹): 편성 · 세계수 · 홈 · 던전 · 뽑기. `TabGroup`(SafeArea)이 편성(`Equit`)·던전(`DungeonUI`)·뽑기(`GachaUI`) 페이지를 켜고 끕니다. 홈 = 아무 페이지도 없음. **세계수 = `ComingSoonButton`**("아직 개발중인 기능입니다." 알림, `ConfirmPopup.ShowNotice`).
- 씬을 바꾸는 곳은 `SceneLoader.Load(이름)`을 씁니다(가운데 나이테 아이콘이 도는 로딩 팝업, 최소 0.3초, 터치 막음). 예외: Start → Main(`StartLoading` 퍼센트 화면), **벤치마크는 아직 `SceneManager.LoadScene` 직접 호출**. 인게임 설정 "로비로 돌아가기"·결과 팝업 "한 번 더하기/나가기"(`ResultPopup`, 10-30 최적화 작업)는 `SceneLoader` — 두 번 눌러도 한 번만 넘어가고 입장 횟수도 넘어갈 때만 씀.
- **뒤로가기(Android 버튼·제스처, 에디터 Esc, `BackButton`):** 열린 팝업이 있으면 닫기(`PopupGate.HandleBack`, 닫기 수단이 없는 InGame 결과 팝업은 안 닫힘) → 없으면 씬 처리: Main = 탭이 열려 있으면 홈, 홈이면 "게임을 종료할까요?"(`MainBackHandler`) / InGame = 설정 팝업(`StageHud`). 로딩 중엔 무시.

### 3.1 Main (방치형)

**Stage(`SafeArea/Stage`) 자식 순서 = 그리는 순서(뒤 → 앞):** `BG`(배경 한 장) · `GgomaArea` · `EnemyArea` · `IdleBattle`(전투 HUD·수확기·숫자) · `OptionButton` · `TopBar`(꺼짐) · `BottomBar` · `Equit`(편성) · `UpgradePanel`(강화 목록) · `DungeonUI` · `GachaUI` · `WalletBar`(골드·균사석) · `OptionPopup`

- 전투 레이어는 **`Stage` 안, 배경 바로 위**여야 합니다. 캔버스 직속으로 두면 모든 패널·팝업 위로 올라옵니다(10-30 수정).
- **배경:** `BG` 한 장(`Assets/Art/Generated/Backgrounds/bg_main.png`, 940×1672, Codex 생성)이 전투 들판부터 하단바 뒤까지 덮습니다. `AspectRatioFitter`(Envelope Parent)로 비율을 지키며 꽉 채움. 예전 전투 배경 `InGameBG`는 삭제.
- **꼬마 배치:** `GgomaArea`(232×362) 안 `Ggoma1~5` = 편성 칸 1~5. 앞줄(x +58) 1·2·3, 뒷줄(x −58) 4·5 지그재그, 모두 가운데 앵커 100×100 (10-30 정리).
- **전투(`IdleBattle`):** 웨이브 `IdleProgress.WavesPerStage` + 보스(제한 시간). 적 처치 골드는 1초마다 모아 `Wallet`에, 보스 처치 = 균사석 `BossMycelite` + 골드 `BossGold`(일반 20마리분) + 다음 스테이지. 꼬마 무리 체력이 0이면 같은 스테이지 1웨이브부터. 수치는 `BALANCE_IDLE.md`.
- **하단 탭:** 탭이 열리면 `UpgradePanel`은 숨김(`TabGroup.homeOnly`). **10-30 KHD 이후 편성(`Equit`, y 233~2340)·던전·뽑기(y 200~2340) 패널은 하단바 위부터 화면 맨 위까지 덮습니다**(전투는 탭이 열린 동안 가려짐). 던전·뽑기는 `ScrollRect`(Viewport/Content). 탭별로 열린 동안 끌 것은 `TabGroup.Tab.hide`(지금 Main 씬에선 비어 있음).
- **편성(`Equit`):** `GGoma`(칸 3×2 = 꼬마 칸 5 + 6번째 **펫 칸**(`Middle/Pet`, 누르면 펫 창) + 자동 편성 + 꼬마 판 위에 붙은 장착 장비 칸 `EquippedGearView`), `Char`(꼬마 고르기 — 목록 위쪽(`Outside`) 또는 오른쪽 위 X(`Out`, 코드 생성)로 닫힘), `Wepon`·`Amor`·`Pet`(장비 팝업, `EquipmentPanel`). 장비 탭은 `TabGroup.pagesArePopups`라 다른 팝업과 한 번에 하나만. 하단 꼬마/펫 탭(`Down`)은 10-30 KHD가 삭제.
- **꼬마 강화(KHD 10-30):** 처음 얻으면 Lv.1, 또 얻으면 조각, 조각 (Lv+1)개로 +1레벨(최대 100, `Equipment.CopiesToNext`와 같음). 공격 배율 = 1레벨 배율 × (1 + 0.05 × (Lv−1)) — 5%는 임시값. 꼬마 고르기에서 꼬마를 누르면 **선택만** 되고 아래 정보 칸 `CharacterDetail`(그림·등급·Lv·조각 막대·배율→다음)의 **편성**으로 칸에 놓음, **합성**·**일괄 합성**으로 레벨업. 미보유도 정보는 보임(편성·합성 막음). 첫 칸 X는 바로 편성 해제.
- **던전:** 위에 배너(`Banner`, 화면 위에 고정 — 남은 시간 = 다음 한국 00시까지, `DungeonResetTimer`), 그 아래 목록 판(`ListPanel`, 배너 아래를 28 덮음) 안에서 카드 "던전 1~5"가 스크롤. 카드마다 `DungeonEntry`(남은 횟수 `n/2`, 0이면 입장 막힘). **던전 1만 씬 연결**(`InGame`), 2~5는 "해금되지 않음" 덮개(`LockOverlay`, 씬 없음). 횟수는 `DungeonTickets`(던전마다 하루 `PerDay`=2, `Dungeon.<n>.Day/Used`).
- **뽑기:** 배너("소환" 현판) + 목록 판 안 `GachaPanel` 4종(수확기·방어구·펫·꼬마, 카드 그림 = 요정의 고리, 대표 그림은 고리 가운데). 오른쪽 열(`GachaCardView`): 뽑기 레벨·경험치, **무료 11회(하루 1회, 한국 00시, `GachaProgress.FreeAvailable`)** · 11회·35회(균사석). 결과는 `GachaResultPopup`(칸이 하나씩 튀어나옴, 등급 색, NEW!!, 누르면 건너뛰기).
- **팝업:** `PopupGate` — 새 팝업이 열리면 열려 있던 것을 닫음(`UiPopup`·꼬마 고르기·장비 탭·방치 정산). 하단 탭을 바꾸면 모두 닫힘. 그리는 층은 **`PopupLayer.Raise(go, offset)`**(KHD 10-30) — 자기 `Canvas`를 붙여 부모 캔버스 + offset에 그리고 입력도 먼저 받음: 편성 안 팝업 +10(`Panel`), `UiPopup`·뽑기 결과 +100(`Popup`), 토스트 +200(`Toast`, 입력 통과). 그래서 Dim을 누르면 팝업만 닫힘.
- **재화:** `Wallet`(골드·균사석). `WalletBar`가 `Wallet` 값. 예전 테스트 지급(균사석 50,000, `Wallet.TestGrant`)은 받은 기기에서 한 번 회수(`Wallet.TestReclaimed`).
- **방치 보상:** `IdleAway` — 탭을 연 동안·앱 밖 시간의 골드를 모아 홈으로 돌아오면 `IdleReportPopup`(오버레이 캔버스 30000).
- **강화 목록 잠금 문구:** 이름·값 글자(x 180~) 오른쪽 자리(x 330~)에만 쓰고 폭이 좁으면 글자를 줄인다(`UpgradePanel.LockTextLeft`).
- **절전 모드:** `PowerSaveMode`(코드로 만드는 오버레이 31000, 씬에 둘 것 없음). Main에서 3분 무입력 → 검은 덮개 + 카메라 끄기 + 10fps, 절전 중 번 골드·스테이지 표시, 누르면 해제. 전투는 계속 돎.

### 3.2 해상도 대응 (2026-10-30, `278ebce`)

실기기(약 20:9)에서 좌우·위아래가 잘린 것을 고쳤습니다. 원인: 에디터 게임 뷰가 16:9라 캔버스 폭이 1316인 상태로 UI를 고정 폭으로 맞춰 왔음(Expand 1080×2340 → 20:9 폰에선 캔버스 폭 1080).

- **`StageFrame`**(모든 씬 `Canvas/SafeArea/Stage`): 높이 **2340 고정**, 폭 **1080~1316**(화면에 맞춤). 더 길거나 넓은 화면은 남는 쪽이 **검정**(카메라 배경). 노치·홈바로 안전 영역이 모자라면 통째로 축소(scale < 1). `[ExecuteAlways]`라 편집 중에도 게임 뷰 비율대로 보임.
- **앵커 규칙:** 전체 폭 요소는 가로 stretch(하단바, 던전·뽑기 카드·판), 가장자리 요소는 그쪽 앵커(이름표 왼쪽, 열쇠·버튼 오른쪽). 칸 위치를 고정값으로 박은 묶음(편성 3×2 `Middle`, 꼬마 고르기 `Char/Char`, 장비 창 `Wepon`·`Amor`·`Pet`)은 **`FitWidth`**(designWidth 1316, 비율 유지로 통째 축소, 플레이 중에만).
- **배경:** Main `BG`는 Stage를 Envelope로 채움(1316×2340 = 그림 비율). Start·InGame 배경은 `BGMask`(RectMask2D) 안, InGame Stage에 RectMask2D(필드 밖 장식 벽).
- **팝업은 Stage 아래에 만든다**(노치·홈바에 안 걸리게). 코드로 만드는 오버레이(방치 정산 1080×2340 기준, 로딩, 절전)는 화면 전체.
- **확인 방법:** 게임 뷰 크기 목록에 `MMM 1080x2340 (19.5:9)`·`MMM 1080x2400 (20:9)`·`MMM 1440x3200 (20:9)` 추가해 둠(+ 16:9 `1920x1080 Portrait`). 플레이 중 모든 Graphic의 월드 사각형이 Stage 밖으로 나가는지 재는 방식으로 Main(탭·장비 창·꼬마 고르기·옵션·정산·뽑기 결과)·InGame·Start를 측정 → 넘침 0. 노치는 SafeArea 앵커를 직접 바꿔 흉내(위 120·아래 60).
- **작업 기준:** 게임 뷰를 **1080×2340**으로 두고 작업하세요. 16:9에서만 맞추면 같은 문제가 다시 생깁니다.

---

## 4. InGame (던전 1)

> 10-30 `e7c6336`에서 지웠다가 같은 날 KHD `3183125`가 **던전 1로 복원**하고 빌드에 다시 넣었습니다. 들어오는 길은 Main 던전 탭의 던전 1 "입장하기"뿐이고, 판 하나를 마치면 결과 팝업(번 골드만 표시, "한 번 더하기" · "나가기")에서 Main으로 돌아갑니다. 스킬 트리가 없어져 판 사이 성장은 없고, `DungeonRequest`(`HANDOFF_IDLE_PIVOT.md` 1장)도 아직 없어 늘 일반 스테이지 기본값으로 시작합니다. 첫 입장 때 튜토리얼(`InGameTutorial`: "드래그하여 발사" → "버섯을 캐십쇼", 한 번만). 벤치마크는 끝나면 Main으로 돌아옵니다. 아래 판 규칙·수치는 그대로입니다(결과 팝업 버튼 설명 중 "다음 스테이지·보스 도전·파밍·스킬트리로"는 옛 내용).

### 4.1 판 규칙

1. 판이 시작되면 수확기가 바 위에서 대기합니다. **타이머는 첫 투척부터** 흐릅니다. 던지기는 한 판에 한 번(함께 던지는 수확기가 있으면 같이 나감).
2. 끌었다 놓으면 끈 반대 방향으로 던집니다(40px 미만은 취소). 수확기는 감속 없이 같은 속도로 벽·바·버섯에 튕깁니다.
3. 던진 뒤에는 화면 어디를 눌러도 좌우로 끄는 만큼 바가 움직입니다. 바는 **윗면만** 판정합니다.
4. **군락지:** 가운데 군락 버섯 1개 + 둘레 단일 버섯 n개. 군락지 하나는 같은 등급(일반·에픽·유니크)이고, 버섯마다 따로 황금 변종이 정해집니다. 군락지를 다 캐면 사라지고 리스폰 시간 뒤 빈 자리에 새로 돋습니다. 처음엔 벌집 격자에 놓고, 자리가 모자라면 버섯을 최소 0.35배까지 줄입니다.
5. 한 방에 캐지는 버섯은 뚫고 지나가고, 체력이 남는 버섯에는 튕깁니다(관통 버프 중엔 안 튕김).
6. **점수**는 판 중에 쌓이고, 끝나면 **골드 = 점수 × 전환율**을 지갑(`Wallet`)에 넣습니다. 결과 팝업에 골드·점수·수확량(버섯 개수).
7. 콤보 = 판 전체에 하나(`HarvestField.Combo`). 수확기가 버섯을 캘 때마다 +1, **수확기가 바를 놓쳐 바닥 벽에 닿으면 0**(바에 닿아도 유지). 분열체처럼 잠깐 있는 수확기가 바닥에 닿는 건 끊지 않음. HUD·점수 배율이 같은 값을 봄. 점수 ×(1 + min(콤보, `comboCap` 20) × 콤보 계수 0.05) — **콤보 점수 배율은 스킬 전 기준 최대 ×2.0**(스킬 `ComboRate`·피버 `ComboMul`이 계수 0.05에 곱해지므로 스킬을 배우면 ×2.0을 넘을 수 있음, 의도), HUD 콤보 숫자는 상한 없이 계속 오름(2026-10-27, 측정 M1: 상한 없이 0.1이면 후반 골드 ×9~21).
8. 게임 오버는 시간 종료, 또는 보스 처치(보스 판). 결과 팝업엔 닫기가 없고 다음 판 / 스킬트리로만 있습니다.
9. **일반 스테이지 = 목표 없음 (사용자 결정, 2026-10-28 `e3ee606`):** 일반 판은 마치면 늘 다음 스테이지로 넘어갑니다. 10-27에 넣었던 "버섯 N개 수확" 목표·연속 실패 완화(`goalRelief`·`FailStreak`·튜토리얼 고정 목표)는 코드에서 지웠고, Stage.xlsx의 `goalType`·`goal` 칸만 표에 남아 있습니다(게임은 안 읽음). `mushHpMul`(단단한 스테이지: 일반 버섯 체력 배율, 색은 `hardTint`로 조금 어둡게)은 그대로 씁니다. 보스 스테이지는 보스를 잡아야 넘어갑니다(4.4).
10. **균사석 첫 클리어 (2026-10-27):** 보스 10·20·30·40·50 첫 처치 10·15·20·25·30(Boss.xlsx `gem`) + ★ 5·15·25·35·45 첫 클리어 3·4·5·6·7(Stage.xlsx `gem`) = 총 125. `StageProgress.Cleared`(키 `Stage.Cleared`, 넘은 가장 높은 스테이지)보다 높은 스테이지를 넘을 때만 지급(`StageProgress.FirstClearGem`) — 재도전·파밍·50보스 다시 잡기·목표 실패는 0. 결과 팝업에 보라색 강조 줄 "첫 클리어 보상 · 균사석 +N"(카운트업).
    - 옛 세이브 이관: 키가 없으면 처음 읽을 때 `Current − 1`. 단 `Current`가 마지막(50)이고 지금까지 받은 균사석(지갑 + 균사석 노드에 쓴 몫, `SkillProgress.Spent`)이 50 전 보스 합(70)을 넘으면 50보스를 이미 잡은 세이브로 보고 50. **근사 판정**(`StageProgress.MigrateCleared`): Current<50이면 정확하지만 Current=50일 때만 휴리스틱이라 (1) 옛 규칙은 같은 보스를 다시 잡아도 균사석을 또 줬으므로(개발 메뉴로 되감은 뒤 재처치 등) 50 미클리어인데 합 >70이면 50으로 오인, (2) `Spent`는 **지금** 가격으로 합산하므로 가격표가 바뀌기 전(ddffddf 이전)에 균사석 노드를 산 세이브는 합이 어긋날 수 있음. 실제 사용자 세이브(Current 22)엔 영향 없음. 개발용 `StageProgress.Set(n)`은 `Cleared = n − 1`.

### 4.2 판 수치 (`RoundStats`, `RoundBase`)

판 시작 때 `HarvestGame.Awake`가 배운 스킬(`SkillProgress.Total`)과 지금 스테이지로 `RoundStats`를 만들어 필드·HUD에 넣습니다. 판 중에는 바뀌지 않습니다.
`RoundBase`(HarvestGame 인스펙터)에 스킬이 없을 때의 기본값과, 표에 없어서 정한 값이 있습니다.

| 값 | 기본 | 출처 |
|---|---|---|
| 제한 시간 | 15초 | 표의 시작값 |
| 군락지 / 둘레 단일 버섯 / 리스폰 | 3 / 3 / 7초 | 표의 시작값 |
| 에픽 / 유니크 확률 | 5% / 1% | 정함 |
| 콤보 1당 점수 / 점수용 콤보 상한 | +5% / 20 | PC판 10%를 M1 측정으로 낮춤 (`comboFactor`·`comboCap`) |
| 치명타 피해 (치명 박살 없을 때) | +50% | 정함 |
| 황금 변종 점수 / 수확량 | ×10 / ×5 | PC판 |
| 가속 | +30% | 정함 |
| 분열체 수 / 크기 / 공격 | 2 / 0.7배 / 0.5배 | 정함 |
| 포자 디버프 확률 / 시간 / 느림 / 약화 | 20% / 3초 / 0.75배 / 0.5배 | 정함 (PC판 비슷하게) |
| 기본 보스 체력 | 100 | 정함 (× 보스 배수 × 스테이지 계수) |

### 4.3 스킬 효과 (`SkillAbility` 키 → 효과)

스킬 효과는 **Skill.xlsx 설명**을 따릅니다. 표의 원본은 PC 프로젝트 기획 초안 `C:\Project\MoreMustMush\기획서\균사트리_배치안\생성스크립트\nodes.py`이고, PC판 실제 트리와 다른 스킬이 많습니다. 표에 없는 세부만 PC판(`Assets/Scripts/Round/RoundSim*.cs`)과 `기획서/전투기획서_Ver05_정리.md`를 참고했습니다.

- 표의 수치는 **레벨당 더하는 값**입니다(누적값으로 적힌 줄은 앞 노드와의 차이로 넣음). 같은 능력 키를 가진 노드들의 합이 `SkillProgress.Total(ability)` = Σ(abilityBase + abilityValue × 레벨).
- `param` = 레벨과 무관한 고정 수치(번개 공격력 30%, 버프 지속 등). `SkillProgress.Param`으로 읽습니다.
- **바 버프:** 확률이 안 적힌 버프(사냥 준비·가속·관통·유도·피버·지혜·약손)는 **바에 닿을 때마다 100%** 걸리고 시간이 새로 채워집니다(사용자 결정). 분열·분신술만 10%. 분열체는 바 버프를 받지 않습니다.
- **확률 발동:** 이독제독(단일 버섯 캘 때), 연속 번개(군락지를 다 캘 때), 회오리·포자 폭발(가운데 군락 버섯에 부딪힐 때). 지혜 버프 중엔 발동률 +. 스킬 피해가 다른 스킬을 다시 부르지 않게 막아 두었습니다.
- **최적화:** 분열체·분신 바·꼬마 수확기·번개 그림은 **판 시작 때 최대치만큼 풀에 미리 만듭니다**(사용자 요청). 판 중에 Instantiate하지 않습니다.

| 파일 | 맡은 일 |
|---|---|
| `RoundStats.cs` | `RoundBase`(기본값) + 스킬·스테이지로 계산한 판 수치 |
| `HarvestField.cs` | 이동·충돌, 군락지 배치·리스폰, 등급·황금, 치명타, 관통·유도·가속·느림, 여러 바, 장애물, 임시 수확기, 보스 몸 |
| `HarvestGame.cs` | 점수·수확량·골드, 스테이지·보스 판 결정, 보스 처치, 스테이지 진행 |
| `SkillEffects.cs` | 바 버프, 분열, 분신 바, 확률 스킬, 포자 디버프, 꼬마 수확기 (버프 글자 목록은 10-28 삭제, 수확기 색 표시만) |
| `FieldDevices.cs` | 환경 복원: 그루터기(튕김 + 공격력) · 꽃밭(가속) · 도토리 3개(다 켜면 점수 2배) |
| `BossGimmicks.cs` | 보스 기믹 4종. `Step(dt)`로 시험 가능 |
| `BossHud.cs` | 보스 판 상단 패널 아래 보스 이름 + 본체 체력 바·% (깎인 몫은 잔상), 분신이 살아 있으면 "분신 ×N". 일반·파밍 판엔 숨김 |
| `StageHud.cs` | 타이머·점수·콤보·배속, "스테이지 n". `ForceSpeed`(저장 안 하는 배속, 벤치마크용) |
| `Harvester.cs` | 버프 타이머(`HarvesterBuff`: Power·Haste·Pierce·Homing·Heal·Stump·Slow·Weak), 임시 여부·수명, 다음 타격 배율 |
| `Mushroom.cs` | 등급·황금·크기·체력 배율, 군락지 번호, IsCenter·IsBoss, 회복 |

이벤트(확장 지점): `HarvestField.Launched`, `BarHit`, `Hit(m, h, damage, crit)`, `Harvested(m, h)`(스킬 피해면 h가 null일 수 있음), `ColonyCleared(center)`, `BeforeColonies`. 스킬 피해는 `HarvestField.DealDamage(m, damage, source)`.

> **충돌 계산은 Physics2D를 쓰지 않습니다.** 화면이 전부 UI 캔버스라 Field 로컬 좌표(캔버스 픽셀)에서 원-원·원-선분 충돌을 직접 계산합니다.

### 4.4 보스 판

- Stage.xlsx에서 `boss`가 있는 스테이지(10·20·30·40·50)가 보스 판입니다. 군락지 대신 보스가 나오고 다시 돋지 않습니다.
- 보스 체력 = 100 × 보스 `hpRate` × 스테이지 `hpRate`. 관통되지 않고, 유도·번개·회오리는 보스를 노립니다.
- 맞힐 때마다 점수 = 피해 ÷ 보스 체력 × 0.35(`bossHitShare`, 기본 비율 — 다 깎았을 때 받는 몫) × **직전 일반 판 점수**. 보스 체력을 넘친 피해는 점수가 없습니다.
- 분신까지 모두 쓰러뜨리면 처치: 추가 골드 **max(이번 판, 직전 일반 판) × 3**(`bossRewardMul`, PC판 2에서 올림) + 첫 처치면 균사석(`gem`) + 레전더리 버섯, 판이 바로 끝납니다.
- 못 잡으면 **패널티 없음**: 그 판 골드 × `failGold`(표 1.0), 강제 파밍 없음. 결과 팝업 버튼이 **재도전 / 파밍 / 스킬트리로** 셋이 됩니다. 파밍 = `StageProgress.ChooseFarm` → `FarmRounds`(1)판 같은 스테이지 일반 판 뒤 재도전. SkillTree 스테이지 글자에 "파밍 n". 끈기(진 판마다 보스 피해 ↑)·스킬 스테이지 잠금은 넣지 않음.
- **RoundBase 값은 `InGame.unity`에 직렬화**(`comboFactor`·`comboCap`·`bossHitShare`·`bossRewardMul`·`hardTint` 등 모두 씬에 있음). 씬 값이 우선이므로 C# 기본값만 바꾸면 반영되지 않습니다 — 인스펙터(HarvestGame)에서 고치세요. C# 기본값은 씬 값과 같게 맞춰 둠.
- **보스 판엔 황금 버섯이 나오지 않습니다**(`HarvestField.SpawnMushroom` :702 `golden = !stats.BossRound && …`, 1차 작업 T4에서 의도한 변경). 지금은 보스 판에 군락지가 없어 이 경로로 버섯이 안 나오므로 방어용 가드(보스 몸은 `SpawnBossPart`에서 원래 황금 아님). 보스 스테이지의 **파밍 판**은 일반 판이라 황금이 나옴.
- **보스 HUD (2026-10-28):** `BossHud` — 상단 패널 아래 이름(Boss.xlsx `name`) + 본체 체력 바(그림 `progress_fill_boss.png` = 타이머 바 채움의 색만 빨강). 본체가 먼저 쓰러지고 분신이 남으면 바는 0%로 남습니다. 보스 머리 위 작은 체력 바는 띄우지 않습니다.
- 보스 체력 (M2, 2026-10-27 다시 맞춤): Boss.xlsx `hpRate` 0.1809 · 0.5278 · 1.52 · 1.5 · 14.84 → 체력 72 · 256 · 1,054 · 1,040(+분신) · 15,434. 목표 첫 시도 처치율 90·85·60·55·45%. M1의 "30·50 보스 벽"은 옛 체력(10-26 맞춤)이 **e5f377e 전 바 폭 0 버그 상태**(바가 필드 가운데 폭 0 → 수확기가 위쪽 절반만 오감, 보스 피해 30에서 ×2.5)로 잰 값이었기 때문 — 바를 고친 뒤 30·50이 벽이 됐음. 50은 ★ 균사석으로 3339(꼬마 수확기)를 더 사게 되어 체력을 거의 그대로 둠. 40은 3341(분신술) 구매 여부로 피해가 약 5배 갈려 3341 없이 ~40% 처치가 되게 30보다 낮춤.

| 기믹 | 보스 | 동작 |
|---|---|---|
| None | 대왕송이 | 없음 |
| Heal | 숲 속의 청소부 | 3초마다 최대 체력의 3% 회복 |
| Purge | 죽음의 천사 | 2초마다 임시 수확기(분열체) 전부 제거 |
| Split | 환상 버섯 | 본체 66%·33%에서 분신 하나씩 (분신 체력 = 그 순간 본체 체력) |
| Move | 움직이는 버섯 | 좌우 260px/초, 몸마다 따로 벽에서 돌아섬 |

### 4.5 무한 스테이지 (2026-10-28, `StageProgress`)

- Stage.xlsx는 50스테이지까지입니다. 51부터는 `StageProgress.Get` → `Generate`가 **표의 마지막 10개 묶음(41~50)을 본떠** 만듭니다(`CycleLength` 10). 이름은 "들판 61"처럼 마지막 지역 이름 + 번호.
- 체력·보상 계수(`hpRate`·`rewardRate`)는 한 바퀴마다 표의 마지막 묶음이 커진 만큼(50 ÷ 40) 곱해 끊김 없이 이어 갑니다. `mushHpMul`·★ `gem`은 본뜬 자리 값 그대로입니다(첫 클리어에만 줌).
- 10의 배수 스테이지는 보스: 표의 보스 5종을 차례로 다시 쓰고, 마지막 보스(50)만큼 버티게 체력을 맞춥니다(`StageData.BossHpMul`, 코드에서만 있는 값).
- 마지막 스테이지(50)에 머물던 옛 세이브는 `Current = max(저장값, Cleared + 1)`로 넘은 스테이지 다음으로 이어집니다. 개발 메뉴·측정기도 표 뒤까지 진행합니다.

---

## 5. 스킬 트리 (SkillTree 씬) — 10-30 제거, `Backup/SkillTree/`에 백업. 아래는 기록용 (`SkillProgress`·스킬 테이블은 남아 있음)

| 파일 | 역할 |
|---|---|
| `SkillTreeScreen.cs` | 하단 메뉴, 설정 팝업, 노드 선택 ↔ 스킬 정보 창, 스테이지 이름 표시, 가지 넘기기 화살표(`BranchPrev`·`BranchNext`) |
| `SkillTreeView.cs` | `Build(branch)`: 시작 노드 + 한 가지의 노드를 깔고 선·이름(뒤에 어두운 판)을 그린다. 가지를 넘길 때마다 다시 만든다 |
| `SkillTreeLayout.cs` | 한 가지의 자동 배치(노드 자리 + 선 꺾임점). UI와 무관한 순수 계산 |
| `SkillTreeZoom.cs` | 두 손가락 확대·축소(에디터는 Ctrl+휠). 지금은 트리 폭 = 화면 폭이라 축소는 1배에서 막히고 확대(~1.25배)만 됩니다 |
| `SkillNode.cs` | 노드 하나 (`Setup(index)`) |
| `SkillInfoPanel.cs` | 이름·레벨·설명·못 채운 선행 조건, 배우기 버튼(가격 표시, 돈이 모자라면 꺼짐) |
| `SkillProgress.cs` | 레벨 저장(PlayerPrefs), 선행 조건, 가격(`Price`·`NextPrice`·`CanAfford`, `LevelUp`이 지갑에서 차감), `Total`·`Param`·`SeriesLevel`, `SkillAbility` 키. `SimLevels`를 넣으면 저장값 대신 그 레벨을 씀(측정기) |
| `Wallet.cs` | 골드·균사석(PlayerPrefs, long), `Add`·`TrySpend`·`Changed`, `Format`(만·억·조·경) |

- **노드 규격 (사용자 확정):** `Prefabs/Mobile/SkillTree/SkillNode` — 루트 150×150 `btn_cream`, Icon 80×80(위로 16), Level Jua 24pt. 새 노드는 이 프리팹 인스턴스로, `skillIndex`만 바꿉니다. 모양을 바꿀 땐 먼저 물어보세요.
- **선행 조건 (사용자 확정, 2026-10-28):** 트리에서 선으로 이어진 이전 노드(`link`)를 1레벨 이상 배웠을 것 — 이것 하나뿐입니다. 표의 `requires`(같은 스킬의 앞 레벨 노드, 135노드 중 89개에 있었고 모두 선과 다른 곳을 가리킴)는 선으로 보이지 않는 조건이라 해금 판정에서 뺐습니다(표 칸은 남아 있고 읽기만 함). 그래서 같은 스킬의 Lv.2·Lv.3 노드를 앞 레벨 노드 없이 먼저 배울 수 있고, 계열 레벨(`SeriesLevel`: 연속 번개 Lv.3 이상이면 어디서나 번개, 회오리 Lv당 지속 +1초)도 그 노드의 레벨로 바로 올라갑니다. 밸런스 측정기(`BalanceBot`)도 같은 판정을 씁니다.
- **가지 페이지:** 가지(독 · 식용 · 약용)마다 한 화면. 처음엔 **식용**. 양옆 화살표로 이웃 가지로 넘깁니다(독 ← 식용 → 약용, 끝 가지에선 그쪽 화살표를 숨김). 시작 노드 "사냥 준비"(3001)는 모든 가지의 맨 아래 가운데에 있고 그 가지의 첫 노드(3101·3201·3301)로 이어집니다. 넘기면 선택·스킬 정보 창은 닫힙니다.
- **배치 (코드 자동, 표의 `lane`·`row` 자리는 안 씀):** 가로 3칸(칸 간격 320, 화면 폭에 맞춤) × 층 간격 290, 위아래로만 스크롤. 가지마다 17~20층(식용 20, 독 19, 약용 17). 시작 노드 중심 높이 650(하단 메뉴 위로 ~100px 띄움). 화면에 3칸 × 4층쯤 보입니다.
  1. 층: 깊이 우선(형제는 작은 갈래부터)으로 쌓으며, 부모보다 위이고 칸이 남은 가장 낮은 층에. 한 갈래를 먼저 쌓아 대부분 부모 바로 위에 놓입니다. 단 **첫 갈림**(식용 3101, 약용 3201, 독 3301)에서는 두 갈래를 따로 쌓으며 함께 키웁니다(더 낮은 층에 놓일 쪽부터). (표의 row 순서로 쌓으면 갈래가 뒤섞여 여러 층을 건너는 선이 4배쯤 많아서 버렸습니다. 모든 갈림에서 따로 키워도 긴 선이 2배쯤 늘었습니다.)
  2. 한 층의 좌우 순서: 형제 중 먼저 쌓는 갈래를 가운데, 나머지를 왼쪽·오른쪽에 붙인 트리를 깊이 우선으로 훑은 순서. 여러 층을 건너는 선은 거치는 층마다 '지나가는 점'을 두고 도착 노드의 순서를 따릅니다 → 이웃 두 층 사이의 선끼리 엇갈리지 않습니다. 곁가지를 어느 쪽에 붙일지는 지나가는 선이 가운데 기준 좌우로 고르게 나뉘도록 찾습니다(갈림마다 하나씩 뒤집어 보며 나아질 때만 받아들이기, 고정 시드로 16번 다시 시작). 가지 하나에 수십 ms라 `SkillTreeView`가 가지별로 기억해 둡니다.
  3. 노드는 칸 가운데, 지나가는 점은 칸 사이 틈·빈 칸·바깥 틈(양옆 115px)에. 같은 틈의 선끼리는 10px씩 비키고, 아래 층에서 지나던 x를 이어 써서 곧게 갑니다.
  4. 선은 아래 점 → 층 사이 띠 → 가로 → 위 점. 같은 띠에서 가로 구간이 겹치면 엇갈리지 않는 높이로 나눕니다. 같은 부모의 갈래는 한 가로 선을 같이 씁니다.
  - 현재 표 기준 세 가지 모두 **선 교차 0, 노드·이름 침범 0**(아래에서 노드로 들어오는 세로 선은 이름 판 뒤로 지나감). 여러 층을 건너는 선은 가지마다 5~8개이고 가운데 기준 좌:우 = 독 5:3, 식용 2:3, 약용 3:4 (지나가는 층 수 10:9, 12:9, 5:7). 바깥 틈으로 가는 선은 양옆 화살표 밑을 살짝 지나갑니다.
  - 나중에 표에 위치 칸을 넣고 싶으면 `SkillTreeLayout.Build`의 1)·3) 단계만 바꾸면 됩니다.

---

## 6. 데이터 표

### 6.0 방치형 통합 표 (2026-10-30)

- **원본:** `Assets/Data/Tables/GameBalance.xlsx` → 검증 통과 시 `Assets/Resources/Tables/GameBalance.bytes`로 복사. `MoreMush/Data/Reimport Tables` 또는 저장 후 자동 임포트.
- **시트:** 적 10행, 보스 2행, 꼬마 9행, 업그레이드 7행, 스테이지 1~1000(1000행), 리소스 27행 = **1055행** + `info` 사용법/전체 컬럼 설명. 1행 영문 키, 2행 자료형, 3행 한글 설명/필터, 4행부터 데이터. `#` 컬럼은 미리보기.
- **체력 계약:** 적/보스의 `hpCoefficient × 스테이지.stageCoefficient`. 초기 계수는 기존 1스테이지 기준으로 적 2, 보스 72. 스테이지 계수는 기존 `ExpectedHit(s)`를 소수 8자리로 저장. 기존 코드의 웨이브 체력 증가·보스 기대 공격 속도 보정은 이 새 모델에 없으므로 **실제 게임 적용 전에 밸런스 재측정 필요**.
- **검증:** `Data/Editor/GameBalanceValidator.cs`가 필수 컬럼·자료형, ID/key 중복, 수치 범위, 해금 순환, 스테이지 연속 번호, 외래키, 실제 리소스를 검사. 오류가 있으면 기존 정상 bytes 유지. 검증기는 에디터 전용이라 `link.xml` 추가 불필요.
- **적용 범위:** 사용자 확정 = 표 정리/임포트/검증만. `IdleProgress`·`Formation`·`IdleBattle`은 기존 코드 수치를 계속 사용. 보스 이름도 데이터만 준비했으며 화면 표시 연결은 추후.
- **Legacy:** 아래에 설명하는 Mushroom·Harvester·Skill·Stage·Boss 엑셀은 `Assets/Data/Tables/Legacy/`, 복사본은 `Assets/Resources/Tables/Legacy/`. GUID/내용 유지. `GameData.ResourceFolder = "Tables/Legacy"`로 기존 스킬트리 로딩을 유지하며, Legacy 엑셀은 자동 재임포트에서 제외.
- **확인:** Unity 임포트 1055행 문제 0, Sprite 27개 실재, Legacy 스킬 135/스테이지 50/보스 5 로딩, 원본/bytes 일치, 수식 캐시 2014개/시트 7개 렌더 확인, 잘못된 입력 10종 거부. 플레이 모드/PlayerPrefs/열린 Main 씬의 저장 전 변경은 건드리지 않음.

**아래 표와 6.1~6.2는 Legacy 데이터 기록입니다.**

엑셀(xlsx)로 관리하고 런타임에 파싱합니다.

| 표 | 파일 | index | 상태 |
|---|---|---|---|
| 버섯 | `Assets/Data/Tables/Mushroom.xlsx` | 1001~1999 | 스키마만 |
| 수확기 | `Harvester.xlsx` | 2001~2999 | 스키마만 |
| 스킬 | `Skill.xlsx` | 3001~3999 | 135행 |
| 스테이지 | `Stage.xlsx` | 5001~5999 | 50행 |
| 보스 | `Boss.xlsx` | 6001~6999 | 5행 |

공통 열: `index`, `key`, `name`, `icon`, `desc`, `#memo`(`#`으로 시작하는 열은 읽지 않음).
**시트 형식:** 1행 열 이름(= C# 필드 이름) · 2행 자료형 · 3행 설명 · **4행부터 데이터**. `index`가 빈 행은 건너뜁니다.

**Skill.xlsx 주요 열:** `branch`(Start·Edible·Medicinal·Toxic) · `rarity`(0~19, 유저 비공개) · `tag` · `group`(같은 스킬 계열) · `startLevel` · `maxLevel` · `lane`(형제의 좌우 순서만 씀)·`row`(지금 배치엔 안 씀) · `tier`(1 = 시작 스킬, link로 한 칸 멀어질 때마다 +1, 독·약용 10 / 식용 11까지. 노드 배치에 쓰려고 넣었고 아직 코드에선 안 씀. 노드를 더하거나 link를 바꾸면 손으로 맞춰야 함) · `ability`·`abilityBase`·`abilityValue`·`param` · `unlockPrice`·`currency`(1레벨 가격·재화) · `levelPrice`·`levelCurrency`(2레벨부터 첫 가격·재화, 비우면 1레벨과 같은 재화) · `priceGrowth`(레벨마다 배율) · `link`(유일한 해금 조건) · `requires`(지금은 안 씀).

**Stage.xlsx:** `stage` · `theme`(숲 1~, 달빛 밤 20~, 들판 45~ — PC판 지역) · `hpRate`(체력 계수 1 → 10.4) · `rewardRate`(점수·수확량 배율 1 → 736, hpRate와 따로 — 수입 성장 목표용) · `boss` · `goalType`·`goal`(**표에만 남음, 게임은 안 씀** — 10-28 일반 목표 삭제) · `mushHpMul`(일반 버섯 체력 배율, 비우면 1) · `gem`(★ 첫 클리어 균사석).

**Boss.xlsx:** `hpRate`(체력 배수) · `gimmick`·`gimmickValue`·`gimmickInterval` · `color` · `reward`(처치 점수) · `drop`(레전더리 버섯) · `gem`(균사석, 첫 처치 때만) · `failGold`(지금 전부 1 = 감액 없음). 이름·기믹·색·기믹 수치는 PC판 DB(`MoreMustMush/Assets/Resources/DB/db.json`)에서 가져왔고, **체력 배수는 다시 계산**했습니다. PC판 배수(1·0.35·65·400·200)는 지역마다 ×10이 따로 붙는 구조를 전제로 해서 메모 열에만 남겼습니다.

**흐름:** 엑셀 저장 → `TableImporter`(에디터)가 `Assets/Resources/Tables/<이름>.bytes`로 복사하고 검사 결과를 콘솔에 출력 → 런타임 `GameData`가 씬 로드 전에 파싱. `.bytes` 복사본도 커밋해야 빌드에 들어갑니다.
**수동 갱신:** 메뉴 `MoreMush/Data/Reimport Tables`. 엑셀이 파일을 열고 있으면 스크립트로 저장할 수 없습니다.
**열 추가:** 행 클래스(`TableRows.cs`)에 public 필드 + 엑셀에 같은 이름의 열. **표 추가:** 행 클래스 → `GameData` 등록 → `TableImporter.Validate` → `link.xml`.

### 6.1 밸런스 (2026-10-26, 1차)

`Desktop/2026-10-26_모바일이관_밸런스_지표_이론.md`(밸런스 지표 이론 문서)의 방식으로 맞췄습니다: 이론 → 봇 측정 → 고정점 반복 → 독립 검증. 독립 검증(별도 스크립트)은 사람 모델을 바꾸기 전 버전에서 했고, 그 지적으로 사람 모델을 문서 값으로 바꿨습니다. 최종본은 다시 독립 검증하지 않았습니다.

- **사람 모델 (사용자 결정, 문서 9-R4):** 사람 수입 = 봇 × 0.6, 사람 보스 피해 = 봇 × 0.234. 문서대로 **보스 체력을 봇 도전 수로 맞추지 않습니다.** 봇은 SKILL 1.0(바를 수확기 떨어질 자리로 옮김), 판 밖 시간 60초.
- **가격:** 목표 구매 일정(위상 순서, 구간별 판당 2~3개)에 맞춰 반복 조정. 골드 45 ~ 6,500만(분신술 3341 = 26,000,000). 첫 두 스킬 45·55(첫 판 수입의 0.75 이하). 균사석 노드 4개(3001 2레벨부터·3143·3235·3339).
- **스테이지:** `hpRate`는 그 시점 공격력을 따라가고, `rewardRate`는 구간별 판당 수입 배율 목표(1~6 ×1.5, 7~11 ×1.35, 12~31 ×1.25, 32~46 ×1.15, 47~50 ×1.12)에 맞춤.
- **보스:** 목표 첫 도전 실패율 10·15·40·45·55%. 보스 판을 죽지 않는 보스로 여러 번 돌려(`BalanceBot.ProbeBoss`) 사람 피해 분포의 분위수로 체력을 정함. 환상 버섯 실효 체력은 PC 추정 ×1.97이 아니라 모바일 실측 **×3.27**. 이때 체력(100 × 보스 hpRate × 스테이지 hpRate): 51 · 244 · 2,069 · 1,236(+분신) · 16,864. **→ 2026-10-27 M2가 다시 맞춤(아래 6.2), 이 값은 옛 값.** 이 맞춤은 e5f377e 전 바 폭 0 버그 상태에서 잰 것이라 바를 고친 뒤 30·50이 벽이 됐음. ×3.27도 지금 코드의 실제 판 기준으론 약 ×2.1.

**최종 측정 (봇 20회, 실제 보스)**

| 항목 | 목표 | 결과 |
|---|---|---|
| 보스 실패율 10/20/30/40/50 | ≤15/≤15/~40/≤50/50~60% | 13/13/31/44/52% |
| 판당 수입 배율 | 위 구간 목표 | 대부분 통과, 7~11만 ×1.27 (측정마다 1.27~1.38) |
| 12~31 판당 구매 | ≥2 | 1.95 |
| 50보스 처치 | 90분 | 중앙값 76분 (65~100) |

**90분 미달은 구조 문제입니다.** 일반 스테이지는 무엇을 샀든 넘어가므로 50판 × 1.32분 ≈ 65분이 바닥이고, 시간을 늘리는 건 보스 실패뿐입니다. 판 시간·스테이지 수·일반 스테이지 통과 조건 중 무엇을 바꿀지는 사용자 결정으로 남았습니다(9장).

### 6.2 밸런스 (2026-10-27, M2 — 목표·보스 체력)

- **보스 `hpRate`:** 0.128 · 0.502 · 2.984 · 1.783 · 16.215 → **0.1809 · 0.5278 · 1.52 · 1.5 · 14.84** (체력 72 · 256 · 1,054 · 1,040+분신 · 15,434). 캠페인에서 그 보스에 처음 도착한 스킬 상태로 **실제 보스 판**을 체력 배수별로 돌려(시드마다 8~12회) 첫 시도 처치율 90·85·60·55·45%가 되는 값. `ProbeBoss`(죽지 않는 보스)는 분신 ×3.27이 지금 코드와 안 맞아(실제 ≈ ×2.1) 최종 판단엔 안 씀.
- 옛 맞춤(6.1)이 30·50에서 벽이었던 원인 = **바 폭 0 측정**(e5f377e 전: 바가 필드 가운데 폭 0, 리플렉션으로 재현 시 보스 피해 30에서 ×2.5, 옛 체력 30 처치율 0% → 90%).
- 보스 피해 분포가 좁아(CV 5~11%) 처치율 곡선이 가파름 → 도착 상태가 조금만 달라도 캠페인마다 첫 시도 처치율이 크게 흔들림. 판당 처치율로 볼 것.
- **일반 목표 N:** (10-28 삭제) 표의 `goal` 칸에 맞춘 값이 남아 있습니다.
- **결과 (r8, 12시드):** 50보스 처치까지 평균 **88.3분**(85~91). 90분에 −1.7분 — 더 늘리려면 ★ 통과율 55% → 35%.
- **⚠️ 이 수치는 지금 규칙과 다릅니다.** 10-28에 일반 스테이지 목표를 지웠고(목표 실패로 같은 스테이지를 다시 하던 시간이 없어짐), 해금 조건도 이전 노드 하나로 바뀌어(같은 스킬 앞 레벨 없이 뒤 레벨을 살 수 있음) 구매 순서·속도가 달라졌습니다. 50보스 시간은 다시 재야 합니다.

**측정 스크립트**(아직 저장소에 없음, 임시 폴더): `schedule.py`(목표 일정) · `theory.py` · `iterate.py`(가격 고정점) · `boss_fit.py`(보스 체력) · `rescale_reward.py`(보상 배율) · `metrics.py`(지표) · `apply_prices.py`·`apply_stage_boss.py`(엑셀 쓰기).

---

## 7. 팝업·설정

| 프리팹 | 용도 | 비고 |
|---|---|---|
| `OptionPopup` | Main·Start 설정 | Main은 씬에 미리 둠(`Stage/OptionPopup`, 우상단 `OptionButton`). 음향(BGM·SFX)·언어·진동·알림 4종, 창 아래 X. **10-30 게임 종료·데이터 초기화·도움말 버튼 제거**. 남은 항목은 전부 **연결 안 됨**(값 저장도 없음) |
| `Main/GGoma`·`Char`·`Wepon`·`Amor`·`Pet` | 편성·꼬마 고르기·장비 | 3.1. `Char` 아래 버튼 줄(`Panel`: Button 두 개 + 닫기)은 **꺼 둠** — 합성·일괄 합성은 KHD가 코드로 만드는 정보 칸 `CharacterDetail`로 대신함(10-30). 소환 버튼은 없음 |
| `Main/DungeonUI`·`GachaUI` | 던전 목록·뽑기 | `Banner`(고정) + `ListPanel` + `Viewport/Content`(스크롤). 던전 1 입장하기 → InGame(하루 2회), 2~5는 잠김 |
| `GachaResultPopup` (코드) | 뽑기 결과 연출 | `PopupLayer.Popup` 층 |
| `InGameOptionPopup` | InGame 옵션 | `OptionPopup`의 변형(원본을 고치면 같이 바뀜). BGM/SFX, **로비로 돌아가기 → Main**(`LobbyRow`, `SceneLoader`). 열면 일시정지 |
| `ConfirmPopup` | 예/아니오 확인·알림 | `Show(title, message, onYes)` / `ShowNotice(title, message)`(확인 하나). 종료 확인(`MainBackHandler`)·개발중 알림(`ComingSoonButton`)이 씀 |
| `ResultPopup` | InGame(던전 1) 결과 | 10-30 간소화: **번 골드만**, 버튼 "한 번 더하기"(같은 씬 다시) · "나가기"(→ Main). 스테이지·점수·도감·신종·전투 통계·파밍·닫기는 제거 |
| `Toast` | 안내 문구 | 공용 |

알림·확인 팝업은 `PopupSlot`으로 들고 있다가 **처음 열 때 Stage 아래에 만듭니다**. Main의 팝업·패널은 씬/프리팹에 미리 있습니다. 정렬 순서: Main 캔버스 10000, 편성 안 팝업 +10, `UiPopup`·뽑기 결과 +100, 토스트 +200 (`PopupLayer`), 방치 정산 30000, 절전 31000, 로딩 팝업 32000.

**연결 안 된 UI (10-30):** 옵션 팝업 남은 항목(위 표), Main 던전 2~5 입장(잠김), 세계수(개발중 알림). **도감 팝업(`CollectPopup`)은 여는 버튼이 없음**(프리팹만 남김). `ConfirmPopup`·`CollectPopup` 프리팹은 다시 쓸 예정이라 남김.

**데이터 초기화:** 설정 팝업 버튼은 10-30 제거. 개발 중에는 메뉴 `MoreMush/방치형 진행 초기화` 또는 `PlayerPrefs.DeleteAll()` 뒤 `Wallet.Reload()`·`IdleProgress.ReloadLevels()`(`DataResetButton.cs`가 그 순서).

---

## 8. 개발용

**메뉴:** `MoreMush/Data/Reimport Tables` · `MoreMush/Debug/Go To Next Boss Stage` · `MoreMush/Debug/Reset Stage` · `MoreMush/Debug/Run Benchmark` · `MoreMush/Debug/Open Benchmark Folder`
**PlayerPrefs 키:** `Skill.Level.<index>`, `Stage.Current`, `Stage.FarmLeft`, `Stage.LastNormalScore`, `Stage.LastNormalGold`, `Stage.Cleared`, `Wallet.Gold`, `Wallet.Mycelite`, `Wallet.TestGrant`, `Wallet.TestReclaimed`, `InGame.SpeedIndex`, `Option.BgmVolume`, `Option.SfxVolume`, `Dungeon.<n>.Day`·`Dungeon.<n>.Used`(던전 입장 횟수, 한국 날짜)
방치형: `Idle.Stage`, `Idle.Lv.<stat>`, `Idle.GoldPerSec`, `Idle.LastSeen`, `Formation.<칸>`·`Formation.Level.<id>`·`Formation.Copies.<id>`·`Formation.MigratedLevel`(예전 `Formation.Owned.<id>`는 한 번 옮기고 지움), `Gacha.*`(무료 뽑기 날짜는 예전 키 `Gacha.<종류>.AdDay` 그대로), 장비 `Weapon.*`·`Armor.*`·`Pet.*`(`.Level.<i>`·`.Copies.<i>`·`.Equipped`). 메뉴 `MoreMush/방치형 진행 초기화`.

**밸런스 측정기 (`BalanceBot`, InGame 씬 플레이 모드에서 MCP `execute_code`로)**
- `RunCampaigns(seeds, skill, incomeMul, probeBoss, schedulePath, outPrefix, maxRounds, bossDamageMul)` → `Temp/<prefix><seed>.csv`(판마다 한 줄) + `_buys.csv`. 문서 사람 모델은 `(…, 1.0f, 0.6f, false, null, "Temp/bb_s", 250, 0.234f)`.
- `ProbeBoss(stage, "index:level;…", refScore, refGold, tries, skill, seed, outPath)` → 보스 한 판 피해를 여러 번.
- 저장값은 건드리지 않습니다(`SkillProgress.SimLevels`, `HarvestGame.Simulating`).
- 진행 규칙은 게임과 같음: 일반 판은 늘 다음, 보스에 지면 바로 재도전(`Config.farmRounds` 0 = "재도전" 선택), 균사석은 `StageProgress.FirstClearGem`, 스킬 구매는 `SkillProgress.CanLevelUp`(해금 조건 = 이전 노드 하나). 오래 걸리면 MCP 호출은 시간 초과로 실패로 뜨지만 계속 돌아 CSV가 나옵니다.
- 측정기는 한 프레임 안에서(에디터가 뒤에 있으면 frameCount가 안 오름) 수천 판을 돕니다. 그래서 **프레임 끝에 처리되는 것(`Destroy`, 끝나지 못한 코루틴)을 판마다 만들면 쌓여서 점점 느려집니다**(2026-10-27 수정 전: 약 3,000판 뒤 ×70). 판마다 다시 쓰는 오브젝트는 풀/재사용, 짧은 연출은 코루틴 대신 Update로. 같은 시드는 같은 결과(판 사이에 남는 상태는 `Restart`에서 초기화).

**성능 벤치마크 (`Benchmark`, 2026-10-28)**
- 시나리오 7개(일반 1·25·49스테이지, 분신 보스 40, 이동 보스 50 / 스킬 없음·절반·최대 + 25스테이지 분열만 최대 1배속·4배속)마다 InGame 씬을 새로 열어 자동 조작으로 판을 돌리며 매 프레임 잽니다. 기본 시나리오당 30초(예열 3초 버림), 약 4분. 시나리오는 `Benchmark.Default`에서 `skill`(전체 비율)·`maxAbilities`(이 능력 키의 스킬만 최대)·`speed`(배속, `Time.timeScale`)로 정합니다. 측정 시간은 배속과 상관없이 실제 초이고, 제한 시간은 배속만큼 늘려 줍니다.
- 잰 값: FPS(평균·하위 1%·목표의 1.5배를 넘긴 끊김 비율), 프레임 시간 p95·최대, CPU 메인·렌더 스레드 ms, GPU ms, 메모리(Total Used·System Used·Mono 최대), GC 할당 KB/s, 드로우 콜·SetPass·배치, 평균 버섯·수확기 수. 기기 정보도 같이 적습니다.
- 결과: `Application.persistentDataPath/Benchmark/<시각>_summary.csv`(시나리오별)·`_timeline.csv`(1초 단위). 엑셀에서 한글이 안 깨지게 BOM을 붙입니다. 끝나면 화면에 요약이 뜨고, 누르면 SkillTree 씬으로 갑니다.
- 시작: 에디터 메뉴 `Run Benchmark`(플레이 중이 아니면 플레이 모드에 들어가 시작), 개발 빌드 옵션 팝업의 "벤치마크 (개발용)" 버튼(정식 빌드에선 숨김), 또는 실행 인자 `-benchmark`(선택 `-benchmark-seconds=60`·`-benchmark-repeat=3`). 안드로이드: `adb shell am start -n <패키지>/com.unity3d.player.UnityPlayerGameActivity -e unity "-benchmark"`, 결과는 `adb pull /sdcard/Android/data/<패키지>/files/Benchmark/`.
- 저장값은 건드리지 않습니다(`SkillProgress.SimLevels`, `HarvestGame.Simulating`). 보스는 죽지 않게(`BossHpMul`), 분신 보스는 30%로 깎아 분신 둘을 띄운 채로, 배속은 시나리오 값으로 고정합니다(배속 버튼은 무시). 바 조작은 `BalanceBot.AimX`를 같이 씁니다.
- CPU·GPU 프레임 시간은 Player Settings의 **Frame Timing Stats**(이번에 켬)가 있어야 나옵니다. 드로우 콜 같은 렌더 카운터는 정식 빌드에서 0으로 나오는 기기가 있습니다. 에디터 수치는 PC 성능이라 참고용입니다. **MCP 스크린샷(`manage_camera screenshot`)은 플레이 모드를 일시정지시키므로 벤치마크 도중엔 찍지 마세요**(그 구간 수치가 망가집니다).
- 앱 전체 목표 프레임은 `AppSettings.TargetFrameRate` = 60 (전에는 설정이 없어 모바일 기본 30fps였음).
- **Firebase Test Lab Game Loop** (2026-10-28): 안드로이드 빌드 때 `GameLoopManifest`(`IPostGenerateGradleAndroidProject`)가 런처 액티비티에 `com.google.intent.action.TEST_LOOP` intent-filter를 붙입니다(커스텀 매니페스트 없음). 이 신호로 켜지면 벤치마크가 바로 돌고, 끝나면 요약+1초 기록을 Firebase 결과 파일(intent data URI)에 쓰고 `Application.Quit()`. 시나리오 번호는 무시하고 늘 7개 전부(약 4분 → Test Lab 제한 시간 10분 권장). CSV도 받으려면 가져올 폴더에 `/sdcard/Android/data/com.soomyouth.moremustmush/files/Benchmark` 지정.
- 안드로이드 패키지 이름: `com.soomyouth.moremustmush` (2026-10-28, 전에는 URP 템플릿 기본값).

**주의할 점**
- **폰트:** 지금 쓰는 폰트는 **LINESeedKR Rg·Bd(Dynamic, 원본 TTF가 빌드에 들어감)** 뿐이고 TMP 기본 폰트도 LINESeed Rg입니다. 예전 Jua·Nunito·GowunDodum·ONEMobile·LINESeed Thin·TMP LiberationSans는 10-30 삭제(`b7e2863`). 프리팹·씬에 **`m_fontMaterial` 오버라이드가 남으면 그 폰트 전체가 빌드에 끌려 들어갑니다**(WorldTree TopBar가 Jua 4종 16MB를 끌고 있었음) — 런타임 값이라 저장되면 지우세요.
- **MCP 스크린샷:** `manage_camera screenshot`은 화면 오버레이 캔버스를 빼고 찍히고(빈 하늘만 나옴) 플레이 모드를 일시정지시킵니다. UI를 보려면 캔버스를 잠시 `ScreenSpaceCamera`로 바꿔 카메라를 `RenderTexture`에 렌더해 PNG로 저장하세요(스킬 트리 확인 때 이렇게 함). 찍은 뒤 `EditorApplication.isPaused = false`.
- **에디터 포커스:** Unity 창이 뒤에 있으면 플레이 루프가 멈추거나(frameCount가 안 오름) 가상 입력이 버려집니다. 시험은 `HarvestField.Step(dt)`, `BossGimmicks.Step(dt)`처럼 정해진 시간만 흘리는 방식으로 하세요. **`Time.deltaTime`이 쌓일 때까지 도는 while 루프는 deltaTime이 0이면 Unity가 멈춥니다**(실제로 한 번 강제 종료함).
- **저장 안 된 씬:** 에디터에서 씬을 고친 채 저장하지 않으면, 스크립트·MCP로 씬을 저장할 때 그 변경까지 같이 저장됩니다. 플레이 모드 중엔 씬 저장이 안 됩니다.
- **화면 비율:** CanvasScaler 1080×2340 Expand + `StageFrame`(3.2). InGameArea 1080×1520 고정.
- **테스트 스킬:** 스킬을 많이 배워 두면 필드가 몇 초 만에 비고 보스도 순식간에 죽습니다.
- **Awake 순서:** `HarvestGame.Awake`가 `HarvestField.Awake`보다 먼저 `Configure`를 부를 수 있습니다. 필드가 Awake에서 준비하는 값은 처음 쓸 때 준비되게 하세요(`SaveBarBase` 참고).

**2026-10-30 작업에서 알게 된 것**
- **잔액·강화 레벨은 메모리 캐시(KHD `4a783a7`):** `Wallet.Gold/Mycelite`와 `IdleProgress.Level`은 처음 한 번만 PlayerPrefs를 읽습니다. **값은 꼭 `Wallet`·`IdleProgress`를 거쳐 쓰고**, PlayerPrefs를 직접 지우거나 고쳤으면 `Wallet.Reload()`·`IdleProgress.ReloadLevels()`를 부르세요(데이터 초기화 버튼은 부름, 플레이 시작 때 자동). **10-30 최적화 작업부터 `Formation`(칸·꼬마 레벨·조각)·`Equipment`(레벨·조각·장착, `TotalPercent`)도 같은 방식** — 직접 고쳤으면 `Formation.Reload()`·`Equipment.ReloadAll()`.
- **머지 뒤 새 스크립트가 컴파일에서 빠짐:** KHD 머지 직후 Unity가 새 .cs 5개를 에셋 DB엔 등록하고 컴파일 대상에선 빼서 "Missing script"·"type not found"가 났습니다. 다시 임포트로는 안 되고, 에디터에서 **파일 이름을 바꿨다 되돌리면**(`AssetDatabase.RenameAsset`) 들어갑니다. `CompilationPipeline.GetAssemblies()`의 `sourceFiles`로 확인.
- **TMP 색을 코드로 바꾼 프리팹:** `LoadPrefabContents`에서 `TMP_Text.color`만 바꾸고 저장하면 `m_fontColor32`가 옛 값으로 남아, 씬을 저장할 때마다 글자 색 덮어쓰기가 수십 줄 생깁니다. 프리팹을 고칠 땐 `SerializedObject`로 `m_fontColor32.rgba`(= A<<24|B<<16|G<<8|R)도 맞추세요(편성 프리팹 5개는 10-30 맞춤).
- **MCP로 플레이 확인:** 에디터가 뒤에 있으면 프레임이 안 넘어가 애니메이션·코루틴이 멈춥니다. `EditorApplication.isPaused = true` 후 `EditorApplication.Step()`을 반복하면 한 프레임씩 진행됩니다(이때 콘솔의 `JobTempAlloc` 경고는 무시). 스크린샷은 `manage_camera screenshot`(include_image=false로 파일 저장 → 잘라서 보기)이 안정적이고, `ScreenCapture.CaptureScreenshotAsTexture`는 빈 화면이 나왔습니다. 꺼진 오브젝트에선 코루틴이 안 돌므로 `SetActive(true)` 먼저.
- **MCP `execute_code`에서 `AssetDatabase.DeleteAsset`은 막혀 있음** → 파일 삭제는 `git rm` 후 리프레시.
- **Codex로 그림 만들기:** `codex exec --skip-git-repo-check --sandbox workspace-write -i 참고.png - <<'EOF' … EOF` (프롬프트는 표준 입력으로 — `-i`가 뒤 인자를 이미지로 먹음). 결과는 `~/.codex/generated_images/`에도 남습니다. 자리를 맞춰야 하면 **색 상자 배치 안내 그림**을 두 번째 `-i`로 넘기면 잘 따릅니다(다만 톤이 바뀔 수 있어 10-30에는 이전 그림으로 되돌림).
- **폰트 에셋 `LINESeedKR-Bd/Rg SDF.asset`:** Dynamic 아틀라스라 플레이할 때마다 글리프가 쌓여 "수정됨"으로 뜹니다. **커밋하지 마세요**(KHD 머지 때 이 파일로 충돌).
- **Player Settings는 저장해야 파일에 남음:** 회사명·게임명 등은 바꾸는 즉시 `ProjectSettings.asset`에 쓰이지 않습니다. **File > Save Project** 후 커밋하세요(안 하면 브랜치 전환·병합 때 되돌아감, 10-30).
- **프리팹 Variant:** `InGameOptionPopup`은 `OptionPopup`의 Variant입니다. 원본에서 자식을 지우면 Variant에서 그 자식에 붙여 둔 것이 부모 밖으로 밀려납니다(10-30 "로비로 돌아가기"가 그랬음).
- **MCP 입력 시험:** 에디터 창에 포커스가 없으면 `InputSystem.QueueStateEvent`로 넣은 키가 처리되지 않습니다. 뒤로가기는 `PopupGate.HandleBack()` → `BackButton.SceneHandler`를 직접 불러 확인했습니다.

**최적화·안정화 (2026-10-30 `de3432e`, 코드 컴파일만 확인 — 에디터·실기기 확인 필요. 자세히는 `HANDOFF_OPTIMIZATION.md`)**
- **PlayerPrefs 읽기 제거:** 전투가 매 프레임(`FinalHp`·`FinalHpRegen`)·던질 때마다(`FinalAttack`) 장비 15개 × 3종 레벨을 PlayerPrefs에서 읽고 있었음(프레임당 약 70번 + 키 문자열 할당, 안드로이드에선 JNI 호출). `Equipment`·`Formation`을 메모리 캐시로 바꾸고 `Equipment.TotalPercent`는 레벨·장착이 바뀔 때만 다시 셈.
- **저장 모으기 `SaveScheduler`:** 자주 바뀌는 값(전투 골드 1초마다 `Wallet`, 강화 꾹 누르기 `IdleProgress.TryUpgrade`, 마지막 접속 시각 `IdleAway.MarkSeen`, 배속·볼륨)은 `SaveScheduler.MarkDirty()`만 하고 5초마다·앱이 뒤로 갈 때(`OnApplicationPause`/`Focus`)·종료 때 한 번에 `PlayerPrefs.Save()`. 뽑기·합성·편성·스테이지처럼 드물고 중요한 쓰기는 그대로 바로 저장(밀린 값도 같이 저장됨). **새로 자주 쓰는 값은 `MarkDirty`를 쓰세요.**
- **전투 서브 캔버스:** `IdleBattle`이 Awake에서 `IdleBattle`·`EnemyArea`·`GgomaArea`에 `Canvas`를 붙임(`IdleBattle`만 `GraphicRaycaster`). 전투가 매 프레임 움직여도 Main 캔버스의 강화 목록·하단바·재화 바는 다시 묶이지 않음. **에디터에서 확인할 것:** 숫자 팝업·타격 이펙트가 옵션 버튼·재화 바·강화 목록보다 위로 그려지지 않는지(중첩 캔버스 그리는 순서). 문제가 있으면 `AddSubCanvas` 세 줄을 지우면 예전과 같아짐. 숫자 팝업·타격 이펙트는 `canvasRenderer.SetAlpha`로 흐려짐(TMP color를 바꾸면 매 프레임 메시를 새로 만듦). 체력·전투력 글자는 값이 바뀔 때만.
- **기타 성능:** `UpgradePanel` — 잔액이 바뀔 때(1초마다)는 버튼 활성·색만(`RefreshAfford`), 글자는 강화 레벨이 바뀔 때만. `PowerSaveMode` — 씬 이름을 매 프레임 읽지 않음(문자열 할당). `BossHud` % 글자는 바뀔 때만. `Wallet.Format` 단위 배열 static.
- **안정화:** 화면 꺼짐 방지(`Screen.sleepTimeout = NeverSleep`, `AppSettings` — 안 하면 기기 화면 꺼짐(30초~2분)이 절전 모드(3분)보다 먼저 와서 방치 전투가 멈춤). 앱으로 돌아오면 절전 덮개 해제. 골드 더하기가 long을 넘으면 0이 되던 것 → 최댓값에서 멈춤. 앱 밖 보상: 초당 골드를 `InvariantCulture`로 저장·읽기(기기 언어가 바뀌면 값이 1만 배 틀어질 수 있었음), 망가진 `Idle.LastSeen` 값 범위 검사, **시계를 되돌려도 마지막 접속 시각은 뒤로 가지 않음**. **던전 입장 횟수·무료 뽑기는 저장된 날짜보다 '뒤의 날'일 때만 다시 채움**(시계를 되돌렸다 맞추는 반복 방지). 둘 다 저장값이 지금보다 하루(`IdleAway.MaxClockSkew`) 넘게 앞서면 한때 틀린 시계로 보고 버림. 표에 없는 보스 스테이지는 일반 스테이지로 취급(`StageProgress.HasBoss`, 보스 판·파밍 판에 갇히지 않게). 무한 스테이지 체력·보상 float 넘침 상한(1e30), 스킬 가격 long 넘침 상한, 표에 없는 보스면 보스 판으로 보지 않음.
- **설정:** URP `Mobile_RPAsset` — HDR·메인 라이트 그림자·라이트 쿠키·렌즈 플레어·지형 구멍·LOD 크로스페이드·혼합 조명·반사 프로브 블렌딩 끔(UI만 쓰는 게임이라 셰이더 변형·대역폭 절약), **Render Scale 0.8 → 1**(Main 캔버스가 Screen Space-Camera라 UI 전체가 80%로 그려져 흐렸음). Player — **Optimized Frame Pacing(Swappy) 켬**, 밉맵 스트리핑 켬, 릴리스 빌드 Log·Warning 스택 트레이스 끔(Error·Exception은 유지).
- **안 한 것:** 스프라이트 아틀라스(Sprite Packer가 꺼져 있음 — 켜고 아틀라스를 만들면 드로우 콜이 줄지만 에디터에서 확인하며 해야 함), `homeTabs`가 Main 씬에서 비어 있어 전투가 늘 '홈'으로 동작(탭을 열어도 숨기지 않음·방치 정산 팝업 안 뜸 — 의도인지 확인 필요), InGame에 있던 시간이 앱 밖 시간으로 세어지는 것(던전 다녀오면 방치 보상도 받음 — 의도인지 확인 필요).

**출시 준비 (Google Play, 2026-10-30 검증)**
- **대상 API 36 고정**(`AndroidTargetSdkVersion: 36`, 2026-08-31부터 새 앱·업데이트 필수, 연장 시 11-01까지). Automatic이면 SDK 36이 없는 PC에서 내려가므로 고정함. 최소 25, IL2CPP + ARM64, 16KB 페이지는 Unity 6000.3 기본 지원, 런처 액티비티 `exported="true"`, 권한·서드파티 SDK 없음.
- 화면 **세로 고정**(자동 회전 끔). API 36에선 큰 화면(sw ≥ 600dp)에서 방향 고정이 무시되지만 Stage 구조로 잘리지 않음.
- **남은 것:** 서명 키(keystore, 지금 디버그 서명), **AAB 빌드**(Build App Bundle), 실제 빌드 후 최종 매니페스트 확인(targetSdk·exported·16KB), Play Console 개인정보처리방침·데이터 보안 양식.
- **리소스 정리(`b7e2863`):** 빌드 씬 + Resources 의존성 기준으로 안 쓰는 것 삭제, 텍스처는 보이는 크기에 맞춤(그림 위주 ASTC 6×6, 꼬마 아이콘 256, 할아버지 보스 512, 로고 1024). 일부러 남긴 것: `CollectPopup`·`ConfirmPopup`, `GameBalance.bytes`·엑셀, `link.xml`, TMP 셰이더.

---

## 9. 다음 작업 후보

### 방치형 (2026-10-30 기준, 우선)
0. **실기기 확인 + 출시 준비:** 해상도(노치·20:9)·뒤로가기 키를 실기기에서 확인, 서명 키·AAB 빌드 (8장 "출시 준비")
1. **던전 다듬기:** 던전 1은 InGame으로 연결, 입장 횟수(하루 2회) 완료. 남은 것: 던전 2~5 내용·해금 조건, 보상 규칙, `DungeonRequest`로 던전 종류·스테이지 넘기기(지금은 늘 기본값)
2. **InGame ↔ Main 이동에 로딩 팝업:** `Benchmark`만 남음 (`InGameOption`·`ResultPopup`은 10-30 완료)
3. **꼬마 강화 다듬기:** 레벨당 +5%는 임시값 → 밸런스 곡선에 맞춰 정하기. 꺼 둔 `Char` 아래 버튼 줄은 지울지 결정 (`Formation` 캐시는 10-30 완료)
4. **도감 팝업을 여는 버튼** 다시 두기 (지금 어디서도 안 열림)
5. 옵션 팝업 기능 연결: 언어·진동·알림·BGM/SFX (게임 종료·도움말·데이터 초기화는 10-30 버튼 제거)
6. 세계수를 다시 만들지·Main 패널로 흡수할지 (`Backup/WorldTree/`, `HANDOFF_IDLE_PIVOT.md` ❓). 스킬 트리를 다시 쓸지(`Backup/SkillTree/`)
7. 무료 뽑기·던전 횟수의 시간 조작 대응(지금은 기기 시계의 UTC 기준), 광고 보상이 다시 필요하면 `GachaPanel` 무료 버튼 자리에
8. Main 배경 `bg_main.png`: 꼬마·적 자리가 길이 아니라 풀밭 — 꼬마 배치는 사용자가 조정함(10-30). 길 위로 그린 버전은 톤이 달라 되돌림
9. 방치형 수치를 `GameBalance.xlsx`에서 읽게 전환 (지금은 `IdleProgress` 등에 하드코딩, 6장) / 밸런스 실측(`밸런스설계/`, `BALANCE_IDLE.md`)

### 판 플레이 (InGame = 던전 1)
1. **밸런스 다시 재기:** 일반 목표 삭제·해금 조건 변경(10-28) 뒤 50보스 시간·구매 일정을 `BalanceBot`으로 다시 측정 (6.2의 88.3분은 옛 규칙 기준). 90분 목표를 무엇으로 맞출지(판 시간·스테이지 수·보스 실패) 사용자 결정 필요
2. **스킬 트리 배치를 `tier`로:** 사용자가 노드 배치를 바꾸려고 Skill.xlsx에 `tier`를 넣음(10-28). 어떻게 쓸지(티어 = 층? 한 티어 최대 10개라 3칸 한 줄엔 안 들어감)는 사용자와 정할 것. 손댈 곳은 `SkillTreeLayout.Build`의 층 정하기
3. `SkillTreeZoom`: 트리 폭 = 화면 폭이 된 뒤 확대만 됨 — 둘지 지울지 사용자 결정
4. **수확기 추가 노드 3339**가 26,000,000 골드 노드(3341) 뒤에 있음 — 해금 조건이 바뀌었으니 다시 확인
5. **거의 못 캐는 판:** 약 1% 판에서 버섯 2~8개만 캠. 수확기가 어딘가 끼는 버그로 보임
6. 첫 구매가 ×1.5 효과가 되게 (문서 요구, 지금은 점수 +10%)
7. 밸런스·표 생성 스크립트를 저장소 `Tools/`로 (지금은 임시 폴더)
8. 실기기 터치 테스트: 바 조작, Main 탭·팝업, 절전 모드 해제
9. 아트 교체: 등급·황금·버프·장치·보스, HUD 점수 아이콘(지금은 골드 그림)
10. 보스 연출: 분신·회복 연출 (이름·체력 표시는 10-28 `BossHud`로 끝)
11. 사운드 매니저 + 옵션 볼륨 적용
12. ~~WorldTree 시트 내용~~ (씬 제거, `Backup/WorldTree/`)
