# UI 개편 가이드 — 와이어프레임(2026.10.23 Ver.01) 적용

> 대상: 유니티 초보자. 기준 와이어프레임 `20261023_모바일UI와이어프레임.pdf` (박지수)
> 원칙: **기존 코드는 고치지 않는다.** 코드가 꼭 필요한 곳은 ⚠️로 표시했고, 손대기 전에 먼저 묻는다.

---

## 0. 한 장 요약 — 와이어프레임 ↔ 지금 프로젝트

| 와이어프레임 화면 | 지금 프로젝트에서 대응하는 것 | 코드 없이 가능한 범위 |
|---|---|---|
| 01 로딩 | `Start` 씬 (Logo, LoadPercentageText, LoadBar) | 로고·%·게이지·버전 글자 배치 전부. 설정 버튼만 ⚠️ |
| 02 메인 로비 | `WorldTree` 씬 (골드·균사석·바구니·설정·꾸미기 등) | 배치 전부, 업그레이드(스킬트리)로 이동, 의뢰·도감·꾸미기 팝업. **전투 이동만 ⚠️** |
| 03 업그레이드 | `SkillTree` 씬 (스킬 트리·하단 메뉴) | 배치 전부, 의뢰·공방 팝업, 창닫기(X)=세계수로 |
| 04 업그레이드 통계 | 없음 (`IndexPopup` 자리를 빌려 쓸 수 있음) | 빈 틀(레이아웃)만. 수치 표시는 ⚠️ |
| 05 수확기 공방 | `WorkshopPopup` (지금 "준비 중") | 빈 틀만. 수확기 시스템 자체가 없음 ⚠️ |
| 06 일일/주간 의뢰 | `QuestPopup` (지금 "준비 중") | 빈 틀 + 탭 전환(토글로 가능). 퀘스트 시스템은 ⚠️ |
| 07 전투 | `InGame` 씬 HUD (점수·타이머·콤보·배속·설정) | 배치 전부. 스테이지 글자 표시는 ⚠️ |
| 08 수확 완료 | `ResultPopup` (골드·점수·버섯, 다시하기·세계수로) | 배치, "다음 스테이지로"·"업그레이드 하러가기" 연결. 스테이지/균사석/도감 기록은 ⚠️ |
| 09 전투 통계 | 없음 | 빈 틀만. 통계 수집 ⚠️ |
| 10 도감 / 11 세트 보너스 | `IndexPopup` (지금 "준비 중") | 빈 틀 + 탭 전환. 버섯 데이터(Mushroom.xlsx 비어 있음) ⚠️ |
| 12 설정 | `OptionPopup`, `InGameOptionPopup` | 배치, BGM/SFX 슬라이더, 로비로 돌아가기. 나머지 항목 ⚠️ |

> **2026-10-28 틀 적용 현황** (모두 빈 틀 — 값·동작은 데이터/시스템이 생기면 코드 필요)
> - 01 로딩 `Start`: SafeArea, 로고 위·게이지 아래, ⑤ `Version`, ④ `Option`(→ 씬 안 `OptionPopup`). Canvas 기준 1080×2340.
> - 03 업그레이드: ④ `SafeArea/StatsBtn`(→ 씬 안 `UpgradeStatsPopup`), ⑥ `Quest` · ⑦ `Workshop`을 왼쪽 아래로 다시 켬(스크립트가 팝업을 엶).
> - 04 `UpgradeStatsPopup.prefab`(새): 능력치 2열×3줄 · 보유 스킬 2열 스크롤 · 하단 바 X.
> - 05 `CraftPopup`(로비)·`WorkshopPopup`(업그레이드): 제목 · 장착 슬롯 5 · 늘리기 손잡이 · 정보(이미지·레벨·이름/능력·다음 레벨·레벨업·장착) · 3열 목록. 시트는 위 HUD 아래까지.
> - 06 `QuestPopup`: 주간(줄 3개: 이름·NPC·설명·달성도·의뢰 완료) / 일일(2열 칸) · ⑬⑭ 탭 토글로 전환. 시트는 위 HUD 아래까지.
> - 08 `ResultPopup`: ① 수확 완료 · ② 스테이지(`Caption`) · ③-1 골드(스크립트가 채움) · ③-2 `Mycelite`(꺼 둠, 보스 전용) · ④~⑦ 도감 기록/신종 줄 · ⑧ `StatsButton` · ⑨ 업그레이드 하러가기(예전 세계수로 버튼, ResultPopup의 World Tree Scene = SkillTree) · ⑩ 당장 다음 스테이지로(예전 다시하기).
> - 09 `ResultPopup/BattleStatsPopup`(새, 결과 안에 둠): 열 이름 · 스킬/수확기 통계 줄 · 이전 화면으로(닫기) · 업그레이드/다음 스테이지(`SceneButton`).
> - 12 `OptionPopup`: UID · 설정 · 안내 · 음향(BGM/SFX 슬라이더 + 스위치) · 언어 설정(드롭다운 자리) | 진동 스위치 · 알림·연출 4개(글자 + 스위치). **스위치** = `Switch`(회색 OFF 판, Toggle) 안의 `OnState`(주황 ON 판)를 Toggle의 On Value Changed → SetActive로 켜고 끔 (코드 없음, 값 저장은 아직 없음) · 불러오기/저장/도움말 · 게임 종료/데이터 초기화 · 계속하기(닫기)/버전 · 옵션 적용 알림 · 하단 바 X. `InGameOptionPopup`(변형): 같은 모양, 슬라이더는 새 BGM/SFX 칸에 연결(소리 저장 동작), 게임 종료 자리 = 로비로 돌아가기.

