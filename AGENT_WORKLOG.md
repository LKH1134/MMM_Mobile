# 에이전트 작업 기록 (2026-10-27)

관리 에이전트가 작업을 나눠 주고, 각 에이전트는 작업을 끝낼 때 **자기 섹션에만** 기록한다.
다른 에이전트의 담당 범위는 건드리지 않는다. 이미 기록된 조사·결론은 다시 하지 않고 이 문서를 근거로 쓴다.

## 진행 순서

1. 이론 (Opus): 원인·설계안을 세운다. 코드 수정은 하지 않는다.
2. 검증 1 (Sonnet): 이론이 코드와 맞는지 단순 확인한다.
3. 재설정 (Opus): 검증 결과로 이론을 고치고, 버그는 수정까지 한다.
4. 검증 2 (Sonnet): 수정과 최종안을 다시 확인한다.

## 담당표

| ID | 작업 | 담당 범위 | 산출물 |
| --- | --- | --- | --- |
| T1 | 콤보 초기화 버그 (슈퍼콤보 70은 뜨는데 우상단 콤보가 0~60으로 리셋) | 콤보 카운트·리셋 로직, 콤보 HUD | 원인 + 수정 |
| T2 | 분신 수확기 스킬이 두 곳에 존재 | 스킬 표(Skill.xlsx/bytes)·스킬 효과 코드 | 원인 + 수정안 |
| T3 | 보스 재시도 버튼이 보스 라운드가 아닐 때도 보임 | 재시도 버튼 표시 조건 | 원인 + 수정 |
| T4 | 보스 라운드에서 황금 변종 제외 | 버섯 스폰(황금 변종) | 원인 + 수정 |
| T5 | 보스 클리어 시 균사석 지급 | 보스 보상·지갑·밸런스 | 설계안 (구현은 사용자 승인 후) |
| T6 | 단계적 허들 (일반 스테이지 클리어 조건, 보스 패배 패널티, 보스 타격/처치 점수) | 스테이지 구조·밸런스 설계 | 설계안 (구현은 사용자 승인 후) |

---

## 기록

(각 에이전트는 아래에 `### [단계] T번호 — 에이전트` 제목으로 추가한다.)

### [이론] T2 — opus

**결론:** 표에서 같은 `ability` 키가 두 그룹에 들어간 경우는 **없습니다**(Skill.xlsx 43개 ability 전수 확인, 그룹·이름 모두 1:1). 그래서 "같은 스킬이 두 곳"은 *키 중복*이 아니라 *보이는 효과/표 구조가 겹치는* 경우이고, 후보는 아래 순서입니다.

**원인 (후보, 가능성 순)**
1. **분열(약용 3228/3236/3237) ↔ 분신술(독 3322/3329/3341)** — 가장 유력
   - 표 구조가 완전히 같음: 둘 다 tag `SlideBar`, `param`=0.1(10%), abilityBase 3 → +1 → +1 (3·4·5초), 3단계. desc "10% 확률로 분열(3초) 버프" vs "10% 확률로 3초 분신 바". 이름도 분열/분신으로 거의 같은 말.
   - 발동도 같은 자리·같은 판정: `SkillEffects.cs:161-162` `OnBarHit`에서 바에 닿을 때 각각 `Chance(…)` 10%.
   - 하지만 실제 생기는 건 다름: 분열 = 작은 **수확기** 2개(`SkillEffects.cs:201-216`, `field.SpawnHarvester` → Harvester 프리팹, 0.7배·공격 0.5배·보라색, 3~5초), 분신술 = **바** 복제(`SkillEffects.cs:219-246`, Bar 풀). 즉 트리에서 글만 보면 같은 스킬, 게임에선 "분신처럼 생긴 수확기"는 분열 쪽만 나옴.
2. **분열(약용) ↔ 회전 칼날(독 3314/3326/3344, `orbit_harvester_count`)** — 게임 화면 기준
   - 꼬마 수확기 그림이 **수확기 그림 그대로**: `Assets/Scenes/InGame.unity:3062` `orbitSprite` guid 7e392c9b… = `Assets/Art/Placeholder/InGame/harvester.png`(Harvester.prefab:121과 같은 그림). `SkillEffects.cs:96,338-379`에서 메인 둘레를 54px 크기로 돔.
   - 분열체도 같은 수확기 그림의 작은 복사본(`SkillEffects.cs:206-212`). → 화면에선 "작은 분신 수확기"가 두 스킬(약용·독)에서 나옴.
3. **분열 ↔ 장인의 도구(독 3339, `harvester_slot`)** — 둘 다 진짜 Harvester 인스턴스를 같은 풀에서 꺼냄(`HarvestField.cs:150` 풀 크기 `1 + ExtraHarvesters + MaxSplit`, `HarvestField.cs:173-174`). 다만 하나는 상시·같은 크기, 하나는 잠깐·작은 크기라 중복이라 보기 어려움.
- 참고: 원본 기획 `MoreMustMush/기획서/균사트리_배치안/생성스크립트/nodes.py:30`(분열)·`:36`(분신술)·`:44`(회전 칼날)에 셋 다 별개 스킬로 있음 → 표 입력 실수는 아님. 기획 단계에서부터 겹친 것.

**수정안**
- 후보 1이면 (추천: 삭제가 아니라 **구분**)
  - 표만 고치기: 분열 3228/3236/3237 `desc`를 "10% 확률로 수확기가 작은 수확기 2개로 갈라짐 (3초/4초/5초)"처럼 결과가 보이게, 분신술 3322/3329/3341 이름을 "분신 바"(거울 분신술 3343은 "거울 분신 바")로 바꿔 수확기와 헷갈리지 않게. `MoreMush/Data/Reimport Tables`로 bytes 다시 만들기. 코드 수정 없음.
  - 더 확실히 다르게 하려면 분열의 발동 조건을 바가 아닌 "버섯 수확 시 n%" 등으로 바꾸는 방안(`SkillEffects.cs:161`을 `OnHarvested`로 옮김) — 사용자 결정 필요.
  - **삭제는 비추천:** 분열을 지우면 ① 3236·3237의 requires 사슬, 3233(link 3228)·3240(link 3236)의 link를 다시 이어야 하고 ② 보스 6003 죽음의 천사 기믹 Purge(`BossGimmicks.cs:8`, `HarvestField.cs:575-586`)가 지울 대상(분열체)이 없어져 기믹이 무의미해짐 ③ 죽은 코드: `RoundStats.cs:97,118-119`, `SkillEffects.cs:161,200-216`.
- 후보 2이면: 꼬마 수확기에 전용 그림을 넣음(InGame.unity `SkillEffects.orbitSprite` 교체, 코드 무관). 임시 아트 문제.
- 후보 3이면: 수정 불필요(설명만).

**확신도:** 키 중복 없음 = 높음. 사용자가 말한 쌍이 후보 1이라는 것 = 중간(60%), 후보 2 = 30%, 후보 3 = 10%.

**확인이 필요한 점:** 사용자가 트리 글(분열/분신술)을 보고 말한 건지, 게임 화면(작은 수확기가 두 스킬에서 나옴)을 보고 말한 건지. 그리고 원하는 것이 삭제인지 구분인지(삭제면 죽음의 천사 기믹 대안 필요).

### [이론] T4 — opus

**원인 (현재 동작)**
- 황금 여부는 버섯을 꺼낼 때 한 곳에서만 정함: `HarvestField.cs:686` `bool golden = stats.GoldenChance > 0f && Random.value < stats.GoldenChance;` → `:687` `m.Setup(..., golden, ...)`. 이 `SpawnMushroom`은 `SpawnColony`(`:660-679`)만 부르고, `SpawnColony`는 `PlanColonies`(`:501`)·`TrySpawnColony`(`:602,606`)만 부름.
- 확률: `RoundStats.cs:148` `GoldenChance = Clamp01(T(golden_spawn))` (일확천금의 신화 3131/3136/3142, 0.01+0.02+0.02).
- 보스 판 표시: `RoundStats.cs:88` `BossRound => Boss != null`; `HarvestGame.cs:75`에서 `StageProgress.BossActive`(`StageProgress.cs:45-52`, 보스 스테이지이고 파밍 중이 아님)일 때만 `Stats.Boss`를 넣음.
- 보스 판에서는 `HarvestField.cs:177-178`이 `PlanColonies()` 대신 `SpawnBoss()`를 부르고, `:209`에서 군락지 리스폰도 막음. 보스 몸은 `HarvestField.cs:559` `Setup(Legendary, ..., false, ...)`로 **이미 황금 아님**. 점수 쪽도 보스는 `HarvestGame.cs:100-108`에서 먼저 return 하므로 `:117,122`의 황금 배율을 안 탐.
- → **지금 코드로는 보스 판에 황금 변종이 나올 길이 없음.** 사용자가 본 것은 아마 ① 보스 스테이지의 **파밍 판**(보스 실패 후 `FarmLeft>0`, `BossActive=false` → 일반 판이라 군락지·황금이 나옴) 이거나 ② 보스 색이 황금처럼 보이는 경우(Boss.xlsx 6005 움직이는 버섯 `#f2a64a` 주황, 6001 대왕송이 `#c9a27a`; 황금 색은 `HarvestField.cs:46` `(1,0.85,0.25)`).

**수정안**
- 확실히 막는 가드(앞으로 보스 판에 버섯을 부르는 기믹이 생겨도 안전): `HarvestField.cs:686`을
  `bool golden = !stats.BossRound && stats.GoldenChance > 0f && Random.value < stats.GoldenChance;`
  로 바꿈. 한 줄, 다른 영향 없음.
- (대안/추가) `HarvestGame.cs:75` 바로 뒤에 `if (Stats.BossRound) Stats.GoldenChance = 0f;` — FromSkills는 Boss를 넣기 전이라 거기선 못 함.
- 사용자가 말한 "보스 라운드"가 보스 스테이지의 파밍 판까지 포함이면: 조건을 `stats.Stage != null && stats.Stage.boss != 0`(보스 스테이지 전체)로 바꿔야 함. 단 파밍 판은 보스 재도전 전에 골드를 모으는 판이라 황금을 빼면 파밍 수입이 줄고 밸런스(BalanceBot 측정)에 영향.
- 보스 색 오해라면 Boss.xlsx 6005 `color`를 황금과 먼 색으로 바꾸는 것이 해결.

**확신도:** "현재 보스 판엔 황금이 안 나온다" = 높음(스폰 경로 전부 확인). 가드 한 줄 수정안 = 높음. 사용자가 실제로 본 상황(파밍 판 vs 보스 색) = 낮음~중간.

**확인이 필요한 점:** 사용자가 황금을 본 판이 보스가 나온 판인지, 보스 실패 뒤의 파밍 판인지. 파밍 판도 빼길 원하는지.

### [이론] T1 — opus

**먼저 확인할 점 (범위 불일치):** 모바일(`C:\Project\MMM_Mobile`)에는 "슈퍼콤보"도, 70이라는 문턱도 없다 (`Assets` 전체 grep: SUPER/슈퍼 0건, 콤보 HUD는 `StageHud.SetCombo` 하나뿐). 제보 내용(70에서 뜨는 문구 + 우상단 콤보 + 바닥 벽 리셋)은 **PC Unity판 `C:\Project\MoreMustMush`** 코드와 정확히 맞는다. 아래 원인·수정안은 PC판 기준이고, 모바일 상태는 맨 끝에 따로 적었다. 사용자에게 어느 프로젝트의 제보인지 확인이 필요하다.

**원인 (PC판): 우상단 HUD와 슈퍼 피버(70)가 서로 다른 카운터를 본다**
- 70에서 뜨는 문구: `Core/Defs.World.cs:108` `FEVER = { (25,"FEVER!"), (70,"SUPER FEVER!!") }` → `Round/RoundSim.Effects.cs:317-321` `FeverLv(chain)`. (`SUPER COMBO!` 배너는 `Effects.cs:311` `chain % FLAME_STEP(=10000)`이라 70과 무관. 사용자가 본 70짜리는 SUPER FEVER!!)
- `chain` = 전역 연속 수확 수. `Effects.cs:306` `chain = chainT > 0 ? chain + 1 : 1`, 수확할 때마다(`RoundSim.Balls.cs:434`, 보스 `RoundSim.Boss.cs:178`) 오르고, **시간 창(comboWin)이 지나야만** 0이 된다(`Effects.cs:328`). 기록 `maxCombo`(`Effects.cs:308`)와 의뢰 "combo"(`Game.Quests.cs:76`, `Effects.cs:422`)도 이 `chain`.
- 우상단 HUD: `Flow/StageHUD.cs:134-136` `foreach (var b in R.balls) if (b.hits > c) c = b.hits;` → **수확기별 타격 수 `b.hits`의 최댓값**. `b.hits`는 바닥 벽에 닿으면 0 (`RoundSim.Balls.cs:174` `b.combo = 0; b.hits = 0;`), 새 수확기/분열체는 0부터, 수확기가 사라지면 최댓값 후보에서도 빠진다.
- 결과: 바를 놓쳐 바닥에 닿으면 HUD는 0으로 떨어지는데 `chain`은 계속 이어져 70에서 SUPER FEVER!!가 뜬다 → 제보 그대로("슈퍼 70은 뜨는데 우상단은 0~60에서 리셋").
- 경위: `git log -L` 결과 커밋 `b7a53c57 [MOD] 수정사항`에서 HUD가 `b.combo`(바 콤보) → `b.hits`로 바뀌었다. 처음부터 `chain`을 본 적이 없다. `View/RoundView.Combo.cs:8` 주석은 "콤보 수는 오른쪽 위 HUD에만 뜬다"고 해 HUD 콤보 = 피버·기록 콤보로 의도된 것으로 읽힌다.

**수정안 (PC판, 권장 A)**
- A. `Assets/Scripts/Flow/StageHUD.cs:134-135`를
  `int c = R.chain;   // 콤보 = 시간 창 안 연속 수확 (피버·SUPER COMBO·최장 콤보 기록과 같은 값)`
  으로 바꾼다 (`RoundSim.cs:75` `public int chain`). 파일 머리 주석 `StageHUD.cs:10`의 "Cur_Combo(지금 가장 큰 콤보)"도 같이 고친다. 0으로 끊길 때의 `comboDrop` 연출(`StageHUD.cs:186`)은 chainT 만료 때 그대로 동작한다.
- B(대안). 피버를 HUD 쪽 기준(수확기별 `b.hits`, 바닥 리셋)으로 바꾸는 방법 — 피버·기록·의뢰를 모두 손대야 하고 원작(`Prototype/MoreMush.v14.html:3352` 전역 chain) 규칙과 달라져 비권장.
- 원작 참고: `Prototype/MoreMush.v14.html`에는 두 콤보가 따로 있다 — 전역 `R.chain`(시간 창, 피버/SUPER COMBO) 과 수확기별 바 콤보 `b.combo`(바에 맞으면 +1 `:2861`, 바닥 벽 0 `:2752`, 점수 배율 `:2888`, 수확기 위 작은 "x3" `:3764`). PC판 HUD는 이 둘 어느 것도 아닌 `b.hits`를 보여 주고 있다.

**모바일 상태 (참고, 같은 문제를 모바일에서 찾는다면)**
- 모바일 콤보 = "마지막으로 바에 닿은 뒤 캔 버섯 수" (`HarvestGame.cs:8`, HANDOFF 4.x 7번). 바에 닿을 때마다 0 (`HarvestField.cs:349`, HUD `HarvestGame.cs:56`), 던질 때 0 (`Harvester.cs:107`, `HarvestGame.cs:53`), 수확 시 +1 (`HarvestField.cs:405`), HUD는 Main 수확기만 (`HarvestGame.cs:127`). 바닥 벽(`HarvestField.cs:343`)은 콤보를 건드리지 않는다.
- 즉 모바일은 원작 바 콤보와 리셋 조건이 **반대**(원작: 바에서 +1, 바닥에서 0 / 모바일: 바에서 0). 바를 잘 받을수록 콤보가 끊기는 구조다. 이게 의도된 모바일 규칙(HANDOFF에 명시)인지 사용자 확인이 필요하다. 원작 규칙으로 맞춘다면: `HarvestField.cs:349` `h.Combo = 0` → `h.Combo++`, `HarvestField.cs:343`의 바닥 분기에 `h.Combo = 0` (+ HUD 갱신 이벤트), `HarvestField.cs:405`의 `h.Combo++` 제거, `HarvestGame.cs:56` HUD 갱신을 `SetCombo(h.Combo)`로.

**확신도:** PC판 원인 높음(코드로 두 카운터가 갈리는 것이 확인됨). 제보가 PC판이라는 판단 중간~높음(모바일엔 70 문턱·슈퍼 문구 자체가 없음).

**확인이 필요한 점**
- 제보가 PC판(`MoreMustMush`)인지 모바일인지. 이 작업 범위는 모바일로 지정됐으나 모바일에는 해당 기능이 없다.
- PC판 HUD를 `chain`으로 바꿔도 되는지(커밋 b7a53c57에서 일부러 `b.hits`로 바꾼 이유가 있었는지).
- 모바일 콤보 규칙(바에서 리셋)을 원작(바에서 +1, 바닥에서 리셋)으로 되돌릴지.

### [이론] T3 — opus

**먼저 확인할 점 (범위 불일치):** 모바일에는 보스 재도전/재시도 버튼이 없다. 모바일의 재도전은 자동(`StageProgress.cs:45-52` `BossActive` = 보스 스테이지 && `FarmLeft == 0`, 파밍 2판 뒤 자동으로 보스 판)이고, 버튼은 결과 팝업의 "다시 하기"(`ResultPopup.cs:16,55`, 씬 재로드, 보스 조건 없음)뿐이다. "보스에게 처음 지면 해금되는 보스 도전 버튼"은 **PC판 `C:\Project\MoreMustMush`**에 있다. 아래는 PC판 기준.

