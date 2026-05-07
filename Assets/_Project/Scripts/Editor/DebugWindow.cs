#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Core;
using Project.Domain;

namespace Project.EditorTools
{
    // Play 모드에서 Customer/Staff POCO 상태를 실시간으로 보여주는 창.
    // POCO는 Inspector에 안 보이므로 이 창이 디버깅 단일 진입점.
    //
    // 메뉴: Idle Restaurant > Debug Window
    public sealed class DebugWindow : EditorWindow
    {
        Vector2 scroll;
        bool showCustomers = true;
        bool showStaff = true;
        bool showState = true;
        bool autoRepaint = true;

        [MenuItem("Idle Restaurant/Debug Window")]
        public static void Open()
        {
            var win = GetWindow<DebugWindow>("Idle Debug");
            win.minSize = new Vector2(360, 400);
            win.Show();
        }

        void OnInspectorUpdate()
        {
            // 초당 ~10회 repaint (EditorApplication.update보다 부담 적음)
            if (autoRepaint && Application.isPlaying) Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에서만 활성화됩니다.", MessageType.Info);
                return;
            }

            var gm = FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                EditorGUILayout.HelpBox("GameManager 없음.", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (showState) DrawState(gm);
            EditorGUILayout.Space();
            if (showCustomers) DrawCustomers(gm);
            EditorGUILayout.Space();
            if (showStaff) DrawStaff(gm);

            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            showState     = GUILayout.Toggle(showState, "State", EditorStyles.toolbarButton);
            showCustomers = GUILayout.Toggle(showCustomers, "Customers", EditorStyles.toolbarButton);
            showStaff     = GUILayout.Toggle(showStaff, "Staff", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            autoRepaint   = GUILayout.Toggle(autoRepaint, "Auto Refresh", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();
        }

        void DrawState(GameManager gm)
        {
            EditorGUILayout.LabelField("== State ==", EditorStyles.boldLabel);
            var s = gm.State;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField($"Day {s.Day}    Phase: {gm.DaySystem.Phase}    Speed: ×{gm.GameSpeed:F2}{(gm.IsPaused ? " (PAUSED)" : "")}");
                EditorGUILayout.LabelField($"💰 Cash    {s.Cash:F2}");
                EditorGUILayout.LabelField($"💎 Gems    {s.Gems}");
                EditorGUILayout.LabelField($"⭐ Rating  {s.Rating}");
                EditorGUILayout.LabelField($"Day Revenue: {s.DayRevenue:F2}    Total: {s.TotalRevenue:F2}    Passive: {s.PassiveRevenue:F2}");
                EditorGUILayout.LabelField($"Stats: ✅ {s.Successes}    💢 {s.PatienceFails}    📅 Achieved {s.DayAchieved}");
                EditorGUILayout.LabelField($"Track Lv: Menu {s.MenuLv} / Staff {s.StaffLv} / Service {s.ServiceLv} / Venue {s.VenueLv} / Takeout {s.TakeoutLv} / Ads {s.AdsLv} / Tip {s.TipLv} / Merch {s.MerchLv}");
            }

            // 속도 컨트롤
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Speed Control", GUILayout.Width(100));
            if (GUILayout.Button("⏸", GUILayout.Width(30))) gm.Pause();
            if (GUILayout.Button("1×", GUILayout.Width(40))) gm.Resume(1f);
            if (GUILayout.Button("2×", GUILayout.Width(40))) gm.Resume(2f);
            if (GUILayout.Button("3×", GUILayout.Width(40))) gm.Resume(3f);
            if (GUILayout.Button("5×", GUILayout.Width(40))) gm.Resume(5f);
            EditorGUILayout.EndHorizontal();
        }

        void DrawCustomers(GameManager gm)
        {
            var customers = gm.CustomerSystem.Customers;
            EditorGUILayout.LabelField($"== Customers ({customers.Count}) ==", EditorStyles.boldLabel);

            // 테이블 헤더
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Id",       GUILayout.Width(40));
                GUILayout.Label("Stage",    GUILayout.Width(110));
                GUILayout.Label("Seat",     GUILayout.Width(40));
                GUILayout.Label("Q",        GUILayout.Width(30));
                GUILayout.Label("Mood",     GUILayout.Width(60));
                GUILayout.Label("Wait/St",  GUILayout.Width(80));
                GUILayout.Label("Notes",    GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < customers.Count; i++)
                {
                    var c = customers[i];
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"#{c.Id}",                      GUILayout.Width(40));
                    GUILayout.Label(c.Stage.ToString(),              GUILayout.Width(110));
                    GUILayout.Label(c.SeatIdx >= 0 ? c.SeatIdx.ToString() : "-",  GUILayout.Width(40));
                    GUILayout.Label(c.QueueIdx >= 0 ? c.QueueIdx.ToString() : "-", GUILayout.Width(30));
                    GUILayout.Label(MoodLabel(c.Mood),               GUILayout.Width(60));
                    GUILayout.Label($"{(c.WaitActive ? "W" : "-")} {c.StageElapsed:F1}", GUILayout.Width(80));
                    GUILayout.Label(CustomerNotes(c),                GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        void DrawStaff(GameManager gm)
        {
            var staff = gm.StaffSystem.Staffs;
            EditorGUILayout.LabelField($"== Staff ({staff.Count}) ==", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Id",       GUILayout.Width(40));
                GUILayout.Label("Name",     GUILayout.Width(80));
                GUILayout.Label("Rarity",   GUILayout.Width(70));
                GUILayout.Label("State",    GUILayout.Width(140));
                GUILayout.Label("Target",   GUILayout.Width(80));
                GUILayout.Label("Notes",    GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < staff.Count; i++)
                {
                    var s = staff[i];
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"#{s.Id}",                              GUILayout.Width(40));
                    GUILayout.Label(s.Name ?? "-",                            GUILayout.Width(80));
                    GUILayout.Label(s.Rarity != null ? s.Rarity.DisplayName : "-", GUILayout.Width(70));
                    GUILayout.Label(s.State.ToString(),                       GUILayout.Width(140));
                    GUILayout.Label(s.TargetSeatIdx >= 0 ? $"seat {s.TargetSeatIdx}" : "-", GUILayout.Width(80));
                    GUILayout.Label(StaffNotes(s),                            GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        static string MoodLabel(CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Success: return "✓";
                case CustomerMood.PFail:   return "✗ pfail";
                case CustomerMood.QFail:   return "✗ qfail";
                default:                   return "-";
            }
        }

        static string CustomerNotes(Customer c)
        {
            var sb = new StringBuilder();
            if (c.IsQueueBound) sb.Append("[qb] ");
            if (c.WaitActive) sb.Append($"wait {c.WaitElapsed:F1}s ");
            if (c.Order != null && c.Order.Count > 0)
            {
                sb.Append("order:");
                for (int i = 0; i < c.Order.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(c.Order[i].Id);
                }
            }
            if (c.LastRevenue > 0) sb.Append($" $+{c.LastRevenue}");
            return sb.ToString();
        }

        static string StaffNotes(Domain.Staff s)
        {
            var sb = new StringBuilder();
            if (s.CurrentStation != null) sb.Append($"station:{s.CurrentStation.Id} ");
            if (s.Order != null && s.Order.Count > 0) sb.Append($"order×{s.Order.Count} ");
            sb.Append($"t {s.StateElapsed:F1}s");
            return sb.ToString();
        }
    }
}
#endif
