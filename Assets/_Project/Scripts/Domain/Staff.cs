using System.Collections.Generic;
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

        // 주문 사본. 직원이 주방에서 한 메뉴씩 만들기 위해 보유.
        public List<MenuItemSO> Order;

        // 현재 조리 진행 인덱스 (Order의 몇 번째 메뉴를 만들고 있는지)
        public int CookingStepIdx;

        // 현재 조리 중인 스테이션의 메뉴. CookingMove/CookingItem 시에만 의미.
        public MenuItemSO CurrentStation;
    }
}