### 화면 흐름 비교

```
지금:          Start → WorldTree ↔ SkillTree(하단 메뉴 허브) → InGame → 결과
와이어프레임:  로딩 → 메인 로비(허브) ↔ 업그레이드 / 전투 / 도감 / 의뢰 / 세계수
```

→ **허브가 SkillTree에서 WorldTree(메인 로비)로 바뀝니다.** 이게 이번 개편의 핵심입니다.
WorldTree 씬은 이미 골드·균사석·바구니(=방치형 보상)·설정·꾸미기를 갖고 있어서 "메인 로비"로 쓰기에 가장 가깝습니다.

---

## 1. 시작 전 준비

### 1-1. 백업 (꼭!)
지금 `WorldTree.unity`, `Mobile_RPAsset.asset`이 수정된 채 커밋 안 된 상태입니다. 개편 전에 한 번 커밋해 두면 잘못돼도 되돌릴 수 있습니다.
Unity에서 작업할 때마다 **Ctrl+S(씬 저장)**를 습관처럼 누르세요.

### 1-2. Unity MCP 연결
1. Unity에서 `Window > MCP for Unity` 창을 열고 서버를 켭니다 (HTTP, `localhost:8080`).
2. Claude 쪽에서 `/mcp` → `unityMCP` → Reconnect.
3. 연결되면 제가 씬 구조를 직접 읽고, 원하시면 오브젝트 만들기·배치도 대신 해 드립니다.

### 1-3. 폰트
와이어프레임은 **원 모바일 팝**(연출·강조)과 **LINE Seed Sans**(본문)를 씁니다. 프로젝트에는 `ONEMobileRegular`(팝 아님), Jua 등만 있습니다.
- 두 폰트 TTF를 `Assets/Fonts/Source/`에 넣고 → `Window > TextMeshPro > Font Asset Creator`로 SDF를 만듭니다.
- 한글은 글자 수가 많아서 **Atlas 2048×2048 이상**, Character Set은 "Custom Characters"로 게임에 쓰는 글자만 넣거나 Dynamic으로 만드세요. (HANDOFF 8장: 지금 폰트들은 Static이라 없는 글자가 □로 나옵니다)

### 1-4. 어떤 화면 크기에도 맞추는 규칙 (적용 완료)
- **Canvas Scaler:** 4개 씬 모두 `Scale With Screen Size`, 기준 1080×1920, **Screen Match Mode = Expand**. 1080×1920이 항상 화면 안에 다 들어가고, 남는 공간은 폰에서는 세로로, 태블릿에서는 가로로 생깁니다.
- **SafeArea:** Canvas 아래 `SafeArea` 오브젝트(`SafeArea.cs`)가 노치·펀치홀·홈바를 뺀 영역으로 자동으로 줄어듭니다. **배경은 SafeArea 밖(Canvas 바로 아래), 버튼·글자는 SafeArea 안**에 둡니다. 위아래 여백을 손으로 넣지 마세요. (지금은 WorldTree만 적용. Start·SkillTree·InGame도 같은 방식으로 옮겨야 함)
- **하단 버튼줄:** `BottomBar`(Horizontal Layout Group, Child Alignment = Middle Right) + 버튼마다 Layout Element(Min 120 / Preferred 400 / Flexible 0). 화면이 넓어지면 버튼이 같이 늘어나고, 2:1(400×200)에서 멈춘 뒤 묶음이 오른쪽으로 붙습니다.
- 에디터에서 확인할 때는 Game 창 왼쪽 위 `Game`을 `Simulator`로 바꾸고 기기(iPhone, Galaxy, iPad)를 고르면 노치까지 보입니다.

---

## 2. 유니티 UI 기초 (이것만 알면 됩니다)

| 개념 | 설명 |
|---|---|
| **Canvas** | UI가 그려지는 판. 이 프로젝트는 1080×1920 기준(세로), 가로 폭에 맞춰 늘어납니다 |
| **RectTransform** | UI 오브젝트의 위치·크기. 인스펙터 왼쪽 위 네모(**Anchor Presets**)로 "화면 어디에 붙을지"를 정합니다. Alt+클릭하면 위치까지 같이 옮겨집니다 |
| **Anchor** | 상단바는 "위쪽 가로로 늘림", 하단 버튼줄은 "아래쪽 가로로 늘림"으로 두면 기기 비율이 달라도 안 깨집니다 |
| **Image / TMP_Text / Button** | 그림 / 글자(TextMeshPro) / 누를 수 있는 것 |
| **Horizontal/Vertical/Grid Layout Group** | 자식들을 자동으로 줄 세우기. 하단 버튼 4개, 공방 수확기 목록, 도감 칸에 씁니다 |
| **Scroll Rect** | "슬라이드 가능" 영역. 와이어프레임의 "슬라이드 바X"는 Scroll Rect의 Scrollbar 칸을 비워 두면 됩니다 |
| **Prefab** | 재사용 부품 파일. 팝업은 전부 `Assets/Prefabs/Mobile/`의 프리팹이고, 더블클릭해서 따로 편집합니다 |
| **Toggle + ToggleGroup** | 탭 전환(일일/주간, 식용/약용/독성, 도감/세트보너스)을 **코드 없이** 만들 수 있습니다 (아래 3-6) |

