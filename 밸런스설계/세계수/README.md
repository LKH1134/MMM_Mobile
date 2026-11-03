# 세계수 밸런스 — 관리자·이론·검증 공유 공간 (2026-11-02)

> **11-03 13:04 변경 (KHD 관리자, 사용자 지시): 수확 보따리 제거.** 앱 밖 보상과 겹쳐서 뺐다. 보따리 효과는 처치 골드·보스 균사석·제작 속도로 바꿨고, 보따리 전용 열·상수는 메모로 돌리거나 지웠다(`밸런스설계/tools/remove_tree_bag.py`). 나이테(진화 조건만)·호감도(효과 없음)·★진화(특기 단계만)의 보상이 줄었으니 W3·W8과 전체 수치를 다시 판정해야 한다.

> **상태 (11-03):** KHD 방치 v3 머지(`c53ecf2`) 뒤 라운드 4(조건부)·5(통과) — **v4 = v3 + 9305 branchSlots 6**, **v5 = v4 + 의뢰 시트(9901~)·treeQuestDaily** (라운드 6 통과). 의뢰 기준 W10 초안은 `이론.md` v5.6. 기준선은 머지된 라이브 표(앱 밖 12h·0.5, 광고 뽑기 4, 스테이지 1~700 + 꼬리). (11-02 끝: 수치안 v3 표 적용·검증 통과 `a3e1463`.) 결과 요약은 `이론.md` 0장, 판정은 `검증.md`, 경과는 `현황/manager.md`. 남은 것: 사용자 결정 **U1**(전투 효과 크기, 이론.md 8장). U1이 정해지면 이 흐름으로 다시 돌린다.

세계수 시스템(성장·꼬마·공방·꾸미기·수확 보따리)의 **모든 수치**를 지금 방치형 게임에 맞게 다시 잡는다. 관리자(main) + 이론(`wt-theory`) + 검증(`wt-verify`). 정의는 `.claude/agents/worldtree-theory.md`, `worldtree-verify.md`.

방치형 전체 밸런스(`밸런스설계/README.md`, KHD 작업)와는 **별개 작업**이다. 그쪽 파일(`밸런스설계/이론.md`·`검증.md`·`테스트.md`·`theory/`·`verify/`·`test/`·`현황/`)은 **읽기만** 한다.

## 흐름 (사용자 지시)

1. 이론이 수치안(이론.md 적용안)을 낸다.
2. 관리자가 표(`GameBalance.xlsx`)에 적용한다 — 표·코드는 관리자만 쓴다.
3. 검증이 적용된 표로 독립 판정한다.
4. 통과 → 관리자가 사용자에게 제출. 반려 → 정정 요청을 이론에게 → 1로. 같은 쟁점이 3번 넘게 오가면 관리자가 사용자에게 묻는다.

## 범위 (전부 다룬다)

| 시트 | 열 |
|---|---|
| 세계수 | evoExp, evoGoldKills, evoMycelite, ringKills, bagKillsPerHour, bagMycelitePerHour, craftSlots, root/trunk/branch/crownSlots, preferIds |
| 가호 | effect, value (단계마다 하나) |
| 꼬마 | favoriteIds, bonusEffect, bonusValue, evoStage (세계수 열만 — attackMultiplier 등 방치 열은 KHD 몫) |
| 꼬마진화 | mycelite, needLevel, bagMul |
| 호감도 | need, bagBonus |
| 장식 | effect, value, grade, zone |
| 장식등급 | goldKills, mycelite, hours |
| 적 | value, requestCount (세계수 열만) |
| 상수 | treeRingMax, treeRingGrowth, treeRingBagBonus, treeHarvestPerKill, tributePreferMul, giftFavoriteMul, giftMaxCount, requestPerDay, requestAffinity, requestGoldKills, requestWeekGoal, requestWeekMycelite, kidBagShare, decoSetBagMul, decoFirstMycelite, decoFirstAffinity, craftRushSeconds, craftRushPerDay, bagCapHours |

공식은 코드에 있다: `Assets/Scripts/Mobile/WorldTree/WorldTree*.cs` (Growth·Kids·Craft·Bonus), 전투 반영은 `Idle/IdleProgress.cs`(FinalAttack·FinalHp·FinalSpeed·FinalCrit*)와 `Idle/IdleBattle.cs`(창고 버섯 AddHarvest, 처치 골드·보스 균사석 배율). 골드 비용·보상은 모두 "지금 스테이지 적 처치 골드 × kills"라 방치 골드 곡선을 따라간다. 코드 공식 변경이 꼭 필요하면 적용안에 따로 적는다(최소로).

