namespace Project.Domain
{
    // 손님 FSM 단계. HTML 프로토 v0.15.1의 c.stage 문자열을 enum으로 변환.
    // 흐름: Arriving → (Seating | Queueing) → SeatedOrdering → SeatedWaiting → Eating → Result → Leaving
    public enum CustomerStage
    {
        Arriving,
        Queueing,
        Seating,
        SeatedOrdering,
        SeatedWaiting,
        Eating,
        Result,
        Leaving,
    }

    // 손님 표정/결과. v0.15에서 성공률 시스템 제거되어 QFail은 사실상 미사용 (예약).
    public enum CustomerMood
    {
        None,
        Success,
        QFail,
        PFail,
    }

    // 런타임 손님 인스턴스 (POCO). MonoBehaviour 아님.
    // 시간 단위는 모두 "초" — HTML의 ms를 Unity dt에 맞춰 변환.
    public sealed class Customer
    {
        public int Id;
        public CustomerStage Stage;

        // 현재 스테이지에 머무른 시간 (초). 새 스테이지 진입 시 0으로 리셋.
        public float StageElapsed;

        // 좌석 인덱스 (점유 중일 때만, 아니면 -1)
        public int SeatIdx = -1;

        // 줄 위치 (0이 맨 앞, 큐가 아니면 -1)
        public int QueueIdx = -1;

        public CustomerMood Mood = CustomerMood.None;

        // Spawn 시점 결정된 의도. true = Arriving 단계 동안 큐 위치로 이동 (문 거치지 않음).
        // 좌석 여유가 없을 때만 true. 실제 좌석/큐 진입은 여전히 Arriving 만료 시점에 재확인 (좌석이 비면 EnterSeat).
        public bool IsQueueBound;

        // 주문 — 단일 메뉴 + 수량. default(Order) (Item==null) 이면 "아직 주문 없음".
        // SeatedOrdering 진입 시 CustomerSystem 이 GenerateOrder() 로 채움.
        public Order Order;

        // 인내심 누적 시간 (초). queueing / seated_ordering 진행 동안만 적립.
        public float WaitElapsed;

        // 인내심 카운트 활성 여부. HTML의 waitStart=null과 대응 (직원이 주문 받는 순간 false).
        public bool WaitActive;

        // 식사 완료 시 결제된 금액 (수익). SceneController가 RevenuePopup 트리거 시 참조.
        public int LastRevenue;

        // Spawn 시 결정. SceneController가 GameConfig.CustomerBodySprites[SpriteIdx]를 Body에 적용.
        // 같은 손님은 spawn~leaving 동안 항상 같은 모습 유지 (매 프레임 재선택 X).
        public int SpriteIdx;

        // 시각적 도착 플래그 — CustomerView가 매 프레임 갱신. CustomerSystem의 stage 전환 게이트로 사용.
        //   FSM 시간만으로 전환 시 손님이 visually 좌석에 도착하기 전 다음 stage 진입 → 직원이 빈 좌석 응대 등 버그.
        //   true 가 된 후에야 SettleElapsed/timer 가 진행되어 SeatedOrdering으로 전환.
        public bool VisuallyAtSeat;

        // 시각적으로 TopExit에 도달했는지 (Leaving stage 완료 트리거).
        //   TLeaving timer 만으로 제거 시 손님이 도어 근처에서 사라지는 버그 → 시각 도달 시 즉시 제거.
        public bool VisuallyAtExit;

        // 시각적으로 큐 슬롯에 도달했는지 — Queueing 인내심 타이머 시작 게이트.
        //   Stage=Queueing 자체는 walk 시작 시 진입하므로 그때부터 인내심 카운트하면 도착도 전에 PFail 위험.
        //   CustomerView가 movePos~QueuePos(QueueIdx) 거리로 갱신. CustomerSystem이 이 플래그가 true일 때만 WaitActive=true.
        public bool VisuallyAtQueue;

        // 좌석 visually 도달 후 settle 누적 시간 (TSeatingSettle 와 비교). VisuallyAtSeat 가 true 일 때만 누적.
        public float SettleElapsed;
    }
}
