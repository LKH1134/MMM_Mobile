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
| 흰 글자 외곽선 (11-05) | 갈색 **바깥** 외곽선(글자 안으로 파고들지 않음). 폰트 `Resources/UI/Fonts/LINESeedKR-Bd SDF Wide`(여백 20) + 재질 `LINESeedKR-Bd Wide Outline S·M·L` — 글자 크기(자동 축소면 최대 크기) **32 미만 S 2px · 32~47 M 4px · 48 이상 L 6px**. 코드로 만드는 글자는 `OutlineText.Apply(t)`·`ApplyIfWhite(t)` (**흰 글자는 무조건 외곽선**). 외곽선 글자에 `outlineWidth`를 따로 쓰지 않는다(예전 폰트 재질 복사본이 생겨 글자가 조각난다). 재질은 Face Dilate = Outline Width(바깥으로만), 가짜 굵게(`_WeightBold`) 0 — 폰트가 이미 Bd라 덧굵게 하면 외곽선과 뭉친다. 두 줄 이상·자동 축소로 실제 글자가 작아지는 칸은 한 단계 얇은 재질 + 글자·줄 간격(예: 방치 정산 "광고 보고 2배 보상" = S, 간격 4·줄 14) |
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
| `Scripts/Mobile/Idle/IdleReportPopup.cs` (9차) | `Create`: 프리팹 `Resources/UI/Prefabs/IdleReportPopup`(`IdleReportView`)이 있으면 그것을 띄우고 `Bind`, 없으면 예전 `Build()`. `RenderView()` 추가(시간·골드·앱 밖·균사석·처치·스테이지 칸을 따로 채움, 스테이지는 HUD와 같은 "7-7" 표기), `Show`의 2배 버튼 null 확인. 보상 계산·광고 2배 지급은 그대로 | 중간. `Create`·`Render`·`Show`를 고친 브랜치와 충돌 |
| `Scripts/Mobile/Idle/EquipmentPanel.cs` | 색 상수 3개 → 그림 경로 상수, `BuildMergeBar` 막대 그림, `Refresh` 칸 바탕 그림(고른/장착/보통), 못 얻은 장비 회색 칠 제거 | 중간 (`Refresh` 안 칸 그리기) |
| `Scripts/Mobile/Idle/CharacterDetail.cs` | 색 상수 → 그림 경로 상수, 막대 그림, 스킬 칸 그림(해금 초록/잠금 회색), 못 얻은 꼬마 회색 칠 제거 | 중간 |
| `Scripts/Mobile/UI/GachaCardView.cs` | 재화 버튼 `UI_Btn_Sky`, `CoinColor` 흰색, 막대 색 흰색 | 낮음 |
| `Scripts/Mobile/UI/GachaResultPopup.cs` | 등급 테두리: 등급색 칠 → 등급별 그림 `GradeSprites` | 낮음 |
| `Scripts/Mobile/WorldTree/UI/TreeUi.cs` | 그림 경로 상수 블록, `Card`·`Button`·`Bar`가 색 대신 그림을 고른다 (`ButtonOn/Alt/Off`·`CardOn/Off` 색 값은 어떤 그림을 쓸지 고르는 데만 씀) | 중간 (세계수 UI 공용 도우미) |
| `EquipmentPanel.cs`·`GachaCardView.cs`·`DailyQuestPopup.cs`·`AdBoxPopup.cs`·`IdleReportPopup.cs`·`TreeUi.cs` (추가) | 버튼을 만든 줄 뒤에 `UiSkin.GreyWhenDisabled(…)` 한 줄씩, `EquipmentPanel.SelectedSprite` → `InnerLine1` | 낮음 |
| `UI/GachaCardView.cs` (추가) | `Style`: 제목 40·자동 축소, 아래 줄 아이콘 56·가격 38 | 낮음 |
| `UI/GachaPanel.cs`·`Ads/AdBoxPopup.cs`·`Quest/DailyQuestPopup.cs`·`UI/PowerSaveMode.cs`·`InGame/InGameTutorial.cs`·`WorldTree/UI/TreeUi.cs`·`Idle/EquipmentPanel.cs`·`Idle/IdleReportPopup.cs` (10차-보완) | 흰 글자를 만드는 줄 뒤에 `OutlineText.ApplyIfWhite(t)` 한 줄씩. `GachaPanel.CreateTitle`은 `outlineWidth`·`outlineColor` 두 줄 삭제(글자 조각남 원인) | 낮음 (한 줄 추가) |
| `UI/GachaResultPopup.cs`·`Ads/AdBoxPopup.cs`·`WorldTree/UI/TreeGrowPanel.cs` (16차) | 화면 만드는 부분(`Build`·`BuildCell`·생성자·`Build`) 배치·그림 교체, `AdBoxPopup.RewardPair` 추가 | **중간.** 이 세 화면의 모양을 다른 브랜치가 고쳤으면 충돌 |
| `WorldTree/UI/TreePanel.cs`·`TreeKidPanel.cs`·`TreeDecoPanel.cs`·`TreeCraftPanel.cs` (18차) | `TreePanel`에 견본 도우미 추가, 세 시트 `Build` 앞에 견본 경로 추가 + 예전 코드는 `BuildByCode`로 이름만 바꿔 남김 | **중간.** 세 시트 내용을 바꾼 브랜치는 견본·`Build`에도 같이 반영 |
| `Ads/AdBoxPopup.cs`·`WorldTree/UI/TreeGrowPanel.cs` (17차) | 프리팹/견본이 있으면 그것을 쓰는 앞부분 추가. `TreeGrowPanel`의 예전 코드는 `BuildByCode`로 이름만 바꿔 남김 | **중간.** 성장 시트 내용(카드 추가 등)을 바꾼 브랜치는 견본·`Build`에도 같이 반영해야 한다 |
| `WorldTree/UI/TreeUi.cs` (16차) | `ButtonBlue` 색·그림, `CardBeigeSprite`, `Card(…, string sprite = null)` 선택 인자 | 낮음 (기존 호출 그대로 동작) |
| `Ads/AdBox.cs` (22차) | 상수 2줄: `TargetY` 0.175, `ChestIcon` = `UI/Icon/advertisement` | 낮음. main은 이 줄을 `ChestKid`(달 꼬마 그림)로 바꿨다 — 합칠 때 광고 아이콘으로 맞춘다 |
| `WorldTree/UI/TreeQuestPanel.cs` (21차) | `Cards()`의 `desc`·`reward`가 `InfoBox/Desc`·`InfoBox/Reward`도 찾음 (2줄) | 낮음 |
| `UI/GachaPanel.cs` (20차-보완2) | `BuildRightColumn`에 `GachaCardView.OneLine(freeText)` 한 줄, `Refresh`의 광고 버튼 글자 2줄 → 한 줄 제목(아래 줄 "내일 다시"·"광고 시청" 없음) | 낮음. 광고 버튼 문구를 고친 브랜치와 `Refresh` 첫 줄 충돌 |
| `UI/GachaCardView.cs` (20차) | `Style` 끝: 재화 아이콘이 없는 버튼(광고·무료 소환)은 버튼 좌우 끝에 잎(`Leaf_L/R_Beige`) — `Leaf()` 함수 추가. 오른쪽 열 배치 상수 `ExpY 162`·`FreeY 82`·`CoinY −80`·`CoinHeight 210` | 낮음 (상수·몇 줄) |
| `UI/GachaCardView.cs` (10차) | `Outline()`: `OutlineText.Apply(t)` 먼저, 새 재질이 없을 때만 예전 `outlineWidth 0.2` | 낮음 |
| `Idle/EquipmentPanel.cs` (추가) | `FitDetailText`: 이름 폭을 정보 칸 안으로 제한 | 낮음 |
| `Idle/EquipmentPanel.cs` (12차) | `Slot.normal` 추가, `Refresh` 칸 그림: 보통 칸은 프리팹 그림(없으면 `SlotSprite`) | 낮음 (2줄) |
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

