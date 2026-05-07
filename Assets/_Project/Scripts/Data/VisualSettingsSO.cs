using UnityEngine;

namespace Project.Data
{
    // 시각 튜닝 값 — 코드 const 분산을 SO Inspector 로 외부화 (B1).
    // CustomerView / StaffView / RevenuePopupView 가 [SerializeField] 슬롯으로 참조.
    // 슬롯 비어있으면 클래스 내부 const 폴백 (호환).
    [CreateAssetMenu(menuName = "Idle Restaurant/Visual Settings", fileName = "VisualSettings")]
    public sealed class VisualSettingsSO : ScriptableObject
    {
        [Header("손님 이동")]
        [Tooltip("최대 이동 속도 (world unit/s)")]
        [Range(0.2f, 3f)] public float customerMaxSpeed = 1f;
        [Tooltip("가속/감속 (world unit/s²)")]
        [Range(0.5f, 10f)] public float customerAcceleration = 2.5f;
        [Tooltip("Bob 점프 높이 (world unit). 0 = bob 없음.")]
        [Range(0f, 0.6f)] public float customerBobAmpY = 0.20f;
        [Tooltip("Sway 좌우 흔들림 폭 (world unit). 절반 주파수.")]
        [Range(0f, 0.2f)] public float customerSwayAmpX = 0.02f;

        [Header("직원 이동")]
        [Range(0.2f, 3f)] public float staffMaxSpeed = 1.1f;
        [Range(0.5f, 10f)] public float staffAcceleration = 3f;
        [Range(0f, 0.6f)] public float staffBobAmpY = 0.19f;
        [Range(0f, 0.2f)] public float staffSwayAmpX = 0.02f;

        [Header("공통 애니메이션")]
        [Tooltip("이동 1 unit 당 bob 사이클 수 (높을수록 발걸음 빠르게).")]
        [Range(0.3f, 4f)] public float stepsPerUnit = 1.3f;
        [Tooltip("Idle 호흡 bob 주파수 (Hz). Staff Idle 상태일 때만 활성.")]
        [Range(0.5f, 5f)] public float idleBobFreq = 1.6f;
        [Tooltip("Idle 호흡 bob 진폭 (world unit).")]
        [Range(0f, 0.2f)] public float idleBobAmp = 0.04f;

        [Header("Revenue Popup")]
        [Tooltip("Popup 수명 (초). 이 시간 동안 떠오르며 fade out.")]
        [Range(0.5f, 5f)] public float popupLifeSec = 1.4f;
        [Tooltip("Popup 이 위로 떠오를 거리 (world unit).")]
        [Range(0.2f, 3f)] public float popupRiseDistance = 0.9f;
        [Tooltip("Popup 폰트 사이즈 (TextMeshPro size).")]
        [Range(1f, 20f)] public float popupFontSize = 5f;
        [Tooltip("Popup 색상 (수익 = 녹색 계열 권장).")]
        public Color popupColor = new Color(0.13f, 0.65f, 0.30f);
    }
}
