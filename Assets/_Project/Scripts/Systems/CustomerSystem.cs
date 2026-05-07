using System.Collections.Generic;
using UnityEngine;
using Project.Core;
using Project.Data;
using Project.Domain;

namespace Project.Systems
{
    // 손님 spawn + FSM 진행. HTML 프로토 v0.15.1의 tick() 손님 부분을 Unity로 이식.
    // POCO (MonoBehaviour 아님). GameManager가 인스턴스 보유하고 매 Update에서 Tick(dt) 호출.
    //
    // 시간 단위는 내부에서 모두 "초"로 통일 (HTML의 ms를 변환).
    // 직원 시스템은 Day 5+ 추가 예정 — 지금은 seated_ordering에서 자동 진행되지 않아
    // 모든 손님이 인내심 timeout으로 떠나는 것이 정상 동작.
    public sealed class CustomerSystem
    {
        readonly GameState state;
        readonly GameConfigSO config;
        readonly IRandomProvider random;

        readonly List<Customer> customers = new();
        // FindById O(1) 조회. Spawn/Remove 와 동기 유지. List 와 별도지만 항상 일관.
        readonly Dictionary<int, Customer> customerById = new();

        // 좌석 배열. 길이 = SeatingMax (고정 6). value = 점유 중인 손님 Id, null이면 빈 자리.
        // 활성 영역은 SeatingCap()까지만 사용 (매장 Lv에 따라 동적).
        readonly int?[] seats;

        // 주문 대기 큐 (앉아서 직원을 기다리는 손님 Id 목록). FIFO.
        // Day 5+ StaffSystem이 idle 직원과 매칭할 때 사용.
        readonly List<int> waitingOrderQueue = new();

        int nextCustomerId = 1;

        // 다음 손님 도착까지 남은 시간 (초). 0 이하가 되면 spawn 시도.
        float arrivalTimer;

        // 게임 시작 직후 첫 손님이 너무 늦게 들어오지 않도록 짧게 설정 (HTML 프로토와 동일 1.5s).
        const float FirstArrivalDelaySec = 1.5f;

        public IReadOnlyList<Customer> Customers => customers;
        public IReadOnlyList<int> WaitingOrderQueue => waitingOrderQueue;
        public IReadOnlyList<int?> Seats => seats;

        public CustomerSystem(GameState state, GameConfigSO config, IRandomProvider random)
        {
            this.state = state;
            this.config = config;
            this.random = random;
            seats = new int?[config.SeatingMax];
            arrivalTimer = FirstArrivalDelaySec;
        }

        // ===== 외부 API =====

        public void Tick(float dt)
        {
            // 1) Spawn: 도착 간격마다 손님 1명 시도 (만석+큐만석이면 스킵, 간격은 진행)
            arrivalTimer -= dt;
            while (arrivalTimer <= 0f)
            {
                TrySpawn();
                arrivalTimer += ArrivalIntervalSec();
            }

            // 2) 모든 손님 stage / 인내심 진행
            for (int i = 0; i < customers.Count; i++)
            {
                var c = customers[i];
                c.StageElapsed += dt;
                if (c.WaitActive) c.WaitElapsed += dt;
                // SettleElapsed 는 좌석에 visually 도달한 후에만 누적 (FSM Seating 단계에서)
                if (c.Stage == CustomerStage.Seating && c.VisuallyAtSeat)
                    c.SettleElapsed += dt;
                ProcessStage(c);
            }

            // 3) Leaving 종료된 손님 제거 — VisuallyAtExit (TopExit 도달) OR safety timeout (TLeaving×20).
            //    TLeaving (HTML 기본 ~1.5s) 만으로 제거 시 손님이 도어 부근에서 사라짐 → 시각 도달 우선.
            float leavingMaxSec = (config.TLeaving / 1000f) * 20f;
            customers.RemoveAll(c =>
            {
                bool drop = c.Stage == CustomerStage.Leaving && (c.VisuallyAtExit || c.StageElapsed >= leavingMaxSec);
                if (drop) customerById.Remove(c.Id);
                return drop;
            });
        }