- 스크립트 `Scripts/Mobile/UI/Main/`: `MainHudView`, `UpgradeNotebookView`, `UpgradeRowView`, `SafeAreaDock`, `TabIconLift`, `FormationScreenView`, `FormationCellView`, `DexScreenView`, `PopupBottomBand`, `BottomBarLayer`, `FitInRegion`, `ScaleToWidth`, `UiSkin`, `ScaleChildren`, `TopBarView`, `QuestScreenView`(+`PopupGate`), `QuestCardView`, `QuitGameButton` / `Scripts/Mobile/Idle/IdleReportView.cs` / `Scripts/Mobile/Idle/IdleBattle.Hud.cs`
- 프리팹 `Prefabs/Mobile/UI/Main/`: `MainHud`, `UpgradeNotebook`, `UpgradeRow`, `FormationScreen`, `DexScreen`, `QuestScreen` / `Resources/UI/Prefabs/PopupBottomBand`, `Resources/UI/Prefabs/IdleReportPopup`
- 리소스 `Resources/UI/` (+ `.meta`의 Sprite·9-slice 설정, `UI_PP_Under_Base01/02`는 왼쪽·오른쪽 면 두 조각)
- 머티리얼 `Fonts/LINESeedKR-Bd SDF Outline(.mat)`, `… Outline Thick.mat` (10차부터 안 씀, 남겨 둠)
- 폰트 `Resources/UI/Fonts/LINESeedKR-Bd SDF Wide.asset` + 재질 `LINESeedKR-Bd Wide Outline S·M·L.mat`, 스크립트 `UI/Main/OutlineText.cs`, 에디터 전용 `Editor/FontReimportRefresh.cs`(폰트를 다시 가져오면 글자를 다시 그림)

## 3-0. UI 프리팹 위치 (11-05 정리, `Assets/Prefabs/Mobile/` 아래)

화면별로 묶었다. 파일은 Unity에서 옮겨 GUID가 그대로라 씬·프리팹 연결은 바뀌지 않는다 (모든 씬·중첩 프리팹에서 끊긴 연결 0 확인). **이 문서의 위쪽(이전) 기록은 옛 경로로 적혀 있다** — 아래 표로 찾는다.

| 새 위치 | 프리팹 | 옛 위치 |
|---|---|---|
| `UI/Common/` (여러 씬 공용) | `TopBar` · `BottomBar` · `Toast` · `ConfirmPopup` · `OptionPopup` · `CollectPopup` | `Prefabs/Mobile/` 바로 아래 |
| `UI/Main/` (홈) | `MainHud` · `UpgradeNotebook` · `UpgradeRow` · `QuestScreen`(일일 의뢰) · `DexScreen`(도감) | 그대로 |
| `UI/Main/Formation/` (편성 탭) | `FormationScreen` · `Wepon`(무기) · `Amor`(방어구) · `Pet` | `UI/Main/`, `Main/` |
| `UI/Main/Dungeon/` | `DungeonUI` | `Main/` |
| `UI/Main/Gacha/` | `GachaUI` · `Gacha_Panel1`(뽑기 카드) | `Main/`, `Gacha_Panel/` |
| `UI/WorldTree/` (세계수 시트) | `GrowPopup` · `SkinPopup` · `KidPopup` · `CraftPopup` · `QuestPopup`(의뢰) | `WorldTree/`, `Prefabs/Mobile/` |
| `UI/InGame/` (던전 전투 화면) | `ResultPopup`(수확 완료) · `InGameOptionPopup` | `Prefabs/Mobile/` |
| `InGame/` (UI 아님, 그대로) | `Harvester` · `Mushroom_Single` · `Mushroom_Cluster` | — |
| `UI/Common/Resources/` (코드가 이름으로 읽음: `Resources.Load("PopupBottomBand")`) | `PopupBottomBand`(팝업 하단 띠 X) | `Assets/Resources/UI/Prefabs/` |
| `UI/Main/Resources/` (코드가 이름으로 읽음: `Resources.Load("IdleReportPopup")`) | `IdleReportPopup`(방치 정산) | `Assets/Resources/UI/Prefabs/` |

- `Resources` 폴더 안 두 프리팹은 **파일 이름을 바꾸면 안 된다** (코드가 이름으로 찾는다: `PopupBottomBand.PrefabPath`·`IdleReportPopup.PrefabPath`). 같은 이름을 다른 `Resources` 폴더에 또 만들지 않는다.
- 파일 이름(`Wepon`·`Amor` 철자 포함)은 바꾸지 않았다 — 씬 오브젝트 이름·다른 브랜치와 맞추기 위해.
- **합칠 때:** 다른 브랜치가 옛 위치의 프리팹을 고쳤으면 git이 대개 "이름 바뀜(rename)"으로 알아서 합친다. 옛 위치에 같은 이름의 **새** 프리팹을 만든 브랜치가 있으면 위치가 갈라지므로 이 표의 새 위치로 옮긴다. `UI_REWORK_GUIDE.md`·`HANDOFF.md`·`AGENT_WORKLOG.md`(다른 작업자 문서)의 경로는 옛 경로 그대로다.

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

## 4-0. main에 합칠 때 (11-05 밤 기준: 갈라진 곳 `c59443c`, main `c6d8675`)

갈라진 뒤 **main에 34커밋**이 쌓였다(펫 삭제, 던전 소탕 삭제, 폰트 정적 굽기, 그림 교체·미사용 리소스 185개 삭제, BGM, 개발자 모드 등). PJS는 UI 개편 전부. 두 쪽이 같은 파일을 많이 고쳤으므로 **PJS를 main에 바로 합치지 말고**, 새 브랜치에서 main을 PJS 쪽으로 받아(`git merge main`) Unity에서 풀고 확인한 뒤 PR로 main에 넣는다.

**1. main이 지운 기능 — main 쪽을 따른다**
| main 커밋 | 내용 | PJS에서 할 일 |
|---|---|---|
| `08bb82b`·`d29c186` 펫 삭제 | `Pet.prefab` 삭제, `GachaKind.Pet`·`Equipment.Pet`·편성 6번째 펫 칸 제거, 편성 칸 위 3·아래 2 | `UI/Main/Formation/Pet.prefab` 삭제, 편성 `SideButtons`의 펫 버튼 삭제(무기·방어구만), 뽑기 `GachaUI`의 펫 카드 삭제, Main 씬의 Pet 인스턴스 삭제. 펫을 부르는 PJS 코드가 있으면 컴파일 오류로 드러난다 |
| `344f7fb` 던전 소탕 삭제 | `ResultPopup.cs` 소탕 부분 삭제 | `ResultPopup.cs`는 main 것을 받고 PJS의 `BuildAutoRow` 체크 상자 그림 2줄만 다시 넣는다 |
| `c1e73d8`·`21f5ded` 미사용·임시 리소스 삭제 | 그림 185개가량 삭제·교체 | 합친 뒤 PJS 프리팹에 빠진 그림(빈 칸·흰 네모)이 없는지 전체 검사 |
| (old path) `CollectPopup.prefab` 삭제 | 예전 도감 | PJS의 `UI/Common/CollectPopup.prefab`도 삭제 (6-1장의 미사용 항목도 같이 해결) |

