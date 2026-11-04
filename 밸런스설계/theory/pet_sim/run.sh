#!/bin/bash
# 사용: run.sh <이름> <표 경로(xlsx|bytes)> [WEAPON_MUL=1] [최대 일=60] [시드 수=10]
# 결과: scratchpad/theory_pet/<이름>/ (stage_*.csv, snap_*.csv, r_*.txt). 분석: python pet_sim/analyze.py <이름> ...
SP='C:/Users/user/AppData/Local/Temp/claude/82d5963c662fda11/2c54f97f-d275-490e-a84e-6bcf329252d2/scratchpad'
name=$1; tbl=$2; wm=${3:-1}; md=${4:-60}; ns=${5:-10}
od="$SP/theory_pet/$name"; mkdir -p "$od"
export THEORY_OUT="$od" WEAPON_MUL="$wm"
for sd in $(seq 1 $ns); do for nm in B Bhalf Bads; do echo "$nm $sd"; done; done | \
  xargs -P 8 -L1 bash -c 'timeout 900 dotnet "'"$SP"'/simcopy/bin/IdleSim.dll" run name=$0 seed=$1 maxDays='"$md"' stuck=20 tag=${0}_s$1 table="'"$tbl"'" > "'"$od"'/r_${0}_s$1.txt" 2>&1'
echo "$name done"