        // Day 전환 시 호출 — 모든 손님 제거, 좌석 비우고, 첫 손님 1.5s 후로 재설정.
        // 누적 통계(state.Successes 등)는 건드리지 않음.
        public void ResetForNewDay()
        {
            customers.Clear();
            customerById.Clear();
            waitingOrderQueue.Clear();
            for (int i = 0; i < seats.Length; i++) seats[i] = null;
            arrivalTimer = FirstArrivalDelaySec;
        }

        // 손님 Id로 인스턴스 조회. 없으면 null. O(1) Dictionary 조회.
        public Customer FindById(int id)
        {
            customerById.TryGetValue(id, out var c);
            return c;
        }

        // 주문 대기열의 맨 앞 손님 Id를 꺼냄 (FIFO). 비었으면 -1.
        // StaffSystem이 idle 직원과 매칭할 때 사용.
        public int DequeueWaitingOrder()
        {
            if (waitingOrderQueue.Count == 0) return -1;
            int id = waitingOrderQueue[0];
            waitingOrderQueue.RemoveAt(0);
            return id;
        }

        // 주문 생성. HTML generateOrder() 그대로:
        //   - GenerateProb >= 1.0 인 메뉴는 항상 포함 (hotdog)
        //   - 그 외는 확률적으로 추가 (cola 50%, salad 30%)
        // 직원 시스템에서 호출 예정 (Day 5+). 현재는 미사용이지만 미리 작성해 검증.
        public List<MenuItemSO> GenerateOrder()
        {
            var order = new List<MenuItemSO>();
            var menu = config.MenuItems;
            for (int i = 0; i < menu.Count; i++)
            {
                var item = menu[i];
                if (item.GenerateProb >= 1f || random.Value() < item.GenerateProb)
                    order.Add(item);
            }
            // 안전망: 모든 확률이 1 미만이고 모두 실패한 경우 첫 메뉴 강제. 현재 데이터에선 발생 X.
            if (order.Count == 0 && menu.Count > 0) order.Add(menu[0]);
            return order;
        }

        // ===== 내부 로직 =====

        void TrySpawn()
        {
            // 좌석이 비면 seat-bound (문→좌석 경로), 만석이면 queue-bound (큐 위치로 직행).
            // 의도를 spawn 시점에 결정해 Arriving 단계에서 어색한 백트래킹(문→큐) 방지.
            bool queueBound = FindFreeSeat() < 0;
            if (queueBound && EffectiveQueueOccupancy() >= QueueCap()) return; // 만석 + 큐 가득 → 안 받음

            int spriteCount = config.CustomerBodySprites.Count;
            var c = new Customer
            {
                Id = nextCustomerId++,
                Stage = CustomerStage.Arriving,
                StageElapsed = 0f,
                IsQueueBound = queueBound,
                SpriteIdx = spriteCount > 0 ? random.Range(0, spriteCount) : 0,
            };
            customers.Add(c);
            customerById[c.Id] = c;
            if (queueBound) ReassignQueueIndices(); // 잠정 QueueIdx 부여 (queueing + arriving qb 통합 ordering)
            Debug.Log($"[Customer #{c.Id}] arriving (queueBound={queueBound})");
        }

        // Stage 별 dispatch — 각 case 본문은 Tick<Stage> 메서드로 추출.
        // State Pattern 까지는 오버엔지니어링 (8 case, 추가 가능성 낮음). 메서드 추출로 가독성/디버깅 개선.
        void ProcessStage(Customer c)
        {
            switch (c.Stage)
            {
                case CustomerStage.Arriving:        TickArriving(c); break;
                case CustomerStage.Queueing:        TickQueueing(c); break;
                case CustomerStage.Seating:         TickSeating(c); break;
                case CustomerStage.SeatedOrdering:  break; // 직원 처리 대기 — 자체 timer 없음
                case CustomerStage.SeatedWaiting:   break; // 동상
                case CustomerStage.Eating:          TickEating(c); break;
                case CustomerStage.Result:          TickResult(c); break;
                case CustomerStage.Leaving:         break; // Tick 끝의 RemoveAll에서 제거
            }
        }

