using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 도어 anchor — 외부/실내 boundary 기준점 (singleton).
    public sealed class DoorAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.Door;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.3f, 0f));
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, "DOOR", AnchorGizmo.Door);
#endif
        }
    }
}