### ⚠️ 절대 지우면 안 되는 것
스크립트가 인스펙터에 연결해 둔 오브젝트(예: `SkillTreeScreen`의 `Stage`, `Index` 버튼)를 **삭제하면 게임 시작 시 에러**가 납니다.
안 쓰는 버튼은 **삭제 대신 체크 해제(비활성화)**하거나 화면 밖으로 옮기세요. 비활성화된 버튼은 보이지 않을 뿐 에러는 나지 않습니다.
이름 바꾸기·위치 옮기기·크기·그림 교체·부모 바꾸기(같은 씬 안)는 자유입니다.

---

## 3. 화면별 작업 순서

작업 순서는 **흐름의 중심부터**: 메인 로비 → 업그레이드 → 전투 → 결과 → 팝업들 → 로딩 → 설정.

### 3-1. 메인 로비 (`WorldTree` 씬) — 와이어프레임 02

**들어 있는 스크립트:** Canvas의 `WorldTreeScreen`
- 골드(`goldText`), 균사석(`myceliteText`), 바구니(`basketText`, `basketButton`) → 와이어프레임 ②③④
- 설정(`optionButton`) → ⑫
- `menus` 배열: 버튼 하나 = 팝업 하나 (지금 성장·꼬마·꾸미기·제작소)
- `Back` 버튼 → `GoBack()` → `backScene`(= SkillTree)로 이동

> ✅ **2026-10-28 적용 완료.** 현재 구조:
> `BG/WorldTreeImage` ─ 피벗 (0.5, 0) = 뿌리(아래 가운데)가 기준점. Pos Y 265 = 화면 맨 아래에서 뿌리까지. 레벨마다 그림을 바꿔 높이가 커져도 뿌리는 그 자리에 있고 위로만 자란다.
>
> `SafeArea` ─ `TopBar`(Level 130×130 · Gold/Mycelite 250×75 · Spacer · OptionIcon 75×75, Horizontal Layout Group, Child Alignment = **Upper Left** → 모두 레벨 아이콘 윗선에 맞춤) · `Basket`(→ `BG/WorldTreeImage`의 자식: 나무 그림 **위 가운데**에 앵커, Pos Y -27 → 나무가 위로 자라면 따라 올라감) · `Skin`/`Quest`(왼쪽 아래) · `BossBadge`(`BottomBar/BattleBtn`의 자식 — 전투 버튼 **오른쪽 위 모서리**에 앵커, 300×110 고정, 버튼 위로 6 띄움. 지금은 항상 보임) · `BottomBar`(업그레이드 · 세계수 · 도감 · 추가콘텐츠 · 전투, 간격 8). `추가콘텐츠`는 아직 눌러도 아무 일 없음. `Kid`·`Workshop`·`Back`은 꺼 둠.
> 상단바의 `Spacer`는 Flexible Width 1인 빈칸이라 남는 폭을 다 먹고, 설정 버튼을 항상 오른쪽 끝으로 밀어 줍니다.
> 아래 내용은 처음 계획(참고용)입니다.

**할 일**
1. **상단바**: 빈 오브젝트 `TopBar`를 만들고 Anchor를 "top-stretch"로. 그 안에 ① 세계수 레벨 아이콘+TEXT, ② Gold, ③ Mycelite, ⑫ OptionIcon을 옮깁니다(드래그로 부모 바꾸기).
2. **④ 방치형 보상**: 기존 `Basket`을 화면 가운데 위쪽으로. (팝업은 "미완, 비워둘 것" → 지금처럼 토스트만 뜨는 상태 유지)
3. **⑤ 꾸미기 / ⑥ 의뢰**: 왼쪽 아래 세로로 둡니다.
   - 꾸미기 = 기존 `Skin` 버튼 그대로.
   - 의뢰 = `menus` 배열에 항목을 하나 추가 → button에 새 버튼, popup에 `QuestPopup` 프리팹. (`menus`의 Size를 1 늘리면 칸이 생깁니다)
4. **⑦~⑩ 하단 버튼줄**: `BottomBar`(bottom-stretch) + Horizontal Layout Group.
   - ⑦ 업그레이드 = 기존 `Back` 버튼 재활용 (`GoBack`이 SkillTree로 보냄) ✅
   - ⑧ 세계수 = 기존 `Grow`(세계수 키우기) 버튼, 또는 `Kid`/`Grow`를 묶은 팝업 ✅
   - ⑨ 도감 = `menus`에 추가, popup에 `IndexPopup` ✅
   - ⑩ 전투 = 새 버튼 만들기(Hierarchy 우클릭 → UI → Button - TextMeshPro) → 인스펙터 맨 아래 **Add Component → `SceneButton`** → `Scene` 칸에 `InGame` ✅
     (`SceneButton`은 버튼 하나에 씬 하나를 연결하는 공용 부품입니다. 다른 씬 이동 버튼에도 같은 방법으로 씁니다)
5. **⑪ 보스전 여부 표시**: 일단 그림+글자만 배치. 실제로 보스 스테이지일 때만 켜지게 하려면 ⚠️ (질문 2).
6. 기존 `Workshop`(제작소)·`Kid` 버튼은 와이어프레임 로비에 없으니 **비활성화**.