        void TickArriving(Customer c)
        {
            if (c.StageElapsed < config.TArriving / 1000f) return;
            bool wasQueueBound = c.IsQueueBound;
            int free = FindFreeSeat();
            if (free >= 0) EnterSeat(c, free);
            else if (CountQueueing() < QueueCap()) EnterQueue(c);
            else
            {
                // 안전망: TrySpawn이 막아주므로 실제로는 거의 진입 X
                StartLeaving(c);
                Debug.LogWarning($"[Customer #{c.Id}] arriving 후 자리/큐 모두 가득 → leaving");
            }
            // queue-bound 였던 손님이 stage 전환되면 뒤따르는 arriving qb 손님들의 잠정 idx 재계산
            if (wasQueueBound) ReassignQueueIndices();
        }

        void TickQueueing(Customer c)
        {
            // 큐 슬롯에 visually 도달했고 아직 인내심 미시작 → 시작.
            //   walk 중에는 WaitActive=false 유지 → 인내심 UI(Pie) 도 자동으로 안 보임.
            //   QueueIdx가 바뀌어 새 슬롯으로 walk할 때도 한번 켜진 WaitActive는 유지(WaitElapsed 누적 보존).
            if (!c.WaitActive && c.VisuallyAtQueue)
            {
                c.WaitActive = true;
                c.WaitElapsed = 0f;
            }

            // 줄 맨 앞 + 좌석 빔 → 승격
            if (c.QueueIdx == 0)
            {
                int free = FindFreeSeat();
                if (free >= 0)
                {
                    EnterSeat(c, free);
                    ReassignQueueIndices();
                    return;
                }
            }
            if (!c.WaitActive) return;
            float queueLimit = (config.TPatienceQueue / 1000f) * PatienceMult();
            if (c.WaitElapsed >= queueLimit)
            {
                c.Mood = CustomerMood.PFail;
                state.PatienceFails++;
                StartLeaving(c);
                ReassignQueueIndices();
                Debug.Log($"[Customer #{c.Id}] pfail (queue) → leaving");
            }
        }

        void TickSeating(Customer c)
        {
            // 좌석에 visually 도달 + 정착(settle) 시간 경과 시 SeatedOrdering 으로 전환.
            //   VisuallyAtSeat 게이트가 없으면 손님이 걷는 동안 직원이 빈 좌석에 가서 응대 시작 버그 발생.
            if (!c.VisuallyAtSeat) return;
            if (c.SettleElapsed < config.TSeatingSettle / 1000f) return;

            c.Stage = CustomerStage.SeatedOrdering;
            c.StageElapsed = 0f;
            c.SettleElapsed = 0f;
            c.WaitElapsed = 0f;
            c.WaitActive = false; // 좌석에 앉으면 인내심 카운트 안 함 (Idle 톤). 인내심은 큐에서만 작동.
            // 주문을 SeatedOrdering 진입 시 미리 생성 → 손님 머리 위 말풍선에 즉시 표시 가능.
            if (c.Order == null) c.Order = GenerateOrder();
            waitingOrderQueue.Add(c.Id);
        }

        void TickEating(Customer c)
        {
            float eatTime = (config.TEating / 1000f) * Formulas.EatingSpdMult(state.ServiceLv, config);
            if (c.StageElapsed < eatTime) return;

            int revenue = Formulas.OrderTotalPrice(c.Order, state.MenuLv, config);
            state.Cash += revenue;
            state.TotalRevenue += revenue;
            state.DayRevenue += revenue;
            state.Successes++;
            c.LastRevenue = revenue;
            c.Mood = CustomerMood.Success;
            c.Stage = CustomerStage.Result;
            c.StageElapsed = 0f;
            Debug.Log($"[Customer #{c.Id}] success +${revenue} → result");
        }

