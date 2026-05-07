using UnityEngine;

namespace Project.Data
{
    public enum TrackAxis
    {
        Active,   // 손님 사이클에 직접 영향 (메뉴/직원/서비스/매장)
        Passive,  // 시간당 자동 수입 (배달/광고/팁/굿즈)
    }

    [CreateAssetMenu(menuName = "Idle Restaurant/Track Config", fileName = "Track_New")]
    public sealed class TrackConfigSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("코드에서 이 트랙을 식별하는 문자열. 한 번 정하면 바꾸지 마세요. 예: menu, staff, takeout")]
        [SerializeField] string id = "";

        [Tooltip("UI 표시 이름. 예: 메뉴, 직원, 배달")]
        [SerializeField] string displayName = "";

        [Tooltip("UI 표시 아이콘 (프로토 단계: 이모지)")]
        [SerializeField] string icon = "";

        [TextArea(1, 3)]
        [Tooltip("UI 설명 한 줄. 예: 메뉴 가격 상승")]
        [SerializeField] string description = "";

        [Header("분류")]
        [Tooltip("Active = 손님 응대 효율 (사이클 영향) / Passive = 자동 수입")]
        [SerializeField] TrackAxis axis = TrackAxis.Active;

        [Tooltip("카드 배경색. 프로토 단계 시각 차별화용.")]
        [SerializeField] Color color = Color.white;

        [Header("비용 곡선")]
        [Tooltip("Lv 1 → 2 업그레이드 비용 ($).")]
        [SerializeField, Range(1, 500)] int baseCost = 10;

        [Tooltip("매 Lv마다 비용에 곱해지는 배율. 1.15 = +15%/Lv. cost = baseCost × growth^(Lv-1).")]
        [SerializeField, Range(1.01f, 2f)] float growth = 1.15f;

        [Header("제약")]
        [Tooltip("최대 Lv. 0 = 무제한. (예: 매장 트랙은 5로 설정 → 6석/8대기에서 멈춤)")]
        [SerializeField, Range(0, 50)] int maxLv = 0;

        public string Id => id;
        public string DisplayName => displayName;
        public string Icon => icon;
        public string Description => description;
        public TrackAxis Axis => axis;
        public Color Color => color;
        public int BaseCost => baseCost;
        public float Growth => growth;
        public int MaxLv => maxLv;
    }
}
