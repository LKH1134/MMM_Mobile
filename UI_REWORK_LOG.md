# UI 개편 변경 기록 (PJS 브랜치, 2026-11-04~)

다른 브랜치와 합칠 때 보는 문서입니다. **기존 파일을 어디서 어떻게 바꿨는지**와 **충돌이 날 만한 곳**을 적습니다.
새로 추가한 파일은 충돌하지 않으므로 목록만 둡니다. 코드 안의 바꾼 곳에는 `[UI 개편]` 또는 "색은 그림으로" 주석을 남겼습니다.

---

## 0. 규칙 (이번 개편에서 정한 것)

| 규칙 | 내용 |
|---|---|
| 색 | 코드·씬에서 그림에 색을 입히지 않는다. 색이 필요하면 같은 색 리소스로 바꿔 낀다 (`UI_Btn_Green/Red/Sky/Yellow/Beige/Grey`, `UI_PP_In_Color_*`) |
| 회색 | 누를 수 없음·잠김·진행 중 = `UI_Btn_Grey`, `UI_PP_In_Color_Grey` |
| 층 순서 | 화면 내용(Main 캔버스 10000) < 광고 상자 10005 < **하단 공용 바 10020** < 편성 안 팝업(무기·방어구·펫) 10030 < 팝업 10100 < 안내 10200 < 방치 정산 30000 |
| 하단 공용 바 | 화면 내용보다 늘 앞 (`BottomBarLayer`). 아래 X가 있는 팝업형 화면은 하단 바를 `UI_PP_Under_Side03` 띠로 덮고 그 가운데 X (`PopupBottomBand`) |
| 화면 끝 | 하단에 붙는 UI(하단 바, 강화 노트, 편성 화면, 팝업 하단 띠)는 `SafeAreaDock`으로 화면 좌우 끝·안전 영역 아래 끝에 붙인다. 홈바 자리까지 바탕을 늘린다(bleed) |
| 제목 | 갈색 판(표지/틀)과 베이지 판(속지) 사이에 끼운 메모(`UI_PP_Out_Small`) — 위로 튀어나오고 아래는 속지에 가린다 |

## 0-1. 공통 규격 (`Scripts/Mobile/UI/Main/UiSkin.cs`에 상수로 둠, 폭 1080 기준)

| 항목 | 값 |
|---|---|
| 창 | 갈색 틀 `UI_PP_Out_Wood` + 안쪽 속지 `UI_PP_Out_Paper2` (좌우·아래 22, 위 70) + 제목 메모 `UI_PP_Out_Small` 440×140 (틀 위로 튀어나옴) |
| 하단에 붙는 판 | 표지 `UI_PP_Under_Side02` + 속지 `Side01` (위 30) + 같은 메모 제목 |
| 정보 구역 | `UI_PP_In_InnerLine2` (모서리 장식) |
| 목록 구역 | `UI_PP_Out_Paper1` / 칸 `UI_PP_In_Color_Beige1`, 고른 칸 `UI_PP_In_InnerLine1`, 장착 칸 `UI_PP_In_Color_Green` |
| 구역 제목 칩 | `UI_PP_In_Color_Brown` + 흰 글자 34 |
| 레벨 띠 | `UI_Btn_Beige` 작은 띠, 진한 글자 |
| 버튼 | 주 버튼 `UI_Btn_Green` 높이 120~132 흰 글자(외곽선) 44 / 보조 `UI_Btn_Beige` 높이 96~100 진한 글자 38 / 누를 수 없음 = `UI_Btn_Grey` (SpriteSwap, `UiSkin.GreyWhenDisabled`) |
| 글자 | 제목 56 · 구역 36 · 본문 32 · 작은 글자 26, 모두 Bold, 넘치면 자동 축소 |
| 여백 | 화면 좌우 30 · 칸 사이 16 |

---

## 1. 기존 코드 수정 — 충돌 위험 순

