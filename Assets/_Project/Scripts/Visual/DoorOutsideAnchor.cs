using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 도어 통로의 외부 측 waypoint — 손님이 외부에서 실내로 들어올 때 첫 경유지.
    public sealed class DoorOutsideAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.DoorOutside;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.35f, 0.35f, 0f));
            Gizmos.DrawLine(transform.position + Vector3.up * 0.3f, transform.position + Vector3.down * 0.3f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, "▼ DoorOut", AnchorGizmo.DoorOutside);
#endif
        }
    }
}
