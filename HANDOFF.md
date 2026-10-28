# HANDOFF — MoreMustMush 모바일 포팅

> 작성 2026-10-26 · 기준 커밋 `e5f377e` (main, 푸시 안 함)
> itch.io에 PC 웹빌드로 출시한 **MoreMustMush**를 모바일(세로 1080×1920)로 옮기는 Unity 프로젝트입니다. 이 문서는 지금까지의 구조와 다음 작업자가 알아야 할 것을 정리합니다. 팀 공유용 요약은 claude.ai의 "MoreMustMush 모바일 인계문서"에 있습니다.

---

## 1. 한눈에 보기

| 항목 | 상태 |
|---|---|
| InGame 핵심 플레이 (수확기·바·군락지·타이머·점수·콤보) | ✅ 동작 |
| 배속 1/2/4, 결과·옵션 팝업 | ✅ 동작 |
| 스킬 표 135노드 + 스킬 트리 화면 (나무 모양, 스크롤, 핀치 확대·축소) | ✅ 동작 |
| 스킬 효과 전부 (스탯·바 버프·소환·확률 스킬·장치·디버프·보스) | ✅ 동작 (플레이 모드에서 코드로 흉내 내 확인) |
| 스테이지 50개 + 보스 5종 (기믹 4종) | ✅ 동작 |
| 데이터 초기화 (확인 팝업 → Start 로딩) | ✅ 동작 |
| WorldTree 버튼 6개 → 팝업·토스트 | ✅ 연결 (내용은 "준비 중") |
| 지갑 (골드·균사석 저장, 세계수·스킬트리 표시) | ✅ 동작 |
| 스킬 가격 (배우기 때 차감) | ✅ 동작 |
| 밸런스 (가격·스테이지 보상·보스 체력) | ✅ 1차 — 밸런스 지표 이론 문서 방식, 봇 측정. 50보스 약 76분 (목표 90분, 6.1) |
| 밸런스 측정기 `BalanceBot` | ✅ 헤드리스 캠페인·보스 반복 측정 |
| 사운드 | ❌ 슬라이더 값만 저장 |
| 실기기 터치 테스트 | ⚠️ 안 함 (핀치·바 조작 모두 에디터에서만) |
| 아트 | ⚠️ 임시. 등급·황금·버프·장치·보스는 색으로만 구분 |

**환경:** Unity 6000.3.19f1 · URP · Input System (`InputSystemUIInputModule`) · TextMesh Pro · Unity MCP (`.mcp.json`, `http://localhost:8080/mcp`)
**저장소:** `https://github.com/LKH1134/MMM_Mobile` (`main`)
**커밋 규칙:** `[태그] YYMMDD 설명` (예: `[ADD] 261026 스테이지·보스 표 및 보스 기믹`)

---

## 2. 커밋 (지난 HANDOFF `d1841f4` 이후)

| 커밋 | 내용 |
|---|---|
| `87fb6da` | 데이터 표(xlsx) 구조 — XlsxReader, DataTable, TableImporter |
| `e11e953` | 스킬트리 노드(SkillNode 프리팹), 스킬 정보 창 |
| `fdf775e` | 스킬 표 135노드 입력 |
| `26a120f` | 스킬 트리 세로 스크롤 배치, Jua 원본 TTF·누락 글자 추가 |
| `230b683` | 선행 조건(link·requires) 갱신, 트리 재배치, 틈으로 도는 선 |
| `bc014db` | 나무 모양 배치(28칸 × 31줄), 가로 스크롤, 핀치 확대·축소 |
| `a0b951b` | 인게임 스킬 1단계: 군락지·등급·황금·점수→골드·스탯 스킬 |
| `3af5272` | 인게임 스킬 2단계: 바 버프·소환·확률 스킬·필드 장치 |
| `7f7fe45` | 인게임 스킬 3단계: 포자 디버프, 보스 판 |
| `ca06b4e` | 옵션 팝업 데이터 초기화 버튼 |
| `f7e210f` | 데이터 초기화 확인 팝업 (ConfirmPopup) |
| `a9a238d` | Stage·Boss 표, 스테이지 진행, 보스 기믹 |
| `450f569` | HANDOFF 갱신 |
| `ddffddf` | 밸런스 패치: 지갑, 스킬 가격, 스테이지 보상 분리, 보스 체력, 파밍 규칙, BalanceBot |
| `e5f377e` | 낫이 필드 가운데에서 발사되던 문제 수정 (바 원래 크기 저장 순서) |

