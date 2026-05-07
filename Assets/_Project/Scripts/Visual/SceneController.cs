using System.Collections.Generic;
using UnityEngine;
using Project.Core;
using Project.Data;
using Project.Domain;
using Project.Utils;

namespace Project.Visual
{
    // 시각 표현 동기화. 정적 객체(테이블/좌석/스테이션/Zone)는 EditorScript가 미리 씬에 배치.
    // SceneController는 매 프레임 POCO Customer/Staff → SpriteRenderer GameObject 동기화 + 좌석 색 갱신만 담당.
    //
    // Note: Object Pool 제거 (2026-05-05) — Pool 재사용 시 transform.position 잔여로 인한 spawn 위치 버그 가능성 차단.
    //   현재 게임 규모(동시 손님 6명) 에서 Instantiate/Destroy GC alloc은 무시할 수준.
    //   M2 단계 이후 손님 수 증가 시 풀링 재도입 고려.
    public sealed class SceneController : MonoBehaviour
    {
        [SerializeField] GameObject customerPrefab;
        [SerializeField] GameObject staffPrefab;
        [SerializeField] SceneAnchors anchors;
        [Tooltip("VisualSettings.asset (B1) — RevenuePopup 색상/사이즈/lifeSec 등 외부화. 비어있으면 const 폴백.")]
        [SerializeField] VisualSettingsSO visualSettings;

        GameManager gm;

        readonly Dictionary<int, CustomerView> customerViews = new();
        readonly Dictionary<int, StaffView> staffViews = new();
        readonly Dictionary<int, Domain.CustomerMood> prevMoods = new();
        readonly HashSet<int> aliveCustomers = new();
        readonly List<int> deadCustomers = new();

        SpriteRenderer[] seatRenderers;
        SpriteRenderer[] seatOutlineRenderers; // cap 안: 진한 외곽선 / 밖: 옅은 외곽선

        static readonly Color SeatActive       = new Color(0.85f, 0.83f, 0.80f);
        static readonly Color SeatLocked       = new Color(0.92f, 0.92f, 0.92f);
        static readonly Color SeatActiveOutline = new Color(0.47f, 0.45f, 0.42f);
        static readonly Color SeatLockedOutline = new Color(0.78f, 0.78f, 0.78f);
        static readonly Color RevenueColor      = new Color(0.13f, 0.65f, 0.30f);

        void Start()
        {
            gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[SceneController] GameManager를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }
            if (anchors == null)
            {
                anchors = Object.FindFirstObjectByType<SceneAnchors>();
                if (anchors == null)
                {
                    Debug.LogError("[SceneController] SceneAnchors 미발견. SceneBuilder 재실행 또는 Inspector에서 직접 연결.", this);
                    enabled = false;
                    return;
                }
            }
            CacheSeats();
        }

        void CacheSeats()
        {
            int max = gm.Config.SeatingMax;
            seatRenderers = new SpriteRenderer[max];
            seatOutlineRenderers = new SpriteRenderer[max];
            for (int i = 0; i < max; i++)
            {
                var go = GameObject.Find($"Seat_{i}");
                if (go == null) continue;
                seatRenderers[i] = go.GetComponent<SpriteRenderer>();

                // 외곽선 자식 자동 추가 (이미 있으면 재사용)
                Transform outlineT = go.transform.Find("Outline");
                if (outlineT == null)
                {
                    var outlineGo = new GameObject("Outline");
                    outlineGo.transform.SetParent(go.transform, false);
                    outlineGo.transform.localPosition = Vector3.zero;
                    outlineGo.transform.localScale = new Vector3(1.18f, 1.18f, 1f); // 살짝 큼 = 테두리 효과
                    var outSr = outlineGo.AddComponent<SpriteRenderer>();
                    outSr.sprite = Utils.SpriteFactory.Circle();
                    outSr.color = SeatActiveOutline;
                    // sortingOrder는 부모(Seat=-5)보다 작게 → 부모 뒤
                    outSr.sortingOrder = SortingOrders.SeatOutline;
                    seatOutlineRenderers[i] = outSr;
                }
                else
                {
                    seatOutlineRenderers[i] = outlineT.GetComponent<SpriteRenderer>();
                }
            }
        }

        void LateUpdate()
        {
            if (gm == null) return;
            // 게임 속도 적용 dt — 일시정지 시 0, 가속 시 배율 적용. 시각도 게임 속도 따라감.
            float dt = Time.deltaTime * gm.GameSpeed;
            SyncCustomers(dt);
            SyncStaff(dt);
            UpdateSeatTints();
        }

