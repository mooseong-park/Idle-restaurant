using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 도어 통로의 실내 측 waypoint — 실내에서 외부로 나갈 때 첫 경유지.
    public sealed class DoorInsideAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.DoorInside;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.35f, 0.35f, 0f));
            Gizmos.DrawLine(transform.position + Vector3.up * 0.3f, transform.position + Vector3.down * 0.3f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, "▲ DoorIn", AnchorGizmo.DoorInside);
#endif
        }
    }
}