`LiberationSans SDF - Fallback.asset`은 TextMesh Pro가 실행 중에 스스로 고치는 파일이라 계속 "수정됨"으로 뜹니다. 동작과는 무관합니다.
`Assets/_Recovery/`는 Unity를 강제 종료할 때 생긴 씬 백업입니다. 커밋하지 않았고 지워도 됩니다.

---

## 3. 씬 흐름

`Start`(로딩) → `WorldTree`(세계수) ↔ `SkillTree`(스킬트리) → `InGame`(판) → 결과 팝업 → 다시 하기 / 세계수로

- 데이터 초기화(어느 옵션 팝업에서든) → 저장값 전부 삭제 → `Start`.
- SkillTree의 스테이지 버튼 위 글자는 지금 스테이지 이름(`숲 12`, 보스면 `달빛 밤 30 보스`)입니다.

---

## 4. InGame

### 4.1 판 규칙

1. 판이 시작되면 수확기가 바 위에서 대기합니다. **타이머는 첫 투척부터** 흐릅니다. 던지기는 한 판에 한 번(함께 던지는 수확기가 있으면 같이 나감).
2. 끌었다 놓으면 끈 반대 방향으로 던집니다(40px 미만은 취소). 수확기는 감속 없이 같은 속도로 벽·바·버섯에 튕깁니다.
3. 던진 뒤에는 화면 어디를 눌러도 좌우로 끄는 만큼 바가 움직입니다. 바는 **윗면만** 판정합니다.
4. **군락지:** 가운데 군락 버섯 1개 + 둘레 단일 버섯 n개. 군락지 하나는 같은 등급(일반·에픽·유니크)이고, 버섯마다 따로 황금 변종이 정해집니다. 군락지를 다 캐면 사라지고 리스폰 시간 뒤 빈 자리에 새로 돋습니다. 처음엔 벌집 격자에 놓고, 자리가 모자라면 버섯을 최소 0.35배까지 줄입니다.
5. 한 방에 캐지는 버섯은 뚫고 지나가고, 체력이 남는 버섯에는 튕깁니다(관통 버프 중엔 안 튕김).
6. **점수**는 판 중에 쌓이고, 끝나면 **골드 = 점수 × 전환율**을 지갑(`Wallet`)에 넣습니다. 결과 팝업에 골드·점수·수확량(버섯 개수).
7. 콤보 = 판 전체에 하나(`HarvestField.Combo`). 수확기가 버섯을 캘 때마다 +1, **수확기가 바를 놓쳐 바닥 벽에 닿으면 0**(바에 닿아도 유지). 분열체처럼 잠깐 있는 수확기가 바닥에 닿는 건 끊지 않음. HUD·점수 배율이 같은 값을 봄. 점수 ×(1 + min(콤보, `comboCap` 20) × 콤보 계수 0.05) — **콤보 점수 배율은 스킬 전 기준 최대 ×2.0**(스킬 `ComboRate`·피버 `ComboMul`이 계수 0.05에 곱해지므로 스킬을 배우면 ×2.0을 넘을 수 있음, 의도), HUD 콤보 숫자는 상한 없이 계속 오름(2026-10-27, 측정 M1: 상한 없이 0.1이면 후반 골드 ×9~21).
8. 게임 오버는 시간 종료, 또는 보스 처치(보스 판).
9. **일반 스테이지 목표 (2026-10-27):** Stage.xlsx `goalType`·`goal` = "버섯 N개 수확". HUD 왼쪽 위 `Goal`에 "수확 n/N", 채우면 "목표 달성!" — 판은 시간 끝까지 계속. 못 채우면 **패널티 없이**(골드 100%) 같은 스테이지를 다시, 연속 실패마다 목표 ×0.9(최저 0.7, `RoundBase.goalRelief`·`goalReliefMin`, `Stage.FailStreak`). 보스·파밍 판엔 목표 없음. `mushHpMul`은 일반 버섯 체력에만 곱하고(같은 풀 프리팹, 색만 `hardTint`로 조금 어둡게) 보스엔 안 곱함.
   - 10스테이지 묶음 계단(첫 시도 목표 통과율): 1~3 튜토리얼 고정 3·4·5(연속 실패 완화도 없음 — `RoundStats.TutorialStages`, `GoalFor`) / x1~x3 85% / x4 75%(체력 ×1.1) / **★x5 35%(×2.0)** / x6~x7 85% / x8~x9 60%(×1.3, 시간 손잡이로 **65% 분위로 맞춤**) / x0 보스.
   - **N 값 (M2, 2026-10-27):** 사람 모델 캠페인 여러 묶음(33시드)의 첫 시도 수확 평균 × (1 + z·CV)로 맞춤. 15(★)는 15초 판 공급 상한(≈40개)에 걸려 40으로 수동 고정. 최종 확인(12시드) 첫 시도 통과율: x1~3 69% · x4 70% · ★ 55% · x6~7 87% · x8~9 67%. x1~3이 낮은 건 큰 구매(3341 등) 직전 스테이지의 구매 계단 때문. 자세한 표는 `AGENT_WORKLOG.md` [측정·조정] M2.
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
| `SkillEffects.cs` | 바 버프, 분열, 분신 바, 확률 스킬, 포자 디버프, 꼬마 수확기, 버프 임시 표시 |
| `FieldDevices.cs` | 환경 복원: 그루터기(튕김 + 공격력) · 꽃밭(가속) · 도토리 3개(다 켜면 점수 2배) |
| `BossGimmicks.cs` | 보스 기믹 4종. `Step(dt)`로 시험 가능 |
| `Harvester.cs` | 버프 타이머(`HarvesterBuff`: Power·Haste·Pierce·Homing·Heal·Stump·Slow·Weak), 임시 여부·수명, 다음 타격 배율 |
| `Mushroom.cs` | 등급·황금·크기·체력 배율, 군락지 번호, IsCenter·IsBoss, 회복 |

