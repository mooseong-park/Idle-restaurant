using UnityEngine;
using Project.Core;
using Project.Data;

namespace Project.Systems
{
    public enum DayPhase
    {
        Playing,        // 정상 영업 중 — Customer/Staff/Passive 모두 진행
        Transitioning,  // Day End 모달 표시 중 (HTML과 동일 3초) — 게임 정지
    }

    // Day 진행/종료/다음날 전환 관리. HTML 프로토 v0.15.1의 endDay() 흐름을 이식.
    // GameManager.Update에서 매 프레임 Tick(dt) 호출.
    public sealed class DaySystem
    {
        readonly GameState state;
        readonly GameConfigSO config;
        readonly CustomerSystem customerSystem;
        readonly StaffSystem staffSystem;

        DayPhase phase = DayPhase.Playing;
        float dayElapsed;
        float transitionElapsed;

        public DayPhase Phase => phase;
        public float DayElapsed => dayElapsed;
        public float DayLengthSec => config.DayLengthMs / 1000f;
        public float TransitionSec => config.DayTransitionMs / 1000f;

        // Day End 직후 결과 — UI/디버그 표시용. 다음 Day 시작 시까지 유지.
        public DayResult LastResult;

        public DaySystem(GameState state, GameConfigSO config, CustomerSystem customerSystem, StaffSystem staffSystem)
        {
            this.state = state;
            this.config = config;
            this.customerSystem = customerSystem;
            this.staffSystem = staffSystem;
        }

        public void Tick(float dt)
        {
            switch (phase)
            {
                case DayPhase.Playing:
                    dayElapsed += dt;
                    if (dayElapsed >= DayLengthSec) EndDay();
                    break;
                case DayPhase.Transitioning:
                    transitionElapsed += dt;
                    if (transitionElapsed >= TransitionSec) StartNextDay();
                    break;
            }
        }

        // ===== Day End 처리 =====

        void EndDay()
        {
            var rep = Formulas.ComputeRep(state.Rating, config.RepLevelTable);
            bool achieved = state.DayRevenue >= rep.Goal;
            int gemReward = achieved ? config.GemBaseReward + config.GemGoalBonus : config.GemBaseReward;
            state.Gems += gemReward;

            int starEarned = 0;
            int prevLv = rep.Lv;
            int newLv = prevLv;
            bool leveledUp = false;
            if (achieved)
            {
                state.DayAchieved++;
                state.Rating += 1;
                starEarned = 1;
                var newRep = Formulas.ComputeRep(state.Rating, config.RepLevelTable);
                newLv = newRep.Lv;
                leveledUp = newLv > prevLv;
            }

            LastResult = new DayResult
            {
                Day = state.Day,
                Revenue = state.DayRevenue,
                Goal = rep.Goal,
                Achieved = achieved,
                GemReward = gemReward,
                StarEarned = starEarned,
                LeveledUp = leveledUp,
                NewLv = newLv,
            };

            phase = DayPhase.Transitioning;
            transitionElapsed = 0f;
            state.Events?.RaiseDayPhaseChanged();

            Debug.Log(
                $"=== Day {state.Day} 종료 ===\n" +
                $"💰 Revenue ${state.DayRevenue:F0} / 목표 ${rep.Goal} → " +
                (achieved ? "달성 🎉" : "미달") + "\n" +
                $"💎 +{gemReward}" + (starEarned > 0 ? $" | ⭐ +{starEarned}" : "") +
                (leveledUp ? $" | 🆙 명성 Lv {newLv} 달성!" : "") + "\n" +
                $"누적 ⭐ {state.Rating} | 누적 😋 {state.Successes} | 🚶 {state.PatienceFails}\n" +
                $"(Transition {TransitionSec:F0}s 후 다음 Day 시작)");
        }

        void StartNextDay()
        {
            // HTML과 동일 순서: customers/seats/staff 리셋 → state 갱신
            customerSystem.ResetForNewDay();
            staffSystem.ResetAllToIdle();

            state.Day++;
            state.DayRevenue = 0f;
            // Successes/PatienceFails는 누적 유지 (HTML stats.success/pfail와 동일)

            dayElapsed = 0f;
            transitionElapsed = 0f;
            phase = DayPhase.Playing;
            state.Events?.RaiseDayPhaseChanged();
            state.Events?.RaiseDayChanged();

            var rep = Formulas.ComputeRep(state.Rating, config.RepLevelTable);
            Debug.Log(
                $"=== Day {state.Day} 시작 ===\n" +
                $"💰 ${state.Cash:F0} | 💎 {state.Gems} | ⭐ {state.Rating} | 명성 Lv {rep.Lv} (목표 ${rep.Goal})");
        }
    }

    // Day End 결과 스냅샷. UI 모달이 읽을 수 있도록 보관.
    public struct DayResult
    {
        public int Day;
        public float Revenue;
        public int Goal;
        public bool Achieved;
        public int GemReward;
        public int StarEarned;
        public bool LeveledUp;
        public int NewLv;
    }
}
