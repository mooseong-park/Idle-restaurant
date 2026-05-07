namespace Project.Visual
{
    // SpriteRenderer.sortingOrder 매직 넘버 통합 — Z-fighting 디버그 시 한 곳만 보면 됨.
    //
    // 레이어 정책 (낮은 값 → 멀리 / 높은 값 → 가까이):
    //   배경:
    //     -25 OutdoorFloor   (전체 잔디)
    //     -23 RoadFloor      (yPct 0~10 도로)
    //     -22 RoadStripes    (도로 흰 줄무늬)
    //     -20 DiningFloor / KitchenFloor (건물 내부 바닥)
    //     -19 GrassDecor     (잔디 식물)
    //     -18 KitchenDivider (다이닝/주방 경계선)
    //     -15 BuildingWalls  (외곽선)
    //     -14 DoorFrame      (도어 표시)
    //   가구/스테이션:
    //     -10 Bar
    //      -9 BarTop
    //      -8 Station BG
    //      -7 Station Icon
    //      -6 SeatOutline
    //      -5 Seat
    //   캐릭터:
    //       4 CustomerBgFrame
    //       5 CustomerBody
    //       6 StaffBgFrame
    //       7 StaffBody
    //   머리 위 오버레이:
    //       9 PiePatience / OrderBubble Tail / BG / PatienceBg
    //      10 OrderBubbleIcon / PatienceFg
    //   World popup:
    //      30 RevenuePopup (TextMeshPro)
    public static class SortingOrders
    {
        // 배경
        public const int OutdoorFloor   = -25;
        public const int RoadFloor      = -23;
        public const int RoadStripe     = -22;
        public const int Floor          = -20;   // DiningFloor / KitchenFloor
        public const int GrassDecor     = -19;
        public const int KitchenDivider = -18;
        public const int BuildingWall   = -15;
        public const int DoorFrame      = -14;

        // 가구/스테이션
        public const int Bar            = -10;
        public const int BarTop         = -9;
        public const int StationBg      = -8;
        public const int StationIcon    = -7;
        public const int SeatOutline    = -6;
        public const int Seat           = -5;

        // 캐릭터
        public const int CustomerBgFrame = 4;
        public const int CustomerBody    = 5;
        public const int StaffBgFrame    = 6;
        public const int StaffBody       = 7;

        // 머리 위 오버레이
        public const int CharacterUI     = 9;    // PiePatience, OrderBubble (BG/Tail), PatienceBg
        public const int OrderBubbleIcon = 10;   // 메뉴 아이콘 (가장 위), PatienceFg

        // World popup (TextMeshPro)
        public const int RevenuePopup    = 30;
    }
}