**2. 폰트 — main의 정적 아틀라스를 받는다**
- main `970fa52`·`772656a`가 `LINESeedKR-Bd/Rg SDF`를 정적 아틀라스로 굽고 재질 ScaleRatio를 다시 계산했다(플레이 중 HUD 글자 사라짐 대응). **폰트 에셋·재질은 main 것을 받는다.** PJS는 이 둘을 커밋하지 않았다(1차 `e5f128e` 한 번 제외).
- PJS의 **`Resources/UI/Fonts/LINESeedKR-Bd SDF Wide`(+ Outline S·M·L·Red 재질)는 아직 동적**이다. 같은 "글자 사라짐"이 날 수 있으니 합친 뒤 main과 같은 방법으로 정적으로 굽는 것을 권장(여백 20이라 재질 ScaleRatio도 다시 계산). 굽고 나면 4-1장의 "폰트 되돌리기"는 필요 없어진다.

**3. PJS가 옮긴 프리팹을 main이 옛 위치에서 고침** (11차 폴더 정리 — 내용이 많이 달라 git이 이름 바뀜으로 못 맞출 수 있다 → "수정/삭제 충돌"로 나오면 **PJS의 새 위치 파일을 남기고** main의 작은 변경을 Unity에서 손으로 다시 넣는다)
| main에서 고친 옛 위치 | main 변경 | 새 위치에 다시 넣을 것 |
|---|---|---|
| `Main/DungeonUI.prefab` | 던전 카드 그림 교체(`6c040da`)·배너(`9c18676`) — 그림 비율(AspectRatioFitter 2.86 → 2.35) | `UI/Main/Dungeon/DungeonUI`의 카드 `Art` 비율·배너 그림 |
| `Main/GachaUI.prefab` | 펫 카드 삭제로 목록 높이 1762 → 1334(1732 → 1304), 배너 교체(`a2f0768`) | `UI/Main/Gacha/GachaUI`에서 펫 카드 삭제 후 높이 같은 식으로 |
| `OptionPopup.prefab` | `08b2ebb` BGM 슬라이더에 `BgmVolumeSlider` 컴포넌트 | `UI/Common/OptionPopup`의 `SoundBgm/Slider`에 같은 컴포넌트(던전 변형 `InGameOptionPopup`은 기본을 따라오는지 확인) |
| `QuestPopup`·`WorldTree/GrowPopup`·`WorldTree/KidPopup` | 임시 버섯 그림 → 새 그림(GUID 교체) | 세 프리팹에서 빠진 그림 확인. 꼬마 시트는 PJS에서 새로 만들어 해당 그림을 안 쓸 수 있음 |
| `Main/GGoma.prefab` | 펫 칸 삭제로 위치 | PJS에서 예전 편성 프리팹은 이미 안 씀(4장 3번) — 삭제 유지 |

**4. 둘 다 고친 코드** — 합칠 때 양쪽 변경을 모두 살린다
| 파일 | main | PJS (1장 표) |
|---|---|---|
| `Idle/IdleBattle.cs` | 전투 크기·간격, 보스 10종 | 예전 코드 HUD 제거 — **충돌 위험 높음** |
| `Idle/EquipmentPanel.cs` | 펫 삭제, `Equipment.Grade` → `Eq.Grade` | 칸 그림·정보 칸 — 21차에 `mid`·`Down` 구조(안쪽 판 그림을 그 자체로) |
| `UI/GachaPanel.cs` | 펫 삭제 | 광고 버튼 한 줄·횟수 |
| `UI/GachaResultPopup.cs` | 등급 색 줄 삭제 | 칸 모양 다시 그림 |
| `Ads/AdBox.cs` | 상자 그림 = 달 꼬마, 광고 균사석 = 상수 시트 | `BindCamera()` 두 줄 |
| `UI/PopupLayer.cs` | 주석(펫) | — |
| `WorldTree/UI/TreeDecoPanel.cs` | 꾸미기 탭 숫자 = 만든 장식 수/전체 | 견본 경로 + `BuildByCode` — main의 탭 숫자 계산을 견본 경로(`ZoneTab` 글자)에도 넣는다 |
| `Idle/FormationPanel.cs` | 펫 칸 삭제 | PJS는 새 편성 화면(`FormationScreenView`)을 씀 |

**5. 씬** (`Main`·`WorldTree`·`Start`) — 텍스트로 병합하지 않는다
- main 변경은 작다: Main = 펫 인스턴스·연결 삭제, WorldTree = 세계수 그림·비율(`0.4378`), Start = 로딩 배경 그림. **PJS 씬을 받고 이 몇 가지만 Unity에서 다시 넣는 것**이 쉽다.
- BGM(`9f09db1`)·개발자 모드(`3b32267`)가 씬 오브젝트를 쓰는지 main 씬에서 확인하고, 쓰면 PJS 씬에도 추가.

**6. 합친 뒤 확인 순서**
1. 컴파일 오류 0 (펫 참조)
2. Missing Script·빈 그림 검사 — 모든 UI 프리팹·씬
3. 폰트: Wide 글자·외곽선이 정상인지, 플레이 중 글자가 사라지지 않는지
4. 화면: 홈·편성(무기·방어구)·던전·뽑기(카드 3장)·세계수 시트 5개·설정 — S26·Z플립7·iPad Pro 13
5. 버튼 눌림(21차처럼 보이는 모든 버튼 가운데를 눌러 그 버튼이 받는지)

## 4-1. 커밋할 때 주의사항 · 권장사항