> 와이어프레임에서 "세계수(성장)"는 별도 씬이고 그 아래에 꼬마 버섯·세계수 키우기·꾸미기 팝업이 있습니다. 지금은 씬을 새로 만들지 말고, 세계수 버튼 → 성장 팝업으로 두는 것을 추천합니다. 내용이 생기면 그때 씬으로 분리해도 됩니다.

### 3-2. 업그레이드 (`SkillTree` 씬) — 와이어프레임 03

> **적용됨 (하단 버튼줄):** 하단 버튼줄은 프리팹 `Assets/Prefabs/Mobile/BottomBar.prefab` 하나를 로비와 같이 씁니다. 프리팹을 더블클릭해 고치면 두 씬에 함께 반영됩니다(씬에서 직접 고친 값은 그 씬에만 남는 "오버라이드"가 되니, 모양은 프리팹에서 고치세요).
> - **지금 화면 표시:** 하단 버튼줄에서 지금 화면 버튼의 아이콘(`GrowIcon`)만 160×160, Pos Y +30(버튼 위로 30 튀어나옴). 로비 = `WorldTreeBtn`, 업그레이드 = `UpgradeBtn`. 씬마다 다른 값이라 프리팹이 아니라 **각 씬에서 바꾼 값(오버라이드)** — 프리팹의 아이콘 크기를 바꿔도 이 두 아이콘은 따로 고쳐야 함.
> - 이 씬의 `Canvas/SafeArea/BottomBar`: 업그레이드 = 지금 화면이라 동작 없음, 세계수 = 로비(WorldTree 씬)로, 도감 = 도감 팝업, 추가콘텐츠 = 아직 없음, 전투 = InGame. 이 씬에서만 업그레이드·전투 버튼의 `SceneButton`을 뺐습니다(전투는 `SkillTreeScreen`이 맡음).
> - **상단바(적용됨):** 프리팹 `Assets/Prefabs/Mobile/TopBar.prefab`을 로비와 같이 씁니다(`Canvas/SafeArea/TopBar`). `SkillTreeScreen`의 goldText·myceliteText·optionButton을 새 상단바에 연결했고, 예전 `Gold`·`Mycelite`·`OptionIcon`은 꺼 두었습니다. 레벨 "Lv.1"은 아직 고정 글자.
> - **스킬 정보 창 `SkillInfo`(적용됨):** 위쪽 고정(피벗 위), 높이는 설명 길이에 맞춰 아래로 늘었다 줄었다 함. Vertical Layout Group(패딩 10/10/10/30) + Content Size Fitter(Vertical = Preferred Size). 제목 `SkillName`은 Layout Element 높이 100, 설명 `Desc`는 자동 줄바꿈 + 좌우 Margin 14. `LearnButton`은 창의 길이 계산에서 빠짐(Layout Element Ignore Layout).
> - **배우기 버튼(적용됨):** 새 스크립트 `LearnButtonFollow`(Canvas에 붙음)가 고른 노드 **바로 위**(간격 Gap 10)로 `LearnButton`을 옮김. 노드와 같은 판(`Nodes`) 안이라 스크롤·확대를 따라가고, 스킬 정보 창이 닫히면 숨음. 가격·레벨 업·누를 수 있는지는 기존 `SkillInfoPanel`이 그대로 맡음.
> - 예전 버튼 `WorldTree`·`Quest`·`Index`·`Workshop`·`Stage`는 스크립트가 참조하므로 지우지 않고 꺼 두었습니다. (스테이지 이름 표시 `StageName`도 함께 꺼짐)

**들어 있는 스크립트:** `SkillTreeScreen` — 버튼 5개(worldTree·quest·index·workshop·stage)와 팝업 3개(quest·index·workshop), 설정, 골드·균사석.
이 5개 버튼은 **반드시 남아 있어야** 합니다(코드가 시작할 때 연결함).

| 와이어프레임 | 쓸 것 |
|---|---|
| ①②③⑫ 상단바 | 기존 Gold·Mycelite·OptionIcon을 로비와 같은 위치로 (화면을 오가도 자리가 같게) |
| ④ 업그레이드 통계 | `Index` 버튼을 왼쪽 위로 옮기고 아이콘 교체. `indexPopup`에 새 "업그레이드 통계" 프리팹 연결 (3-5) |
| ⑤ 업그레이드 노드 | 기존 스킬 트리 그대로 (이미 슬라이드·핀치 됨, 스크롤바 없음 ✅) |
| ⑥ 의뢰 / ⑦ 공방 | 기존 `Quest` / `Workshop` 버튼을 왼쪽 아래로 |
| ⑧ 창닫기 X | 기존 `WorldTree` 버튼을 하단 가운데로, 아이콘을 X로 → 누르면 로비(WorldTree)로 ✅ |
| (없음) | `StageBtn`(스테이지 입장)은 와이어프레임에 없음 → **비활성화** (전투 진입은 로비에서) |

> 와이어프레임 노트: "팝업·씬 닫기는 늘 같은 위치". X 버튼 위치를 모든 화면에서 통일하세요(하단 가운데 짙은 띠).

### 3-3. 전투 (`InGame` 씬) — 와이어프레임 07

**스크립트:** `UIPanel`의 `StageHud` (점수·타이머·콤보·설정·일시정지·배속)