## 기준선

- **방치 쪽은 지금 표**(`Assets/Data/Tables/GameBalance.xlsx`, 라이브 값: offlineCapHours 8·offlineGoldRate 1·adGachaPulls 11·퀘스트 균사석 1/2/1/2/3 등)를 기준으로 한다.
  - (11-02 `43c2308`) 상수 `adGachaPulls`는 지워졌다 — 광고 뽑기 수는 `뽑기상품` 시트 광고 행 `pullCount`(11), 하루 한도는 `dailyLimit`. `theory/idle_feed.py`·`verify/feed.py`가 다시 돌려면 그 행을 읽게 고쳐야 한다.
- 방치 밸런스가 바뀌는 중이다(KHD 수치안 v2/v3, 사용자 결정 D1~D4 — `밸런스설계/README.md`·`이론.md` 10.2·`theory/stage_proposal_v2.csv`). → **그 수치안을 넣었을 때도 세계수 기준이 깨지지 않는지 민감도로 본다.** 세계수 수치는 방치 수입에 대한 비율로 정해 어느 쪽에서도 버티게 한다.
- 플레이어: 방치 이론의 "보통 하루"(접속 1.5시간/일, 광고 절반 시청 = 방치 기준 6)를 기준선으로, 무광고·많이는 민감도.

## 세계수 기준 (관리자 초안 — 이론이 근거와 함께 숫자 목표를 정하고, 바꾸려면 이유를 적는다)

- **W1 페이스**: 세계수 8단계가 방치 장기 진행과 함께 간다. 첫 진화는 첫날~이틀, 뒤로 갈수록 단계 사이가 벌어진다(조사 P3). 단계별 도달 예상(일)을 표로.
- **W2 균사석**: 세계수의 균사석 소모(진화·★진화·제작·가속)가 방치 기준 2(일일 퀘스트로 하루 35뽑)를 깨지 않는다. 세계수 균사석 수입(보따리·주간 상자·첫 배치)과 소모의 하루 흐름, 하루 균사석 전체에서의 비중을 표로.
- **W3 골드**: 세계수 골드 소모(나이테·진화·제작)와 수입(보따리·부탁)이 하루 골드에서 차지하는 비중. 보따리는 앱 밖 보상(이미 하루 골드의 큰 몫)과 겹치므로 작은 몫으로.
- **W4 전투 효과**: 가호·꼬마 특기·장식을 기대 진행대로 모았을 때 공격·체력·공속·치명타·처치 골드·보스 균사석 배율 합이 방치 벽(조사 P4: 1.3~2.0배)을 지우지 않는다. 처치 골드·보스 균사석 효과가 방치 재화 흐름(P20, 기준 2)을 흔들지 않는다. 공속은 방치 기준 5(3.00/초 봉인)와 충돌하는지 확인.
- **W5 창고 버섯**: 하루 처치 수 → 창고 → 공물 경험치·선물·부탁이 맞물린다. 부탁은 보통 하루 플레이로 깰 수 있고, 공물만으로 진화 경험치가 W1 페이스에 맞는다.
- **W6 꼬마**: 호감도 최대·★진화 조건(꼬마 레벨 = 뽑기 레벨, ggomaMaxLevel 100, 합성)이 실제 뽑기 흐름에서 언제 닿는지. 높은 등급 꼬마가 불리하지 않다(방치 기준 8의 정신).
- **W7 공방·꾸미기**: 장식 24개를 언제쯤 다 만드는지, 제작 시간·슬롯·가속 비용이 맞물리는지, 장식 칸 수가 단계별 보유 장식 수와 맞는지.
- **W8 보따리**: 보관 시간·시간당 적립이 앱 밖 상한(8h, 수치안 12h)과 어울리고, 접속 유도가 겹치지 않는다.
- **W9 회귀**: 검증기 통과, long 넘침·NaN·0 나눗셈 없음, 단계·레벨이 오를수록 비용·보상이 줄지 않음.

## 파일 (한 파일 = 한 작성자)

| 이름 | 쓰는 파일 |
|---|---|
| 관리자 | `세계수/README.md`, `세계수/현황/manager.md`, 표·코드 |
| `wt-theory` | `세계수/이론.md`, `세계수/theory/`, `세계수/현황/theory.md` |
| `wt-verify` | `세계수/검증.md`, `세계수/verify/`, `세계수/현황/verify.md` |

시각은 `date +%H:%M`으로 적는다.
