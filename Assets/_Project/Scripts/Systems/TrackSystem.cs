using Project.Core;
using Project.Data;

namespace Project.Systems
{
    // 8 트랙(메뉴/직원/서비스/매장 + 배달/광고/팁/굿즈) Lv 조회·업그레이드 결제.
    // HTML 프로토 v0.15.1의 buyTrack(trackId) 흐름 이식.
    public sealed class TrackSystem
    {
        readonly GameState state;
        readonly GameConfigSO config;

        public TrackSystem(GameState state, GameConfigSO config)
        {
            this.state = state;
            this.config = config;
        }

        public enum BuyResult { Success, NotEnoughCash, Maxed, InvalidTrack }

        public int GetLv(TrackConfigSO track)
        {
            if (track == null) return 0;
            switch (track.Id)
            {
                case "menu":    return state.MenuLv;
                case "staff":   return state.StaffLv;
                case "service": return state.ServiceLv;
                case "venue":   return state.VenueLv;
                case "takeout": return state.TakeoutLv;
                case "ads":     return state.AdsLv;
                case "tip":     return state.TipLv;
                case "merch":   return state.MerchLv;
            }
            return 0;
        }

        public int GetCost(TrackConfigSO track) => Formulas.TrackCost(track, GetLv(track));
        public bool IsMaxed(TrackConfigSO track) => Formulas.TrackMaxed(track, GetLv(track));
        public bool CanAfford(TrackConfigSO track) => state.Cash >= GetCost(track);

        public BuyResult TryUpgrade(TrackConfigSO track)
        {
            if (track == null) return BuyResult.InvalidTrack;
            if (IsMaxed(track)) return BuyResult.Maxed;
            int cost = GetCost(track);
            if (state.Cash < cost) return BuyResult.NotEnoughCash;
            state.Cash -= cost;
            SetLv(track, GetLv(track) + 1);
            state.Events?.RaiseTrackUpgraded();
            return BuyResult.Success;
        }

        void SetLv(TrackConfigSO track, int newLv)
        {
            switch (track.Id)
            {
                case "menu":    state.MenuLv    = newLv; break;
                case "staff":   state.StaffLv   = newLv; break;
                case "service": state.ServiceLv = newLv; break;
                case "venue":   state.VenueLv   = newLv; break;
                case "takeout": state.TakeoutLv = newLv; break;
                case "ads":     state.AdsLv     = newLv; break;
                case "tip":     state.TipLv     = newLv; break;
                case "merch":   state.MerchLv   = newLv; break;
            }
        }
    }
}