**커밋 전에 꼭**
1. **폰트 SDF 되돌리기**: `Assets/Fonts/LINESeedKR-Bd SDF.asset`·`LINESeedKR-Rg SDF.asset`·`Assets/Resources/UI/Fonts/LINESeedKR-Bd SDF Wide.asset`이 바뀌어 있으면 플레이 중 글자가 추가된 것이다. `git checkout -- "Assets/Fonts/LINESeedKR-Bd SDF.asset" "Assets/Fonts/LINESeedKR-Rg SDF.asset"` 후 Unity에서 두 에셋을 **Reimport**. (Bd SDF는 1차 커밋 `e5f128e`에 한 번 들어갔다 — 새 글자가 필요해서가 아니면 다시 넣지 않는다)
   - 되돌리면 글자 자리(아틀라스 좌표)가 바뀌어, 에디터에 이미 그려진 글자가 **한 단어가 조각나 2~3개씩 겹쳐** 보일 수 있다(실행하면 다시 그려져 정상). `Editor/FontReimportRefresh.cs`가 폰트를 다시 가져올 때 그 폰트 글자를 모두 다시 그린다. 그래도 남으면 그 프리팹·씬을 닫았다 다시 연다.
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
| 11-06 | 24차-보완3 | 성장 시트 글자 줄이기: 진화 칸 "요구: 나이테 Lv.10" + 다음 줄에 좋아지는 것만("이점:"·"경험치 MAX" 삭제), 나이테 설명은 최대일 때 "최대 Lv"만(아니면 비움), 공물은 제목 줄 "공물 (창고 버섯 n개)" / 아래 "선호 버섯 (×2)" / 버섯 칸, 가호 칸은 제목 "세계수의 가호 (2/8)"만(받은 수·다음 가호 줄 숨김 — 자세한 건 목록 팝업). 오른쪽 네 버튼 180×140으로 줄이고 세로 가운데 |
| 11-06 | 24차-보완2 | 성장 시트 배치: 화면을 탭 아래 선까지 40 올림. 오른쪽 네 칸 버튼을 칸 높이만큼(위아래 16) 키우고 **버튼 안에 비용**: 진화 = "진화" + 골드 줄 + 균사석 줄(아이콘 `UI/Icon/gold`·`mycelium_stone` + 값, 균사석이 0이면 숨김), 나이테 = "성장" + 골드 줄(최대면 숨기고 글자 가운데), 공물 = "바치기" + 얻는 경험치. 진화 칸 본문은 비용을 빼고 "요구: 나이테 Lv.10 · 경험치 MAX"와 "이점: 장식 칸·제작 슬롯·가호"만. 공물 칸: 제목 줄 오른쪽에 창고 수·선호 배율, 아래에 이 단계 선호 버섯 칸(`Items/Item` 견본: 그림 · 오른쪽 아래 ×개수 · 아래 가운데 이름). 현재 효과는 길어지면 위아래로 밀어 볼 수 있게(`InfoBox/Scroll` 안 `Effects`) |
| 11-06 | 24차-보완 | 성장 시트: 진화 칸을 오른쪽 맨 위로(오른쪽을 1/4씩 네 칸: 진화·나이테·공물·가호, 진화 칸 왼쪽 그림 = 다음 단계 나무 `evolveIcon`), 왼쪽은 세계수 그림 + 현재 효과(아래 2/3 전부). 가호 목록은 시트 안 창 → **메뉴판 팝업** `GrowPopup/BlessPopup`(Dim + 광고 보기 창과 같은 틀: 우드 `Frame`·노란 탭 "세계수의 가호"·속지·빨간 닫기, Dim을 눌러도 닫힘). 줄 견본 `BlessRow`(단계 띠 `Badge/Stage`·`Effect`, 받은 가호 베이지·잠긴 가호 회색 `In_Color_Grey`)·`Header`·`StoreRow`(`Icon`·`Name`·`Count`)는 `Frame/Paper/Scroll/Content` 아래 — 모양은 프리팹에서 고치고 이름만 지키면 된다. 세계수 씬 꾸미기 버튼: 사용자가 고친 값(버튼 `Skin` y 400 → 408, 글자 `SkinText` y 16 → −12)이 11-06 09:09에 씬 파일이 다시 저장되며 예전 값으로 돌아가 있어 다시 넣음 |
| 11-06 | 24차 | **넓은 화면 홈**: 퀘스트·도감 버튼(`MainHud/SideButtons`)은 왼쪽 끝 기준(폰에서 같은 자리 95), `MainHud` 루트를 가로로 꽉(자식은 모두 가운데 기준이라 그대로), 꼬마 묶음 `GgomaArea`는 왼쪽 끝·세로 가운데, 적 영역 `EnemyArea`는 오른쪽 끝·세로 가운데 기준으로 — 폰은 그대로, 넓은 화면에서 양옆으로 붙는다. 새 `UI/ScaleWithParentWidth.cs`(부모 폭/1080, 1~1.35배)를 두 영역에 붙여 넓은 화면에서 전투가 커짐(iPad 1.3배). **세계수 성장 시트 새 화면**: `GrowPopup/Sheet/Page/Content/GrowScreen` + 새 `WorldTree/UI/TreeGrowScreen.cs`. 왼쪽 1/3 = 세계수 그림·단계 띠(`TreeBox`) / 현재 효과(`InfoBox`, 효과 합계 + 제작 슬롯·장식 칸) / 진화(`EvolveBox`: 경험치 막대·조건·열리는 것·진화 버튼), 오른쪽 2/3 = 1/3 높이씩 나이테(Lv·막대·성장 버튼)·공물(창고→경험치·선호·바치기)·세계수의 가호(받은 수·다음 가호·목록 버튼). 목록 버튼 → 가호·창고 버섯 목록 창(`ListPanel`, 닫기). 예전 `TreeGrowPanel`(Content에 붙은 것)은 **끄기만** 함(견본 `Templates` 그대로). S26·iPad Pro 13 확인 |
| 11-06 | 23차 | 홈 전투 배경 `Canvas/BG`: `BGimage`(830×1896, 화면 전체에 늘여 찌그러짐) → 새 `Resources/Art/Dream/Backgrounds/BGimage1440`(1440×1440, Sprite로 가져옴·설정은 예전 배경과 같게). 새 `UI/BackgroundCover.cs`: 그림 비율을 지킨 채 화면 위 ~ 업그레이드 노트 위 끝(+60 겹침) 영역을 꽉 덮고, 남는 쪽은 넘쳐서 잘림(폰 = 좌우가 잘림, 더 넓은 화면 = 위아래). 노트가 꺼진 다른 탭에서는 마지막 노트 위치를 써서 크기가 탭마다 안 바뀜. S26 1452²·Z플립7 1542²·iPad Pro 13 1754² 확인 |
| 11-06 | 22차 | 홈 전투: 꼬마 칸 묶음·적 영역(Main 씬 `IdleBattle`의 칸 부모·`EnemyArea`)을 1.2배로 키우고 가운데를 화면 위에서 1050(화면 가운데 1170보다 살짝 위, 업그레이드 노트 위 1392 안쪽)으로 내림 — 꼬마 칸 왼쪽 끝·적 영역 오른쪽 끝은 그대로, 칸 사이 간격도 1.2배. `ggomaSize` 130 → 156, `enemySize` 110 → 132(보스 560·수확기 64는 그대로). 광고 상자(`AdBox`): 머무는 높이 `TargetY` 0.13 → 0.175(적 영역 바로 위를 지나가게, 화면 위에서 약 760 — 처음 0.25(1/4)에서 한 번 내림), 그림 `treasure_chest` → `UI/Icon/advertisement` |
| 11-05 | 21차-보완 | 꼬마 시트 버섯 부탁 칸 세로 +80(줄 350·칸 346): 늘어난 만큼 안쪽 내용 칸(`IconBox`)이 커지고 버섯 그림도 커짐, 설명 글자 17 → 22·보상 14 → 17. 아래 주간 상자·꼬마 정보·구분선·"보유 꼬마"·목록을 80 아래로. 구분선(`Line`)은 주간 상자 바로 아래(16 띄움)로 옮기고 꼬마 정보를 그 아래, "보유 꼬마" 제목은 정보 칸 10 아래. 주간 상자 버튼: 받을 수 있으면 `Btn_Yellow`(못 받으면 회색 그대로), 좌우 끝·세로 가운데에 잎(`Leaf_L/R_Beige` 60, 안쪽 30) |
| 11-05 | 21차 (전체 검토) | **① 계층**: 안쪽 판 위에 그려지지만 판의 형제였던 것을 판 안으로(보이는 위치 그대로, 1080×2340 기준으로 오프셋 다시 계산) — 세계수 시트 5개 `Sheet/Content` → `Sheet/Page/Content`, 의뢰 주간 카드 `Desc`·`Reward` → `InfoBox`, 편성 `Body`의 9개(SlotRow~Scroll) → `Body/Page`, 수확 완료 `Content`·두 버튼 → `Window/Paper`, 업그레이드 통계 `Text` → `Stats/Paper`. 장비 창 3개는 코드가 `mid`·`Down`의 자식 이름으로 찾아서 반대로 안쪽 판 그림(`Panel`)을 `mid`·`Down` 자체에 옮기고 `Panel` 삭제(`TreeQuestPanel`은 `InfoBox/Desc`·`InfoBox/Reward`도 찾게 2줄). **② 안 쓰는 것**: 꼬마 시트의 예전 화면(`TreeKidPanel` 꺼진 컴포넌트·견본 `Templates`·`Placeholder`) 삭제. `CollectPopup`(예전 도감, `DexScreen`과 중복)은 어디서도 안 쓰지만 HANDOFF에 "다시 쓸 예정"이라 남김. **③ 씬 덮어쓰기 → 프리팹**: 하단 바(Main 127·세계수 99개 중 두 씬이 같은 값 118개 + `SafeAreaDock`·`BottomBarLayer`·`TabIconLift`를 프리팹으로, 씬에는 장면별 값만 남김: Main `WorldTreeBtn.SceneButton`, 세계수 `MainTabButton` 4개·`TabIconLift` tab/currentScene, 세계수 `WorldTreeBtn` 투명 그림의 옛 색 덮어쓰기는 삭제), 위 바(레이아웃이 정하는 0 값 되돌림·레벨 글자색 적용, 설정 버튼 연결은 씬), 설정 팝업(TMP 자동 값 26개 되돌림), 업그레이드 노트(사용자가 씬에서 고친 4개 → 프리팹), 던전·뽑기 `Viewport.ScaleToWidth` → 프리팹. **④ 최적화**: 글자 터치 끔 108개(버튼 대상·입력칸 제외), 터치 받는 부모가 다 덮는 꾸밈 그림 터치 끔 213개, 광고 보기 창 보상 줄 ContentSizeFitter 2개 → 부모 레이아웃이 크기 결정. 확인: Main·세계수에서 화면에 보이는 모든 버튼 가운데를 눌러 그 버튼이 받는지(가려진 홈 HUD 버튼 3개 외 전부 정상) |
| 11-05 | 20차-보완3 | 뽑기 광고 버튼 끝에 본 횟수/하루 횟수(안 봤으면 "0/1", 다 보면 "1/1"+회색, 하루 횟수는 뽑기상품 시트 `dailyLimit` — 지금 1). 글자 잘림 방지: 자동 축소 최소 40 → 16, 넘침 허용, 글자 자리를 잎 바로 안쪽까지 |
| 11-05 | 20차-보완2 | 뽑기: 균사석 아이콘 56 → 80. 광고 소환 버튼을 한 줄 "광고 보고 11회 소환"으로(앞 "광고 보고"는 예전 아래 줄 색 `#FFF2BF`, 뒤는 흰색, 아래 줄 숨김, 줄바꿈 없이 자동 축소). 다 보면 "내일 다시"로 바꾸지 않고 이름 그대로 버튼만 회색(하루 횟수가 2 이상인 표면 남은 횟수만 작게 붙음) |
| 11-05 | 20차-보완 | 꼬마 시트 버섯 부탁 줄을 정보 칸 폭에 맞춤: 칸을 0.567배로 줄여 쓰던 것 → 실제 크기(높이 266)로 바꾸고 가로는 줄 폭을 나눠 늘어남(`Requests` HorizontalLayoutGroup 폭 채우기 + 칸마다 `LayoutElement` flexible). 그림은 비율 유지, 늘어나는 건 제목·그림 칸·막대·버튼(꼬마 그림 오른쪽~끝)뿐. 뽑기 카드(`GachaCardView` 상수): 경험치 줄을 제목 띠와 같은 줄(위 162), 광고 버튼 60 위로(82), 균사석 소환 버튼 높이 150 → 210(가운데 −80). 광고 버튼 잎은 글자 옆이 아니라 버튼 좌우 끝·세로 가운데(56, 안쪽 26) |
| 11-05 | 20차 | **꼬마 시트 버섯 부탁 칸**: 주간 의뢰 카드처럼 — 위 꼬마 이름, 가운데 진한 베이지 칸(`IconBox`) 안에 부탁 버섯 그림·"○○ n개 주세요"·보상, 아래 왼쪽 부탁한 꼬마 그림(`KidIcon`), 오른쪽 버튼(늘 보임: 들어줄 수 있으면 초록 "들어주기", 아니면 회색 "진행 중", 들어줬으면 회색 "완료"), 맨 아래 막대. 새 `KidRequestView`(`QuestCardView` + `kidIcon`). 꼬마 정보: 단계를 이름 줄에서 빼 레벨 칸으로 "Lv.3 [2단계]"(별 잘림 방지). **의뢰 시트**: 일일 제목(`DailyTitle`)을 주간 제목과 같게(글자 60·좌우 잎, 남은 시간 줄은 원래 공용), 사용자가 고친 주간 `Quest_1` 배치(`InfoBox`·`Desc`·`Reward`·`Claim`)를 `Quest_2·3`에 그대로. **토스트**: `Common/Toast` 기준점 화면 가운데 → 위에서 1/4(앵커 0.75), 모든 토스트 공용. **던전**: 제목 "던전 n"(`Panel (1)`)을 카드 왼쪽 위 모서리에 붙이고 왼쪽 정렬, 사용자가 고친 첫 카드(입장 버튼 잎·제목 바탕 없음·남은 횟수 글자 위치)를 아래 카드 4개에 일괄. **뽑기**: 광고 소환 버튼 아래 줄 좌우 잎(던전 입장 버튼과 같은 그림). **누름 표시**: 새 `UI/ButtonPressShade.cs` — 누르는 동안 버튼 그림을 0.78 회색으로(ColorTint 버튼 기본 눌림과 같은 값, `CanvasRenderer` 곱하기 색이라 리소스 그림 색은 그대로). 그림 바꾸기(회색 비활성 그림)·없음 버튼 146개는 눌림 표시가 없었다 → 실행 시 0.5초마다 새 버튼을 찾아 자동으로 붙여 프리팹·코드 수정 없이 팝업·코드로 만든 버튼까지 적용(ColorTint 버튼은 건드리지 않음). `Main.unity` 변경(업그레이드 노트 위치·제목 50)은 사용자가 고친 것 |
| 11-05 | 19차 | **세계수 꼬마 시트를 편성 화면처럼 새로**: `KidPopup/Sheet/Content/KidScreen` + 새 스크립트 `WorldTree/UI/TreeKidScreen.cs`(값 채우기·버튼 연결)·`KidCellView.cs`(보유 꼬마 칸). 위에서부터 버섯 부탁 4칸(홈 일일 의뢰 카드 `QuestCardView`를 236×266로 줄여 가로 4칸, 부탁이 적으면 `Request_n`째 꺼서 가운데로 모임) → 주간 상자 버튼(`Btn_Yellow`, 못 받으면 회색) → 꼬마 정보(편성 `Info` 복사: 이름 "[n단계] 이름 ★", 호감도 레벨, 특기, 진화 조건 줄 `Evolve`, 버튼 선물(초록)·진화(하늘)) → 구분선 → "보유 꼬마" 목록(칸 누르면 위 정보가 바뀜). 칸: 편성 표시·잠금 삭제, 레벨 왼쪽 위, 그 옆 `Badges`(세로 묶음) 안 `GiftBadge`(`In_Color_Yellow` "선물 가능")·`EvolveBadge`(`In_Color_Blue` "진화 가능") — 켜진 것만 위에서부터 1·2 자리. 펫·무기·방어구·자동 편성·일괄 합성 버튼 없음. 시트 높이 1750. 예전 `TreeKidPanel`(Content에 붙은 것)은 **끄기만** 함(코드·견본 `Templates` 그대로, 다시 켜면 예전 화면), 예전 `Placeholder`("준비 중입니다")도 끔. 기존 코드 수정 없음(새 파일 2개) |
| 11-05 | 18차-보완 | 세계수 시트 5개 내용 위 여백 170 → 130(`Sheet/Content` 위 offset). 의뢰 시트 주간 카드(`Quest_1~3`)를 일일 카드와 같은 조각(`Title`·`IconBox/Icon`·`Desc`·`Reward`·`Claim`·`Bar/Text`)으로, 배치는 가로(그림 칸 왼쪽·설명·보상·막대 가운데·버튼 오른쪽). 의뢰인 이름 줄(`NpcName`)은 일일 카드에 없어 뺐다(그림으로 구분). 이어서: 그림 칸 바탕 끔·캐릭터 크게, 제목 48, 설명·보상 뒤 진한 베이지 칸 `InfoBox`(`In_Color_Beige2`), 오른쪽 버튼 늘 보임(`Claim`, 못 받으면 회색 "진행 중", 받을 수 있으면 초록 — `TreeQuestPanel`이 주간 버튼에 `GreyWhenDisabled`, 버튼 숨김은 일일만) |
| 11-05 | 18차-정정 | 방향 정정: 홈 일일 의뢰(`QuestScreen`) 카드는 18차 전으로 되돌리고, **세계수 의뢰 시트의 일일 카드(`Daily_1~6`)를 홈 일일 의뢰 카드와 같게**(`Title`·`IconBox/Icon`·`Desc`·`Reward`·`Claim`·`Bar/Text`, 칸 416×470). `TreeQuestPanel`이 이 이름도 찾고(`Reward`가 있으면 경험치는 보상 줄, 진행 중이면 버튼 숨김, 받으면 바탕 없이 "완료") |
| 11-05 | 18차 | 세계수 꼬마·꾸미기·공방·의뢰 시트 틀을 성장과 같게(탭 −110~30, 제목 −97~−15 가운데, 속지 위 −60, 탭 아이콘 삭제). **꼬마·꾸미기·공방 내용도 견본으로**(`Sheet/Content/Templates`): 꼬마 `Header`·`Empty`·`RequestCard`·`WeekCard`·`KidCard`, 꾸미기 `Tabs`·`ZoneTab`(고른 자리 `Btn_Yellow`)·`Info`·`Empty`·`DecoCard`, 공방 `Header`·`SlotEmpty`·`CraftCard`·`RecipeCard` — 견본 도우미(`HasTemplates`·`Use`·`SetText`·`SetIcon`·`SetButton`·`SetBar`·`CardOff`)를 `TreePanel`로 옮겨 네 시트가 같이 씀, 견본이 없으면 각 시트의 `BuildByCode`. 광고 보기 창 여백을 방치 정산과 같게(틀 880×820, 속지 위 65·좌우 34·아래 195, 버튼 320×130을 속지 아래 우드 위 38). 편성 자동 편성 = 갈색 글자, 일괄 합성 = 흰 글자+갈색 외곽선. 업그레이드 노트 탭에 "업그레이드" 제목(탭 34 위로, 씬 덮어쓰기도 같이). ~~일일 의뢰 카드를 세계수 일일 카드처럼~~ → 18차-정정에서 반대로 되돌림 |
| 11-05 | 17차 | **코드 화면을 고칠 수 있게 프리팹으로**. 광고 보기 창: `Prefabs/Mobile/UI/Main/Resources/AdBoxPopup.prefab` + 새 `Ads/AdBoxView.cs`(값 칸·버튼 연결), `AdBoxPopup` 생성자는 프리팹이 있으면 그것을 띄우고 없으면 예전 코드. 세계수 성장 시트: `GrowPopup/Sheet/Content/Templates`(꺼진 묶음) 안 견본 8개 `CurrentCard`·`RingCard`·`TributeCard`·`EvolveCard`·`Header`·`BlessingRow`·`StoreEmpty`·`StoreRow` — `TreeGrowPanel.Build`가 견본을 복사해 값만 채움(자식 이름을 지켜야 값이 들어감, 하나라도 없으면 예전 코드 `BuildByCode`). 버튼 색은 견본 그림이 "누를 수 있을 때" 색, 누를 수 없으면 회색. 뽑기 결과 칸 바탕 배율 3 |
| 11-05 | 16차 | 코드로 그리는 화면 3개 손질. **뽑기 결과**(`GachaResultPopup`): 칸 바탕 `Btn_Beige` → `UI_PP_In_InnerLine2`(배율 3), "NEW!" 흰 글자 + 빨간 외곽선(새 재질 `Resources/UI/Fonts/LINESeedKR-Bd Wide Outline Red`) 왼쪽 위 15° 기울임, 등급·이름은 칸 아래 흰 글자 + 갈색 외곽선(그림은 그만큼 위로), 닫기 `Btn_Red`. **광고 보기 창**(`AdBoxPopup`): 우드 틀 880×1060 + 노란 제목 탭(위로 40) + 속지 `Out_Paper1`, 상자 그림 → `UI/Icon/advertisement`(홈의 날아오는 상자는 그대로), 골드·균사석은 아이콘+값 쌍을 가로 묶음으로 가운데. **세계수 성장 시트**: 탭 아이콘 지우고 제목 가운데(의뢰 시트와 같게), 카드 바탕 `In_Color_Beige1`(`TreeUi.Card`에 바탕 그림 인자 추가, 다른 시트는 그대로), 나이테·공물·진화 카드 왼쪽 아이콘(`level_up`·`gift`·다음 단계 나무), 진화 버튼 하늘색(`TreeUi.ButtonBlue` → `UI_Btn_Sky`, 못 하면 회색) |
| 11-05 | 15차-보완7 | 편성 화면 일괄 합성·자동 편성: 버튼 120%(231×84 → 277×101), 글자 150%(32 → 48, 34 → 51, 자동 편성 흰 글자는 6px 재질 L). 가운데 정렬 유지, 늘어난 높이(17)만큼 구분선·"보유 꼬마" 제목·목록을 아래로 |
| 11-05 | 15차-보완6 | 편성 화면 다시: **속지 한 장**(`Body/Page` 끝까지)으로 되돌리고 `ListBook`(둘째 속지·노란 탭) 삭제. 일괄 합성 줄 아래 구분선 `Body/Line`(`UI_Sticker_Line_Beige1`, 위 −725), 그 아래 "보유 꼬마" 제목 `Body/ListTitle`(태그 없이 글자 44, 위 −742). 일괄 합성·자동 편성은 가운데 정렬·높이 70 → 84. 목록 `Scroll` 위 −812, `Viewport` 위 여백 50(예전 칩 자리) → 0 |
| 11-05 | 15차-보완5 | 편성 화면 판 나누기: 뒷판 우드(`Book`, `Side02`)는 하나, 속지만 둘 — 첫 속지(`Body/Page`)는 일괄 합성 줄 아래(Body 위 −700)에서 끝나고, 30 띄워 `Body/ListBook`(이미지 없는 묶음) 안에 노란 탭 "보유 꼬마" + 둘째 속지(`Side01`, 위 60). 목록 `Scroll`은 둘째 속지 안(위 −804). "편성" 탭·첫 속지를 `Body` 안으로 옮겨 iPad에서도 두 탭 크기가 같다(`ScaleToWidth`). 제목이 탭으로 가서 "보유 꼬마" 칩(`ListChip`)은 끔(삭제 안 함). 자동 편성(`Auto`)을 `SideButtons`에서 빼 일괄 합성 오른쪽(같은 줄·같은 크기 231×70)으로, 위 칸 줄 `SlotRow` 가운데 정렬, 펫·무기·방어구는 `SideButtons`에 HorizontalLayoutGroup(가운데, 270×80, 간격 16). 버튼 연결(TabGroup·`autoButton`) 그대로 확인 |
| 11-05 | 15차-보완4 | 사용자가 추가한 연두색 잎 `UI_Sticker_Leaf_L/R.png`(Sprite로 가져옴, 기존 잎과 같은 설정)으로 **모든 잎 교체**: 설정 팝업 구역 제목·편성 보유 꼬마 칸(+`CollectPopup` 안 설정 창). 편성 위쪽 칸(`SlotTemplate`): 레벨 배경 띠 삭제, 레벨 흰 글자 38(자동 축소 26~)·4px 외곽선으로 강조 |
| 11-05 | 15차-보완3 | 편성 보유 꼬마 칸(`FormationScreen/…/CellTemplate`): 위 줄 `TopRow`(HorizontalLayoutGroup) = 레벨(배경 띠 `LevelBand` 삭제, 흰 글자 34·4px 외곽선) + `Badge`(편성) — 편성 안 된 꼬마는 레벨만 가운데. 아래 줄 `NameRow` = 잎 L + 이름(배경 없음) + 잎 R(40×40). 그림은 두 줄 사이. 코드 수정 없음(`FormationCellView` 연결 그대로) |
| 11-05 | 15차-보완2 | 수확 완료 창(`ResultPopup`): "한 번 더하기"(`RetryButton`)와 "나가기"(`MainButton`) 자리 맞바꿈 — 나가기 왼쪽(−195), 한 번 더하기 오른쪽(195). 코드는 필드로 찾아 위치와 무관 |
| 11-05 | 15차-보완 | 설정 팝업 간격 통일: **모든 구역·버튼 사이 30**(창 위 → UID 40). 사용자가 던전 변형에서 줄인 구역 높이(음향·알림 330, 제목 −30, BGM −100·SFX −200·알림 −110, 언어 버튼 테두리 1.2)를 기본 프리팹으로 옮기고 변형의 `Stack` 아래 덮어쓰기 201개 제거. 창 높이 로비 1274 · 던전 1404(로비 줄 100 + 간격 30만큼 큼). 구역 높이를 바꾸면 창 높이(`Body` Height)도 같은 만큼 바꿔야 아래 간격이 30으로 유지된다 |
| 11-05 | 15차 | 설정 팝업 구역 제목: 음향·알림 구역을 위로 70 늘려 제목을 안에 감쌈(줄은 그만큼 아래로), 제목 바탕 그림 삭제, 글자 34 → 44(흰 글자 4px 외곽선), 좌우 잎 `UI_Sticker_Leaf_L/R_Green` 52×52 — 제목 오브젝트에 HorizontalLayoutGroup(간격 14)이라 글자 길이가 바뀌어도 잎이 붙어 따라온다. 제목이 밖으로 안 튀어나와 `Stack` 간격 44 → 30, 창 높이 1258 → 1358(던전 변형 1388 → 1488). 사용자가 던전 변형에서 고친 UID `In_Color_Beige1`·언어 버튼 `Btn_Sky`도 기본 프리팹으로 옮김(언어 글자 흰색·굵게·2px 외곽선), 변형의 그림 덮어쓰기 4개(지워진 제목 바탕 노란색 2개 포함) 제거 |
| 11-05 | 14차 | 설정 팝업 게이지·토글(사용자가 던전 설정 창 BGM 줄에서 고친 것을 **기본 `OptionPopup`으로 옮겨** 모든 줄에): 슬라이더 채움 = 바 크기(`Fill Area` 여백 6 → 0), 손잡이 `UI_PP_Bar_Stage_Off`(갈색 동그라미)·BGM/SFX 둘 다, 토글 7개(BGM·SFX·진동·알림 4) 켜짐 글자 (10,−4) 28·손잡이 50 (−13), 꺼짐 쪽 대칭 (−10,−4)·(13), 구역 제목 칩 글자 −1.9, `Stack` 위 −40. 옵션 이름 8개(BGM·SFX·언어 설정·진동·알림 4) Rg → Bd 굵게. 던전 변형(`InGameOptionPopup`)의 `Stack` 아래 덮어쓰기 207개 제거(기본과 같은 값·레이아웃 값뿐이었음 → 이제 기본 프리팹만 고치면 두 창이 같이 바뀐다) |
| 11-05 | 13차-보완 | **설정 팝업 계층 정리(손으로 옮기기 쉽게)**: 구역 제목 칩·줄이 구역과 형제로 따로 고정돼 있어 구역을 옮겨도 안 따라오던 것 → `Window/Stack`(VerticalLayoutGroup, 간격 44, 위 −30) 아래 `UID`·`SectionSound`(+`SoundTitle`·`SoundBgm`·`SoundSfx`)·`SectionLang`(+`LangVibe`)·`SectionNotify`(+`NotifyTitle`·`Notify`)로 묶음. **순서는 Hierarchy에서 `Stack` 안 순서를 끌어 바꾸면 된다**(지금: UID → 음향 → 언어 → 알림). 간격은 `Stack`의 Spacing 하나, 구역 높이는 각 구역 Height. 칩은 구역 위 끝에 +28로 걸친다. 계속하기/게임 종료·`Notice`는 창 아래 기준이라 그대로. 던전 변형의 로비 버튼 줄도 그대로 |
| 11-05 | 13차 | 일일 의뢰: 받은 의뢰는 회색 버튼 바탕 없이 "완료" 글자만(`QuestScreenView`: `button.image.enabled = can`). **설정 팝업 재구성**(사용자가 고친 장비 창 비율 기준): 창 960×1258·틀이 창을 가득(예전 틀 968 > 창 952로 넘쳤음), 속지 여백 34·아래 50·위 70, 구역 좌우 26(폭 840)·구역 사이 44~60·구역 제목 칩은 구역 위 끝에 걸침, 알림 칸 370×86 간격 40·24, UID 560×64, 계속하기/게임 종료 360×110 간격 40, 화면 맞춤 위 200·아래 220·좌우 70. `InGameOptionPopup` 안 설정 창의 예전 `Window` 크기 덮어쓰기 제거. S26·Z플립7·iPad 확인. 던전 설정 창(`InGameOptionPopup`)에만 있는 "로비로 돌아가기"(`LobbyRow/WorldTreeButton`, `InGameOption.lobbyButton`)가 예전 자리(위 −735)에 남아 알림 칸을 덮던 것 → 이 변형만 창 높이 +130(1388), 로비 줄을 아래 기준 170(UID와 계속하기/게임 종료 사이)·버튼 560×100, 글자 Rg 50 → Bd 38 |
| 11-05 | 12차 | 사용자가 고친 방어구 창(`Amor`)을 무기·펫 창에 똑같이: 탭 글자·속지 위치, 정보 칸(위 −100·높이 570·폭 −120, 아이콘·이름 위치, 막대·효과 줄 720, 버튼 300·합성 `Btn_Sky`), 목록 판 `In_Color_Beige1`·칸 `In_Color_Beige2`, 아래 소환 `Btn_Red`·일괄 합성 `Btn_Sky` (속성 94개 경로별 복사, 창 안 연결 23개는 각자 오브젝트로 되돌림 확인). `EquipmentPanel`: 보통 칸 그림을 코드 상수(`Beige1`) 대신 **프리팹 칸 그림**으로 (고른 칸 `InnerLine1`·장착 칸 `Green`만 바꿔 낌) — 프리팹에서 바꾼 칸 그림이 실행 중에도 남는다. 무기 창 루트가 켜진 채 저장돼 있던 것 끔(다른 두 창과 같게) |
| 11-05 | 11차 | UI 프리팹 폴더 정리(3-0장): 화면별 `UI/Common`·`UI/Main`(+`Formation`·`Dungeon`·`Gacha`)·`UI/WorldTree`·`UI/InGame`로 20개 이동, 빈 폴더 `Main`·`Gacha_Panel`·`WorldTree` 삭제. GUID 유지, 끊긴 연결 0, 실행 오류 0. 하단 띠·방치 정산 프리팹도 `UI/Common/Resources`·`UI/Main/Resources`로(읽는 경로 `PopupBottomBand.cs`·`IdleReportPopup.cs` 한 줄씩), `Assets/Resources/UI/Prefabs` 삭제 |
| 11-05 | 10차-보완 | **흰 글자는 무조건 외곽선**: 프리팹·씬의 흰 글자 79개(설정 ON/OFF·구역 칩, 장비 창 버튼, 던전 칩, 뽑기 카드 제목, 도감 탭, 편성, Start 버전 글자)에 크기별 재질, 코드로 만드는 흰 글자는 `OutlineText.ApplyIfWhite`(광고 상자 창·예전 의뢰 창·뽑기 카드 레벨·막대 숫자·뽑기 제목·세계수 시트 버튼·절전 모드·전투 튜토리얼·장비/방치 정산 예전 화면). 전투 숫자(`IdleBattle`)는 원래 외곽선이 있어 그대로. **글자 조각남 원인 2가지 수정**: ① `GachaPanel.CreateTitle`이 `outlineWidth`로 예전 폰트 재질 복사본을 만든 뒤 새 폰트로 바뀌어 다른 아틀라스를 읽음 → `outlineWidth` 줄 삭제 ② 동적 폰트 되돌린 뒤 에디터의 낡은 글자 → `FontReimportRefresh`. 실행 중 Main·WorldTree 글자 1,700여 개에서 폰트·재질 불일치 0, 외곽선 없는 흰 글자 0 확인. 도감 탭: `DexScreenView`가 탭 그림을 `tabOn/tabOff`(예전 `Btn_Green/Beige`)로 덮던 것 → 프리팹 값을 사용자 그림(`In_Color_Green/Grey`)으로, 탭 글자색을 코드에서 바꾸지 않음 |
| 11-05 | 10차 | 흰 글자 외곽선을 바깥 외곽선 2·4·6px로(0-1장): 여백 넓힌 새 폰트 `LINESeedKR-Bd SDF Wide`(샘플 64·여백 20·2048 다중 아틀라스)와 재질 S·M·L, 예전 `LINESeedKR-Bd SDF Outline(Thick)` 재질을 쓰던 글자 전부(프리팹 13개·Main 하단 바·WorldTree 하단 바·옆 버튼) 교체, `GachaCardView.Outline` → `OutlineText.Apply`. 상단 바 세계수 레벨 흰 글자 + 갈색 외곽선. 예전 외곽선 재질은 다른 브랜치용으로 남김(지우지 않음) |
| 11-05 | 9차 | 방치 정산 팝업을 규격 프리팹으로(`Resources/UI/Prefabs/IdleReportPopup` + 새 `IdleReportView`): 갈색 틀·노란 탭·속지, 골드·균사석 줄(아이콘 + 굵은 값, 앱 밖 보상 작은 글자), 처치·스테이지 줄, 2배 적용 표시, 확인 초록·광고 하늘색. 설정 팝업: 토글·손잡이에 입혀 있던 색(회색 `B8B8B8`·주황 `FFAD40`·`FFF7E6`)을 빼고 그림 그대로, 구역 사이 여백(음향·언어·알림 구역 간격 50, 구역 제목 칩이 구역 위 끝에 걸치게), 알림 칸 간격, UID를 창 안(버튼 위)으로, 버튼 여백. S26·iPad 확인 |
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

