using UnityEngine;
using Project.Data;
using Project.Domain;
using Project.Utils;

namespace Project.Visual
{
    // 직원 1명 시각 표현 (slim — 책임 분리 후).
    // - 이동/bob: CharacterMover (POCO, idle bob 모드 활성)
    // - 본 클래스: state 기반 target world 계산 + body sprite 자체.
    [DisallowMultipleComponent]
    public sealed class StaffView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sr;
        [Tooltip("VisualSettings.asset (B1) — 비어있으면 클래스 const 폴백.")]
        [SerializeField] VisualSettingsSO visualSettings;

        Staff staff;
        int idleSlot;
        SceneAnchors anchors;
        readonly CharacterMover mover = new CharacterMover
        {
            // 직원 기본값 (VisualSettings 없을 때 폴백).
            MaxMoveSpeed = 1.1f,
            Acceleration = 3f,
            BobAmpY = 0.19f,
        };

        public Staff Staff => staff;

        void Awake()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            if (sr.sprite == null) sr.sprite = SpriteFactory.Circle();
            // Prefab 에서 BgFrame/Body sortingOrder 미리 설정. Awake 는 sr 이 있을 때만 보정 (구버전 prefab 호환).
            if (sr.sortingOrder < SortingOrders.StaffBody) sr.sortingOrder = SortingOrders.StaffBody;
            if (transform.localScale == Vector3.one)
                transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            if (visualSettings != null)
            {
                mover.MaxMoveSpeed = visualSettings.staffMaxSpeed;
                mover.Acceleration = visualSettings.staffAcceleration;
                mover.BobAmpY      = visualSettings.staffBobAmpY;
                mover.SwayAmpX     = visualSettings.staffSwayAmpX;
                mover.StepsPerUnit = visualSettings.stepsPerUnit;
                mover.IdleBobFreq  = visualSettings.idleBobFreq;
                mover.IdleBobAmp   = visualSettings.idleBobAmp;
            }
        }

        public void Bind(Staff s, int idleSlot, SceneAnchors anchors)
        {
            staff = s;
            this.idleSlot = idleSlot;
            this.anchors = anchors;
            int slot = anchors != null && anchors.StaffSlotCount > 0
                ? Mathf.Clamp(idleSlot, 0, anchors.StaffSlotCount - 1) : 0;
            Vector3 startPos = anchors != null ? anchors.StaffSlotPos(slot) : Vector3.zero;
            transform.position = startPos;
            mover.Reset(startPos);
        }

        // dt: SceneController 가 gameSpeed 반영해 전달. 일시정지 시 시각/idle bob 모두 정지.
        public void UpdateView(float dt)
        {
            if (staff == null || anchors == null) return;
            Vector3 target = TargetWorld(staff, idleSlot, anchors);
            // Idle 상태에서 정지하면 호흡 같은 미세 bob 활성.
            transform.position = mover.Tick(dt, target, enableIdleBob: staff.State == StaffState.Idle);
        }

        static Vector3 TargetWorld(Staff s, int idleSlot, SceneAnchors anchors)
        {
            int slotsLen = anchors.StaffSlotCount;
            int slot = slotsLen > 0 ? Mathf.Clamp(idleSlot, 0, slotsLen - 1) : 0;
            Vector3 idleSlotPos = anchors.StaffSlotPos(slot);

            switch (s.State)
            {
                case StaffState.Idle:
                case StaffState.Returning:
                case StaffState.ReturningToKitchen:
                    return idleSlotPos;

                case StaffState.TakingOrderMove:
                case StaffState.TakingOrder:
                    return s.TargetSeatIdx >= 0 ? anchors.SeatPos(s.TargetSeatIdx) : idleSlotPos;

                case StaffState.CookingMove:
                case StaffState.CookingItem:
                    return s.CurrentStation != null ? anchors.StationPos(s.CurrentStation) : idleSlotPos;

                case StaffState.ServingMove:
                case StaffState.Serving:
                    return s.TargetSeatIdx >= 0 ? anchors.ServePos(s.TargetSeatIdx) : idleSlotPos;

                default:
                    return idleSlotPos;
            }
        }
    }
}
