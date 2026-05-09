using Project.Data;

namespace Project.Domain
{
    // 직원 FSM 상태. HTML 프로토 v0.15.1의 s.state 9가지를 enum으로 변환.
    // 사이클: Idle → TakingOrderMove → TakingOrder → ReturningToKitchen
    //        → (CookingMove → CookingItem) × N메뉴 → ServingMove → Serving → Returning → Idle
    public enum StaffState
    {
        Idle,
        TakingOrderMove,
        TakingOrder,
        ReturningToKitchen,
        CookingMove,
        CookingItem,
        ServingMove,
        Serving,
        Returning,
    }

    // 런타임 직원 인스턴스 (POCO).
    // 시간 단위는 모두 "초".
    public sealed class Staff
    {
        public int Id;
        public string Name;
        public RarityConfigSO Rarity;

        public StaffState State;
        public float StateElapsed;

        // 응대 중인 손님의 좌석 / Id (idle 상태나 작업 종료 후엔 모두 -1)
        public int TargetSeatIdx = -1;
        public int TargetCustomerId = -1;

        // 주문 사본. 단일 메뉴 + 수량 모델 — struct 라서 손님 Order 대입 시 자동 사본.
        public Order Order;

        // 현재 조리 진행 인덱스 (Order.Qty 중 몇 번째 cook 인지). 0..Qty-1 순회.
        public int CookingStepIdx;

        // 현재 조리 중인 스테이션의 메뉴. CookingMove/CookingItem 시에만 의미.
        public MenuItemSO CurrentStation;
    }
}
