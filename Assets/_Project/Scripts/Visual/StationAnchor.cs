using UnityEngine;
using Project.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 주방 station anchor — MenuItemSO 참조로 식별 (id 매칭)
    public sealed class StationAnchor : MonoBehaviour
    {
        [SerializeField] MenuItemSO menuItem;
        public MenuItemSO MenuItem => menuItem;

        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.Station;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.5f, 0f));
#if UNITY_EDITOR
            string label = menuItem != null ? menuItem.Id : "?";
            AnchorGizmo.DrawLabel(transform.position, label, AnchorGizmo.Station);
#endif
        }
    }
}
