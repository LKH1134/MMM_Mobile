# HANDOFF — MoreMustMush 모바일 포팅

> 작성 2026-10-28 · **갱신 2026-11-12 (꼬마 호감도 삭제·표 이름 MainTable·용어 수확기)** · 기준 브랜치 `main` = `LKH` = origin `0f6365a`
> itch.io에 PC 웹빌드로 낸 **MoreMustMush**를 모바일(세로) **방치형**으로 옮긴 Unity 프로젝트입니다.
> 읽는 순서: **0장 지금 상태·출시 체크리스트** → 1장 기능 표 → 3~8장 구조·데이터·개발 메모 → 9장 남은 일. 커밋별 상세는 2장, 날짜별 작업 요약은 맨 끝 **10장**(기록). 팀 공유용 요약은 claude.ai의 "MoreMustMush 모바일 인계문서".
> 관련 문서: 방치형 수치 `BALANCE_IDLE.md`·`밸런스설계/README.md`·`HANDOFF_BALANCE_V3.md` · 최적화 `HANDOFF_OPTIMIZATION.md` · 개발자 모드 [HANDOFF_DEVMODE.md](HANDOFF_DEVMODE.md) · UI 개편 [UI_REWORK_LOG.md](UI_REWORK_LOG.md) · 표 조사 [DATA_AUDIT.md](DATA_AUDIT.md)

---

## 0. 지금 상태 (2026-11-11)

### 0.1 게임 한 줄 요약

- **씬 4개:** `Start`(로딩·광고 동의) → **`Main`(방치 전투 + 하단 탭, 상주)** ↔ `WorldTree`(세계수) / `InGame`(수확 던전 1~3).
- **Main:** 꼬마 5칸 방치 전투(웨이브 5 + 보스, `스테이지 n-n` 표기) · 강화 8종 · 가이드 퀘스트 바 · 일일/주간 퀘스트 · 룰렛 · 도감(세트 보너스) · 7일 출석 · 광고 상자 · 하단 탭(편성·세계수·홈·던전·뽑기, 스테이지로 해금).
- **던전 5종:** 1 수확 · 2 골드 · 3 균사석(= `InGame` 씬) · 4 보스 러시 · 5 보스 토벌(= 방치 화면). 해금 1-5 / 1-10 / 2-10 / 3-10 / 5-10(스테이지 5/10/20/30/50), 하루 2회 + 광고 2회(한국 00시).
- **수치·문구는 모두 표:** `Assets/Data/Tables/MainTable.xlsx`(→ `Resources/Tables/MainTable.bytes`) · `String.xlsx`(한국어·영어, → `String.bytes`). 표를 고치면 메뉴 `MoreMush/Data/Reimport Tables`. 새 시트는 빈 index 대역(19301~ 등)을 쓸 것(2장 "전수 조사 2차").
- **언어:** 한국어·영어(첫 실행 때 기기 언어). 11-11 점검으로 누락 0(2장). 코드가 내보내는 글자는 `Loc.T/F`, 프리팹 고정 글자는 `LocalizedText`.
- **광고:** AdMob 리워드(실제 ID) + UMP 동의. 시계 조작은 `GameClock`이 막음(뒤로 돌리기).

### 0.2 빌드 설정 (11-11 `ProjectSettings.asset`에서 확인)

| 항목 | 값 |
|---|---|
| 패키지 | `com.soomyouth.moremustmush` · 회사 `soomyouth` · 제품 `moremustmush` |
| 버전 | **`0.4.0` · versionCode `4`** (11-11 — 버전 이름은 `0f6365a`에서 0.1.0 → 0.4.0. versionCode 3은 Play Console에 올라감, 4는 아직 안 올려서 일부러 그대로 둠) — 시작 화면 버전 글자는 `Application.version`을 읽음(`VersionText`) |
| SDK | minSdk 25 · targetSdk **36** 고정 · IL2CPP · ARM64만 · 세로 고정 · 앱 분류 game |
| 업로드 키 | `C:/Users/user/Desktop/keystore/user.keystore` · alias `moremustmush` — **경로·별칭·Custom Keystore 켬이 `ProjectSettings.asset`에 커밋돼 있음**(11-06·11-10). 키 파일·비밀번호는 저장소에 없음. 다른 PC에서 빌드하면 경로를 그 PC에 맞출 것 |
| 광고 | 앱 ID `ca-app-pub-3609467797467871~5164610964`(`GoogleMobileAdsSettings.asset`) · 리워드 `ca-app-pub-3609467797467871/2013896497`(`AdService.cs`, `UNITY_ANDROID`) — **실제 ID** |
| 빌드 씬 | `Start` · `Main` · `WorldTree` · `InGame` |
| 개발자 모드 | `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`에서만 컴파일 → **Development Build를 끄고 빌드** |
| 마지막 빌드 | **`Builds/Android/MoreMustMush_0.1.0_4.aab`(67.2MB) + `.apk`(67.4MB)** — 11-11 13:43, `5ee13ef` 기준, versionCode 4 (2장 "11-11 출시 빌드"). **버전 이름은 0.1.0** — 그 뒤 `0f6365a`에서 0.4.0으로 바꿨으므로, 스토어·기기에 0.4.0으로 보여야 하면 다시 빌드할 것(코드 변경은 없음). 디버그 심볼 `MoreMustMush_0.1.0_4_aab.symbols.zip`(AAB에 안 들어감 → Play Console에 따로 올림). 이전: `MoreMustMush_0.1.0_3.aab`(11-10), `moremustmush-0.1.0-2.aab`(11-06), `-1.aab`(10-30) |

### 0.3 출시 빌드 체크리스트

**빌드 전**
1. [x] **versionCode:** 3은 Play Console에 올라감 → **11-11 4로 올림**. 다음 업로드마다 +1(같은 번호는 다시 못 올림).
2. [ ] `git pull` 후 `main` 최신(`0f6365a` 이후)에서 빌드. Unity에서 `MoreMush/Data/Reimport Tables` → 경고 없음 확인(MainTable 1373행·String 570행 OK가 11-12 기준).
3. [ ] 표·코드·씬에 새 글자를 넣었다면 `MoreMush/Fonts/Bake Static Atlases` 후 폰트 에셋 커밋(빠진 글자는 □로 나옴, `Reimport Tables`가 표 글자 누락을 경고).
4. [ ] Player Settings를 바꿨으면 **File › Save Project** 후 커밋(안 하면 브랜치 전환 때 되돌아감).
5. [ ] Publishing Settings에 키스토어·키 **비밀번호 두 개 입력**(에디터를 열 때마다 다시 입력). Build Profiles › Android: **Build App Bundle (Google Play) 켬 · Development Build 끔**, 네이티브 디버그 심볼 = Symbol Table(`Library`라 빌드 PC마다).

**빌드 후**
6. [x] (11-11 `_4` 빌드에서 확인 — 2장 "11-11 출시 빌드") AAB 확인: targetSdk 36 · versionCode · arm64-v8a · 네이티브 라이브러리 16KB 정렬 · 권한(INTERNET·AdMob이 넣는 광고 ID 등) · 업로드 키 서명 (10-30에 같은 항목을 확인한 기록은 8장 "출시 준비" AAB 빌드).
7. [ ] **실기기 확인** (지금까지 에디터·Firebase 벤치마크만): 노치·20:9·태블릿 화면, 뒤로가기(팝업 → 홈 → 종료), 실제 광고 보기·보상, UMP 동의(EEA는 VPN 등), 해금 팝업·잠긴 탭, 던전 1~5, 영어 화면(글자 넘침), 룰렛·도감·주간 퀘스트 탭 터치, 데이터 초기화(테스트 세이브로), 절전 모드 해제, BGM·광고 중 소리 멈춤.
8. [ ] 올리지 말 것: 같은 폴더의 `*_BurstDebugInformation_DoNotShip`, `.zip`.

**Play Console · AdMob (코드 밖)**
9. [ ] Play Console에 versionCode 4 AAB 올리기(versionCode 3은 이미 올라감 — 어느 트랙인지는 Play Console에서 확인). 첫 AAB의 키가 업로드 키로 등록돼 있으니 같은 키로 서명. 같은 화면 "앱 번들 탐색기"에서 디버그 심볼 zip도 올리기.
10. [ ] 스토어 등록정보·**개인정보처리방침 URL**·**데이터 보안 양식**(AdMob이 광고 ID·기기 정보 수집)·콘텐츠 등급·"광고 포함" 표시·대상 연령.
11. [ ] AdMob: 테스트 기기 등록(실제 광고를 누르지 않게), **개인 정보 보호 및 메시지 › GDPR 메시지 만들고 게시**(안 하면 EEA에서 양식 없이 광고 제한), 앱을 스토어와 연결, `app-ads.txt`(개발자 웹사이트가 있으면).

**출시를 막지 않는 알려진 것** (9장에 자세히): 밸런스 시뮬에 도감·주간 퀘스트·룰렛이 없음(진행이 빨라졌을 수 있음) · 주간 퀘스트 골드·균사석 임시값 · 효과음 없음(BGM만, SFX 슬라이더는 값만) · 버섯 이름 없음(도감 "식용 희귀 3") · 꼬마별 트리 안 함 · 세계수 외 저장값 백업·클라우드 저장 없음 · 옛 세이브 옮기기 코드 남음.

---

## 1. 한눈에 보기

| 항목 | 상태 |
|---|---|
| **Main 방치형 전투** (꼬마 5칸이 수확기 투척, 웨이브 5 + 보스, 패배 시 같은 스테이지 처음부터) | ✅ 동작 (3.1). 11-05: 보스 = 표의 10종(10스테이지마다 무작위 순서), 꼬마 169·버섯 121·보스 560px |
| Main 강화 목록 (공격력·체력·재생·공격 속도·치명타 + 상급 공격력·일반 적 피해량) / 방치 정산 팝업 / 앱 밖 골드 | ✅ 동작 (성장 곡선 10-30 재설계, `BALANCE_IDLE.md`). **11-10 KHD 개편 `3d2bc82`**: 공격력·체력·재생 Incremental 곡선, 공격 속도는 공격력 단계로 해금, 스테이지 71~200 적 2배·벽 보스 (2장) |
| **가이드 퀘스트** (11-10 KHD `3d2bc82`) | ✅ 전투력 위 한 줄 바(`GuideBarView`), `가이드` 시트 고정 11단계 → `가이드반복` 시트 반복 목표, 보상 균사석 (`GuideQuests`) |
| **꼬마 성장** (11-10 KHD `3d2bc82`) | ✅ Lv.5·10 관문에서 균사석으로 성장(1→2→3단계), 편성 화면 성장 버튼. 상수 `ggomaCopiesBase`·`ggomaGrowMycelite2/3` 등 9090~9098 |
| **도감 세트 보너스** (11-10 LKH `d3a6597`) | ✅ 방치 보스 처치마다 열린 무리(종류 × ★1~5)에서 새 버섯, 무리 완성 = 식용 공격력·약용 체력/재생·독성 공격 속도 %. `도감`·`도감세트` 시트, `MushroomDex`. 진행 영향 실측 전 |
| **룰렛** (11-10 KHD) | ✅ 홈 왼쪽 버튼, 하루 3회·5분 대기, 골드·균사석 6칸 (`룰렛` 시트 18201~, 상수 19006·19007 — 11-10 전수 조사 2차에서 9104·9105에서 옮김) |
| **던전 1~5** (11-06 KHD `dc3c7a2`) | ✅ 던전 시트 `mode`로 진행 방식: 1 수확·2 골드·3 균사석 = `Harvest`(InGame 씬 재사용, `DungeonSession.Current`로 구분, 보상만 다름) / 4 보스 러시 = `BossRush`(방치 화면, 스테이지 보스 `bossRushCount` 5마리 연속, 보스마다 보상 1/n) / 5 보스 토벌 = `BossLevel`(방치 화면, 한 마리를 잡을 때마다 레벨업, `bossLv*` 상수, 60초). 해금 스테이지 **5/10/20/30/50**, 하루 `dungeonPerDay` 2회 + 광고 `dungeonAdPerDay` 2회(한국 00시). 입장권은 보상 받을 때 씀. 레벨 보상은 깼을 때만(소탕 없음). 실기기 전 |
| **기능 해금** (11-09 KHD `6c206b6`, `Unlock/`) | ✅ 편성 = 스테이지 3(1-3 진입) · 소환 = 편성 화면 처음 열기 · 던전 = 던전 시트 가장 낮은 `unlockStage` · 세계수 = 스테이지 7 (`FeatureUnlock`, PlayerPrefs `Unlock.<기능>`, 한 번 열리면 안 잠김). 잠긴 탭 어둡게+자물쇠·눌러서 안내(`BottomBarLocks`), "새로운 콘텐츠!" 창(`UnlockAnnouncer`, 코드 생성 오버레이), 탭 가리키기(`TutorialSpotlight`) |
| **언어 (한국어·영어)** (11-09 KHD `4fbf3b0`) | ✅ `Assets/Data/Tables/String.xlsx`(key·ko·en·memo, 577행 — 11-11) → `Resources/Tables/String.bytes`. `Loc`(첫 실행 때 기기 언어: 한국어 기기 = 한국어, 나머지 = 영어, 옵션에서 바꿈 `Option.Language`), 글자에 `LocalizedText`, 언어 버튼 `LanguageButton`. 번역 없으면 한국어, key 없으면 기본 문구. **새 화면 글자는 문자열 표에 key로 넣을 것** |
| Main 하단 탭: 편성(꼬마 편성·장비) · 세계수 · 홈 · 던전 · 뽑기(결과 연출 `GachaResultPopup`) | ✅ 동작. 탭은 스테이지로 해금(기능 해금 줄). 던전은 위 던전 줄. 뽑기 버튼 셋은 **뽑기상품 시트**(종류마다 광고 1 · 재화 2, 가격·횟수·광고 하루 한도 — 광고 뽑기 35회, 11-10 KHD). 뽑기 3종: 수확기·방어구·꼬마(11-04 펫 삭제) |
| 던전·뽑기 탭 그림 (배너·카드, Codex 생성) | ✅ 2026-10-30 (`Assets/Art/Generated/Dungeon`·`Gacha`). 11-05 소환 배너·던전 배너·던전 1~5 카드·로딩 배경(`bg_title`) 교체 |
| 해상도 대응 (`StageFrame`·`FitWidth`, 16:9~20:9·노치, 남는 쪽 검정) | ✅ 2026-10-30 에디터에서 측정 (3.2). 실기기 재확인 필요 |
| Android 뒤로가기 (팝업 닫기 → 홈 → 종료 확인 / InGame = 설정) | ✅ 2026-10-30 로직 확인. 실제 키 입력은 실기기 확인 필요 |
| Google Play 대상 API 36 / 세로 고정 / 업로드 키 서명 AAB | ✅ 빌드 설정은 **0.2**(versionCode 4). versionCode 3은 Play Console에 올라감, 4 빌드는 0.2 마지막 AAB 줄 |
| 재화: 웨이브 적 = 골드, 보스 = 균사석 + 골드 / Main 재화 바 상시 표시 | ✅ 2026-10-30 (테스트 균사석 50,000 지급 제거·회수). 11-02 던전·뽑기 탭에서도 보임(`TabDock`이 재화 바를 배너 제목 줄 오른쪽으로 옮김) |
| 씬 전환 로딩 팝업 (`SceneLoader`) / 팝업 하나만 (`PopupGate`) / 팝업 층 (`PopupLayer`) | ✅ 2026-10-30 |
| 절전 모드 (`PowerSaveMode`, Main에서 3분 무입력 → 검은 덮개·10fps) | ✅ 2026-10-30 (KHD) |
| InGame = **던전 1** (판 하나 → 결과 "한 번 더하기 · 나가기") + 첫 입장 튜토리얼 | ✅ 2026-10-30 복원 (KHD). 11-03 상단 HUD 정리(보유 골드·균사석, 스테이지 글자 오른쪽)·임시 그림 8장 교체(Codex). 아래 InGame 줄은 판 플레이 규칙 기록 |
| InGame 핵심 플레이 (수확기·바·군락지·타이머·점수·콤보) | ✅ 동작 |
| 배속 1/2/4, 결과·옵션 팝업 | ✅ 동작 (결과 팝업은 10-30 간소화: 번 골드만) |
| ~~스킬 트리 화면~~ | 🗑️ **씬 제거 (10-30, KHD)** — `Backup/SkillTree/`. 스킬 표·`SkillProgress`는 남음 (5장) |
| 스킬 해금 조건 = 선으로 이어진 이전 노드 1레벨 이상 하나뿐 | (스킬 트리 제거로 지금 쓰지 않음) |
| 스킬 효과 전부 (스탯·바 버프·소환·확률 스킬·장치·디버프·보스) | ✅ 동작 (플레이 모드에서 코드로 흉내 내 확인) |
| 스테이지 50개(표) + 51부터 무한 생성, 보스 5종 (기믹 4종) | ✅ 동작 (무한 스테이지 2026-10-28, 4.5) |
| 일반 스테이지 | ✅ 목표 없음 — 판을 마치면 늘 다음 스테이지 (2026-10-28) |
| 보스 HUD (이름 + 본체 체력 바) | ✅ 2026-10-28 |
| 성능 벤치마크 (FPS·CPU·GPU·메모리) + Firebase Test Lab Game Loop | ✅ 2026-10-28 (8장) |
| 데이터 초기화 | ✅ 11-10 KHD `25384f8` 옵션 창에 다시 넣음: 5초 누르기 경고 창(`HoldConfirmPopup`) → 기록 전체 삭제 → Main. (10-30에 한 번 뺐었음) |
| **세계수 (WorldTree 씬)** | ✅ 11-02 LKH. 성장·꼬마·공방·꾸미기 동작(플레이 모드 확인, 수확 보따리는 11-03 KHD `b28b237`에서 제거). 11-05 나무 그림 4장·장식 24종 새 그림, 수치는 표, 밸런스 v4(11-03 KHD 방치 v3 위에서 재검증 통과). 하단바 세계수 = `SceneButton`(WorldTree). 실기기·터치 확인 전. **전투 효과 크기(U1)는 사용자 결정 대기** (3.3) |
| **세계수 의뢰 (일일·주간)** | 🗑️ 11-10 삭제 → 메인 퀘스트로 합침 (세계수 경험치 = 퀘스트 `treeExp`). 11-03 원래 구현 기록은 3.3 |
| 표 ↔ 게임 연동 (장비·뽑기상품·방치전투·수확전투·수확등급·던전) | ✅ 11-02 LKH (6.0). 플레이 모드에서 값이 표대로 들어가는 것 확인 |
| 지갑 (골드·균사석 저장, Main 재화 바 표시) | ✅ 동작 |
| 스킬 가격 (배우기 때 차감) | ✅ 동작 |
| (던전 1 판 플레이 시절) 밸런스 — 가격·스테이지 보상·보스 체력 | 기록용: M2(10-27) 50보스 88.3분은 옛 규칙(일반 스테이지 목표) 기준(6.2). **방치형 밸런스는 `밸런스설계/`, 남은 일 9.2** |
| 밸런스 측정기 `BalanceBot` | ✅ 헤드리스 캠페인·보스 반복 측정 |
| **AdMob** | ✅ SDK Google Mobile Ads Unity 11.6.0 + EDM 2.1.0. **앱 ID·리워드 단위 ID 모두 실제 ID**(11-06 KHD `bd85376`, 값은 0.2). UMP 동의는 Start 로딩에서(8장 "출시 안정화"). 상태 문구는 `ad.status_*`(11-11). 실기기 확인 전 |
| **광고 상자** (`Ads/AdBox.cs`·`AdBoxPopup.cs`) | ✅ 홈 화면을 볼 때만 시간이 감(`adBoxIntervalHours` 1시간) → 전투 화면 위 빈 풀밭 두 자리(왼쪽 버튼 열 옆·오른쪽 전투력 옆)를 오가며 `adBoxStaySeconds` 60초 머묾 → 누르면 "광고 보기" 창 → 끝까지 보면 **골드(스테이지 적 처치 골드 × `adBoxGoldKills`) 또는 균사석(`adBoxMycelite` 90) 중 하나**(50:50 `adBoxGoldChance`, 같은 보상 연속 상한 `adBoxMaxSameInRow` 2). 놓치거나 보면 쿨타임 처음부터. 수치는 상수 시트, 상자 그림 임시(`icon_chest`). 실기기 확인 전 |
| **일일·주간 퀘스트** (`Quest/DailyQuests.cs`·`UI/Main/QuestScreenView.cs`) | ✅ 전투 화면 왼쪽 퀘스트 버튼 → 창(하단 주간/일일 탭·일괄 수령). **퀘스트 시트**: `period` daily/weekly, 종류 kill/stage/upgrade/gacha/ad/dungeon/daily(주간만), 보상 골드(= 스테이지 적 처치 골드 × `goldKills`)·균사석·**세계수 경험치 `treeExp`**(11-10 세계수 의뢰를 합침). 일일 = 한국 00시, 주간 = 월요일 한국 00시. 진행도는 라이브 플레이만(앱 밖 정산분 제외). 받을 보상이 있으면 빨간 점. **주간 골드·균사석은 임시값** |
| 사운드 | 🟡 11-05 **BGM만**: 메인 BGM Glowing Woodland(`BgmPlayer`, `Resources/Audio/Bgm`, Start 다음 씬부터 반복·씬 전환에도 유지, 광고 중 `AudioListener.pause`), 모든 옵션 창 배경음 슬라이더 = BGM 음량(`BgmVolumeSlider`, `Option.BgmVolume`). **효과음은 없음**(SFX 슬라이더는 값만 저장) |
| 개발자 모드 | ✅ 11-05 (`` ` `` 키, 에디터·Development Build만) — [HANDOFF_DEVMODE.md](HANDOFF_DEVMODE.md). 11-06 출시 빌드에서 컴파일 제외(`UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, `bae4ea3`) |
| 7일 출석 · 퀘스트 일괄 수령 | ✅ 출석 11-06 KHD(2장, 보상은 `출석` 시트 19101~). 퀘스트 창 하단 **일괄 수령**(일일+주간, 받을 게 없으면 회색, 11-09 `2a205ca`) |
| 실기기 터치 테스트 | ⚠️ 안 함 — 에디터·Firebase 벤치마크만. 확인 목록은 0.3 7번 |
| 아트 | ⚠️ 일부 임시. 던전 버섯·수확기·링·화살표·배속 아이콘은 11-03 Codex 그림(`Art/Generated/InGame`, `Art/Placeholder`는 삭제). 등급·황금·버프·장치·보스는 여전히 색으로만 구분(던전 쪽). 11-05 세계수·장식·로딩·소환·던전 배너/카드·방치 보스 10종 그림 교체 |

**환경:** Unity 6000.3.19f1 · URP · Input System (`InputSystemUIInputModule`) · TextMesh Pro · Unity MCP (`.mcp.json`, `http://localhost:8080/mcp`) · 이미지 생성은 Codex CLI(`codex exec`, `image_generation` 기능)
**저장소:** `https://github.com/LKH1134/MMM_Mobile` (`main` — 11-06부터 LKH·KHD·PJS를 main에 모아 머지. **11-11 기준 main = LKH = origin `0f6365a`**, KHD 브랜치는 `6b71941`(main에 포함) — KHD의 11-11 `5ee13ef`는 main에 바로 올라옴. 작업 브랜치: `LKH`(이 문서 기준) · `InGame`(10-30 방치형 통합) · `KHD`(장비·UI·방치 밸런스·최적화·광고). 원격에 `Admob`·`UI_SafeArea` 브랜치도 있음 — 이 문서는 다루지 않음)
**커밋 규칙:** `[태그] YYMMDD 설명` (예: `[ADD] 261026 스테이지·보스 표 및 보스 기믹`)

---

## 2. 커밋

### 2026-11-12 (`LKH`) — 용어 수확기 · 표 이름 MainTable · 꼬마 호감도 삭제

- **용어:** 화면의 '무기' → **'수확기'**, 영어 'Weapon' → **'Harvester'** (`String.xlsx` `equip.weapon`·`equip.summon_weapon`·`attendance.final_msg`, `Wepon.prefab`·`FormationScreen.prefab` 고정 글자, `Equipment.Weapon` 기본 이름). 코드 이름(`Weapon`, `GachaKind.Weapon`)과 주석은 그대로.
- **표 이름:** `GameBalance.xlsx` → **`MainTable.xlsx`**, `Resources/Tables/GameBalance.bytes` → **`MainTable.bytes`** (GUID 유지). `GameBalance.ResourceName = "Tables/MainTable"`, `TableImporter`의 표 이름 `MainTable`. C# 클래스 `GameBalance`·`GameBalanceValidator`는 이름 그대로. 도구·문서·에이전트 경로도 바꿈(`test/out` 기록 제외).
- **꼬마 호감도 삭제:** 보따리 삭제(11-03) 뒤 효과가 없던 호감도를 통째로 지움.
  - 코드: `WorldTree.Kids.cs` 호감도·선물(`AffinityLevel`·`Gift`·`GiftPick`·`GiftValue`·`Likes`) 삭제, 부탁·장식 첫 배치는 호감도 없이 골드·균사석만. `KidState.affinity` 삭제(예전 저장의 값은 읽을 때 무시됨). `AffinityRow`·`GameBalance.AffinityLevels`·`link.xml` 항목 삭제.
  - 화면: `KidPopup` — 선물 버튼(`Info/Merge`)·호감도 줄(`Info/Stat`)·꼬마 칸 `GiftBadge` 삭제, 진화 버튼(`Place`)을 가운데로·특기/진화 조건 줄을 위로 당김. `TreeKidScreen`·`KidCellView`에서 해당 필드 삭제.
  - 표: `호감도` 시트, 상수 `giftFavoriteMul`·`giftMaxCount`·`requestAffinity`·`decoFirstAffinity` 삭제. 문자열 `kid.badge_gift`·`kid.affinity_lv`·`kid.affinity_max`·`kid.gift*` 7개 삭제, `kid.reward(_line)`·`deco.first_place`에서 호감도 부분 삭제. `꼬마.favoriteIds`는 부탁 버섯으로 계속 씀.
  - **세계수 모델 현행화 (같은 날):** 이론(`밸런스설계/세계수/theory/` v7, `이론.md` 맨 위)·검증(`verify/` 라운드 8, `검증.md` 맨 위)을 지금 코드에 맞춤 — 보따리(11-03)·꼬마 등급(11-04)·의뢰 시트(11-10)·호감도/선물(11-12)을 빼고 메인 퀘스트 세계수 경험치를 넣음. 둘 다 그 전부터 지금 표로는 안 돌던 상태였다. 같은 11-03 feed면 8단계 이론 36.0일 · 검증 36.33일로 맞음. 지금 표 근사 feed(검증 `r8/feed_now.py`)로는 보통 39.33 · 무광고 40.33 · 광고 많이 38.67일. 검증 판정 **조건부 통과**(조건 = 처치 골드 효과 실측). 두 모델의 ★3 시점(이론 59일 · 검증 40~41일)은 하루 꼬마 뽑기 입력(약 34 vs 45~48회) 차이 — 방치 feed를 실측으로 바꿔야 확정(9.2).

### 2026-11-10 ~ 11-11 (`main` = `LKH` `0f6365a`) — 커밋 표

