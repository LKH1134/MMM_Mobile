#!/bin/bash
# 사용: run_pet_one.sh <base|cur> <시나리오 B|Bhalf|Bads> <시드> [maxDays]
cd "$(dirname "$0")/sim"
v=$1; nm=$2; sd=$3; md=${4:-120}
SP='C:/Users/user/AppData/Local/Temp/claude/82d5963c662fda11/2c54f97f-d275-490e-a84e-6bcf329252d2/scratchpad/tables'
if [ "$v" = base ]; then d=bin/base; else d=bin/Release/net10.0; fi
t=pet_${v}_${nm}_s$sd
timeout 900 dotnet $d/IdleSim.dll run name=$nm seed=$sd maxDays=$md stuck=20 tag=$t table="$SP/$v.bytes" > ../out/pet/r_$t.txt 2>&1