**원인 (PC판)**
- 버튼: `Assets/Scripts/Flow/MainGameController.cs:173-200` `WireBossButton` (`Button_Boss`, 없으면 출발 버튼을 복제), 누르면 `ToggleBoss`(`:202-208`)로 `G.bossOn` 켜고 끔.
- 표시 조건: `MainGameController.cs:163` `UIUtil.Show(bossButton, Unlock.On(Unlock.BOSS));` — 해금 여부만 본다. 해금 = `Core/Unlock.cs:34` `BOSS => G.bossFailed`, `bossFailed`는 보스에게 처음 지면 true가 되고(`Round/RoundSim.Effects.cs:419`) 다시 false가 되지 않는다. 그래서 한 번 열리면 보스 스테이지를 넘긴 뒤 일반 스테이지에서도 계속 보인다.
- `RenderBoss`(`:210-219`)는 `NextBossStage(ContentNow())`로 다음 보스를 안내하도록 짜여 있어, 일반 스테이지에서 보이는 것 자체가 원래 구현 의도였던 것으로 보인다(버그라기보다 사양 변경 요청).

**수정안 (PC판)**
- `MainGameController.cs:163`을
  `UIUtil.Show(bossButton, Unlock.On(Unlock.BOSS) && IsBossStage(ContentNow()));`
  로 바꾼다 (`IsBossStage`: `Core/Defs.Boss.cs:46`, `ContentNow`: `Core/Game.cs:61`, 둘 다 이 파일의 `using static`으로 바로 쓸 수 있음). `ApplyUnlocks`는 씬 시작 때 호출(`:93`)되므로 라운드 뒤 메인으로 돌아올 때마다 다시 판정된다.
- 안전장치: 버튼이 숨겨진 일반 스테이지에서 `G.bossOn == false`로 남아 있으면 다음 보스 스테이지에서 버튼 없이 파밍만 하게 될 수 있다(지금은 일반 스테이지에서도 끌 수 있었으므로 기존 세이브에 가능). `ApplyUnlocks`(또는 `RoundSim.Effects.cs:420` 스테이지가 넘어갈 때)에서 `if (!IsBossStage(ContentNow()) && !G.bossOn) { G.bossOn = true; SaveGame(); }` 를 함께 넣는 것을 권장. 보스 스테이지에서는 버튼이 보이므로 문제없다.
- `RenderBoss`의 `where` 문구는 보스 스테이지에서만 보이게 되므로 "지금 스테이지 {0} · {1}" 분기만 남기고 단순화 가능(선택).

**확신도:** 원인 높음(표시 조건이 해금 여부 하나뿐임을 코드로 확인). 제보가 PC판이라는 판단 높음(모바일엔 해당 버튼이 없음).

**확인이 필요한 점**
- 제보가 PC판인지. 모바일이라면 "보스 판이 아닐 때 보이는 재시도 버튼"에 해당하는 UI가 없으므로, 사용자가 말한 화면(씬·버튼 이름)을 받아야 한다. 혹시 모바일 결과 팝업 "다시 하기"를 말한 것이라면 그 버튼은 보스와 무관하게 항상 보이고 같은 스테이지 진행 상태(`StageProgress`)로 다음 판을 연다.
- "보스 판이 아닐 때"의 기준: 지금 스테이지가 보스 스테이지일 때만 보일지(위 수정안), 아니면 보스에게 져서 파밍 중(`!G.bossOn`)일 때만 보일지.

### [검증1] T2 — sonnet

**틀림 (핵심):** "같은 ability 키가 두 그룹에 들어간 경우는 없다"는 거짓. 표(Skill 덤프)를 그룹 기준으로 다시 묶으면 같은 ability가 서로 다른 그룹에 걸친 것이 2개 있음.
- `colony_count`: 그룹 `wide_view`(넓은 시야 3103/3113/3117, +1씩) / `sky_view`(하늘의 시야 3123/3126/3139, +2씩) / `god_eye`(신의 눈 3144, +3) — 세 그룹이 군락지 수를 늘림.
- `gold_conversion`: 그룹 `bargain`(흥정 기술 3102/3108/3110, 점수 골드 전환율 +10→15→20%) / `regular_customer`(단골 손님 3122/3138/3146, +30→40→50%) — 두 그룹이 같은 능력.
- 고유 ability 수는 43개가 맞지만 "그룹·이름 1:1"은 아님. 사용자가 말한 "같은 스킬이 두 곳"의 1순위 후보는 오히려 이 쌍들(특히 흥정 기술 ↔ 단골 손님: 둘 다 '점수 골드 전환율', 군락지 수 3계열)일 가능성이 큼. 분열/분신술 쪽은 ability가 달라(`split` vs `clone`) 키 중복은 아님.
- 단, 이 중복이 의도된 계단식 설계(점수 전환율 누계 +10→…→+50%)일 수 있음. desc가 이어서 누적 표기(+30%→+50%)라 같은 계열로 의도된 듯 — 삭제가 아니라 이름/묶음 정리 여부는 사용자 결정 사항.

**맞음:**
- 분열(`SkillEffects.cs:161` OnBarHit → `Split` 200-216): `field.SpawnHarvester`로 작은 Harvester 복사본(Temporary, Size×splitSize, 분열 tint) 생성. 맞음.
- 분신술(`:162` → `SpawnCloneBar` 219-246): 바 풀(`clonePool`, `field.Bar` 복제)에서 바 복제. 맞음.
- 둘 다 tag SlideBar, 3→4→5초 구조(덤프 3228/3236/3237, 3322/3329/3341 param 0.1) 맞음.
- 회전 칼날 orbit 그림: `InGame.unity:3062` orbitSprite guid 7e392c9b…가 `harvester.png.meta`의 guid와 일치 → 같은 그림. 맞음. orbit은 `SkillEffects.cs:~96`에서 `OrbitCount`만큼 생성.
- 보스 6003 Purge: `BossGimmicks.cs:52-53` → `HarvestField.PurgeTemporaryHarvesters`(576-587, `Temporary`인 것만 제거). Temporary는 분열체뿐이라 분열이 없으면 무의미 — 맞음. 풀 크기도 `HarvestField.cs:151` `1+Extra+MaxSplit` (인용 150은 ±1줄).

**보완:** `StageProgress.cs`는 InGame 폴더가 아니라 `Assets/Scripts/Mobile/StageProgress.cs`. 수정안에 `colony_count`/`gold_conversion` 중복 정리안(이름 통일 또는 한 그룹으로 합치기, 합산 값이 RoundStats에서 T()로 누적되는지 확인)을 추가해야 함.

### [검증1] T4 — sonnet

**맞음:**
- 황금 판정은 `HarvestField.cs:686` 한 곳뿐(grep 전수: `Golden`을 정하는 곳은 여기, 소비는 Mushroom.Setup·HarvestGame:117,122). `RoundStats.cs:148` GoldenChance 맞음.
- `SpawnMushroom` 호출자는 `SpawnColony`(666,676)뿐이고 `SpawnColony` 호출자는 `PlanColonies`(501)·`TrySpawnColony`(602,606)뿐. `PlanColonies`는 `:178`(BossRound 아닐 때)에서만 호출. `TrySpawnColony`는 `respawns` 처리(217)에서만 호출되고, `respawns.Add`는 `:209`(보스 판 제외)와 `:503`(PlanColonies 안)뿐. → 보스 판에서 일반 버섯 경로 없음. 맞음.
- 보스 기믹(`BossGimmicks.cs`): Heal·Purge·Split·Move 중 버섯을 만드는 것은 Split(`SpawnBossPart`)뿐이고, 이는 `Setup(..., false, ...)`(`:559` 인근)로 golden=false 고정. 보스 몸은 황금 아님. 맞음.
- BossActive 설명(`StageProgress.cs:45-52`, `FarmLeft==0`일 때만 보스 판; FarmLeft는 보스 실패 후 설정되고 판마다 감소 `:67`)과 `HarvestGame.cs:75`에서 Boss를 BossActive일 때만 넣는 구조 맞음 → 파밍 판은 일반 판으로 황금 나옴. 보스 점수 경로는 `HarvestGame.cs:100-108`에서 먼저 return 맞음.
- 라인 번호는 대체로 ±1~2줄 이내.

**보완/위험:**
- 가드 `!stats.BossRound && ...` 안전, 부작용 없음(Random 호출 순서가 보스 판에서만 달라져 BalanceBot 시드 재현엔 영향 없음). 사실상 현재 동작 변경 없는 방어 코드이므로 "사용자가 본 현상"을 고치지는 않음 — 실제 원인은 파밍 판 또는 색 오해일 가능성 그대로. 이 점을 사용자에게 명확히 해야 함.
- 대안안 `HarvestGame.cs:75` 뒤 `Stats.GoldenChance = 0`: Configure(`field.Configure(Stats)`)가 그 뒤에 호출되므로 동작은 하나, HarvestGame 외 경로(BalanceBot이 Restart/Configure 직접 호출)에서는 빠질 수 있어 `HarvestField.cs:686` 가드가 더 확실함.
- 파밍 판까지 제외하려면 `stats.Stage.boss != 0` 조건이나 StageProgress에서 판별 필요 — 사용자 결정 사항(밸런스 영향) 문구 맞음.

### [검증1] T1 — sonnet

**맞음:** 모든 file:line 직접 확인.
- PC `Defs.World.cs:108` FEVER 70 "SUPER FEVER!!", `Effects.cs:306-321` ComboHit/FeverLv(chain), `:328` chainT 만료 때만 chain=0·fever=0 (`chain = 0`은 이 한 곳뿐), `RoundSim.cs:75` `public int chain`(StageHUD에서 접근 가능), `Balls.cs:174` 바닥 벽 `b.combo=0; b.hits=0`, `:434`·`Boss.cs:178` ComboHit 호출, `StageHUD.cs:134-136` b.hits 최댓값 — 전부 일치.
- 커밋 b7a53c57: StageHUD가 `b.combo` → `b.hits`로 바뀌고, 같은 커밋에서 Balls.cs에 `b.hits=0`(바닥)과 `b.hits++`가 추가됨. 즉 HUD가 chain을 본 적은 없음 — 맞음.
- 모바일: Scripts grep에 SUPER/슈퍼 문구·70 문턱 없음(피버는 스킬 버프 FeverBuff뿐). 맞음.
- 수정안 `int c = R.chain;` 개념상 컴파일 문제 없음.

**보완:** chain 자체도 시간 창(comboWin=COMBO_WINDOW) 안에 다음 수확이 없으면 0이 됨. 수확기가 바닥에 닿아 오래 못 캐면 chain도 끊기므로, 사용자가 "바닥 닿아서"가 아니라 "시간 지나서" 리셋을 또 불평할 수 있음(의도된 규칙). HUD를 chain으로 바꾸면 이전 b.combo/hits 감각(수확기 개별 카운트)과 달라진다는 점도 사용자 확인 사항.

### [검증1] T3 — sonnet

**맞음:**
- `MainGameController.cs:163` `UIUtil.Show(bossButton, Unlock.On(Unlock.BOSS))` 해금만 봄. `Unlock.cs:34` `BOSS => G.bossFailed`. `bossFailed = true`는 `Effects.cs:419`(보스 판 미클리어)에서만 설정, false로 되돌리는 코드 없음(grep: Save.cs:42 선언 외 쓰기 없음) — 맞음.
- `WireBossButton` 173-200, `ToggleBoss` 202-208, `RenderBoss` 210-219, `ApplyUnlocks` 157 호출 `:93` — 일치. `IsBossStage`(Defs.Boss.cs:46), `ContentNow`(Game.cs:61) 이름 실존, 파일에 `using static MoreMush.Defs/Game` 있음 — 제안 식 컴파일 가능.
- bossOn 우려도 실제: `Effects.cs:419`에서 지면 bossOn=false가 되고(Save 기본값 true), 켜는 곳은 ToggleBoss뿐. 버튼을 일반 스테이지에서 숨기면 OFF 상태로 다음 보스 스테이지에 도달해 `BossRoundNext()`(Game.cs:63 `G.bossOn && IsBossStage`)가 false → 보스 도전 못 함. 다만 보스 스테이지에선 버튼이 보이므로 그때 켤 수 있음(우려는 "사용자가 켜는 걸 잊는 UX" 수준).
- 모바일: "보스 재도전 버튼"·Button_Boss 없음(grep), 재시도는 `ResultPopup.cs:16,27,55` retryButton(씬 재로드)뿐. 맞음.

**보완:** 안전장치(`!IsBossStage && !bossOn → bossOn=true`)는 "보스에게 져서 일부러 파밍 중(OFF)"인 상태를 덮어쓴다 — 같은 스테이지가 보스 스테이지일 땐 조건 밖이라 괜찮지만, 파밍 판은 보스 스테이지에 머물므로(`IsBossStage` 참) 영향 없음. 단 스테이지 넘어간 직후 자동으로 ON이 되는 게 기획 의도인지 확인 필요.

### [이론] T5 — opus

**현재 균사석 흐름 (코드·표·봇 기록 확인)**
- 얻는 곳은 **보스 처치 하나뿐**: `HarvestGame.Finish` → `if (BossKilled && Stats.Boss.gem > 0) Wallet.Add(Mycelite, gem)` (`HarvestGame.cs:151`). 값은 `Boss.xlsx` `gem` = 10·15·20·25·30 (합 100). 이미 지급되고 있지만 결과 팝업 한 줄(`"보스 처치 · 균사석 N"`)로만 보여서 눈에 안 띈다.
- 첫 처치 구분이 없다. 50 보스는 처치해도 `StageProgress.Advance`가 50에 머물러 **50 보스를 다시 잡을 때마다 30개씩** 들어온다(구멍).
- 필요량 92 = 3001 Lv2~10 **19** (1,1,1,2,2,2,3,3,4) + 3235 **18** (2,3,3,4,6) + 3143 **25** (3,4,5,6,7) + 3339 **30** (5,6,8,11 — `Math.Round(6.5)=6`).
- 봇(가장 싼 것부터) 실측 `bb_final_*`: 10보스 직후 3001 Lv2~7, 20보스 직후 Lv8~10으로 **처음 25개가 전부 사냥 준비로**. 3235는 41~44스테이지, 3339는 3341(분신술) 뒤라 50보스 뒤(70~78분, 사실상 엔딩)에 산다. ※ 이 기록은 3341 = 1.2억 시절 값. 지금 표는 2,600만 → 봇 기준 41~43, 사람(수입 ×0.6) 기준 약 44~46스테이지에 3339가 열린다.
- **핵심 문제:** 3339가 열리는 시점(~45)까지 누적 균사석 70인데, 열린 균사석 노드 합은 19+18+25+30 = 92 → **22 부족**. 3143·3235를 먼저 샀다면 꼬마를 못 산다. 그리고 지급이 10판(약 13분)마다 한 번뿐이라 "균사석이 생겼다"는 체감이 약하다.

**설계안 (택1 + 공통)**

| 안 | 내용 | 3339 열릴 때(~45) 누적 | 총량 | 비고 |
|---|---|---|---|---|
| A | 3001 Lv2+를 **골드**로 (`Skill.xlsx` 3001 `levelCurrency` Gold, `levelPrice`·`priceGrowth` 다시 맞춤, 가안 300·×1.9 → Lv10 약 5.1만) | 70 (필요 73) | 100 | PC 10-26 결정 "사냥 준비 골드화"와 같음. 균사석 노드 3개만 남아 의미가 또렷해짐 |
| B | 보스 `gem` 상향 **15·20·25·35·25** (첫 처치만) | 95 (필요 92) | 120 | 표 숫자만 바꿈. 50의 25개는 세계수(진화·꼬마 각성)용 여유 |
| C (T6 채택 시 권장) | 보스 10·15·20·25·30 유지 + **★허들 스테이지(x5) 첫 클리어 3·4·5·6·7** | 95 | 125 | 5스테이지(약 6~7분)마다 지급 → 체감 주기 2배. P16 "보스 1마리마다 균사석 노드 1~2레벨" 충족 |

- 권장: **T6를 하면 C, 안 하면 B.** A는 독립 옵션(같이 쓰면 남는 균사석은 세계수 시트가 생길 때 소모처로).
- 3339를 트리에서 더 앞으로 옮기면(HANDOFF 9-2) C로도 25스테이지 누적 37 − 3001 19 = 18 < 30이라 부족 → 그땐 A가 필요.

**공통**
1. **첫 처치만 지급**: `StageProgress`에 `Stage.BossCleared`(처치한 가장 높은 보스 스테이지) 키 추가, `stage > BossCleared`일 때만 지급. 50 반복 처치는 0(또는 소량 3 — 결정).
2. **보이게**: 결과 팝업에 균사석 줄을 따로(아이콘 + 카운트업, "첫 처치 보상"), 스킬트리에 "균사석으로 살 수 있는 노드" 표시(배지/토스트). 오브젝트는 팝업 안 고정 요소라 풀 규칙과 무관.
3. 페이스 영향: 균사석 노드는 모두 공격·확률 계열이라 일반 판 수입 탄력이 낮다(공격력은 보스에만 듣는다, 지표 문서 4절) → 90분 구조엔 영향 작음. 3339(수확기 +1~4)는 수확량을 크게 올려 **45~50 구간 수입을 앞당김** → 50보스 시간 약간 단축 예상, 봇으로 다시 잼.

**바꿀 파일:** `Assets/Data/Tables/Boss.xlsx`(gem) · (C) `Stage.xlsx` `gem` 열 + `TableRows.cs` `StageData.gem` · (A) `Skill.xlsx` 3001 · `Assets/Resources/Tables/*.bytes` 재생성 · `StageProgress.cs`(BossCleared/첫 클리어 기록) · `HarvestGame.cs`(지급 조건·결과 문구) · `ResultPopup.cs`(+프리팹 균사석 줄) · `BalanceBot.cs`(163행 지급 규칙 동일하게) · `HANDOFF.md` 6장.

**결정 필요:** ① A/B/C 중 무엇 ② 50보스 반복 처치 균사석(0 / 소량) ③ 남는 균사석을 세계수 시트용으로 둘지.

### [이론] T6 — opus