### 6-1. 21차 검토에서 찾았지만 손대지 않은 것

| 무엇 | 내용 | 권장 |
|---|---|---|
| **캔버스 하나에 UI 전부** (화면 갱신 비용) | Unity UI는 캔버스 단위로 그림·글자를 묶어 그린다. 캔버스 안에서 하나라도 바뀌면(숫자·위치·색) 그 캔버스 전체를 다시 묶는다. 전투 화면(`IdleBattle`의 칸·적 영역·효과)은 이미 하위 캔버스로 나뉘어 있다(`AddSubCanvas`). 나머지 Main UI(그림·글자 약 1,900개)는 한 캔버스라, 위 바 골드·HUD 체력/전투력 글자처럼 자주 바뀌는 글자 하나 때문에 안 바뀐 하단 바·노트·버튼까지 매번 같이 다시 묶인다. 꺼진 팝업은 비용 없음. 고장은 아니고 요즘 폰은 대부분 체감 없음 — 저사양 폰 발열·배터리에서 손해 | **먼저 프로파일러로 잰다**(`Canvas.BuildBatch`·`Canvas.SendWillRenderCanvases`). 크면 자주 바뀌는 곳(위 바 골드·균사석, 전투 HUD)에만 하위 `Canvas`를 달아 따로 묶이게 한다(Unity 권장 방식). 나눌 때 주의: ① 하위 캔버스는 그리는 순서를 따로 가질 수 있다 — `overrideSorting`을 켜지 말고 부모 순서를 따르게(팝업 위로 위 바가 튀어나오지 않게, 팝업 순서는 `PopupLayer`가 캔버스 순서로 관리) ② 버튼이 있는 하위 캔버스엔 `GraphicRaycaster`를 같이 붙인다(빠지면 설정 버튼 등이 안 눌림) ③ 나눈 뒤 팝업·광고 상자·토스트 순서와 버튼 눌림을 S26·Z플립7·iPad에서 확인 |
| 레이아웃 그룹 아래 ContentSizeFitter | 설정 팝업 구역 제목 글자(3개 프리팹), 편성·꼬마 보유 칸 이름 줄, 광고 보기 창 보상 글자 — Unity 경고가 날 수 있는 구조지만 동작은 정상 | 형제 오브젝트 폭이 그룹 제어로 바뀌지 않는지 보고 하나씩 정리 |
| 둥근 마스크(Mask) 9개 | 던전·뽑기 카드 그림의 둥근 모서리용(`ArtMask`) — 스텐실이라 RectMask2D보다 무겁지만 모양에 필요 | 유지 |
| `CollectPopup` | 예전 도감, 어디서도 안 씀(새 `DexScreen`과 중복). HANDOFF에 "다시 쓸 예정"이라 남김 | 다시 안 쓰기로 하면 삭제 |
| 코드 화면 예비 경로 | `DailyQuestPopup`·`AdBoxPopup`·`IdleReportPopup`의 코드 화면, 세계수 시트 `BuildByCode` — 프리팹·견본이 있으면 안 쓰이는 예비 길 | 다른 브랜치와 합친 뒤 프리팹이 확실하면 정리 |
| 세계수 `Basket` 버튼 | 위치 값이 NaN이라 화면에 안 보이고 안 눌림(원래부터, 지금은 수확 보따리 `TreeBagButton`이 대신) | 쓰지 않으면 삭제 |