| 커밋 | 작성 | 내용 |
|---|---|---|
| `83780b0` | LKH | 인계 문서 11-06~11-09 반영 |
| `34bcc5f` | LKH | **코드에 있던 콘텐츠 수치를 표로** (아래 절) |
| `3d2bc82` | KHD | **밸런스 개편** — 공격력·체력·재생 `valueMode` Incremental(Lv 100까지 +10/+10/+1, 비용 Lv 50까지 +1골드·100까지 +2골드, 뒤로 증가량 커짐), 공격 속도는 공격력 단계표로 해금(2500에서 3.0/s, 비용 가중 `speedCostRampFromLv` 30·`speedCostRamp` 115), 공격력으로 열리는 강화 비용 보정(`GateCostFactor`), 스테이지 71~200 적 체력·공격 2배, 벽 보스 `wallBoss*`(100·150·200 ×5, 250부터 50단위 ×2, 던전 4·5 제외). **가이드 퀘스트**(`GuideQuests`·`GuideBarView`, `가이드`·`가이드반복` 시트, 전투력 위 한 줄 바, 고정 11단계 뒤 반복, 균사석). **꼬마 성장**(Lv.5·10 관문에서 균사석, 편성 화면 성장 버튼, 상수 `ggoma*` 9090~9098). 광고 뽑기 11 → 35회, 적 공격 연출, 가이드 바 자리만큼 전투 영역·왼쪽 버튼 위로 |
| `61bb3ad` | KHD | 뽑기 결과창 간격·등급 연출, 완료한 의뢰 회색(`DoneCover`), 강화 잠금 줄 모양 |
| `f6dcef5` | KHD | **시뮬을 지금 게임 규칙에 맞춤**(`밸런스설계/test/sim`: 광고 상자 한 가지 보상·꼬마 성장·가이드 퀘스트·출석 균사석·광고 뽑기 횟수·던전 보상을 게임 함수로·던전 입장 상수) + 수치안·집계 스크립트(`밸런스설계/test/khd_prop.py`·`khd_dung.py`·`khd_agg.py`), 결과 `밸런스설계/test/out/khd1110`·`stage_k_*`·`snap_k_*` |
| `702cf88`·`f0f912e` | KHD | **모바일 최적화·안정성**: 가이드 바 바뀐 글자만 갱신, 뽑기 반짝임 객체 재사용·RectMask2D 필요할 때만, 강화 비용 하한 1·지수 넘침 보정, 모르는 가이드 저장값 처리, 큰 dt 공격 연출 1회, 재컴파일 뒤 하단 잠금 NRE, 전투 영역 40 내림, 120일 안정성 시뮬(`stab_*`) |
| `1fe2c8d` | LKH | **방치 스테이지 표기 `스테이지 n-n` 통일** (아래 절) |
| `11b63ed` | LKH | main → LKH 머지 (아래 절) |
| `d3a6597` | LKH | **도감 세트 보너스** (아래 절) |
| `e52a545` | KHD | 옵션 창 미구현 기능 제거(진동·알림·연출 간소화), 언어 설정 가운데, 창 높이 360 줄임 |
| `25384f8` | KHD | **옵션 창 데이터 초기화** 버튼 + 5초 누르기 경고 창(`HoldConfirmPopup`) — 전투를 먼저 멈추고 기록 전체를 지운 뒤 Main으로, 가이드·출석 캐시도 비움 |
| `59cee33` | KHD | 판정 최적화: 수확 충돌에서 버섯 위치 캐시·빈 목록 검색 생략, 장식 그래픽 `raycastTarget` 끔, `RaycastTargetAudit`(메뉴 `MoreMush/Optimize`) |
| `61c9a5b`·`c1ec5a4`·`8e6fb58` | KHD | **룰렛**(`Roulette`·`RouletteScreen`, 홈 왼쪽 버튼): 하루 3회(한국 00시), 한 번 돌린 뒤 5분 대기(게임 시계), 6칸 골드·균사석, 결과를 돌리는 순간 정해 저장. 수치는 `룰렛` 시트(머지 뒤 18201~18206)·상수 `rouletteDailyCount`·`rouletteCooldownMinutes`(9104·9105). 세계수 배경 변경 |
| `5dc8fa1` | LKH | **세계수 의뢰 → 메인 일일·주간 퀘스트** (10장 11-10 요약) |
| `95449fe` | LKH | KHD → LKH 머지 (10장 11-10 요약: 룰렛·상수 ID 옮김, OptionPopup 루트 끔) |
| `9eb2de6`·`eaba113` | LKH | 도감 버튼 위치 복구, 세트 목록 "(n-n부터)" 삭제 |
| `ebb34e3` | LKH | 인계 문서 11-10 반영 |
| `40f8d1b` | LKH | **안드로이드 versionCode 3**, 업로드 키 별칭 `moremustmush` |
| `6b71941` | LKH | **데이터 전수 조사 2차** (아래 절) |
| `1663e2c` | LKH | **번역 누락 점검·수정** (아래 절, 11-11) |
| `30bce04` | LKH | 인계 문서 출시 빌드 기준 정리(0장·9장 재구성), **versionCode 4** |
| `5ee13ef` | KHD | **잔버그·폴리싱** (아래 절, 11-11) |
| `0f6365a` | LKH | **버전 이름 0.1.0 → 0.4.0** (`bundleVersion`, versionCode는 4 그대로) |

### 2026-11-11 — 출시 빌드 (`5ee13ef` 기준)

- **결과:** `Builds/Android/MoreMustMush_0.1.0_4.aab`(67.2MB, 2분 23초) · `MoreMustMush_0.1.0_4.apk`(67.4MB, 41초), 둘 다 오류 0 (`build_result_0.1.0-4_rebuild.txt`). 심볼 `MoreMustMush_0.1.0_4_aab.symbols.zip`·`…-v4-IL2CPP.symbols.zip`. 같은 폴더 `[AAB]`·`[APK]…zip`은 전달용 압축.
- **확인:** versionCode 4 · targetSdk 36 · 업로드 키 서명(SHA-256 `F5:27:28…31:10`) · `.so` 8개 모두 16KB 정렬 · APK zipalign 16KB · 개발자 모드(`DevMode`)·`SkipAds` 코드 없음 · `5ee13ef`의 새 함수(`GatedCost`·`NextStep`·`OwnCurve`·`StepStartLevel`)가 들어감. **버전 이름은 0.1.0**(`0f6365a`의 0.4.0 전).
- **빌드가 멈출 때:** 10:38 첫 시도가 EDM(External Dependency Manager)의 "Resolving Android Gradle dependencies."에서 2시간 넘게 멈췄다(그 사이 이전 `_4` 결과물도 지워짐). 에디터를 다시 켜고 키 비밀번호를 다시 넣은 뒤 빌드하니 6분 만에 끝남. Editor.log가 5분 넘게 안 바뀌면 멈춘 것으로 보고 에디터를 재시작할 것.
- **빌드 뒤 되돌릴 것:** 빌드할 때마다 줄 끝만 바뀌는 파일(gradleTemplate.properties·Mobile_RPAsset·URP 전역 설정·GraphicsSettings·ProjectSettings)과 지워지는 `PerformanceTestRunInfo/Settings.json(+meta)` → `git checkout`으로 되돌림. 복구 씬 `Assets/_Recovery`(에디터 강제 종료 때 생김)도 지움.

### 2026-11-11 (`KHD` `5ee13ef`) — 잔버그·폴리싱

- **해금 강화 비용 (`IdleProgress.ComputeCost`):** `3d2bc82`의 `GateCostFactor`(옛 곡선 대비 비율 곱하기)를 `GatedCost`로 바꿈. 해금·단계로 열리는 강화(공격 속도·치명타·고급 공격력·일반 적 피해량)는 **단계 첫 레벨 비용 = 그 단계를 연 기준 강화(공격력 등)를 그 기준 레벨에서 한 번 올리는 비용**, 단계 안에서는 다음 단계 첫 비용까지 곱으로 고르게 오름, 마지막 단계는 시트 곡선 비율(`OwnCurve`). 단계 첫 레벨·다음 단계는 `StepStartLevel`·`NextStep`(공격 속도는 `SpeedSteps`, 나머지는 `capStep`). → 해금된 강화가 열리는 순간 너무 싸거나 비싸지 않게. **시뮬은 아직 옛 공식** — 9.2-1 재측정 때 같이 확인.
- **가이드 바 (`GuideBarView`):** 받기를 누르면 "균사석 +n 획득!" 문구(0.9초) 없이 바로 다음 목표. → String 표 `guide.claimed`는 이제 안 씀(9.4).
- **던전 목록 (`DungeonUI.prefab`):** 입장권 줄 4곳에 열쇠 아이콘(`UI/Icon/key`) Image 추가.

### 2026-11-11 (`LKH`) — 번역 누락 점검

정적 분석으로 영어 화면에 한국어가 남는 곳을 찾았다(스크립트는 커밋 안 함: 표·코드 key·프리팹/씬 TMP 글자·LocalizedText·폰트 대조).

- **문제없음:** String 565행 영어 칸 빈 곳 0 · 코드의 key 모두 표에 있음(조합 key `upgrade.<스탯>`·`effect.<효과>`·`unlock.<기능>.name/desc`·`equip.*` 포함) · 게임 표 12시트 한글 `name` 150개 모두 `name.<index>` 있음 · LocalizedText 110개 key 있고 프리팹 글자 = 표 한국어(다르면 언어를 바꿔도 안 바뀜) · 프리팹·씬 한글 146곳은 코드가 `Loc`로 덮어쓰거나 꺼진 템플릿 · 정적 아틀라스(Bd·Rg 746자)에 표 글자 전부 있음.
- **고친 것 3곳:**
  - 광고 상태 문구: `AdService.Report`가 한글을 직접 넘겨 광고 상자 창 상태 줄에 그대로 떴다 → `Report(key, detail)`, 화면엔 `ad.status_*` 11개 문구만, SDK 오류 내용은 로그에만.
  - 세계수 꼬마 화면 빈 목록 안내(`KidPopup` `EmptyText`, 프리팹 고정 한글) → `TreeKidScreen`이 `kid.no_kids`(표에 있었지만 안 쓰던 key)를 씀.
  - 시작 화면 `버전 0.1.0`(고정 글자) → 새 `VersionText`(Start 씬 `Version`에 붙임)가 `start.version` + `Application.version`.
- 표: String 12행 추가(`밸런스설계/tools/add_loc_fix_strings.py`), Reimport Tables String 577행·GameBalance 1387행 OK. 새 글자는 아틀라스에 이미 있어 폰트 다시 굽기 불필요. 컴파일 오류 없음.
- **새 화면 규칙:** 코드가 화면에 내보내는 글자는 `Loc.T/F`, 프리팹 고정 글자는 `LocalizedText`(프리팹 글자를 표 한국어와 똑같이).

### 2026-11-10 (`LKH`) — 데이터 전수 조사 2차 (`6b71941`)

오늘 들어온 코드(가이드·강화 개편·룰렛·도감·퀘스트 합침 등, `34bcc5f` 이후 51파일)를 다시 훑고 `Tools/audit_game_data.py`로 표 전체를 대조했다. 표 편집 스크립트 `밸런스설계/tools/table_audit_1110.py`(엑셀 XML 직접 수정).

- **새 시트 `공격속도단계`**(19201~19208, `SpeedStepRow`: gateLevel·capLevel) ← `IdleProgress.SpeedSteps` 배열(공격력 20/375/…/2500 → 공격 속도 30/60/…/275). 검증기: 행 있음·둘 다 오름차순·첫 gateLevel = 공격 속도 `unlockLevel`(20)·마지막 capLevel ≤ `maxLevel`(275).
- **상수 19008~19013**: `guideFallbackKills` 50·`guideFallbackMycelite` 50(`GuideQuests` — 반복 목표가 없을 때 대신 내는 처치 목표), `upgIncLinearLevel` 100·`upgIncCostStep1Level` 50·`upgIncCostStep1` 1·`upgIncCostStep2` 2(`IdleProgress` 증분 강화 구간, 검증기: step1Level ≤ linearLevel).
- **리소스 7426~7437**: 미등록 그림 12개(강화 아이콘 `UI/Icon/Upgrade_Icon/*` 8장, `UI/Icon/warning`, `UI/roulette_pin`·`roulette_wheel`, `Sprite/BG/image2`). 감사 도구 문제 12 → **0**.
- **업그레이드 아이콘**: 표 `resourceId`가 옛 그림(7021~7028 `Art/Dream/Icons`)을 가리켜 아무도 안 읽고, 화면은 프리팹에 놓은 그림을 썼다 → 표를 실제 그림으로(공격력 7366 `UI/Icon/sword`, 나머지 7428~7434) 바꾸고 `UpgradeNotebookView`가 줄 아이콘을 표에서 읽는다(못 읽으면 프리팹 그림). 7021~7028 행은 남겨 둠.
- **시트끼리 겹치던 index 정리**(지금까지 검사는 시트 안만 봤다): 상수 9099~9105가 던전 9101~9105와, 출석 17001~가 가이드 17001~과 같은 번호였다 → **상수 9099~9105 → 19001~19007**(상수는 이제 9001~9099 + 19001~19099 두 대역, 게임 읽기 범위 9001~19099), **출석 → 19101~19107**(범위 19101~19199). 코드는 key로 읽어 동작은 같다. 검증기 `ValidateUniqueIndex`가 시트를 넘는 겹침을, `ValidateConsts`가 상수 대역을 막는다. **새 시트는 빈 대역(19301~ 등)을 쓸 것.**
- 시뮬 `밸런스설계/test/sim/Tables.cs`: 상수 범위 9001~19099, `speedSteps` 주입. ※ 시뮬 csproj의 게임 dll 경로가 다른 세션 scratchpad(`b_cur`)를 가리켜 그대로는 빌드가 안 된다(가이드 등 없음) — `Library/ScriptAssemblies` dll로 바꿔 빌드하면 오류 0, `smoke` 실행·검증기 문제 0 확인.
- 확인: 컴파일 오류 없음, Reimport Tables **1387행 OK·폰트 경고 없음**, 에디터에서 옛 코드 식과 대조 — 공격 속도 단계 cap(공격력 0~3000)·다음 단계 gate(Lv 0~274)·증분 비용/값(공격력·체력·재생 Lv 0~3000) **불일치 0**, 상수·출석(7일·무기 11010) 값 그대로, 강화 아이콘 8개 로드 확인.
- **표로 옮기지 않은 것**(구조·연출·안전값): 룰렛 칸 수 6(`Roulette.SliceCount` — 원판 프리팹 칸 수, 검증기가 행 6개 확인), 도감 별 5·종 수 31(저장 비트), 꼬마 성장 2회(`Formation.GrowStageCount`), 룰렛 회전 시간·5초 누르기·가이드 바 깜빡임·적 공격 연출·뽑기 반짝임 시간, 퀘스트 보상 넘침 보호(9e18). 버섯 **이름**은 아직 정해지지 않아 표에 없음(도감 화면 임시 "식용 희귀 3").

### 2026-11-10 (`LKH`) — 방치 스테이지 표기 통일 (`1fe2c8d`)

- 방치 스테이지 글자는 모두 `IdleBattle.HudStageNumber(stage)` = **`챕터-칸`** (스테이지 번호 s → `(s-1)/stagesPerChapter+1`-`(s-1)%stagesPerChapter+1`, 5 → 1-5, 11 → 2-1)로 쓴다. 표·코드 안의 값(해금 스테이지 등)은 계속 번호로 적고 **보여 줄 때만** 바꾼다.
- 고친 곳: 던전 카드 잠금 "스테이지 5 진입 시 해금" → "스테이지 1-5 …"(`DungeonEntry`), 스테이지가 오를 때 가운데 글자(`IdleBattle`), 절전 화면(`PowerSaveMode`), 개발자 모드 "방치 스테이지 2-5 (15)". 전투 배너는 같은 계산을 따로 하던 것을 합침. 원래 `n-n`이던 곳: Main HUD·방치 정산·잠긴 탭 안내.
- 그대로 둔 것: 수확 던전(InGame) 상단 "스테이지 n"(던전 판 번호), 세계수·꼬마 "n단계", 가이드 "스테이지 n번 진입"(횟수). String `dungeon.unlock_at_stage` 메모 `{0}=챕터-칸 (1-5)`.

### 2026-11-10 (`LKH`) — main 머지 (`11b63ed`)

- 엑셀 4개(GameBalance·String xlsx/bytes)가 바이너리 충돌 → **main 표를 기준으로 LKH 변경만 다시 얹음**: `add_attendance_consts.py`를 main 표에 다시 돌림(출석 시트는 빈 시트 번호 자동), String 메모 1줄. **main(`3d2bc82` 꼬마 성장)이 상수 9090~9098을 먼저 써서 LKH 상수를 9099~9103으로 옮김**(코드는 key로 읽어 영향 없음, 스크립트 번호도 고침).
- `IdleBattle.cs`: main의 `GuideQuests.Report(GuideKind.StageEnter)`와 LKH의 `HudStageLabel`을 함께 둠. 컴파일·Reimport Tables(1322행) 통과.

### 2026-11-10 (`LKH`) — 도감 세트 보너스

- **표**: 새 시트 `도감`(18001~18015, `DexRow`: kind `MushroomKind`·star·count·resourceId·unlockStage·dropChance)·`도감세트`(18101~18120, `DexSetRow`: kind·star·stat `DexStat`·value %). 그림은 기존 리소스 7275~7289(`Art/Dream/Index` 15장, 조각 = 종). `info` 3행. 스크립트 `밸런스설계/tools/add_dex_sets.py`·문구 `add_dex_strings.py`(String `dex.drop`·`dex.set_done`·`dex.set_header`·`dex.set_from`·`dex.set_active`).
- **검증기**: 두 스키마, `MushroomKind`·`DexStat` 자료형, 종류×별 한 행·star 1~5·count 1~31(저장 비트)·dropChance 0~1·그림 시트에 `시트이름_0..count-1` 조각, 세트 행은 도감 무리를 가리키고 무리·능력치 중복 없음·값 양수. `link.xml`에 두 행 클래스. Reimport Tables **1357행 OK, 폰트 경고 없음**.
- **코드**: `MushroomDex`(저장 `Dex.<index>` 비트·`RollBossDrop`·`Percent(DexStat)` 캐시·`Changed`), `IdleProgress.AttackMultiplier`/`HpMultiplier`/새 `HpRegenMultiplier`에 도감 % 더함, 새 `SpeedMultiplier`를 `FinalSpeed`에 곱함(전투력·통계 창도 따라감), `IdleBattle` 방치 보스 처치 때 드롭(던전 보스 제외) + 보스 자리 위 "도감 +1 식용 ★2"·"… 세트 완성!" 글자.
- **UI** (`DexScreenView`·`DexScreen.prefab`): 목록을 도감 시트로 만듦(전엔 그림 폴더를 훑음), 못 얻은 버섯 = 그림자 + "???", **세트 보너스 버튼**(전엔 꺼둠) = `Body/SetPanel`(Scroll 자리에 TMP 한 장): 종류 탭별 수집 수·★1~5 모은 수/종 수·효과(완성 초록)·적용 중 합(안 열린 무리의 "(n-n부터)" 표시는 `eaba113`에서 뺌). 도감 버튼 = 다시 목록. KHD 머지 뒤 화면 밖으로 밀린 Main 도감 버튼은 왼쪽 버튼 열 룰렛 아래로(`9eb2de6`).
- 확인: 에디터에서 ★1·★5 무리를 채워 배율이 표대로 오르는지(공격 ×+22.05, 체력·재생 +12.6, 속도 ×1.33), 스테이지 1에선 ★1만 나오는지, 그림 누락 0, 플레이 모드 도감 목록·세트 목록 화면. 시험 저장 키는 지움(사용자 세이브 그대로).

### 2026-11-10 (`LKH`, main `c58cbcf` 위) — 코드에 있던 콘텐츠 수치를 표로

- 조사: 콘텐츠 코드의 `const`·리터럴·하드코딩 문자열을 훑음(`Tools/audit_game_data.py`는 상수 key·리소스·수확전투만 봄). 남아 있던 것은 11-06~11-09에 들어온 콘텐츠 값뿐이었다.
- **새 시트 `출석`**(17001~17007 → 전수 조사 2차에서 **19101~19107**, `AttendanceRow`): day·kind(`AttendanceRewardKind`)·amount·equipId. 출석 일수 = 행 수(`Attendance.Days`가 const → 표). 7일째 무기는 이름("날다람쥐") 대신 **장비.index 11010**. 검증기: day 1부터 연속, 무기 equipId는 장비 시트 무기, 재화 양수.
- **상수 9099~9103** (main이 9090~9098을 먼저 써서 병합 때 옮김 → 전수 조사 2차에서 **19001~19005**): `adBoxGoldChance` 0.5·`adBoxMaxSameInRow` 2(`AdBox.NextReward`), `bossRushEasyLevels` 5(`DungeonRewards.Stage`), `unlockTeamStage` 3·`unlockWorldTreeStage` 7(`FeatureUnlock`, 이제 const가 아니라 속성).
- 방치 보스 체력 꼬리의 챕터 길이(`IdleProgress.ChapterLength` 10) → `방치전투.stagesPerChapter`(10).
- **리소스 7425** `UI/Icon/advertisement_mycelite`(광고 상자 균사석 그림, 미등록이었음). 감사 도구 문제 1 → 0.
- 값은 모두 옮기기 전과 같다(에디터에서 출석 7일·무기 날다람쥐·해금 3/7/5·보스 러시 레벨 1~5 기준 스테이지 30 → 6부터 +5·보스 체력 710 벽 ×1.4 확인). 표 편집 스크립트 `밸런스설계/tools/add_attendance_consts.py`(엑셀 XML 직접 수정), Reimport Tables 1297행 OK.
- **표로 옮기지 않은 것**(게임 데이터가 아니라 동작·안전값): 연출 시간·크기, 저장 주기, 광고 재시도, 절전 3분(`PowerSaveMode.IdleSeconds`), 표를 못 읽을 때 기본값(`AdBox` 간격, `FeatureUnlock` 던전 5), 방어력 상한 0.9 같은 계산 보호값, 세이브 옮기기용 옛 값(`Wallet.TestMycelite`·`Equipment.OldGradeCount`), 코드 구조 상수(`Formation.SlotCount` — 검증기가 표와 같은지 봄, `GachaProgress.GradeCount`).

### 2026-11-09 (`LKH`·`KHD` → `main` 머지 `0110323`·`667987a`·`1af7e60`·`16cbebc`)

| 커밋 | 작성 | 내용 |
|---|---|---|
| `3ba8a59` | LKH | **던전(벽돌깨기) 화면을 Main과 맞춤** — 배경 `BGimage.png`(세로 비율 맞춤), 상단 Main `TopBar` 프리팹·스테이지/타이머는 MainHud 형식, 수확기 = 장착 무기 그림, 버섯 = Main과 같은 Enumy 군락 그림 + 둘레 같은 종 Unit 그림(`BattleLooks.cs`), 단단한 버섯 회색 틴트·수확기 버프 색 제거(그림이 어두워지던 문제) |
| `dee1a7f` | LKH | 던전 수확 완료 팝업: 골드·균사석 아이콘+숫자 두 줄(`MyceliteRow`), 버튼 글자 여백·최소 24 ("다음 레벨 (레벨 n)" 넘침) |
| `5ec7d0f` | LKH | 던전 군락 버섯이 단일 버섯에 가려지던 문제 — 버섯 레이어 아래 `Singles` → `Clusters` 하위 레이어로 풀 나눔 |
| `9374068` | LKH | 설정 팝업 "광고 개인정보 설정" 글자 배치(던전 설정 창 +80, Main 설정 창 +90) |
| `a6c6a0c`·`ffd3c15` | KHD | 폰트 다시 굽기 / 굽기 최적화(아틀라스 용량 약 33%↓, 글자 모양 동일) |
| `3b3d0b7` | KHD | 화면 비율 대응 (`SafeAreaDock`) |
| `54bcced` | KHD | 골드 던전(2) 보상 `rewardKills` 20000 → 10000 (광고 상자 골드의 2.5배) |
| `4fbf3b0` | KHD | **영어 번역**(`String.xlsx`·`Localization/Loc`·`LocalizedText`·`StringTable`·`LanguageButton`, 기기 언어 자동 선택, 프리팹 다수에 `LocalizedText`), 클로버 위치, **광고 상자 개편**(두 자리 배회·골드/균사석 한 가지 보상). 90파일 |
| `6c206b6` | KHD | **해금 팝업**(`Unlock/FeatureUnlock`·`BottomBarLocks`·`UnlockAnnouncer`·`TutorialSpotlight`) 및 예외처리, `BarBackdrop`, 출석 창 수정 |
| `25fdcb9` | KHD | 뽑기 결과 창 닫기 위치 (`GachaResultPopup`) |
| `2a205ca` | LKH | **의뢰 일괄 수령**(세계수 `TreeQuestPanel`·`WorldTree.Quests`, 메인 `QuestScreen` 하단 주간/일일/일괄 수령), **확인 창 게임 UI 톤**(`ConfirmPopup`: 나무 테두리·노란 이름표·종이 안쪽·빨강/베이지 버튼 — 게임 종료·데이터 초기화·던전 나가기), 편성 중 꼬마 = 카드 전체 반투명 판 + "편성 중", Main 씬 Badge 오버라이드 제거, 문자열 `common.claim_all`·`treequest.claim_all_toast`·`formation.in_team` |

**(11-10 KHD가 main에 머지 `c58cbcf` — 기록용)** KHD에만 있던 11-09 오후 커밋: `50f354c` **코드 UI 캔버스화**(64파일 — 코드로 만들던 뽑기 카드·결과 창·세계수 패널·절전 모드·로딩 등을 프리팹으로, `TreeGrowPanel`·`TreeKidPanel`·`DailyQuestPopup`·`BarBackdrop` 삭제, `GachaResultCell` 추가), `a1590aa` 출석 팝업 수정. `git merge-tree`로 main과 충돌 없음 확인(11-10). 머지 뒤 Unity 컴파일·플레이 확인 필요.

### 2026-11-06 (`KHD`·`LKH`·`PJS` → `main`) — 던전 2~5 · UI 개편 머지 · 출시 안정화

| 커밋 | 작성 | 내용 |
|---|---|---|
| `d9204a2` | KHD | 던전 작업 준비([작업준비_261106.md](작업준비_261106.md)) |
| `dc3c7a2`·`0adbbba` | KHD | **던전 추가 및 광고 수정** — `DungeonSession`(지금 던전 번호·방치 던전 시작/끝·입장권), `DungeonMode` Harvest/BossRush/BossLevel(던전 시트 `mode` 열), `IdleBattle.Dungeon.cs`(보스 러시·보스 토벌), `DungeonRewards`·`DungeonTickets` 던전별 레벨, `HarvestGame`/`HarvestTuning`이 `DungeonSession.Current`를 읽음, 상수 `bossRushCount`·`bossLv*` 9083~9089, 보스 러시 UI |
| `29ad5b3` | PJS | UI 개편 22~24차(홈 전투 배치·배경 `BGimage1440`·넓은 화면·세계수 성장 시트 `TreeGrowScreen`) — [UI_REWORK_LOG.md](UI_REWORK_LOG.md) |
| `b2c6eb1` | LKH | PJS 머지 뒤 LKH 규칙 누락분: 꾸미기 탭 숫자, PJS 그림 123개 리소스 시트 7302~7424 등록(1279행 OK), 폰트 다시 굽기 733자 |
| `a8a2dac` | KHD | 강화 노트북이 넓은 화면에서 Stage 밖으로 늘어나던 문제 |
| `bae4ea3` | KHD | **개발자 모드 출시 빌드 컴파일 제외** |
| `41ebff9` | LKH | 버튼 누름 스캐너(`ButtonPressShade.Scanner`)가 플레이 종료 뒤 에디터에 남아 missing script 오류가 쌓이던 문제 |
| `bd85376` | KHD | 출시 안정화·최적화 1차: 광고 콜백 메인 스레드·**실제 광고 ID**·광고 후 정산 제외, 저장 주기 15초·종료 저장, 그림 캐시, link.xml (8장) |
| `33bfa1b`·`bcb20cc` | KHD·LKH | **출시 빌드 세팅** — `AndroidBundleVersionCode` 2, 키스토어 `C:/Users/user/Desktop/keystore/user.keystore`(10-30 기록의 `.../key/`와 경로 다름), Main 씬. `Builds/Android/moremustmush-0.1.0-2.aab` 있음 |
| `6f41e79` | KHD | **출시 안정화 2차** — `GameClock`(시계 되돌리기 차단), 광고 동의 UMP·`AdPrivacyLink`, 방치 전투 오류 재시도, 세계수 세이브 `LocalBackup`, `AdCredits`, 전수 점검 반영 (8장) |
| `f99781a` | KHD | 입장권은 보상 때 사용·레이드 나가기 확인·세계수 선물 상한 (아래 절) |
| `0ab0f83`·`ba7ce65` | KHD | 던전 필드 하위 캔버스·할당 줄이기 / 안 쓰는 패키지 4개 제거·기본 렌더 파이프라인 `Mobile_RPAsset` (8장) |
| `bded102` | KHD | 폰트 `LINESeedKR-Bd SDF Wide` 정리 |
| `5816498`·`c9859d3` | KHD | 7일 출석 (아래 절) |

### 2026-11-06 (`KHD` 브랜치) — 출석 체크 이벤트 (7일)

