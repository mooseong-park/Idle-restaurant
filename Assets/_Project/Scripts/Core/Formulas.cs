using System.Collections.Generic;
using UnityEngine;
using Project.Data;

namespace Project.Core
{
    // 모든 게임 공식이 여기에 모여 있음.
    // 수식 모양을 바꾸고 싶으면 이 파일만 수정하면 됨.
    // 입력은 (Lv, GameConfigSO 또는 TrackConfigSO), 출력은 배율 / 비용 / 용량.
    public static class Formulas
    {
        // ===== Active Track 효과 (Lv 1일 때 모두 ×1.0 = 효과 없음) =====

        // 메뉴 Lv → 가격 배율 (선형, Lv 오를수록 +)
        public static float PriceMult(int menuLv, GameConfigSO cfg)
            => 1f + (menuLv - 1) * cfg.PriceMultPerLv;

        // 직원 Lv → 이동·조리 속도 배율 (점근, 시간에 곱하므로 시간이 줄어듦)
        public static float StaffSpdMult(int staffLv, GameConfigSO cfg)
            => 1f / (1f + (staffLv - 1) * cfg.StaffSpdGrowth);

        // 서비스 Lv → 식사 시간 배율 (점근, 식사 시간 단축)
        public static float EatingSpdMult(int serviceLv, GameConfigSO cfg)
            => 1f / (1f + (serviceLv - 1) * cfg.EatingSpdGrowth);

        // 서비스 Lv → 인내심 배율 (선형, 시간 증가)
        public static float PatienceMult(int serviceLv, GameConfigSO cfg)
            => 1f + (serviceLv - 1) * cfg.PatiencePerLv;

        // ===== 매장 용량 (매장 Lv에 비례, 최대치 클램프) =====

        public static int SeatingCap(int venueLv, GameConfigSO cfg)
            => Mathf.Min(cfg.SeatingMax, cfg.SeatingBase + (venueLv - 1) * cfg.SeatingPerLv);

        public static int QueueCap(int venueLv, GameConfigSO cfg)
            => Mathf.Min(cfg.QueueMax, cfg.QueueBase + (venueLv - 1) * cfg.QueuePerLv);

        // ===== Passive 자동 수입 (Lv 1부터 작동, Lv × 계수) =====

        public static float TakeoutPerSec(int takeoutLv, GameConfigSO cfg) => takeoutLv * cfg.TakeoutPerLv;
        public static float AdsPerSec(int adsLv, GameConfigSO cfg)         => adsLv * cfg.AdsPerLv;
        public static float TipPerSec(int tipLv, GameConfigSO cfg)         => tipLv * cfg.TipPerLv;
        public static float MerchPerSec(int merchLv, GameConfigSO cfg)     => merchLv * cfg.MerchPerLv;

        public static float PassiveTotalPerSec(GameState s, GameConfigSO cfg)
            => TakeoutPerSec(s.TakeoutLv, cfg)
             + AdsPerSec(s.AdsLv, cfg)
             + TipPerSec(s.TipLv, cfg)
             + MerchPerSec(s.MerchLv, cfg);

        // ===== 트랙 비용 곡선 =====

        public static int TrackCost(TrackConfigSO track, int currentLv)
            => Mathf.FloorToInt(track.BaseCost * Mathf.Pow(track.Growth, currentLv - 1));

        public static bool TrackMaxed(TrackConfigSO track, int currentLv)
            => track.MaxLv > 0 && currentLv >= track.MaxLv;

        // ===== 주문 가격 (메뉴 가격 합 × 메뉴 Lv 배율, 반올림) =====

        public static int OrderTotalPrice(IReadOnlyList<MenuItemSO> order, int menuLv, GameConfigSO cfg)
        {
            float mult = PriceMult(menuLv, cfg);
            float sum = 0f;
            for (int i = 0; i < order.Count; i++) sum += order[i].BasePrice * mult;
            return Mathf.RoundToInt(sum);
        }

        // ===== 명성 Lv 계산 (누적 ⭐ 기반) =====

        public readonly struct RepInfo
        {
            public readonly int Lv;
            public readonly int StarsInLv;
            public readonly int StarsToNext;
            public readonly int Goal;
            public readonly bool IsMax;

            public RepInfo(int lv, int starsInLv, int starsToNext, int goal, bool isMax)
            {
                Lv = lv; StarsInLv = starsInLv; StarsToNext = starsToNext; Goal = goal; IsMax = isMax;
            }
        }

        public static RepInfo ComputeRep(int totalStars, RepLevelTableSO table)
        {
            int consumed = 0;
            for (int i = 0; i < table.Levels.Count; i++)
            {
                var lvCfg = table.Levels[i];
                int needed = lvCfg.starsToNext;
                int starsInThisLv = totalStars - consumed;

                if (needed <= 0)
                    return new RepInfo(i + 1, starsInThisLv, 0, lvCfg.goal, true);

                if (starsInThisLv < needed)
                    return new RepInfo(i + 1, starsInThisLv, needed, lvCfg.goal, false);

                consumed += needed;
            }
            // 마지막 Lv 도달 (모든 starsToNext가 충족된 경우)
            var last = table.Levels[table.Levels.Count - 1];
            return new RepInfo(table.Levels.Count, 0, 0, last.goal, true);
        }
    }
}
