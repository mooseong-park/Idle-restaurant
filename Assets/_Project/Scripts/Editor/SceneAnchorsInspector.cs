#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Project.Visual;

namespace Project.EditorTools
{
    // SceneAnchors Inspector 확장 — 어떤 anchor 가 있는지/누락됐는지 한눈에 표시.
    // Play 모드에서만 의미 있음 (Awake에 BuildLookups 호출).
    [CustomEditor(typeof(SceneAnchors))]
    public sealed class SceneAnchorsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== Anchor 진단 ===", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play 모드에서만 정확한 정보가 표시됩니다. " +
                    "Edit 모드에선 anchor 수만 확인 가능 (Hierarchy를 직접 보세요).",
                    MessageType.Info);
                ShowEditModeCheck();
                return;
            }

            var sa = (SceneAnchors)target;

            EditorGUILayout.IntField("Seat 수", sa.SeatCount);
            EditorGUILayout.IntField("StaffSlot 수", sa.StaffSlotCount);
            EditorGUILayout.LabelField("Door (anchors.DoorPos)", sa.DoorPos.ToString("F2"));
            EditorGUILayout.LabelField("OutsideSpawn", sa.OutsideSpawnPos.ToString("F2"));
            EditorGUILayout.LabelField("TopExit", sa.TopExitPos.ToString("F2"));
            EditorGUILayout.LabelField("Bar", sa.BarPos.ToString("F2"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Door Corridor:", EditorStyles.boldLabel);
            if (sa.HasDoorWaypoints)
            {
                EditorGUILayout.LabelField("  DoorOutside", sa.DoorOutsidePos.ToString("F2"));
                EditorGUILayout.LabelField("  DoorInside",  sa.DoorInsidePos.ToString("F2"));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "DoorOutsideAnchor / DoorInsideAnchor 가 씬에 없습니다 — Route가 도어 corridor 없이 동작 (벽 통과 위험). " +
                    "메뉴 'Idle Restaurant > Add Door Waypoints (non-destructive)' 실행 권장.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Rebuild Lookups (anchor 재수집)"))
            {
                sa.BuildLookups();
                Debug.Log("[SceneAnchors] BuildLookups 재실행. 런타임에 anchor 추가/이동 후 호출.");
            }
        }

        // Edit 모드 — Hierarchy에 marker가 몇 개 있는지 카운트
        void ShowEditModeCheck()
        {
            int seats     = CountInScene<SeatAnchor>();
            int slots     = CountInScene<StaffSlotAnchor>();
            int queues    = CountInScene<QueueSlotAnchor>();
            int stations  = CountInScene<StationAnchor>();
            bool door     = HasInScene<DoorAnchor>();
            bool spawn    = HasInScene<OutsideSpawnAnchor>();
            bool exit     = HasInScene<TopExitAnchor>();
            bool bar      = HasInScene<BarAnchor>();
            bool doorOut  = HasInScene<DoorOutsideAnchor>();
            bool doorIn   = HasInScene<DoorInsideAnchor>();

            EditorGUILayout.IntField("SeatAnchor 수",      seats);
            EditorGUILayout.IntField("StaffSlotAnchor 수", slots);
            EditorGUILayout.IntField("QueueSlotAnchor 수", queues);
            EditorGUILayout.IntField("StationAnchor 수",   stations);
            EditorGUILayout.Toggle("DoorAnchor",         door);
            EditorGUILayout.Toggle("OutsideSpawnAnchor", spawn);
            EditorGUILayout.Toggle("TopExitAnchor",      exit);
            EditorGUILayout.Toggle("BarAnchor",          bar);
            EditorGUILayout.Toggle("DoorOutsideAnchor",  doorOut);
            EditorGUILayout.Toggle("DoorInsideAnchor",   doorIn);

            if (!doorOut || !doorIn)
            {
                EditorGUILayout.HelpBox(
                    "Door corridor waypoint 누락 — 'Idle Restaurant > Add Door Waypoints (non-destructive)' 실행 권장.",
                    MessageType.Warning);
            }
        }

        static int CountInScene<T>() where T : MonoBehaviour =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        static bool HasInScene<T>() where T : MonoBehaviour =>
            Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) != null;
    }
}
#endif