| 파일 | 바꾼 곳 | 충돌 위험 |
|---|---|---|
| `Scripts/Mobile/Idle/IdleBattle.cs` | `class` → `partial class`, 그림 경로 상수 2줄, **`Awake`의 `BuildHud();` 호출을 주석으로 바꾸고 `BuildHud()` 메서드(약 90줄)를 지움**. HUD 필드·`RefreshHud`·`RefreshBossBars`·`RefreshPower`·`RefreshHp`는 남겼다(필드가 비어 있으면 바로 돌아간다) | **높음.** 다른 브랜치가 `BuildHud` 안(배너·전투력·퀘스트·도감 버튼 등)을 고쳤으면 수정/삭제 충돌 → 그 변경은 프리팹 `MainHud`(+`MainHudView`)로 옮겨 적용. 새 HUD 상태는 `IdleBattle.Hud.cs`에서 읽는다 |
| `Scripts/Mobile/UI/StageFrame.cs` | `Apply`: 폭 상한(1316)·높이 고정(2340)을 없애고 안전 영역 끝까지 늘인다 (`Mathf.Max(MinWidth, …)`, `Mathf.Max(DesignHeight, …)`, `sizeDelta` 높이). 상수 `MaxWidth`는 남겨 둠 | **중간.** 아이패드·Z플립에서 화면이 비던 근본 원인. 고정 크기 UI는 가운데, 앵커 UI는 끝에 붙는다. Stage 크기를 2340 고정으로 가정한 코드가 다른 브랜치에 생기면 확인 |
| `Scripts/Mobile/UI/PopupLayer.cs` | 층 상수 `Panel` 10→30, `AdBox`(5)·`BottomBar`(20) 추가 + 주석 | 중간. 다른 브랜치가 층 값을 바꾸면 충돌 |
| `Scripts/Mobile/Ads/AdBox.cs` | `SortingOrder` 10050 → `10000 + PopupLayer.AdBox` (팝업·하단 바 아래로) · 상자 그림 경로 | 낮음 (상수 줄) |
| `Scripts/Mobile/Ads/AdBox.cs` (7차) | **`BindCamera()` 추가**: `Create`·`OnSceneLoaded`에서 씬의 Screen Space - Camera 캔버스와 같은 카메라에 붙인다 (없으면 오버레이). Main 캔버스가 카메라 모드라 오버레이였던 상자가 정렬 순서와 상관없이 팝업 위에 그려지던 문제 | 중간. 다른 브랜치가 `Create`·`OnSceneLoaded`를 고쳤으면 `BindCamera()` 호출 두 줄을 다시 넣는다 |
| `Scripts/Mobile/WorldTree/UI/TreeQuestPanel.cs` (7차) | `Card`에 `questIcon`(`Desc/QuestIcon`)·`barFill`(`Bar/Fill`) 추가 + `QuestIconPath(kind)`, 일일 칸도 `CompleteBtn`이 있으면 그 버튼으로 받기(없으면 예전처럼 Desc 눌러 받기), 달성도 글자에 경험치를 주간·일일 모두 붙임 | 중간. 의뢰 칸을 찾는 `Cards()`·`Fill()`을 고친 브랜치와 충돌 |
| `Scripts/Mobile/Quest/DailyQuestPopup.cs` | 그림 경로 상수 블록, `Refresh`의 버튼 그림/글자색 2줄, `BuildUi` 제목 색·닫기 버튼 숨김 + `PopupBottomBand.Attach`, `BuildRow` 줄 바탕·막대 그림, `NewButton` 인자 `Color tint` → `string sprite` | **높음.** 퀘스트 창 레이아웃을 다른 브랜치가 고치면 `BuildUi`·`BuildRow`·`NewButton`에서 충돌 |
| `Scripts/Mobile/Ads/AdBoxPopup.cs` | 그림 경로 상수, 제목 색 → 흰색, 취소/광고 버튼 색 → `UI_Btn_Red`/`UI_Btn_Sky`, `NewButton(… Color tint …)` → `(… string sprite …)` | 중간 (`NewButton` 시그니처) |
| `Scripts/Mobile/Idle/IdleReportPopup.cs` | `NewButton(… Color tint …)` → `(… string spritePath …)`, 확인 `UI_Btn_Green`·2배 `UI_Btn_Sky` | 중간 |
| `Scripts/Mobile/Idle/EquipmentPanel.cs` | 색 상수 3개 → 그림 경로 상수, `BuildMergeBar` 막대 그림, `Refresh` 칸 바탕 그림(고른/장착/보통), 못 얻은 장비 회색 칠 제거 | 중간 (`Refresh` 안 칸 그리기) |
| `Scripts/Mobile/Idle/CharacterDetail.cs` | 색 상수 → 그림 경로 상수, 막대 그림, 스킬 칸 그림(해금 초록/잠금 회색), 못 얻은 꼬마 회색 칠 제거 | 중간 |
| `Scripts/Mobile/UI/GachaCardView.cs` | 재화 버튼 `UI_Btn_Sky`, `CoinColor` 흰색, 막대 색 흰색 | 낮음 |
| `Scripts/Mobile/UI/GachaResultPopup.cs` | 등급 테두리: 등급색 칠 → 등급별 그림 `GradeSprites` | 낮음 |
| `Scripts/Mobile/WorldTree/UI/TreeUi.cs` | 그림 경로 상수 블록, `Card`·`Button`·`Bar`가 색 대신 그림을 고른다 (`ButtonOn/Alt/Off`·`CardOn/Off` 색 값은 어떤 그림을 쓸지 고르는 데만 씀) | 중간 (세계수 UI 공용 도우미) |
| `EquipmentPanel.cs`·`GachaCardView.cs`·`DailyQuestPopup.cs`·`AdBoxPopup.cs`·`IdleReportPopup.cs`·`TreeUi.cs` (추가) | 버튼을 만든 줄 뒤에 `UiSkin.GreyWhenDisabled(…)` 한 줄씩, `EquipmentPanel.SelectedSprite` → `InnerLine1` | 낮음 |
| `UI/GachaCardView.cs` (추가) | `Style`: 제목 40·자동 축소, 아래 줄 아이콘 56·가격 38 | 낮음 |
| `Idle/EquipmentPanel.cs` (추가) | `FitDetailText`: 이름 폭을 정보 칸 안으로 제한 | 낮음 |
| `Prefabs/Mobile/QuestPopup.prefab` (7차) | **주간 `Quest_1~3`·일일 `Daily_1~6` 칸 안을 새로 짬** (Name·PortraitBox/IconBox·Portrait/Icon·NpcName·Desc/QuestIcon·Desc/Text·Bar/Fill·Progress·CompleteBtn/Label), 탭 `Icon` 삭제 | **중간.** 의뢰 칸 프리팹을 고친 브랜치와 충돌 → 새 칸 이름 기준으로 다시 적용 |
| `Prefabs/Mobile/Main/DungeonUI·GachaUI.prefab` (7차) | 배너 `TitleMemo`·`TitlePlaque` 삭제 → `ListPanel/Tab` 추가, `Page`·`Viewport` 위 여백, 던전 입장 버튼 안 `LevelText` | 중간 |
| `Prefabs/Mobile/Main/Wepon·Amor·Pet.prefab` (추가) | **구조 변경**: Dim/Frame(Tab·Paper)/Up/mid/Down/BottomBand, 펫 Up을 무기 Up 복제로 교체 | **중간.** 장비 창 프리팹을 고친 브랜치와 충돌 → 새 구조 기준으로 다시 적용 |
| `Prefabs/Mobile/TopBar.prefab` | 그림·글자·`TopBarView` 추가 (Main·세계수 공용) | 중간 |
| `Scenes/Main.unity` (추가) | `WalletBar`·`OptionButton` 삭제, `TopBar` 인스턴스 추가(설정 버튼 → `OptionPopup.Toggle`) | 높음 (같은 씬) |
| `InGame/ResultPopup.cs` | `BuildAutoRow`: 체크 상자·체크 색 → 그림 (2줄 추가) | 낮음 |
| `WorldTree/UI/TreeUi.cs` (추가) | `Text`: 글자 1.15배·Bold·자동 축소, `ScrollList` 간격 | 낮음 |
| `EquipmentPanel.cs` (추가) | `NewText`: 글자 1.25배·Bold·자동 축소 | 낮음 |
| `Prefabs/Mobile/Main/DungeonUI·GachaUI.prefab`, `Gacha_Panel/Gacha_Panel1.prefab`, `Prefabs/Mobile/WorldTree/*Popup.prefab`, `Prefabs/Mobile/QuestPopup.prefab` | 그림·제목 메모·하단 띠 (위 진행 기록) | 중간 (세계수 시트 구조: Tab 순서·Page·BottomBand 추가) |
| `GachaPanel.cs`, `FormationPanel.cs`, `QuestButton.cs`, `DexButton.cs`, `SceneLoader.cs` | 그림 경로 상수 한 줄씩 (`Art/Dream/UI…` → `UI/…`) | 낮음 |
| `Scripts/Mobile/Quest/QuestButton.cs`, `DexButton.cs` | **삭제** (HUD 왼쪽 버튼은 `MainHud` 프리팹의 `SideButtons`, 동작은 `MainHudView`) | 다른 브랜치가 고쳤으면 수정/삭제 충돌 → `MainHudView`로 옮김 |
| `Scripts/Mobile/Idle/FormationPanel.cs`, `CharacterDetail.cs`, `EquippedGearView.cs` | **삭제** (편성은 `FormationScreen` 프리팹 + `FormationScreenView`) | **중간.** 편성 기능(합성·편성·자동 편성)을 다른 브랜치가 고쳤으면 `FormationScreenView`로 옮겨 적용 |
| `Scripts/Mobile/Idle/UpgradePanel.cs` | **삭제** (새 `UpgradeNotebookView`로 대체, 어디서도 안 씀) | 다른 브랜치가 이 파일을 고쳤으면 삭제/수정 충돌 → 새 노트 쪽에 옮겨 적용 |

