using UnityEngine;
using Project.Data;
using Project.Domain;
using Project.Utils;

namespace Project.Visual
{
    // 손님 1명 시각 표현 (slim — 책임 분리 후).
    // - 이동/bob: CharacterMover (POCO)
    // - 인내심 파이/주문 말풍선: CustomerUIOverlay (sibling 컴포넌트)
    // - Body sprite + mood 색 + route follower + arrival flags: 본 클래스
    //
    // SceneController 가 호출하는 public API:
    //   Bind(Customer, SceneAnchors), UpdateView(patiencePct, dt), SetBodySprite(Sprite)
    [DisallowMultipleComponent]
    public sealed class CustomerView : MonoBehaviour
    {
        // sr = BgFrame (mood 색), bodySr = Body (Customer PNG)
        [SerializeField] SpriteRenderer sr;
        [SerializeField] SpriteRenderer bodySr;

        // 호환 — 구버전 prefab 의 horizontal patience bar 슬롯. Awake 에서 SetActive(false).
        // pie patience UI 가 대체. 필드는 남겨 두어 prefab 직렬화 호환.
        [SerializeField] GameObject patienceRoot;
        [SerializeField] Transform patienceFgT;
        [SerializeField] SpriteRenderer patienceFgSr;

        [Tooltip("VisualSettings.asset (B1) — 비어있으면 클래스 const 폴백.")]
        [SerializeField] VisualSettingsSO visualSettings;

        Customer customer;
        SceneAnchors anchors;
        CustomerUIOverlay overlay;
        readonly CharacterMover mover = new CharacterMover();

        // ===== Route follower 상태 =====
        // currentRoute = 거쳐갈 waypoint 시퀀스. 마지막 원소가 최종 목적지.
        Vector3[] currentRoute;
        int routeIdx;
        CustomerStage prevStage;
        int prevSeatIdx = -2;
        int prevQueueIdx = -2;
        const float WaypointReachThreshold = 0.18f;

        const float ArrivalDistThreshold = 0.30f;

        static readonly Color ColorIdle    = new Color(0.23f, 0.51f, 0.96f);
        static readonly Color ColorSuccess = new Color(0.13f, 0.77f, 0.37f);
        static readonly Color ColorPFail   = new Color(0.94f, 0.27f, 0.27f);

        public Customer Customer => customer;

        void Awake()
        {
            EnsureSelfRenderer();
            if (patienceRoot != null) patienceRoot.SetActive(false);

            // Overlay sibling — prefab 에 wired 안 됐으면 자동 생성.
            overlay = GetComponent<CustomerUIOverlay>();
            if (overlay == null) overlay = gameObject.AddComponent<CustomerUIOverlay>();

            // VisualSettings 적용 (있으면). Mover 의 기본값을 SO 값으로 덮어씀.
            if (visualSettings != null)
            {
                mover.MaxMoveSpeed = visualSettings.customerMaxSpeed;
                mover.Acceleration = visualSettings.customerAcceleration;
                mover.BobAmpY      = visualSettings.customerBobAmpY;
                mover.SwayAmpX     = visualSettings.customerSwayAmpX;
                mover.StepsPerUnit = visualSettings.stepsPerUnit;
                // Customer 는 idle bob 안 씀 — IdleBobFreq/Amp 무시.
            }
        }

        // Prefab 미사용 시 BgFrame + Body 자동 생성 — SceneBuilder.EnsureCustomerPrefab 와 동일 구조.
        void EnsureSelfRenderer()
        {
            if (transform.localScale == Vector3.one)
                transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            if (sr == null)
            {
                var bg = new GameObject("BgFrame");
                bg.transform.SetParent(transform, false);
                bg.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
                sr = bg.AddComponent<SpriteRenderer>();
            }
            if (sr.sprite == null) sr.sprite = SpriteFactory.RoundedSquare();
            sr.sortingOrder = SortingOrders.CustomerBgFrame;

            if (bodySr == null)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(transform, false);
                body.transform.localScale = Vector3.one;
                bodySr = body.AddComponent<SpriteRenderer>();
            }
            if (bodySr.sprite == null) bodySr.sprite = SpriteFactory.Circle();
            bodySr.sortingOrder = SortingOrders.CustomerBody;
            bodySr.color = Color.white;
        }

        public void SetBodySprite(Sprite s)
        {
            if (bodySr == null) return;
            if (s != null) bodySr.sprite = s;
        }

