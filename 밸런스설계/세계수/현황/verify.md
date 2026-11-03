# wt-verify 현황
지금: 끝 — 라운드 6 (의뢰 v5.1, d_dungeon) 통과: W1 8단계 40.0일(의뢰 없음 48.33), W10 일일 100%·주간 보통 4~5일, W4 60일 안 ≤3일. 참고 R6-a~d (11-03 10:34)
- 라운드 6: 검증.md 맨 위 R6-0 · r6/wt_quest.py · r6/cases_r6.log · r6/feedback_r6.log · feed.py에 d1n(던전 1 판 수) 추가 · 11-03 10:34
- 라운드 5: 통과 (11-03 09:28)
- 라운드 5: 검증.md 맨 위 R5-0 · check_apply_v4.py · r5/cases_r5.log · r5/feedback_r5.log · 11-03 09:28
- 라운드 4: 조건부 통과 D1·D2 (11-03 09:00)
- 라운드 4: 검증.md 맨 위 R4-0 · feed.py(라이브 표 직접, 광고 뽑기 = 뽑기상품 pullCount·dailyLimit) · r4/cases_r4.log · r4/feedback_r4.log · r4/w7_r4.log (라운드 3 래퍼 r3/feed_r3.py) · 11-03 09:00
- 라운드 3: 통과 (11-02 16:45)
- 라운드 3: 검증.md 맨 위 R3-0 · check_apply_v3.py · cases.log · feedback.log (라운드 2 자료 r2/) · 16:45
- [x] #3 → wt-theory: C1~C4 → 라운드 3에서 해결 확인 (16:45)
- [x] manager → 라운드 3 판정 · 검증.md (16:45)

## 공유할 결과
- `밸런스설계/세계수/검증.md` · 라운드 2: 조건부 통과. 표 194건 일치. W4 되먹임 직접 측정: v2 보통 30·45일 0·0일, 라이브 30일 2일. 라이브 폭주는 49일부터 실제로 일어남(보스 균사석 → 뽑기 되먹임) · 16:36
- `밸런스설계/세계수/verify/` · check_apply_v2.py · feedback_cmp.py → feedback.log · w7_unplaced.py · cases.log (라운드 1 자료는 r1/) · 16:36
- (라운드 1) 검증.md 9절 · 16:11

## 요청
- [ ] #7 → wt-theory: R6-a(W10 의뢰 몫 10~25%는 보통 기준인지 문구) · R6-b(d_kill 4000, ×0.5 여유 1.25%) (11-03 10:34) [참고]
- [ ] #8 → manager: R6-c(던전 판 중단 시 미집계, AddQuest 처치마다 할당) · R6-d(U5: 8단계 40일 뒤 의뢰 exp 쓸 곳 없음) (11-03 10:34) [참고]
- [x] #5 → wt-theory: 검증.md R4-4 D1(이론.md 0·1·3·4장 v3 기준선 숫자, idle_feed.py 머지 표에서 안 돔) · D2(W7 목표 또는 5단계 칸) (11-03 09:00)
- [ ] #6 → manager: D3(U4 숫자 갱신: 무광고 부리 ★3 63.3일), KHD 재현 명령 run_v3d.py가 머지 표에서 KeyError 711 (11-03 09:00) [참고]
- [ ] #3 → wt-theory: 검증.md 6절 C1~C3 문구 정정 (C4 선택) (16:37) [참고]
- [ ] #4 → manager: U1 함께 제출, 라이브 폭주(49일~, KHD 쪽) 기록 (16:37) [참고]
- [x] #1 → wt-theory: R1~R6 → 라운드 2에서 해결 확인 (16:36)
- [x] #2 → manager: ClampToLong 반영 확인 (16:36)

## 받은 요청 처리
- [x] manager → 라운드 6 재검증 (의뢰 v5.1) · 검증.md R6 (11-03 10:34)
- [x] manager → 라운드 5 재검증 (이론 v4 적용) · 검증.md R5 (11-03 09:28)
- [x] manager → 라운드 4 재검증 (KHD v3 머지) · 검증.md R4 (11-03 09:00)
- [x] manager → 라운드 1 판정 · 검증.md (16:11)
- [x] manager → 라운드 2 판정 · 검증.md (16:36)
