#!/bin/bash
# 기준선(광고 절반 시청) Bhalf/Chalf — tbl·s275 각 시드 10개
cd "$(dirname "$0")/sim"
jobs=()
for sd in 1 2 3 4 5 6 7 8 9 10; do for nm in Bhalf Chalf; do
  jobs+=("name=$nm seed=$sd tag=${nm}_tbl_s$sd"); jobs+=("name=$nm seed=$sd speedMax=275 tag=${nm}_s275_s$sd"); done; done
for sd in 1 2 3 4 5 6 7 8 9 10; do jobs+=("name=Bhalf seed=$sd equip=game tag=Bhalf_tbl_equipgame_s$sd"); done   # 민감도: 게임처럼 처음 얻은 묶음만 자동 장착
printf '%s\n' "${jobs[@]}" | xargs -P 8 -I{} bash -c 'a="{}"; t=$(echo "$a" | sed "s/.*tag=//"); timeout 900 dotnet bin/Release/net10.0/IdleSim.dll run $a stuck=20 > ../out/r_$t.txt 2>&1'
echo done
