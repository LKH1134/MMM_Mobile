using System;
using System.IO;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세이브 백업: PlayerPrefs와 따로 기기 안 파일(Application.persistentDataPath/SaveBackup)에 같은 내용을 남긴다.
    // PlayerPrefs 값이 깨지거나 사라져 새로 시작하게 될 때 여기서 읽어 되살린다 (앱 데이터 삭제·재설치 땐 함께 지워진다).
    // 쓰기는 임시 파일에 먼저 쓰고 옮긴다 — 쓰는 도중 앱이 꺼져도 둘 중 하나는 온전히 남는다
    public static class LocalBackup
    {
        const string Folder = "SaveBackup";
        static bool warned;     // 쓰기 실패 경고는 한 번만 (저장 주기마다 로그가 쌓이지 않게)

        static string Dir => Path.Combine(Application.persistentDataPath, Folder);
        static string PathOf(string name) => Path.Combine(Dir, name + ".json");
        static string TempOf(string name) => PathOf(name) + ".tmp";

        public static void Write(string name, string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = PathOf(name), tmp = TempOf(name);
                File.WriteAllText(tmp, text);
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
            catch (Exception e)
            {
                if (warned) return;
                warned = true;
                Debug.LogWarning($"[LocalBackup] {name} 백업을 쓰지 못했다: {e.Message}");
            }
        }

        // 본 파일 → 임시 파일 순으로 읽는다 (본 파일을 옮기다 꺼진 경우)
        public static bool TryRead(string name, out string text)
        {
            foreach (var path in new[] { PathOf(name), TempOf(name) })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    text = File.ReadAllText(path);
                    if (!string.IsNullOrEmpty(text)) return true;
                }
                catch (Exception e) { Debug.LogWarning($"[LocalBackup] {path} 를 읽지 못했다: {e.Message}"); }
            }
            text = null;
            return false;
        }

        // 깨진 원문을 따로 남긴다 (원인 조사용, 덮어쓴다)
        public static void Keep(string name, string text) => Write(name, text ?? "");

        // 게임 기록 전체 초기화 때 백업도 지운다 (안 지우면 다음 실행에 옛 기록이 되살아난다)
        public static void DeleteAll()
        {
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); }
            catch (Exception e) { Debug.LogWarning($"[LocalBackup] 백업 폴더를 지우지 못했다: {e.Message}"); }
        }
    }
}
