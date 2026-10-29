using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // Main 밖의 씬에 둔 하단바 버튼: 누르면 Main으로 가서 그 탭을 연다 (-1 = 홈)
    [RequireComponent(typeof(Button))]
    public class MainTabButton : MonoBehaviour
    {
        [SerializeField] int tab = MainNavigation.Home;

        void Awake() => GetComponent<Button>().onClick.AddListener(() => MainNavigation.Open(tab));
    }
}
