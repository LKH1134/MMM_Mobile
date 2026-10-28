using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 안드로이드 빌드 때 메인 액티비티(런처)에 Firebase Test Lab Game Loop 신호(TEST_LOOP)를 받는 intent-filter를 붙인다.
    // 이 신호로 켜지면 Benchmark가 바로 돌고, 결과를 Firebase 결과 파일에 쓴 뒤 앱을 끈다.
    // 커스텀 매니페스트를 따로 두지 않고, Unity가 만든 unityLibrary 매니페스트를 빌드마다 고친다
    class GameLoopManifest : IPostGenerateGradleAndroidProject
    {
        const string Android = "http://schemas.android.com/apk/res/android";
        const string TestLoop = "com.google.intent.action.TEST_LOOP";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest))
            {
                Debug.LogWarning($"[GameLoopManifest] 매니페스트가 없다: {manifest}");
                return;
            }
            var doc = new XmlDocument();
            doc.Load(manifest);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", Android);

            // Unity는 gradle 프로젝트를 다시 쓰므로 전에 붙인 것이 남아 있을 수 있다 — 지우고 늘 같은 자리에 다시 붙인다
            foreach (XmlNode old in doc.SelectNodes($"//intent-filter[action/@android:name='{TestLoop}']", ns))
                old.ParentNode.RemoveChild(old);
            var launcher = doc.SelectSingleNode("//activity[intent-filter/category[@android:name='android.intent.category.LAUNCHER']]", ns) as XmlElement;
            if (launcher == null)
            {
                Debug.LogWarning("[GameLoopManifest] 런처 액티비티를 못 찾아 Game Loop 신호를 붙이지 못했다");
                return;
            }

            var filter = doc.CreateElement("intent-filter");
            filter.AppendChild(Element(doc, "action", "name", TestLoop));
            filter.AppendChild(Element(doc, "category", "name", "android.intent.category.DEFAULT"));
            filter.AppendChild(Element(doc, "data", "mimeType", "application/javascript"));
            // 런처 intent-filter 바로 뒤에 둔다. 액티비티 끝(<layout> 뒤)에 붙였더니 Firebase가
            // "Game Loop intent filter가 없거나 형식이 잘못됐다"며 받지 않았다 (안드로이드 자체는 순서를 안 따진다)
            var launcherFilter = launcher.SelectSingleNode("intent-filter[category/@android:name='android.intent.category.LAUNCHER']", ns);
            launcher.InsertAfter(filter, launcherFilter);
            doc.Save(manifest);
            Debug.Log($"[GameLoopManifest] {launcher.GetAttribute("name", Android)}에 Game Loop 신호를 붙였다");
        }

        static XmlElement Element(XmlDocument doc, string tag, string attribute, string value)
        {
            var e = doc.CreateElement(tag);
            e.SetAttribute(attribute, Android, value);
            return e;
        }
    }
}