| 와이어프레임 | 지금 오브젝트 |
|---|---|
| ① 장착 꼬마버섯 | 없음 → 이미지 하나 추가(지금은 그림만). 꼬마버섯 장착은 "추가 기획 필요" |
| ② 획득 골드(점수) | `EarnedGold(text)` + `GoldIcon` |
| ③ 타이머 | `Timer(ProgressBar)` + `Timer(text)` |
| ※ 설정 | `Option` |
| ④ 콤보 | `Combo` (콤보 2 이상일 때만 자동으로 보임) |
| ⑤ 배수 조절 | `Speed` (`SpdIcon`, `SpdText`) → 필드 오른쪽 아래로 |
| 슬라이드 영역 | 필드 아래쪽. 바 손잡이 그림(⊙)은 `Bar` 아래 이미지 추가로 |

> **적용됨 (2026-10-28):** 기준 화면 = **갤럭시 S26 (Canvas Scaler 1080×2340, Expand)**. 세로는 어떤 기기든 2340에 맞고, 넓은 화면은 가로만 늘어남.
> - 구조: `Canvas/SafeArea/InGameArea`(Horizontal Layout Group) = `LeftSide` · **`Column`** · `RightSide`.
>   - `Column`(게임 기둥, RectMask2D) Layout Element **Min 1080**(S26 폭) / **Preferred 1462**(갤럭시 탭 S11 1600×2560 비율 폭) / Flexible 0 → 화면 폭을 따라 1080~1462 사이로 늘어나고, 그보다 넓으면(아이패드) 1462에서 멈춤.
>   - `LeftSide`/`RightSide`: Flexible 1 → 1462를 넘는 남는 폭을 나눠 가짐. 안의 `Wall`(타일 `wall_left`/`wall_right`, Tiled, 폭 512)이 기둥 가장자리에 붙어 바깥으로 그려져서, 남는 폭이 0이면 화면 밖이라 안 보이고 생기면 그만큼 보임. 수확기·버섯은 필드(=기둥) 밖으로 못 나가므로 벽은 그림만.
> - `Column` 안: HUD `UIPanel` 270(가로 stretch) → `Field` 높이 **1674** = 버섯 영역 1592(S26 기준 HUD 아래의 80%) + 10 + 바 72 (**피벗은 반드시 가운데** — 게임 코드가 필드 가운데를 원점으로 씀, 폭은 기둥을 따라감) → 그 아래 전부가 슬라이드 영역(`Field/SlideArea`, 투명 터치 판 → `FieldInput`이 받음).
> - **바 위치:** 슬라이드 영역 맨 위(버섯 영역 아래 + 10). 수확기가 바에 닿으려면 바가 판정 영역 안에 있어야 해서 `Field`는 바 아래끝까지. `HarvestField`의 **Spawn Bottom 462**(버섯은 바에서 충분히 떨어진 위쪽에만). 바 높이를 바꾸면 필드 높이·바 위치·Spawn Bottom을 같이 맞춰야 함.
>   - 벽 `Wall`은 남는 칸을 꽉 채우는 크기(남는 폭이 0이면 그릴 것도 0).
>   - `Wall/Fill` = 하늘 그림(`wall_fill_left`/`wall_fill_right`, 960×1024)을 **원본 크기 그대로**(폭 960, 세로는 Tiled로 반복) 기둥 쪽에서 40 바깥에 붙임 → 절벽과 같은 크기·같은 시작점이라 물결 모양이 맞물림. (늘리면 그림의 투명한 물결 가장자리도 같이 커져 절벽과 하늘 사이로 필드가 비쳤음)
>   - `Wall/Back` = 하늘 그림(960)보다 넓은 화면(모니터)에서 그 바깥을 하늘 바깥 끝 색(37,97,133)으로 칠하는 판 · `Wall/Seam` = 절벽 바로 옆(기둥에서 30~140) 어두운 하늘 색(29,47,50) 판 — 물결 사이 빈틈을 메움. 하늘 그림을 바꾸면 이 두 색도 새 그림에 맞춰야 함. · `Wall/Cliff` = 절벽 줄(`wall_cliff_left`/`wall_cliff_right`, 92×1024)을 기둥 쪽 가장자리에 폭 92(원본 크기)로, 세로로만 반복(Tiled, Pixels Per Unit Multiplier 1). 아무리 넓어도 가로로 반복되지 않음. 절벽을 굵게 하려면 Cliff의 Width를 늘리고 Multiplier를 같은 비율로 줄임(예: 폭 92 ↔ 1).
>   - 예전 `wall_left.png`·`wall_right.png`는 안 씀.
> - 필드 폭이 기기마다 1080~1462로 달라져도 **버섯 군락 수는 같음** — 군락 수는 스킬·스테이지가 정하고(`stats.Colonies`), 격자 칸은 놓을 후보 자리일 뿐. 넓은 화면은 같은 수가 더 넓게 퍼짐.
> - 배경 `BG`·`StageImage`: Aspect Ratio Fitter(Envelope Parent, 그림 비율 1.78) → 찌그러지지 않고 화면을 꽉 덮음(넘치는 양옆은 잘림).
> - **옵션 버튼 통일:** 세 씬(로비·업그레이드·인게임) 모두 Canvas Scaler 기준 1080×2340 + 옵션 = 배경 없는 톱니 아이콘 **80×80, SafeArea 오른쪽 34 · 위 50**. (상단바 프리팹 양옆 Padding 34, 각 씬 `TopBar` Pos Y -50 / 인게임 `Option` (-34, -50), 인게임 HUD도 위 50·왼쪽 +10) 인게임 `Option`은 `Column/UIPanel` 안, 기둥(절벽 안쪽) 오른쪽 위 (-34, -50) — 왼쪽 `KidSlot`(34, -50)과 좌우 대칭. 폰~탭 S11은 기둥 = 화면 폭이라 로비와 같은 자리, 아이패드처럼 벽이 보이는 화면에선 절벽 안쪽. 로비·업그레이드는 `TopBar`(위 40) 안. 다른 HUD(레벨·재화 / 꼬마버섯·골드)도 윗선 40에 맞춤.
> - HUD: `KidSlot`(212, 빈 틀) · `EarnedGold`(왼 278·오른쪽 242 여백으로 늘어남) · `TimerIcon` + `Timer(ProgressBar)`(왼 353·오른쪽 242로 늘어남, 남은 시간 글자는 바 안) · `Option`(오른쪽 위). `Pause`는 꺼 둠.
> - 기둥 오른쪽 위 `Combo`, 버섯 영역 오른쪽 아래 `Speed` → 기둥 오른쪽 끝에 붙어서 폭이 바뀌면 같이 움직임.
> - 바(`Field/Bar`) 그림 = `Assets/Art/InGame/bar_steel.png` (Sliced, 테두리 15/6/15/6, Pixels Per Unit Multiplier 0.5 → 높이 72). 스킬로 폭이 늘어나도 나무 끝 장식은 그대로.

