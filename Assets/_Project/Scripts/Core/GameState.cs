using Project.Core.Events;

namespace Project.Core
{
    // 런타임 상태 (POCO). MonoBehaviour 아님, ScriptableObject 아님.
    // 게임 진행 중 변하는 값만 보관. 변하지 않는 설정은 GameConfigSO에 있음.
    //
    // Cash/Gems/Rating/DayRevenue는 setter에서 GameEventsSO 채널 raise (UI polling 제거 목적).
    // events == null 이면 raise 스킵 (테스트/CLI 환경 호환).
    public sealed class GameState
    {
        GameEventsSO events;

        public GameEventsSO Events => events;
        public void BindEvents(GameEventsSO e) => events = e;

        // 통화 / 명성 — events 채널 발행
        float cash;
        public float Cash
        {
            get => cash;
            set
            {
                if (cash == value) return;
                cash = value;
                events?.RaiseCash(cash);
            }
        }

        int gems = 10;
        public int Gems
        {
            get => gems;
            set
            {
                if (gems == value) return;
                gems = value;
                events?.RaiseGems(gems);
            }
        }

        int rating;
        public int Rating
        {
            get => rating;
            set
            {
                if (rating == value) return;
                rating = value;
                events?.RaiseRating(rating);
            }
        }

        // 8 트랙 Lv (모두 1부터 시작)
        public int MenuLv = 1;
        public int StaffLv = 1;
        public int ServiceLv = 1;
        public int VenueLv = 1;
        public int TakeoutLv = 1;
        public int AdsLv = 1;
        public int TipLv = 1;
        public int MerchLv = 1;

        // Day 진행
        public int Day = 1;

        float dayRevenue;
        public float DayRevenue
        {
            get => dayRevenue;
            set
            {
                if (dayRevenue == value) return;
                dayRevenue = value;
                events?.RaiseDayRevenue(dayRevenue);
            }
        }

        // 통계 (누적 — Day 전환 시 리셋되지 않음)
        public int Successes;
        public int PatienceFails;
        public int DayAchieved;
        public float TotalRevenue;
        public float PassiveRevenue;
    }
}
