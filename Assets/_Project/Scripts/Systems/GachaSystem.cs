using UnityEngine;
using Project.Core;
using Project.Data;
using Project.Utils;

namespace Project.Systems
{
    // 직원 가챠 — StaffPackSO 1회 구매 시 💎 차감 + 등급 추첨 + StaffSystem.SpawnStaff.
    // HTML 프로토 v0.15.1의 buyPack(type) + pickRarity 흐름 이식.
    public sealed class GachaSystem
    {
        readonly GameState state;
        readonly GameConfigSO config;
        readonly StaffSystem staffSystem;
        readonly IRandomProvider random;

        public GachaSystem(GameState state, GameConfigSO config, StaffSystem staffSystem, IRandomProvider random)
        {
            this.state = state;
            this.config = config;
            this.staffSystem = staffSystem;
            this.random = random;
        }

        public enum BuyResult { Success, NotEnoughGems, MaxStaffReached, InvalidPack }

        public bool CanBuy(StaffPackSO pack)
        {
            if (pack == null) return false;
            if (state.Gems < pack.GemCost) return false;
            if (staffSystem.Staffs.Count >= config.MaxStaff) return false;
            return true;
        }

        // 시도 → 결과. drawn/spawnedName은 Success일 때만 채워짐 (UI/로그용).
        public BuyResult Buy(StaffPackSO pack, out RarityConfigSO drawn, out string spawnedName)
        {
            drawn = null;
            spawnedName = null;
            if (pack == null) return BuyResult.InvalidPack;
            if (staffSystem.Staffs.Count >= config.MaxStaff) return BuyResult.MaxStaffReached;
            if (state.Gems < pack.GemCost) return BuyResult.NotEnoughGems;

            state.Gems -= pack.GemCost;
            drawn = PickRarity(pack);
            spawnedName = RandomName.Make(config);
            staffSystem.SpawnStaff(drawn, spawnedName);
            // 모달 표시는 UIController.OnGachaClicked 에서 직접 처리 (Buy 의 return 값 + out 매개변수 사용).
            // OnStaffChanged 만 채널 발행 — 가챠 버튼 enable/disable 갱신용.
            state.Events?.RaiseStaffChanged();
            return BuyResult.Success;
        }

        // 누적 확률 추첨. weight 합이 1이 아니어도(SO OnValidate 경고) 안전 동작.
        RarityConfigSO PickRarity(StaffPackSO pack)
        {
            var weights = pack.Weights;
            float r = random.Value();
            float cum = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                cum += weights[i].weight;
                if (r < cum && weights[i].rarity != null) return weights[i].rarity;
            }
            // fallback: 마지막 슬롯 (또는 첫 번째 valid 슬롯)
            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i].rarity != null) return weights[i].rarity;
            return null;
        }
    }
}