**지금 구조 (사실)**
- 일반 스테이지는 조건 없음: `StageProgress.Complete` 일반 판이면 무조건 `Advance`. 50판 × 1.35분 ≈ 65분이 바닥 → 90분 미달은 구조 문제(HANDOFF 6.1).
- **스킬 해금에 스테이지 조건은 없다** (`SkillProgress`는 link·requires·가격만). "보스를 잡아야 다음 업그레이드"로 느끼는 이유는 ① 스테이지가 멈추면 `rewardRate`(판당 수입 배율 ×1.12~1.5/스테이지)가 멈춰 가격을 못 따라감 ② 균사석이 보스에서만 나옴.
- 일반 버섯 체력 = 프리팹 maxHp(단일 1·군락 5) × 등급 hpMul × 스테이지 `hpRate`. `hpRate`를 "그 시점 공격력"에 맞춰 놓아 **단일 버섯은 늘 한 방**(예: 15스테이지 hpRate 4.85 vs 봇 공격력 7) → 막힘이 없다.
- 보스 패배 패널티 3가지: (a) 그 판 골드 × `failGold` 0.8 (b) 타격 점수 자체가 낮음: 보스 체력 전부 = 직전 일반 판 점수 × `bossHitShare` 0.6 → 진 판 ≈ 일반 판의 0.29~0.38배 (c) **강제 파밍 2판**(`StageProgress.FarmRounds`) 뒤 재도전. 처치: 타격 0.6 + 보너스 max(이번, 직전 일반 골드) × `bossRewardMul` 2 → 처치 판 ≈ 일반 판 2.6배 + 균사석 + 레전더리 수확량.

**제안 1 — 일반 스테이지 클리어 조건 ("목표")**
- 종류: **수확 N개**(기본). 이유: `HarvestCount`가 이미 있음, HUD에 "수확 12/25"로 바로 읽힘, rewardRate(최대 ×736)와 무관해 숫자가 작음. 표에선 `goalType`(Harvest/Score/Colony)으로 열어 두되 처음엔 Harvest만.
- 목표 달성해도 **판은 시간 끝까지 계속**(점수는 계속 쌓임) + HUD "목표 달성!" 연출. 실패 시 **패널티 없음**: 번 골드 100% 지급, 같은 스테이지 다시 도전. 결과 팝업 "목표 21/25 · 다시 도전".
- 목표값 = 그 스테이지에서 **사람 모델 수확 분포의 분위수**(보스 체력 맞춘 방식과 동일, `BalanceBot` 캠페인에 목표 판정 추가해 측정). 10스테이지 한 묶음의 계단:

| 스테이지 | 역할 | 목표 분위수 (첫 시도 통과율) | 버섯 체력 배율 `mushHpMul` |
|---|---|---|---|
| 1~3 | 튜토리얼 | 고정 3·4·5개 (≈100%) | 1.0 |
| x1~x3 | 쉬움 | p10 (90%) | 1.0 |
| x4 | | p15 (85%) | 1.1 |
| **x5** | **★중간 허들** | p45 (55%) | **2.0** (단일 버섯 1~2방, 군락 2배 → 튕김이 늘어 체감) |
| x6~x7 | 숨 돌리기 | p10 (90%) | 1.0 |
| x8~x9 | 보스 예고 | p20 (80%) | 1.3 |
| x0 | 보스 | (보스 처치) | — |

- 가안 숫자(봇 중앙 × 0.6 근사, 측정 전): 8스테이지 목표 ≈ 11개 · 15 ≈ 12(체력 2배 반영 전) · 25 ≈ 38 · 35 ≈ 85 · 45 ≈ 160. **체력 배율을 먼저 정하고 그 상태로 분위수를 재야 함**(체력이 수확 수를 바꾸므로).
- 반복 실패 완화(벽 방지): 같은 스테이지 연속 실패마다 목표 × 0.9 (최저 0.7). `Stage.FailStreak` 키 하나.
- 풀 친화: 새 오브젝트 없음. "단단한 버섯"은 같은 풀 프리팹 + `Setup(hpMultiplier)` + 색만(기존 TierLook 방식). HUD 목표 텍스트 1개, 결과 팝업 줄 1개.

**제안 2 — 해금: 일반 클리어 vs 보스 클리어**
- 일반 클리어 → 다음 스테이지(= rewardRate 상승). ★x5 첫 클리어 → 균사석 3~7 (T5 안 C).
- 보스 클리어 → 다음 지역 + 큰 보상(아래) + 균사석. 스킬에 스테이지 잠금은 **추가하지 않음**(지금처럼 가격·선행으로만; 잠금을 넣으면 표 열·트리 UI까지 커짐 — 원하면 결정 ⑤).

**제안 3 — 보스 패배 패널티 제거 + 점수 재배분**

| 항목 | 지금 | 제안 |
|---|---|---|
| `failGold` (Boss.xlsx) | 0.8 | **1.0** (감액 없음) |
| 강제 파밍 `FarmRounds` | 2 | **0** — 결과 팝업에 "보스 재도전 / 파밍(직전 스테이지 일반 판)" 선택 |
| `bossHitShare` (RoundBase) | 0.6 | **0.35** |
| `bossRewardMul` (RoundBase) | 2 | **3** (사용자 원안대로 크게 가려면 4) |
| 진 판 (체력 60~80% 깎음) | 일반 판의 0.29~0.38배 | 0.21~0.28배 |
| 처치 판 | 2.6배 | **3.35배** (×4면 4.35배) |

- 지표 문서 P15(처치 판 2~5배, 진 판 ≥ 0.5배 & < 일반, 보스 보너스 초과분 ≤ 전체 골드 15%) 대비: 처치 3.35배 OK. **진 판 ≥ 0.5배는 지금도(0.29~0.38) 어긋나 있고 제안은 더 낮음** → 사용자 원안 우선이면 P15 하한을 버림(결정). ×4는 보너스 비중이 15%를 넘을 위험(40보스 기준 약 30%) → 기본 ×3 권장.
- "패널티 대신 성장" 옵션: PC판 **끈기**(진 판마다 보스 피해 ×1.05, 상한 ×1.5, 처치 시 0). 강제 파밍을 없앤 대신 재도전이 조금씩 쉬워짐.
- 결과 팝업: 처치 시 "처치 보상 +X골드 · 균사석 +N" 별도 큰 줄(카운트업), 진 판은 "보스 체력 n% 남음"(감액 문구 없음).
- 코드 주의: `bossHitShare`·`bossRewardMul`은 `InGame.unity`에 아직 직렬화돼 있지 않아 지금은 C# 기본값이 쓰임. 씬을 저장하면 그 값이 박히니 C#·씬 둘 다 확인. 일반 버섯에만 `mushHpMul`을 곱하고 **보스 체력(`BossHp`)엔 곱하지 않게**(`RoundStats.HpRate`를 직접 바꾸면 보스까지 바뀜).

**페이스 영향 (어림, 사람 1판 1.35분)**
- 일반 실패 추가 판 ≈ Σ(1/p − 1): x1~x9 40개 평균 통과 80% → +10판, ★x5 5개 통과 45% → +6판 ⇒ **+16판 ≈ +21분**.
- 보스: 강제 파밍 제거로 진 판 비용 3판 → 1판, 대신 재도전 성공률은 낮아짐 → 대략 −5분. 처치 보너스 ↑로 구매가 앞당겨져 −1~2분.
- 합: 76 − 5 + 21 − 2 ≈ **90분**. 손잡이: 일반 평균 실패율 1%p ≈ ±0.5분, x5 실패율 10%p ≈ ±1.5분.
- 밸런스 모델: 판당 수입 배율 목표(×1.25/스테이지)는 "스테이지당"으로 남고 재시도 판은 수입이 평평 → 판당 성장이 그만큼 느려지는 게 곧 늘어난 시간. 가격(구매 일정 = 판 번호 기준)은 `iterate.py`로 다시 고정점, 보스 체력은 재도전 규칙이 바뀌어 `boss_fit.py` 다시.

**바꿀 파일:** `Stage.xlsx`(+`goalType`·`goal`·`mushHpMul`, T5-C면 `gem`) · `Boss.xlsx`(failGold) · `Resources/Tables/*.bytes` · `TableRows.cs`(StageData 필드, `StageGoal` enum) · `TableImporter`(Validate) · `RoundStats.cs`(RoundBase 값, 일반 버섯 체력 배율) · `HarvestField.cs`(SpawnMushroom 체력) · `HarvestGame.cs`(목표 판정·Gold 식·결과 문구) · `StageProgress.cs`(목표 실패 시 머무름, FarmRounds 0, FailStreak, 첫 클리어 기록) · `StageHud.cs`(목표 진행 텍스트) · `ResultPopup.cs`+프리팹(목표/보상 줄, 재도전·파밍 버튼) · `SkillTreeScreen.cs`(스테이지 글자 "숲 15 ★ 목표 30") · `BalanceBot.cs`(목표·재도전 규칙, CSV `goal`·`goalMet` 열) · `InGame.unity`(RoundBase 직렬화 값) · `HANDOFF.md`.

**결정 필요:** ① 목표 종류(수확 N개 권장 / 점수 / 군락 전멸) ② 목표 달성 시 판 계속 vs 바로 끝 ③ 계단 표(★x5 + x8~9 예고)와 통과율 목표 ④ 반복 실패 완화(목표 ×0.9) 넣을지 ⑤ 스킬에 스테이지 잠금을 추가할지(권장 안 함) ⑥ 강제 파밍 0 + 선택 버튼 / 끈기 넣을지 ⑦ `bossRewardMul` 3 vs 4, P15 진 판 하한 포기 여부 ⑧ 90분을 이 계단으로 맞출지(측정 후 통과율 조정).

### [이론-PC] T2 — opus

**대상:** PC판 `C:\Project\MoreMustMush` (DB = `Assets/DB/MMM_DB_Ver.06.xlsx` → `Assets/Resources/DB/db.json`). 모바일 분석은 위 [이론] T2 / [검증1] T2 참고, 여기서는 다시 하지 않음.

**원인 (file:line)**
- PC 스킬 표에서 "작은 임시 수확기(=기본 수확기 분신)를 소환"하는 스킬이 약용(md) 가지에 **두 노드**로 들어 있다. 키는 다르지만 실제 효과가 같은 함수·같은 결과물이다.
  1. **분열** `split` — Upgrade 12015 (`db.json:798`, Upg_Pre 12012 날 갈기, Tier 3, MaxLv 4), Skill 행 `db.json:3028` (Kind `bar`). 효과: `Round/RoundSim.Bar.cs:97` `SpawnTemp(b.x, b.y-4, 1 + sk["split"].P, 6, false, …)`. 문구 `Core/Defs.Nodes.cs:313` "임시 수확기 {0}개 (6초)".
  2. **분신 수확기** `clone` — Upgrade 12030 (`db.json:864`, Upg_Pre 12024 포자 폭발, Tier 3, MaxLv 4), Skill 행 `db.json:3052` (Kind `harvest`). 효과: `Round/RoundSim.Balls.cs:520` `SpawnTemp(m.x, m.y, 1 + sk["clone"].P, 4, true, null)`. 문구 `Core/Defs.Nodes.cs:316` "작은 임시 수확기 {0}개, 4초".
- 둘 다 `RoundSim.Balls.cs:35-49` `SpawnTemp` 하나를 부르고, 거기서 **똑같이** `MakeBall(..., BASE_HV, ...)` + `b.statMul = CLONE_STAT`(`:42-43`, 주석 "스킬로 생긴 수확기는 기본 수확기 모양의 분신")로 만든다. 다른 점은 발동 시점(바 vs 수확)·크기(보통 vs small)·수명(6초 vs 4초)뿐.
- 게임 안 글도 둘을 같은 것으로 부른다: String 900773 "분열·분신으로 생긴 수확기는 기본 수확기 분신(공격력 20%)", 결과 통계 `Round/RoundSim.Stats.cs:20` `HvKey`가 분열·분신·꼬마(SpawnTemp 출신) 수확기를 **모두** `sub_clone` → `:51` "분신 수확기 (스킬)" 한 줄로 묶음(분열 출신도 "분신 수확기"로 표시). → 트리에서는 "분신 수확기"가 분열(날 갈기 밑)과 분신 수확기(포자 폭발 밑) 두 곳에 있는 셈.
- 참고: 꼬마 수확기 `md_kid` 12007(`RoundSim.Bar.cs:99`, SpawnTemp small 8초)도 같은 분신을 만들지만 스킬(Skill 행)이 아닌 일반 노드라 3번째 후보로만 둔다. 분신 바 `barclone` 12062는 바를 복제하는 다른 효과(단, 아이콘이 분열과 같은 `s_split`, `db.json` 12062 Icon).

**수정안 (사용자 선택 필요 — 추천 A)**
- **A. 분신 수확기(12030) 삭제, 분열로 일원화**
  - xlsx Upgrade 시트: 12030 행 삭제, 충격파 12042의 `Upg_Pre` 12030 → **12024**(포자 폭발)로 다시 잇기(`db.json:955`). Skill 시트 `clone` 행 삭제, Upgrade_Cost 90040·String 22030(upg:clone:name/desc) 정리. db.json 재생성.
  - 코드: `Core/Defs.Nodes.cs:316` `["clone"]` 줄 삭제, `Round/RoundSim.Balls.cs:520` 줄 삭제, `Editor/BalanceSim.cs:42`의 `|| st.sk["clone"].on` 삭제. (`sk["clone"]` 접근이 남으면 KeyNotFound 위험 — 삭제 후 `"clone"` grep 0건 확인. `View/RoundView.cs:698`의 `"clone"`은 보스 "분신 소환!" 글자 그림이라 무관.)
  - 세이브: 12030에 이미 레벨을 넣은 세이브는 `Core/SaveMigration.cs`에서 그 레벨 비용 환급 + 키 제거 필요.
  - String 202008(md_templife desc) "분열·분신·꼬마" → "분열·꼬마", 900773 "분열·분신으로" → "분열로" 수정.
- **B. 남기되 구분** — 분신 수확기 효과를 SpawnTemp 분신이 아닌 다른 것으로 바꾸거나(기획 필요), 최소한 이름을 "꼬마 분신"류로 바꾸고 `RoundSim.Stats.cs:20`에서 분열/분신 출신을 다른 키로 나눠 표시(Ball에 출신 필드 추가 필요).
- 반대로 분열을 지우는 안은 비추천: 분열 12015 밑에 회오리 12039·분신 바 12062가 매달려 있어 사슬을 두 개 고쳐야 함.

**확신도:** 같은 효과가 두 노드(split 12015 / clone 12030)에 있다 = 높음(같은 `SpawnTemp`·같은 CLONE_STAT 분신, 게임 글도 둘 다 "분신"). 사용자가 말한 쌍이 이것 = 중간~높음(70%); 꼬마 수확기 포함 3중 = 20%; 아이콘이 같은 분열/분신 바 = 10%.

### [이론-PC] T4 — opus

**대상:** PC판 `C:\Project\MoreMustMush`. (모바일은 위 [이론] T4: 보스 판에 황금이 나올 길 없음.)

**원인 (file:line)**
- 보스 판에는 일반 버섯 스폰이 없다: `Round/RoundSim.cs:104`(거대 버섯 `st.boss == null`), `:106-111`(보스 판이면 `SpawnBoss`만, 군락지 생략), `:114`(낱개 버섯 생략). 보스 몸 `Round/RoundSim.Boss.cs:35-40` `MakeBossBody`는 `golden`을 안 넣음(기본 false). 스폰 시 황금 판정(`RoundSim.cs:434,459`, `RoundSim.Effects.cs:190`)은 보스와 무관.
- **그러나 때려서 황금으로 바꾸는 경로에 보스 예외가 없다:** `Round/RoundSim.Balls.cs:343-345`
  `if (hv == "gold" && !m.golden && !m.dead && U.Chance((float)a["p"])) { m.golden = true; AddLabel("황금!") ... }`
  — **황금 수확기**(Ball 1008 `gold`)가 보스(`m.boss`)·환상 버섯 분신(`m.bossClone`)을 맞혀도 확률로 `golden = true`가 된다.
- 보스가 황금이 되면: 타격 점수 `RoundSim.Balls.cs:297` `if (m.golden) x *= 10` (`:322`에서 보스 BOSS_SCORE_MUL와 곱해짐 → 보스 허들 의도를 깨뜨림), 몸 색 `View/ShroomView.cs:78` `FxColor(m.golden ? 1 : 0, …)`로 금빛, `View/RoundView.cs:294` 금빛 반짝이, "황금!" 글자. → 사용자가 본 "보스 황금 변종"이 바로 이것.

**수정안 (한 줄)**
- `Assets/Scripts/Round/RoundSim.Balls.cs:343`을
  `if (hv == "gold" && !m.boss && !m.golden && !m.dead && U.Chance((float)a["p"]))`
  로 바꾼다. `m.boss`는 본체·분신 모두 true(`RoundSim.Boss.cs:40` `boss = true, bossClone = clone`)라 둘 다 막힌다. 난수 소모 순서가 바뀌지만(보스일 때 Chance를 안 굴림) 보스 판에만 해당.
- (선택) 보스 판 전체에서 황금을 빼려면 `Core/Game.cs:260` `st.golden` 계산 뒤 보스 판이면 0 — 지금은 보스 판에 일반 버섯이 없어 효과 없음, 앞으로 보스 기믹이 버섯을 부를 때를 위한 가드.

**확신도:** 원인 높음(보스에 황금이 붙는 유일한 경로이고 `m.boss` 검사가 없음을 코드로 확인). 수정 한 줄 = 높음.

### [관리] 사용자 결정 — 대상 프로젝트

- 사용자 답: **모바일 프로젝트(C:\Project\MMM_Mobile)를 고친다.** PC 프로젝트(C:\Project\MoreMustMush)는 수정하지 않는다.
- PC 쪽 조사([이론] T1·T3, [이론-PC] T2·T4)는 "사용자가 본 증상의 원래 규칙"으로만 참고한다.
- 이 결정에 따라 T1·T3·T4는 모바일 기준으로 재설정, T2는 모바일에 같은 중복이 있는지 재확인, T5·T6은 검증1 후 사용자 결정을 받는다.

