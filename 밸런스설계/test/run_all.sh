#!/bin/bash
# 사용: bash 밸런스설계/test/run_all.sh  (결과: test/out/r_*.txt, stage_*.csv → aggregate.py가 모은다)
cd "$(dirname "$0")/sim"
EXE=bin/Release/net10.0/IdleSim.dll
OUT=../out
mkdir -p $OUT
jobs=()
for sd in 1 2 3 4 5 6 7 8 9 10; do
  for nm in A B C Bads Cads; do
    jobs+=("name=$nm seed=$sd tag=${nm}_tbl_s$sd")
    jobs+=("name=$nm seed=$sd speedMax=275 tag=${nm}_s275_s$sd")
  done
done
for sd in 1 2 3 4 5; do
  for pol in eff cost; do
    for nm in A B; do jobs+=("name=$nm seed=$sd policy=$pol tag=${nm}_tbl_${pol}_s$sd"); done
  done
  jobs+=("name=B seed=$sd restart=alive tag=B_tbl_alive_s$sd")
  jobs+=("name=B seed=$sd dungMyc=20 tag=B_tbl_dung20_s$sd")
  jobs+=("name=B seed=$sd fps=30 tag=B_tbl_fps30_s$sd")
done
for sd in 1 2 3 4 5 6; do jobs+=("name=A seed=$sd dt=0.02 goal=500 tag=A_dt002_s$sd"); done
printf '%s\n' "${jobs[@]}" | xargs -P 8 -I{} bash -c 'a="{}"; t=$(echo "$a" | sed "s/.*tag=//"); timeout 900 dotnet '"$EXE"' run $a stuck=20 > '"$OUT"'/r_$t.txt 2>&1'
echo done
