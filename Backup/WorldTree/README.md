# WorldTree 백업 (2026-10-30)

세계수 화면을 아직 쓰지 않기로 해서 씬과 그 화면에서만 쓰던 코드·리소스를 뺐다.
`Assets/` 밖에 두어 Unity가 읽거나 컴파일하지 않는다. `.meta`를 같이 두었으므로
되살릴 때는 `Assets/` 아래 같은 경로로 그대로 복사하면 GUID 연결이 유지된다.

들어 있는 것:
- 씬: `Assets/Scenes/WorldTree.unity`
- 코드: `WorldTreeScreen.cs`(세계수 화면), `MainTabButton.cs`(다른 씬 하단바에서 Main 탭 열기)
- 프리팹: `QuestPopup`(의뢰), `WorldTree/KidPopup`·`SkinPopup`·`CraftPopup`(꼬마·꾸미기·제작소)
- 그림: `Art/Generated/Icons/skilltree.png`, `Resources/Sprite/BG/imsi.png`, `Resources/Sprite/Tree/image.png`

되살릴 때 함께 할 것:
- Build Settings에 `Assets/Scenes/WorldTree.unity` 다시 추가 (Main 다음)
- Main 하단바 `WorldTreeBtn`: `ComingSoonButton`을 빼고 `SceneButton`(scene = `WorldTree`)을 붙인다
- 씬 안 하단바 버튼(홈·던전 등)은 `MainTabButton`을 쓴다

남겨 둔 것 (다른 곳에서 씀): `MainNavigation`(Main 탭 열기), `TopBar`·`Toast`·`OptionPopup` 프리팹,
`BottomBar` 프리팹(세계수 자리 버튼은 이제 "아직 개발중인 기능입니다." 알림).