## 2. 기존 씬·프리팹 수정

| 파일 | 바꾼 곳 | 충돌 위험 |
|---|---|---|
| `Scenes/Main.unity` | 새 프리팹 배치(`UpgradeNotebook`·`MainHud`·`FormationScreen`·`DexScreen`), 예전 `UpgradePanel` 오브젝트 삭제, `GGoma`·`Char` 끔, `TabGroup.homeOnly` → 새 노트, `TabGroup.selectedColor` 알파 0, 하단 바·재화 칸·설정 버튼·편성/던전/뽑기 판 그림 교체, `BottomBar`에 `SafeAreaDock`·`TabIconLift`·`BottomBarLayer` | **높음.** Unity가 저장하며 레이아웃 값도 다시 써서 diff가 크다. 같은 오브젝트(하단 바·Equit·DungeonUI·GachaUI·WalletBar)를 고친 브랜치와 충돌 → Unity에서 열어 다시 적용 권장 |
| `Scenes/WorldTree.unity` | 하단 바(`SafeAreaDock`·`TabIconLift`·`BottomBarLayer`·아이콘·위치 y 24→0), 옆 버튼 아이콘(바탕 없이), 위 재화 줄, 보따리, 설정 아이콘 | 중간 |
| `Prefabs/Mobile/OptionPopup.prefab` (Main·세계수 공용) | 갈색 틀 `Frame` 추가, `Header`(메모 제목)를 `Window`→`Frame` 아래로 옮김, 구역 판 `Section*` 추가, 토글·슬라이더 그림, BGM/SFX 아이콘, UID 위치, 예전 `CloseBtn` 끔 + `BottomBand` 추가 | 중간 (구조 변경: Header 부모 이동) |
| `Prefabs/Mobile/Main/Wepon·Amor·Pet.prefab` | `BottomBand` 추가(`forwardTo` = 기존 `Out`), `Out` 끔, `FitWidth` 끔 + `FitInRegion` 추가 (재화 줄 아래~하단 띠 위에 비율 맞춤) | 낮음 |
| `Prefabs/Mobile/OptionPopup.prefab` (추가) | `Frame`·`Window`를 새 `Body`로 묶고 `FitInRegion`, `Dim`에 `SafeAreaDock(coverScreen)` | 중간 (계층 변경) |
| `Scenes/Main.unity` (추가) | `DungeonUI/Viewport`·`GachaUI/Viewport`에 `ScaleToWidth` | 낮음 |
| `Prefabs/Mobile/Main/GGoma.prefab`, `Char.prefab` | **삭제** (예전 편성 판·꼬마 고르기 창) | 다른 브랜치가 고쳤으면 수정/삭제 충돌 |
| `Scenes/Main.unity` (연결) | `GGoma`·`Char`·`TopBar`(꺼져 있던 예전 재화 줄) 인스턴스 삭제, `Equit` TabGroup의 펫·무기·방어구 탭 버튼 → `FormationScreen/Book/Body/SideButtons/Pet·Weapon·Armor`, `selectedColor` 흰색 | 높음 (같은 씬) |
| `Prefabs/Mobile/BottomBar.prefab` | **안 고침** (두 씬에서 각각 덮어씀) | — |