        void UpdateSeatTints()
        {
            if (seatRenderers == null) return;
            int cap = Formulas.SeatingCap(gm.State.VenueLv, gm.Config);
            for (int i = 0; i < seatRenderers.Length; i++)
            {
                if (seatRenderers[i] != null)
                    seatRenderers[i].color = i < cap ? SeatActive : SeatLocked;
                if (seatOutlineRenderers != null && seatOutlineRenderers[i] != null)
                    seatOutlineRenderers[i].color = i < cap ? SeatActiveOutline : SeatLockedOutline;
            }
        }

        void SyncCustomers(float dt)
        {
            aliveCustomers.Clear();
            var list = gm.CustomerSystem.Customers;
            float patienceMult = Formulas.PatienceMult(gm.State.ServiceLv, gm.Config);
            float queueLimit = (gm.Config.TPatienceQueue / 1000f) * patienceMult;
            float seatedLimit = (gm.Config.TPatienceSeated / 1000f) * patienceMult;

            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                aliveCustomers.Add(c.Id);
                if (!customerViews.TryGetValue(c.Id, out var view))
                {
                    view = CreateCustomerView(c);
                    customerViews[c.Id] = view;
                    prevMoods[c.Id] = Domain.CustomerMood.None;
                }
                view.UpdateView(ComputePatiencePct(c, queueLimit, seatedLimit), dt);

                // mood 전이 감지 — Success 진입 시 수익 popup 트리거 (Passive 팁과 무관, 주문 결제)
                if (prevMoods.TryGetValue(c.Id, out var prev) && prev != c.Mood)
                {
                    if (c.Mood == Domain.CustomerMood.Success && c.LastRevenue > 0 && c.SeatIdx >= 0)
                    {
                        SpawnRevenuePopup(c.SeatIdx, c.LastRevenue);
                    }
                }
                prevMoods[c.Id] = c.Mood;
            }

            deadCustomers.Clear();
            foreach (var id in customerViews.Keys)
                if (!aliveCustomers.Contains(id)) deadCustomers.Add(id);
            for (int i = 0; i < deadCustomers.Count; i++)
            {
                var id = deadCustomers[i];
                if (customerViews.TryGetValue(id, out var v) && v != null)
                    Destroy(v.gameObject);
                customerViews.Remove(id);
                prevMoods.Remove(id);
            }
        }

        void SpawnRevenuePopup(int seatIdx, int amount)
        {
            var go = new GameObject($"RevenuePopup_+{amount}");
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<RevenuePopupView>();
            var pos = anchors.SeatPos(seatIdx);
            pos.y += 0.6f; // 머리 위
            // VisualSettings 의 색/사이즈 사용 (있으면). 없으면 폴백 색.
            Color color = visualSettings != null ? visualSettings.popupColor : RevenueColor;
            view.Show(pos, amount, color, visualSettings);
        }

        void SyncStaff(float dt)
        {
            var list = gm.StaffSystem.Staffs;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (!staffViews.TryGetValue(s.Id, out var view))
                {
                    view = CreateStaffView(s, i);
                    staffViews[s.Id] = view;
                }
                view.UpdateView(dt);
            }
        }

        static float ComputePatiencePct(Customer c, float queueLimit, float seatedLimit)
        {
            if (!c.WaitActive) return -1f;
            float limit = c.Stage == CustomerStage.Queueing ? queueLimit : seatedLimit;
            if (limit <= 0f) return -1f;
            return Mathf.Clamp01(1f - c.WaitElapsed / limit);
        }

        // 단순 Instantiate + Bind. Pool 없음 — Bind에서 transform.position 명시 설정해 spawn 위치 보장.
        CustomerView CreateCustomerView(Customer c)
        {
            GameObject go = customerPrefab != null
                ? Instantiate(customerPrefab, transform)
                : NewEmpty("Customer (auto)");
            go.name = $"Customer_{c.Id}";
            var view = go.GetComponent<CustomerView>();
            if (view == null) view = go.AddComponent<CustomerView>();
            view.Bind(c, anchors);

            var sprites = gm.Config.CustomerBodySprites;
            if (sprites.Count > 0)
                view.SetBodySprite(sprites[c.SpriteIdx % sprites.Count]);
            return view;
        }

        StaffView CreateStaffView(Staff s, int idleSlot)
        {
            GameObject go = staffPrefab != null
                ? Instantiate(staffPrefab, transform)
                : NewEmpty("Staff (auto)");
            go.name = $"Staff_{s.Id}_{s.Name}";
            var view = go.GetComponent<StaffView>();
            if (view == null) view = go.AddComponent<StaffView>();
            view.Bind(s, idleSlot, anchors);
            return view;
        }

        GameObject NewEmpty(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }
    }
}
