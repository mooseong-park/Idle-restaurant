using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 직원 idle slot anchor — slotIdx (0~5) 식별
    public sealed class StaffSlotAnchor : MonoBehaviour
    {
        [SerializeField] int slotIdx;
        public int SlotIdx => slotIdx;

        void OnValidate()
        {
            if (slotIdx < 0)
                Debug.LogError($"[{name}] StaffSlotAnchor.slotIdx 음수 ({slotIdx})", this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.StaffSlot;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, $"St{slotIdx}", AnchorGizmo.StaffSlot);
#endif
        }
    }
}