- **규칙** (`Quest/Attendance.cs`): 하루 = 한국 시간 00:00:00 (`DungeonTickets.Today`, 게임 시계라 시계를 되돌려도 다시 못 받음). 출석한 날 수로 1→7일째를 차례로 받는다(빠진 날 건너뛰지 않음). 저장 `Attendance.Count`(받은 날 수)·`Attendance.Day`(마지막으로 받은 날). 받으면 바로 `SaveScheduler.Flush`.
- **보상표** (코드 안 `Rewards`, 표 시트 아님 — 엑셀 편집 위험을 피함. 칸 수 = `Days`): 균사석 1~3일 500 · 4일 1,000 · 5일 1,500 · 6일 2,000 (합계 6,000), 7일 = 전설 무기 `날다람쥐`(장비 시트 name으로 찾고 없으면 최고 등급 무기, 이미 있으면 조각 +1). 골드 보상 종류도 코드에 있음(받는 순간 스테이지 적 처치 골드 × kills) — 지금 표에는 없음. (사용자 결정 11-06: 14일 → 7일로 줄임)
- **자동 출석** (`Quest/AttendanceEntry.cs`): Main에 들어오고 1초 뒤부터 1초마다 날짜 확인 → 받을 수 있으면 보상을 주고 출석 창. 접속 중 00시가 지나면 그 순간 뜬다. 다른 팝업(PopupGate)·씬 로딩·광고 중이면 닫힐 때까지 기다리고, 보상은 창이 뜰 때 준다. 다른 씬(던전·세계수)에 있으면 Main으로 돌아올 때.
- **버튼** (프리팹 `MainHud` › `SideButtons` › `AttendanceButton`, `AttendanceEntry`): 퀘스트(0)와 도감 사이(−165), 도감은 −330으로 내림. 아이콘 `UI/Icon/daily_reward`, 글자 "출석", `Dot` = 오늘 받을 게 남음. 다 받았으면 실행 때 버튼을 숨기고 도감을 그 자리로 올림. `screen`은 Main 씬에서 연결(프리팹 인스턴스 덮어쓰기).
- **창** (프리팹 `Prefabs/Mobile/UI/Main/AttendanceScreen`, Main 씬 `Stage` 아래 QuestScreen 다음 — 꺼 둠, `AttendanceScreenView`): QuestScreen을 복사한 나무 창(제목 메모 "출석 체크"·하단 띠 X)에 메시지 · `Days/Day1~Day7`(`AttendanceCellView`: 바탕·N일째·그림·체크·개수·NEXT, 5칸 + 가운데 2칸) · 남은 시간. 받은 날 체크, 오늘 받은 칸 초록 + 튀어 오름, 다음 날 NEXT, 마지막 날 노란 칸. 칸 그림·글자는 코드가 넣고, 자리·크기는 프리팹에서 고친다. (사용자 지시 11-06: 코드로 만들지 말고 캔버스에 — 처음의 코드 생성 창 `AttendancePopup` 삭제)
- **개발자 모드**: "던전 · 광고 · 속도" 안 "오늘 출석 초기화 (다시 받기)".
- **확인 (에디터 플레이, 프리팹판)**: 버튼 누르면 창 열림, 날짜 초기화 → 2일째 +500 자동 지급·창·빨간 점 꺼짐, 하단 X로 닫힘. (코드판 때) 방치 정산 창이 떠 있으면 기다렸다가 닫힌 뒤 지급, 1일째 균사석 +500·창 표시, 같은 날 다시 확인하면 아무 일 없음, 5일 받은 상태에서 날짜 초기화 → 6일째 +2,000, 7일째 날다람쥐 Lv.0→1 · 완료 문구, 다 받은 뒤 다음 날에도 더 안 줌. 콘솔 오류 0. 테스트 뒤 세이브 값 되돌림. **실기기 화면은 아직.**

### 2026-11-06 (`KHD` 브랜치) — 던전 입장권·레이드 나가기·세계수 선물

- **입장권은 보상 받을 때 사용**: 입장 때는 남은 횟수만 확인하고 `DungeonSession.MarkEntry(던전, 광고 여부)`로 표시, 보상을 주는 순간 `PayEntry`가 한 번만 쓴다. 던전 1~3 = `HarvestGame.Finish`, 던전 4 = 첫 보스 보상(`GrantRushBoss`), 던전 5 = `BankLevelReward`(모은 보상이 있을 때, 앱을 내릴 때 포함). 보상 없이 로비로 나가거나 첫 보스 전에 지면 횟수가 남는다. 광고 입장은 광고를 입장 때 보고 광고 횟수를 보상 때 쓴다.
- **레이드(던전 5) 도중 나가기**: 뒤로가기(`MainBackHandler`)·하단 씬 버튼(`SceneButton.Go`)에서 `DungeonSession.GuardLeave`가 "현재 보상을 받고 나가시겠습니까?"를 띄운다. 예 → 모은 보상을 받고 끝낸다 (뒤로가기는 결과 창, 씬 이동은 바로 이동). 확인 창은 `MainBackHandler`가 `DungeonSession.Confirm`으로, 끝내기는 `IdleBattle`이 `DungeonSession.FinishIdle`로 등록한다.
- **세계수 선물** (11-12 호감도와 함께 삭제): `GiftPick` 개수를 다음 호감도 레벨까지 필요한 만큼으로 줄인다 (`CapToNextLevel`, `GiftValue`와 같은 계산, 최소 1개).

### 2026-11-05 (`LKH` 브랜치)