이벤트(확장 지점): `HarvestField.Launched`, `BarHit`, `Hit(m, h, damage, crit)`, `Harvested(m, h)`(스킬 피해면 h가 null일 수 있음), `ColonyCleared(center)`, `BeforeColonies`. 스킬 피해는 `HarvestField.DealDamage(m, damage, source)`.

> **충돌 계산은 Physics2D를 쓰지 않습니다.** 화면이 전부 UI 캔버스라 Field 로컬 좌표(캔버스 픽셀)에서 원-원·원-선분 충돌을 직접 계산합니다.

### 4.4 보스 판

- Stage.xlsx에서 `boss`가 있는 스테이지(10·20·30·40·50)가 보스 판입니다. 군락지 대신 보스가 나오고 다시 돋지 않습니다.
- 보스 체력 = 100 × 보스 `hpRate` × 스테이지 `hpRate`. 관통되지 않고, 유도·번개·회오리는 보스를 노립니다.
- 맞힐 때마다 점수 = 피해 ÷ 보스 체력 × 0.35(`bossHitShare`, 기본 비율 — 다 깎았을 때 받는 몫) × **직전 일반 판 점수**. 보스 체력을 넘친 피해는 점수가 없습니다.
- 분신까지 모두 쓰러뜨리면 처치: 추가 골드 **max(이번 판, 직전 일반 판) × 3**(`bossRewardMul`, PC판 2에서 올림) + 첫 처치면 균사석(`gem`) + 레전더리 버섯, 판이 바로 끝납니다.
- 못 잡으면 **패널티 없음**: 그 판 골드 × `failGold`(표 1.0), 강제 파밍 없음. 결과 팝업 버튼이 **재도전 / 파밍 / 세계수로** 셋이 됩니다. 파밍 = `StageProgress.ChooseFarm` → `FarmRounds`(1)판 같은 스테이지 일반 판 뒤 재도전. SkillTree 스테이지 글자에 "파밍 n". 끈기(진 판마다 보스 피해 ↑)·스킬 스테이지 잠금은 넣지 않음.
- **RoundBase 값은 `InGame.unity`에 직렬화**(2026-10-27부터 `comboFactor`·`comboCap`·`bossHitShare`·`bossRewardMul`·`goalRelief`·`goalReliefMin`·`hardTint` 모두 씬에 있음). 씬 값이 우선이므로 C# 기본값만 바꾸면 반영되지 않습니다 — 인스펙터(HarvestGame)에서 고치세요. C# 기본값은 씬 값과 같게 맞춰 둠.
- **보스 판엔 황금 버섯이 나오지 않습니다**(`HarvestField.SpawnMushroom` :702 `golden = !stats.BossRound && …`, 1차 작업 T4에서 의도한 변경). 지금은 보스 판에 군락지가 없어 이 경로로 버섯이 안 나오므로 방어용 가드(보스 몸은 `SpawnBossPart`에서 원래 황금 아님). 보스 스테이지의 **파밍 판**은 일반 판이라 황금이 나옴.
- 보스 체력 (M2, 2026-10-27 다시 맞춤): Boss.xlsx `hpRate` 0.1809 · 0.5278 · 1.52 · 1.5 · 14.84 → 체력 72 · 256 · 1,054 · 1,040(+분신) · 15,434. 목표 첫 시도 처치율 90·85·60·55·45%. M1의 "30·50 보스 벽"은 옛 체력(10-26 맞춤)이 **e5f377e 전 바 폭 0 버그 상태**(바가 필드 가운데 폭 0 → 수확기가 위쪽 절반만 오감, 보스 피해 30에서 ×2.5)로 잰 값이었기 때문 — 바를 고친 뒤 30·50이 벽이 됐음. 50은 ★ 균사석으로 3339(꼬마 수확기)를 더 사게 되어 체력을 거의 그대로 둠. 40은 3341(분신술) 구매 여부로 피해가 약 5배 갈려 3341 없이 ~40% 처치가 되게 30보다 낮춤.