### [검증1] T5 — sonnet
- 맞음: 균사석 지급처는 `HarvestGame.cs:151` 보스 처치 하나뿐(Wallet.Add Mycelite), `Boss.xlsx` gem 10·15·20·25·30(합 100). 결과 문구 `HarvestGame.cs:161`("보스 처치 · 균사석 N") 한 줄도 맞음.
- 맞음: 첫 처치 판정 없음. `StageProgress.Advance`가 `Min(Current+1, MaxStage)`라 50에서 머물고, 보스 판 조건(`boss!=0 && FarmLeft==0`)이 그대로라 50보스는 재처치마다 30 지급. `BossCleared`/`FirstClear` 코드 없음.
- 맞음: 균사석 가격 합 92 = 3001 Lv2~10 [1,1,1,2,2,2,3,3,4]=19 + 3235 [2,3,3,4,6]=18 + 3143 [3,4,5,6,7]=25 + 3339 [5,6,8,11]=30 (`SkillProgress.Price` 공식으로 재계산, 3339 Lv2 6.5→6 확인). 균사석 노드는 이 4개뿐(표 전수 확인).
- 맞음: 3339 link=3341, 3341 unlockPrice 26,000,000. HANDOFF.md 189·248행은 아직 "1억 2천만"이라 낡음 → 보완: 수정 때 HANDOFF 갱신 항목에 포함.
- 맞음(산식): A 필요 73=18+25+30, B 15+20+25+35=95/총 120, C 70+(3+4+5+6+7)=95/총 125, "25스테이지 37−19=18<30" 모두 일치. 3339 열릴 때(~45) 누적 70 → 22 부족도 일치.
- 보완: `bb_final_*` 봇 기록(41~44스테이지, 70~78분)은 저장소 안에 없어(임시 폴더) 확인 못 함 — 미검증으로 표시. 결과 팝업 균사석 줄은 고정 UI라 풀과 무관, HANDOFF 충돌 없음(문서 6장 갱신만 필요).

### [검증1] T6 — sonnet
- 맞음: 일반 판은 `Complete`에서 무조건 `Advance`(보스 스테이지 파밍 제외). `SkillProgress`에 스테이지 잠금 없음(grep). 단일 버섯 maxHp 1 × hpRate 1.0~10.4 (15스테이지 hpRate 4.854 맞음). 버섯 체력 배율은 `Setup(hpMultiplier)`라 풀 프리팹 그대로 쓰는 안은 풀링을 깨지 않음.
- 맞음: 보스 패배 현재값 failGold 0.8(Boss.xlsx 전 행), bossHitShare 0.6, bossRewardMul 2, `FarmRounds=2`. 처치 판 0.6+2=2.6배, 제안 0.35+3=3.35배(×4면 4.35), 진 판 0.6×0.8×(0.6~0.8)=0.29~0.38 / 제안 0.35×(0.6~0.8)=0.21~0.28 모두 맞음.
- 맞음: `bossHitShare`·`bossRewardMul`은 InGame.unity에 직렬화 안 됨(grep 0건). `BossHp`가 `HpRate`를 곱하므로(`RoundStats.cs:89`) HpRate 직접 변경 시 보스까지 바뀐다는 주의도 맞음. "cap" 표현은 코드상 상한이 아니라 기본값(0.6)이다 — 용어만 보완.
- 틀림(산술): 페이스 계산 "일반 평균 통과 80% → +10판"이 표의 통과율과 안 맞음. 표 통과율(90·85·55·90·80)로 계산하면 x1~x9 8개/10스테이지당 Σ(1/p−1)=1.23 → 5묶음 약 6.2판(10판 아님). x5도 표는 통과 55%인데 본문은 "통과 45%"로 6판 계산 → 55%면 0.82×5≈4.1판. 합 ≈ 10판 ≈ 14분(21분 아님) → 총합 76−5+14−2 ≈ 83분. 목표 90분에 맞추려면 통과율을 낮추거나(평균 ~80%, x5 ~45%) 표를 고쳐야 함.
- 보완: 균사석 C안(x5 첫 클리어 지급)과 연결되므로 T5 C 채택 시 `Stage.xlsx gem`은 첫 클리어 기록(키 추가)과 같이 가야 함. 풀링·HANDOFF 충돌 없음(HANDOFF 9-1 후보 "일반 스테이지 통과 조건"과 일치; 채택 시 6장·9장 갱신).

### [재설정] T1 — opus

**적용 규칙 (사용자 의도):** 콤보는 판 전체에 **하나**. 수확할 때마다 +1, 수확기가 **바를 놓쳐 바닥 벽에 닿으면 0**, 바에 닿아도 유지. HUD와 점수 배율이 같은 카운터를 본다(모바일엔 70 같은 콤보 문턱 연출이 없음 — 점수 배율이 유일한 "마일스톤" 소비처).

**바꾼 곳**
- `HarvestField.cs:97-99` `public int Combo { get; private set; }` 추가(판 단위 카운터), `:107` `event Action<int> ComboChanged`, `:306-311` `SetCombo`(값이 바뀔 때만 이벤트).
- `HarvestField.cs:174` `BeginRound`에서 `SetCombo(0)` (Restart·측정기 판마다 초기화).
- `HarvestField.cs:355-360` 바닥 벽 분기: `if (!h.Temporary) SetCombo(0);`. 바 충돌(`:~363`)의 `h.Combo = 0` 삭제. 수확 `:421` `h.Combo++` → `SetCombo(Combo + 1)`.
- `Harvester.cs` `Combo` 속성과 `OnSpawned`·`Launch`의 `Combo = 0` 삭제 (카운터 두 개가 생기지 않게. 분열체 Launch가 콤보를 끊던 경로도 같이 없어짐).
- `HarvestGame.cs:8` 주석, `:51-55` 투척 때 `SetCombo(0)`·바 충돌 `SetCombo(0)` 삭제 → `field.ComboChanged += hud.SetCombo`, `:~111` 점수 `combo = h != null ? field.Combo : 0`(주인 없는 스킬 피해 수확은 전처럼 배율 없음), `OnHarvested` 끝의 `SetCombo(h.Combo)` 삭제.
- `HANDOFF.md` 4.1-7 콤보 규칙 문구 갱신.

**소비처 판단**
- StageHud: 이벤트로만 갱신(2 이상일 때 표시 규칙 그대로).
- RoundStats `combo_rate`·SkillEffects 피버 `ComboMul`: 계수만 바꾸므로 그대로.
- 분열체(Temporary): 수확하면 +1, 바닥에 닿아도 **안 끊음**(3~5초 뒤 사라지는 임시 수확기라 플레이어가 받을 대상이 아님. BalanceBot도 Temporary는 바로 받지 않음).
- 장인의 도구 추가 수확기(상시, Temporary 아님): 바닥에 닿으면 끊음 — 플레이어가 바로 받아야 하는 수확기이므로 "수확기가 바를 놓침"에 해당.
- 회전 꼬마·번개 등 `DealDamage` 수확: 전과 같이 +1 하지 않음(+1은 수확기 몸 충돌 수확만). 주인(source)이 있으면 현재 콤보 배율은 받음.

**컴파일:** `refresh_unity`(compile) 후 `read_console` 에러 0건.

**확인할 점**
- **밸런스 영향 큼:** 예전엔 바에 닿을 때마다 0이라 콤보가 한 바퀴분(0~몇)이었는데, 이제 놓칠 때까지 계속 쌓여 점수 배율 ×(1+콤보×0.1)이 크게 오를 수 있음. 보스 체력·가격은 옛 규칙으로 맞춘 값이라 BalanceBot 재측정 필요(콤보 상한 또는 `comboFactor` 하향 검토 — 사용자 결정).
- 플레이: 바로 받으면 HUD 콤보 유지, 바를 놓쳐 바닥에 닿으면 0, 분열체가 바닥에 닿아도 유지되는지.
- 회전 꼬마·스킬 피해 수확도 +1로 칠지(지금은 안 침).

### [재설정] T2 — opus (조사만, 수정 없음)

**결론: 아니오.** 모바일에서 수확기(Harvester 인스턴스) 분신을 만드는 스킬은 **분열 하나뿐**이다.
- `SpawnHarvester` 호출은 `HarvestField.BeginRound`(메인 + `ExtraHarvesters`=장인의 도구 `harvester_slot`, 상시·같은 크기·Temporary 아님)와 `SkillEffects.Split`(`:207`, `buff_split_duration`, Temporary 분열체) 두 곳뿐.
- 분신술(`clone_bar_duration`/`clone_bar_count`)은 **바**를 복제(`SkillEffects.SpawnCloneBar`), 회전 칼날(`orbit_harvester_count`)은 Harvester가 아닌 그림(`MakeRect`)이 메인 둘레를 돌며 `DealDamage`.
- `SkillProgress.cs` ability 상수에도 수확 시 수확기를 소환하는 PC `clone`(분신 수확기)에 해당하는 키가 없음. → PC의 split/clone 중복은 모바일로 오지 않았다.
- 남는 것은 [이론] T2·[검증1] T2의 *보이는* 겹침(분열/분신술 이름·표 구조, 회전 꼬마 그림 = 수확기 그림, `colony_count`·`gold_conversion` 계단)뿐이며, 고칠지는 사용자 결정. 수정안이 필요하면 그쪽 안(분신술 → "분신 바" 이름 변경, 꼬마 그림 교체)을 쓴다.

### [재설정] T3 — opus (수정 없음)

- 모바일 결과 팝업 "다시 하기"(`ResultPopup.cs:16,27,55`)는 `SceneManager.LoadScene(현재 씬)`일 뿐이다. 팝업이 열리기 전에 `HarvestGame.Finish`가 이미 `StageProgress.Complete`를 불러 진행을 저장하므로, 이 버튼은 "같은 판 재도전"이 아니라 **"다음 판 하기"**(일반 판 → 다음 스테이지, 보스 패배 → 파밍 판, 파밍 끝 → 보스 판)다.
- 보스 재도전 전용 버튼이 아니고 모든 결과에 있어야 하는 기본 진행 버튼(없으면 세계수로만 나갈 수 있음)이라, 보스 판으로 제한하면 일반 판 뒤 다음 판으로 바로 갈 길이 없어진다. → **모바일엔 같은 문제가 없어 수정하지 않음.**
- 참고(선택): 이름이 "다시 하기"라 보스 재도전으로 오해될 수 있음. 바꾼다면 프리팹 `ResultPopup.prefab:1411` 글자를 "다음 판" 등으로(사용자 결정). 보스 패배 뒤 바로 보스에 재도전하는 버튼은 T6 제안 3(강제 파밍 제거 + 재도전/파밍 선택)에서 다룬다.

### [재설정] T4 — opus

- `HarvestField.cs:702` `bool golden = !stats.BossRound && stats.GoldenChance > 0f && Random.value < stats.GoldenChance;` 가드 추가. 보스 판에서만 동작이 달라지고(지금도 보스 판엔 이 경로가 안 불려 사실상 방어 코드), 파밍 판(BossActive=false)의 황금은 그대로 둠(밸런스 영향 — 사용자 결정 대기).
- 컴파일: T1과 같이 확인, 에러 0건.
- 확인할 점: 사용자가 본 황금이 파밍 판이었다면 이 가드로는 사라지지 않음. 파밍 판까지 빼려면 조건을 보스 스테이지 전체로 바꾸고 BalanceBot 재측정.

### [재설정] T5 — opus

**최종 권장: T6 채택 → C안, T6 미채택 → B안.** (T6 채택을 권장하므로 기본값은 C)

| 지급처 | C안 (기본) | B안 (T6 없을 때) |
|---|---|---|
| 보스 10·20·30·40·50 첫 처치 | 10·15·20·25·30 (지금 표 그대로) | 15·20·25·35·25 |
| ★ 5·15·25·35·45 첫 클리어 | 3·4·5·6·7 | — |
| 총량 | 125 | 120 |

**누적 균사석 vs 필요량** (필요: 3001 Lv2~10 누계 1·2·3·5·7·9·12·15·19 / 3235 누계 2·5·8·12·18 / 3143 누계 3·7·12·18·25 / 3339 누계 5·11·19·30)

| 스테이지 끝 | 5 | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 | 50 |
|---|---|---|---|---|---|---|---|---|---|---|
| C 누적 | 3 | 13 | 17 | 32 | 37 | 57 | 63 | 88 | **95** | 125 |
| B 누적 | 0 | 15 | 15 | 35 | 35 | 60 | 60 | 95 | **95** | 120 |

- ~20 (사냥 준비 3001 19개 다 사는 시점): C 32 / B 35 → 둘 다 충족, 남는 13~16으로 3235 Lv4까지.
- ~45 (3339 열림, 3341 = 2,600만 골드 기준 사람 44~46): 3001+3235+3143 = 62는 C에선 35스테이지(63)에 이미 완료, 45에서 95 − 62 = 33 ≥ 30 → 3339 Lv2~5 즉시 전부. 여유 3. B도 40에서 95로 같음.
- 50 처치분(C 30 / B 25)은 남음 → 세계수 시트용 예비(결정 ③).

**첫 클리어 규칙 (공통)**
- `StageProgress`에 키 1개 `Stage.Cleared` = 처치/클리어한 가장 높은 스테이지. 보스·★ 균사석은 `stage > Cleared`일 때만 지급, 지급 후 `Cleared = stage`. 보스용 `BossCleared`를 따로 두지 않아도 됨(스테이지는 순서대로만 넘어가므로 한 키로 충분).
- 파밍(직전 스테이지 일반 판)·50보스 반복 처치·★ 재도전 → 0.
- 기존 세이브 이관: 키가 없으면 `Cleared = Current − 1`로 초기화(이미 받은 보스 중복 지급 방지).
- `BalanceBot.cs`(163행) 지급 규칙도 같게.
- 보이기: 결과 팝업 균사석 줄 분리("첫 클리어 보상 · 균사석 +N", 카운트업) — 고정 UI라 풀 규칙 무관.

**문서 정정 메모:** `HANDOFF.md` 189·248행이 아직 "3341 = 1억 2천만"이라 낡음 → 실제 표는 26,000,000. 구현 커밋에서 같이 고친다. `bb_final_*` 봇 기록은 저장소에 없어 미검증 → 구현 후 봇으로 "45에서 3339 Lv5 구매 가능"을 다시 확인.

### [재설정] T6 — opus

**산식 정리 (검증1 지적 반영: 표와 본문을 한 표로 통일)**
- 판 수 가정: 1~3 튜토리얼(통과 100%). 표 적용 일반 스테이지 = 1묶음 4~9(6개) + 2~5묶음 x1~x9(각 9개) = 42개. 역할별 개수: x1~3 **12**(1묶음 제외), x4 **5**, ★x5 **5**, x6~7 **10**, x8~9 **10**.
- 추가 판 기대값 = 스테이지마다 Σ(실패 확률 누적곱). 패널티 없는 재도전, 완화 없으면 1/p − 1.
- 완화(연속 실패마다 목표 ×0.9, 최저 0.7) 반영: 사람 수확 수가 정규분포, **변동계수 CV 0.2 가정**(측정 전). 목표 ×0.9^k이면 실패 분위 z_k = m·z0 + (m − 1)/CV, m = max(0.9^k, 0.7).
- 목표 시간: 지금 76분 − 강제 파밍 제거 5 − 처치 보너스 1.5 = **69.5분** → 90분까지 **20.5분 ≈ 15.2판**(1.35분/판) 필요.

**옛 표 다시 계산 (90·85·55·90·80)**: 완화 없음 9.9판 = 13.4분 → **82.9분**, 완화 포함 8.3판 → 80.7분. 목표 미달 확인.

**새 통과율 표 (기본: 완화 넣음)**

| 스테이지 | 첫 시도 통과율 | 목표 분위수 | `mushHpMul` | 추가 판/스테이지 (완화 없음) | 추가 판/스테이지 (완화, CV 0.2) |
|---|---|---|---|---|---|
| 1~3 | 100% (고정 3·4·5개) | — | 1.0 | 0 | 0 |
| x1~x3 | 85% | p15 | 1.0 | 0.176 | 0.162 |
| x4 | 75% | p25 | 1.1 | 0.333 | 0.286 |
| **★x5** | **35%** | p65 | 2.0 | 1.857 | **1.022** |
| x6~x7 | 85% | p15 | 1.0 | 0.176 | 0.162 |
| x8~x9 | 60% | p40 | 1.3 | 0.667 | 0.506 |

- ★x5 완화 시 시도별 통과율: 35% → 56% → 74% → 86% → 89% (추가 = 0.65 + 0.65·0.439 + 0.285·0.262 + … = 1.02판).
- 묶음당 추가 판(완화): 2~5묶음 3·0.162 + 0.286 + 1.022 + 2·0.162 + 2·0.506 = **3.13판**, 1묶음(4~9) **2.64판**.
- 합: 2.64 + 4 × 3.13 = **15.2판 × 1.35 = 20.5분 → 69.5 + 20.5 = 90.0분.** CV 0.15면 88.9분, 0.3이면 91.5분.
- 같은 표를 완화 없이 쓰면 21.5판 → 98.5분(넘침). **완화를 빼기로 하면** 대신 85·80·**45**·90·70 표: 12·0.176 + 5·0.25 + 5·1.222 + 10·0.111 + 10·0.429 = 14.9판 = 20.1분 → **89.6분**.
- 손잡이(완화 표 기준): ★ 통과율 ±5%p ≈ ∓0.6판 ≈ ∓0.85분 / x8~9 ±5%p ≈ ∓0.8판 ≈ ∓1.1분 / 판당 시간 ±0.1분 ≈ ±1.5분(추가분만).
- 주의: 실패 판도 골드 100%를 주므로 재도전 판이 다음 구매를 조금 앞당김 → 실제 추가 시간은 위보다 약간 짧을 수 있음. 분위수는 `mushHpMul`을 먼저 넣은 상태에서 `BalanceBot` 사람 모델로 측정, 측정 후 통과율만 다시 맞춘다.

