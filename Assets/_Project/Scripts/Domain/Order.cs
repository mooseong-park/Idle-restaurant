using Project.Data;

namespace Project.Domain
{
    // 손님 주문 — 단일 메뉴 + 수량.
    // default(Order) = (Item=null, Qty=0) → "주문 없음" 상태.
    public struct Order
    {
        public MenuItemSO Item;
        public int Qty;

        public bool IsValid => Item != null && Qty > 0;
    }
}