## 3. 새 파일 (충돌 없음)

- 스크립트 `Scripts/Mobile/UI/Main/`: `MainHudView`, `UpgradeNotebookView`, `UpgradeRowView`, `SafeAreaDock`, `TabIconLift`, `FormationScreenView`, `FormationCellView`, `DexScreenView`, `PopupBottomBand`, `BottomBarLayer`, `FitInRegion`, `ScaleToWidth`, `UiSkin`, `ScaleChildren`, `TopBarView`, `QuestScreenView`(+`PopupGate`), `QuestCardView`, `QuitGameButton` / `Scripts/Mobile/Idle/IdleBattle.Hud.cs`
- 프리팹 `Prefabs/Mobile/UI/Main/`: `MainHud`, `UpgradeNotebook`, `UpgradeRow`, `FormationScreen`, `DexScreen`, `QuestScreen` / `Resources/UI/Prefabs/PopupBottomBand`
- 리소스 `Resources/UI/` (+ `.meta`의 Sprite·9-slice 설정, `UI_PP_Under_Base01/02`는 왼쪽·오른쪽 면 두 조각)
- 머티리얼 `Fonts/LINESeedKR-Bd SDF Outline(.mat)`, `… Outline Thick.mat`

## 3-1. 화면 크기 대응 방식

