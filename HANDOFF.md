# HANDOFF — MoreMustMush 모바일 포팅

> 작성 2026-10-22 · 기준 커밋 `7700343`  
> itch.io에 PC 웹빌드로 출시한 **MoreMustMush**를 모바일(세로 1080×1920)로 옮기는 Unity 프로젝트입니다. 이 문서는 2026-10-22 하루 동안 한 작업과 다음 작업자가 알아야 할 것을 정리합니다.

---

## 1. 한눈에 보기

| 항목 | 상태 |
|---|---|
| InGame 핵심 플레이 (수확기·바·버섯·타이머·돈·콤보) | ✅ 동작 |
| 배속 1/2/4 | ✅ 동작 |
| 결과 팝업 / 인게임 옵션 팝업 | ✅ 동작 |
| WorldTree 버튼 6개 → 팝업·토스트 | ✅ 연결 (내용은 "준비 중") |
| 번 돈을 지갑에 저장 | ❌ 미구현 |
| 바구니 실제 지급 | ❌ 미구현 (문구만) |
| 사운드 (BGM/SFX) | ❌ 슬라이더 값만 저장 |
| 실기기 터치 테스트 | ⚠️ 안 함 (에디터에서 코드로 입력을 흉내 내 검증) |
| 아트 | ⚠️ 수확기·버섯·바·아이콘은 임시 리소스 |

**환경:** Unity 6000.3.19f1 · URP · Input System (`InputSystemUIInputModule`) · TextMesh Pro · Unity MCP (`.mcp.json`, `http://localhost:8080/mcp`)  
**저장소:** `https://github.com/LKH1134/MMM_Mobile` (`main`)  
**커밋 규칙:** `[태그] YYMMDD 설명` (예: `[ADD] 261022 모바일 인게임 구현`)

---

## 2. 오늘 커밋

| 커밋 | 내용 |
|---|---|
| `d8f6964` | Initial commit (.gitignore, .gitattributes) |
| `f705ada` | `[ADD] 261022 모바일 인게임 구현`: InGame 전체 메카닉, 풀링, 결과/옵션 팝업, 임시 아트 |
| `7700343` | `[ADD] 261022 인게임 배속 및 세계수 팝업 구현`: 배속, 레터박스 제거, WorldTree 팝업·토스트 |

`LiberationSans SDF - Fallback.asset`은 TextMesh Pro가 실행 중에 스스로 고치는 파일이라 계속 "수정됨"으로 뜹니다. 동작과는 무관합니다.

---

## 3. InGame 씬

### 3.1 게임 규칙 (현재 구현)

1. 판이 시작되면 수확기(낫)가 바(통나무) 위에서 대기합니다. **타이머는 멈춰 있습니다.**
2. 화면을 눌러 끌었다 놓으면 **끈 반대 방향**으로 수확기를 던집니다. 끄는 동안 방향을 화살표로 보여 줍니다. 40px보다 짧게 끌면 취소됩니다.
3. **던지는 순간부터 15초 타이머가 흐릅니다.** 던지기는 한 판에 한 번뿐입니다.
4. 수확기는 **감속 없이 같은 속도(2200px/초)** 로 날아가며, 상하좌우 벽에서 반사각으로 튕깁니다.
5. 던진 뒤에는 **화면 어디를 눌러도** 좌우로 끄는 만큼 바가 움직입니다 (한 손가락만 받습니다).
6. 바는 **윗면만 판정**합니다. 위에서 내려온 수확기만 튕기고, 옆·아래에서 오면 뚫고 지나갑니다 (갇힘 방지).
7. 버섯
   - 단일: 체력 1, 한 번 맞으면 캐지고 10원.
   - 군락: 체력 5, 다 닳을 때까지 맞아야 캐지고 80원. 다친 뒤에는 체력 바가 보입니다.
   - 한 번에 캐지는 버섯은 수확기가 뚫고 지나가고, 체력이 남는 버섯에 맞으면 튕깁니다.
   - 캐진 버섯은 0.8초 뒤 빈 자리에 새로 돋습니다. 필드에 12개를 유지하고, 군락은 최대 3개입니다.
8. **콤보** = 마지막으로 바에 닿은 뒤 캔 버섯 수 (바에 닿으면 0으로 돌아감).
9. 게임 오버는 **시간 종료뿐**입니다. 끝나면 필드·입력이 멈추고 0.6초 뒤 결과 팝업이 열립니다.
10. 거의 수평으로 날면 좌우 벽만 오가며 갇히므로, 세로 속도를 최소 25%로 보정합니다 (`minVertical`).

### 3.2 씬 구조

```
Canvas  (StageHud, HarvestGame)
├─ BG                 어두운 바탕
├─ StageImage         화면 전체로 늘어남 — 레터박스를 덮음 (InGameArea 마스크 밖)
└─ InGameArea         1080×1520, RectMask2D. Image는 꺼 둠
   ├─ Field           1080×1220 (HarvestField, FieldInput, 투명 터치 Image)
   │  ├─ Mushrooms    버섯 풀
   │  ├─ Bar          400×64, y=-460
   │  ├─ ReadyRing    던지기 전 대기 원
   │  ├─ Harvesters   수확기 풀
   │  └─ Arrow / Shaft / Head
   ├─ UIPanel         상단 HUD (Option, Pause, 타이머, 돈)
   ├─ Combo
   └─ Speed           배속 버튼 (SpdIcon, SpdText)
```

