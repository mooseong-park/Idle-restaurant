using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 바 카운터 anchor — 직원 ServePos 계산용 (singleton).
    public sealed class BarAnchor : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.Bar;
            Gizmos.DrawWireSphere(transform.position, 0.18f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, "BAR", AnchorGizmo.Bar);
#endif
        }
    }
}
