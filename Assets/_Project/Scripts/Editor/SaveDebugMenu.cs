#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Project.Core;

namespace Project.EditorTools
{
    // 디버그용 세이브 관리 메뉴.
    // - Reset Save: 다음 Play 시 fresh state (Day 1, Cash 0, 직원 InitialStaffRarities 만)
    // - Print Save Json: 현재 저장된 JSON 콘솔 출력
    public static class SaveDebugMenu
    {
        [MenuItem("Idle Restaurant/Debug/Reset Save")]
        public static void ResetSave()
        {
            if (!SaveManager.HasSave())
            {
                Debug.Log("[SaveDebug] 저장된 세이브 없음.");
                return;
            }
            if (!EditorUtility.DisplayDialog(
                "세이브 초기화",
                "저장된 게임 진행도를 모두 삭제합니다. 다음 Play 시 Day 1부터 시작합니다.\n계속하시겠습니까?",
                "삭제", "취소")) return;

            SaveManager.Reset();
            Debug.Log("[SaveDebug] 세이브 삭제 완료. 다음 Play 부터 fresh state.");
        }

        [MenuItem("Idle Restaurant/Debug/Print Save Json")]
        public static void PrintSaveJson()
        {
            if (!SaveManager.HasSave())
            {
                Debug.Log("[SaveDebug] 저장된 세이브 없음.");
                return;
            }
            var json = PlayerPrefs.GetString("save_v1");
            Debug.Log($"[SaveDebug] save_v1:\n{json}");
        }

        // RNG 시드 디버그. 설정 후 다음 Play 부터 가챠/스폰 분포 재현 가능.
        [MenuItem("Idle Restaurant/Debug/Set RNG Seed (next play)")]
        public static void SetRngSeed()
        {
            int currentSeed = PlayerPrefs.GetInt("rng_seed", 12345);
            string input = EditorInputDialog.Show("RNG Seed", $"다음 Play 시 사용할 시드 (정수). 같은 시드 = 같은 가챠/스폰 시퀀스.", currentSeed.ToString());
            if (string.IsNullOrEmpty(input)) return;
            if (!int.TryParse(input, out int seed))
            {
                Debug.LogError($"[SaveDebug] 정수 입력 필요 (입력: '{input}')");
                return;
            }
            PlayerPrefs.SetInt("rng_seed", seed);
            PlayerPrefs.Save();
            Debug.Log($"[SaveDebug] RNG seed = {seed} 저장. 다음 Play 부터 적용. (Clear 메뉴로 해제)");
        }

        [MenuItem("Idle Restaurant/Debug/Clear RNG Seed")]
        public static void ClearRngSeed()
        {
            if (!PlayerPrefs.HasKey("rng_seed"))
            {
                Debug.Log("[SaveDebug] RNG seed 미설정 — 이미 Unity Random 모드.");
                return;
            }
            PlayerPrefs.DeleteKey("rng_seed");
            PlayerPrefs.Save();
            Debug.Log("[SaveDebug] RNG seed 삭제. 다음 Play 부터 Unity Random (매 실행마다 다른 시퀀스).");
        }
    }

    // EditorGUI 다이얼로그 헬퍼 — Unity 에 빌트인 input 다이얼로그가 없어 작은 모달 윈도우로 구현.
    // X 로 창 닫기 = 취소 처리 (기본값 cancelled=true, 확인 시에만 false).
    sealed class EditorInputDialog : EditorWindow
    {
        string input;
        string message;
        bool cancelled = true;

        public static string Show(string title, string message, string defaultValue)
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new UnityEngine.GUIContent(title);
            window.message = message;
            window.input = defaultValue;
            window.minSize = new UnityEngine.Vector2(420, 140);
            window.maxSize = new UnityEngine.Vector2(420, 140);
            window.ShowModalUtility();
            return window.cancelled ? null : window.input;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            input = EditorGUILayout.TextField(input);
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("취소")) { cancelled = true; Close(); }
                if (GUILayout.Button("확인")) { cancelled = false; Close(); }
            }
            if (UnityEngine.Event.current.isKey && UnityEngine.Event.current.keyCode == UnityEngine.KeyCode.Return)
            { cancelled = false; Close(); }
            if (UnityEngine.Event.current.isKey && UnityEngine.Event.current.keyCode == UnityEngine.KeyCode.Escape)
            { cancelled = true; Close(); }
        }
    }
}
#endif
