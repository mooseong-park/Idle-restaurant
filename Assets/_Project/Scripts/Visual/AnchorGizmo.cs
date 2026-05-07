using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // Anchor Marker 들이 공유하는 Gizmo 색 팔레트 + 라벨 헬퍼.
    // 별도 파일로 분리: Unity의 MonoScript는 .cs 1개당 1 클래스 매핑이 자연스러움 —
    // MonoBehaviour 옆에 static 헬퍼 두면 'ExtensionOfNativeClass' 에러 발생.
    internal static class AnchorGizmo
    {
        public static readonly Color Seat        = new Color(0.30f, 0.78f, 1.00f, 0.9f); // cyan
        public static readonly Color Station     = new Color(1.00f, 0.62f, 0.20f, 0.9f); // orange
        public static readonly Color StaffSlot   = new Color(0.40f, 0.90f, 0.45f, 0.9f); // green
        public static readonly Color QueueSlot   = new Color(1.00f, 0.92f, 0.30f, 0.9f); // yellow
        public static readonly Color Door        = new Color(1.00f, 0.30f, 0.30f, 0.9f); // red
        public static readonly Color DoorOutside = new Color(1.00f, 0.55f, 0.10f, 0.9f); // amber (외부 측)
        public static readonly Color DoorInside  = new Color(0.50f, 0.30f, 1.00f, 0.9f); // purple (실내 측)
        public static readonly Color Spawn       = new Color(0.80f, 0.80f, 0.80f, 0.9f); // white-gray
        public static readonly Color Exit        = new Color(0.70f, 0.70f, 0.95f, 0.9f); // light blue
        public static readonly Color Bar         = new Color(0.55f, 0.38f, 0.22f, 0.9f); // brown

#if UNITY_EDITOR
        public static void DrawLabel(Vector3 pos, string text, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = color },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
            };
            Handles.Label(pos + Vector3.up * 0.35f, text, style);
        }
#endif
    }
}