| 기믹 | 보스 | 동작 |
|---|---|---|
| None | 대왕송이 | 없음 |
| Heal | 숲 속의 청소부 | 3초마다 최대 체력의 3% 회복 |
| Purge | 죽음의 천사 | 2초마다 임시 수확기(분열체) 전부 제거 |
| Split | 환상 버섯 | 본체 66%·33%에서 분신 하나씩 (분신 체력 = 그 순간 본체 체력) |
| Move | 움직이는 버섯 | 좌우 260px/초, 몸마다 따로 벽에서 돌아섬 |

---

## 5. 스킬 트리 (SkillTree 씬)

| 파일 | 역할 |
|---|---|
| `SkillTreeScreen.cs` | 하단 메뉴, 설정 팝업, 노드 선택 ↔ 스킬 정보 창, 스테이지 이름 표시 |
| `SkillTreeView.cs` | 표의 lane·row에 노드를 깔고 link를 선으로 연결. 가려지면 노드 사이 틈으로 돌아가는 선 |
| `SkillTreeZoom.cs` | 두 손가락 확대·축소 0.35~1.25배, 에디터는 Ctrl+휠 |
| `SkillNode.cs` | 노드 하나 (`Setup(index)`) |
| `SkillInfoPanel.cs` | 이름·레벨·설명·못 채운 선행 조건, 배우기 버튼(가격 표시, 돈이 모자라면 꺼짐) |
| `SkillProgress.cs` | 레벨 저장(PlayerPrefs), 선행 조건, 가격(`Price`·`NextPrice`·`CanAfford`, `LevelUp`이 지갑에서 차감), `Total`·`Param`·`SeriesLevel`, `SkillAbility` 키. `SimLevels`를 넣으면 저장값 대신 그 레벨을 씀(측정기) |
| `Wallet.cs` | 골드·균사석(PlayerPrefs, long), `Add`·`TrySpend`·`Changed`, `Format`(만·억·조·경) |

- **노드 규격 (사용자 확정):** `Prefabs/Mobile/SkillTree/SkillNode` — 루트 150×150 `btn_cream`, Icon 80×80(위로 16), Level Jua 24pt. 새 노드는 이 프리팹 인스턴스로, `skillIndex`만 바꿉니다. 모양을 바꿀 땐 먼저 물어보세요.
- **선행 조건:** `link`(연결 노드)는 1레벨 이상, `requires`(같은 스킬의 이전 레벨 노드)는 최대 레벨까지.
- **배치:** 표의 `lane`(0~27)·`row`(0~30). 시작 노드가 row 0. 하위 나무끼리 겹치지 않게 펼친 나무 모양이고, 레어도 낮은 노드가 아래입니다. 트리는 약 6000 × 9800px이라 가로·세로로 끌어서 봅니다. 처음엔 시작 노드가 가운데 아래에 옵니다.

---

## 6. 데이터 표

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

**Skill.xlsx 주요 열:** `branch`(Start·Edible·Medicinal·Toxic) · `rarity`(0~19, 유저 비공개) · `tag` · `group`(같은 스킬 계열) · `startLevel` · `maxLevel` · `lane`·`row`(트리 위치) · `ability`·`abilityBase`·`abilityValue`·`param` · `unlockPrice`·`currency`(1레벨 가격·재화) · `levelPrice`·`levelCurrency`(2레벨부터 첫 가격·재화, 비우면 1레벨과 같은 재화) · `priceGrowth`(레벨마다 배율) · `link` · `requires`.

