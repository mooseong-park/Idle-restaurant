using System;
using UnityEngine;

namespace Project.Core.Events
{
    // 게임 이벤트 단일 진입점 (ScriptableObject Event Channel 컨테이너).
    // Producer (System): events.RaiseCash(state.Cash) 호출.
    // Consumer (UIController): events.OnCashChanged += UpdateLabel 구독.
    //
    // - per-event SO를 만들지 않고 한 SO에 Action 이벤트들을 모음 — prototype 단계 파일 수 ↓.
    // - SO 라이프사이클: Domain reload 후에도 인스턴스 살아있음. Play stop 시 잔여 구독 누적 방지를 위해
    //   OnDisable에서 모든 listener를 비움 (ClearAllSubscribers).
    [CreateAssetMenu(menuName = "Idle Restaurant/Events/Game Events", fileName = "GameEvents")]
    public sealed class GameEventsSO : ScriptableObject
    {
        // 진행 (값 포함)
        public event Action<float> OnCashChanged;
        public event Action<int> OnGemsChanged;
        public event Action<int> OnRatingChanged;
        public event Action<float> OnDayRevenueChanged;

        // Void 이벤트 (시점만 알림 — consumer가 GameState/Systems pull)
        public event Action OnTrackUpgraded;
        public event Action OnDayPhaseChanged;
        public event Action OnDayChanged;     // 새 Day 시작
        public event Action OnStaffChanged;   // 가챠/spawn 등 직원 수 변화

        public void RaiseCash(float v) => OnCashChanged?.Invoke(v);
        public void RaiseGems(int v) => OnGemsChanged?.Invoke(v);
        public void RaiseRating(int v) => OnRatingChanged?.Invoke(v);
        public void RaiseDayRevenue(float v) => OnDayRevenueChanged?.Invoke(v);
        public void RaiseTrackUpgraded() => OnTrackUpgraded?.Invoke();
        public void RaiseDayPhaseChanged() => OnDayPhaseChanged?.Invoke();
        public void RaiseDayChanged() => OnDayChanged?.Invoke();
        public void RaiseStaffChanged() => OnStaffChanged?.Invoke();

        // Domain reload / Play stop 시 잔여 구독 제거. 기본 SO 라이프사이클 hook 활용.
        void OnDisable() => ClearAllSubscribers();

        public void ClearAllSubscribers()
        {
            OnCashChanged = null;
            OnGemsChanged = null;
            OnRatingChanged = null;
            OnDayRevenueChanged = null;
            OnTrackUpgraded = null;
            OnDayPhaseChanged = null;
            OnDayChanged = null;
            OnStaffChanged = null;
        }
    }
}