| 커밋 | 내용 |
|---|---|
| `d29c186` | **펫 삭제 밸런스 보정** — 상수 9081 `weaponEffectMul` 1.75(`Equipment.BaseOwnPercent` 무기만), 스테이지 421~700 체력(`밸런스설계/tools/apply_pet_fix.py`), verify#20 조건부 통과 |
| `43cd817` | 시뮬레이터 빌드 결과물(`밸런스설계/test/sim/bin`·`obj`) git 추적 제외 |
| `21f5ded` | 임시 버섯 리소스 제거 · 11-04 새 리소스로 대체 |
| `c1e73d8` | 미사용 리소스·코드 정리(`CollectPopup` 프리팹·`bg_main`·`gacha_pet`·Resources `BG_Main` 삭제, 호출 0 코드 삭제 — `ConfirmPopup.ShowNotice`·`StageProgress.ChooseFarm` 등), 같은 용도 그림 임포트 설정 통일(UI Max 512·Full Rect·HQ, 장비·수확기 아이콘 256, 벽 1024) |
| `344f7fb` | **던전 소탕 삭제** — 레벨 상한 없이 매 판 직접 플레이, 못 깨면 판 골드만(상수 `dungeonSweepMul` 삭제, `DungeonRewards` 단순화) |
| `9f09db1`·`08b2ebb`·`dcfd389` | **메인 BGM** Glowing Woodland(`Resources/Audio/Bgm/GlowingWoodland.wav`, Vorbis 0.7 Streaming, `BgmPlayer`: Start 다음 씬부터 반복·씬 전환에도 유지) / 모든 옵션 창(`OptionPopup` = Start·Main·InGame) 배경음 슬라이더 → `BgmVolumeSlider`(`Option.BgmVolume`) / 광고 중 `AudioListener.pause`(`AdService`) |
| `51fcc6b` | 꼬마 2단계 그림(`icon_GGoma_2`) 8칸 좌우반전 — 공격 방향(오른쪽) |
| `970fa52` | **폰트 정적 아틀라스** — `LINESeedKR Bd/Rg SDF`를 Static으로 굽기(메뉴 `MoreMush/Fonts/Bake Static Atlases`, `FontBaker`: 표·코드·씬/프리팹 글자 수집, 60pt·여백 6·1024). `Reimport Tables`가 폰트에 없는 표 글자를 경고. 원인: 동적 아틀라스에서 플레이 중 전투 HUD 글자가 사라짐(재현 못 함) |
| `2657c9b`·`0061dab` | 세계수 장식 24종 새 그림(`Props/dew_chime` 등 이름 그대로, `PropsLegacy` 삭제) / 꾸미기 탭 숫자 = 그 자리 장식 중 만든 수 / 전체 수 |
| `3b32267`·`fd75a82` | **개발자 모드**(`` ` `` 키, 에디터·Development Build만) + [HANDOFF_DEVMODE.md](HANDOFF_DEVMODE.md) |
| `f9a84f7` | **세계수 그림** `Tree01`(1단계)·`Tree02`(2~4)·`Tree03`(5~7)·`Tree04`(8), `WorldTreeView.stageSizes` 210→760px로 단계마다 커짐(땅 고정). 옛 `tree_0~7`·`world_tree_0` 삭제 |
| `66d6c2a` | 로딩 화면(Start) 배경 `bg_title.png` 교체(830×1896) |
| `a2f0768`·`6c040da`·`9c18676` | 소환(뽑기) 탭 상단 배너·수확기/방어구/꼬마 배너 교체(배너 소환진의 그려진 아이콘은 지움 — 게임 아이콘이 얹힘, 아이콘 위치는 `84772db`에서 소환진에 맞춤) / 던전 1~5 카드 / 던전 탭 상단 배너(`DungeonUI` 배너 비율 2.86→2.35) |
| `43c925c` | **표 갱신·정리** — 리소스 전수 등록 보충(`BGimage`·임시도감 Unit 15·`bgm_main` AudioClip — 검증기가 AudioClip 허용), 안 읽는 열 삭제(적·보스 `hpCoefficient`, 스테이지 `stageCoefficient`, 방치전투 `chapterName`, 세계수 `#bag` 2열), 광고 상자 균사석 상수 `adBoxMycelite`=90(`adBoxMyceliteBosses` 삭제), 보스 그림을 표에서 읽기 시작 |
| `1f43d8d`·`68927e2`·`6aef012` | 방치 전투 꼬마 130→169px, 일반 버섯 110→121px(1.1배) / 꼬마 5칸 지그재그 간격 가로 190·세로 85~170, 꼬마 영역 75px 아래, 적 영역 460×287 → 500×380 |
| `772656a` | 폰트 머티리얼 ScaleRatio(TMP 재계산) |
| `4ef560d` | **보스 10종 교체** — 보스 시트 2001~2010 + `cell` 열(적 군락 시트의 조각): 버섯 문어·새싹 정령·상자 괴물·부리 버섯·결정 피라미드·버섯 슬라임·꽃 요정·나비 토끼·심장 버섯·버섯 애벌레. 할아버지(`Grandpa` 그림·리소스 7012)·옛 부리 버섯 삭제. 스테이지 `bossId` = 10스테이지마다 10종 무작위 순서(시드 20261105). 보스 조각은 일반 적 목록·창고 아이콘에서 뺌. `FontBaker` 버그 수정(정적 폰트 원본 글꼴 재연결, 남은 옛 아틀라스 텍스처 제거) |
| `c6d8675` | 표 `info` 마무리(보스 예시값, 개편 기록 2행 "2026-11-05 표 정리·보스 10종"), 폰트 다시 굽기(730자), 인계 문서 갱신 |
| `84772db` | 뽑기 탭 수확기·방어구·꼬마 카드의 **대표 아이콘 칸(소환진 가운데)을 왼쪽으로** — 새 배너 소환진 위치에 맞춤 (Main 씬의 `GachaUI` 오버라이드, 가로 위치 320 → 260·260·240) |

### 2026-11-04 (`LKH` 브랜치)

| 커밋 | 내용 |
|---|---|
| `77ad593` | **수확기(무기) 10종 개편 · 고급 등급 삭제** — 등급 0 일반·1 희귀·2 영웅·3 전설(ownBase 2/50/250/1250), 뽑기 `oddsUncommon` 삭제, 무기 일반 4·희귀 3·영웅 2·전설 1(그림 `Art/Dream/HarvesterSet`, 리소스 7254~7263), 방어구·펫 고급 3종씩 삭제, `Equipment`가 배치를 장비 시트에서 읽음 + 예전 세이브 옮김(`RemapLayout`) |
| `f303741` | **KHD `c59443c` 머지** — 꼬마 등급 열 삭제·진화 그림·꼬마스킬 시트, 적 독성·약용 버섯, 버섯 리소스. `MainTable.xlsx`는 KHD 표 위에 LKH 변경(등급·뽑기·장비 시트, 리소스 7254~7263, 상수 9004~9006 삭제)을 다시 얹어 병합, 검증기 `equipSubWeight` 검사 삭제·`enemyToxicRate` 검사 유지 |
| `b6b2d6a` | **안드로이드 몰입 모드** `ImmersiveMode.cs` — 시작·포커스 복귀마다 상태 바·하단 내비게이션 바 숨김(API 30+ `WindowInsetsController`, 아래는 IMMERSIVE_STICKY). 씬에 둘 필요 없음(`RuntimeInitializeOnLoadMethod`). **실기기 확인 전** |
| `23c9c83` | 방어구 아이콘 첫 5장 (`Art/Dream/Icons/Armor`) |
| `7b17e68` | **방어구 10종 개편** — 12종 → 10종(일반 풀잎 망토·나뭇잎 조끼·이끼 외투·나무 갑옷 / 희귀 수정·달빛·화산 갑옷 / 영웅 황금·별빛 갑옷 / 전설 세계수 갑옷), 그림 10장을 HarvesterSet 규격(256×256, 244px 맞춤, 임포트 설정 동일)으로, 리소스 7264~7273, `Equipment.LayoutVersion` 3(12종 방어구 세이브 옮김) |
| `08bb82b` | **펫 삭제** — 장비 펫·펫 뽑기·편성 펫 칸 삭제, 편성 칸 위 3 · 아래 2 가운데 (9.9 "2026-11-04 미완료" A) |
| `9caca14` | 펫 삭제 밸런스 실측·보정안·인계 문서(검증 중단 상태, 보정 적용은 11-05 `d29c186`) |

### 2026-11-03 (`LKH` 브랜치)

| 커밋 | 내용 |
|---|---|
| `c53ecf2` | **KHD `66ed1e6` 머지** (방치 밸런스 v3·던전 레벨 보상·소탕). 엑셀은 공통 조상·LKH·KHD 세 판을 시트·열·행 단위로 병합(6.0 "KHD v3 머지"), `GameBalance.cs` 던전 행 사전+목록, 검증기 던전 열·상수 합침 |
| `c4d1058` | **세계수 밸런스 v4** — KHD v3 곡선 위 재검증(라운드 4 조건부 → 5단계 `branchSlots` 5→6 → 라운드 5 통과), 이론·검증 스크립트가 라이브 표를 직접 읽음, 창고에서 보스 처치 제외 |
| `8f2250a` | 에디터 메뉴 **`MoreMush/게임 기록 전체 초기화`** (`Editor/SaveDataMenu.cs`, 지우는 코드는 `DataResetButton.ClearSaves()` 공유, 세계수 캐시도 버림) |
| `51cbd87` | **세계수 일일·주간 의뢰** (`WorldTree.Quests`·`TreeQuestPanel`, `의뢰` 시트·상수 `treeQuestDaily`, 보스 도전으로 세기 K3, 콤보 의뢰 → 던전 1 판 수, v5 라운드 6 통과) |
| `648360a` | **전수 조사·정리** — 안 쓰는 스크립트 4개·메서드 5개·세계수 임시 그림 3장 삭제, `밸런스설계/**/*.log` 커밋 (8장 "정리") |
| `3e2263f` | 던전 상단 HUD: 빈 `KidSlot` → 보유 골드·균사석 두 줄(`WalletLabel`), 스테이지 글자를 설정 버튼 왼쪽으로 |
| `dc68380` | **기획서 「세계수 단계별 장식 개편 Ver02」 칸 수** — 합계 1/2/3/4/6/8/10/12·구역당 3·해금 뿌리1/줄기2/가지4/수관6, 9307 evoExp 41만, 장식 9711 효과, 옛 세이브 넘친 배치 정리 (`TrimPlacedToSlots`), 세계수 v6 라운드 7 조건부 |
| `c2814a2` | 던전 임시 그림 8장 → Codex 수채화 그림(`Art/Generated/InGame`, GUID 유지), `Art/Placeholder` 삭제, 배속 버튼 배경 `btn_cream`, 상단 이번 판 골드 표시 제거, 튜토리얼 "버섯을 채집하세요!" |

### 2026-11-02 (`LKH` 브랜치, 세계수·표 연동)

| 커밋 | 내용 |
|---|---|
| `cf9ed0e`·`c00c251` | WorldTree 씬 복구, 하단바 세계수 → WorldTree, 성장 단계 그림 임시 4장 |
| `72712b3` | 방치 전투 HUD 글자가 사라지던 문제 (서브 캔버스를 글자보다 먼저) |
| `49d1d29` | KHD 머지 (AdMob SDK·광고 상자·광고 뽑기·일일 퀘스트·2차 최적화 `347e362`·NewInGame 씬(kms)) |
| `7b10110` | 던전·뽑기 탭에서도 골드·균사석 표시 (`UI/TabDock.cs`) |
| `0b74b4a` | `Backup/WorldTree` 삭제(복원 끝), `MMM_Mobile.slnx` 추적 해제 |
| `7cd69e6` | **세계수 기능 구현** — `WorldTree/WorldTree*.cs`(모델)·`WorldTree/UI/Tree*.cs`(시트 내용), 세계수 시트 6개 + 꼬마·적·상수 열, PC 그림 복사(`Resources/Art/Dream/Farm/Tree`·`Props`), 방치 전투 연결(`IdleProgress`·`IdleBattle`), `SaveScheduler.BeforeFlush`. `WorldTreeGrowth.cs` 삭제 |
| `fadb928` | KHD 머지 (`3d4631c` 밸런스 중간저장. 검증기 충돌은 양쪽 합침, 엑셀은 KHD 쪽 + 세계수 시트 다시 생성) |
| `a3e1463` | **세계수 밸런스 v3** (이론·검증 에이전트 3라운드), `밸런스설계/세계수/`, `.claude/agents/worldtree-*.md`, 코드 K1(★진화 필요 레벨 등급 배율)·K2(보스 균사석 배율 소수 확률)·보따리 long 넘침 방지 |
| `46a23d9` | (Codex) 표에 구현 데이터 정의 추가 — `장비`·`뽑기상품`·`방치전투`·`수확전투`·`수확등급`, 던전 `name`·`scene`·`implemented`, 리소스 7225~7227. 검증해서 코드·씬과 차이 0 |
| (11-03) | **KHD `66ed1e6` 머지** — 방치 밸런스 v3·던전 레벨 보상·소탕. 엑셀은 세 방향 병합(6.0 "KHD v3 머지"), `GameBalance.cs`·검증기 던전 열은 양쪽 합침 |
| `43c2308` | **표 ↔ 게임 연동** (6.0) — `Equipment`·`GachaPanel`·`GachaProgress`(광고 하루 횟수)·`IdleBattle`·`InGame/HarvestTuning.cs`(신규)·`HarvestGame`·`HarvestField`·`FieldInput`·`DungeonEntry`. 상수 `adGachaPulls` 삭제 |

### 2026-10-29 ~ 10-30 (`InGame` 브랜치, 방치형 전환)

| 커밋 | 날짜 | 내용 |
|---|---|---|
| `e3f4710`·`35fbb31`·`8371051` | 10-29 | Main 방치형 전투·강화, 꼬마 편성·편성 해제 (LKH) |
| `bcc39f5`~`13ecfcc` | 10-29 | KHD: UI 리워크(SafeArea·하단바·패널 프리팹), 하단 탭(`TabGroup`·`MainNavigation`·`MainTabButton`), **장비 초안**(`Equipment`·`EquipmentPanel`·`CombatPower`·`IdleAway`·`IdleReportPopup`) |
| `90598e0` | 10-30 | KHD → InGame 머지 (폰트 아틀라스 충돌은 InGame 쪽 유지) |
| `3c45137` | 10-30 | Start → **Main** 연결, 전투 배경 임시 이미지 |
| `d762fb8` | 10-30 | Main 옵션 버튼 복구, **InGame 씬으로 가는 UI 연결 해제** |
| `b2b2f54` | 10-30 | 옵션 팝업이 전투 뒤로 숨는 문제(`UiPopup`이 열릴 때 자기 캔버스로 맨 위) |
| `dcd8d5a` | 10-30 | **씬 전환 로딩 팝업** `SceneLoader` |
| `5e91ce7` | 10-30 | 세계수 홈·던전 버튼이 팝업을 잠깐 띄우던 문제 |
| `34cb9c0` | 10-30 | 전투 레이어(꼬마·수확기·버섯)를 `SafeArea` 안 배경 바로 위로 (패널·팝업 위로 보이던 문제) |
| `f0d8b0c` | 10-30 | 테스트 균사석 지급 제거·회수, **Main 재화 바(`WalletBar`) 상시 표시** |
| `e7c6336` | 10-30 | 던전·뽑기 페이지를 전투 아래 영역(스크롤)으로, 탭이 열리면 강화 목록 숨김, **`PopupGate`(팝업 하나만)**, 스킬트리 하단바 연결, **`InGame.unity` 삭제·빌드 제외** |
| `9e89056` | 10-30 | 던전 목록 "던전 1~5" + "해금되지 않음" 덮개 |
| `7538439`~`260fb33` | 10-30 | 편성 화면: 장착 장비 아이콘(`EquippedGearView`), 임시 흰 박스 → 게임 UI 리소스, '동료'→'꼬마', 꼬마 이름 대조 띠, 장비 이름 겹침 |
| `a009228`·`50a12da` | 10-30 | 꼬마 고르기 팝업 정리(위 빈 UI 삭제, 아래 버튼 줄 숨김, 하단바 위로), **하단바 나무 판 배경** |
| `c3b9dc7`·`50137ed` | 10-30 | **Main 배경 한 장(`bg_main.png`, Codex 생성)**, 편성 프리팹 TMP 저장값 정리 |
| `5a152c4` | 10-30 | 꼬마 배치(앞 3 · 뒤 2 지그재그), HANDOFF 갱신 |
| `c6b2c82` | 10-30 | **방치형 통합 표 `MainTable.xlsx`** + 검사기(`GameBalanceValidator`), 기존 표 5개는 `Tables/Legacy/` |
| `d1d8b1e`·`c74f65d` | 10-30 | 간판(`header_plaque`) 나뭇잎 제거·수정 |
| `3183125` | 10-30 | **KHD:** 성장 곡선 재설계(골드·강화 비용 지수 항, 치명타 0.1%/Lv, 공속 상한 제거), 강화 2종(상급 공격력·일반 적 피해량)과 단계 제한, **던전 1 → InGame 연결·InGame 씬 복원**, InGame 결과 팝업 간소화·첫 입장 튜토리얼, 뽑기 결과 연출, **절전 모드**, `PopupLayer`, `TabGroup` 탭별 `hide`, 편성·던전·뽑기 패널을 화면 위까지, 설정 창 정리, 세계수 뒤로가기 → Main, **SkillTree 씬·스크립트 제거**(`Backup/SkillTree/`), 골드 획득량 강화 제거, `밸런스설계/` 폴더 |
| `a61e7f0` | 10-30 | KHD → InGame 머지 (충돌 없음) |
| `bdc30df` | 10-30 | HANDOFF 정리 (KHD 머지 반영) |
| `4a783a7` | 10-30 | **KHD:** 방치 전투 최적화 — `Wallet` 잔액·`IdleProgress` 강화 레벨을 메모리에 둠(`Wallet.Reload`·`IdleProgress.ReloadLevels`), 적 공격력은 스테이지 바뀔 때만 계산, 보스 HUD 초 글자는 초가 바뀔 때만 |
| `b647911` | 10-30 | **KHD:** **꼬마 강화(합성)** — 레벨·조각(장비와 같은 방식), 레벨마다 공격 배율 +5%, 꼬마 고르기 아래 정보 칸 `CharacterDetail`(편성·합성·일괄 합성), 예전 보유 장수 세이브 옮김 |
| `034c20f` | 10-30 | **KHD:** 뽑기 카드 오른쪽 열 개편 `GachaCardView` — 뽑기 레벨·경험치 막대, 광고 소환(초록) · 11회/35회(보라, 재화 아이콘·가격) |
| `a902a76` | 10-30 | **KHD:** 타격 이펙트 `HitEffects` (MoreMustMush `FX/hit_0~4`, `Resources/Art/Dream/FX`) |
| `dc7de93` | 10-30 | **KHD:** 편성 3×2(꼬마 5 + **펫 칸**), 하단 꼬마/펫 탭(`Down`) 삭제, 꼬마 고르기 X 닫기 버튼 |
| `cd69bd5` | 10-30 | KHD → InGame 머지 (충돌 없음) |
| `cfbfc2c`·`d3b5fa2` | 10-30 | **던전·뽑기 탭 그림**(Codex 생성 배너·카드), 배너를 화면 위에 고정하고 목록 판을 그 위로 |
| `4baa8d1`·`854549e` | 10-30 | 회사명·게임명 `soomyouth`/`moremustmush` 저장(브랜치 전환 때 되돌아가던 것), 앱 아이콘 v2 |
| `c2feb81` | 10-30 | **던전 입장 횟수**: 하루 2회, 한국 00시 충전(`DungeonTickets`·`DungeonEntry`·`DungeonResetTimer`), 결과 "한 번 더하기"도 1회 사용 |
| `5e58cd0`·`fe15987` | 10-30 | 설정 팝업 게임 종료·데이터 초기화 버튼 제거, 꼬마 고르기 `Outside` 크기 정리 |
| `278ebce` | 10-30 | **해상도 대응**: `StageFrame`(디자인 영역)·`FitWidth`, 모든 씬 UI를 Stage 아래로, 고정 폭 UI 앵커 재정비 (3.2) |
| `339b0e4` | 10-30 | 광고 11회 뽑기 → **하루 1회 무료 뽑기**(한국 00시) |
| `b7e2863` | 10-30 | **대상 API 36 고정·세로 고정**, 안 쓰는 리소스 삭제(폰트 4종이 WorldTree의 낡은 머티리얼 오버라이드 때문에 빌드에 끌려 들어가던 것 등), 텍스처 크기·압축 정리 |
| `72c6a1d` | 10-30 | 설정 도움말 버튼 제거, **세계수 탭 = "개발중" 알림**(`ComingSoonButton`), **WorldTree 씬 제거**(`Backup/WorldTree/`), 인게임 "로비로 돌아가기" → Main |
| `dabec53` | 10-30 | **Android 뒤로가기**(`BackButton`·`MainBackHandler`) |
| `a0ab49c` | 10-30 | HANDOFF 정리 |
| `34bd391` | 10-30 | `.gitignore`에 `*.keystore`·`*.jks` (서명 키 커밋 금지). 키 설정 자체는 커밋하지 않음 |

### 2026-10-26 ~ 10-28 (`LKH` 브랜치, 판 플레이 시절)

그 전 커밋은 `git log`로 보세요.

| 커밋 | 날짜 | 내용 |
|---|---|---|
| `6f65608` | 10-27 | 콤보 규칙(수확 +1 / 바닥 0, 점수용 상한 20·계수 0.05), 보스 판 황금 제외, 일반 스테이지 목표(→ 10-28 삭제), 보스 패배 패널티 제거·재도전/파밍, 첫 클리어 균사석, 보스 체력 재맞춤, `AGENT_WORKLOG.md` |
| `e3ee606` | 10-28 | **일반 스테이지 목표 삭제**(판을 마치면 늘 다음), 결과 팝업 "다음 스테이지 / 스킬트리로", HUD "스테이지 n" |
| `6a62d2d` | 10-28 | 금액 단위 '억' Jua에 추가 |
| `2c5aad9` | 10-28 | **50스테이지 뒤 무한 스테이지** (10개마다 보스 재사용) |
| `b6a3536` | 10-28 | 51스테이지부터 이름이 비던 문제 |
| `3ed657f` | 10-28 | **성능 벤치마크** + 목표 60fps |
| `cf30e51` | 10-28 | Firebase Test Lab Game Loop + 안드로이드 패키지 이름 |
| `ec2fb57` | 10-28 | Game Loop intent-filter 위치 (Firebase 인식) |
| `25f525f` | 10-28 | 벤치마크 시나리오 배속·특정 스킬 최대 옵션 |
| `22c4f33` | 10-28 | 벤치마크 배속 시나리오의 HUD 배속 아이콘 |
| `ceb7ede` | 10-28 | **보스 HUD** (이름 + 본체 체력 바), 보스 머리 위 작은 바 제거 |
| `d005fbd` | 10-28 | 버프 글자 표시 제거 |
| `7159ba7` | 10-28 | **스킬 트리 가지별 페이지** + 가로 3칸 자동 배치(`SkillTreeLayout`) |
| `51bbacc` | 10-28 | 스킬 트리 연결 선 좌우 균형 |
| `c926b09` | 10-28 | **해금 조건 = 이전 노드(link) 1레벨 이상만** (requires 제거) |
| `ea9c2b2` | 10-28 | Skill 표에 `tier` 칸 |

**작업 트리에 늘 남는 수정:** `Assets/Settings/DefaultVolumeProfile.asset`·`Mobile_RPAsset.asset`·`UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ProjectSettings.asset`은 Unity가 열 때 스스로 다시 쓰는 파일이라 계속 "수정됨"으로 뜹니다. 일부러 바꾼 게 아니면 커밋에 넣지 마세요.
`LiberationSans SDF - Fallback.asset`은 TextMesh Pro가 실행 중에 스스로 고치는 파일이라 계속 "수정됨"으로 뜹니다. 동작과는 무관합니다.
`Assets/_Recovery/`는 Unity를 강제 종료할 때 생긴 씬 백업입니다. 커밋하지 않았고 지워도 됩니다.

---

## 3. 씬 흐름

빌드 씬: `Start` · `Main` · `WorldTree` · `InGame` (`SkillTree`는 10-30 삭제, `Backup/SkillTree/`에 백업. `WorldTree`는 11-02 복원. `InGame`은 던전 1 "입장하기"로 들어가고 결과 팝업에서 `Main`으로 돌아온다. `NewInGame`(kms, 11-02)은 빌드에 없음)

`Start`(퍼센트 로딩, `StartLoading.nextScene = Main`) → **`Main`(방치형, 상주)**
`Main` 던전 탭 → 던전 1 "입장하기"(`DungeonEntry`, 입장 횟수 1 사용) → **`InGame`**(판 하나) → 결과 팝업 "한 번 더하기"(입장 횟수 1 사용, 없으면 막힘) / "나가기"(→ `Main`). 인게임 설정 "로비로 돌아가기" → `Main`

- 하단바(`BottomBar` 프리팹): 편성 · 세계수 · 홈 · 던전 · 뽑기. `TabGroup`(SafeArea)이 편성(`Equit`)·던전(`DungeonUI`)·뽑기(`GachaUI`) 페이지를 켜고 끕니다. 홈 = 아무 페이지도 없음. **세계수 = `WorldTree` 씬으로 이동**(11-02, 3.3). 던전·뽑기 탭이 열리면 `TabDock`이 재화 바를 배너 제목 줄 오른쪽(-534, -24)으로 옮긴다.
- 씬을 바꾸는 곳은 `SceneLoader.Load(이름)`을 씁니다(가운데 나이테 아이콘이 도는 로딩 팝업, 최소 0.3초, 터치 막음). 예외: Start → Main(`StartLoading` 퍼센트 화면), **벤치마크는 아직 `SceneManager.LoadScene` 직접 호출**. 인게임 설정 "로비로 돌아가기"·결과 팝업 "한 번 더하기/나가기"(`ResultPopup`, 10-30 최적화 작업)는 `SceneLoader` — 두 번 눌러도 한 번만 넘어가고 입장 횟수도 넘어갈 때만 씀.
- **뒤로가기(Android 버튼·제스처, 에디터 Esc, `BackButton`):** 열린 팝업이 있으면 닫기(`PopupGate.HandleBack`, 닫기 수단이 없는 InGame 결과 팝업은 안 닫힘) → 없으면 씬 처리: Main = 탭이 열려 있으면 홈, 홈이면 "게임을 종료할까요?"(`MainBackHandler`) / InGame = 설정 팝업(`StageHud`). 로딩 중엔 무시.

### 3.1 Main (방치형)

**Stage(`SafeArea/Stage`) 자식 순서 = 그리는 순서(뒤 → 앞):** `BG`(배경 한 장) · `GgomaArea` · `EnemyArea` · `IdleBattle`(전투 HUD·수확기·숫자) · `OptionButton` · `TopBar`(꺼짐) · `BottomBar` · `Equit`(편성) · `UpgradePanel`(강화 목록) · `DungeonUI` · `GachaUI` · `WalletBar`(골드·균사석) · `OptionPopup`

- 전투 레이어는 **`Stage` 안, 배경 바로 위**여야 합니다. 캔버스 직속으로 두면 모든 패널·팝업 위로 올라옵니다(10-30 수정).
- **배경:** `BG` 한 장(`Resources/Art/Dream/Backgrounds/BGimage.png`, 830×1896, KHD `c59443c`)이 전투 들판부터 하단바 뒤까지 덮습니다(Stage 전체로 stretch). 예전 `bg_main.png`(Codex)는 11-05 삭제, 전투 배경 `InGameBG`도 삭제.
- **꼬마 배치:** `GgomaArea`(380×520, 11-05) 안 `Ggoma1~5` = 편성 칸 1~5. 앞줄(x +95) 1·2·3, 뒷줄(x −95) 4·5 지그재그(세로 85~170 간격), 칸은 가운데 앵커 100×100이고 그림 크기는 `IdleBattle.ggomaSize` 169.
- **전투(`IdleBattle`):** 크기 `ggomaSize` 169 · `enemySize` 121 · `bossSize` 560, 적 영역 `EnemyArea` 500×380 (11-05). **보스 그림 = 표**: 스테이지 `bossId` → 보스 시트 `resourceId`(적 군락 시트) + `cell`(조각 번호), 10스테이지마다 10종이 무작위 순서(11-05 `4ef560d`). 보스로 쓰는 조각은 일반 적·세계수 창고 아이콘에서 뺌(`GameBalance.IsBossSprite`). 웨이브 `IdleProgress.WavesPerStage` + 보스(제한 시간). 적 처치 골드는 1초마다 모아 `Wallet`에, 보스 처치 = 균사석 `BossMycelite` + 골드 `BossGold`(일반 20마리분) + 다음 스테이지. 꼬마 무리 체력이 0이면 같은 스테이지 1웨이브부터. 수치는 `BALANCE_IDLE.md`.
- **하단 탭:** 탭이 열리면 `UpgradePanel`은 숨김(`TabGroup.homeOnly`). **10-30 KHD 이후 편성(`Equit`, y 233~2340)·던전·뽑기(y 200~2340) 패널은 하단바 위부터 화면 맨 위까지 덮습니다**(전투는 탭이 열린 동안 가려짐). 던전·뽑기는 `ScrollRect`(Viewport/Content). 탭별로 열린 동안 끌 것은 `TabGroup.Tab.hide`(지금 Main 씬에선 비어 있음).
- **편성(`Equit`):** `GGoma`(꼬마 칸 5 = **위 3칸 · 아래 2칸 가운데**(11-04 펫 칸 삭제, 칸 위치는 `GGoma.prefab` `Middle/Button`~`Button (4)`) + 자동 편성 + 꼬마 판 위에 붙은 장착 장비 칸 `EquippedGearView`), `Char`(꼬마 고르기 — 목록 위쪽(`Outside`) 또는 오른쪽 위 X(`Out`, 코드 생성)로 닫힘), `Wepon`·`Amor`(장비 팝업, `EquipmentPanel`. `Pet` 팝업·`Pet.prefab`은 11-04 삭제). 장비 탭은 `TabGroup.pagesArePopups`라 다른 팝업과 한 번에 하나만. 하단 꼬마/펫 탭(`Down`)은 10-30 KHD가 삭제.
- **꼬마 강화(KHD 10-30):** 처음 얻으면 Lv.1, 또 얻으면 조각, 조각 (Lv+1)개로 +1레벨(최대 100, `Equipment.CopiesToNext`와 같음). 공격 배율 = 1레벨 배율 × (1 + 0.05 × (Lv−1)) — 5%는 임시값. 꼬마 고르기에서 꼬마를 누르면 **선택만** 되고 아래 정보 칸 `CharacterDetail`(그림·등급·Lv·조각 막대·배율→다음)의 **편성**으로 칸에 놓음, **합성**·**일괄 합성**으로 레벨업. 미보유도 정보는 보임(편성·합성 막음). 첫 칸 X는 바로 편성 해제.
- **던전:** 위에 배너(`Banner`, 화면 위에 고정 — 남은 시간 = 다음 한국 00시까지, `DungeonResetTimer`), 그 아래 목록 판(`ListPanel`) 안에서 카드 "던전 1~5"가 스크롤. 카드마다 `DungeonEntry`(이름 = 던전 시트 `name`, 남은 횟수, 잠김 = "스테이지 n-n 진입 시 해금"). **던전 1~5 모두 연결**(1장 던전 줄): 1~3 → `InGame` 씬, 4·5 → 방치 화면(`IdleBattle.Dungeon.cs`). 횟수는 `DungeonTickets`(던전마다 하루 `dungeonPerDay` 2 + 광고 `dungeonAdPerDay` 2, `Dungeon.<n>.Day/Used/AdDay/AdUsed`). **소탕 없음(11-05 삭제):** 어떤 레벨이든 직접 플레이, 레벨 보상은 깼을 때만.
- **뽑기:** 배너("소환" 현판, 11-05 배너 그림 교체 — 소환진 가운데에 게임 아이콘이 얹힘) + 목록 판 안 `GachaPanel` 3종(수확기·방어구·꼬마 — 11-04 펫 뽑기 칸 `Gacha_Panel1 (2)` 삭제, `GachaUI.prefab`; 카드 그림 = 요정의 고리, 대표 그림은 고리 가운데). 오른쪽 열(`GachaCardView`): 뽑기 레벨·경험치, 광고 뽑기 · 11회·35회(균사석). **버튼 셋의 횟수·가격·재화·광고 하루 한도는 뽑기상품 시트**(11-02, `GachaPanel.LoadProducts`: 광고 행 → 광고 버튼, 재화 행은 pullCount 순으로 11·35 버튼). 광고 하루 사용 수 = `Gacha.<종류>.AdCount`(한국 00시, `GachaProgress.FreeLeft`; 날짜만 있고 횟수가 없는 옛 세이브는 1회 쓴 것으로). 결과는 `GachaResultPopup`(칸이 하나씩 튀어나옴, 등급 색, NEW!!, 누르면 건너뛰기).
- **팝업:** `PopupGate` — 새 팝업이 열리면 열려 있던 것을 닫음(`UiPopup`·꼬마 고르기·장비 탭·방치 정산). 하단 탭을 바꾸면 모두 닫힘. 그리는 층은 **`PopupLayer.Raise(go, offset)`**(KHD 10-30) — 자기 `Canvas`를 붙여 부모 캔버스 + offset에 그리고 입력도 먼저 받음: 편성 안 팝업 +10(`Panel`), `UiPopup`·뽑기 결과 +100(`Popup`), 토스트 +200(`Toast`, 입력 통과). 그래서 Dim을 누르면 팝업만 닫힘.
- **재화:** `Wallet`(골드·균사석). `WalletBar`가 `Wallet` 값. 예전 테스트 지급(균사석 50,000, `Wallet.TestGrant`)은 받은 기기에서 한 번 회수(`Wallet.TestReclaimed`).
- **방치 보상:** `IdleAway` — 탭을 연 동안·앱 밖 시간의 골드를 모아 홈으로 돌아오면 `IdleReportPopup`(오버레이 캔버스 30000).
- **강화 목록 잠금 문구:** 이름·값 글자(x 180~) 오른쪽 자리(x 330~)에만 쓰고 폭이 좁으면 글자를 줄인다(`UpgradePanel.LockTextLeft`).
- **절전 모드:** `PowerSaveMode`(코드로 만드는 오버레이 31000, 씬에 둘 것 없음). Main에서 3분 무입력 → 검은 덮개 + 카메라 끄기 + 10fps, 절전 중 번 골드·스테이지 표시, 누르면 해제. 전투는 계속 돎.

### 3.2 해상도 대응 (2026-10-30, `278ebce`)

실기기(약 20:9)에서 좌우·위아래가 잘린 것을 고쳤습니다. 원인: 에디터 게임 뷰가 16:9라 캔버스 폭이 1316인 상태로 UI를 고정 폭으로 맞춰 왔음(Expand 1080×2340 → 20:9 폰에선 캔버스 폭 1080).

- **`StageFrame`**(모든 씬 `Canvas/SafeArea/Stage`): 높이 **2340 고정**, 폭 **1080~1316**(화면에 맞춤). 더 길거나 넓은 화면은 남는 쪽이 **검정**(카메라 배경). 노치·홈바로 안전 영역이 모자라면 통째로 축소(scale < 1). `[ExecuteAlways]`라 편집 중에도 게임 뷰 비율대로 보임.
- **앵커 규칙:** 전체 폭 요소는 가로 stretch(하단바, 던전·뽑기 카드·판), 가장자리 요소는 그쪽 앵커(이름표 왼쪽, 열쇠·버튼 오른쪽). 칸 위치를 고정값으로 박은 묶음(편성 3×2 `Middle`, 꼬마 고르기 `Char/Char`, 장비 창 `Wepon`·`Amor`·`Pet`)은 **`FitWidth`**(designWidth 1316, 비율 유지로 통째 축소, 플레이 중에만).
- **배경:** Main `BG`는 Stage 전체로 늘림(11-05 기준 `BGimage`, 비율 유지 없음 — 10-30엔 `bg_main` Envelope). Start·InGame 배경은 `BGMask`(RectMask2D) 안, InGame Stage에 RectMask2D(필드 밖 장식 벽).
- **팝업은 Stage 아래에 만든다**(노치·홈바에 안 걸리게). 코드로 만드는 오버레이(방치 정산 1080×2340 기준, 로딩, 절전)는 화면 전체.
- **확인 방법:** 게임 뷰 크기 목록에 `MMM 1080x2340 (19.5:9)`·`MMM 1080x2400 (20:9)`·`MMM 1440x3200 (20:9)` 추가해 둠(+ 16:9 `1920x1080 Portrait`). 플레이 중 모든 Graphic의 월드 사각형이 Stage 밖으로 나가는지 재는 방식으로 Main(탭·장비 창·꼬마 고르기·옵션·정산·뽑기 결과)·InGame·Start를 측정 → 넘침 0. 노치는 SafeArea 앵커를 직접 바꿔 흉내(위 120·아래 60).
- **작업 기준:** 게임 뷰를 **1080×2340**으로 두고 작업하세요. 16:9에서만 맞추면 같은 문제가 다시 생깁니다.

### 3.3 세계수 (WorldTree 씬, 2026-11-02)

PC판 `MoreMustMush`의 `Game.Farm*.cs`를 방치형에 맞게 옮겼습니다. **사용자가 고른 대응(모두 추천안):** 창고 버섯 = 방치 전투에서 잡은 일반 적(적 그림 이름으로 적 시트 행을 찾음) · 세계수 꼬마 = 뽑기 꼬마 9종(가진 꼬마만) · 수확 보따리 = 단계별 시간당 표 · 효과 = 방치 능력치 배율.

> **지금과 다른 점:** 수확 보따리는 **11-03 KHD(`b28b237`)에서 제거**됨 — `TreeBagButton`·보따리 효과(BagGold·BagMycelite·BagHours) 없음, `TreeEffect`는 Attack·Hp·Speed·CritChance·CritDamage·KillGold·BossMycelite·Harvest·CraftSpeed. 아래 표의 보따리 내용은 기록용. **11-05:** 나무 그림 `Tree01~04`(4장을 단계가 공유, 단계마다 커짐), 장식 24종 새 그림. **11-09 KHD `50f354c`:** 코드로 만들던 패널(`TreeGrowPanel`·`TreeKidPanel` 등)을 프리팹으로 옮김 — 지금 화면 코드는 `WorldTree/UI/TreeGrowScreen`·`TreeKidScreen`·`TreeCraftPanel`·`TreeDecoPanel`. **11-10:** 의뢰 삭제(아래 표 줄).

| 파일 | 맡은 일 |
|---|---|
| `WorldTree/WorldTree.cs` | 효과 종류 `TreeEffect`(Attack·Hp·Speed·CritChance·CritDamage·KillGold·BossMycelite·Harvest·BagGold·BagMycelite·BagHours·CraftSpeed — 표의 effect 열 이름과 같아야 함), 자리 `TreeZone`(Root·Trunk·Branch·Crown), 저장(`WorldTree.Save` JSON 하나), `Changed` 이벤트 |
| `WorldTree.Growth.cs` | **성장:** 단계 1~8, 단계마다 나이테 Lv1~`treeRingMax`(골드). 나이테를 다 채우면 진화 = 경험치 + 골드 + 균사석. 경험치는 **공물**(창고 버섯을 통째로 바침, 단계가 좋아하는 버섯 `preferIds`는 ×`tributePreferMul`). **가호** = 지금 단계까지 것이 영구 적용. 창고 버섯 쌓기 `AddHarvest`(처치당 `treeHarvestPerKill` × 버섯 획득 효과, 소수는 확률) |
| `WorldTree.Kids.cs` | **꼬마:** **★진화** = 균사석 + 꼬마 레벨(필요 레벨 × `kidEvoLevelGradeMul`^등급, 높은 등급이 불리하지 않게 — K1) + 세계수 단계. 특기 = `bonusEffect` × (★ + 장식 세트면 1). **부탁:** 하루 `requestPerDay`건(한국 날짜), 창고 버섯을 주면 골드, 이번 주 `requestWeekGoal`건이면 주간 상자(균사석) |
| `WorldTree.Craft.cs` | **공방:** 주인 꼬마를 가진 장식만 목록에 열림, 골드·균사석·시간(장식등급), 슬롯 수 = 단계 `craftSlots`, 다 되면 받아야 생김. 가속 = 남은 `craftRushSeconds`마다 균사석 1, 하루 `craftRushPerDay`번. **꾸미기:** 장식을 자기 자리(뿌리·줄기·가지·수관)에 놓음, 자리별 칸 수 = 단계 `rootSlots` 등. 놓은 장식만 효과. 처음 놓으면 균사석, 한 꼬마의 장식을 다 놓으면 세트 |
| `WorldTree.Bonus.cs` | 효과 합계 = 가호 + 꼬마 특기 + 놓은 장식 (**캐시**, 세계수·꼬마 보유가 바뀔 때만 다시 셈). `Mul(effect)` = 1 + 합. **보따리:** 시간당 골드(지금 스테이지 적 처치 골드 × `bagKillsPerHour`)·균사석(`bagMycelitePerHour`)이 보관 시간(`bagCapHours` + BagHours 효과)까지 쌓임, 배율 = 나이테 + 꼬마 기여 + 보따리 효과. 비율이 바뀌는 행동 전에 `SettleBag`. long 넘침은 `ClampToLong` |
| `WorldTree/UI/TreePanel.cs` 외 | 시트 팝업 껍데기(`GrowPopup` 등 프리팹)는 그대로 두고 **Content 안을 코드로 채움**: `TreeGrowPanel`(성장)·`TreeKidPanel`(꼬마·부탁)·`TreeCraftPanel`(공방)·`TreeDecoPanel`(꾸미기)·`TreeBagButton`(보따리 받기). `TreePanel`은 `Changed`에 다시 만들고 1초마다 남은 시간 글자만 갱신. 조각·색은 `TreeUi` |
| ~~`WorldTree.Quests.cs` · `UI/TreeQuestPanel.cs`~~ | **🗑️ 11-10 삭제 — 메인 퀘스트(`DailyQuests`)로 합침, 세계수 경험치는 퀘스트 `treeExp`. 아래는 기록용.** **의뢰 (11-03):** PC `Game.Quests` 기반. 일일 = `의뢰` 시트 daily 행에서 하루(KST) `treeQuestDaily`(3)개, 주간 = weekly 행 전부(월요일 KST 00시), 날짜는 '뒤의 날'만 새로 뽑음. 종류: `stage` 보스 도전(`IdleBattle.StartWave` 보스 등장, 메인 일일 퀘스트 Stage와 같은 지점) · `kill` 일반 적 처치·`species` 지정 적 처치(`AddHarvest` 안, 라이브만) · `dungeon` 던전 1 판 수(`HarvestGame.Finish`; 소탕은 11-05 삭제) · `daily` 일일 받은 수(주간). 보상 = 세계수 경험치. 의뢰마다 가진 꼬마 하나가 의뢰인. 패널은 프리팹 `QuestPopup`에 놓인 칸을 이름으로 채움(주간 카드 3 = 의뢰 완료 버튼, 일일 칸 = Desc 눌러 받기, 남는 칸 숨김). 저장은 `WorldTree.Save` 안 |
| `UI/WorldTreeView.cs` | 단계별 나무 그림(세계수 시트 → 리소스 시트, `Resources/Art/Dream/Farm/Tree/Tree01~04` — 11-05 새 그림, 1 / 2~4 / 5~7 / 8단계가 공유, 옛 PC판 `tree_0~7` 삭제). 칸 크기 `stageSizes` 210→760px로 단계마다 커지고 땅(아래 변)은 고정. 놓은 장식을 자리별로 배치(`Farm/Props`, 11-05 새 그림 24종) |
| `UI/WorldTreeScreen.cs` | 재화 글자·옵션·메뉴 버튼 → 시트 팝업(`PopupSlot`), 뒤로 = Main. 예전 바구니 코드는 지움(보따리가 대신함) |

- **전투 반영:** `IdleProgress.FinalAttack`·`FinalHp`·`FinalHpRegen` × `WorldTree.Mul`, 새로 `FinalSpeed`·`FinalCritChance`·`FinalCritDamage`(IdleBattle·CombatPower가 씀). `IdleBattle` 일반 처치 → `AddHarvest`, 처치 골드 × Mul(KillGold), 보스 균사석 × Mul(BossMycelite)(소수 부분은 확률 — K2).
- **비용 규칙:** 골드 비용·보상은 전부 **"지금 스테이지 적 처치 골드 × kills"**(`KillGold`)라 방치 골드 곡선을 따라감. 표의 `*Kills` 열이 그 마리 수.
- **저장:** 창고 버섯처럼 처치마다 바뀌는 값은 메모리만 고치고 `SaveScheduler.BeforeFlush`에서 JSON을 씀. 진화·제작·받기 같은 드문 쓰기는 바로 저장.
- **확인:** 플레이 모드에서 다섯 기능을 코드로 눌러 보며 확인(재화 증감·저장·되읽기). 실기기·손 터치는 안 함.

**밸런스 v3 (`a3e1463`, `밸런스설계/세계수/`)** — 관리자(메인) + 이론(`worldtree-theory`) + 검증(`worldtree-verify`) 에이전트. 이론 수치안 → 관리자가 표 적용 → 검증 판정, 반려되면 이론으로. 기준 W1~W9는 `세계수/README.md`. 라운드 1 반려(W4: 전투 효과가 방치 진행을 앞당김) → v2 조건부 통과 → **v3 통과**.
- **11-03 재검증 (KHD 방치 v3 머지 뒤, 라운드 4·5):** 라운드 4 조건부(5단계에 칸이 없어 못 놓은 장식 4개 > 목표 3) → 사용자 "칸 늘리기" → **v4 = 세계수 9305 `branchSlots` 5 → 6** → 라운드 5 통과. 보통 플레이 2~8단계 도달 1.0 / 3.0 / 6.0 / 10.0 / 19.0 / 30.3 / **48.3일**(하루 처치 수가 줄어 11-02보다 늦음, 7주 끝 — 무광고 48.7일), 균사석 순소모 ≤ 4.6%, 골드 쓰기 ≤ 6.5%, 보따리 ≤ 2.6%, 49일 폭주 없음. 창고 버섯엔 보스 처치가 안 들어감(코드 기준, 검증 스크립트 고침). 숫자는 모델 추정, Unity 실측 없음.
- **11-03 기획서 칸 수 (v6, 라운드 7 조건부 통과):** 팀 기획서 「세계수 단계별 장식 개편 Ver02」(`MoreMustMush_세계수단계별장식개편_추가콘텐츠_Ver02.pptx`, 임시안)의 단계별 배치 칸을 적용 — 합계 1/2/3/4/6/8/10/12(구역당 최대 3), 자리 해금 뿌리 1·줄기 2·가지 4·수관 6단계(칸 수에서 자동, `ZoneOpenStage`). 장식은 24개 그대로라 8단계에도 12개만 놓이고 나머지는 보유 목록(교체 = 빼고 놓기). 불러올 때 칸을 넘는 배치는 뒤에서부터 뺌(`WorldTree.TrimPlacedToSlots`). 보정: 9307 evoExp 44만 → 41만, 장식 9711 `fire_lantern` 보따리 골드 0.02 → 보따리 균사석 0.03. 결과: 8단계 39.7일(의뢰 함)·48.0일(안 함) — 장식을 안 바꾸고 의뢰도 안 하면 51~52일(사용자: 지금대로). 남은 정리 C7-1~3(`세계수/검증.md` 라운드 7). 기획서의 꼬마 집·꼬마 일꾼(동시 작업 3·5·7단계 +1)·즉시 완료 하루 5회·푸시 알림·광고 리워드·보따리 삭제·친구/랭킹은 아직 반영 안 함.
- **11-03 의뢰 (v5, 라운드 6 통과):** 일일 d_stage 보스 도전 30 · d_kill 4,000 · d_species 350 · d_dungeon 던전 1 2판, 각 경험치 1,000 / 주간 w_daily 14(4,000) · w_stage 보스 도전 350(3,000) · w_kill 40,000(3,000). 의뢰를 다 하면 8단계 **40.0일**(일일만 42, 주간만 46, 안 하면 48.3), 의뢰 경험치 몫 15~20%. PC의 '한 판 콤보' 의뢰는 던전 1 실측(레벨 5부터 한 판 수확 1~3개·콤보 0~6)으로 성립하지 않아 '던전 1 판 수'로 바꿈(사용자 결정). ⚠️ **U5 (사용자 결정 대기):** 의뢰 보상이 세계수 경험치뿐이라 8단계(약 40일) 뒤엔 쓸 곳이 없음 — 지금은 그대로 쌓임.
- **11-02 결과 (옛 라이브 표, 보통 플레이):** 2~8단계 도달 1 / 1.7 / 3 / 6.3 / 13.7 / 22 / **34.3일**(KHD v2 방치안이면 45일). 전투 효과 합은 **공격 +2% · 체력 +2% · 치명 피해 +0.03뿐**(처치 골드·보스 균사석·치명 확률·공속 0). 보상은 세계수 안 효과(버섯 획득 +58~82%, 제작 속도, 보관 시간)와 보따리(하루 골드의 ≤5%, 8단계 균사석 하루 약 38)로 줌. 수치 전체는 `세계수/이론.md` 6장, 판정은 `검증.md`.
- **⚠️ 사용자 결정 대기 U1 (전투 효과 크기):** "방치 진행을 3일 넘게 앞당기지 않는다" 기준이라 전투 효과가 거의 장식 수준. 크게 하려면 KHD 방치 곡선이 세계수 효과를 품어야 함(그러면 v1 값 공격·체력 +10~30%로 되돌릴 수 있음). U2(공속 효과 0)·U3(창고 = 앱을 켠 동안만)·U4(D4)는 지금 결정 기준.
- **KHD에 넘길 것:** 라이브 표는 49일쯤부터 "보스 균사석 5 + s/10 → 뽑기 → 장비" 되먹임으로 하루 +180 스테이지씩 폭주(검증 측정, `세계수/현황/manager.md`). KHD v2/v3안은 보스 균사석 2 고정으로 다룸.

---

## 4. InGame (던전 1)

> 10-30 `e7c6336`에서 지웠다가 같은 날 KHD `3183125`가 **던전 1로 복원**하고 빌드에 다시 넣었습니다. 들어오는 길은 Main 던전 탭의 던전 1 "입장하기"뿐이고, 판 하나를 마치면 결과 팝업(번 골드만 표시, "한 번 더하기" · "나가기")에서 Main으로 돌아갑니다. 스킬 트리가 없어져 판 사이 성장은 없고, `DungeonRequest`(`HANDOFF_IDLE_PIVOT.md` 1장)도 아직 없어 늘 일반 스테이지 기본값으로 시작합니다. 첫 입장 때 튜토리얼(`InGameTutorial`: "드래그하여 발사" → "버섯을 채집하세요!", 한 번만). 벤치마크는 끝나면 Main으로 돌아옵니다. 아래 판 규칙·수치는 그대로입니다(결과 팝업 버튼 설명 중 "다음 스테이지·보스 도전·파밍·스킬트리로"는 옛 내용).

**상단 HUD (11-03):** `Column/UIPanel` — 왼쪽 위 `Wallet`(보유 골드·균사석 두 줄, `WalletLabel`이 지갑 값을 따라감), 오른쪽 위 `Stage(text)`(설정 버튼 왼쪽, 오른쪽 정렬·자동 크기) · `Option`, 가운데 아래 타이머 바. 이번 판 점수(번 골드) 글자는 뺐다(`StageHud.earnedGoldText`는 비워도 됨, 결과 팝업이 번 골드를 보여 줌). 예전 꼬마 칸 `KidSlot`(빈 박스)은 삭제. 배속 버튼 배경은 `btn_cream`.
**그림 (11-03):** 버섯 군락·단일 버섯(도감·의뢰 팝업 칸도 같은 그림)·수확기 낫·발사 대기 링·조준 화살표 머리·배속 1/2/4는 `Art/Generated/InGame/`(Codex 생성, 기존 Dream 수채화 그림을 참고 이미지로). 버섯은 흰색에 가깝게 그려 등급 색(`HarvestField` tier tint)이 곱해져 보이게 했다. 배속 2·4는 1배 삼각형을 겹쳐 만듦. 예전 GUID를 그대로 써서 씬·프리팹 연결은 바뀌지 않음.

### 4.1 판 규칙

1. 판이 시작되면 수확기가 바 위에서 대기합니다. **타이머는 첫 투척부터** 흐릅니다. 던지기는 한 판에 한 번(함께 던지는 수확기가 있으면 같이 나감).
2. 끌었다 놓으면 끈 반대 방향으로 던집니다(40px 미만은 취소). 수확기는 감속 없이 같은 속도로 벽·바·버섯에 튕깁니다.
3. 던진 뒤에는 화면 어디를 눌러도 좌우로 끄는 만큼 바가 움직입니다. 바는 **윗면만** 판정합니다.
4. **군락지:** 가운데 군락 버섯 1개 + 둘레 단일 버섯 n개. 군락지 하나는 같은 등급(일반·에픽·유니크)이고, 버섯마다 따로 황금 변종이 정해집니다. 군락지를 다 캐면 사라지고 리스폰 시간 뒤 빈 자리에 새로 돋습니다. 처음엔 벌집 격자에 놓고, 자리가 모자라면 버섯을 최소 0.35배까지 줄입니다.
5. 한 방에 캐지는 버섯은 뚫고 지나가고, 체력이 남는 버섯에는 튕깁니다(관통 버프 중엔 안 튕김).
6. **점수**는 판 중에 쌓이고, 끝나면 **골드 = 점수 × 전환율**을 지갑(`Wallet`)에 넣습니다. 결과 팝업에 골드·점수·수확량(버섯 개수).
7. 콤보 = 판 전체에 하나(`HarvestField.Combo`). 수확기가 버섯을 캘 때마다 +1, **수확기가 바를 놓쳐 바닥 벽에 닿으면 0**(바에 닿아도 유지). 분열체처럼 잠깐 있는 수확기가 바닥에 닿는 건 끊지 않음. HUD·점수 배율이 같은 값을 봄. 점수 ×(1 + min(콤보, `comboCap` 20) × 콤보 계수 0.05) — **콤보 점수 배율은 스킬 전 기준 최대 ×2.0**(스킬 `ComboRate`·피버 `ComboMul`이 계수 0.05에 곱해지므로 스킬을 배우면 ×2.0을 넘을 수 있음, 의도), HUD 콤보 숫자는 상한 없이 계속 오름(2026-10-27, 측정 M1: 상한 없이 0.1이면 후반 골드 ×9~21).
8. 게임 오버는 시간 종료, 또는 보스 처치(보스 판). 결과 팝업엔 닫기가 없고 다음 판 / 스킬트리로만 있습니다.
9. **일반 스테이지 = 목표 없음 (사용자 결정, 2026-10-28 `e3ee606`):** 일반 판은 마치면 늘 다음 스테이지로 넘어갑니다. 10-27에 넣었던 "버섯 N개 수확" 목표·연속 실패 완화(`goalRelief`·`FailStreak`·튜토리얼 고정 목표)는 코드에서 지웠고, Stage.xlsx의 `goalType`·`goal` 칸만 표에 남아 있습니다(게임은 안 읽음). `mushHpMul`(단단한 스테이지: 일반 버섯 체력 배율, 색은 `hardTint`로 조금 어둡게)은 그대로 씁니다. 보스 스테이지는 보스를 잡아야 넘어갑니다(4.4).
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
| `SkillEffects.cs` | 바 버프, 분열, 분신 바, 확률 스킬, 포자 디버프, 꼬마 수확기 (버프 글자 목록은 10-28 삭제, 수확기 색 표시만) |
| `FieldDevices.cs` | 환경 복원: 그루터기(튕김 + 공격력) · 꽃밭(가속) · 도토리 3개(다 켜면 점수 2배) |
| `BossGimmicks.cs` | 보스 기믹 4종. `Step(dt)`로 시험 가능 |
| `BossHud.cs` | 보스 판 상단 패널 아래 보스 이름 + 본체 체력 바·% (깎인 몫은 잔상), 분신이 살아 있으면 "분신 ×N". 일반·파밍 판엔 숨김 |
| `StageHud.cs` | 타이머·점수·콤보·배속, "스테이지 n". `ForceSpeed`(저장 안 하는 배속, 벤치마크용) |
| `Harvester.cs` | 버프 타이머(`HarvesterBuff`: Power·Haste·Pierce·Homing·Heal·Stump·Slow·Weak), 임시 여부·수명, 다음 타격 배율 |
| `Mushroom.cs` | 등급·황금·크기·체력 배율, 군락지 번호, IsCenter·IsBoss, 회복 |

이벤트(확장 지점): `HarvestField.Launched`, `BarHit`, `Hit(m, h, damage, crit)`, `Harvested(m, h)`(스킬 피해면 h가 null일 수 있음), `ColonyCleared(center)`, `BeforeColonies`. 스킬 피해는 `HarvestField.DealDamage(m, damage, source)`.

> **충돌 계산은 Physics2D를 쓰지 않습니다.** 화면이 전부 UI 캔버스라 Field 로컬 좌표(캔버스 픽셀)에서 원-원·원-선분 충돌을 직접 계산합니다.

### 4.4 보스 판

- Stage.xlsx에서 `boss`가 있는 스테이지(10·20·30·40·50)가 보스 판입니다. 군락지 대신 보스가 나오고 다시 돋지 않습니다.
- 보스 체력 = 100 × 보스 `hpRate` × 스테이지 `hpRate`. 관통되지 않고, 유도·번개·회오리는 보스를 노립니다.
- 맞힐 때마다 점수 = 피해 ÷ 보스 체력 × 0.35(`bossHitShare`, 기본 비율 — 다 깎았을 때 받는 몫) × **직전 일반 판 점수**. 보스 체력을 넘친 피해는 점수가 없습니다.
- 분신까지 모두 쓰러뜨리면 처치: 추가 골드 **max(이번 판, 직전 일반 판) × 3**(`bossRewardMul`, PC판 2에서 올림) + 첫 처치면 균사석(`gem`) + 레전더리 버섯, 판이 바로 끝납니다.
- 못 잡으면 **패널티 없음**: 그 판 골드 × `failGold`(표 1.0), 강제 파밍 없음. 결과 팝업 버튼이 **재도전 / 파밍 / 스킬트리로** 셋이 됩니다. 파밍 = `StageProgress.ChooseFarm` → `FarmRounds`(1)판 같은 스테이지 일반 판 뒤 재도전(옛 규칙 — 결과 팝업 간소화로 안 불려 `ChooseFarm`은 11-05 삭제). SkillTree 스테이지 글자에 "파밍 n". 끈기(진 판마다 보스 피해 ↑)·스킬 스테이지 잠금은 넣지 않음.
- **RoundBase 값은 `InGame.unity`에 직렬화**(`comboFactor`·`comboCap`·`bossHitShare`·`bossRewardMul`·`hardTint` 등 모두 씬에 있음). 씬 값이 우선이므로 C# 기본값만 바꾸면 반영되지 않습니다 — 인스펙터(HarvestGame)에서 고치세요. C# 기본값은 씬 값과 같게 맞춰 둠.
- **보스 판엔 황금 버섯이 나오지 않습니다**(`HarvestField.SpawnMushroom` :702 `golden = !stats.BossRound && …`, 1차 작업 T4에서 의도한 변경). 지금은 보스 판에 군락지가 없어 이 경로로 버섯이 안 나오므로 방어용 가드(보스 몸은 `SpawnBossPart`에서 원래 황금 아님). 보스 스테이지의 **파밍 판**은 일반 판이라 황금이 나옴.
- **보스 HUD (2026-10-28):** `BossHud` — 상단 패널 아래 이름(Boss.xlsx `name`) + 본체 체력 바(그림 `progress_fill_boss.png` = 타이머 바 채움의 색만 빨강). 본체가 먼저 쓰러지고 분신이 남으면 바는 0%로 남습니다. 보스 머리 위 작은 체력 바는 띄우지 않습니다.
- 보스 체력 (M2, 2026-10-27 다시 맞춤): Boss.xlsx `hpRate` 0.1809 · 0.5278 · 1.52 · 1.5 · 14.84 → 체력 72 · 256 · 1,054 · 1,040(+분신) · 15,434. 목표 첫 시도 처치율 90·85·60·55·45%. M1의 "30·50 보스 벽"은 옛 체력(10-26 맞춤)이 **e5f377e 전 바 폭 0 버그 상태**(바가 필드 가운데 폭 0 → 수확기가 위쪽 절반만 오감, 보스 피해 30에서 ×2.5)로 잰 값이었기 때문 — 바를 고친 뒤 30·50이 벽이 됐음. 50은 ★ 균사석으로 3339(꼬마 수확기)를 더 사게 되어 체력을 거의 그대로 둠. 40은 3341(분신술) 구매 여부로 피해가 약 5배 갈려 3341 없이 ~40% 처치가 되게 30보다 낮춤.

| 기믹 | 보스 | 동작 |
|---|---|---|
| None | 대왕송이 | 없음 |
| Heal | 숲 속의 청소부 | 3초마다 최대 체력의 3% 회복 |
| Purge | 죽음의 천사 | 2초마다 임시 수확기(분열체) 전부 제거 |
| Split | 환상 버섯 | 본체 66%·33%에서 분신 하나씩 (분신 체력 = 그 순간 본체 체력) |
| Move | 움직이는 버섯 | 좌우 260px/초, 몸마다 따로 벽에서 돌아섬 |

### 4.5 무한 스테이지 (2026-10-28, `StageProgress`)

- Stage.xlsx는 50스테이지까지입니다. 51부터는 `StageProgress.Get` → `Generate`가 **표의 마지막 10개 묶음(41~50)을 본떠** 만듭니다(`CycleLength` 10). 이름은 "들판 61"처럼 마지막 지역 이름 + 번호.
- 체력·보상 계수(`hpRate`·`rewardRate`)는 한 바퀴마다 표의 마지막 묶음이 커진 만큼(50 ÷ 40) 곱해 끊김 없이 이어 갑니다. `mushHpMul`·★ `gem`은 본뜬 자리 값 그대로입니다(첫 클리어에만 줌).
- 10의 배수 스테이지는 보스: 표의 보스 5종을 차례로 다시 쓰고, 마지막 보스(50)만큼 버티게 체력을 맞춥니다(`StageData.BossHpMul`, 코드에서만 있는 값).
- 마지막 스테이지(50)에 머물던 옛 세이브는 `Current = max(저장값, Cleared + 1)`로 넘은 스테이지 다음으로 이어집니다. 개발 메뉴·측정기도 표 뒤까지 진행합니다.

---

## 5. 스킬 트리 (SkillTree 씬) — 10-30 제거, `Backup/SkillTree/`에 백업. 아래는 기록용 (`SkillProgress`·스킬 테이블은 남아 있음)

| 파일 | 역할 |
|---|---|
| `SkillTreeScreen.cs` | 하단 메뉴, 설정 팝업, 노드 선택 ↔ 스킬 정보 창, 스테이지 이름 표시, 가지 넘기기 화살표(`BranchPrev`·`BranchNext`) |
| `SkillTreeView.cs` | `Build(branch)`: 시작 노드 + 한 가지의 노드를 깔고 선·이름(뒤에 어두운 판)을 그린다. 가지를 넘길 때마다 다시 만든다 |
| `SkillTreeLayout.cs` | 한 가지의 자동 배치(노드 자리 + 선 꺾임점). UI와 무관한 순수 계산 |
| `SkillTreeZoom.cs` | 두 손가락 확대·축소(에디터는 Ctrl+휠). 지금은 트리 폭 = 화면 폭이라 축소는 1배에서 막히고 확대(~1.25배)만 됩니다 |
| `SkillNode.cs` | 노드 하나 (`Setup(index)`) |
| `SkillInfoPanel.cs` | 이름·레벨·설명·못 채운 선행 조건, 배우기 버튼(가격 표시, 돈이 모자라면 꺼짐) |
| `SkillProgress.cs` | 레벨 저장(PlayerPrefs), 선행 조건, 가격(`Price`·`NextPrice`·`CanAfford`, `LevelUp`이 지갑에서 차감), `Total`·`Param`·`SeriesLevel`, `SkillAbility` 키. `SimLevels`를 넣으면 저장값 대신 그 레벨을 씀(측정기) |
| `Wallet.cs` | 골드·균사석(PlayerPrefs, long), `Add`·`TrySpend`·`Changed`, `Format`(만·억·조·경) |

- **노드 규격 (사용자 확정):** `Prefabs/Mobile/SkillTree/SkillNode` — 루트 150×150 `btn_cream`, Icon 80×80(위로 16), Level Jua 24pt. 새 노드는 이 프리팹 인스턴스로, `skillIndex`만 바꿉니다. 모양을 바꿀 땐 먼저 물어보세요.
- **선행 조건 (사용자 확정, 2026-10-28):** 트리에서 선으로 이어진 이전 노드(`link`)를 1레벨 이상 배웠을 것 — 이것 하나뿐입니다. 표의 `requires`(같은 스킬의 앞 레벨 노드, 135노드 중 89개에 있었고 모두 선과 다른 곳을 가리킴)는 선으로 보이지 않는 조건이라 해금 판정에서 뺐습니다(표 칸은 남아 있고 읽기만 함). 그래서 같은 스킬의 Lv.2·Lv.3 노드를 앞 레벨 노드 없이 먼저 배울 수 있고, 계열 레벨(`SeriesLevel`: 연속 번개 Lv.3 이상이면 어디서나 번개, 회오리 Lv당 지속 +1초)도 그 노드의 레벨로 바로 올라갑니다. 밸런스 측정기(`BalanceBot`)도 같은 판정을 씁니다.
- **가지 페이지:** 가지(독 · 식용 · 약용)마다 한 화면. 처음엔 **식용**. 양옆 화살표로 이웃 가지로 넘깁니다(독 ← 식용 → 약용, 끝 가지에선 그쪽 화살표를 숨김). 시작 노드 "사냥 준비"(3001)는 모든 가지의 맨 아래 가운데에 있고 그 가지의 첫 노드(3101·3201·3301)로 이어집니다. 넘기면 선택·스킬 정보 창은 닫힙니다.
- **배치 (코드 자동, 표의 `lane`·`row` 자리는 안 씀):** 가로 3칸(칸 간격 320, 화면 폭에 맞춤) × 층 간격 290, 위아래로만 스크롤. 가지마다 17~20층(식용 20, 독 19, 약용 17). 시작 노드 중심 높이 650(하단 메뉴 위로 ~100px 띄움). 화면에 3칸 × 4층쯤 보입니다.
  1. 층: 깊이 우선(형제는 작은 갈래부터)으로 쌓으며, 부모보다 위이고 칸이 남은 가장 낮은 층에. 한 갈래를 먼저 쌓아 대부분 부모 바로 위에 놓입니다. 단 **첫 갈림**(식용 3101, 약용 3201, 독 3301)에서는 두 갈래를 따로 쌓으며 함께 키웁니다(더 낮은 층에 놓일 쪽부터). (표의 row 순서로 쌓으면 갈래가 뒤섞여 여러 층을 건너는 선이 4배쯤 많아서 버렸습니다. 모든 갈림에서 따로 키워도 긴 선이 2배쯤 늘었습니다.)
  2. 한 층의 좌우 순서: 형제 중 먼저 쌓는 갈래를 가운데, 나머지를 왼쪽·오른쪽에 붙인 트리를 깊이 우선으로 훑은 순서. 여러 층을 건너는 선은 거치는 층마다 '지나가는 점'을 두고 도착 노드의 순서를 따릅니다 → 이웃 두 층 사이의 선끼리 엇갈리지 않습니다. 곁가지를 어느 쪽에 붙일지는 지나가는 선이 가운데 기준 좌우로 고르게 나뉘도록 찾습니다(갈림마다 하나씩 뒤집어 보며 나아질 때만 받아들이기, 고정 시드로 16번 다시 시작). 가지 하나에 수십 ms라 `SkillTreeView`가 가지별로 기억해 둡니다.
  3. 노드는 칸 가운데, 지나가는 점은 칸 사이 틈·빈 칸·바깥 틈(양옆 115px)에. 같은 틈의 선끼리는 10px씩 비키고, 아래 층에서 지나던 x를 이어 써서 곧게 갑니다.
  4. 선은 아래 점 → 층 사이 띠 → 가로 → 위 점. 같은 띠에서 가로 구간이 겹치면 엇갈리지 않는 높이로 나눕니다. 같은 부모의 갈래는 한 가로 선을 같이 씁니다.
  - 현재 표 기준 세 가지 모두 **선 교차 0, 노드·이름 침범 0**(아래에서 노드로 들어오는 세로 선은 이름 판 뒤로 지나감). 여러 층을 건너는 선은 가지마다 5~8개이고 가운데 기준 좌:우 = 독 5:3, 식용 2:3, 약용 3:4 (지나가는 층 수 10:9, 12:9, 5:7). 바깥 틈으로 가는 선은 양옆 화살표 밑을 살짝 지나갑니다.
  - 나중에 표에 위치 칸을 넣고 싶으면 `SkillTreeLayout.Build`의 1)·3) 단계만 바꾸면 됩니다.

