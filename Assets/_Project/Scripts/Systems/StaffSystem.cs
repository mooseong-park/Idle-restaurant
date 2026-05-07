using System.Collections.Generic;
using UnityEngine;
using Project.Core;
using Project.Data;
using Project.Domain;

namespace Project.Systems
{
    // 직원 spawn + FSM 진행. HTML 프로토 v0.15.1의 tick() 직원 부분을 Unity로 이식.
    // CustomerSystem에 단방향 의존 — 직원이 손님 stage를 직접 변경.
    public sealed class StaffSystem
    {
        readonly GameState state;
        readonly GameConfigSO config;
        readonly CustomerSystem customerSystem;

        readonly List<Staff> staffList = new();
        int nextStaffId = 1;

        public IReadOnlyList<Staff> Staffs => staffList;

        public StaffSystem(GameState state, GameConfigSO config, CustomerSystem customerSystem)
        {
            this.state = state;
            this.config = config;
            this.customerSystem = customerSystem;
        }

        // ===== 외부 API =====

        public Staff SpawnStaff(RarityConfigSO rarity, string name)
        {
            var s = new Domain.Staff
            {
                Id = nextStaffId++,
                Name = name,
                Rarity = rarity,
                State = StaffState.Idle,
                StateElapsed = 0f,
            };
            staffList.Add(s);
            Debug.Log($"[Staff #{s.Id} {s.Name}] spawned ({rarity.DisplayName})");
            return s;
        }

        // Day 전환 시 호출 — 전 직원을 즉시 Idle로 (조리 중이든 서빙 중이든 무조건).
        public void ResetAllToIdle()
        {
            for (int i = 0; i < staffList.Count; i++)
            {
                var s = staffList[i];
                s.State = StaffState.Idle;
                s.StateElapsed = 0f;
                s.TargetSeatIdx = -1;
                s.TargetCustomerId = -1;
                s.Order = null;
                s.CookingStepIdx = 0;
                s.CurrentStation = null;
            }
        }

        public void Tick(float dt)
        {
            // 1) 타겟 손님이 사라지거나 leaving이면 직원 cleanup (단방향 의존 유지)
            for (int i = 0; i < staffList.Count; i++) ResetIfTargetLost(staffList[i]);

            // 2) 각 직원 FSM 진행
            for (int i = 0; i < staffList.Count; i++)
            {
                var s = staffList[i];
                s.StateElapsed += dt;
                ProcessState(s, dt);
            }

            // 3) idle 직원 ↔ 주문 대기 손님 매칭
            AssignStaffToTakeOrder();
        }

        // ===== 내부 로직 =====

        void ResetIfTargetLost(Staff s)
        {
            if (s.TargetCustomerId < 0) return;
            var c = customerSystem.FindById(s.TargetCustomerId);
            if (c == null || c.Stage == CustomerStage.Leaving)
            {
                // 손님이 인내심 timeout 등으로 사라짐 → 직원 즉시 returning으로
                ResetTo(s, StaffState.Returning);
            }
        }

        // 9 state dispatch — 각 state 본문을 별도 메서드로 추출해 가독성 ↑.
        void ProcessState(Staff s, float dt)
        {
            float spdMult = Formulas.StaffSpdMult(state.StaffLv, config);
            float moveSec = (s.Rarity.MoveTimeMs / 1000f) * spdMult;

            switch (s.State)
            {
                case StaffState.Idle:                                          return; // 외부(AssignStaffToTakeOrder)가 상태 변경
                case StaffState.TakingOrderMove:    TickTakingOrderMove(s, moveSec); break;
                case StaffState.TakingOrder:        TickTakingOrder(s); break;
                case StaffState.ReturningToKitchen: TickReturningToKitchen(s, moveSec); break;
                case StaffState.CookingMove:        TickCookingMove(s, moveSec); break;
                case StaffState.CookingItem:        TickCookingItem(s, spdMult); break;
                case StaffState.ServingMove:        TickServingMove(s, moveSec); break;
                case StaffState.Serving:            TickServing(s); break;
                case StaffState.Returning:          TickReturning(s); break;
            }
        }