| 무엇 | 방식 |
|---|---|
| Stage (모든 UI의 부모) | 안전 영역을 꽉 채운다. 폰(1080×2340)은 예전과 같다 |
| 하단에 붙는 판·목록 (홈 노트, 편성, 도감 본문, 던전·뽑기 목록) | 좌우 끝까지 늘이고 안의 내용은 폰 폭 기준 비율로 확대 (`ScaleToWidth`) |
| 가운데 팝업 창 (설정, 무기·방어구·펫) | 위 재화 줄 아래 ~ 하단 띠 위 영역에 비율 맞춤 (`FitInRegion`, 위 260·아래 230 비움) → 하단 띠에 가리지 않는다 |
| 하단 공용 바 · 팝업 하단 띠 | 좌우 화면 끝, 아래는 홈바 자리까지 |

## 3-2. 게임 진행 영향 점검 (11-05, 개편 시작 전 `c59443c` 대비)

UI 개편이 게임 수치·규칙을 바꾸지 않았는지 코드·씬·프리팹 diff 전체를 확인했다.

| 확인한 것 | 결과 |
|---|---|
| 강화 (`UpgradePanel` → `UpgradeNotebookView`) | 같은 호출(`IdleProgress.TryUpgrade`·`Cost`·`IsMax`·`IsCapped`·`Unlocked`), 살 수 있는 조건 동일, 길게 누르기 `HoldRepeat` 값 동일(0.4 / 0.15 / 0.03) |
| 편성 (`FormationPanel`·`CharacterDetail` → `FormationScreenView`) | 같은 호출(`Formation.Place`·`Clear`·`AutoArrange`·`Merge`·`MergeAll`). 바뀐 것은 고르는 순서(칸을 안 고르면 첫 빈 칸)뿐 |
| 전투 (`IdleBattle`) | HUD 그리기만 프리팹으로 옮김. 수확기 비행 시간은 표 값(`flightTime`) 고정이라 Stage 크기(`StageFrame`)가 바뀌어도 전투 속도·피해 불변 |
| 뽑기·던전·의뢰 | 가격·횟수·보상은 표(뽑기상품·던전·의뢰 시트)에서 읽음. 프리팹에서 빠진 `currency`·`cost11`·`cost35`는 지금 스크립트에 없는 예전 필드 찌꺼기(Unity가 다시 저장하며 지움). 던전 번호(1~5)·`ResultPopup.dungeon` 값 동일 |
| 광고 상자 | 간격·보상 그대로. 층만 바뀜(`BindCamera`) |
| **되돌린 것** | 새 일일 의뢰 화면(`QuestScreen`)·도감(`DexScreen`)이 `PopupGate`에 알리지 않아 **열린 동안 광고 상자 쿨타임이 흐르고 뒤로가기로 닫히지 않았다** (예전 `DailyQuestPopup`은 멈춤·닫힘). `QuestScreenView`·`DexScreenView`의 `OnEnable`/`OnDisable`에 `PopupGate.Opened/Closed`를 넣어 예전과 같게 했다 |

