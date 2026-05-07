using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Visual
{
    // SeatAnchor만 이 파일에 유지 — 기존 씬의 6개 Seat_* GameObject가 이 파일의 GUID + fileID 11500000 를 참조하기 때문.
    // 다른 anchor 클래스들은 단일-클래스 파일로 분리됨 (StationAnchor.cs, DoorAnchor.cs 등).
    // 단일-클래스 파일 패턴 = Unity MonoScript serialization 안정성 보장.

    // 좌석 anchor — seatIdx (0~5) 식별
    public sealed class SeatAnchor : MonoBehaviour
    {
        [SerializeField] int seatIdx;
        public int SeatIdx => seatIdx;

        void OnValidate()
        {
            if (seatIdx < 0)
                Debug.LogError($"[{name}] SeatAnchor.seatIdx 음수 ({seatIdx}) — 0 이상이어야 함", this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = AnchorGizmo.Seat;
            Gizmos.DrawWireSphere(transform.position, 0.30f);
#if UNITY_EDITOR
            AnchorGizmo.DrawLabel(transform.position, $"S{seatIdx}", AnchorGizmo.Seat);
#endif
        }
    }
}