### 3.3 스크립트 (`Assets/Scripts/Mobile/InGame/`)

| 파일 | 역할 |
|---|---|
| `PrefabPool.cs` | 프리팹별 오브젝트 풀 + `IPoolable` (OnSpawned/OnDespawned) |
| `Harvester.cs` | 수확기 상태 (위치·속도·반지름·콤보), 회전 연출 |
| `Mushroom.cs` | 체력·보상·반지름, 맞을 때 출렁임, 체력 바 |
| `Bar.cs` | 바 사각형 → 필드 좌표, `MoveBy()` (필드 안으로 제한) |
| `HarvestField.cs` | 이동·충돌·재생성 전체. 테스트용으로 `Step(dt)`를 직접 부를 수 있음 |
| `FieldInput.cs` | 던지기 전 = 조준·발사, 던진 뒤 = 바 이동 |
| `HarvestGame.cs` | 돈·콤보 → HUD, 첫 투척 때 타이머 시작, 시간 종료 → 결과 팝업 |
| `ResultPopup.cs` | 번 돈(카운트업)·버섯 수, 다시 하기 / 세계수로 |
| `InGameOption.cs` | BGM/SFX 슬라이더 (PlayerPrefs 저장만), 세계수로 돌아가기 |

공용 (`Assets/Scripts/Mobile/`):

| 파일 | 오늘 바뀐 점 |
|---|---|
| `StageHud.cs` | `startOnAwake` + `StartTimer()` (첫 투척부터 시간), 배속 (`speeds`, `NextSpeed()`) |
| `UiPopup.cs` / `PopupSlot.cs` | 변경 없음. 모든 팝업이 이 구조를 씀 |

> **충돌 계산은 Physics2D를 쓰지 않습니다.** 화면이 전부 UI 캔버스 기반이라, Field 로컬 좌표(캔버스 픽셀)에서 원-원 / 원-선분 충돌을 직접 계산합니다. 빠른 수확기가 얇은 바를 지나치지 않도록 한 프레임을 반지름 절반 단위로 쪼개 계산합니다(최대 32번).

### 3.4 조정 가능한 값 (인스펙터)

| 위치 | 값 |
|---|---|
| `StageHud` | `duration` 15, `startOnAwake` false, `speeds` 1/2/4 (아이콘 포함) |
| `HarvestField` | `speed` 2200, `minVertical` 0.25, `pierce` true, `mushroomCount` 12, `respawnDelay` 0.8, `kinds` (단일 비중 4 / 군락 비중 1·최대 3), 스폰 여백 (`spawnTop` 30, `spawnBottom` 380, `spawnSide` 20, `spawnGap` 12) |
| `FieldInput` | `minDrag` 40, `maxDrag` 450, `barSensitivity` 1, 화살표 길이 130–380 |
| `HarvestGame` | `resultDelay` 0.6 |
| `Harvester.prefab` | `radius` 45, `damage` 1, `spinSpeed` 900 |
| `Mushroom_Single.prefab` | `maxHp` 1, `reward` 10, `radius` 58 |
| `Mushroom_Cluster.prefab` | `maxHp` 5, `reward` 80, `radius` 112 |

### 3.5 배속

- Speed 버튼을 누를 때마다 1 → 2 → 4 → 1배속 순서로 바뀝니다. SpdIcon과 SpdText("N배속")가 함께 바뀝니다.
- `Time.timeScale`로 거는 방식이라 **15초 타이머도 같이 빨라집니다** (4배속이면 실제 약 4초). 수확기만 빠르게 하는 방식이 맞는지 아직 확정되지 않았습니다.
- 일시정지나 옵션 팝업이 열리면 0, 닫히면 고른 배속으로 돌아갑니다.
- 고른 배속은 PlayerPrefs `InGame.SpeedIndex`에 저장돼 다음 판에도 이어집니다.

### 3.6 분신·소환 확장 지점 (인크리멘탈 요소 대비)

- `HarvestField.SpawnHarvester(position, prefab)` → `Launch(harvester, direction)`: 프리팹별 풀에서 꺼냅니다. 첫 번째 수확기가 `Main`(플레이어가 던지는 것)입니다.
- `DespawnHarvester(h)`로 풀에 돌려놓습니다.
- 새 버섯 종류는 프리팹을 만들어 `HarvestField.kinds`에 추가하면 됩니다 (비중·최대 수·미리 만들 수).
- 이벤트: `Launched`, `BarHit`, `Harvested(mushroom, harvester)`, `HarvestGame.GameOver(earned)`.

---

## 4. WorldTree 씬