## 4. 합칠 때 주의

1. **폰트 SDF(`LINESeedKR-*.asset`)는 커밋하지 않는다.** 실행할 때마다 동적 아틀라스에 글자가 추가돼 바뀐다. 되돌릴 때는 Unity를 연 채 `git checkout` 후 그 에셋을 **Reimport** 해야 글자가 깨지지 않는다.
2. 씬 충돌은 텍스트로 풀지 말고, 한쪽을 받은 뒤 Unity에서 위 표의 변경을 다시 적용한다.
3. 예전 코드 HUD(`IdleBattle.BuildHud`)·퀘스트/도감 버튼 스크립트·예전 편성 스크립트는 **지웠다**. 이 파일들을 고친 브랜치와 합칠 때는 변경을 새 프리팹·View 스크립트로 옮긴다.

## 4-1. 커밋할 때 주의사항 · 권장사항

**커밋 전에 꼭**
1. **폰트 SDF 되돌리기**: `Assets/Fonts/LINESeedKR-Bd SDF.asset`·`LINESeedKR-Rg SDF.asset`이 바뀌어 있으면 플레이 중 글자가 추가된 것이다. `git checkout -- "Assets/Fonts/LINESeedKR-Bd SDF.asset" "Assets/Fonts/LINESeedKR-Rg SDF.asset"` 후 Unity에서 두 에셋을 **Reimport**. (Bd SDF는 1차 커밋 `e5f128e`에 한 번 들어갔다 — 새 글자가 필요해서가 아니면 다시 넣지 않는다)
2. **씬을 저장한 상태로**: 플레이 모드를 끄고, 씬이 dirty가 아닌지 확인. 플레이 중 바뀐 값이 저장되지 않게 한다.
3. **스크린샷 폴더 넣지 않기**: MCP 스크린샷이 `Assets/Screenshots`에 생기면 지운다(`.meta` 포함).
4. **`.codex/`는 넣지 않는다** (개인 도구 설정).
5. **뜻밖에 바뀐 파일 확인**: 예) `Assets/Resources/UI/Icon/cross.png` — 직접 바꾼 게 아니면 `git checkout`. 이번 개편에서 나는 고치지 않았다.
6. **콘솔 오류 0** 을 확인하고 커밋한다.

**나눠 담기 (권장)**
- 한 커밋 = 한 단계(이 문서 진행 기록의 한 줄). 메시지 머리 `[MOD] UI 개편 N차: …` 형식, 본문에 이 문서 표의 해당 줄을 요약.
- 가능하면 **코드(.cs)·프리팹·씬을 따로** 커밋하면 다른 브랜치와 합칠 때 되돌리기·골라 담기(cherry-pick)가 쉽다. 단 새 컴포넌트를 붙인 프리팹은 그 스크립트와 같은 커밋에 넣는다(안 그러면 Missing Script).
- 기존 파일을 고쳤으면 이 문서 **1장 표에 줄을 추가**하고 같은 커밋에 넣는다 (다른 브랜치가 충돌 위치를 알 수 있게).

**씬 덮어쓰기(override) 줄이기 (권장)**
- 프리팹 인스턴스의 값을 씬에서 고치지 말고 프리팹을 연다. 씬에서 고쳤으면 `Overrides ▸ Apply` 하거나 되돌린다.
- 11-05에 Main·WorldTree 씬의 쓸모없는 덮어쓰기(프리팹과 같은 값, 실행 때 코드·`AspectRatioFitter`가 쓰는 값, TMP 캐시 값)를 지웠다. 남은 것은 루트 위치·크기, `IsActive`, 씬 오브젝트 연결(`MainHud.battle` 등), 설정 버튼 `onClick` 뿐이다.