---

## 6. 데이터 표

### 6.0 방치형 통합 표 (2026-10-30)

- **원본:** `Assets/Data/Tables/MainTable.xlsx` → 검증 통과 시 `Assets/Resources/Tables/MainTable.bytes`로 복사. `MoreMush/Data/Reimport Tables` 또는 저장 후 자동 임포트.
- **KHD v3 머지 (2026-11-03):** 공통 조상 `3d4631c`·LKH·KHD 세 판을 시트·열·행(index) 단위로 비교해 합쳤다. KHD가 바꾼 시트(스테이지 1~700·업그레이드·등급·뽑기·퀘스트)는 KHD 값 그대로, 꼬마 `attackMultiplier`·던전 `unlockStage`·상수 값 변경도 KHD. 던전에는 LKH 열(`name`·`scene`·`implemented`) 뒤에 KHD 열(`baseStage`·`stageStep`·`rewardKills`·`rewardMyc`·`rewardGrowth`). **KHD 새 상수(`dungeonSweepMul`·`tail*` 6개)는 index 9066~9071**(`dungeonSweepMul`은 11-05 소탕과 함께 삭제) — KHD는 9046~을 썼으나 LKH 세계수 상수가 9046~9065라 옮김(게임·검증기는 key로 찾음). KHD가 4로 바꾼 `adGachaPulls`는 LKH가 지운 상수라 **뽑기상품 광고 행 `pullCount` = 4**(이름 "… 4회 (광고)")로 옮겼다. LKH 스테이지 시트 차이는 소수 반올림뿐이라 버림. 병합 뒤 검증기의 필수 열·자료형·상수 범위를 스크립트로 대조(문제 0). **Unity 검증기(Reimport Tables)는 아직 안 돌림**.
- **현재 규모 (2026-11-02):** `info` 포함 **23개 시트**, 데이터 **1358행**(11-03 머지 뒤 스테이지 700행이라 약 1060행). 기존 18개 시트의 밸런스 값·수식 유지.
- **11-05 규모·정리 (`43c925c`·`4ef560d`):** `info` 포함 **25개 시트**(꼬마스킬 포함), 검증기 **1156행**. `리소스` **120행**(BGimage·임시도감 Unit 15·`bgm_main` 추가, `Grandpa` 7012·옛 `tree_0~7` 행 삭제, `type`에 **AudioClip** — 검증기가 허용). `보스` **10행** 2001~2010 + `cell` 열(적 군락 시트의 조각 번호). 안 읽는 열 삭제: 적·보스 `hpCoefficient`, 스테이지 `stageCoefficient`, 방치전투 `chapterName`, 세계수 `#bag` 메모 2열. 상수 `adBoxMyceliteBosses` → `adBoxMycelite`(90), `dungeonSweepMul` 삭제. 아래 "체력 계약"은 열이 없어졌으므로 기록용.
- **세계수 시트 (11-02, `7cd69e6`·`a3e1463`):** `세계수`(단계 1~8: 진화 경험치·골드·균사석, 나이테 비용, 보따리 시간당, 공방 슬롯, 자리별 칸, 좋아하는 버섯) · `가호`(단계마다 효과 하나) · `꼬마진화`(★0~3: 균사석·필요 레벨·보따리 배율) · ~~`호감도`(Lv1~10)~~ 11-12 삭제 · `장식`(24개: 주인 꼬마·등급·자리·효과) · `장식등급`(골드·균사석·시간). 기존 시트에 열 추가: `꼬마` favoriteIds·bonusEffect·bonusValue·evoStage, `적` value·requestCount, `상수` 세계수 상수 20개(`treeRingMax` … `bagCapHours`, `kidEvoLevelGradeMul`). index 대역 9301~(세계수)·9401~(가호)·9501~·9601~·9701~·9801~. 표에서 고칠 열 목록은 `밸런스설계/세계수/README.md` "범위". 1행 영문 키, 2행 자료형, 3행 한글 설명/필터, 4행부터 데이터. `#` 컬럼은 메모/미리보기.
- **이번 추가 정의:** `장비` 45행(무기·방어구·펫 각 15종, 종류별 저장 인덱스 0~14 유지), `뽑기상품` 12행(4종류 × 재화 11/35회·광고 1종), `방치전투` 1행(편성 칸·챕터·전투 타이밍), `수확전투` 58행(던전 1의 RoundBase·필드·수확기·단일/군락 버섯·바·입력 수치), `수확등급` 3행(Normal/Epic/Unique 체력·점수 배율).
- **추가 참조:** `던전`에 `name`·`scene`·`implemented`(던전 1만 InGame, 2~5는 빈 씬/미구현), `리소스`에 무기 그림 sam·saw·hs_gold(7225~7227). `info`에 컬럼별 설명·기준 코드/에셋·실제 적용 범위를 추가.
- **연동 (11-02):** 새 5개 시트와 던전 열을 게임이 읽는다. 씬·프리팹 인스펙터 값은 표에 행이 없을 때만 남는다. `Equipment.Names/Sprite` ← 장비 · `GachaPanel` 버튼 ← 뽑기상품(종류마다 광고 1·재화 2, 광고 하루 횟수 `Gacha.<종류>.AdCount`) · `IdleBattle` flightTime·waveDelay·enemyAttackDelay·챕터 ← 방치전투(편성 칸은 코드 상수, 검증기가 같은지 확인) · `InGame/HarvestTuning.cs`가 판 시작 때 RoundBase·HarvestField·FieldInput·바 크기·등급 배율을 넣고 수확기·군락·단일 버섯 프리팹은 표 값 복사본으로 바꿔 쓴다(원본 에셋 불변) · `DungeonEntry` 씬·이름 ← 던전(implemented FALSE면 입장 막음). 상수 `adGachaPulls`는 지웠다(광고 뽑기 수 = 뽑기상품 광고 행 pullCount).
- **체력 계약 (11-05 열 삭제, 기록용):** 적/보스의 `hpCoefficient × 스테이지.stageCoefficient`. 초기 계수는 기존 1스테이지 기준으로 적 2, 보스 72. 스테이지 계수는 기존 `ExpectedHit(s)`를 소수 8자리로 저장. 기존 코드의 웨이브 체력 증가·보스 기대 공격 속도 보정은 이 새 모델에 없으므로 **실제 게임 적용 전에 밸런스 재측정 필요**.
- **검증:** `Data/Editor/GameBalanceValidator.cs`가 필수 컬럼·자료형, ID/key 중복, 수치 범위, 해금 순환, 스테이지 연속 번호, 외래키, 실제 리소스를 검사. 오류가 있으면 기존 정상 bytes 유지. 검증기는 에디터 전용이라 `link.xml` 추가 불필요.
- **적용 범위 (2026-11-02 전환):** 게임이 `GameBalance`(`Data/GameBalance.cs`, 처음 쓸 때 한 번 읽음)로 이 표를 읽는다. 값은 전환 전 코드와 같다 (옛 구현과 스테이지 1~1500·강화 비용·장비·뽑기 4만 3천 건을 비교해 차이 0, 상대 오차 2e-16).
  - **읽는 것:** `업그레이드`(강화 8종 → `IdleProgress.Defs`) · `꼬마`(이름·등급·공격 배율·시작 레벨·기본 편성·그림 → `Formation`) · `스테이지`(웨이브 수·보스 시간·적 수·골드·균사석·적 공격 → `IdleProgress`) · `상수`(장비 최대 레벨·레벨 보너스·장착 배수·단계 확률·합성 조각 수, 꼬마 최대 레벨·보너스, 던전 하루 횟수, 앱 밖 보상 한도, 전투력 초, 기대 성장 곡선·적 체력 수치) · `등급`(장비 등급별 보유 효과 %) · `뽑기`(레벨별 경험치·등급 확률) · `던전`(던전별 해금 스테이지) · `리소스`(그림·소리 경로) · **11-05부터** 스테이지 `bossId` → `보스`(`resourceId`·`cell` = 방치 전투 보스 그림).
  - **안 읽는 것:** 스테이지의 `enemyIds`(적 그림은 `IdleBattle`이 Enumy 폴더에서 종류 비율로 뽑음), 보스 이름. (`hpCoefficient`·`stageCoefficient`는 11-05 열 삭제.) 적·보스 체력은 `IdleProgress`의 기대 성장 수식(수치는 `상수` 시트)을 그대로 쓴다.
  - **스테이지 시트 vs 상수:** 스테이지 시트에 있는 스테이지(1~1000)의 골드·균사석·적 공격은 시트 값, 그 뒤 스테이지는 `상수` 시트의 곡선(`goldBase`·`enemyAtk*` 등)으로 계산한다. 곡선 상수를 바꾸면 시트 열도 같이 고쳐야 한다.
  - **코드에 남긴 것:** 수식(기대 성장·가격·확률 뽑기), 이름·그림 경로·아이콘, 저장 키, 등급 이름, 장비 이름 목록, 장비 단계 수(`SubPerGrade`), 화면 크기·연출 수치.
  - **고칠 때:** 엑셀 저장 → Unity가 검증 후 `Resources/Tables/MainTable.bytes`로 복사(실패하면 Console에 시트·행·열을 적고 옛 bytes 유지). bytes를 직접 고치지 않는다. 새 상수를 더하려면 `GameBalanceValidator.RequiredConsts`와 읽는 코드를 함께 고친다.
  - **표가 없거나 필수 시트가 비면** 게임이 예외를 던진다 (숫자를 지어내지 않는다).
  - 보스 이름은 아직 화면 표시 연결 안 함.
- **Legacy:** 아래에 설명하는 Mushroom·Harvester·Skill·Stage·Boss 엑셀은 `Assets/Data/Tables/Legacy/`, 복사본은 `Assets/Resources/Tables/Legacy/`. GUID/내용 유지. `GameData.ResourceFolder = "Tables/Legacy"`로 기존 스킬트리 로딩을 유지하며, Legacy 엑셀은 자동 재임포트에서 제외.
- **확인 (2026-11-02):** Unity 임포트 1358행 문제 0. 수정 전 셀 23429개·수식 2016개와 기존 표/검증/틀 고정 보존 확인. 장비·상품·수확 설정·등급·던전/리소스/사용법 렌더 확인, 잘못된 참조·가격·저장 인덱스·필드/자료형·기본 수치 누락 등 10종 거부. 플레이 모드·PlayerPrefs·씬 저장 없이 확인.

**아래 표와 6.1~6.2는 Legacy 데이터 기록입니다.**

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

**Skill.xlsx 주요 열:** `branch`(Start·Edible·Medicinal·Toxic) · `rarity`(0~19, 유저 비공개) · `tag` · `group`(같은 스킬 계열) · `startLevel` · `maxLevel` · `lane`(형제의 좌우 순서만 씀)·`row`(지금 배치엔 안 씀) · `tier`(1 = 시작 스킬, link로 한 칸 멀어질 때마다 +1, 독·약용 10 / 식용 11까지. 노드 배치에 쓰려고 넣었고 아직 코드에선 안 씀. 노드를 더하거나 link를 바꾸면 손으로 맞춰야 함) · `ability`·`abilityBase`·`abilityValue`·`param` · `unlockPrice`·`currency`(1레벨 가격·재화) · `levelPrice`·`levelCurrency`(2레벨부터 첫 가격·재화, 비우면 1레벨과 같은 재화) · `priceGrowth`(레벨마다 배율) · `link`(유일한 해금 조건) · `requires`(지금은 안 씀).

**Stage.xlsx:** `stage` · `theme`(숲 1~, 달빛 밤 20~, 들판 45~ — PC판 지역) · `hpRate`(체력 계수 1 → 10.4) · `rewardRate`(점수·수확량 배율 1 → 736, hpRate와 따로 — 수입 성장 목표용) · `boss` · `goalType`·`goal`(**표에만 남음, 게임은 안 씀** — 10-28 일반 목표 삭제) · `mushHpMul`(일반 버섯 체력 배율, 비우면 1) · `gem`(★ 첫 클리어 균사석).

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
- **일반 목표 N:** (10-28 삭제) 표의 `goal` 칸에 맞춘 값이 남아 있습니다.
- **결과 (r8, 12시드):** 50보스 처치까지 평균 **88.3분**(85~91). 90분에 −1.7분 — 더 늘리려면 ★ 통과율 55% → 35%.
- **⚠️ 이 수치는 지금 규칙과 다릅니다.** 10-28에 일반 스테이지 목표를 지웠고(목표 실패로 같은 스테이지를 다시 하던 시간이 없어짐), 해금 조건도 이전 노드 하나로 바뀌어(같은 스킬 앞 레벨 없이 뒤 레벨을 살 수 있음) 구매 순서·속도가 달라졌습니다. 50보스 시간은 다시 재야 합니다.

**측정 스크립트**(아직 저장소에 없음, 임시 폴더): `schedule.py`(목표 일정) · `theory.py` · `iterate.py`(가격 고정점) · `boss_fit.py`(보스 체력) · `rescale_reward.py`(보상 배율) · `metrics.py`(지표) · `apply_prices.py`·`apply_stage_boss.py`(엑셀 쓰기).

---

## 7. 팝업·설정

| 프리팹 | 용도 | 비고 |
|---|---|---|
| `OptionPopup` | Main·Start·세계수 설정 | Main은 씬에 미리 둠(`Stage/OptionPopup`, 우상단 `OptionButton`). **11-10 KHD 정리(`e52a545`):** 배경음(BGM 음량 `BgmVolumeSlider`)·효과음(값만 저장, 효과음 없음)·**언어**(한국어/영어)·**데이터 초기화**(5초 누르기 `HoldConfirmPopup` → 기록 전체 삭제 → Main, `25384f8`)·광고 개인정보 설정 링크(동의 지역만). 진동·알림·연출 간소화는 없앰 |
| `Main/GGoma`·`Char`·`Wepon`·`Amor` | 편성·꼬마 고르기·장비 | 3.1. (`Pet`은 11-04 삭제.) `Char` 아래 버튼 줄은 꺼 둠 — 정보 칸 `CharacterDetail`이 대신함. 11-10 꼬마 성장 버튼(Lv.5·10 관문) |
| `Main/DungeonUI`·`GachaUI` | 던전 목록·뽑기 | `Banner`(고정) + `ListPanel` + `Viewport/Content`(스크롤). 던전 1~5 모두 입장 가능(해금 스테이지 뒤) |
| `GachaResultPopup` (코드) | 뽑기 결과 연출 | `PopupLayer.Popup` 층 |
| `InGameOptionPopup` | InGame 옵션 | `OptionPopup`의 변형(원본을 고치면 같이 바뀜). BGM/SFX, **로비로 돌아가기 → Main**(`LobbyRow`, `SceneLoader`). 열면 일시정지 |
| `ConfirmPopup` · `HoldConfirmPopup` | 예/아니오 확인 · 꾹 누르기 확인 | `ConfirmPopup.Show(title, message, onYes)` — 종료 확인(`MainBackHandler`)·레이드 나가기(`DungeonSession`). `HoldConfirmPopup` — 데이터 초기화(`DataResetButton`). 11-09 게임 UI 톤(나무 테두리) |
| `ResultPopup` | InGame(던전 1) 결과 | 10-30 간소화: **번 골드만**, 버튼 "한 번 더하기"(같은 씬 다시) · "나가기"(→ Main). 스테이지·점수·도감·신종·전투 통계·파밍·닫기는 제거 |
| `Toast` | 안내 문구 | 공용 |