**용어 정정 — `bossHitShare`**: 상한(cap)이 아니라 **기본 비율**이다. 보스 체력 전부를 깎았을 때 받는 타격 점수 = 직전 일반 판 점수 × `bossHitShare`가 되도록 보스 체력을 정하는 비율. C# 기본값 0.6(`InGame.unity`에 직렬화 안 됨) → 제안 기본값 0.35. 덜 깎으면 비례해서 덜 받는다.

나머지(목표 종류=수확 N개, 판 계속, 패널티 제거, `bossRewardMul` 3, 일반 버섯에만 `mushHpMul`)는 [이론] T6 그대로.

### [재설정] T5·T6 — 사용자 결정 목록 (권장 기본값)

| # | 결정 | 권장 기본값 |
|---|---|---|
| 1 | T6 단계적 허들 채택 여부 | 채택 |
| 2 | 균사석 안 (T6 채택 시 C / 미채택 시 B / A 골드화 별도) | C (보스 10·15·20·25·30 + ★ 3·4·5·6·7), A는 안 함 |
| 3 | 균사석 첫 클리어만 지급(`Stage.Cleared` 1키), 50보스·파밍·재도전 반복 지급 | 첫 클리어만, 반복 0 |
| 4 | 남는 균사석(약 30)을 세계수 시트용 예비로 둘지 | 둠 |
| 5 | 일반 스테이지 목표 종류 | 수확 N개 |
| 6 | 목표 달성 시 | 판은 시간 끝까지 계속 + "목표 달성!" |
| 7 | 통과율 표 | 85·75·★35·85·60 (완화 포함 90.0분), 측정 후 조정 |
| 8 | 연속 실패 완화(목표 ×0.9, 최저 0.7) | 넣음 (빼면 표를 85·80·45·90·70으로) |
| 9 | 스킬에 스테이지 잠금 추가 | 안 함 |
| 10 | 보스 패배 패널티 | `failGold` 1.0, 강제 파밍 0 + "재도전/파밍" 선택 버튼 |
| 11 | 끈기(진 판마다 보스 피해 ×1.05, 상한 ×1.5) | 넣지 않음 (먼저 측정, 벽이 보이면 추가) |
| 12 | `bossHitShare` 기본 비율 / `bossRewardMul` | 0.35 / 3 |
| 13 | 지표 P15 "진 판 ≥ 일반의 0.5배" 하한 | 포기 (패널티 제거가 사용자 원안) |

### [검증2] T1 — sonnet
- 맞음: 콤보 카운터는 `HarvestField.Combo` 하나. `Harvester.Combo`·`h.Combo` 참조는 Assets 전체(BalanceBot·SkillEffects·StageHud·RoundStats·SkillProgress 포함)에서 0건(남은 "Combo"는 `comboFactor`/`ComboMul`/`ComboRate` 계수뿐). Editor 스크립트 폴더 없음.
- 맞음: 초기화는 `BeginRound`(:174, Start·Restart 공통), 바닥 벽 분기(:359)에서 `!h.Temporary`일 때만 0, 수확 시 `SetCombo(Combo+1)`(:421). 바 충돌·`Launch`에서는 안 끊음(삭제 확인). 풀 재사용 시 Temporary는 `OnSpawned`(Harvester.cs:82)에서 false로 초기화, 분열체는 스폰 직후·다음 갱신 전에 true로 세팅돼 문제없음.
- 맞음: `ComboChanged` 구독은 `HarvestGame.Awake` 1회(:55). field·hud·game 모두 같은 씬 오브젝트라 씬 재로드 시 같이 파괴되어 누수/중복 없음. Restart는 구독 추가 없이 `SetCombo(0)`만 호출. 값 같으면 이벤트 생략.
- 맞음: HUD(이벤트→`hud.SetCombo`)와 점수 배율(`field.Combo`)이 같은 값.
- 맞음: 보스 판 — 일반 버섯 콤보는 동일하게 적용, 보스 피격은 콤보를 올리지 않고(보스 `Harvested`는 처치 때만, 점수는 콤보 배율 안 씀) 보스 처치 경로는 콤보 계산 전에 return. 동작 합리적.
- 보완(경미): 시작 시 `BeginRound`의 `SetCombo(0)`은 이미 0이면 이벤트가 안 나가는데, HUD도 `SetCombo(0)`으로 시작하므로 무해. 바닥 벽 판정이 바 판정보다 먼저라, 바가 바닥 벽에 닿을 만큼 낮으면 같은 프레임에 받아도 끊길 수 있음 — 바는 벽 위에 있으므로 실제 영향 없음(플레이 확인만 권장).
- 컴파일: Unity `read_console` 에러 0건.

### [검증2] T2–T4 — sonnet
- T2 맞음: `SpawnHarvester` 호출은 `HarvestField.BeginRound`(:176,179)와 `SkillEffects.Split`(:207) 두 곳뿐. 다른 수확기 복제 경로 없음.
- T3 맞음: `ResultPopup.cs:55` `Retry() => SceneManager.LoadScene(gameObject.scene.name)` — 현재 씬 재로드. 진행 저장은 `HarvestGame.Finish` 쪽에서 이미 되므로 "다음 판 하기"로 동작한다는 결론과 일치(파일 위치는 `Assets/Scripts/Mobile/InGame/ResultPopup.cs`, 로그의 `Mobile/` 경로는 InGame 하위).
- T4 맞음: `HarvestField.cs:702` `bool golden = !stats.BossRound && stats.GoldenChance > 0f && Random.value < stats.GoldenChance;` — 문법·연산 순서 정상, 컴파일 에러 없음. 파밍 판은 그대로(로그 설명과 일치).
- HANDOFF.md 4.1-7 문구 갱신 확인, 코드와 일치.

### [검증2] T5·T6 — sonnet
- T5 맞음(표): 누적을 다시 계산 — C 5/10/15/20/25/30/35/40/45/50 = 3·13·17·32·37·57·63·88·95·125, B = 0·15·15·35·35·60·60·95·95·120. 총량 125/120도 맞음. 필요량 3001=19, 3235=18, 3143=25, 3339=30 (합 92) 모두 가격 배열에서 재계산해 일치. "95−62=33 ≥ 30"도 맞음(여유 3). C는 35에 63≥62로 앞 셋을 이미 끝냄, B는 35에서 60 <62라 40에서야 충족(표 그대로).
- T5 보완(문구): "20에서 남는 13~16으로 3235 Lv4까지"는 3235 누계 2·5·8·12 → 4번 구매(Lv2~5)까지가 맞아 "Lv5까지"로 쓰는 게 덜 헷갈림. 본질은 영향 없음.
- T5 맞음(이관 규칙): `StageProgress.cs` 저장 키는 Stage.Current/Stage.FarmLeft/LastNormalScore/LastNormalGold뿐이고 `Stage.Cleared`는 없음. `Current`가 "지금 도전 중인 스테이지"이고 `Advance`가 처치·클리어 때만 올리므로 `Cleared = Current−1` 초기화는 타당. 보완 2건: (1) 이미 50을 깬 세이브는 `Advance`가 50에 머물러 Current=50 → Cleared=49가 돼 50보스 30개가 한 번 더 지급됨(중복 방지를 원하면 "FarmKey 등으로 구분 불가 → 허용하거나 50은 예외 처리"를 결정). (2) 개발용 `StageProgress.Set(stage)`는 Cleared를 안 바꾸므로 점프 후 건너뛴 보스·★가 지급되거나(앞으로 점프) 되돌아가면 재지급 안 됨 — Set이 Cleared=stage−1도 같이 쓰게 할 것. 지급 위치는 `HarvestGame.cs:149` 한 곳이라 구현은 단순.
- T6 맞음(개수): 스테이지 1~50, 보스 10·20·30·40·50, 튜토리얼 1~3 → 일반 비튜토리얼 4~49 중 보스 제외 = 42개. 1묶음(4~9) 6개 + 2~5묶음(11~19, 21~29, 31~39, 41~49) 각 9개. 역할별 x1~3 = 4묶음×3 = 12, x4 = 4+1 = 5, ★x5 = 4+1 = 5(5·15·25·35·45와 일치), x6~7 = 8+2 = 10, x8~9 = 8+2 = 10 → 12/5/5/10/10 맞음.
- T6 맞음(산술): 스크래치 pace.py 재실행 결과 new2(85·75·35·85·60) 완화 CV 0.2 = 추가 판 15.16, ×1.35 = 20.5분 → 69.5+20.5 = 90.0분. 묶음당 3.13판, 1묶음 2.64판, 합 2.64+4×3.13 = 15.2판 일치. CV 0.15→88.9, 0.3→91.5, 완화 없음 98.5도 일치. 완화 없는 표 85·80·45·90·70은 12·0.176+5·0.25+5·1.222+10·0.111+10·0.429 = 14.88판 = 20.1분 → 89.6분 맞음(손으로 따로 계산).
- T6 표기 흠: 본문 "2~5묶음 3·0.162 + … = 3.13판"에서 "2~5묶음"이 4개 묶음(11~49)이라는 점은 맞으나, 표 제목의 "x1~x3 … 12개"가 1묶음의 x4~9는 포함·x1~3은 제외한다는 설명이 한 줄 더 있으면 좋음.
- 결정 목록 일관성: 맞음. #1 채택 → #2 C(125), #7 표 = 완화 포함 90.0분, #8 "빼면 85·80·45·90·70" = 89.6분과 일치. #3 첫 클리어 1키, #12 0.35/3, #10 failGold 1.0과 [재설정] T6 본문 일치. 빠진 것: #3 구현 때 위 이관 보완 2건(50 재지급, Set)을 결정/명시할 것.
- T1 영향(중요, 보완): 콤보가 바 충돌로 안 끊기므로 점수 배율이 올라, T5·T6의 시간·골드 수치 중 옛 콤보 규칙 기준 측정값은 모두 재측정 필요. 해당: "지금 76분", "69.5분 = 76−5−1.5", 판당 1.35분은 시간이라 무관할 수 있으나 76분은 옛 점수·골드 수입으로 맞춘 가격·보스 체력(HP 0.234, 수입 ×0.6)에서 나온 값이라 수입이 오르면 같은 가격을 더 빨리 사 총 시간이 줄어든다(90분 목표에 미달 방향). 또 통과율을 정한 "목표 분위수·`mushHpMul`"은 수확 개수 기준이라 콤보와 직접 무관하지만, 보스 `bossHitShare` 0.35(직전 판 점수 대비)·`bossRewardMul`은 점수에 비례하므로 콤보 점수 증가가 일반 점수와 보스 점수에 같이 반영되는지 확인 필요. 균사석 표(T5)는 콤보와 무관해 영향 없음. 권고: T1 콤보 재측정(BalanceBot) 후 76분 기준과 CV 0.2 가정을 다시 맞추고 통과율 표는 그 뒤 조정.

### [관리] 최종 정리

| ID | 상태 | 결과 |
| --- | --- | --- |
| T1 | 수정·검증2 통과 | 콤보 카운터 하나(HarvestField.Combo). 수확 +1, 바닥 벽(임시 분열체 제외) 0, 바 충돌로는 리셋 안 함. 플레이 테스트·밸런스 재측정 필요 |
| T2 | 변경 없음 | 모바일에는 PC의 분열/분신 수확기 중복이 없음. 남은 겹침(이름·그림·상위 티어)은 사용자 결정 |
| T3 | 변경 없음 | 모바일 "다시 하기"는 다음 판 진행 버튼이라 항상 보여야 함. 라벨 변경("다음 판")은 사용자 결정 |
| T4 | 수정·검증2 통과 | 보스 판 황금 가드 추가. 파밍 판 황금은 그대로 |
| T5 | 설계 완료 | 추천 C(보스 10·15·20·25·30 + ★ 3·4·5·6·7, 첫 클리어만). 마이그레이션 보완 2건(50 클리어 저장, dev Set) 반영 필요 |
| T6 | 설계 완료 | 추천 통과율 85·75·★35·85·60 + 완화, 모델상 90분. T1로 점수 규칙이 바뀌어 BalanceBot 재측정 후 조정 |

커밋 없음. T5·T6 구현은 사용자 결정 후.

---

## 2차 작업 (2026-10-27): T5·T6 구현

### [관리] 사용자 결정

- 사용자: "T5·T6을 추천 기본값으로 구현해줘. 먼저 BalanceBot으로 새 콤보 규칙 재측정부터."
- 따라서 `[재설정] T5·T6 — 사용자 결정 목록` 1~13번을 모두 추천값으로 확정한다. 단, T6 통과율 표는 재측정 결과로 다시 맞춘다.
- [검증2] T5·T6의 보완 2건(50 클리어 저장의 마이그레이션, dev `Set(stage)`가 Cleared 갱신)도 구현에 포함한다.

| ID | 작업 | 담당 | 순서 |
| --- | --- | --- | --- |
| M1 | 새 콤보 규칙으로 BalanceBot 재측정 (코드 수정 없음) | opus | 1 |
| I5 | T5 구현: 첫 클리어 균사석 (보스 + ★) | opus | 2 |
| I6 | T6 구현: 일반 스테이지 목표·mushHpMul·보스 패널티 제거·선택 버튼·bossHitShare/RewardMul | opus | 2 (I5와 같은 에이전트, 파일 충돌 방지) |
| V1 | 구현 검증 | sonnet | 3 |
| M2 | 재측정 + 통과율 재조정 | opus | 4 |
| V2 | 최종 검증 | sonnet | 5 |

### [측정] M1 — opus

**방법**
- InGame 씬 플레이 모드에서 MCP `execute_code`로 `BalanceBot.Begin` → `Run(1)` 반복(판마다 콤보 통계를 따로 셈) → `Save`. 문서 사람 모델: SKILL 1.0, 수입 ×0.6, 보스 피해 ×0.234, 판 밖 60초. 시드 1~4, 최대 80판(시드 1은 250/100판).
- 콤보 통계는 `field.ComboChanged`·`Harvested`·`BarHit` 이벤트를 측정 코드에서 구독해 셈(게임 코드 수정 없음).
- **옛 규칙 재현(old):** 측정 코드에서 영구 수확기가 바에 닿을 때마다 리플렉션으로 `SetCombo(0)`. 옛 코드는 수확기마다 콤보가 따로였으므로 수확기가 여럿인 후반엔 옛 수입을 조금 **낮게** 잡는다(초반 수확기 1개 구간은 같음).
- **후보(f05c20):** 실행 중에만 `Base.comboFactor = 0.05` + 콤보가 20을 넘으면 20으로 되돌림. 끝나고 0.1로 복구 확인(`factorRestored=0.1`).
- 출력: 스크래치 `.../scratchpad/m1/*.csv`, 분석 `an.py`. 저장소 변경 없음(`git status` 시작 때와 같음, 씬은 Start로 되돌림).

**봇이 새 콤보를 모델링하는가:** 그렇다. 봇은 바를 0.5초마다 바뀌는 오차로 쫓아 실제로 놓치고, 놓친 수확기는 바닥 벽에서 튕겨 콤보가 0이 된다(바닥 벽은 수확기를 잃지 않고 튕기기만 함 → 놓침의 비용은 콤보뿐). 무한히 쌓이지는 않지만, 바·수확기가 커지는 후반엔 놓침이 드물어 콤보가 수백까지 간다.

| 새 규칙 사람 모델 놓침 | st5 | st15 | st25 | st35 | st45 |
|---|---|---|---|---|---|
| 놓침/(놓침+바 받음) | 16% | 17% | 11% | 7% | 7% |
| 판당 놓침 횟수(시드 1 구간 평균) | 2.6 | 2.7 | 4.0 | 5.5 | 6.6 |

**결과 (시드 평균, 첫 일반 판 기준)**

| 스테이지 | 판 시간 | 규칙 | 평균 콤보(수확 가중) | 최대 콤보 | 점수 | 골드 | old 대비 골드 |
|---|---|---|---|---|---|---|---|
| 5 | 15초 | old | 2.1 | 5 | 2.9e3 | 3.2e3 | 1 |
| | | **new** | 6.8 | 16 | 5.9e3 | 6.4e3 | **×2.0** |
| | | f05c20 | 7.5 | 15 | 3.8e3 | 4.1e3 | ×1.3 |
| 15 | 15~16초 | old | 3.0 | 8 | 4.2e4 | 4.8e4 | 1 |
| | | **new** | 9.2 | 22 | 9.9e4 | 1.16e5 | **×2.4** |
| | | f05c20 | 9.2 | 18 | 5.2e4 | 6.0e4 | ×1.25 |
| 25 | 18~21초 | old | 5.1 | 17 | 8.5e5 | 1.0e6 | 1 |
| | | **new** | 31 | 76 | 3.8e6 | 4.5e6 | **×4.4** |
| | | f05c20 | 14.9 | 20 | 1.2e6 | 1.4e6 | ×1.35 |
| 35 | 27~30초 | old | 8.3 | 34 | 8.1e7 | 1.1e8 | 1 |
| | | **new** | 114 | 308 | 1.6e9 | 2.4e9 | **×21** (구매가 앞서 강해진 몫 포함) |
| | | f05c20 | 18.2 | 20 | 1.0e8 | 1.4e8 | ×1.2 |
| 45 | 30초 | old | 8.7 | 43 | 3.4e8 | 5.1e8 | 1 |
| | | **new** | 121 | 382 | 3.1e9 | 4.6e9 | **×9** |
| | | f05c20 | 18.3 | 20 | 3.4e8 | 5.1e8 | ×1.0 |

