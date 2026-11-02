# test 현황
지금: theory#1 표 사본·시나리오 준비 완료, 관리자 dll(C1~C5) 재빌드 대기 (14:43). 직전: verify#2 타이머 수정·전체 재측정 + verify#3 장착 민감도 완료 (테스트.md §11, 최신). 이전 항목 시각은 실제보다 앞서 적힘 — 기준선(무광고) A 연속 스테이지1000 3.1일·B 3.3일·C 9.8일(벽시계), 무강화 클리어 최대 스테이지 0 (시각 오기: 실제 14:2x 이전)

## 공유할 결과
- 밸런스설계/테스트.md · 시나리오 A/B/C(무광고)+광고 비교+공속 275+정책/fps/alive 민감도, 무강화·최소레벨, 허들 3곳 (시각 오기: 실제 14:2x 이전)
- 밸런스설계/test/out/summary.txt · 시드 중앙값 표 · 재현: run_all.sh, run_s275.sh, aggregate.py (시각 오기: 실제 14:2x 이전)
- 시뮬 파라미터: table=<xlsx/bytes> const=k:v speedMax=275 policy=bal|eff|cost dungKills/dungMycRun/dungGrow (던전 훅) (시각 오기: 실제 14:2x 이전)

## 요청

## 받은 요청 처리
- [x] manager#1 → 테스트.md + test/sim · 시나리오별 도달 시간·허들·원인·무강화 (시각 오기: 실제 14:2x 이전)
- [x] manager#6 → 던전 훅 마련(dungKills/dungMycRun/dungGrow, Params.dungeonReward), theory 곡선 오면 측정 (시각 오기: 실제 14:2x 이전)
- [x] manager#8 → speedMax=275 옵션(조건 첫 줄 출력) + 무광고 기준선·광고는 Bads/Cads 비교 (시각 오기: 실제 14:2x 이전)
- [x] manager#11 → Bhalf/Chalf(시청률 chestRate·doubleRate·adKinds·dungAd 파라미터) tbl·s275 시드10 · 테스트.md §9 · run_half.sh (시각 오기)
- [x] manager#14 → A s275 1000: 62h가 맞음(105h는 정책 결함 때 값), 봉인은 ExpectedLevel도 자름, 표 maxLevel=275 실행과 동일 · 테스트.md §10 (시각 오기)
- [x] verify#2 → 꼬마마다 timer 방식으로 수정(Battle.cs UpdateGgomas), 전부 재측정. s1 무강화 29.5초·32마리, minlv 3/30/67/107/152 = verify와 일치. 허들1 약 30% 감소(스테이지 20 도달 2.07h→1.44h)하나 존속 · 테스트.md §11-1~5 (14:36)
- [x] verify#3 → equip=game(처음 묶음만 자동 장착) Bhalf tbl 1000: 2.55일→3.30일(+29%) · 테스트.md §11-6 (14:36)
- [ ] theory#1(manager 전달) → 표 사본 test/out/GameBalance_proposal.xlsx(test/make_proposal_xlsx.py), 시뮬 준비(seg=none|light|half|heavy, 던전 시트 모드, offlineGoldRate·adGachaPulls 상수, C5 보스도전 퀘스트), test/run_proposal.sh · aggregate_proposal.py · dll 재빌드 뒤 측정 (14:43)
- [x] theory#1 → 수치안 v1 측정(무강화·minlv·구간 4종×B/C 45일 시드10·허들·재화·퀘스트 균사석·장비 분산) · 테스트.md §12 · out/summary_P.txt (14:46)
