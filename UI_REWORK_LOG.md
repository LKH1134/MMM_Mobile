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
| `Scripts/Mobile/Quest/DailyQuestPopup.cs` | 그림 경로 상수 블록, `Refresh`의 버튼 그림/글자색 2줄, `BuildUi` 제목 색·닫기 버튼 숨김 + `PopupBottomBand.Attach`, `BuildRow` 줄 바탕·막대 그림, `NewButton` 인자 `Color tint` → `string sprite` | **높음.** 퀘스트 창 레이아웃을 다른 브랜치가 고치면 `BuildUi`·`BuildRow`·`NewButton`에서 충돌 |
| `Scripts/Mobile/Ads/AdBoxPopup.cs` | 그림 경로 상수, 제목 색 → 흰색, 취소/광고 버튼 색 → `UI_Btn_Red`/`UI_Btn_Sky`, `NewButton(… Color tint …)` → `(… string sprite …)` | 중간 (`NewButton` 시그니처) |
| `Scripts/Mobile/Idle/IdleReportPopup.cs` | `NewButton(… Color tint …)` → `(… string spritePath …)`, 확인 `UI_Btn_Green`·2배 `UI_Btn_Sky` | 중간 |
| `Scripts/Mobile/Idle/EquipmentPanel.cs` | 색 상수 3개 → 그림 경로 상수, `BuildMergeBar` 막대 그림, `Refresh` 칸 바탕 그림(고른/장착/보통), 못 얻은 장비 회색 칠 제거 | 중간 (`Refresh` 안 칸 그리기) |
| `Scripts/Mobile/Idle/CharacterDetail.cs` | 색 상수 → 그림 경로 상수, 막대 그림, 스킬 칸 그림(해금 초록/잠금 회색), 못 얻은 꼬마 회색 칠 제거 | 중간 |
| `Scripts/Mobile/UI/GachaCardView.cs` | 재화 버튼 `UI_Btn_Sky`, `CoinColor` 흰색, 막대 색 흰색 | 낮음 |
| `Scripts/Mobile/UI/GachaResultPopup.cs` | 등급 테두리: 등급색 칠 → 등급별 그림 `GradeSprites` | 낮음 |
| `Scripts/Mobile/WorldTree/UI/TreeUi.cs` | 그림 경로 상수 블록, `Card`·`Button`·`Bar`가 색 대신 그림을 고른다 (`ButtonOn/Alt/Off`·`CardOn/Off` 색 값은 어떤 그림을 쓸지 고르는 데만 씀) | 중간 (세계수 UI 공용 도우미) |
| `EquipmentPanel.cs`·`GachaCardView.cs`·`DailyQuestPopup.cs`·`AdBoxPopup.cs`·`IdleReportPopup.cs`·`TreeUi.cs` (추가) | 버튼을 만든 줄 뒤에 `UiSkin.GreyWhenDisabled(…)` 한 줄씩, `EquipmentPanel.SelectedSprite` → `InnerLine1` | 낮음 |
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

- 스크립트 `Scripts/Mobile/UI/Main/`: `MainHudView`, `UpgradeNotebookView`, `UpgradeRowView`, `SafeAreaDock`, `TabIconLift`, `FormationScreenView`, `FormationCellView`, `DexScreenView`, `PopupBottomBand`, `BottomBarLayer`, `FitInRegion`, `ScaleToWidth`, `GridFitWidth`(지금 안 씀), `UiSkin`, `QuestScreenView`, `QuestCardView`, `QuitGameButton` / `Scripts/Mobile/Idle/IdleBattle.Hud.cs`
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

## 4. 합칠 때 주의