- `Pause` 버튼은 와이어프레임에 없음 → **삭제 말고 비활성화**.
- **주의:** `InGameArea`(1080×1520)는 게임 판정 좌표라 크기를 바꾸지 마세요. HUD만 옮깁니다.
- "꼬마버섯 대신 스테이지 아이콘+작은 글자로 현재 스테이지 표시"를 택하면 ⚠️ (질문 2와 같은 종류).

### 3-4. 수확 완료 (`ResultPopup` 프리팹) — 와이어프레임 08

**스크립트:** `ResultPopup` — `earnedText`(골드 카운트업), `countText`(점수·버섯 한 줄), `retryButton`, `worldTreeButton`, `worldTreeScene`

| 와이어프레임 | 방법 |
|---|---|
| ① 수확 완료 이미지 | 이미지 추가 (그림만) ✅ |
| ③-1 획득 골드 | `Earned` 그대로 ✅ |
| ⑩ 당장 다음 스테이지로! | `RetryButton` 글자 교체 → 이 씬을 다시 여는데, 스테이지는 이미 다음으로 넘어가 있으므로 **그대로 "다음 스테이지"가 됩니다** ✅ |
| ⑨ 업그레이드 하러가기 | `WorldTreeButton` 글자 교체 + 인스펙터 `World Tree Scene` 값을 `SkillTree`로 ✅ |
| ② 스테이지 레벨, ③-2 균사석, ④~⑦ 도감 기록·신종, ⑧ 전투 통계 | 칸(틀)만 만들어 두기. 값 채우기는 ⚠️ |

### 3-5. 새 팝업 만드는 법 (업그레이드 통계·전투 통계 공통)

기존 팝업을 복제하는 게 가장 안전합니다.
1. Project 창에서 `OptionPopup.prefab` 선택 → **Ctrl+D**(복제) → 이름 `UpgradeStatsPopup`.
2. 더블클릭해서 프리팹 편집 → `Window` 안의 `Placeholder`·`ResetButton` 같은 내용물을 지우고 와이어프레임대로 채웁니다.
   - 루트의 `UiPopup` 컴포넌트는 남겨 둡니다(열기·닫기 애니메이션 담당). Style: 가운데 창이면 `Center`, 전체 화면이면 `FullScreen`, 아래에서 올라오면 `BottomSheet`.
   - `DataResetButton` 컴포넌트가 있다면 지웁니다.
   - 닫기 X(`CloseBtn`)는 `UiPopup`의 `Close Button` 칸에 연결돼 있으면 코드 없이 닫힙니다.
3. 목록(보유 스킬, 수확기 목록 등)은 `Scroll Rect` → `Viewport` → `Content`(+ Vertical/Grid Layout Group + Content Size Fitter) 구조로 만들고, 칸 하나를 예시로 두세요. 실제 데이터로 칸을 채우는 건 ⚠️.

### 3-6. 의뢰 / 도감 / 공방 팝업 — 와이어프레임 05·06·10·11

이 세 팝업은 지금 "준비 중" 틀만 있습니다(`QuestPopup`, `IndexPopup`, `WorkshopPopup`). 와이어프레임대로 **겉모습**을 먼저 만듭니다.

**탭 전환을 코드 없이 만드는 법** (일일↔주간, 도감↔세트 보너스, 식용/약용/독성)
1. 탭 버튼 대신 **Toggle** 2~3개를 만들고, 부모에 **Toggle Group** 컴포넌트를 붙인 뒤 각 Toggle의 `Group` 칸에 그 부모를 넣습니다.
2. 탭마다 내용 패널(예: `DailyPanel`, `WeeklyPanel`)을 만듭니다.
3. Toggle 인스펙터 아래 `On Value Changed (Boolean)`에서 + → 그 패널을 끌어다 놓고 → `GameObject > SetActive` 중 **위쪽 "Dynamic bool"** 항목을 고릅니다.
4. 첫 탭(일일 의뢰)의 Toggle만 `Is On` 체크.
→ 탭을 누르면 해당 패널만 켜집니다.

