#!/bin/bash
# theory#1 수치안 측정: 표 사본 = out/GameBalance_proposal.xlsx (make_proposal_xlsx.py), 코드 = 관리자가 C1~C5 넣고 다시 빌드한 dll
# B(하루 3시간 3세션) / C(하루 1시간) × 광고 구간 none·light·half·heavy × 시드 10, 45일. 광고·던전·앱 밖 규칙은 test/sim 그대로
# 사용: bash 밸런스설계/test/run_proposal.sh [표 경로(기본 proposal)]
cd "$(dirname "$0")/sim"
TBL=${1:-../out/GameBalance_proposal.xlsx}
OUT=../out
jobs=()
for sd in 1 2 3 4 5 6 7 8 9 10; do for nm in B C; do for sg in none light half heavy; do
  jobs+=("name=$nm seg=$sg seed=$sd table=$TBL maxDays=45 stuck=45 speedMax=0 bossquest=on tag=P_${nm}_${sg}_s$sd")
done; done; done
printf '%s\n' "${jobs[@]}" | xargs -P 8 -I{} bash -c 'a="{}"; t=$(echo "$a" | sed "s/.*tag=//"); timeout 900 dotnet bin/Release/net10.0/IdleSim.dll run $a > '"$OUT"'/r_$t.txt 2>&1'
dotnet bin/Release/net10.0/IdleSim.dll nolevel stages=10 trials=300 table=$TBL > $OUT/nolevel_P.txt 2>&1
dotnet bin/Release/net10.0/IdleSim.dll minlv stages=10 table=$TBL > $OUT/minlv_P.txt 2>&1
echo done