1. **폰트 SDF(`LINESeedKR-*.asset`)는 커밋하지 않는다.** 실행할 때마다 동적 아틀라스에 글자가 추가돼 바뀐다. 되돌릴 때는 Unity를 연 채 `git checkout` 후 그 에셋을 **Reimport** 해야 글자가 깨지지 않는다.
2. 씬 충돌은 텍스트로 풀지 말고, 한쪽을 받은 뒤 Unity에서 위 표의 변경을 다시 적용한다.
3. 예전 코드 HUD(`IdleBattle.BuildHud`)·퀘스트/도감 버튼 스크립트·예전 편성 스크립트는 **지웠다**. 이 파일들을 고친 브랜치와 합칠 때는 변경을 새 프리팹·View 스크립트로 옮긴다.

---

## 5. 진행 기록

| 날짜 | 단계 | 내용 |
|---|---|---|
| 11-04 | 1차 | 홈 강화 노트·HUD·하단 바 프리팹화, 리소스 교체 |
| 11-04 | 2차 | 편성(와이어프레임 공방 배치)·도감 새 화면, 코드 그림 경로·색 → 리소스 |
| 11-04 | 5차-화면별 ② | 일일 의뢰 새 화면(`QuestScreen` 프리팹 + `QuestScreenView`·`QuestCardView`, 2열 카드·주간/일일 버튼, HUD 의뢰 버튼이 연결 — 없으면 예전 `DailyQuestPopup`), 설정(갈색 구역 칩 음향·알림 설정, 계속하기/게임 종료 버튼 + `QuitGameButton`, BGM/SFX 줄 정리), 도감 탭 글자색 |
| 11-04 | 5차-화면별 ① | 공통 규격 `UiSkin`. 편성(칸 줄 5칸·버튼 줄·정보 구역·목록 칩), 무기·방어구·펫 창(틀·속지·정보/목록 구역, 프리팹 자체에 리소스 반영 + 씬 덮어쓰기 되돌림), 비활성 버튼 회색 그림(프리팹·코드 팝업 전부) |
| 11-04 | 4차-연결 | 예전 UI를 숨기던 방식 → 코드에서 새 프리팹 연결: IdleBattle 예전 HUD 생성 제거, 퀘스트·도감 버튼은 MainHud, Equit 탭은 새 편성 화면 버튼, 예전 편성 스크립트·프리팹 삭제 |
| 11-04 | 3차-화면 대응 | Stage를 안전 영역 끝까지 늘림, 판·목록은 폰 폭 기준 비율 확대(`ScaleToWidth`, 최대 1.3), 팝업 창은 상단~하단 띠 사이 맞춤(`FitInRegion`), 하단 띠가 부모 확대에도 바닥에 붙게 수정. S26·Z플립7·iPad Pro 13 확인 |
| 11-04 | 3차-기반 | 층 순서 정리(광고 상자 < 하단 바 < 편성 팝업 < 팝업), 팝업 하단 띠(Side03 + X), 회색 리소스 적용 |

---

## 6. 다음에 이어서 할 일 (11-04 중단 지점)

| 순서 | 화면 | 남은 것 |
|---|---|---|
| 1 | 던전 · 뽑기 | 제목·판을 공통 규격(메모 제목·속지)으로, 목록 칸 정리 |
| 2 | 세계수 창들 (성장·꾸미기·꼬마·공방·의뢰) | 아래에서 올라오는 시트 — 플레이에서 열어 목업과 비교 (에디터 백그라운드면 애니메이션이 멈춰 확인 어려움 → 창을 켜 둔 상태에서 직접 확인) |
| 3 | 장비 창 글자 | `EquipmentPanel`이 코드로 만드는 칸 글자(`Lv.5 (7/6)`)·효과 줄이 작다 |
| 4 | 설정 창 | BGM/SFX 줄 수정 후 화면 확인 안 함. 데이터 관리(불러오기·저장) 구역은 기능이 없어 비움 |
| 5 | 일일 의뢰 | 카드 글자 위치 조정 후 화면 확인 안 함. 주간 의뢰는 시스템이 없어 회색 버튼 |
| 6 | 공통 | iPad·Z플립에서 이번 화면들(의뢰·설정) 다시 확인 |