**합치기 전 (main 등 다른 브랜치와)**
- 먼저 `git fetch` 후 `git diff main...PJS --stat`로 겹치는 파일을 본다. `Main.unity`·`WorldTree.unity`가 둘 다 바뀌었으면 텍스트 병합하지 말고 한쪽을 받은 뒤 Unity에서 다시 적용(4장 2번).
- 1장 표에서 **충돌 위험 높음** 파일(`IdleBattle.cs`·`DailyQuestPopup.cs`·`Main.unity`)을 상대 브랜치가 고쳤는지 먼저 확인.
- 합친 뒤: 컴파일 오류 → Missing Script·빈 연결(프리팹 열어 확인) → S26·Z플립7·iPad Pro 13 화면 순서로 확인.

---

## 5. 진행 기록

| 날짜 | 단계 | 내용 |
|---|---|---|
| 11-04 | 1차 | 홈 강화 노트·HUD·하단 바 프리팹화, 리소스 교체 |
| 11-04 | 2차 | 편성(와이어프레임 공방 배치)·도감 새 화면, 코드 그림 경로·색 → 리소스 |
| 11-05 | 8차-정리 | 게임 진행 영향 점검(3-2장) → 일일 의뢰·도감 화면 `PopupGate` 복구. 씬 덮어쓰기 정리(Main: 무기·방어구·펫·던전·뽑기·강화 노트·설정·하단 바·HUD·위 바, WorldTree: 하단 바·위 바), 던전·뽑기 배너 `ScaleChildren` 빈 칸 제거, 강화 노트 회색 버튼 그림을 프리팹에 지정, 안 쓰는 `GridFitWidth` 삭제, 도감 아래 버튼 간격. 커밋 주의사항(4-1장) |
| 11-05 | 7차 | 광고 상자를 Main 카메라 캔버스에 붙여 팝업 아래로(`AdBox.BindCamera`). 가운데 팝업 노란 탭을 틀 위로 40 튀어나오게(탭 +40~−130, 속지가 아래를 덮음; 설정·도감·일일 의뢰·통계·수확 완료·장비 창·수집·전투 설정). 장비 창 정보 칸 410→530(창 1660→1780, 목록·요약 아래로, 버튼 글자 40·44). 던전·뽑기: 배너 제목 메모 → 목록 판 위 노란 탭, 스크롤 잘림을 베이지 속지 위 끝에 맞춤(Viewport 위 −510, Page 위 −70), 설명 판 위로, 던전 레벨을 입장 버튼 안(`LevelText`)으로. 일일 의뢰 카드·버튼 폭을 좁아진 창(892)에 맞춤, 도감 격자 가운데·좌우 여백(`ScaleToWidth` 1100)·아래 버튼 간격. 세계수 의뢰 시트(`QuestPopup`): 탭 아이콘 지우고 제목 가운데, 주간 카드(의뢰인 그림 칸·말풍선 안 의뢰 아이콘·달성도 막대·완료 버튼 오른쪽), 일일 카드(일일 의뢰 창과 같은 구성: 이름·그림 칸·말풍선·완료 버튼·막대), 탭 선택 그림 `Btn_Yellow`. S26 확인, 의뢰 시트는 iPad도 확인 |
| 11-05 | 6차 | 팝업 틀 통일: 갈색 틀(`Out_Wood`)과 속지(`Out_Paper1`) 사이에 노란 탭(`Under_Tag_Yellow`) 제목 — 설정·도감·의뢰·업그레이드 통계·수확 완료·장비 창 / 아래 시트(성장·꾸미기·꼬마·공방·의뢰·편성)는 표지 `Side02`·속지 `Side01` 사이 노란 탭. 장비 창 구조 정돈(창 960×1660 세로형, 틀·탭·속지가 창 안, 장비 칸 4열, 펫 정보 칸을 무기와 같은 구조로), 팝업 좌우 여백 70. 던전·뽑기 카드 좌우 56·간격 30. 뽑기 버튼 글자·아이콘·가격 확대(`GachaCardView.Style`). 위 공용 바 통일: `TopBar` 프리팹을 세계수 화면 모양으로 + `TopBarView`(레벨·골드·균사석), Main의 `WalletBar`·`OptionButton`을 `TopBar`로 교체, 던전·뽑기 배너 제목을 바 아래로 |
| 11-05 | 5차-화면별 ④ | 던전·뽑기 배너 안 제목·설명을 폭 비율로 키움(`ScaleChildren`), 세계수 카드 글자 1.15배·굵게·간격 `UiSkin.Gap`(`TreeUi.Text`·`ScrollList`), 수확 완료 창(`ResultPopup.prefab`: 틀·속지·메모 제목·버튼) · 다음 레벨 자동 도전 체크 상자를 그림으로(`ResultPopup.cs`), 전투 설정 창 예전 그림 교체 |
| 11-05 | 5차-화면별 ③ | 던전(제목 메모·설명 속지·카드 `Paper1`·칩 갈색·잠금 회색), 뽑기(제목 메모, 카드 바탕 `Beige2`, 판 속지) — 씬 덮어쓰기 되돌리고 프리팹에 반영. 세계수 시트 5개(성장·꾸미기·꼬마·공방·의뢰 `QuestPopup`): 표지+속지, 메모 제목+새 아이콘, 공용 하단 띠(`forwardTo` = 기존 닫기), 예전 CloseBar 그림 끔. 장비 창 코드 글자 1.25배·굵게. 설정 BGM/SFX 줄, 의뢰 카드 확인 |
| 11-04 | 5차-화면별 ② | 일일 의뢰 새 화면(`QuestScreen` 프리팹 + `QuestScreenView`·`QuestCardView`, 2열 카드·주간/일일 버튼, HUD 의뢰 버튼이 연결 — 없으면 예전 `DailyQuestPopup`), 설정(갈색 구역 칩 음향·알림 설정, 계속하기/게임 종료 버튼 + `QuitGameButton`, BGM/SFX 줄 정리), 도감 탭 글자색 |
| 11-04 | 5차-화면별 ① | 공통 규격 `UiSkin`. 편성(칸 줄 5칸·버튼 줄·정보 구역·목록 칩), 무기·방어구·펫 창(틀·속지·정보/목록 구역, 프리팹 자체에 리소스 반영 + 씬 덮어쓰기 되돌림), 비활성 버튼 회색 그림(프리팹·코드 팝업 전부) |
| 11-04 | 4차-연결 | 예전 UI를 숨기던 방식 → 코드에서 새 프리팹 연결: IdleBattle 예전 HUD 생성 제거, 퀘스트·도감 버튼은 MainHud, Equit 탭은 새 편성 화면 버튼, 예전 편성 스크립트·프리팹 삭제 |
| 11-04 | 3차-화면 대응 | Stage를 안전 영역 끝까지 늘림, 판·목록은 폰 폭 기준 비율 확대(`ScaleToWidth`, 최대 1.3), 팝업 창은 상단~하단 띠 사이 맞춤(`FitInRegion`), 하단 띠가 부모 확대에도 바닥에 붙게 수정. S26·Z플립7·iPad Pro 13 확인 |
| 11-04 | 3차-기반 | 층 순서 정리(광고 상자 < 하단 바 < 편성 팝업 < 팝업), 팝업 하단 띠(Side03 + X), 회색 리소스 적용 |

---

## 6. 다음에 이어서 할 일 (11-05 오후 기준)

| 순서 | 화면 | 남은 것 |
|---|---|---|
| 1 | 전투 통계 | 게임에 화면이 없음 (와이어프레임 09) |
| 2 | 세트 보너스 | 게임에 화면이 없음 (와이어프레임 11) |
| 3 | 수확 완료 | 와이어프레임 08의 도감 기록·신종 발견·스테이지 표시는 데이터가 없어 틀만 맞춤 |
| 4 | 전투(InGame) HUD | 이번 개편 범위 밖, 손대지 않음 |