| 버튼 | 동작 | 프리팹 |
|---|---|---|
| OptionIcon | 옵션 팝업 (가운데) | `Prefabs/Mobile/OptionPopup` |
| Grow | 성장 시트 (하단에서 올라옴) | `Prefabs/Mobile/WorldTree/GrowPopup` |
| Kid | 꼬마 시트 | `…/WorldTree/KidPopup` |
| Skin | 꾸미기 시트 | `…/WorldTree/SkinPopup` |
| Workshop | 제작소 시트 | `…/WorldTree/CraftPopup` |
| Basket | 화면 중앙에 "[바구니 골드]골드를 획득했습니다!" 3초 | `Prefabs/Mobile/Toast` |

- `WorldTreeScreen.menus` 배열이 버튼과 팝업을 짝지어 둡니다. 시트를 더 늘릴 때는 이 배열에 항목만 추가하면 됩니다.
- 시트 4개는 SkillTree의 공방(`WorkshopPopup`)을 **복사**해 탭 제목·아이콘만 바꾼 것입니다 (프리팹 변형이 아니므로 원본을 고쳐도 따라 바뀌지 않음). 내용은 "준비 중입니다".
- 제작소는 공방 `WorkshopPopup`과 이름이 겹치지 않게 `CraftPopup`으로 지었습니다.
- 바구니: `WorldTreeScreen.CollectBasket()`이 문구만 띄웁니다. 골드 지급과 바구니 비우기는 여기에 붙이면 됩니다. 값은 `SetBasket(double)`로 넣습니다.
- `Toast.cs`는 공용입니다. 실제 시간으로 세서 일시정지·배속의 영향을 받지 않고, 터치를 막지 않습니다. 보이는 중에 다시 부르면 시간을 처음부터 셉니다.

---

## 5. 프리팹 목록

| 프리팹 | 용도 | 비고 |
|---|---|---|
| `InGame/Harvester` | 수확기 | 풀링 |
| `InGame/Mushroom_Single` / `Mushroom_Cluster` | 버섯 | 풀링 |
| `InGameOptionPopup` | InGame 옵션 | `OptionPopup`의 **변형 프리팹** (WorldTree와 공유하는 원본은 그대로) |
| `ResultPopup` | InGame 결과 | 닫기 버튼·배경 닫기 없음 |
| `WorldTree/Grow·Kid·Skin·CraftPopup` | WorldTree 시트 | BottomSheet |
| `Toast` | 안내 문구 | 공용 |

모든 팝업은 `PopupSlot`으로 들고 있다가 **처음 열 때 만듭니다** (씬에 미리 두지 않음).

---

## 6. 임시 리소스 (교체 예정)

`Assets/Art/Placeholder/InGame/` — 코드로 그린 임시 이미지입니다. **같은 파일 이름으로 덮어쓰면** 프리팹과 씬에 그대로 반영됩니다.

| 파일 | 쓰는 곳 |
|---|---|
| `harvester.png` | Harvester/Visual |
| `mushroom_single.png` / `mushroom_cluster.png` | 버섯 Visual |
| `bar_log.png` | Field/Bar |
| `arrow_head.png` | 조준 화살촉 (몸통은 흰 Image) |
| `ready_ring.png` | 던지기 전 대기 원 |
| `speed_x1/x2/x4.png` | 배속 아이콘 (StageHud `speeds`에 연결) |

---

## 7. 주의할 점

- **폰트 글자 누락:** 폰트 SDF가 전부 Static 아틀라스입니다. 아틀라스에 없는 글자는 □로 나옵니다 (예: Jua에 '돈'이 없어서 결과 팝업 문구를 "이번 수확"으로 바꿈). 새 문구를 넣을 때는 확인하거나 아틀라스를 다시 구워야 합니다. Nunito는 한글이 없습니다.
- **저장 안 된 씬:** 에디터에서 씬을 고친 채 저장하지 않으면, 스크립트나 MCP로 씬을 저장할 때 그 변경까지 같이 저장됩니다.
- **에디터 포커스:** Unity 창이 포커스를 잃으면 플레이 루프가 멈추거나 느려져서, 애니메이션·타이머 검증이 어긋납니다. 토스트가 3초 동안 떴다 사라지는 흐름은 이 때문에 끝까지 확인하지 못했습니다.
- **PlayerPrefs 키:** `InGame.SpeedIndex`, `Option.BgmVolume`, `Option.SfxVolume`
- **화면 비율:** 기준은 1080×1920 (CanvasScaler 가로 맞춤). InGameArea는 1080×1520 고정이고, 남는 위아래는 StageImage가 덮습니다.

---

## 8. 다음 작업 후보

1. 결과의 번 돈을 플레이어 지갑에 저장 (`HarvestGame.GameOver`에 연결) → WorldTree 골드 표시와 연동
2. 바구니 기능 (쌓임 → `SetBasket`, 지급 → `CollectBasket`)
3. 사운드 매니저 + 옵션 볼륨 슬라이더 실제 적용
4. 실기기 터치 테스트 (멀티터치, 드래그 감도 `barSensitivity`)
5. PC판과 규칙 맞추기: 바에 맞은 위치에 따라 반사각이 달라지는지, 배속이 타이머에도 걸리는지
6. 분신·소환 수확기, 버섯 종류 추가 (3.6 확장 지점 사용)
7. 성장·꼬마·꾸미기·제작소 시트 내용
8. 임시 아트 교체