| 지표 | old (재현, 4시드) | **new (4시드)** | f05c20 (3시드) | 옛 기준(HANDOFF 6.1) |
|---|---|---|---|---|
| 50보스 첫 도전까지 | 102~107분 (1시드는 80판 안에 못 감) | **71~75분 (평균 72.9)** | 86~103분 (평균 95) | 50보스 처치 중앙값 76분 |
| 3341(2,600만) 구매 | 80~86분 (60~65판) | **43~47분 (33~36판)** | 67~77분 (51~58판) | — |
| 3339 1레벨(균사석, 3341 뒤) | 3341과 같은 판 | 3341과 같은 판(시드 3만 62분) | 3341과 같은 판 | — |
| 스킬 트리 전부 구매 | 68~73판 | **35~37판** | 56~65판 | — |
| 보스 10 실패 | 1·1·1·1 | 0·0·0·0 | 0·0·0 | 13% |
| 보스 20 실패 | 0·1·1·0 | 0·0·0·0 | 0·0·1 | 13% |
| 보스 30 실패 | 8·9·7·9 | 2·1·2·1 | 7·5·8 | 31% |
| 보스 40 실패 | 0 | 0 | 0 | 44% |
| 보스 50 | **못 잡음** | **못 잡음** (시드 1: 65연패) | **못 잡음** | 52% |

**보스 50 벽 (콤보와 무관, 중요):** 세 규칙 모두 50보스를 한 번도 못 잡았다(117회). 실제 보스 체력 = 16,864 ÷ 0.234 = 72,068, 봇 판당 피해 평균 42,003, 최대 55,198(트리 전부 산 상태). 보스 피해는 콤보·골드와 무관(트리가 다 차면 더 강해질 방법 없음). 보스 30도 old 재현에서 시도당 처치 약 11%(피해 평균 6,482 / 필요 8,842)로 기준 31% 실패보다 훨씬 어렵다. **지금 코드는 옛 기준(76분, ddffddf에서 측정)을 콤보와 상관없이 재현하지 못한다.** 의심 원인: 기준 측정 뒤 커밋 `e5f377e`(바 원래 크기를 Awake 순서와 상관없이 저장) — 그 전엔 측정 플레이 모드에서 바가 폭 0으로 필드 가운데에 놓였을 수 있고, 그러면 수확기가 위쪽 절반만 오가 보스를 훨씬 자주 때린다. 지금 바는 정상(폭 600, y −492). 확인 안 함 — M2가 `ddffddf`와 지금 코드로 같은 시드를 돌려 확인 필요.

**판단**
- 새 규칙 그대로(0.1, 상한 없음)는 **받아들일 수 없다.** 골드가 초반 ×2, 중반 ×4, 후반 ×9~21. 3341과 트리 전체를 옛 대비 절반 판 수(약 35판)에 사고, 50보스 도착이 약 30분 빨라진다. 이후 시간은 전부 보스 벽에서 나옴 → 90분 목표를 "돈"이 아니라 "벽"이 만든다.
- 콤보 배율이 후반에 ×10~13(평균 콤보 100~120)이 되는 건 바·수확기 업그레이드로 놓침이 7%까지 줄기 때문. 상한 없이 계수만 낮추면(예: 0.02) 초반 ×1.13, 후반 ×3.4로 곡선이 오히려 뒤로 쏠림 → **상한이 필요.**
- **권장: `comboFactor` 0.05 + 콤보 배율 상한 20 (점수 배율 최대 ×2.0).** 측정 효과: 골드 old 대비 ×1.2~1.35(st45 ×1.0), 3341 구매 67~77분(old 재현 80~86분, new 43~47분), 50보스 도착 평균 95분(old 104, new 73). old 재현이 후반 옛 수입을 조금 낮게 잡으므로 실제 옛 규칙과 거의 같은 수준.
- 상한 구현 제안: HUD 콤보 수는 계속 오르게 두고 점수 계산만 `Mathf.Min(combo, comboCap)`(`RoundBase.comboCap = 20`) — 플레이어는 콤보를 계속 보고, 배율만 ×2에서 멈춤. 피버 `ComboMul`·`combo_rate`는 계수에 곱해지므로 그대로 둬도 상한 ×2를 넘는 몫은 그 스킬 효과로 남음.
- 대안(측정 안 함): 0.1 + 상한 10(최대 ×2.0) — 후반은 같고, 초반(평균 콤보 7~9)이 ×1.7~1.9로 f05c20보다 셈.
- **먼저 할 일:** 보스 50(그리고 30) 벽 원인을 확인하고 보스 체력을 다시 맞춘 뒤에 콤보·통과율을 맞출 것. 벽이 그대로면 총 시간은 정의되지 않는다(무한 재도전).

### [관리] M1 결과 반영

- 콤보: M1 추천대로 **comboFactor 0.05 + 점수용 콤보 상한 20**(HUD 숫자는 계속 셈)을 구현에 포함한다. 되돌리기 쉬운 수치라 사용자에게 보고 후 조정 가능.
- 보스 50 벽(117회 전패)·보스 30 고실패는 콤보와 무관한 기존 문제. 보스 체력 재피팅은 M2에서 구현 후 함께 한다.
- 다음: I5+I6+콤보 상한을 한 에이전트(opus)가 구현.

### [구현] I5·I6·콤보 상한 — opus

**바꾼 파일 (핵심 줄)**
- `RoundStats.cs` — `RoundBase.comboFactor` 0.05, `comboCap` 20(:19), `bossHitShare` 0.35, `bossRewardMul` 3, 새 헤더 "일반 스테이지 목표" `goalRelief` 0.9(:53)·`goalReliefMin` 0.7·`hardTint`. `RoundStats.ComboCap`(:175), `MushHpMul`(:101, 보스 체력엔 안 곱함), `Goal`·`HasGoal`, `GoalFor(stage, failStreak, base)`(:106, 보스 스테이지·goalType None이면 0, 완화 max(0.7, 0.9^k), 반올림 최소 1).
- `HarvestField.cs:704-707` — `SpawnMushroom`에서 일반 버섯 체력 × `MushHpMul`, >1이면 색 × `hardTint` (같은 풀 프리팹, Instantiate 없음). 보스 스폰 경로(:575)는 그대로.
- `HarvestGame.cs` — 점수용 콤보 `Mathf.Min(field.Combo, ComboCap)`(:125, HUD는 `ComboChanged` 그대로 실제 값). `GoalMet`·`FarmRound`·`StageCleared`(:46 = 보스 처치 / 파밍 아닌 일반 판 목표 달성)·`MyceliteEarned`. `Prepare(..., failStreak = 0)`(:84 Goal 설정, :87 HUD). 수확마다 목표 HUD 갱신, 달성 순간 1번 "목표 달성!"(:143). `Finish`: 균사석 = `StageProgress.FirstClearGem(..., StageProgress.Cleared)`(:168) 후 `Complete(boss, StageCleared, …)`. 결과 문구 "목표 N개 달성"/"목표 n/N · 다시 도전"/"보스 처치"/"보스 실패", 팝업에 균사석·보스 패배 전달.
- `StageProgress.cs` — 키 `Stage.Cleared`·`Stage.FailStreak`. `Cleared`(없으면 `MigrateCleared` :43), `FirstClearGem`(:61), `Complete`(:97: 보스 패배 → 그대로(강제 파밍 없음), 일반 판 목표 실패 → FailStreak+1, 성공 → Advance), `ChooseFarm`(:116, FarmLeft = `FarmRounds` 1), `Advance`가 Cleared·FailStreak 갱신, `Set(n)`(:132) Cleared = n−1·FailStreak 0.
  - **50 클리어 세이브 처리:** 옛 코드는 50 처치 후에도 Current=50·FarmLeft=0이라 저장 키로 구분 불가. 옛 규칙에선 균사석이 보스 처치에서만 나왔으므로 Current가 마지막이고 (지갑 균사석 + 균사석 노드에 쓴 합 `SkillProgress.Spent(Mycelite)` :127) > 50 전 보스 gem 합(70)이면 Cleared = 50. 편집 모드 확인: 60·70 → 49, 71·101 → 50. 사용자 실제 세이브(Current 22, 균사석 25) → 21.