        void TickResult(Customer c)
        {
            if (c.StageElapsed < config.TResult / 1000f) return;
            if (c.SeatIdx >= 0)
            {
                seats[c.SeatIdx] = null;
                c.SeatIdx = -1;
            }
            StartLeaving(c);
        }

        void EnterSeat(Customer c, int seatIdx)
        {
            c.Stage = CustomerStage.Seating;
            c.StageElapsed = 0f;
            c.SettleElapsed = 0f;       // 좌석 visual 도달 후부터 settle 누적 시작
            c.VisuallyAtSeat = false;   // 좌석 idx만 잡혔을 뿐 아직 visually 도달 X — CustomerView가 갱신
            c.SeatIdx = seatIdx;
            c.QueueIdx = -1;
            c.IsQueueBound = false;
            c.WaitElapsed = 0f;
            c.WaitActive = false;
            seats[seatIdx] = c.Id;
        }

        void EnterQueue(Customer c)
        {
            c.Stage = CustomerStage.Queueing;
            c.StageElapsed = 0f;
            c.IsQueueBound = false; // Stage 자체로 queueing 표현되므로 플래그는 정리
            c.QueueIdx = CountQueueing(); // 마지막 위치 (직전 ReassignQueueIndices 결과와 일치)
            c.WaitElapsed = 0f;
            c.WaitActive = false; // 큐 슬롯에 visually 도달하기 전엔 인내심 카운트 안 함. Queueing tick이 도착 시 켬.
            c.VisuallyAtQueue = false;
        }

        void StartLeaving(Customer c)
        {
            c.Stage = CustomerStage.Leaving;
            c.StageElapsed = 0f;
            c.WaitActive = false;
            c.QueueIdx = -1;
            c.IsQueueBound = false;
        }

        // 큐의 손님들에 0,1,2,... 순서 재부여. customers 리스트 순서 = id 순서 = 도착 순서.
        // Arriving + IsQueueBound 손님도 통합해서 인덱싱 (도착 중이지만 줄 자리 예약된 상태) → 큐 시각 표시 일관성.
        void ReassignQueueIndices()
        {
            int idx = 0;
            for (int i = 0; i < customers.Count; i++)
            {
                var c = customers[i];
                bool inQueueLine = c.Stage == CustomerStage.Queueing
                                || (c.Stage == CustomerStage.Arriving && c.IsQueueBound);
                if (inQueueLine) c.QueueIdx = idx++;
            }
        }

        int FindFreeSeat()
        {
            int cap = SeatingCap();
            for (int i = 0; i < cap; i++)
                if (seats[i] == null) return i;
            return -1;
        }

        bool AllSeatsTaken()
        {
            int cap = SeatingCap();
            for (int i = 0; i < cap; i++)
                if (seats[i] == null) return false;
            return true;
        }

        int CountQueueing()
        {
            int n = 0;
            for (int i = 0; i < customers.Count; i++)
                if (customers[i].Stage == CustomerStage.Queueing) n++;
            return n;
        }

        // 큐 점유 (실제 + arriving 단계 queue-bound 예약). spawn 시 큐 cap 검사용.
        int EffectiveQueueOccupancy()
        {
            int n = 0;
            for (int i = 0; i < customers.Count; i++)
            {
                var c = customers[i];
                if (c.Stage == CustomerStage.Queueing) n++;
                else if (c.Stage == CustomerStage.Arriving && c.IsQueueBound) n++;
            }
            return n;
        }

        // ===== 헬퍼 (config / state 동적 값) =====
        float ArrivalIntervalSec() => config.ArrivalIntervalMs / 1000f;
        int SeatingCap() => Formulas.SeatingCap(state.VenueLv, config);
        int QueueCap()   => Formulas.QueueCap(state.VenueLv, config);
        float PatienceMult() => Formulas.PatienceMult(state.ServiceLv, config);
    }
}