알림·확인 팝업은 `PopupSlot`으로 들고 있다가 **처음 열 때 Stage 아래에 만듭니다**. Main의 팝업·패널은 씬/프리팹에 미리 있습니다. 정렬 순서: Main 캔버스 10000, 편성 안 팝업 +10, `UiPopup`·뽑기 결과 +100, 토스트 +200 (`PopupLayer`), 방치 정산 30000, 절전 31000, 로딩 팝업 32000.

**연결 안 된 UI (11-11 기준):** 옵션 창 효과음 슬라이더(값만 저장), 도감 황금 토글(모양만). 그 밖의 버튼은 모두 연결됨.

**데이터 초기화:** 옵션 창 버튼(11-10 다시 넣음, `DataResetButton.ClearSaves`) 또는 에디터 메뉴 `MoreMush/게임 기록 전체 초기화`(8장). 강화·스테이지만 지우려면 `MoreMush/방치형 진행 초기화`.

---

## 8. 개발용

**메뉴:** `MoreMush/Data/Reimport Tables` · `MoreMush/Fonts/Bake Static Atlases`(11-05, 아래 "폰트") · `MoreMush/Debug/Go To Next Boss Stage` · `MoreMush/Debug/Reset Stage` · `MoreMush/Debug/Run Benchmark` · `MoreMush/Debug/Open Benchmark Folder`
**PlayerPrefs 키:** `Skill.Level.<index>`, `Stage.Current`, `Stage.FarmLeft`, `Stage.LastNormalScore`, `Stage.LastNormalGold`, `Stage.Cleared`, `Wallet.Gold`, `Wallet.Mycelite`, `Wallet.TestGrant`, `Wallet.TestReclaimed`, `InGame.SpeedIndex`, `Option.BgmVolume`, `Option.SfxVolume`, `Dungeon.<n>.Day`·`Dungeon.<n>.Used`(던전 입장 횟수, 한국 날짜)
방치형: `Idle.Stage`, `Idle.Lv.<stat>`, `Idle.GoldPerSec`, `Idle.LastSeen`, `Formation.<칸>`·`Formation.Level.<id>`·`Formation.Copies.<id>`·`Formation.MigratedLevel`(예전 `Formation.Owned.<id>`는 한 번 옮기고 지움), `Gacha.*`(광고 뽑기 날짜는 예전 키 `Gacha.<종류>.AdDay` 그대로 + 11-02 하루 사용 수 `Gacha.<종류>.AdCount`), 장비 `Weapon.*`·`Armor.*`(`.Level.<i>`·`.Copies.<i>`·`.Equipped`·`.Layout` — 번호 = 장비 시트 itemIndex, 배치가 바뀌면 `Equipment.RemapLayout`이 한 번 옮김. 11-04 펫 삭제로 `Pet.*`·`Gacha.Pet.*`는 안 읽음), **세계수 `WorldTree.Save`**(JSON 하나: 단계·나이테·경험치·창고·꼬마 ★·부탁·제작·장식·보따리), **도감 `Dex.<도감.index>`**(11-10, 모은 종 비트 — 번호 i = 그림 조각 `시트이름_i`).
**메뉴 `MoreMush/방치형 진행 초기화`는 `Idle.Stage`와 강화 8종 레벨만 지웁니다** — 지갑·장비·꼬마·뽑기·던전·세계수는 남음. **전부 지우려면 `MoreMush/게임 기록 전체 초기화`**(11-03, `Editor/SaveDataMenu.cs`): 확인 창 → PlayerPrefs 전부 삭제 + 메모리 캐시(`Wallet`·`IdleProgress`·`Formation`·`Equipment`·`WorldTree`·`DailyQuests`·광고 상자) 버림. 플레이 중이면 플레이를 끝낸 뒤 지움(끝날 때 `SaveScheduler`가 메모리 값을 다시 저장하므로). 지우는 코드는 설정 팝업용 `DataResetButton.ClearSaves()`와 같음. 벤치마크 결과 파일(`persistentDataPath/Benchmark`)은 안 지움.

**밸런스 측정기 (`BalanceBot`, InGame 씬 플레이 모드에서 MCP `execute_code`로)**
- `RunCampaigns(seeds, skill, incomeMul, probeBoss, schedulePath, outPrefix, maxRounds, bossDamageMul)` → `Temp/<prefix><seed>.csv`(판마다 한 줄) + `_buys.csv`. 문서 사람 모델은 `(…, 1.0f, 0.6f, false, null, "Temp/bb_s", 250, 0.234f)`.
- `ProbeBoss(stage, "index:level;…", refScore, refGold, tries, skill, seed, outPath)` → 보스 한 판 피해를 여러 번.
- 저장값은 건드리지 않습니다(`SkillProgress.SimLevels`, `HarvestGame.Simulating`).
- 진행 규칙은 게임과 같음: 일반 판은 늘 다음, 보스에 지면 바로 재도전(`Config.farmRounds` 0 = "재도전" 선택), 균사석은 `StageProgress.FirstClearGem`, 스킬 구매는 `SkillProgress.CanLevelUp`(해금 조건 = 이전 노드 하나). 오래 걸리면 MCP 호출은 시간 초과로 실패로 뜨지만 계속 돌아 CSV가 나옵니다.
- 측정기는 한 프레임 안에서(에디터가 뒤에 있으면 frameCount가 안 오름) 수천 판을 돕니다. 그래서 **프레임 끝에 처리되는 것(`Destroy`, 끝나지 못한 코루틴)을 판마다 만들면 쌓여서 점점 느려집니다**(2026-10-27 수정 전: 약 3,000판 뒤 ×70). 판마다 다시 쓰는 오브젝트는 풀/재사용, 짧은 연출은 코루틴 대신 Update로. 같은 시드는 같은 결과(판 사이에 남는 상태는 `Restart`에서 초기화).

**성능 벤치마크 (`Benchmark`, 2026-10-28)**
- 시나리오 7개(일반 1·25·49스테이지, 분신 보스 40, 이동 보스 50 / 스킬 없음·절반·최대 + 25스테이지 분열만 최대 1배속·4배속)마다 InGame 씬을 새로 열어 자동 조작으로 판을 돌리며 매 프레임 잽니다. 기본 시나리오당 30초(예열 3초 버림), 약 4분. 시나리오는 `Benchmark.Default`에서 `skill`(전체 비율)·`maxAbilities`(이 능력 키의 스킬만 최대)·`speed`(배속, `Time.timeScale`)로 정합니다. 측정 시간은 배속과 상관없이 실제 초이고, 제한 시간은 배속만큼 늘려 줍니다.
- 잰 값: FPS(평균·하위 1%·목표의 1.5배를 넘긴 끊김 비율), 프레임 시간 p95·최대, CPU 메인·렌더 스레드 ms, GPU ms, 메모리(Total Used·System Used·Mono 최대), GC 할당 KB/s, 드로우 콜·SetPass·배치, 평균 버섯·수확기 수. 기기 정보도 같이 적습니다.
- 결과: `Application.persistentDataPath/Benchmark/<시각>_summary.csv`(시나리오별)·`_timeline.csv`(1초 단위). 엑셀에서 한글이 안 깨지게 BOM을 붙입니다. 끝나면 화면에 요약이 뜨고, 누르면 SkillTree 씬으로 갑니다.
- 시작: 에디터 메뉴 `Run Benchmark`(플레이 중이 아니면 플레이 모드에 들어가 시작), 또는 실행 인자 (옵션 팝업 버튼 `BenchmarkButton`은 팝업에서 빠져 11-03 삭제) `-benchmark`(선택 `-benchmark-seconds=60`·`-benchmark-repeat=3`). 안드로이드: `adb shell am start -n <패키지>/com.unity3d.player.UnityPlayerGameActivity -e unity "-benchmark"`, 결과는 `adb pull /sdcard/Android/data/<패키지>/files/Benchmark/`.
- 저장값은 건드리지 않습니다(`SkillProgress.SimLevels`, `HarvestGame.Simulating`). 보스는 죽지 않게(`BossHpMul`), 분신 보스는 30%로 깎아 분신 둘을 띄운 채로, 배속은 시나리오 값으로 고정합니다(배속 버튼은 무시). 바 조작은 `BalanceBot.AimX`를 같이 씁니다.
- CPU·GPU 프레임 시간은 Player Settings의 **Frame Timing Stats**(이번에 켬)가 있어야 나옵니다. 드로우 콜 같은 렌더 카운터는 정식 빌드에서 0으로 나오는 기기가 있습니다. 에디터 수치는 PC 성능이라 참고용입니다. **MCP 스크린샷(`manage_camera screenshot`)은 플레이 모드를 일시정지시키므로 벤치마크 도중엔 찍지 마세요**(그 구간 수치가 망가집니다).
- 앱 전체 목표 프레임은 `AppSettings.TargetFrameRate` = 60 (전에는 설정이 없어 모바일 기본 30fps였음).
- **Firebase Test Lab Game Loop** (2026-10-28): 안드로이드 빌드 때 `GameLoopManifest`(`IPostGenerateGradleAndroidProject`)가 런처 액티비티에 `com.google.intent.action.TEST_LOOP` intent-filter를 붙입니다(커스텀 매니페스트 없음). 이 신호로 켜지면 벤치마크가 바로 돌고, 끝나면 요약+1초 기록을 Firebase 결과 파일(intent data URI)에 쓰고 `Application.Quit()`. 시나리오 번호는 무시하고 늘 7개 전부(약 4분 → Test Lab 제한 시간 10분 권장). CSV도 받으려면 가져올 폴더에 `/sdcard/Android/data/com.soomyouth.moremustmush/files/Benchmark` 지정.
- 안드로이드 패키지 이름: `com.soomyouth.moremustmush` (2026-10-28, 전에는 URP 템플릿 기본값).

**주의할 점**
- **폰트:** 지금 쓰는 폰트는 **LINESeedKR Rg·Bd** 뿐이고, **11-05부터 정적(Static) 아틀라스**(`970fa52`, 60pt·여백 6·1024)입니다 — 동적 아틀라스에서 플레이 중 전투 HUD 글자가 사라지던 문제(재현 못 함) 대응. 아틀라스에 없는 글자는 □로 나오므로 **표·코드·씬에 새 글자를 넣었으면 메뉴 `MoreMush/Fonts/Bake Static Atlases`로 다시 굽고 폰트 에셋을 커밋**하세요(`FontBaker`가 표·코드·씬/프리팹 글자를 모음, `Reimport Tables`가 폰트에 없는 표 글자를 경고). TMP 기본 폰트도 LINESeed Rg입니다. 예전 Jua·Nunito·GowunDodum·ONEMobile·LINESeed Thin·TMP LiberationSans는 10-30 삭제(`b7e2863`). 프리팹·씬에 **`m_fontMaterial` 오버라이드가 남으면 그 폰트 전체가 빌드에 끌려 들어갑니다**(WorldTree TopBar가 Jua 4종 16MB를 끌고 있었음) — 런타임 값이라 저장되면 지우세요.
- **MCP 스크린샷:** `manage_camera screenshot`은 화면 오버레이 캔버스를 빼고 찍히고(빈 하늘만 나옴) 플레이 모드를 일시정지시킵니다. UI를 보려면 캔버스를 잠시 `ScreenSpaceCamera`로 바꿔 카메라를 `RenderTexture`에 렌더해 PNG로 저장하세요(스킬 트리 확인 때 이렇게 함). 찍은 뒤 `EditorApplication.isPaused = false`.
- **에디터 포커스:** Unity 창이 뒤에 있으면 플레이 루프가 멈추거나(frameCount가 안 오름) 가상 입력이 버려집니다. 시험은 `HarvestField.Step(dt)`, `BossGimmicks.Step(dt)`처럼 정해진 시간만 흘리는 방식으로 하세요. **`Time.deltaTime`이 쌓일 때까지 도는 while 루프는 deltaTime이 0이면 Unity가 멈춥니다**(실제로 한 번 강제 종료함).
- **저장 안 된 씬:** 에디터에서 씬을 고친 채 저장하지 않으면, 스크립트·MCP로 씬을 저장할 때 그 변경까지 같이 저장됩니다. 플레이 모드 중엔 씬 저장이 안 됩니다.
- **화면 비율:** CanvasScaler 1080×2340 Expand + `StageFrame`(3.2). InGameArea 1080×1520 고정.
- **테스트 스킬:** 스킬을 많이 배워 두면 필드가 몇 초 만에 비고 보스도 순식간에 죽습니다.
- **Awake 순서:** `HarvestGame.Awake`가 `HarvestField.Awake`보다 먼저 `Configure`를 부를 수 있습니다. 필드가 Awake에서 준비하는 값은 처음 쓸 때 준비되게 하세요(`SaveBarBase` 참고).

**2026-10-30 작업에서 알게 된 것**
- **잔액·강화 레벨은 메모리 캐시(KHD `4a783a7`):** `Wallet.Gold/Mycelite`와 `IdleProgress.Level`은 처음 한 번만 PlayerPrefs를 읽습니다. **값은 꼭 `Wallet`·`IdleProgress`를 거쳐 쓰고**, PlayerPrefs를 직접 지우거나 고쳤으면 `Wallet.Reload()`·`IdleProgress.ReloadLevels()`를 부르세요(데이터 초기화 버튼은 부름, 플레이 시작 때 자동). **10-30 최적화 작업부터 `Formation`(칸·꼬마 레벨·조각)·`Equipment`(레벨·조각·장착, `TotalPercent`)도 같은 방식** — 직접 고쳤으면 `Formation.Reload()`·`Equipment.ReloadAll()`.
- **머지 뒤 새 스크립트가 컴파일에서 빠짐:** KHD 머지 직후 Unity가 새 .cs 5개를 에셋 DB엔 등록하고 컴파일 대상에선 빼서 "Missing script"·"type not found"가 났습니다. 다시 임포트로는 안 되고, 에디터에서 **파일 이름을 바꿨다 되돌리면**(`AssetDatabase.RenameAsset`) 들어갑니다. `CompilationPipeline.GetAssemblies()`의 `sourceFiles`로 확인.
- **TMP 색을 코드로 바꾼 프리팹:** `LoadPrefabContents`에서 `TMP_Text.color`만 바꾸고 저장하면 `m_fontColor32`가 옛 값으로 남아, 씬을 저장할 때마다 글자 색 덮어쓰기가 수십 줄 생깁니다. 프리팹을 고칠 땐 `SerializedObject`로 `m_fontColor32.rgba`(= A<<24|B<<16|G<<8|R)도 맞추세요(편성 프리팹 5개는 10-30 맞춤).
- **MCP로 플레이 확인:** 에디터가 뒤에 있으면 프레임이 안 넘어가 애니메이션·코루틴이 멈춥니다. `EditorApplication.isPaused = true` 후 `EditorApplication.Step()`을 반복하면 한 프레임씩 진행됩니다(이때 콘솔의 `JobTempAlloc` 경고는 무시). 스크린샷은 `manage_camera screenshot`(include_image=false로 파일 저장 → 잘라서 보기)이 안정적이고, `ScreenCapture.CaptureScreenshotAsTexture`는 빈 화면이 나왔습니다. 꺼진 오브젝트에선 코루틴이 안 돌므로 `SetActive(true)` 먼저.
- **MCP `execute_code`에서 `AssetDatabase.DeleteAsset`은 막혀 있음** → 파일 삭제는 `git rm` 후 리프레시.
- **Codex로 그림 만들기:** `codex exec --skip-git-repo-check --sandbox workspace-write -i 참고.png - <<'EOF' … EOF` (프롬프트는 표준 입력으로 — `-i`가 뒤 인자를 이미지로 먹음). 결과는 `~/.codex/generated_images/`에도 남습니다. 자리를 맞춰야 하면 **색 상자 배치 안내 그림**을 두 번째 `-i`로 넘기면 잘 따릅니다(다만 톤이 바뀔 수 있어 10-30에는 이전 그림으로 되돌림).
- **폰트 에셋 `LINESeedKR-Bd/Rg SDF.asset`:** ~~Dynamic 아틀라스라 커밋 금지~~ — **11-05 폐기된 규칙.** 지금은 정적 아틀라스라 플레이해도 바뀌지 않으며, 다시 구운 뒤에는 **커밋 대상**입니다(위 "폰트").
- **Player Settings는 저장해야 파일에 남음:** 회사명·게임명 등은 바꾸는 즉시 `ProjectSettings.asset`에 쓰이지 않습니다. **File > Save Project** 후 커밋하세요(안 하면 브랜치 전환·병합 때 되돌아감, 10-30).
- **프리팹 Variant:** `InGameOptionPopup`은 `OptionPopup`의 Variant입니다. 원본에서 자식을 지우면 Variant에서 그 자식에 붙여 둔 것이 부모 밖으로 밀려납니다(10-30 "로비로 돌아가기"가 그랬음).
- **MCP 입력 시험:** 에디터 창에 포커스가 없으면 `InputSystem.QueueStateEvent`로 넣은 키가 처리되지 않습니다. 뒤로가기는 `PopupGate.HandleBack()` → `BackButton.SceneHandler`를 직접 불러 확인했습니다.

**최적화·안정화 (2026-10-30 `de3432e`, 코드 컴파일만 확인 — 에디터·실기기 확인 필요. 자세히는 `HANDOFF_OPTIMIZATION.md`)**
- **PlayerPrefs 읽기 제거:** 전투가 매 프레임(`FinalHp`·`FinalHpRegen`)·던질 때마다(`FinalAttack`) 장비 15개 × 3종 레벨을 PlayerPrefs에서 읽고 있었음(프레임당 약 70번 + 키 문자열 할당, 안드로이드에선 JNI 호출). `Equipment`·`Formation`을 메모리 캐시로 바꾸고 `Equipment.TotalPercent`는 레벨·장착이 바뀔 때만 다시 셈.
- **저장 모으기 `SaveScheduler`:** 자주 바뀌는 값(전투 골드 1초마다 `Wallet`, 강화 꾹 누르기 `IdleProgress.TryUpgrade`, 마지막 접속 시각 `IdleAway.MarkSeen`, 배속·볼륨)은 `SaveScheduler.MarkDirty()`만 하고 5초마다·앱이 뒤로 갈 때(`OnApplicationPause`/`Focus`)·종료 때 한 번에 `PlayerPrefs.Save()`. 뽑기·합성·편성·스테이지처럼 드물고 중요한 쓰기는 그대로 바로 저장(밀린 값도 같이 저장됨). **새로 자주 쓰는 값은 `MarkDirty`를 쓰세요.**
- **전투 서브 캔버스:** `IdleBattle`이 Awake에서 `IdleBattle`·`EnemyArea`·`GgomaArea`에 `Canvas`를 붙임(`IdleBattle`만 `GraphicRaycaster`). 전투가 매 프레임 움직여도 Main 캔버스의 강화 목록·하단바·재화 바는 다시 묶이지 않음. **에디터에서 확인할 것:** 숫자 팝업·타격 이펙트가 옵션 버튼·재화 바·강화 목록보다 위로 그려지지 않는지(중첩 캔버스 그리는 순서). 문제가 있으면 `AddSubCanvas` 세 줄을 지우면 예전과 같아짐. 숫자 팝업·타격 이펙트는 `canvasRenderer.SetAlpha`로 흐려짐(TMP color를 바꾸면 매 프레임 메시를 새로 만듦). 체력·전투력 글자는 값이 바뀔 때만.
- **기타 성능:** `UpgradePanel` — 잔액이 바뀔 때(1초마다)는 버튼 활성·색만(`RefreshAfford`), 글자는 강화 레벨이 바뀔 때만. `PowerSaveMode` — 씬 이름을 매 프레임 읽지 않음(문자열 할당). `BossHud` % 글자는 바뀔 때만. `Wallet.Format` 단위 배열 static.
- **안정화:** 화면 꺼짐 방지(`Screen.sleepTimeout = NeverSleep`, `AppSettings` — 안 하면 기기 화면 꺼짐(30초~2분)이 절전 모드(3분)보다 먼저 와서 방치 전투가 멈춤). 앱으로 돌아오면 절전 덮개 해제. 골드 더하기가 long을 넘으면 0이 되던 것 → 최댓값에서 멈춤. 앱 밖 보상: 초당 골드를 `InvariantCulture`로 저장·읽기(기기 언어가 바뀌면 값이 1만 배 틀어질 수 있었음), 망가진 `Idle.LastSeen` 값 범위 검사, **시계를 되돌려도 마지막 접속 시각은 뒤로 가지 않음**. **던전 입장 횟수·무료 뽑기는 저장된 날짜보다 '뒤의 날'일 때만 다시 채움**(시계를 되돌렸다 맞추는 반복 방지). 둘 다 저장값이 지금보다 하루(`IdleAway.MaxClockSkew`) 넘게 앞서면 한때 틀린 시계로 보고 버림. 표에 없는 보스 스테이지는 일반 스테이지로 취급(`StageProgress.HasBoss`, 보스 판·파밍 판에 갇히지 않게). 무한 스테이지 체력·보상 float 넘침 상한(1e30), 스킬 가격 long 넘침 상한, 표에 없는 보스면 보스 판으로 보지 않음.
- **설정:** URP `Mobile_RPAsset` — HDR·메인 라이트 그림자·라이트 쿠키·렌즈 플레어·지형 구멍·LOD 크로스페이드·혼합 조명·반사 프로브 블렌딩 끔(UI만 쓰는 게임이라 셰이더 변형·대역폭 절약), **Render Scale 0.8 → 1**(Main 캔버스가 Screen Space-Camera라 UI 전체가 80%로 그려져 흐렸음). Player — **Optimized Frame Pacing(Swappy) 켬**, 밉맵 스트리핑 켬, 릴리스 빌드 Log·Warning 스택 트레이스 끔(Error·Exception은 유지).
- **안 한 것:** 스프라이트 아틀라스(Sprite Packer가 꺼져 있음 — 켜고 아틀라스를 만들면 드로우 콜이 줄지만 에디터에서 확인하며 해야 함), `homeTabs`가 Main 씬에서 비어 있어 전투가 늘 '홈'으로 동작(탭을 열어도 숨기지 않음·방치 정산 팝업 안 뜸 — 의도인지 확인 필요), InGame에 있던 시간이 앱 밖 시간으로 세어지는 것(던전 다녀오면 방치 보상도 받음 — 의도인지 확인 필요).

**정리 (2026-11-03 전수 조사)**
- 기준: 빌드 씬 4개 의존성(`AssetDatabase.GetDependencies`) + Resources(코드 문자열·폴더 로드·`리소스` 시트) + 스크립트별 씬·프리팹 GUID·코드 참조 + 메서드별 호출(씬 `m_MethodName`·메뉴·`RuntimeInitializeOnLoadMethod` 포함).
- **지움:** 어디에도 붙거나 불리지 않는 스크립트 `AdmobTestButton`(광고 확인용 임시)·`BenchmarkButton`·`BossBadgeLabel`(옛 판 플레이 하단바)·`ComingSoonButton`, 안 불리는 메서드 `DungeonRewards.Blocked`·`IdleProgress.FarmFactor`·`WorldTree.CraftProgress`·`DataTable.TryGet/GetByKey`, 세계수 임시 그림 `Art/Generated/WorldTree/world_tree_1~3`(PC판 `Farm/Tree`로 대체). 빌드 크기엔 영향 없음(원래 빌드에 안 들어감).
- **확인하고 남긴 것:** Resources 안 파일은 모두 코드·표·씬이 씀(`리소스` 시트 63행 전부 다른 시트가 가리킴). `CollectPopup`(도감 — 11-05 삭제)·`world_tree_0`(씬이 씀 — 11-05 `Tree01`로 바꾸고 삭제)·`Sprite/BG/imsi`·`Sprite/Tree/image`(세계수 하늘·들판 배경, 이름만 임시)·`NewInGame` 씬(kms)·앱 아이콘 v2(Player Settings).
- **남은 후보 (결정 필요, 11-11 갱신):** ~~`밸런스설계/test/sim/bin·obj`~~(11-05 git 제외), 행 없는 Legacy 표 `Mushroom`·`Harvester`(게임이 읽기만 함), ~~어디에도 안 붙은 `DataResetButton`~~(11-10 옵션 창에 다시 붙음), 출시 전 옛 세이브 옮기기 코드(`Wallet` 테스트 균사석 회수·`Formation`/`Equipment` MigratedLevel·`Gacha.<종류>.AdDay`) — 9.4.
- `.gitignore`: `*.log`에서 `밸런스설계/**/*.log`는 예외(문서가 근거로 가리키는 계산 기록).

**출시 준비 (Google Play, 2026-10-30 검증)**
- **대상 API 36 고정**(`AndroidTargetSdkVersion: 36`, 2026-08-31부터 새 앱·업데이트 필수, 연장 시 11-01까지). Automatic이면 SDK 36이 없는 PC에서 내려가므로 고정함. 최소 25, IL2CPP + ARM64, 16KB 페이지는 Unity 6000.3 기본 지원, 런처 액티비티 `exported="true"`, 권한·서드파티 SDK 없음.
- 화면 **세로 고정**(자동 회전 끔). API 36에선 큰 화면(sw ≥ 600dp)에서 방향 고정이 무시되지만 Stage 구조로 잘리지 않음.
- **업로드 키 (10-30 생성, LKH PC):** Unity Keystore Manager로 만든 키(**지금 경로 `C:/Users/user/Desktop/keystore/user.keystore`** — 10-30 기록의 `.../key/`에서 옮김), Alias `moremustmush`, RSA 2048, 유효 2076-10-17까지, 인증서 `CN=soomyouth, O=soomyouth, C=KR`, SHA-256 `F5:27:28:D8:67:19:C2:3F:A4:B0:8A:9B:9D:5A:B0:5D:FA:3A:65:3B:FA:B4:C2:BF:B6:D8:A3:96:58:94:31:10`. 인증서 서명이 SHA1withRSA(Unity 기본, keytool이 weak 표시)지만 업로드 키로는 문제없음(Play App Signing이 앱 서명 키로 다시 서명).
  - **키 파일·비밀번호는 저장소에 없음**(`.gitignore`로 막음). 잃어버리면 Play Console에서 업로드 키 재설정 요청 필요 → 키 파일과 비밀번호 두 개는 LKH가 따로 백업.
  - **키 설정(Custom Keystore 켬·경로·Alias)은 11-06·11-10에 커밋됨**(10-30엔 커밋하지 않기로 했었음). LKH PC 바탕화면 경로라 다른 PC에서 빌드하면 Publishing Settings에서 그 PC 경로로 바꾸고, 그 변경은 커밋하지 말 것. 비밀번호는 Unity가 파일에 안 남김 → 에디터를 열 때마다 다시 입력.
- **AAB 빌드 (10-30):** `Builds/Android/moremustmush-0.1.0-1.aab` 46.1MB(네이티브 25.6 · assets 15.6 · dex 2.2MB), IL2CPP 첫 빌드 약 4분 30초. `Builds/`·`*.aab`는 git 제외. 같은 폴더의 `*_BurstDebugInformation_DoNotShip`은 올리지 말 것. **이후 빌드:** `moremustmush-0.1.0-2.aab`(11-06, versionCode 2, 빌드 6분 16초 — `build_result_0.1.0-2.txt`) · `MoreMustMush_0.1.0_3.aab`(11-10 16:52, 67MB, versionCode 3) · **`MoreMustMush_0.1.0_4.aab`·`.apk`(11-11, versionCode 4, `5ee13ef` — 2장 "11-11 출시 빌드")**.
  - 확인한 것(빌드된 매니페스트·번들): targetSdk 36 / compileSdk 36 / minSdk 25, `com.soomyouth.moremustmush` 0.1.0(versionCode 1), portrait, 런처 `exported="true"`(+ TEST_LOOP 필터), arm64-v8a만, 네이티브 라이브러리 6개 모두 **16KB 정렬**(0x4000), 권한 INTERNET뿐, 업로드 키 서명.
  - 빌드 방법: Build Profiles에서 Android · "Build App Bundle (Google Play)" 켜고 Build. 또는 `BuildPipeline.BuildPlayer`(MCP로는 `EditorApplication.delayCall` 안에서 돌려야 시간 초과로 끊기지 않음).