        void TickTakingOrderMove(Staff s, float moveSec)
        {
            if (s.StateElapsed < moveSec) return;
            var c = customerSystem.FindById(s.TargetCustomerId);
            if (c == null) { ResetTo(s, StaffState.Returning); return; }

            // 손님 자리에 도착 — 주문 받기 시작.
            //   c.Order 는 이미 CustomerSystem 의 SeatedOrdering 진입 시 미리 생성됐음 (말풍선 표시용).
            //   직원은 그 주문을 사본으로 받아감. null 이면 안전망으로 신규 생성.
            if (c.Order == null) c.Order = customerSystem.GenerateOrder();
            s.Order = new List<MenuItemSO>(c.Order);
            c.WaitActive = false;
            c.WaitElapsed = 0f;
            s.State = StaffState.TakingOrder;
            s.StateElapsed = 0f;
        }

        void TickTakingOrder(Staff s)
        {
            if (s.StateElapsed < config.TTakingOrder / 1000f) return;
            var c = customerSystem.FindById(s.TargetCustomerId);
            if (c != null && c.Stage == CustomerStage.SeatedOrdering)
            {
                c.Stage = CustomerStage.SeatedWaiting;
                c.StageElapsed = 0f;
            }
            s.State = StaffState.ReturningToKitchen;
            s.StateElapsed = 0f;
        }

        void TickReturningToKitchen(Staff s, float moveSec)
        {
            if (s.StateElapsed < moveSec) return;
            s.CookingStepIdx = 0;
            StartCookingNextItem(s);
        }

        void TickCookingMove(Staff s, float moveSec)
        {
            if (s.StateElapsed < moveSec) return;
            s.State = StaffState.CookingItem;
            s.StateElapsed = 0f;
        }

        void TickCookingItem(Staff s, float spdMult)
        {
            var item = s.CurrentStation;
            if (item == null) { ResetTo(s, StaffState.Returning); return; }
            float cookSec = (item.CookTimeMs / 1000f) * s.Rarity.CookMult * spdMult;
            if (s.StateElapsed < cookSec) return;
            s.CookingStepIdx++;
            StartCookingNextItem(s);
        }

        void TickServingMove(Staff s, float moveSec)
        {
            if (s.StateElapsed < moveSec) return;
            var c = customerSystem.FindById(s.TargetCustomerId);
            if (c != null && c.Stage == CustomerStage.SeatedWaiting)
            {
                c.Stage = CustomerStage.Eating;
                c.StageElapsed = 0f;
                c.WaitActive = false;
                c.WaitElapsed = 0f;
            }
            s.State = StaffState.Serving;
            s.StateElapsed = 0f;
        }

        void TickServing(Staff s)
        {
            if (s.StateElapsed < config.TServeAction / 1000f) return;
            ResetTo(s, StaffState.Returning);
        }

        void TickReturning(Staff s)
        {
            // HTML은 returning에서만 spdMult 미적용 — 1:1 재현
            if (s.StateElapsed < s.Rarity.MoveTimeMs / 1000f) return;
            s.State = StaffState.Idle;
            s.StateElapsed = 0f;
        }

        void StartCookingNextItem(Staff s)
        {
            if (s.Order == null || s.CookingStepIdx >= s.Order.Count)
            {
                // 모든 메뉴 조리 완료 → 서빙
                s.State = StaffState.ServingMove;
                s.StateElapsed = 0f;
                s.CurrentStation = null;
                return;
            }
            s.CurrentStation = s.Order[s.CookingStepIdx];
            s.State = StaffState.CookingMove;
            s.StateElapsed = 0f;
        }

        void ResetTo(Staff s, StaffState newState)
        {
            s.State = newState;
            s.StateElapsed = 0f;
            s.TargetSeatIdx = -1;
            s.TargetCustomerId = -1;
            s.Order = null;
            s.CookingStepIdx = 0;
            s.CurrentStation = null;
        }

        void AssignStaffToTakeOrder()
        {
            while (true)
            {
                var idle = FindIdleStaff();
                if (idle == null) break;

                int cid = customerSystem.DequeueWaitingOrder();
                if (cid < 0) break;

                var c = customerSystem.FindById(cid);
                if (c == null || c.Stage != CustomerStage.SeatedOrdering) continue; // dequeue된 손님이 이미 사라짐 — 다음 시도

                idle.State = StaffState.TakingOrderMove;
                idle.StateElapsed = 0f;
                idle.TargetCustomerId = c.Id;
                idle.TargetSeatIdx = c.SeatIdx;
            }
        }

        Staff FindIdleStaff()
        {
            for (int i = 0; i < staffList.Count; i++)
                if (staffList[i].State == StaffState.Idle) return staffList[i];
            return null;
        }
    }
}