**Stage.xlsx:** `stage` · `theme`(숲 1~, 달빛 밤 20~, 들판 45~ — PC판 지역) · `hpRate`(체력 계수 1 → 10.4) · `rewardRate`(점수·수확량 배율 1 → 736, hpRate와 따로 — 수입 성장 목표용) · `boss` · `goalType`(`StageGoal`: None / Harvest) · `goal`(목표 버섯 수, M2 맞춤) · `mushHpMul`(일반 버섯 체력 배율, 비우면 1) · `gem`(★ 첫 클리어 균사석).

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
- **일반 목표 N:** 4.1-9. x8~9는 시간 손잡이로 65% 분위.
- **결과 (r8, 12시드):** 50보스 처치까지 평균 **88.3분**(85~91). 90분에 −1.7분 — 더 늘리려면 ★ 통과율 55% → 35%.

**측정 스크립트**(아직 저장소에 없음, 임시 폴더): `schedule.py`(목표 일정) · `theory.py` · `iterate.py`(가격 고정점) · `boss_fit.py`(보스 체력) · `rescale_reward.py`(보상 배율) · `metrics.py`(지표) · `apply_prices.py`·`apply_stage_boss.py`(엑셀 쓰기).

---

## 7. 팝업·설정

| 프리팹 | 용도 | 비고 |
|---|---|---|
| `OptionPopup` | 세계수·스킬트리 설정 | 데이터 초기화 버튼 (`DataResetButton`) |
| `InGameOptionPopup` | InGame 옵션 | `OptionPopup`의 변형. BGM/SFX, 세계수로, 데이터 초기화 |
| `ConfirmPopup` | 예/아니오 확인 | `Show(title, message, onYes)`. 다시 쓸 수 있음 |
| `ResultPopup` | InGame 결과 | 골드(카운트업) · 점수 · 버섯, "보스 처치/실패"·"목표 n/N · 다시 도전", 첫 클리어 균사석 줄(`Mycelite`), 보스 패배 시 재도전·파밍(`FarmButton`)·세계수로 |
| `WorldTree/Grow·Kid·Skin·CraftPopup` | WorldTree 시트 | "준비 중" |
| `Toast` | 안내 문구 | 공용 |

모든 팝업은 `PopupSlot`으로 들고 있다가 **처음 열 때 만듭니다**(씬에 미리 두지 않음). 확인 팝업은 캔버스 맨 위에 만들어 다른 팝업 위에 뜹니다.

**데이터 초기화:** `PlayerPrefs.DeleteAll()` → `Time.timeScale = 1` → `Start` 씬. 스킬 레벨·스테이지·배속·볼륨까지 모두 지웁니다.

---

## 8. 개발용

**메뉴:** `MoreMush/Data/Reimport Tables` · `MoreMush/Debug/Go To Next Boss Stage` · `MoreMush/Debug/Reset Stage` · `MoreMush/Debug/Run Benchmark` · `MoreMush/Debug/Open Benchmark Folder`
**PlayerPrefs 키:** `Skill.Level.<index>`, `Stage.Current`, `Stage.FarmLeft`, `Stage.LastNormalScore`, `Stage.LastNormalGold`, `Stage.Cleared`, `Stage.FailStreak`, `Wallet.Gold`, `Wallet.Mycelite`, `InGame.SpeedIndex`, `Option.BgmVolume`, `Option.SfxVolume`