- **스토어 업로드:** Play Console에는 **AAB만** 올린다(키 파일은 올리지 않음). 새 앱은 Play App Signing 기본 사용 → 첫 AAB의 키가 업로드 키로 등록. **업데이트마다 versionCode +1**(지금 4 · 버전 이름 0.4.0, 3은 올라감), 같은 업로드 키로 서명. 처음엔 "내부 테스트" 트랙 권장.
- **출시 안정화·최적화 (2026-11-06, KHD):**
  - 광고: 콜백을 `MobileAdsEventExecutor.ExecuteInUpdate`로 메인 스레드에 넘김(폐기 예정 `RaiseAdEventsOnUnityMainThread` 대신). 광고 단위 ID는 **모든 안드로이드 빌드에서 실제 ID**(`ca-app-pub-3609467797467871/2013896497`) — 테스트 기기는 AdMob 콘솔에 등록해 실제 광고를 누르지 않게. 광고를 보느라 앱이 잠깐 뒤로 간 것은 앱 밖 보상·정산 창으로 치지 않음(`AdService.IsShowing`).
  - 저장: `SaveScheduler` 주기 5→15초(전투가 늘 dirty라 사실상 주기마다 파일 전체 쓰기). 뒤로가기 종료 때 `SaveScheduler.Flush`. `IdleProgress.Stage`는 메모리에 둠.
  - 성능: 홈 HUD 체력 글자 0.2초마다, 세계수 적 그림(`EnemySprite`) 캐시.
  - 기타: 정산 표시 long 넘침 방지, `link.xml`에 `BossRow`·`TreeQuestRow`. 이 PC 빌드 설정의 네이티브 디버그 심볼 = Symbol Table(번들 포함, Play Console 크래시 해석용 — `Library`라 커밋 안 됨, 빌드 PC마다 켤 것).
  - **11-06 추가 해결:**
    - ① 시계 조작: `GameClock` — 지금까지 본 가장 늦은 기기 시각보다 뒤로 가지 않는 게임 시계(`Clock.MaxSeen`). 던전·뽑기·일일 퀘스트·광고 통계·세계수(의뢰·제작·급속)·앱 밖 보상이 모두 이것을 쓴다. 시계를 앞으로 돌렸다 되돌려 반복 수령하는 것을 막는다. 앞으로만 돌리기(하루 일찍 넘기기)는 서버 시간 없이는 못 막음. 게임 규칙의 "지금"에 `DateTime.UtcNow`를 새로 쓰지 말 것.
    - ② 광고 동의(UMP): **Start 씬 로딩(`StartLoading`)에서** `AdService.Initialize` → 동의 정보 확인 → 처음 실행한 동의 지역 사용자면 로딩 화면 위로 동의 양식, 답할 때까지 Main으로 넘어가지 않음(확인 자체는 최대 6초, `consentCheckTimeout`) → 광고 SDK 시작. 동의 지역이 아니거나 이미 답한 사용자에겐 아무것도 안 뜬다(Google 규칙). 에디터는 NotRequired로 바로 통과 — EEA 양식은 실기기에서 확인 필요. **양식 자체는 AdMob 콘솔 › 개인 정보 보호 및 메시지 › 유럽 규정(GDPR) 메시지에서 만들고 게시해야 뜬다**(안 만들면 양식 없이 광고만 시작, EEA에선 광고 제한). 동의를 바꾸는 입구: 설정 창 Notice 줄의 "광고 개인정보 설정" 링크(`AdPrivacyLink`, 동의 지역 사용자에게만 보임).
    - ④ 전투 오류: 예외 점수 30에 닿으면 멈추고 10초 → 30초 → 1분 뒤 정리(적 거두기·체력 채우기·1웨이브) 후 재시도. 세 번 다 실패하면 "문제가 생겼어요" 안내 → 확인 시 저장 후 Start 씬부터 다시. 2분 탈 없이 돌면 재시도 횟수 초기화. 로그: 같은 예외는 처음 한 번만 스택과 함께, 나머지는 개수만(재시도·초기화 때 한 줄 요약).
    - ⑤ 세계수 세이브: 저장할 때마다 `persistentDataPath/SaveBackup/worldtree.json`에 사본(`LocalBackup`, 임시 파일 → 복사). 읽을 때 비었거나 깨졌으면 사본에서 되살리고 깨진 원문은 `worldtree.corrupt.json`에 남긴다. 게임 기록 전체 초기화는 사본도 지운다. **다른 값(골드·스테이지·강화 등 PlayerPrefs 개별 키)은 백업 대상 아님.**
  - **11-06 나머지 안정화:**
    - 광고 보상 누락: 광고를 끝까지 봤는데 그 순간 줄 수 없으면(창이 닫힘·씬 로딩 중) `AdCredits`에 "광고 없이 한 번"을 남겨 다음 같은 버튼에서 바로 쓴다 (뽑기 `Gacha.<종류>`, 던전 `Dungeon.<번호>`).
    - 표 읽기: `GameBalance.Preload`를 Start 씬 로딩에서 — 홈이 뜰 때 멈칫(PC 약 65ms, 저사양 0.2~0.4초 추정)이 로딩 화면으로 옮겨감. 읽기 실패 시 로딩 화면에 "게임 데이터를 읽지 못했어요" 안내 후 멈춤(깨진 채 홈으로 가지 않음). xlsx를 미리 변환하는 형식 변경은 하지 않음(밸런스 시뮬 등 도구가 같은 bytes를 xlsx로 읽음).
    - 성능: 강화 목록은 버튼을 누르고 있어도 0.1초마다 한 번만 다시 그림, 글자 크기·자리는 바뀔 때만 씀. 세계수 성장 화면 목록은 1초마다가 아니라 값이 바뀔 때만 다시 만듦.
    - Android 16: Unity가 만든 매니페스트에 이미 `enableOnBackInvokedCallback="true"`(Unity가 새 뒤로가기를 받아 전달)와 `appCategory="game"`(대화면 방향 무시 규칙에서 게임 제외)가 있어 코드 변경 없음 — **실기기에서 뒤로가기·태블릿 확인만 남음**.
    - 앱 밖 시각을 Main에서만 갱신하는 것은 설계대로(세계수·던전 씬에선 홈 전투가 안 돌므로 그 시간을 홈 복귀 때 앱 밖 보상으로 채움) — 변경 없음.
    - 남긴 것: 세계수 외 저장값 백업·클라우드 저장(폰 교체 시 이어하기), 홈 60fps(배터리) 조정, 버튼 눌림 스캐너 0.5초 검색.
    - 에디터 플레이 시작 때 "The referenced script (Unknown) on this Behaviour is missing!" 오류가 여러 개 — 출처가 `SerializableManagedRef`(URP 전역 설정 등 SerializeReference 에셋)로 우리 스크립트·프리팹(45개 검사)은 아님. 빌드 영향은 미확인.
- **전수 점검 반영 (2026-11-06, 영역 4개 병렬 점검 → 직접 확인 후 수정):**
  - 공통: 뽑기 결과 등급 테두리 그림이 5등급 배열이라 한 칸씩 밀려 있던 것(희귀=초록 등) 4등급으로. 강화 꾹 누르기를 떼면 1레벨 더 오르던 것(되풀이가 있었으면 클릭 무시). 절전 덮개 아래에서 뒤로가기가 탭·종료 창을 열던 것. 던전 보상 지급 뒤 바로 저장. 광고 보는 사이 00시가 지나면 광고 입장이 사라지던 것(그날 입장 횟수로 대신, 안 되면 크레딧). 뽑기 화면이 1초마다 전체 갱신하던 것(지갑 변화엔 버튼만). 씬 열기 실패 때 탭 예약 지움. 옛 세이브 스킬 가격 합 넘침.
  - 던전(InGame): 재도전으로 같은 씬을 다시 열 때 표 값 복사본 보관함이 옛 씬에 남아 함께 지워질 수 있던 것(보관함을 쓰는 씬에 둠). 판 도중 앱이 뒤로 가면 옵션 창을 열어 멈춤. 결과 처리 중 예외가 나도 결과 창은 뜨게. 보스 회복 int 넘침. 버섯 최소 크기 표 값이 1보다 크면 판 시작 NRE. 콤보 글자 문자열 할당 없앰.
  - 세계수: 버튼마다 PlayerPrefs.Save + 백업 파일 쓰기 → PlayerPrefs에만 넣고 파일 쓰기는 SaveScheduler(15초·앱 뒤로 갈 때)에. 꼬마·의뢰·성장 창이 1초마다 전체 다시 쓰던 것 → 값이 바뀔 때 + 5초(의뢰 남은 시간 글자만 1초). 처치마다 군락 조각 이름을 자르던 것 캐시.
  - 에셋·설정: 정적 폰트 아틀라스(LINESeedKR-Bd/Rg SDF, 1024×4장씩) Read/Write 끔 — RAM 약 8MB. 물리 자동 시뮬레이션 끔(3D·2D 모두 Script, 리지드바디·콜라이더 없음).
  - **던전 필드 캔버스 분리·할당 줄이기 (11-06):** InGame 씬은 캔버스가 하나뿐이라 수확기가 움직이는 매 프레임 HUD까지 다시 묶였다 → `Field`(입력 받음, GraphicRaycaster)·`Field/Mushrooms`·`Field/Harvesters`에 하위 캔버스를 씬에 붙임(정렬 덮어쓰기 없음 — 그리는 순서·RectMask2D 잘림 그대로, 입력 순서도 HUD 버튼 → 필드로 확인). 홈 전투는 이미 `IdleBattle.AddSubCanvas`로 분리돼 있음. 할당: `field.Mushrooms/Harvesters/Colonies`(IReadOnlyList) foreach → for(분열·번개·소용돌이·보스 회복), 분신 바 자리 배열·군락 리스폰 격자 목록 재사용.
  - **패키지·렌더 설정 (11-06, 안전한 것만):** 안 쓰는 패키지 4개 제거(Visual Scripting·AI Navigation·Timeline·Multiplayer Center — 코드·씬·에셋 사용처 없음, 다른 패키지 의존 없음) → Android 스크립트 어셈블리 31→24. 기본 렌더 파이프라인(GraphicsSettings)을 `PC_RPAsset` → `Mobile_RPAsset` — Android는 원래 품질 레벨 Mobile(PC 레벨은 Android 제외)이라 화면은 그대로, 빌드에 PC용 셰이더 변형이 안 들어간다. 에디터 Main 플레이 오류 0.
  - **위험이 있어 안 한 것 (사용자 결정 11-06: 위험 가능성 있는 건 하지 않음):** 아래 동적 폰트 정적화(글자 빠짐), 스프라이트 아틀라스(압축·묶음 바뀜), Resources 안 쓰는 그림 삭제(표가 이름으로 읽을 수 있음), 9-slice(모양 바뀜), R8(ProjectSettings.asset — 빌드하는 쪽에서).
  - **남긴 것(결정·큰 작업):** ~~동적 폰트 `LINESeedKR-Bd SDF Wide` 정적화~~(11-06 `bded102`에서 정리, 지금 폰트는 Bd·Rg 정적 2개뿐) · ~~기본 렌더 파이프라인 PC_RPAsset~~·~~안 쓰는 패키지~~(위 줄에서 해결) · 스프라이트 아틀라스 없음(UI 270장 개별) · R8 Minify 꺼짐 · Resources 안 안 쓰는 배경·아이콘 약 40개 · 씬 전용 배경이 Resources에 있음 · `UI_PP_Under_Base/Side` 큰 단색 그림(9-slice로).
- **남은 것:** 0.3 출시 빌드 체크리스트로 옮김(Play Console·개인정보처리방침·데이터 보안 양식·실기기).
- **리소스 정리(`b7e2863`):** 빌드 씬 + Resources 의존성 기준으로 안 쓰는 것 삭제, 텍스처는 보이는 크기에 맞춤(그림 위주 ASTC 6×6, 꼬마 아이콘 256, 할아버지 보스 512(11-05 삭제), 로고 1024). 일부러 남긴 것: `CollectPopup`(11-05 삭제)·`ConfirmPopup`, `MainTable.bytes`·엑셀, `link.xml`, TMP 셰이더.

---

## 9. 남은 일 (2026-11-11 정리)

> 11-11에 날짜별 "남은 일" 목록을 하나로 합쳤다. 끝난 것은 빼고, 남은 것은 아래 9.1~9.4로 옮겼다. 원래 목록은 9.9(기록)에 그대로 있다.

### 9.1 출시 전 — 0.3 체크리스트

빌드·실기기·Play Console·AdMob 할 일은 **0.3**에 모았다. 코드 쪽에서 출시를 막는 것은 없음(11-11 기준).

### 9.2 출시 뒤 바로 (밸런스·데이터)

1. **밸런스 다시 재기:** 11-10에 들어온 보상원 — **도감 세트**(보스 처치 드롭·세트 %), **주간 퀘스트**(골드·균사석·세계수 경험치), **룰렛**(하루 3회) — 이 시뮬(`밸런스설계/test/sim`)에 없다. KHD `f6dcef5`(11-10) 측정이 지금 규칙의 마지막 기준. 셋이 겹치면 초중반(D3~D14)이 빨라질 수 있음. 도감은 `Player.cs`에 도감 %(공격·체력·재생은 장비%에 더하고 속도는 곱함)와 보스 드롭을 넣고 보통·무광고로 재기(idle-test). 빠르면 `도감` 시트 `dropChance`·`unlockStage`나 ★3·★4 값을 낮춘다(`밸런스설계/도감세트.md` 마지막 절).
   - 먼저 고칠 것: 시뮬 csproj의 게임 dll 경로가 옛 scratchpad(`b_cur`)를 가리켜 그대로는 빌드 실패 → `밸런스설계/test/build/mkproj.py`로 다시 만들거나 `Library/ScriptAssemblies`를 가리키게.
   - 지운 상수를 아직 읽는 스크립트: `adBoxMyceliteBosses`(→ `adBoxMycelite` 90) — `test/sim/Driver.cs`·`theory/progress.py` / `dungeonSweepMul`(소탕 삭제) — `theory/run_pet.py`·`세계수/theory/idle_feed.py`·`세계수/verify/feed.py` / `adGachaPulls`(→ 뽑기상품 광고 행 `pullCount`) — `test/make_proposal_xlsx.py`·`theory/proposal.py`·`verify/proposal_port.py`. KHD `run_v3d.py`는 700행 표에서 `KeyError 711`.
   - 이론 모델(`밸런스설계/theory/`)은 11-06 던전 개편(해금 5/10/20/30/50, 던전 2·3 보상 성격, 4·5 새 방식) 전 기준.
2. **주간 퀘스트 골드·균사석 확정:** 지금 임시값(주간 균사석 합 250 = 일일 한 주 2,625의 약 10%). 시뮬에 퀘스트 보상을 넣을지 idle-test로 결정.
3. **세계수 경험치 재검증(선택):** 의뢰 → 메인 퀘스트로 합쳤고 경험치 합은 같지만 메인 일일 목표가 더 쉽다(처치 300 vs 4,000, 보스 3 vs 30) → 일부만 하는 사람은 8단계가 빨라짐. 필요하면 worldtree-verify.

### 9.3 결정 필요 (사용자·기획)

1. **꼬마별 트리** ([작업준비_261106.md](작업준비_261106.md) 2장) — 손대지 않음. Q2(효과를 팀 전체에 줄지 그 꼬마에만 줄지)·Q3(비용 재화)부터.
2. **세계수 U1** 전투 효과 크기(지금 공격·체력 +2% 수준, 3.3) · **U5** 8단계(약 40일) 뒤 세계수 경험치 쓸 곳.
3. **도감:** 이미 모은 버섯이 다시 나올 때 보상 없음 · 앱 밖 시간엔 드롭 없음 · 버섯 **이름** 표(정해지면 `도감` 시트에 열 추가) · 황금 버섯 토글은 모양만.
4. 스테이지 `enemyIds`를 게임이 읽게 할지(지금은 `IdleBattle`이 종류 비율로 적 그림을 고름).
5. `homeTabs`가 Main 씬에서 비어 있어 전투가 늘 '홈'으로 동작(탭을 열어도 숨기지 않음) · InGame에 있던 시간이 앱 밖 시간으로 세어짐(던전 다녀오면 방치 보상) — 둘 다 의도인지.
6. `Assets/Scenes/NewInGame.unity`(kms 작업 씬, 빌드에 없음) 둘지 지울지.
7. 세계수 화면에 "세계수 경험치는 퀘스트에서" 안내가 없음 — 필요하면 성장 창 문구나 퀘스트로 가는 버튼.
8. ~~**독성·약용 버섯이 세계수 창고에 들어가는가**~~ → **11-12 결정: 들어간다(코드 그대로).** 상수 9079 `enemyToxicRate` 설명을 코드에 맞게 고침. 세계수 모델 결과(8단계 약 36~39일)가 이 기준이다.
9. **세계수 처치 골드 효과 크기:** 최대 합 0.47(가호·꼬마 특기·장식) → 하루 골드 약 +15~25%. 보따리 삭제 때 옮겨 온 효과라 기준이 없음. 받아들일지 줄일지(검증 C8-1: test 시뮬로 스테이지 앞당김 ≤ 3일인지 먼저 잴 것).
10. **꼬마 ★3 조건:** 레벨 12 = Formation 성장 2번 필요. 성장 안 하면 ★3 없음 — 의도인지(이론 U-C: needLevel 10이면 약 43일).

### 9.4 정리·작은 일

1. **효과음** + SFX 볼륨 적용(지금 BGM만).
2. 개발자 모드: 실기기에서 여는 입구(지금 `` ` `` 키뿐), 도감 채우기 버튼(`MushroomDex.DevSetGroup`은 있음).
3. String.xlsx `treequest.*` 문구는 `treequest.in_progress`만 씀 — 나머지 정리 가능. `guide.claimed`도 11-11 `5ee13ef`부터 안 씀.
4. 옛 세이브 옮기기 코드(`Wallet` 테스트 균사석 회수·`Formation`/`Equipment` MigratedLevel·`Gacha.<종류>.AdDay`) — 출시 전 데이터만 대상이라 첫 출시 뒤 지워도 됨. 옛 세계수 의뢰 진행(`WorldTree.Save`의 quest* 필드)은 이미 안 읽음.
5. 세계수 외 저장값 백업·클라우드 저장(폰 교체 시 이어하기), 홈 60fps(배터리), 스프라이트 아틀라스(위험해서 11-06 보류).
6. 행 없는 Legacy 표 `Mushroom`·`Harvester`(게임이 읽기만 함), `밸런스설계/test/out/` 결과물 정리.
7. 던전 1(수확) 판: 스킬 구매 UI가 없어 레벨 5부터 한 판(15초)에 버섯 1~3개(11-03 실측) — 판 보상·재미 확인. 약 1% 판에서 버섯 2~8개만 캐는 끼임 버그 추정.
8. 커밋에서 뺄 것(로컬에서 자주 바뀜): `ProjectSettings/GvhProjectSettings.xml`·`ExternalDependencyManagerResolverSettings.asset`, GoogleMobileAds placeholder 프리팹(`768x1024.prefab`), `밸런스설계/test/sim/bin`·`obj`(이미 `.gitignore`).

### 9.9 지난 목록 (기록 — 11-11에 9.1~9.4로 합침)

#### 2026-11-09 남은 일 (가장 먼저)

1. ~~KHD 미머지 2커밋 머지~~ ✅ 11-10 KHD가 main에 머지(`c58cbcf`). Main/세계수/뽑기/출석 플레이 확인은 남음
2. ~~로컬 main 푸시~~ ✅ 11-10
3. **꼬마별 트리** ([작업준비_261106.md](작업준비_261106.md) 2장) — 손대지 않음. Q2(효과를 팀 전체에 줄지 그 꼬마에만 줄지)·Q3(비용 재화) 결정부터
4. (11-10 KHD `f6dcef5`가 시뮬을 지금 규칙(던전 보상 함수·광고 상자 한 가지·출석·꼬마 성장·가이드)에 맞추고 다시 잼 — 결과 `밸런스설계/test/out/khd1110`. **도감 세트·주간 퀘스트·룰렛은 아직 시뮬에 없음**, 이론 모델 `theory/`는 확인 안 함) **밸런스 모델이 지금 던전과 다름:** 해금 5/25/150/250/400 → 5/10/20/30/50, 던전 2·3 보상 성격 변경(골드·균사석), 4·5 새 방식(보스 러시·토벌), 광고 상자 한 가지 보상(기대값 절반), 출석 균사석 6,000·전설 무기. `밸런스설계/` 시뮬·이론은 이 변화 전 기준 — 11-05 남은 일 1번(지운 상수)과 함께 고쳐 다시 재기
5. ~~**번역 누락**~~ ✅ 11-11 점검·수정 (2장 "2026-11-11 번역 누락 점검"). 남은 것: 영어 화면 실기기 확인(글자 넘침)
6. **실기기 확인:** 해금 팝업·잠긴 탭, 광고 상자 배회·실제 광고·UMP 동의, 던전 2~5, 영어 화면, 확인 창 새 디자인
7. **스토어:** `moremustmush-0.1.0-2.aab`(버전 코드 2) Play Console 내부 테스트 업로드, 개인정보처리방침·데이터 보안 양식 (8장 "출시 준비")
8. 1장 던전·광고 상자 줄 아래쪽 설명에 옛 값(해금 5/25/150/250/400, 골드+균사석 둘 다)이 남아 있음 — 새 줄 기준으로 읽을 것

#### 2026-11-10 남은 일 (공통)

1. **오늘 들어온 보상원을 시뮬에 함께 넣어 다시 재기:** 도감 세트(아래), 주간 퀘스트 보상, 룰렛(하루 3회 골드·균사석) — KHD `f6dcef5` 측정에는 셋 다 없다. 셋이 겹치면 초중반이 빨라질 수 있음
2. **플레이 확인:** KHD 머지 뒤 Main 왼쪽 버튼 열(룰렛·도감·퀘스트)·가이드 바·전투 영역 위치, 옵션 창 데이터 초기화(사용자 세이브 말고 테스트 세이브로), 실기기 터치
3. 1장 아래쪽 옛 설명(광고 상자 "골드와 균사석", 던전 해금 5/25/150/250/400 등)은 새 줄 기준으로 읽을 것
4. **시뮬 csproj 게임 dll 경로**(`IdleSim.csproj`의 `b_cur` scratchpad 경로)가 낡아 그대로는 빌드 실패 — `밸런스설계/test/build/mkproj.py`로 다시 만들거나 `Library/ScriptAssemblies`를 가리키게 고칠 것
5. 버섯 이름 표(도감 시트 열)는 이름이 정해지면 추가

#### 2026-11-10 남은 일 (퀘스트 합침)

1. **주간 퀘스트 골드·균사석 확정:** 지금 임시값(주간 균사석 합 250 = 일일 한 주 2,625의 약 10%). 시뮬(`밸런스설계/test/sim`)에는 퀘스트 보상이 없으니 넣을지 idle-test로 결정
2. **세계수 경험치 재검증(선택):** 합은 예전 의뢰와 같지만 메인 일일 목표가 의뢰보다 쉽다(처치 300 vs 4,000, 보스 3 vs 30) — 다 하는 사람 기준 8단계 약 40일은 그대로, 일부만 하는 사람은 빨라짐. 필요하면 worldtree-verify
3. 세계수 화면에 "세계수 경험치는 퀘스트에서" 안내가 없다 — 성장 창 문구나 홈 퀘스트로 가는 버튼이 필요하면 추가
4. 옛 세계수 의뢰에서 다 채우고 안 받은 보상은 옮기지 않았다(사라짐). 출시 전 데이터라 그대로 둠
5. String.xlsx의 `treequest.*` 문구는 `treequest.in_progress`(꼬마 부탁 칸)만 쓰고 나머지는 안 쓴다 — 정리해도 됨
6. 실기기: 주간 탭 버튼 터치·보상 둘째 줄 글자 크기(작음) 확인

#### 2026-11-10 남은 일 (도감 세트 보너스)

1. **진행 영향 실측:** 시뮬(`밸런스설계/test/sim/Player.cs`)에 도감 % 합(공격·체력·재생은 장비%에 더하고 속도는 곱함)과 보스 처치 드롭을 넣고 보통·무광고를 다시 잰다 (idle-test). 예상: D3~D14 구간이 빨라지고 끝 지점 DPS 약 ×2.6 (`밸런스설계/도감세트.md` 마지막 절). 빠르면 표의 `dropChance`·`unlockStage`나 ★3·★4 값을 낮춘다
2. 버섯 **이름**이 없다 — 칸 이름은 "식용 희귀 3" 임시. 이름 표가 생기면 도감 시트에 열을 더한다
3. 이미 모은 버섯은 다시 안 나와서 겹침 보상이 없다 / 앱 밖 시간엔 드롭이 없다 — 필요하면 기획 결정
4. 황금 버섯(황금 토글)은 그림·기록이 없어 그대로 모양만
5. 개발자 모드에 도감 채우기 버튼이 없다 (`MushroomDex.DevSetGroup`은 있음)

#### 2026-11-05 남은 일

1. **밸런스 시뮬·이론 모델 상수 고치기:** `밸런스설계/test/sim/Driver.cs`·`밸런스설계/theory/progress.py`가 지운 상수 `adBoxMyceliteBosses`를 아직 읽음 → `adBoxMycelite`(90, 스테이지와 무관한 고정값)로 바꿔야 다음 실측·이론이 돈다. 게임은 원래 90을 줬고 모델은 "보스 균사석 × 5 = 10"으로 가정했었음(`HANDOFF_BALANCE_V3.md` 참고). 지운 `dungeonSweepMul`을 읽는 `theory/run_pet.py`·`세계수/theory/idle_feed.py`·`세계수/verify/feed.py`도 소탕 계산을 빼야 함
2. 스테이지 `enemyIds`는 아직 게임이 안 읽음(적 그림은 `IdleBattle`이 종류 비율로 뽑음) — 읽게 할지 결정
3. 판단 보류: `Assets/Scenes/NewInGame.unity`(kms 작업 씬). (`Resources/Art/Dream/Index` 도감 그림은 11-10부터 도감 시트가 씀)
4. **개발자 모드:** 실기기에서 여는 입구(지금은 `` ` `` 키뿐), ~~출시 빌드에 개발자 모드가 없는지 확인~~ ✅ 11-06 `bae4ea3` 컴파일 제외 ([HANDOFF_DEVMODE.md](HANDOFF_DEVMODE.md))
5. ~~표 `info` 마무리(보스 예시값·개편 기록 2행)가 미커밋.~~ (`c6d8675`로 커밋됨) **커밋에서 뺄 것:** `ProjectSettings/GvhProjectSettings.xml`, GoogleMobileAds placeholder 프리팹(`768x1024.prefab`)
6. 확인 못 한 것: 새 보스 10종·꼬마/버섯 크기·세계수 나무 크기·BGM을 실기기에서

#### 2026-11-04 미완료 (11-05 모두 커밋됨 — 기록용)

**A. 펫 삭제 커밋 — ✅ 커밋됨 (`08bb82b`, 문서 `9caca14`)** — 사용자 지시 "펫 기능 없애고 편성 UI는 위 3·아래 2". 끝난 것:
- 코드: `GachaKind`에서 `Pet` 제거(**`Character = 3` 고정** — 씬·프리팹에 숫자로 저장돼 있어 바꾸면 꼬마 뽑기 칸이 다른 종류가 됨), `Equipment.All = {Weapon, Armor}`, `IdleProgress.AttackMultiplier = 1 + 무기%/100`(펫% 빠짐), `FormationPanel` 펫 칸 코드 삭제, `GachaPanel` 제목, 검증기 장비 종류, 주석 정리(`EquipmentPanel`·`PopupLayer`·`TabGroup`·`GameBalance`).
- 프리팹·씬: `GGoma.prefab` 펫 칸 삭제·아래 줄 x −200/+200, `GachaUI.prefab` 펫 뽑기 칸 삭제·꼬마 칸 위로·높이 −428, `Main.unity` `Equit/Pet` 삭제·`Equit` TabGroup 탭/닫기 버튼 정리(무기·방어구 2개), `Pet.prefab` 삭제. 누락 참조 0.
- 표: 장비 펫 12행·뽑기상품 펫 3행(12007~12009, 꼬마 12010~12012 번호는 그대로) 삭제, 데이터 검증 목록에서 Pet 제거, info·상수 설명, 개편 기록. 검증기 0건(1155행).
- 확인: Unity 컴파일 오류 0, 편성 칸 배치는 프리팹 렌더로 확인. **플레이 모드 확인 전**(편성·장비 탭·뽑기 화면).
- **커밋할 때 뺄 것:** ~~`Assets/Fonts/LINESeedKR-*.asset`(커밋 금지 규칙)~~ — 11-05 정적 아틀라스로 바뀌어 폐기(폰트 에셋은 이제 커밋), `Assets/GoogleMobileAds/.../768x1024.prefab`, `ProjectSettings/GvhProjectSettings.xml`(내용 변화 없음), `밸런스설계/test/sim/bin`·`obj` 빌드 결과물.

