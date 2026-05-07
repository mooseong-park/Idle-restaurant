using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 외부 spawn 위치 — 손님이 게임 화면에 처음 나타나는 지점 (보통 화면 좌측 밖).
    public sealed class OutsideSpawnAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.Spawn;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.4f, 0.4f, 0f));
            Gizmos.DrawLine(transform.position, transform.position + Vector3.right * 0.6f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, "SPAWN→", AnchorGizmo.Spawn);
#endif
        }
    }
}