- `SkillProgress.cs:127` — `Spent(currency)`.
- `StageHud.cs:121` — `goalText`·`goalMetColor`, `SetGoal(count, goal)`: 0이면 숨김, "수확 n/N" → "목표 달성! n/N"(초록).
- `ResultPopup.cs` — `Show(..., mycelite, bossLost)`(:44): 균사석 줄(보라, "첫 클리어 보상 · 균사석 +N" 카운트업, 0이면 숨김). 보스 패배면 `ShowChoice`(:61)가 버튼 셋(재도전 / 파밍 / 세계수로, 폭 230, x −245·0·245)으로, 아니면 프리팹 배치·"다시 하기". `Farm`(:105) = `ChooseFarm` + 씬 다시.
- `BalanceBot.cs` — `cleared`·`failStreak` 상태, `Prepare(..., failStreak)`, 진행 규칙 게임과 동일(:164~), 균사석 `FirstClearGem`(:168), `Config.farmRounds` 기본 0(= 재도전 선택). CSV에 `goal,goalMet,failStreak` 열 추가(끝에 붙임 — 기존 열 순서 그대로).
- `TableRows.cs` — `StageData.goalType`(`StageGoal { None, Harvest }`)·`goal`·`mushHpMul`·`gem`.
- 프리팹 `Prefabs/Mobile/ResultPopup.prefab` — `Content/Mycelite`(Count 복제, y −350, 꺼진 채), `Content/FarmButton`(RetryButton 복제, "파밍", 꺼진 채), `myceliteText`·`farmButton`·`retryLabel` 연결.
- 씬 `InGame.unity` — HUD `InGameArea/Goal`(Buffs 복제 TMP, 왼쪽 위 (30, −340), 44pt) → `StageHud.goalText`. **RoundBase 직렬화 결정: 씬에 직렬화**(이전엔 bossHitShare·bossRewardMul이 빠져 C# 기본값이 쓰였음). 지금 씬에 comboFactor 0.05·comboCap 20·bossHitShare 0.35·bossRewardMul 3·goalRelief 0.9·goalReliefMin 0.7·hardTint가 모두 있고 C# 기본값도 같게 맞춤. HANDOFF 4.4에 "씬 값 우선, 인스펙터에서 고칠 것" 명시.
- `HANDOFF.md` — 4.1-7 콤보 상한, 4.1-9 목표, 4.1-10 균사석·이관, 4.2 표, 4.4 보스(0.35·×3·failGold 1·재도전/파밍·직렬화·50 벽), 6 Stage/Boss 열, 6.1 "3341 = 26,000,000", 7 ResultPopup, 8 키·봇 규칙, 9-1·9-2 갱신.

**표 바꿈** (원본 백업: 스크래치 `Stage_before.xlsx`·`Boss_before.xlsx`, `MoreMush/Data/Reimport Tables` 실행 → Stage 50행·Boss 5행 OK, `.bytes` 갱신)
- `Stage.xlsx`: `boss` 뒤에 `goalType`·`goal`·`mushHpMul`·`gem` 4열 삽입(1~3행 이름·자료형·설명). 보스 스테이지는 비움. `gem`: 5·15·25·35·45 = 3·4·5·6·7. `mushHpMul`: x4 1.1, x5 2.0, x8~9 1.3.
- `Boss.xlsx`: `failGold` 전부 1(0.8 → 1), 설명 칸 갱신. `gem` 10·15·20·25·30 그대로.

**임시 목표 N (M2가 다시 맞출 것)** — 근거: M1 `f05c20_s1~3.csv`(새 콤보 규칙 후보와 같은 수치)에서 스테이지별 첫 일반 판 `harvested` 평균 m. N = round(m × mushHpMul^−0.5 × (1 + z·0.2)), CV 0.2 가정, z = 통과율 분위(85% → −1.036, 75% → −0.674, 35% → +0.385, 60% → −0.253). 체력 보정 ^−0.5는 근거 없는 가정.

| 1~9 | 3·4·5·16·**19**·23·30·25·26 |
|---|---|
| 11~19 | 30·29·29·31·**30**·33·37·44·57 |
| 21~29 | 63·68·77·90·**79**·98·109·122·107 |
| 31~39 | 341·382·406·436·**430**·458·494·588·585 |
| 41~49 | 728·727·717·739·**678**·704·706·746·747 |

- 봇 확인(시드 1, 사람 모델, 40판): ★5 수확 19/19, ★15 30/30 — 체력 2.0에서 수확 감소가 ×0.76~0.78로 가정(×0.71)보다 작아 ★ 통과율은 35%보다 높을 가능성. 28스테이지에서 1회 실패 → 재도전 목표 110(×0.9) 통과. 나머지는 여유 있게 통과 → 1시드 기준 일반 목표가 전체적으로 쉬운 편. 30보스는 10연패(M1의 보스 벽과 같은 문제, 봇은 계속 재도전).

**컴파일·스모크**
- `read_console` 에러 0 (두 번 컴파일). 타입 존재 확인.
- 플레이 모드(사용자 세이브 백업 후 → 끝나고 원복: Current 22·FarmLeft 0·LastNormal*·Wallet 금액, `Stage.Cleared`·`Stage.FailStreak` 키 삭제 = 원래 상태; 백업 `scratchpad/prefs_backup.tsv`). 에디터가 뒤에 있어 프레임이 멈춰서 `Application.runInBackground = true`(런타임만, 저장 안 됨)로 돌림.
  - 일반 판 5스테이지: goal 19·mushHpMul 2·comboCap 20·share 0.35·×3 확인. HUD "수확 0/19" → 298스텝에 "목표 달성! 19/19", 판은 끝까지(수확 37). Cleared 4 → 5, Current 6, 균사석 +3, 결과 팝업 균사석 줄 켜짐.
  - 보스 판 10스테이지(BossHpMul 1000으로 강제 패배): HUD 목표 숨김, 골드 감액 없음(gold = total), 균사석 0, Current 10·BossActive 유지·FarmLeft 0. 팝업 "재도전 / 파밍 / 세계수로"(230폭, −245/0/245). 파밍 버튼 → FarmLeft 1·BossActive false·씬 다시 → 같은 스테이지 파밍 판(goal 0, StageCleared false).
  - BalanceBot 40판 정상(`scratchpad/i56_smoke_s1.csv`). 플레이 모드 종료, 열린 씬은 원래대로 Start(저장 안 된 변경 없음).
- 처음 결과 문구가 "목표 달성 37/19"로 어색해 "목표 19개 달성"으로 고침(그 뒤 재컴파일만 확인, 팝업 재확인은 안 함).

**V1이 볼 것**
1. `StageProgress.Complete`·`Advance`·`MigrateCleared` 순서: `Finish`에서 `FirstClearGem(..., Cleared)`가 `Complete` 전에 불려 판 전 값을 쓰는지, 첫 접근 이관이 `Advance` 전에 일어나는지.
2. 파밍 판(보스 스테이지 일반 판)이 목표·균사석·Cleared를 건드리지 않는지, 파밍 판 결과 팝업은 "다시 하기"(다음 = 보스)인지.
3. 50 이관 판정(받은 균사석 > 70)이 맞는 가정인지 — 옛 코드에서 균사석이 보스 외에 들어온 경로가 없었는지.
4. 실기기·Game 뷰로 HUD `Goal`(왼쪽 위 −340)이 Buffs(−305)·콤보와 겹치지 않는지, 결과 팝업 균사석 줄(y −350)이 Count 줄·버튼과 겹치지 않는지 (스크린샷 확인 안 함).
5. `BalanceBot` CSV 새 열이 기존 분석 스크립트(an.py 등)와 맞는지(끝에 붙임). `ProbeBoss` 동작 그대로인지.
6. HUD 콤보는 상한 없이 오르고 점수만 ×2에서 멈추는지(`HarvestGame.cs:125`), 피버 `ComboMul`은 상한과 별개로 곱해짐(의도, M1 제안대로).

**남은 것:** 목표 N·★ 통과율·보스 체력(30·50 벽)은 M2. SkillTree 스테이지 글자에 ★/목표 표시는 안 넣음(선택 사항). 커밋 안 함.

### [검증1] I5·I6·콤보 상한 — sonnet

읽기 전용 검증(코드·표·씬 수정 없음, 플레이 모드 안 들어감). 컴파일: `read_console` 에러 0건. 보스 체력·목표 N은 M2 몫이라 제외.

**콤보 상한**
- 맞음: `RoundStats.cs` comboFactor 0.05·comboCap 20, 씬 직렬화 값도 같음(InGame.unity RoundBase). 점수 배율은 `HarvestGame.cs:124-125`에서 `Min(field.Combo, ComboCap)`만 적용, HUD는 `ComboChanged → hud.SetCombo`(:60)로 실제 값. 피버 `ComboMul`은 계수에 곱해져 상한과 별개(M1 제안대로).
- 맞음: `Harvester.Combo` 제거 후 `.Combo` 남은 참조 없음(컴파일 OK). 판 시작 `BeginRound` SetCombo(0), 분열체(Temporary)는 바닥에 닿아도 안 끊음(`HarvestField.cs:~358`).
- 보완(낮음): 상한 20 컷은 `comboFactor` 0.05×20 = 최대 ×2.0. 스킬 `ComboRate`가 계수에 곱해지므로 ×2.0을 넘을 수 있음(의도, HANDOFF 4.1-7은 "점수 배율 최대 ×2.0"이라 적어 정확히는 "스킬 전"이라고 덧붙이면 좋음).

**T5 균사석 (StageProgress.cs)**
- 맞음: 첫 클리어 규칙 `FirstClearGem`(:61) `stage <= clearedBefore`면 0. `HarvestGame.Finish`(:168)에서 `StageProgress.Cleared`를 `Complete` 전에 읽음 → 판 전 값 사용, 첫 접근 이관도 `Advance` 전에 일어남(인자 평가 시점). 파밍 판(StageCleared=false)·재도전·목표 실패·50보스 재처치(stage 50 <= Cleared 50) 모두 0.
- 맞음: `Set(n)` = Cleared n−1·FailStreak 0·FarmLeft 0 (:132). 개발 메뉴 `InGameDebugMenu.cs:22,29`가 이것을 씀.
- 맞음: 균사석 총 125 = 보스 100(10+15+20+25+30) + ★ 25(3+4+5+6+7). 표 확인(Boss gem 10/15/20/25/30, Stage gem은 xlsx 5·15·25·35·45 행).
- 맞음: BalanceBot `BalanceBot.cs:164-185`가 같은 규칙(`FirstClearGem`·cleared 갱신·failStreak·파밍 판 farmLeft−1). 팝업 균사석 줄·카운트업 `ResultPopup.cs`.
- 이관 `MigrateCleared`(:43-58): 
  - 맞음: 사용자 실제 세이브(Current 22) → 21. 50 미클리어 세이브가 정확히 받은 균사석 70(=보스 10~40 합)이면 `>`라서 49로 처리 — 오발동 없음. `Spent`(`SkillProgress.cs:127`)는 `Min(level, MaxLevel)`로 합산해 맞음. 균사석을 얻는 경로는 옛 코드 `HarvestGame`(보스 처치) 하나뿐(Wallet.Add 호출 grep 확인, 세계수 화면은 표시만).
  - 보완(낮음, 가능한 오발동 둘): (1) 옛 규칙은 같은 보스를 다시 잡아도 균사석을 또 줬으므로(개발 메뉴로 `Set` 되감기 후 재처치 등) Current=50·50 미클리어인데 받은 합 >70이면 50 클리어로 오인. (2) `Spent`는 **현재** 가격으로 계산하므로 균사석 노드를 산 뒤 가격표가 바뀐 세이브는 합이 어긋남(밸런스 패치 ddffddf 전후 구매 시). 실사용 세이브(22)엔 영향 없음, 출시 전 세이브가 없다면 무시 가능. 문서(HANDOFF 4.1-10)에 "근사 판정" 한 줄 추가 권장.
  - 보완(정보): Current<50 세이브는 `Current−1`이라 정확. Current==50일 때만 휴리스틱.

**T6**
- 맞음: 목표 HUD(`StageHud.SetGoal` :121, 씬 `Goal` 오브젝트·goalText 연결). 판은 목표 후에도 계속(Finish는 시간 종료/보스 처치만). 달성 순간 1회만 갱신(`goalShown`, HarvestGame:140). 보스 판·파밍 판은 `GoalFor`가 0을 돌려줘 HUD 숨김(`RoundStats.cs:106`).
- 맞음: 실패 → `Complete` FailStreak+1(:107), 골드는 `Wallet.Add(TotalGold)` 전부, 같은 스테이지 재도전. 성공 `Advance`에서 FailStreak 0. 완화 `max(0.7, 0.9^k)`·최소 1.
- 맞음: `mushHpMul`은 `SpawnMushroom`(`HarvestField.cs:704-707`)에서 일반 버섯만, 보스 `SpawnBoss`(:575)엔 안 곱함. 같은 풀(`pool.Get`+`Setup`이 색을 매번 덮어씀) → Instantiate 없음.
- 맞음: 보스 failGold 표 전부 1, 강제 파밍 없음(`Complete`는 보스 패배 시 아무것도 안 바꿈). 팝업 `ShowChoice`: 보스 패배면 재도전/파밍/세계수로, 파밍 → `ChooseFarm`(FarmLeft 1) → 씬 재로드. 프리팹 `myceliteText`·`farmButton`·`retryLabel` 연결 확인(ResultPopup.prefab:485-487).
- 맞음: bossHitShare 0.35·bossRewardMul 3·goalRelief 0.9·goalReliefMin 0.7·hardTint가 C# 기본값과 씬 직렬화값 일치.
- 보완(중간-낮음): `HarvestField.cs:702` `golden = !stats.BossRound && …` — 황금 버섯을 보스 판에서 막는 **로그·HANDOFF에 없는 동작 변경**. 보스 판엔 일반 버섯이 안 돋거나(군락지 리스폰 없음) 의도일 수 있으나 기록 필요. 의도가 아니면 되돌릴 것.
- 보완(낮음): HUD `Goal` 텍스트가 auto-size(18~26)라 로그의 "44pt"와 다름(실제 표시는 최대 26). Buffs를 복제해서 그대로 온 것. 겹침(Goal −340 vs Buffs −305, 팝업 균사석 y −350 vs 버튼)은 스크린샷 없이는 확인 못 함 — 육안 확인 필요(미확인).
- 보완(낮음): 튜토리얼 1~3(목표 3·4·5)도 연속 실패 완화가 적용됨(3→2). "고정"이라면 `GoalFor`에서 제외해야 하나 값이 작아 영향 미미. HANDOFF엔 "튜토리얼 고정 3·4·5"라고만 적혀 있어 문구를 "첫 시도 목표"로 정확히 하거나 코드로 제외하는 쪽을 정하면 됨.

**엣지**
- 맞음: 보스 스테이지엔 harvest goal 없음(`GoalFor` boss!=0 → 0), 표에서도 goalType·goal·mushHpMul 비어 있음(10·20·30·40·50).
- 맞음: 파밍 판(보스 스테이지 일반 판) — Goal 0, `StageCleared=false`(`HarvestGame.cs:44`), 균사석 0, Complete는 FarmLeft−1만 하고 단계 안 넘김. 다음은 보스.
- 맞음: 50 재처치 — 균사석 0, `Advance`가 Current를 50에 머물게 함(Min), 강제 파밍 없음.
- 맞음: 보스 다음 스테이지(11 등)는 `Advance`가 FarmLeft·FailStreak을 0으로 만들어 이전 실패가 이어지지 않음.
- 맞음: 일반 스테이지에서 FailStreak은 보스·파밍 판에서 안 올라감.

**HANDOFF.md**: 위 항목과 일치(4.1-7·9·10, 4.4, 6, 7, 8 키). 보완은 "튜토리얼 고정"·이관 "근사 판정"·콤보 ×2.0은 스킬 전 값 정도. 
**BalanceBot**: CSV 새 열 3개는 끝에 붙어 기존 열 순서 유지(분석 스크립트 호환), `farmRounds` 기본 0.

**판정:** 통과. 치명 버그 없음. 확인·결정 필요: `HarvestField.cs:702` 황금 보스 판 차단(기록 누락), 이관 휴리스틱 근사성(`StageProgress.cs:43-58`), Goal 텍스트 크기·겹침(육안).

### [관리] V1 결과 처리

- V1 지적 1(HarvestField.cs:702 황금 가드)은 1차 작업 T4에서 의도한 변경이다. HANDOFF에 기록만 추가한다.
- 지적 2~5(마이그레이션 "근사" 표기, 튜토리얼 1~3 완화 제외, Goal HUD 글자 크기 기록, 콤보 ×2.0 문구)는 M2가 끝난 뒤 처리한다. M2가 플레이 모드로 측정 중이라 지금 코드를 고치면 재컴파일로 측정이 끊긴다.

### [측정·조정] M2 — opus

**방법**
- InGame 플레이 모드 + MCP `execute_code`로 `BalanceBot`(사람 모델: SKILL 1.0, 수입 ×0.6, 보스 피해 ×0.234, 판 밖 60초, 보스 지면 바로 재도전). 게임·봇 코드는 안 고침. 스크립트·CSV: 스크래치 `.../scratchpad/m2/` (`camp.cs` 캠페인, `snap.py` 첫 도착 스킬 스냅숏, `an.py`·`perstage.py` 분석, `rtfit.py` 보스 맞춤, `goalfit.py`·`campfit.py` 목표 맞춤, `setx.py` 엑셀 쓰기).
- **보스:** 캠페인에서 그 보스에 처음 도착한 순간의 스킬 레벨을 뽑아, 그 상태로 **실제 보스 판**(죽지 않는 보스가 아님)을 체력 배수 k별로 반복(시드마다 8~12회)해 처치율 곡선을 만들고 목표 처치율이 되는 k로 `hpRate`를 바꿈. (`ProbeBoss`의 분신 실효 ×3.27은 실제 판과 크게 달라 최종 판단엔 안 씀 — 아래.)
- **목표 N:** 1회차는 스테이지마다 첫 도착 스냅숏으로 일반 판을 12회씩 다시 돌려 분위수로(`goalfit.py`). 그 뒤는 캠페인 첫 시도 수확 평균 m × (1 + z·CV)(`campfit.py`, CV는 다시 돌린 값 0.03~0.15로 자름, z = 통과율 분위)를 여러 캠페인 묶어(r3~r6, 33시드) 평균 — 구매 계단 때문에 한 번 결과로 맞추면 벽이 다음 스테이지로 옮겨 다님(아래).
- 목표 시드: 최종 확인 r8 = 시드 1~12.
- 플레이어 세이브: 레지스트리 키 통째 백업(`m2/prefs_backup.reg`) → 끝나고 복원, 키 목록 40개 그대로(새 키 없음). 플레이 모드 종료, Start.unity, 저장 안 된 씬 없음. 커밋 안 함.

**e5f377e(바 폭 0) 가설 확인 (값싼 방법):** 지금 코드에서 `HarvestField.barBaseSize/Pos`를 리플렉션으로 0으로 만들어(옛 버그 상태 = 폭 0 바가 필드 가운데) 같은 스냅숏으로 보스 판을 돌림 → 보스 피해 중앙값 10: ×1.3, 20: ×2.0, **30: ×2.5**, 옛 체력 2,069에서 30 처치율 0% → 90%. **M1 추측이 맞다:** 옛 보스 체력은 바가 깨진 상태(수확기가 위쪽 절반만 오감)로 맞춘 값이라 정상 바에선 30·50이 벽이었다.
- 50 벽의 다른 원인: M1 때는 균사석이 보스에서만 나와 3339(꼬마 수확기)를 1레벨만 사서 트리 156레벨, 50보스 판당 피해 ~3.7만. 지금은 ★ 균사석으로 3339 4레벨(159레벨) → 판당 6~9만. 그래서 50 체력은 거의 그대로 둬도 됨.

**보스 목표 첫 시도 처치율** (옛 맞춤의 실패율 10·15·40·45·55%를 그대로 = 처치 90·85·60·55·45%)

| 보스 | hpRate 옛 → 새 | 체력(×스테이지) 옛 → 새 | 최종 r8 첫 시도 처치 | 판당 처치(여러 캠페인 합) | 시도 수(r8) |
|---|---|---|---|---|---|
| 10 대왕송이 | 0.128 → **0.1809** | 51 → 72 | 11/12 | — | 최대 2 |
| 20 청소부 | 0.502 → **0.5278** | 244 → 256 | 12/12 | — | 1 |
| 30 죽음의 천사 | 2.984 → **1.52** | 2,069 → 1,054 | 11/12 | r2~r7 1.653에서 69% | 최대 2 |
| 40 환상 버섯 | 1.783 → **1.5** | 1,236 → 1,040 | 3/12 | r5~r6 1.83에서 39% | 최대 4 |
| 50 움직이는 버섯 | 16.215 → **14.84** | 16,864 → 15,434 | 5/12 | r2~r6 15.46에서 42% | 최대 3 |

- 40: 3341(분신술, 2,600만)을 샀느냐로 피해가 **약 5배** 갈림. 지금 목표표에선 3341을 40~41에 사서 40 도착 때 둘이 섞임. 3341 없는 상태에서 처치 ~40%가 되게 낮춤(있으면 거의 100%). 그래서 40 체력이 30보다 낮음(분신 셋을 다 잡아야 해 실효는 더 큼).
- 보스 피해 분포가 좁아(CV 5~11%) 처치율 곡선이 가파름 → 도착 상태가 조금만 달라도 첫 시도 처치율이 캠페인마다 크게 흔들림(30: r6 92%, r7 20%, r8 92%). 표는 판당 처치율 기준으로 봐야 함.

**일반 스테이지 목표 N (I5 임시값 → 새 값)**

| 묶음 | x1 | x2 | x3 | x4 | ★x5 | x6 | x7 | x8 | x9 |
|---|---|---|---|---|---|---|---|---|---|
| 1 (4~9) | — | — | — | 16→**20** | 19→**23** | 23→**25** | 30→**29** | 25→**34** | 26→**32** |
| 2 (11~19) | 30→**37** | 29→**37** | 29→**37** | 31→**39** | 30→**40** | 33→**45** | 37→**54** | 44→**64** | 57→**65** |
| 3 (21~29) | 63→**83** | 68→**89** | 77→**95** | 90→**103** | 79→**130** | 98→**127** | 109→**125** | 122→**133** | 107→**140** |
| 4 (31~39) | 341→**204** | 382→**196** | 406→**227** | 436→**238** | 430→**293** | 458→**388** | 494→**344** | 588→**387** | 585→**391** |
| 5 (41~49) | 728→**636** | 727→**779** | 717→**886** | 739→**929** | 678→**902** | 704→**927** | 706→**924** | 746→**929** | 747→**928** |

- 1~3 튜토리얼 3·4·5 그대로. `mushHpMul` 그대로(1.1·2.0·1.3). 15(★)는 판 안 공급 상한(15초 판 ≈ 40개)에 걸려 41이면 첫 시도 0%라 **40으로 수동 고정**. x8~9는 시간 손잡이로 60% → **65%** 분위로 맞춤.

**통과율 (첫 시도, 사람 모델)**

| 역할 | 목표 | r2 (6시드) | r6 (12) | r7 (12) | **최종 r8 (12)** | 스테이지당 추가 판(r8) |
|---|---|---|---|---|---|---|
| x1~3 | 85% | 76% | 74% | 72% | **69%** | 0.38 |
| x4 | 75% | 70% | 77% | 76% | **70%** | 0.32 |
| ★x5 | 35% | 60% | 38% | 44% | **55%** | 0.48 |
| x6~7 | 85% | 87% | 87% | 86% | **87%** | 0.13 |
| x8~9 | 60%(→65%) | 58% | 62% | 72% | **67%** | 0.35 |

- x1~3이 낮은 이유는 분위 문제가 아니라 **구매 계단**: 큰 구매(약 34~35, 3341 ≈ 40~41에서 수확 ×1.7, 3339 꼬마 ≈ 42에서 690 → 950)가 있는 직전 스테이지는 그 구매를 할 때까지 실패(완화로 최대 2~4판). r8에서 31·41·42가 x1~3에 걸림. 목표를 계단 뒤 곡선에 맞추면 앞이 벽, 앞에 맞추면 벽이 다음 스테이지로 옮겨 감(r2→r3→r4에서 33 → 34·39 → 35·42로 이동 확인). 그래서 여러 캠페인 평균으로 둠.
- 수확 수 CV가 0.03~0.15(T6 가정 0.2보다 작음) → 완화 ×0.9면 두 번째 시도에 거의 통과. 실패 1번 ≈ 추가 1판.

**총 시간 (50보스 처치까지, 판 + 판 밖 60초)**

| 캠페인 | 표 상태 | 시드별 분 | 평균 |
|---|---|---|---|
| r1 | 보스 새 체력 1차 + I5 임시 목표 | 90.1 · 89.7 · 84.0 · 95.8 | 89.9 |
| r6 | 목표 3차, 보스 40 = 1.83 | 94.8 · 89.4 · 92.3 · 88.2 · 104.2 · 88.9 · 91.2 · 90.7 · 92.9 · 103.4 · 99.4 · 93.8 | 94.1 |
| r7 | 최종 목표, 보스 40 = 1.5 | 84.6 · 94.9 · 89.2 · 85.0 · 112.7 · 94.5 · 82.5 · 90.7 · 87.2 · 90.4 · 96.2 · 90.4 | 91.5 |
| **r8 (최종)** | 위 + 보스 30 1.52·50 14.84 | **88.8 · 85.1 · 86.6 · 90.5 · 87.9 · 87.7 · 88.1 · 89.2 · 91.4 · 87.4 · 90.8 · 86.2** | **88.3** (85~91) |

- 90분 목표에 −1.7분. 더 늘리려면 ★ 목표를 지금 55% → 35%로(★ 5개 × 약 +0.2판 ≈ +1.4분). 이번엔 안 함(그 정도는 측정 잡음과 같은 크기).
- 시간 늘어지는 꼬리는 거의 보스 재도전(r7 시드 5: 50보스 14회 → 112.7분).

**균사석 타임라인 (r8, 12시드, "스테이지/분")**

| 구매 | 결과 | T5 기대 |
|---|---|---|
| 3001 Lv2~4 | 5 / 7~8분 | — |
| 3001 Lv5~8 | 10 / ~16분 | — |
| 3001 Lv10 | **20 / 27~33분** | ~20 스테이지 ✓ |
| 3235 Lv1~5 | 36~37 / 53~64분 | — |
| 3339 Lv1~4 | **40~41 / 63~74분** (3341을 산 그 판에 바로 4레벨) | ~45에 살 수 있음 ✓ (균사석은 35쯤 이미 충분, 막는 건 골드 노드 3341) |
| 3143 Lv1~4 / Lv5 | 42~43 / 69~78분, 45 | — |
| 끝 남는 균사석 | 33 (전 시드 같음) | ~30 예비 ✓ |

**기록만 (코드 수정 안 함)**
1. **봇 판이 플레이 모드 하나에서 점점 느려짐:** 늦은 스테이지 한 판이 새 세션 0.2초 → 약 3,000판 뒤 17초(×70). `HarvestField/SkillEffects/BossGimmicks.Restart`를 거듭할 때 뭔가 쌓이는 것으로 보임(풀·구독 등, 조사 안 함). 결과값(수확 수)은 같았음. 실제 게임은 판마다 씬을 다시 읽어서 영향이 없을 수 있으나, static에 쌓이는 거라면 영향 있음 → 확인 필요. 측정은 6시드마다 플레이 모드를 다시 켜서 피함.
2. **보스 재도전 결과가 시드 안에서 서로 붙어 있는 듯:** r7 시드 5가 판당 ~42% 보스를 13연패(독립이면 0.1%). 1번과 같은 '판 사이에 남는 상태'일 수 있음.
3. `BalanceBot.EffectiveBossHp`의 분신 ×3.27은 지금 코드와 안 맞음: 실제 판 처치 기준으로는 약 ×2.1. `ProbeBoss`로 맞추면 40 체력이 2배 넘게 높게 나옴.
4. 수확 공급 상한: 11~17(15초 판) ≈ 40개, 38~39 ≈ 680, 42~49 ≈ 950에서 거의 고정(CV 1~3%). 이 구간 목표는 실력보다 운(몇 개 차이)으로 갈림. 사람은 편차가 더 클 것 — 실기기 테스트로 확인 필요. 목표 종류(수확 N개)를 점수·황금 등으로 바꿀지 검토 거리.
5. `Boss.xlsx` `#memo` 열은 10-26 맞춤 설명 그대로(읽지 않는 열). HANDOFF 6.1·4.4의 보스 체력·"50 벽" 문구는 갱신 안 함(관리자/다음 작업).

### [재설정] V1·M2 후속 — opus

M2가 정한 밸런스 수치(Boss `hpRate`, Stage `goal`)는 건드리지 않음. 커밋 안 함.

**1. 튜토리얼 1~3 목표 완화 제외 (V1 지적)**
- `RoundStats.cs:105` `TutorialStages = 3` 추가, `GoalFor`(:108) `:111` `if (stage.stage <= TutorialStages) return stage.goal;` — 1~3은 연속 실패해도 3·4·5 그대로. 플레이 모드 확인: st1~3 failStreak 0/3 모두 3·4·5, st4 20→15·st5 23→17(완화 그대로).

**2. HANDOFF.md**
- 4.1-7: "점수 배율 최대 ×2.0" → "콤보 점수 배율은 **스킬 전 기준** 최대 ×2.0(`ComboRate`·피버 `ComboMul`이 계수에 곱해져 넘을 수 있음, 의도)".
- 4.1-9: 튜토리얼 고정에 "연속 실패 완화도 없음(`RoundStats.TutorialStages`)" 추가, x8~9 "60%(… 시간 손잡이로 65% 분위로 맞춤)", "N 값은 임시" 문단을 M2 결과(맞춤 방법·15=40 수동·r8 통과율)로 교체.
- 4.1-10: 이관을 **근사 판정**으로 명시 + 이유 2개(옛 규칙의 보스 재처치 균사석 중복 → 50 오인 가능, `Spent`가 지금 가격 기준이라 ddffddf 전 구매 세이브는 합 어긋남), Current<50은 정확, 사용자 세이브(22) 영향 없음.
- 4.4: 황금 가드 기록 추가(`HarvestField.cs:702` `golden = !stats.BossRound && …`, T4 의도, 지금은 보스 판에 군락지가 없어 방어용, 파밍 판은 황금 나옴). "⚠️ 50보스 벽" 줄을 M2 맞춤(hpRate 0.1809/0.5278/1.52/1.5/14.84 = 체력 72/256/1,054/1,040+분신/15,434, 처치 90·85·60·55·45%, 원인 = e5f377e 전 바 폭 0 측정)으로 교체.
- 6 Stage 열 `goal` "임시값" → "M2 맞춤". 6.1 보스 줄에 "옛 값, 6.2로 대체, 바 폭 0 측정, ×3.27은 지금 ≈×2.1" 추가. **6.2 신설**(M2 보스·목표·88.3분 요약). 8 BalanceBot에 "한 프레임 안에서 Destroy/코루틴을 만들면 쌓임" 주의 한 줄. 9-1 갱신.

**3. Boss.xlsx `#memo`** — 10-26 맞춤 설명(사람 피해 = 봇 × 0.8, 실패율 목표, 분신 ×1.97)이 그대로였음 → 5행 모두 "2026-10-27 M2: 사람 모델 캠페인 첫 도착 스킬로 실제 보스 판, 첫 시도 처치 N% 목표, 옛 값 x(10-26)은 바 폭 0 버그 상태 값이라 폐기"(6004에 3341 유무·분신 ≈×2.1, 6005에 3339 4레벨 덧붙임)로 교체. openpyxl, 백업 `scratchpad/revise/Boss_before.xlsx`·`.bytes`. `MoreMush/Data/Reimport Tables` → Boss 5행·Stage 50행 OK, `Boss.bytes` = 새 xlsx와 바이트 동일. 플레이 모드에서 `GameData.Bosses` hpRate 0.1809·0.5278·1.52·1.5·14.84 그대로 확인.

**4. 기록 정정 (I5 기록)** — HUD `InGameArea/Goal` 글자는 **auto-size 18~26**(Buffs 복제)이라 I5 기록의 "44pt"는 틀림. 실제 표시 최대 26pt. (겹침은 여전히 육안 미확인.)

**5. M2 미해결 1 — 봇이 판을 거듭할수록 느려짐: 원인 찾음, 고침**
- 원인: 측정기는 MCP 한 호출(= 한 프레임) 안에서 수천 판을 돈다. 에디터가 뒤에 있으면 `Time.frameCount`가 1~2에서 안 오름(이번에도 확인). **프레임 끝에 처리되는 것이 판마다 생기면 끝없이 쌓인다.** 두 가지:
  1. `FieldDevices.Place`(옛 :36)가 판마다 지난 장치 그림을 `Destroy`(프레임 끝에 지워짐) 후 `new GameObject` — 환경 복원 3레벨이면 판마다 5개. 재현: 3,000번 `field.Restart` 뒤 필드 자식 **15,083개**(장치 15,076).
  2. `Mushroom.Play`(옛 :113-117)가 버섯을 꺼낼 때·맞을 때마다 `StopCoroutine` + `StartCoroutine(Pop)`. 끝나지 못한 코루틴이 Unity 안쪽에 쌓여 꺼내기·돌려놓기가 점점 느려짐. 재현: 같은 버섯을 꺼냄·돌려놓음 5,000회 묶음이 1,866 → 2,189 → 2,387 → 2,680 → 2,905ms로 계속 증가(코루틴 없이 SetActive만 하면 52ms로 일정). 이것이 주원인 — 장치만 고친 상태에서도 300번 `Restart` 묶음이 402 → 1,387 → 2,235ms, 판 3개 28 → 98ms.
- 고침 (런타임 코드, 게임 동작·결과 같음):
  - `FieldDevices.cs:27-46` `made`를 `List<Image>`로, 지난 판 그림은 끄고 `spare` 스택에 넣음, `Make`(:79~)가 `spare`에서 꺼내 다시 씀(없을 때만 생성). 실제 게임은 판마다 씬을 다시 읽어 `Place`가 한 번만 불리므로 차이 없음.
  - `Mushroom.cs` 출렁임을 코루틴 → `Update` 트윈으로(`punchTime/Duration/From/To`, `Pop` :114, `Update` :124, `ShowPunch`). 모양·시간 계산은 옛 코루틴과 같음(시작 프레임 from, 1.15배, to, `Time.deltaTime` 기준이라 일시정지 땐 멈춤). `OnDespawned`는 `punchTime = -1`. 실제 게임에도 맞을 때마다 코루틴 할당이 없어져 GC가 조금 줄어듦.
  - `SkillEffects.cs:109` `Restart`에서 `orbitAngle = 0` (아래 결정성 문제).
- 확인: 고친 뒤 300번 `Restart` 묶음 105·106·104·104ms(필드 자식 12개 그대로), 판 3개 28 → 29ms. **한 프레임 안에서 캠페인 45개(2,961판)** 연속: 시드 1 캠페인 2.90s(처음) → 3.11s(마지막), 다른 시드도 2.4~3.5s 사이로 평평(`scratchpad/revise/timing.txt`). M2의 ×70은 사라짐.
- **실제 게임 영향:** static에 쌓이는 것은 없음(`BalanceBot`의 static 리스트는 `Begin`에서 비움, `SkillProgress.Changed`·`Wallet.Changed` 구독은 전부 OnDestroy/OnDisable에서 해제 확인 — SkillInfoPanel·SkillNode·SkillTreeScreen·SkillTreeView·WorldTreeScreen). 실제 게임은 매 프레임 Destroy·코루틴이 정리되고 판마다 씬을 다시 읽으므로 이 누적은 **측정기(프레임 없이 도는 경우) 전용**. 고친 것은 둘 다 실제 게임에도 무해(오히려 생성·할당이 줄어듦).

**6. M2 미해결 2 — 보스 재도전 결과가 붙어 있는 것**
- 위 누적은 **시간만** 늘림(장치 장애물은 `Restart`에서 비워져 판정에 안 쓰임, 코루틴은 그림 크기만) → 결과 상관의 원인은 아님.
- 대신 찾은 것: **같은 시드를 같은 세션에서 두 번 돌리면 결과가 달랐음**(시드 1: 64판 둘 다지만 골드 3.72e9 vs 3.44e9, 21판 18스테이지 점수부터 갈림). `SkillEffects.orbitAngle`이 `Restart`에서 초기화되지 않아 앞 판·앞 캠페인의 궤도 위치가 이어짐 → `:109`에서 0으로. 고친 뒤 시드 1 두 번 = 완전히 같음(68판, 91.8분, 골드 4,204,565,347). 그래서 지금 시드 1 결과는 M2 r8_s1(66판 88.8분)과 다름 — M2 숫자는 세션 순서에 따라 조금씩 흔들린 값(평균엔 영향 작음, 재측정은 안 함).
- 보스 연패 상관의 설명은 M2가 적은 쪽이 맞아 보임: 같은 시드 안에서 재도전은 같은(거의 같은) 스킬 상태로 하고 보스 피해 CV가 5~11%로 좁아, 그 상태의 처치 확률이 0~100%에 가깝게 갈림. 여러 캠페인을 합친 "판당 42%"는 상태가 다른 시드를 섞은 평균이라 한 시드 안의 재도전을 독립 시행으로 보면 안 됨(시드 5의 13연패 = 그 상태의 처치 확률이 낮았던 것). orbitAngle은 판 사이 잔여 상태이지만 영향이 작아 상관의 원인으로 보긴 어려움.

**컴파일·스모크**
- `read_console` 에러 0(코드 바꿀 때마다 재컴파일 확인, `Mushroom.Update` 존재 리플렉션 확인).
- 플레이 모드 실제 한 판(`Application.runInBackground = true`, 런타임만): 23스테이지, `LaunchMain` 후 프레임 170,208까지 진행, 판 끝(수확 33/목표 95), 남은 버섯 출렁임 모두 크기 1로 끝남, 장치(그루터기) 1개, 에러·경고 0.
- PlayerPrefs: 시작 때 레지스트리 키 통째 백업(`scratchpad/revise/prefs_backup.reg`, 키 40개) → 끝나고 키 삭제·복원. **주의:** 에디터의 PlayerPrefs 메모리 캐시가 레지스트리와 달랐음(내가 시작할 때 이미 캐시에 Current 23·`Stage.Cleared` 22·`Stage.FailStreak` 1·Wallet.Gold 970,428 — 이전 작업들의 스모크 흔적으로 보임, 레지스트리는 Current 22·키 없음). 캐시가 나중에 레지스트리를 덮어쓰지 않게 `PlayerPrefs` API로 백업 값(Current 22·FarmLeft 0·LastNormal* ·Wallet 776,851/25·SpeedIndex 2)을 다시 쓰고 `Stage.Cleared`·`Stage.FailStreak` 삭제 후 `Save` → 레지스트리 내보내기가 백업과 바이트 동일.
- 플레이 모드 종료, 열린 씬 Start.unity(더티 아님).

**남은 것:** M2 미해결 3(`EffectiveBossHp` 분신 ×3.27 → 실제 ≈×2.1)·4(수확 공급 상한 구간 목표)는 손대지 않음. HUD Goal·결과 팝업 겹침 육안 확인 여전히 안 함.

### [검증2] 최종 — sonnet

읽기 전용 검증(코드·표·씬 수정 없음, 플레이 모드·PlayerPrefs 안 건드림). `read_console` 에러 0건.

**Mushroom 트윈 (`Mushroom.cs`)**
- 맞음: 곡선·시간 동일. 옛 코루틴(0.18초 0→1.15→1 / 0.15초 0.8→1.15→1, 앞 반 Lerp(from,1.15,2k)·뒤 반 Lerp(1.15,to,2k−1), 끝에 to 고정)을 `Pop`(:114)·`Update`(:124)·`ShowPunch`(:131)가 그대로 옮김. 시작 프레임에 from 즉시 반영, `Time.deltaTime` 누적이라 일시정지 때 멈춤도 같음.
- 맞음: 풀 리셋. `OnSpawned`(:63)가 visual 크기 1로 되돌린 뒤 `Pop`을 새로 걸고, `OnDespawned`(:81)는 `punchTime=-1`. `PrefabPool.OnGet`이 SetActive(true) 후 `OnSpawned`를 부르므로 `isActiveAndEnabled` 가드에 안 걸림. 출렁 도중 반납돼도 다음 꺼냄에서 크기 1 + 새 트윈이라 크기가 남지 않음.
- 맞음: 비용. `Update`는 `punchTime<0`이면 첫 줄에서 반환(유휴 버섯 비용 거의 0). 할당 없음(코루틴·IEnumerator 제거, 필드 float 4개).
- 보완(없음/참고): `Pop` 가드(`visual==null || !isActiveAndEnabled`)로 비활성 상태에선 안 걸리는데, 그때 `punchTime`은 `OnDespawned`에서 이미 −1이라 남은 값 없음.

**FieldDevices (`FieldDevices.cs`)**
- 맞음: `Place`(:36-44)가 지난 그림을 SetActive(false) 후 `spare`에 넣고, `Make`(:79-)가 `spare`에서 꺼내 이름·활성·위치(anchoredPosition)·크기(sizeDelta)·앵커·sibling index 0·색을 매번 덮어씀. sprite·raycastTarget은 최초 생성 때 한 번 정해 그대로(변하지 않는 값). `acorns`·`lit`은 `Place`에서 초기화. 
- 맞음: 판 중 `Destroy`/`new` 없음. `InGame/*.cs` grep 결과 `Destroy`는 `PrefabPool` 풀 파괴 콜백, `new GameObject`는 `FieldDevices:90`(spare가 빌 때만)·`SkillEffects:477`(생성 시)뿐. 개수는 한 판 장치 수(환경 복원 레벨 기준 수 개)로 상한, 이후 spare 재사용이라 늘지 않음.
- 보완(낮음): 개수 상한이 코드에 명시돼 있진 않고 "한 판 최대 장치 수"로 암묵적 제한 — 현재 호출 구조에선 문제 없음.

**SkillEffects (`SkillEffects.cs:109`)**: 맞음 — `Restart`에서 `orbitAngle = 0f` 한 줄, 다른 변경 없음.

**GoalFor (`RoundStats.cs:108-114`)**
- 맞음: 1~3은 `stage.stage <= TutorialStages`에서 `stage.goal`(3·4·5) 고정 반환(표 확인 3/4/5). 4 이상은 `max(goalReliefMin, goalRelief^failStreak)`·최소 1 그대로. 보스(boss≠0)·목표 없는 스테이지는 맨 위 조건에서 0 반환이라 영향 없음(표: 10·20·30·40·50 goal 비어 있음).

**표·HANDOFF 수치**
- 맞음: `Boss.xlsx` hpRate 0.1809 / 0.5278 / 1.52 / 1.5 / 14.84 (openpyxl 읽기). HANDOFF.md:137·214가 같은 값·체력(72/256/1,054/1,040/15,434), :80·218도 일치(튜토리얼 고정, 88.3분).
- 맞음: `Stage.xlsx` goal이 M2 표와 전부 일치 — 4~9: 20·23·25·29·34·32 / 11~19: 37·37·37·39·40·45·54·64·65 / 21~29: 83·89·95·103·130·127·125·133·140 / 31~39: 204·196·227·238·293·388·344·387·391 / 41~49: 636·779·886·929·902·927·924·929·928. 1~3은 3·4·5.
- 맞음: `Boss.bytes`·`Stage.bytes`가 각 xlsx와 `cmp` 동일(재임포트 반영됨).

**전체 변경 파일 (`git status`, 20개 수정 + 1개 신규, 전부 워크로그상 예상된 것)**
- 표: `Assets/Data/Tables/Boss.xlsx`, `Stage.xlsx`, `Assets/Resources/Tables/Boss.bytes`, `Stage.bytes` — M2 hpRate·goal·#memo.
- 씬·프리팹: `Assets/Scenes/InGame.unity`(+148, 삭제는 `comboFactor 0.1` 한 줄뿐 → 새 필드(goalText·goalMetColor·comboCap·bossHitShare·bossRewardMul·goalRelief·goalReliefMin·hardTint)와 `Goal` 오브젝트 추가), `Assets/Prefabs/Mobile/ResultPopup.prefab`(+403, 삭제 없음, Mycelite·FarmButton·Label 추가). 재직렬화 잡음 없음.
- 스크립트: `Data/TableRows.cs`, `InGame/BalanceBot.cs`, `FieldDevices.cs`, `HarvestField.cs`, `HarvestGame.cs`, `Harvester.cs`(Combo 제거), `Mushroom.cs`, `ResultPopup.cs`, `RoundStats.cs`, `SkillEffects.cs`, `SkillProgress.cs`, `StageHud.cs`, `StageProgress.cs`.
- 문서: `HANDOFF.md`(수정), `AGENT_WORKLOG.md`(신규, 미추적).
- 예상 밖 파일 없음. 모두 미커밋.

**판정:** 통과. 버그 없음. 참고(미확인): HUD Goal·결과 팝업 겹침의 육안 확인은 여전히 안 됨(스크린샷 검증 범위 밖).

### [관리] 2차 작업 최종 정리

| 항목 | 상태 |
| --- | --- |
| 콤보 상한 (계수 0.05, 점수용 상한 20) | 구현·검증 통과 |
| T5 첫 클리어 균사석 (보스 + ★) | 구현·검증 통과 |
| T6 일반 스테이지 목표·보스 패널티 제거·선택 버튼 | 구현·검증 통과 |
| 보스 체력 재피팅 (50 벽 해소) | M2 완료, 사람 모델 평균 88.3분 |
| 봇 누적 지연 (버섯 팝 코루틴·기기 Destroy) | 수정·검증 통과, 실제 게임엔 영향 없었음 |

남은 것: HUD 목표 글자·결과창 균사석 줄 겹침 눈 확인, 실기기 체감, M2 열린 점 3(EffectiveBossHp ×3.27)·4(공급 상한 스테이지 목표 방식). 커밋 없음.