**와이어프레임에서 "씬"으로 표시된 의뢰·도감**은 지금 팝업입니다. 팝업 Style을 `FullScreen`으로 바꾸면 화면 전체를 덮어 씬처럼 보입니다. 씬을 새로 만들 필요는 없습니다.

**공방**의 "상단 늘리기", "꾹 누르기"는 ⚠️ (기능 단계에서).

### 3-7. 로딩 (`Start` 씬) — 와이어프레임 01
- `Logo`, `LoadPercentageText`(nn%), `LoadBar`는 있음 → 위치만 맞추기 ✅
- ⑤ 버전 표기: TMP 텍스트에 직접 `Ver 0.1.0` 입력 ✅ (빌드 버전과 자동으로 맞추려면 ⚠️)
- ④ 설정 버튼: 이 씬엔 팝업을 여는 스크립트가 없음 ⚠️. 로딩이 2초라 누를 틈도 거의 없어서 **생략 추천**.

### 3-6-1. 도감 = 화면 전체 팝업 `CollectPopup` (적용됨) — 와이어프레임 10·11

- 프리팹 `Assets/Prefabs/Mobile/CollectPopup.prefab` (예전 `IndexPopup`의 이름을 바꾸고 내용을 새로 채움 → 씬 연결이 그대로 이어짐). UiPopup Style = **FullScreen**. X = 팝업 닫기 → **연 화면으로 돌아감**.
- 하단 버튼줄의 도감 버튼 이름 = `CollectBtn`. 로비: World Tree Screen → Menus 마지막 칸(CollectBtn → CollectPopup). 업그레이드: Skill Tree Screen → Index Button = CollectBtn, Index Popup = CollectPopup.
- 잠깐 만들었던 `Index` 씬은 빌드 설정에서 뺐음 (파일 `Assets/Scenes/Index.unity`는 남아 있음 — 필요 없으면 지워도 됨).
- 구조 (`CollectPopup` → `Panel` → `BG`·`BarUnder`·`SafeArea`, 그리고 `OptionPopup`(설정 팝업을 안에 함께 넣음)):
  - `Header`(265): ① `Title`(현판, 글자 `CodexTitle`/`SetTitle`) · ※ `Option`(80, 오른쪽 34·위 50 → On Click = 안의 `OptionPopup`의 UiPopup.Open)
  - `CodexPage`: ②③④ `KindTabs`(식용/약용/독성, Toggle Group) · ⑤ `Filters/GoldToggle` · ⑥ `Filters/SortBtn` · ⑦~⑩ `Grid`(스크롤, 4열 Grid Layout) 안의 `Card_1~16`(Name·Icon·Stars·GoldBadge(꺼짐))
  - `SetPage`(처음엔 꺼짐): `Set_1~4` = ② `SetName` · ③ `Mushrooms`(아이콘+이름 4개) · ④ `Effect`
  - ⑪⑫ `PageTabs`(도감 / 세트 보너스 Toggle) → On Value Changed로 페이지·제목을 켜고 끔 (코드 없음)
  - `CloseBar`(160, 짙은 회색) + 가운데 X = 팝업 닫기. `BarUnder` = 홈바 쪽까지 같은 색.
- ⚠️ 지금은 **빈 틀**: 카드·세트는 "???" 자리 표시, 식용/약용/독성·황금·정렬은 눌러도 내용이 안 바뀜, 카드 터치 상세 팝업 없음. 버섯 데이터(Mushroom.xlsx)·획득 기록이 생기면 채우는 코드 필요 (4장 표 5번).

### 팝업 공통 — 하단 닫기 바 (적용됨)

- 대상 프리팹 7개: `QuestPopup`·`IndexPopup`·`WorkshopPopup`(SkillTree 전용, 지금 공방 버튼이 꺼져 있어 안 열림)·`WorldTree/CraftPopup`(로비의 공방)·`GrowPopup`·`KidPopup`·`SkinPopup`.
- 시트(`Sheet`, 도감은 `Panel`) 맨 아래에 `CloseBar`(높이 160, 짙은 회색) → 그 가운데 `CloseBtn`(X, 110). 시트 높이를 160 늘리고 `Content` 아래 끝을 160 올려 내용 칸 크기는 그대로.
- ⚠️ **어느 프리팹이 어느 화면에서 열리는지:** 로비의 공방 = `WorldTree/CraftPopup.prefab`, 업그레이드 화면의 공방 = `WorkshopPopup.prefab`. 씬마다 연결된 프리팹은 로비 `Canvas`의 World Tree Screen → Menus / 업그레이드 `Canvas`의 Skill Tree Screen에서 확인.

### 3-8. 설정 (`OptionPopup`, `InGameOptionPopup`) — 와이어프레임 12
- ② BGM/SFX 슬라이더: `InGameOptionPopup`에 이미 있음(`InGameOption` 컴포넌트). 같은 컴포넌트를 `OptionPopup`에도 붙이면 로비에서도 슬라이더가 저장됩니다 ✅ (소리 자체는 아직 연결 안 됨)
- ⑫ 인게임의 "로비로 돌아가기" = `InGameOption`의 세계수로 버튼 ✅
- ⑬ 계속하기 = `CloseBtn` ✅
- ⑭ 버전, ⑯ UID 글자 칸 ✅(고정 글자)
- 음향 ON/OFF·언어·진동·알림·연출 간소화·불러오기·저장·게임 종료 → 틀만, 기능은 ⚠️. 도움말은 "비워두기".
- 데이터 초기화 버튼(`ResetButton`)은 와이어프레임에 없지만 개발 중엔 남겨 두는 게 편합니다.

