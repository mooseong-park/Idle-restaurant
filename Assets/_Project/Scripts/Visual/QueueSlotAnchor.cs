using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // 큐 슬롯 anchor — queueIdx (0~5) 식별. idx 0이 도어 가장 가까움.
    public sealed class QueueSlotAnchor : MonoBehaviour
    {
        [SerializeField] int queueIdx;
        public int QueueIdx => queueIdx;

        void OnValidate()
        {
            if (queueIdx < 0)
                Debug.LogError($"[{name}] QueueSlotAnchor.queueIdx 음수 ({queueIdx})", this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.QueueSlot;
            Gizmos.DrawWireSphere(transform.position, 0.22f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, $"Q{queueIdx}", AnchorGizmo.QueueSlot);
#endif
        }
    }
}