        public void Bind(Customer c, SceneAnchors anchors)
        {
            customer = c;
            this.anchors = anchors;

            Vector3 startPos;
            if (anchors == null)
            {
                Debug.LogError($"[Customer #{c.Id}] Bind: anchors null — spawn 위치 결정 불가. (0,0)으로 fallback.", this);
                startPos = Vector3.zero;
            }
            else
            {
                startPos = anchors.OutsideSpawnPos;
                if (startPos.sqrMagnitude < 0.01f)
                {
                    Debug.LogWarning($"[Customer #{c.Id}] OutsideSpawnPos=(0,0) — anchor 미발견. fallback (-6, 3.5) 사용.", this);
                    startPos = new Vector3(-6f, 3.5f, 0f);
                }
            }

            transform.localPosition = Vector3.zero;
            transform.position = startPos;
            mover.Reset(startPos);

            currentRoute = null;
            routeIdx = 0;
            prevStage    = (CustomerStage)(-1);
            prevSeatIdx  = -2;
            prevQueueIdx = -2;

            overlay?.Bind(c);
        }

        // patiencePct: 0~1 잔여 비율, 음수면 비활성. dt: SceneController 가 gameSpeed 반영해 전달.
        public void UpdateView(float patiencePct, float dt)
        {
            if (customer == null || anchors == null) return;

            RefreshRouteIfStateChanged();
            AdvanceRouteIfReached();

            Vector3 target = CurrentWaypoint();
            transform.position = mover.Tick(dt, target, enableIdleBob: false);

            if (sr != null) sr.color = ColorFor(customer.Mood);

            overlay?.UpdateOverlay(patiencePct);
            UpdateArrivalFlags();
        }

        // 시각적 도달 여부를 customer POCO에 갱신 — CustomerSystem 의 stage 전환 게이트.
        void UpdateArrivalFlags()
        {
            if (anchors == null) return;

            if (customer.SeatIdx >= 0)
            {
                Vector3 seatPos = anchors.SeatPos(customer.SeatIdx);
                float distSeat = Vector3.Distance(mover.MovePos, seatPos);
                customer.VisuallyAtSeat = distSeat < ArrivalDistThreshold && mover.CurrentSpeed < 0.1f;
            }
            else customer.VisuallyAtSeat = false;

            if (customer.Stage == CustomerStage.Queueing && customer.QueueIdx >= 0)
            {
                Vector3 queuePos = anchors.QueuePos(customer.QueueIdx);
                float distQueue = Vector3.Distance(mover.MovePos, queuePos);
                customer.VisuallyAtQueue = distQueue < ArrivalDistThreshold && mover.CurrentSpeed < 0.1f;
            }
            else customer.VisuallyAtQueue = false;

            if (customer.Stage == CustomerStage.Leaving)
            {
                Vector3 exitPos = anchors.TopExitPos;
                float distExit = Vector3.Distance(mover.MovePos, exitPos);
                customer.VisuallyAtExit = distExit < ArrivalDistThreshold * 2f;
            }
            else customer.VisuallyAtExit = false;
        }

        // ===== Route follower =====
        // 손님은 SceneAnchors.RouteFor() 가 반환한 waypoint 시퀀스를 순서대로 밟아감.
        // Route 재계산 시점: Bind 직후 / Stage / SeatIdx / QueueIdx 변화.

        void RefreshRouteIfStateChanged()
        {
            bool stateChanged = currentRoute == null
                             || customer.Stage   != prevStage
                             || customer.SeatIdx != prevSeatIdx
                             || customer.QueueIdx != prevQueueIdx;
            if (!stateChanged) return;

            currentRoute = anchors.RouteFor(customer, mover.MovePos);
            routeIdx = 0;
            prevStage    = customer.Stage;
            prevSeatIdx  = customer.SeatIdx;
            prevQueueIdx = customer.QueueIdx;
        }

        void AdvanceRouteIfReached()
        {
            if (currentRoute == null || currentRoute.Length == 0) return;
            if (routeIdx >= currentRoute.Length - 1) return;
            float distToWp = Vector3.Distance(mover.MovePos, currentRoute[routeIdx]);
            if (distToWp < WaypointReachThreshold) routeIdx++;
        }

        Vector3 CurrentWaypoint()
        {
            if (currentRoute == null || currentRoute.Length == 0) return mover.MovePos;
            int i = Mathf.Clamp(routeIdx, 0, currentRoute.Length - 1);
            return currentRoute[i];
        }

        // Scene view 디버그 — 손님 GameObject 선택 시 route line + waypoint sphere 시각화.
        void OnDrawGizmosSelected()
        {
            if (currentRoute == null || currentRoute.Length == 0) return;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            int i = Mathf.Clamp(routeIdx, 0, currentRoute.Length - 1);
            Gizmos.DrawLine(transform.position, currentRoute[i]);
            Gizmos.DrawWireSphere(currentRoute[i], 0.12f);

            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.7f);
            for (int j = i; j < currentRoute.Length - 1; j++)
            {
                Gizmos.DrawLine(currentRoute[j], currentRoute[j + 1]);
                Gizmos.DrawWireSphere(currentRoute[j + 1], 0.10f);
            }
        }

        static Color ColorFor(CustomerMood mood)
        {
            switch (mood)
            {
                case CustomerMood.Success: return ColorSuccess;
                case CustomerMood.PFail:
                case CustomerMood.QFail:   return ColorPFail;
                default:                   return ColorIdle;
            }
        }
    }
}