---

## 4. 코드가 필요한 곳 (먼저 결정 필요)

| # | 내용 | 규모 | 제안 |
|---|---|---|---|
| 1 | 로비 → **전투(InGame)** 이동 버튼 | 아주 작음 | ✅ 완료 — `Assets/Scripts/Mobile/SceneButton.cs` 추가 (기존 코드 수정 없음) |
| 2 | 보스전 표시(⑪), 전투 HUD 스테이지 글자, 결과의 스테이지 레벨·균사석 | 작음 | 새 표시용 스크립트, 또는 기존 스크립트에 글자 칸 추가 |
| 2-1 | **보스 전투 On/Off 토글** (⑪, 2026-10-28 사양 확정 · 구현 보류) | 중간 | 아래 사양 참고. `StageProgress.cs` 규칙 변경 + `SkillTreeScreen.cs` 1~2줄 + 새 `BossToggle.cs`. `HarvestGame`은 수정 없음 |
| 2-4 | **보스 배지 글자** ✅ | 작음 | 새 `BossBadgeLabel.cs`를 하단 버튼줄 프리팹의 `BattleBtn/BossBadge/Text`에 붙임. 화면이 열릴 때 보스 판이면 "보스 등장", 아니면 "전투중..." (표시만, 토글은 2-1에서 보류). 문구는 Inspector의 Boss Text / Normal Text |
| 2-3 | **가로 모니터(가로 > 세로) 인게임** (보류) | 작음 | 새 스크립트만 추가: 가로 화면이면 `Column`이 화면 폭 전체로 늘어나고 벽(`LeftSide`/`RightSide`)을 끔, 세로면 지금 규칙(1080~1462). 지금은 가로 모니터에서 양옆에 큰 벽 한 장씩 보임. 가로에선 필드가 매우 넓어져 버섯이 듬성듬성 → 바 폭·버섯 수 따로 조절 검토 |
| 2-2 | **세계수 레벨별 그림 교체** (구현 보류 — 그림 준비 후) | 작음 | 새 `WorldTreeGrowth.cs`만 추가: 레벨별 스프라이트 목록 → 그림 교체 + 크기 = 픽셀 × 약 0.9 + 상단바 "Lv." 갱신. 씬 준비는 끝남(뿌리 피벗, 바구니는 꼭대기 앵커). 그림 규칙: 폭 1672 고정, 높이만 늘림, 뿌리는 맨 아래 가운데. 레벨이 오르는 규칙은 미정 |

**⑪ 보스 전투 토글 사양 (확정)**
- 보스 스테이지에 도착하면 자동으로 **On** → 전투 버튼을 누르면 보스전.
- 보스전에 **지면 Off**로 바뀌고, 보스 **이전 스테이지**에 머문다(그 스테이지를 반복하며 파밍).
- 유저가 준비되면 토글을 눌러 **On** → 보스 스테이지로 진입.
- 자동 On 상태에서도 눌러서 **Off**로 돌릴 수 있다(이전 스테이지로).
- 보스를 **클리어하면 토글이 사라지고** 다음 스테이지로 진행.
- 지금 코드 규칙(지면 보스 스테이지에서 일반 판 2번 → 자동으로 다시 보스)은 이 사양으로 교체한다.
| 3 | 세계수 레벨(계정 레벨) | 기획 필요 | 무엇으로 레벨이 오르는지 정의 필요 |
| 4 | 업그레이드 통계 / 전투 통계 | 중간 | 통계 수집 + 목록 채우기 |
| 5 | 도감·세트 보너스, 결과의 도감 기록·신종 | 큼 | Mushroom.xlsx 데이터 + 획득 기록 저장 |
| 6 | 수확기 공방 (장착 슬롯, 레벨업) | 큼 | Harvester.xlsx 데이터 + 장착 시스템 |
| 7 | 일일/주간 의뢰 | 큼 | 퀘스트 표 + 진행도 + 리셋 시간 |
| 8 | 설정 세부 항목 (언어·진동·알림·저장·종료 등) | 항목별 | 사운드 매니저부터 |

**추천 순서:** 1 → (UI 배치 전부) → 2 → 나머지는 기획 확정 후.

---

## 5. 작업 체크리스트

- [ ] 현재 상태 커밋 (백업)
- [ ] 폰트 2종 SDF 생성
- [ ] 메인 로비 배치 (WorldTree)
- [ ] 업그레이드 배치 (SkillTree), 스테이지 버튼 비활성화
- [ ] 전투 HUD 배치 (InGame)
- [ ] 결과 팝업 배치 + 버튼 2개 재연결
- [ ] 업그레이드 통계·전투 통계 팝업 틀
- [ ] 의뢰·도감·공방 팝업 틀 + 탭 전환
- [ ] 로딩·설정 배치
- [ ] 1080×1920 / 1080×2400(긴 폰) 두 해상도에서 Game 뷰 확인