**B. 펫 삭제 밸런스 보정 — ✅ 11-05 적용·커밋 (`d29c186`)** · verify#20 조건부 통과(`밸런스설계/검증.md` P절), 사용자가 60분 허들 s180→s250 수용 → ①+② 적용: 상수 9081 `weaponEffectMul` 1.75, `Equipment.BaseOwnPercent` 무기만 곱함, 검증기 상수, 스테이지 421~700 체력(`밸런스설계/tools/apply_pet_fix.py`). Unity 검증기 1156행 OK·컴파일 오류 0. 남은 것: (선택) test가 적용본으로 재실측, 플레이 모드에서 장비 화면 수치 확인. 아래는 경과 기록
- **#18 test 실측 끝** (`밸런스설계/테스트.md` §13, `test/out/summary_pet.txt`): 시뮬 빌드 복구(`IdsOfGrade` 제거, 장비·뽑기 종류를 dll에서 읽어 펫 있/없 dll 모두 돎). 기준선 `7b17e68` 대비 보통(광고 절반) D1/D3/D7/D30 = 130/250/360/590 → 120/240/360/500, 120일 끝 750 → 590(10/10 시드가 s590 보스에서 20일↑ 막힘). 원인: 공격 배율 끝 26394% → 6388%(후반 펫%가 무기%의 3배), 첫날 광고 뽑기 2종이 무기+펫 → 무기+방어구.
- **#19 theory 보정안 끝** (`밸런스설계/이론.md` 13절, `theory/pet_sim/`): ① 필수 — 상수 **9081 `weaponEffectMul` = 1.75**, `Equipment.cs` `Config`에 `weaponMul` 추가 후 `BaseOwnPercent` 끝에 `* (Kind == GachaKind.Weapon ? Cfg.weaponMul : 1.0)`(이 한 곳만; `AttackMultiplier`는 고치지 않음), 검증기 `RequiredConsts`에 `("weaponEffectMul", 0.001, false)`. ② 선택(권장) — 스테이지 421~700 `enemyHp`(T열)·`bossHp`(U열) × k(420:1 → 600:0.55 → 700:0.35 로그 보간, 값 `theory/pet_hp_late2.csv`, 사본 생성 `python 밸런스설계/theory/pet_table.py <출력.xlsx> "[[420,1.0],[600,0.55],[700,0.35]]"`). 예측(보통): D1/D3/D7/D30 = 134/260/364/590, 첫 1시간 5분↑ 대기 2회(기준선과 같음), 120일 끝 790.
- **#20 verify 판정 — 11-05 재실행, 조건부 통과.** (이하 11-04 중단 당시 기록) 남은 것은 스크립트 초안뿐(`verify/pet_analyze.py`·`pet_grade8.py`·`pet_table_v.py`), 검증.md 결론 없음. **다시 시작**: verify 에이전트(`.claude/agents/idle-verify.md`)를 manager#20 그대로 다시 띄운다 — 할 일: 따로 만든 dll(무기 ×1.75)·표 사본으로 test 시뮬 재실측해 theory 예측 재현 여부, `weaponEffectMul`이 `OwnPercent`·`EquipPercent`·`GachaProgress.OwnPercent`·`AttackMultiplier`·장비 화면·`CombatPower`에 한 번씩만 들어가는지, 기준 1·8·D11, 표 체력 단조 증가, 민감도(무기만 뽑는 정책 → 과보정 가능성, 배율 1.5/1.75/2, 후반 체력 있/없).
- **판정 때 정할 것:** (a) 60분 허들 시작이 기준선 s180 → s250(+39%)으로 목표 ±10% 밖 — theory는 "첫날 뒤 벽이 조금 늦어질 뿐"이라 그대로 두자고 함. 받아들일지 s170~180 체력 보정을 따로 잴지, (b) 무기만 뽑는 플레이어에게 ×1.75가 과보정인지.
- **통과하면:** 관리자가 위 ①(②)를 표(두 파일 `MainTable.xlsx`·`MainTable.bytes` 동일하게)·코드에 적용 → Unity 검증기·컴파일 → test가 적용본으로 다시 재서 예측과 비교 → 사용자 보고 → 커밋.
- 기존 위반(펫 있을 때도): 첫 1시간 5분↑ 대기 s58~65(기준 6) — 이번 보정 범위 밖, theory#16(v4) 몫.

**C. 확인 못 한 것**
- 몰입 모드(`ImmersiveMode`)·방어구 그림 10장·편성 3+2 배치의 실기기/플레이 모드 확인.
- 시뮬 결과물 정리: `밸런스설계/test/out/snap_pet_*`·`stage_pet_*`·`out/pet/`, `test/sim/bin/base/`(기준선 dll) 등 새 파일이 많다 — 커밋할지 `.gitignore`할지 결정. → 11-05: `test/sim/bin`·`obj`는 git 추적에서 빼고 `.gitignore`에 넣음(파일은 디스크에 남음). `test/out/` 결과물은 그대로


#### 세계수·표 (2026-11-03 기준)
1. **U1 결정 (사용자):** 세계수 전투 효과를 지금처럼 +2%로 둘지, KHD 방치 곡선에 세계수 효과를 넣고 키울지 (3.3). 정하면 `밸런스설계/세계수/` 흐름(이론 → 적용 → 검증)으로 다시 돌림
0. **KHD v3 머지 뒤 확인 (11-03):** Unity에서 `Reimport Tables`로 검증기 통과 확인, 컴파일 확인. (세계수 재검증은 11-03 끝 — v4 통과, 3.3)
2. **KHD 쪽 정리:** 시뮬레이터 빌드 결과물(`밸런스설계/test/sim/bin·obj`, 약 45MB) git 제외 ✅ 11-05(8장 "정리"). `adGachaPulls`를 지웠으므로(KHD v3의 `밸런스설계/tools/apply_v3.py`도 이 상수를 씀) 아직 이 상수를 읽는 KHD 스크립트 — `밸런스설계/test/sim/Driver.cs`·`test/make_proposal_xlsx.py`·`theory/progress.py`·`theory/proposal.py`·`verify/proposal_port.py` — 는 뽑기상품 시트 광고 행 `pullCount`(지금 4)로 바꿔야 다시 돈다(세계수 쪽 `idle_feed.py`·`feed.py`는 11-03 고침). KHD 재현 명령 `run_v3d.py`는 머지된 700행 표에서 `KeyError 711`로 멈춤(옛 1000행 전제). 49일 뒤 보스 균사석 되먹임 폭주도 KHD 몫
3. **세계수 실기기 확인:** 시트 팝업 스크롤·버튼 터치, 1080 폭에서 잘림, 장식 배치 그림 위치
4. **세계수 아트:** 나무(`Tree01~04`)·장식 24종은 11-05 새 그림. 시트 내용 UI는 여전히 코드로 만든 임시 모양
4-1. **던전 1 수확 판 (KHD 참고, 11-03 실측, 우선):** 스킬 구매 UI가 없어 레벨 5부터 한 판(15초)에 버섯 1~3개만 캔다(레벨 1은 ~11개). 판 보상·재미 확인 필요
4-2. 의뢰: 던전 1을 하다 앱을 끄면 입장 횟수는 쓰였는데 의뢰 판 수엔 안 셈(경미). U5(8단계 뒤 의뢰 보상) 결정
5. 던전 2~5: 씬을 만들면 던전 시트 `scene`·`implemented`만 채우면 입장이 열리고, 수확전투 시트에 그 던전 번호로 행을 넣으면 `HarvestTuning`이 읽음

#### 방치형 (2026-10-30 기준, 우선)
0. **실기기 확인 + 스토어 업로드:** 해상도(노치·20:9)·뒤로가기 키를 실기기에서 확인, 서명된 AAB를 Play Console 내부 테스트에 올리기, 개인정보처리방침·데이터 보안 양식 (8장 "출시 준비" — 서명 키·AAB는 10-30 완료)
1. **던전 다듬기:** 던전 1은 InGame으로 연결, 입장 횟수(하루 2회) 완료. 남은 것: 던전 2~5 내용·해금 조건, 보상 규칙, `DungeonRequest`로 던전 종류·스테이지 넘기기(지금은 늘 기본값)
2. **InGame ↔ Main 이동에 로딩 팝업:** `Benchmark`만 남음 (`InGameOption`·`ResultPopup`은 10-30 완료)
3. **꼬마 강화 다듬기:** 레벨당 +5%는 임시값 → 밸런스 곡선에 맞춰 정하기. 꺼 둔 `Char` 아래 버튼 줄은 지울지 결정 (`Formation` 캐시는 10-30 완료)
4. ~~도감~~ ✅ 11-10 `DexScreen` 수집·세트 보너스 (9장 "2026-11-10 남은 일")
5. 옵션 팝업 기능 연결: 언어·진동·알림·SFX (BGM 음량은 11-05 연결. 게임 종료·도움말·데이터 초기화는 10-30 버튼 제거)
6. ~~세계수를 다시 만들지~~ ✅ 11-02 WorldTree 씬으로 구현(3.3). 스킬 트리를 다시 쓸지(`Backup/SkillTree/`)
7. 무료 뽑기·던전 횟수의 시간 조작 대응(지금은 기기 시계의 UTC 기준), 광고 보상이 다시 필요하면 `GachaPanel` 무료 버튼 자리에
8. ~~Main 배경 `bg_main.png`~~ — 지금 배경은 `BGimage.png`(KHD 11-04), `bg_main`은 11-05 삭제
9. ~~방치형 수치를 `MainTable.xlsx`에서 읽게 전환~~ ✅ 11-02 (6장 6.0). 남은 것: (`hpCoefficient`·`stageCoefficient` 열은 11-05 삭제 — 그 모델로 바꾸려면 열부터 다시) / 밸런스 실측(`밸런스설계/`, `BALANCE_IDLE.md`)

#### 판 플레이 (InGame = 던전 1)
1. **밸런스 다시 재기:** 일반 목표 삭제·해금 조건 변경(10-28) 뒤 50보스 시간·구매 일정을 `BalanceBot`으로 다시 측정 (6.2의 88.3분은 옛 규칙 기준). 90분 목표를 무엇으로 맞출지(판 시간·스테이지 수·보스 실패) 사용자 결정 필요
2. **스킬 트리 배치를 `tier`로:** 사용자가 노드 배치를 바꾸려고 Skill.xlsx에 `tier`를 넣음(10-28). 어떻게 쓸지(티어 = 층? 한 티어 최대 10개라 3칸 한 줄엔 안 들어감)는 사용자와 정할 것. 손댈 곳은 `SkillTreeLayout.Build`의 층 정하기
3. `SkillTreeZoom`: 트리 폭 = 화면 폭이 된 뒤 확대만 됨 — 둘지 지울지 사용자 결정
4. **수확기 추가 노드 3339**가 26,000,000 골드 노드(3341) 뒤에 있음 — 해금 조건이 바뀌었으니 다시 확인
5. **거의 못 캐는 판:** 약 1% 판에서 버섯 2~8개만 캠. 수확기가 어딘가 끼는 버그로 보임
6. 첫 구매가 ×1.5 효과가 되게 (문서 요구, 지금은 점수 +10%)
7. 밸런스·표 생성 스크립트를 저장소 `Tools/`로 (지금은 임시 폴더)
8. 실기기 터치 테스트: 바 조작, Main 탭·팝업, 절전 모드 해제
9. 아트 교체: 등급·황금·버프·장치·보스 (버섯·수확기·링·화살표·배속 아이콘은 11-03 교체, HUD 점수 글자는 11-03 뺌)
10. 보스 연출: 분신·회복 연출 (이름·체력 표시는 10-28 `BossHud`로 끝)
11. 효과음 + SFX 볼륨 적용 (BGM·BGM 볼륨은 11-05 `BgmPlayer`·`BgmVolumeSlider`)
12. ~~WorldTree 시트 내용~~ ✅ 11-02 (3.3)

---

## 10. 날짜별 요약 (기록)

> 11-11 정리 전 문서 맨 위에 있던 날짜별 요약을 그대로 옮겼다(최신이 위). 지금 상태는 0장·1장이 기준이고, 여기 적힌 "커밋 전"·"푸시 안 함"·옛 수치는 그때 기준이다. 여기서 말하는 "9장 '… 남은 일'"은 지금 9.9에 있다.

> **2026-11-10 하루 정리 (main = LKH = `eaba113`, 둘 다 푸시됨):** 순서대로 — LKH 인계 문서(`83780b0`) → LKH **코드 수치 표로**(`34bcc5f`, 출석 시트·상수·리소스 7425) → KHD **밸런스 개편·가이드 퀘스트·꼬마 성장**(`3d2bc82`, 강화 Incremental 곡선·공격 속도 단계 해금·스테이지 71~200 적 2배·벽 보스, 광고 뽑기 35회) → KHD 뽑기 결과창·완료 회색(`61bb3ad`) → KHD **시뮬을 지금 규칙에 맞춤 + 11-10 측정**(`f6dcef5`) → KHD **모바일 최적화·안정성**(`702cf88`·`f0f912e`, 전투 영역 40 내림) → LKH **방치 스테이지 표기 `n-n` 통일**(`1fe2c8d`) → LKH main 머지(`11b63ed`, 상수 9090~ 겹침으로 LKH 상수를 9099~9103으로) → LKH **도감 세트 보너스**(`d3a6597`) → KHD 옵션 창 정리·데이터 초기화·판정 최적화·**룰렛**(`e52a545`·`25384f8`·`59cee33`·`61c9a5b`·`c1ec5a4`·`8e6fb58`) → LKH **세계수 의뢰 → 메인 일일·주간 퀘스트**(`5dc8fa1`) → LKH KHD 머지(`95449fe`) → 도감 버튼 위치·세트 목록 문구(`9eb2de6`·`eaba113`). 아래 요약 3개는 LKH 쪽 상세, 커밋 표는 2장 "2026-11-10", 남은 일은 9장 "2026-11-10 …".

> **2026-11-10 (LKH) KHD 머지 (`8e6fb58`까지):** 룰렛(하루 3회·5분 대기)·옵션 창 정리·데이터 초기화(5초 누르기)·세계수 배경·강화 아이콘 등. 엑셀은 시트 단위로 합침(`밸런스설계/tools/merge_khd_roulette.py`): **ID가 겹쳐 KHD 쪽을 옮김** — 룰렛 18001~18006 → **18201~18206**(도감 18001~과 겹침, 코드 범위도 18201~18299), 상수 rouletteDailyCount·rouletteCooldownMinutes 9099·9100 → **9104·9105**(광고 상자 상수와 겹침). 리소스 7252는 KHD가 `imsi.png`를 `image.png`로 바꿔 `Sprite/BG/image`로 고침. WorldTree 씬은 KHD 판을 쓰고 의뢰 버튼·팝업만 다시 지움. KHD `OptionPopup.prefab` 루트가 켜진 채 저장돼 Start·WorldTree에서 설정 창이 저절로 뜨던 것을 꺼 둠(m_IsActive 한 줄). Reimport Tables 통과, 플레이 모드 확인.

> **2026-11-10 (LKH) 세계수 의뢰 → 메인 퀘스트 + 주간 퀘스트 (커밋 전):** 세계수 화면의 **의뢰(일일·주간)를 없애고** 메인 퀘스트(`DailyQuests`·`QuestScreenView`)로 합쳤다. 퀘스트 시트에 `period`(daily/weekly)·`treeExp`(세계수 경험치) 열과 **주간 4행**(9211 일일 퀘스트 25개 · 9212 보스 도전 350 · 9213 처치 40,000 · 9214 던전 10판)을 더했고, 종류에 `dungeon`(던전 한 판, `HarvestGame`·방치 던전 끝)·`daily`(일일 받은 수, 주간만)를 더했다. 세계수 경험치는 예전 의뢰와 같은 합으로 맞춤: 일일 5행 × 600 = 하루 3,000, 주간 3,500+2,500+2,500+1,500 = 10,000. 주간 골드·균사석(goldKills 10,000/5,000, 균사석 100/50/50/50)은 **임시값**. `의뢰` 시트·상수 `treeQuestDaily`·`WorldTree.Quests.cs`·`TreeQuestPanel.cs`·`QuestPopup` 프리팹·세계수 씬 Quest 버튼 삭제, 지정 버섯(species) 의뢰는 없앰. 주간 초기화 = 월요일 한국 00시(`DungeonTickets.ThisWeek`·`IsNewWeek`·`UntilWeekReset`, 일일과 같은 '뒤의 날만' 규칙). 저장: 일일은 예전 키 그대로(`Quest.Day`·`Quest.<일일 순번>.P/.C`), 주간 `QuestW.Week`·`QuestW.<행 index>.P/.C`. 옛 세계수 의뢰 진행(`WorldTree.Save` JSON의 quest* 필드)은 읽지 않고 다음 저장 때 빠진다 — 받지 않은 의뢰 보상은 사라짐. 퀘스트 창: 하단 주간/일일 버튼이 탭(고른 탭 초록·흰 글자), 제목 메모 일일 의뢰/주간 의뢰, 주간은 "초기화까지 n일 hh:mm:ss", 보상 줄 둘째 줄에 세계수 경험치, 일괄 수령·빨간 점은 일일+주간. 도구 `밸런스설계/tools/merge_tree_quests.py`·`add_weekly_quest_strings.py`. 컴파일·Reimport Tables 경고 없음, 플레이 모드 확인(일일 저장 유지·주간 탭·주간 수령 → 세계수 경험치 +1,500·두 번 수령 안 됨·세계수 화면 버튼 없음). 남은 일 9장 "2026-11-10 남은 일 (퀘스트 합침)".

> **2026-11-10 (LKH) 도감 세트 보너스:** 버섯 도감에 **수집·세트 보너스**를 붙였다. 방치 전투 보스를 잡을 때마다 열린 무리(종류 × ★1~5)마다 확률로 **아직 없는 버섯 1종**을 얻고(별이 높을수록 늦게 열림: ★1 s1 · ★2 s40 · ★3 s120 · ★4 s240 · ★5 s360), 한 무리를 다 모으면 세트 효과 — **식용 = 공격력 %, 약용 = 체력·체력 재생 %(장비 보유 효과 %에 더함), 독성 = 공격 속도 %(최종 속도에 곱함)**. ★5 식용·약용 = 전설 무기·방어구 1레벨 보유 효과(2187.5%·1250%). 표 `도감`(18001~18015)·`도감세트`(18101~18120), 코드 `MushroomDex.cs`, 도감 화면 세트 보너스 버튼 = 세트 진행·효과 목록. 수치·시기 근거 [밸런스설계/도감세트.md](밸런스설계/도감세트.md). **시뮬은 아직 도감을 모름 — 진행 영향 실측 전**(9장 "2026-11-10 남은 일").

> **2026-11-09 (main, LKH·KHD):** 던전(벽돌깨기) 화면을 Main과 맞춤(`3ba8a59`, 배경·TopBar·장착 무기 그림·군락 그림 `BattleLooks`) → 수확 완료 팝업 폴리싱·군락 버섯 가림 수정·설정 창 광고 개인정보 글자 배치 → KHD: 화면 비율 대응(`SafeAreaDock`)·폰트 굽기 최적화(아틀라스 약 33%↓) → **영어 번역**(`4fbf3b0`, 문자열 표 `String.xlsx`·`Loc`·`LocalizedText`, 기기 언어 자동 선택) · **광고 상자 개편**(두 자리 배회, 골드 **또는** 균사석 50:50 한 가지) · 골드 던전 보상 20000→10000 → **해금 팝업**(`6c206b6`, `Unlock/` — 하단 탭 잠금·"새로운 콘텐츠!" 안내·탭 가리키기) → 의뢰 **일괄 수령** 버튼(세계수·메인)·확인 창 게임 UI 톤·편성 중 꼬마 카드 덮개(`2a205ca`). main에 머지(`16cbebc`) — **푸시 안 함**. KHD에 **머지 안 한 커밋 2개**(`50f354c` 코드 UI 캔버스화 64파일, `a1590aa` 출석 팝업 수정, main과 충돌 없음 확인). 커밋 표는 2장, 남은 일은 **9장 "2026-11-09 남은 일"**.

> **2026-11-06 (KHD·LKH·PJS):** **던전 2~5 구현**(`dc3c7a2`, `DungeonSession`·`IdleBattle.Dungeon.cs`: 2 골드·3 균사석 = InGame 재사용, 4 보스 러시·5 보스 토벌 = 방치 화면, 해금 5/10/20/30/50) → PJS UI 개편 22~24차 머지(`29ad5b3`) + LKH 누락분 재적용(`b2c6eb1`) → **개발자 모드 출시 빌드 제외**(`bae4ea3`) → **출시 안정화 1·2차**(`bd85376`·`6f41e79`: 실제 광고 ID, `GameClock` 시계 조작 차단, 광고 동의 UMP, 전투 재시도, 세이브 백업 — 8장 "출시 안정화") → 출시 빌드 세팅(`bcb20cc`, 버전 코드 2) → 입장권·레이드 나가기·세계수 선물 → **7일 출석**. 작업 준비 문서 [작업준비_261106.md](작업준비_261106.md) 중 광고 버섯·던전 1~5는 끝, **꼬마별 트리는 안 함**.

> **2026-11-05 (LKH):** 11-04에 멈췄던 **펫 삭제(`08bb82b`)·펫 삭제 밸런스 보정(`d29c186`, 상수 `weaponEffectMul` 1.75·스테이지 421~700 체력, verify#20 조건부 통과)은 커밋됨.** 이어서 미사용 리소스·코드 정리(`c1e73d8`) → **던전 소탕 삭제**(`344f7fb`, 매 판 직접 플레이) → **메인 BGM**(`BgmPlayer`, 옵션 배경음 슬라이더 연결, 광고 중 정지) → **폰트 정적 아틀라스**(`970fa52`, 메뉴 `MoreMush/Fonts/Bake Static Atlases`, 폰트 에셋은 이제 커밋) → 세계수 그림 4장(단계마다 커짐)·장식 24종 새 그림 → 로딩·소환·던전 배너/카드 교체 → 표 정리(`43c925c`, 안 읽는 열 삭제·리소스 120행·`adBoxMycelite`) → 방치 전투 꼬마·버섯 크기·간격 → **보스 10종 교체**(`4ef560d`, 표가 보스 그림을 정함, 할아버지 삭제) → 표·인계 마무리(`c6d8675`) → 뽑기 카드 아이콘을 소환진 가운데로(`84772db`). 테스트용 **개발자 모드**(`` ` `` 키)는 [HANDOFF_DEVMODE.md](HANDOFF_DEVMODE.md). 커밋 표는 2장, 남은 일은 **9장 "2026-11-05 남은 일"**.

> **2026-11-04 (LKH):** 무기(수확기) 10종 개편·고급 등급 삭제(`77ad593`) → KHD 머지(`f303741`, 꼬마 등급 삭제·꼬마 뽑기 같은 확률·버섯 리소스) → 안드로이드 몰입 모드(`b6b2d6a`) → **방어구 10종 개편**(`7b17e68`, 일반 4·희귀 3·영웅 2·전설 1, 그림 10장 = HarvesterSet 규격) → **펫 삭제**(`08bb82b`): 장비 펫·펫 뽑기·편성 펫 칸 삭제, 편성 칸 위 3 · 아래 2 가운데 → 펫 삭제 밸런스 보정(이론 끝, 검증 중단 → 11-05 `d29c186` 적용). 커밋 표는 2장.

> **2026-11-03 데이터 추가 조사:** 통합 23시트·Legacy 5종·게임 코드·씬/프리팹·Resources를 대조해 `수확전투` 16행(스킬 효과·환경 장치·보스 분신 기준·배속), `상수` 3행, `리소스` 26행을 추가했다. 등급 `#name`은 런타임 `name:string`으로 전환했다. 새 전투 설정·상수·등급명은 게임 연동, 기존 값·수식은 보존. Unity 검증 1,116행 OK. 조사 범위·제외 기준·검증은 [DATA_AUDIT.md](DATA_AUDIT.md), 재대조는 `python Tools/audit_game_data.py`.

> 작성 2026-10-28 · **갱신 2026-11-11** · 기준 브랜치 `main` (`eaba113`까지 — LKH와 같은 커밋, KHD `8e6fb58`까지 머지 포함, origin 푸시됨)
> itch.io에 PC 웹빌드로 출시한 **MoreMustMush**를 모바일(세로)로 옮기는 Unity 프로젝트입니다. 이 문서는 지금까지의 구조와 다음 작업자가 알아야 할 것을 정리합니다. 팀 공유용 요약은 claude.ai의 "MoreMustMush 모바일 인계문서"에 있습니다.
>
> **2026-10-29~30 방향 전환:** 게임은 **방치형**이 되었고 처음 화면은 `Main` 씬입니다. 기존 판 플레이(`InGame` 씬)는 10-30 한 번 지웠다가 KHD(`3183125`)가 **던전 1로 되살렸습니다**(Main 던전 탭 → InGame → 결과 팝업 → Main, 4장). **`SkillTree` 씬은 제거**(`Backup/SkillTree/`에 백업, 5장). **`WorldTree` 씬도 10-30 늦게 제거**(`Backup/WorldTree/`에 백업, 하단바 세계수 = "아직 개발중인 기능입니다." 알림). 방치형 화면은 **3장 3.1**, 기획 논의는 `HANDOFF_IDLE_PIVOT.md`, 방치형 수치는 `BALANCE_IDLE.md`·`밸런스설계/`를 보세요.

> **2026-11-02 방치형 밸런스 v3 (KHD):** 스테이지 1~700 표·700 이후 지수 꼬리·Power 강화·퀘스트 35뽑·뽑기 등급 해금·던전 해금 5/25/150/250/400과 던전 1 레벨 보상·소탕을 적용했습니다(소탕은 11-05 삭제). **11-03 LKH 브랜치에 머지**(표 병합 방식은 6.0). **인계는 `HANDOFF_BALANCE_V3.md`**, 기준·결정은 `밸런스설계/README.md`. 실측(test) 전입니다.

> **2026-10-30 최적화·안정화 (KHD `de3432e`):** PlayerPrefs 캐시(`Equipment`·`Formation`), 저장 모으기(`SaveScheduler`), 전투 서브 캔버스, URP·Player 설정, 저장·시계·넘침 안정화. 요약은 8장, **자세한 인계·확인 방법·되돌리기는 `HANDOFF_OPTIMIZATION.md`**. 에디터·실기기 확인 전입니다.

> **2026-10-30 해상도 대응:** 모든 씬의 UI는 `Canvas/SafeArea/Stage`(`StageFrame`) 아래에 있습니다. **높이 2340 고정, 폭 1080~1316 가변, 남는 공간은 검정.** 새 UI는 폭 1080에서 잘리지 않게 앵커로 맞추세요(3.2).

> **2026-10-30 데이터 표 정리:** 새 통합 표는 `Assets/Data/Tables/MainTable.xlsx`입니다(적·보스·꼬마·업그레이드·스테이지·리소스·`info`). 기존 엑셀 5개와 대응 `.bytes`는 각각 `Tables/Legacy/`로 이동했습니다. **11-02에 방치형 수치를 이 표에서 읽게 전환했습니다**(강화·꼬마·스테이지 보상·장비·뽑기·상수 시트, 6장 6.0). 자세한 내용은 6장과 엑셀 `info`.

> **2026-11-02 세계수 구현 (LKH):** `WorldTree` 씬을 되살리고(하단바 세계수 → WorldTree), PC판(`C:\Project\MoreMustMush` `Game.Farm*.cs`)의 세계수를 방치형에 맞게 옮겼습니다 — **성장(나이테·공물·가호) · 꼬마(호감도·부탁·★진화) · 공방(제작 목록·슬롯·가속) · 꾸미기(뿌리·줄기·가지·수관) · 수확 보따리.** 수치는 모두 `MainTable.xlsx` 세계수 시트(코드에 숫자 없음). 대응: 창고 버섯 = 방치 전투 처치, 세계수 꼬마 = 뽑기 꼬마 9종, 보따리 = 단계별 시간당 표, 효과 = 방치 능력치 배율. 이어서 **이론·검증 에이전트 3라운드로 세계수 밸런스 v3**(검증 통과, `밸런스설계/세계수/`). 자세한 내용은 **3.3**.

> **2026-11-03 (LKH):** KHD 방치 밸런스 v3 머지(엑셀 세 방향 병합, 6.0) → 세계수 밸런스 재검증(v4: 5단계 가지 칸 6, 라운드 4·5) → **세계수 일일·주간 의뢰**(PC `Game.Quests` 기반, v5 라운드 6 통과, 3.3) → 에디터 메뉴 `MoreMush/게임 기록 전체 초기화` → **프로젝트 전수 조사·정리**(8장 "정리") → **던전(InGame) 상단 HUD 정리·임시 그림 교체**(4장 첫머리). 커밋 표는 2장.

> **2026-11-02 구현 데이터 누락 조사:** 현재 코드·씬·프리팹과 표를 대조해 `장비`·`뽑기상품`·`방치전투`·`수확전투`·`수확등급`을 추가했습니다. 던전의 이름·실제 연결 씬·구현 여부와 장비 그림 리소스도 보충했습니다. 같은 날 **게임이 이 시트들을 읽게 연동**했습니다(장비=Equipment 이름·그림, 뽑기상품=GachaPanel 가격·횟수·광고 하루 횟수, 방치전투=IdleBattle 타이밍·챕터, 수확전투·수확등급=HarvestTuning, 던전=DungeonEntry 씬·이름). 상수 `adGachaPulls`는 뽑기상품 광고 행과 겹쳐 지웠습니다. 기존 세계수 밸런스·값·수식은 유지(6.0).
