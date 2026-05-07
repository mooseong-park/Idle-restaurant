using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 외부 퇴장 위치 — 손님이 식사 후 사라지기 위해 향하는 화면 밖 지점.
    public sealed class TopExitAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.Exit;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.4f, 0.4f, 0f));
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, "↑EXIT", AnchorGizmo.Exit);
#endif
        }
    }
}