**밸런스 측정기 (`BalanceBot`, InGame 씬 플레이 모드에서 MCP `execute_code`로)**
- `RunCampaigns(seeds, skill, incomeMul, probeBoss, schedulePath, outPrefix, maxRounds, bossDamageMul)` → `Temp/<prefix><seed>.csv`(판마다 한 줄) + `_buys.csv`. 문서 사람 모델은 `(…, 1.0f, 0.6f, false, null, "Temp/bb_s", 250, 0.234f)`.
- `ProbeBoss(stage, "index:level;…", refScore, refGold, tries, skill, seed, outPath)` → 보스 한 판 피해를 여러 번.
- 저장값은 건드리지 않습니다(`SkillProgress.SimLevels`, `HarvestGame.Simulating`).
- 진행 규칙은 게임과 같음: 일반 판은 목표를 채워야 다음(실패면 같은 스테이지, 연속 실패 완화), 보스에 지면 바로 재도전(`Config.farmRounds` 0 = "재도전" 선택), 균사석은 `StageProgress.FirstClearGem`. CSV에 `goal`·`goalMet`·`failStreak` 열. 오래 걸리면 MCP 호출은 시간 초과로 실패로 뜨지만 계속 돌아 CSV가 나옵니다.
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
- **폰트 글자 누락:** 폰트 SDF가 전부 Static 아틀라스입니다. 없는 글자는 □로 나옵니다. Jua는 원본 TTF(`Assets/Fonts/Source/Jua-Regular.ttf`, OFL)가 있어서, 폰트 에셋을 잠시 Dynamic으로 바꿔 `TryAddCharacters` 후 Static으로 되돌리면 추가됩니다(빌드에 TTF가 딸려 가지 않게 `m_SourceFontFile`은 비워 둠). 이번에 흥렉섬굉둔링괴덤탈혜옹박·패·됩을 추가했습니다. 금액 단위(`Wallet.Format`) 억도 추가했습니다 (만·조·경은 원래 있음). `→ − ·`은 Jua에 없고 대체 폰트로 나옵니다. Nunito는 한글이 없습니다.
- **에디터 포커스:** Unity 창이 뒤에 있으면 플레이 루프가 멈추거나(frameCount가 안 오름) 가상 입력이 버려집니다. 시험은 `HarvestField.Step(dt)`, `BossGimmicks.Step(dt)`처럼 정해진 시간만 흘리는 방식으로 하세요. **`Time.deltaTime`이 쌓일 때까지 도는 while 루프는 deltaTime이 0이면 Unity가 멈춥니다**(실제로 한 번 강제 종료함).
- **저장 안 된 씬:** 에디터에서 씬을 고친 채 저장하지 않으면, 스크립트·MCP로 씬을 저장할 때 그 변경까지 같이 저장됩니다. 플레이 모드 중엔 씬 저장이 안 됩니다.
- **화면 비율:** 기준 1080×1920(CanvasScaler 가로 맞춤). InGameArea 1080×1520 고정.
- **테스트 스킬:** 스킬을 많이 배워 두면 필드가 몇 초 만에 비고 보스도 순식간에 죽습니다.
- **Awake 순서:** `HarvestGame.Awake`가 `HarvestField.Awake`보다 먼저 `Configure`를 부를 수 있습니다. 필드가 Awake에서 준비하는 값은 처음 쓸 때 준비되게 하세요(`SaveBarBase` 참고).

---

## 9. 다음 작업 후보

1. **90분 목표:** 일반 스테이지 목표(4.1-9)·보스 패널티 제거(4.4)를 넣음(2026-10-27). 목표 N·보스 체력은 M2가 맞춤(6.2, 88.3분). 남은 것: 수확 공급 상한 구간(11~17·38~39·42~49)은 목표가 운으로 갈림 — 실기기 확인, 목표 종류 검토
2. **수확기 추가 노드 3339**가 26,000,000 골드 노드(3341) 뒤에 있음 — 사람 모델 약 44~46스테이지에 열림. 트리 배치 검토
3. **거의 못 캐는 판:** 약 1% 판에서 버섯 2~8개만 캠. 수확기가 어딘가 끼는 버그로 보임
4. 첫 구매가 ×1.5 효과가 되게 (문서 요구, 지금은 점수 +10%)
5. 밸런스·표 생성 스크립트를 저장소 `Tools/`로 (지금은 임시 폴더)
6. 실기기 터치 테스트: 핀치 확대·축소, 바 조작, 손가락을 뗄 때 노드가 눌리는지
7. 아트 교체: 등급·황금·버프·장치·보스, HUD 점수 아이콘(지금은 골드 그림)
8. 보스 연출: ~~이름·체력 표시~~ (2026-10-28 `BossHud`: 상단 패널 아래 이름 + 본체 체력 바·%, 분신이 살아 있으면 "분신 ×N", 보스 머리 위 작은 체력 바는 숨김. 바 그림 `progress_fill_boss.png` = `progress_fill` 색만 빨강으로) · 남은 것: 분신·회복 연출
9. 사운드 매니저 + 옵션 볼륨 적용
10. WorldTree 시트 내용 (성장·꼬마·꾸미기·제작소)
