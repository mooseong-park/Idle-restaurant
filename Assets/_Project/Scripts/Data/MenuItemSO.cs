using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Idle Restaurant/Menu Item", fileName = "MenuItem_New")]
    public sealed class MenuItemSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("코드에서 이 메뉴를 식별하는 문자열. 한 번 정하면 바꾸지 마세요. 예: hotdog, cola, salad")]
        [SerializeField] string id = "";

        [Tooltip("UI에 표시할 이모지 텍스트 (UI Toolkit Label 용 — 가챠/모달 등)")]
        [SerializeField] string icon = "";

        [Tooltip("월드/말풍선에 표시할 sprite (Menu_Hotdog.png 등). null 이면 SceneAnchors의 Station icon 으로 fallback.")]
        [SerializeField] Sprite iconSprite;

        [Header("게임 데이터")]
        [Tooltip("조리에 걸리는 시간 (밀리초). 예: 2500 = 2.5초")]
        [SerializeField, Range(100, 10000)] int cookTimeMs = 1000;

        [Tooltip("기본 가격. 메뉴 트랙 Lv 배율이 곱해진 후 최종 가격이 됨.")]
        [SerializeField, Range(0, 50)] int basePrice = 1;

        [Tooltip("주문 시 이 메뉴가 뽑힐 가중치 (상대값). 0 = 절대 안 나옴. 다른 메뉴와의 합 대비 비율로 추첨. 예: Hotdog 1.0 / Cola 0.5 → Hotdog 67%")]
        [SerializeField, Range(0f, 1f)] float generateProb = 1f;

        [Header("주문 수량 (단일 메뉴 모델)")]
        [Tooltip("주문 수량 최소값 (1 이상).")]
        [SerializeField, Range(1, 20)] int minQty = 1;

        [Tooltip("주문 수량 최대값 (minQty 이상). 예: 핫도그 1~3, 콜라 1~5")]
        [SerializeField, Range(1, 20)] int maxQty = 3;

        public string Id => id;
        public string Icon => icon;
        public Sprite IconSprite => iconSprite;
        public int CookTimeMs => cookTimeMs;
        public int BasePrice => basePrice;
        public float GenerateProb => generateProb;
        public int MinQty => Mathf.Max(1, minQty);
        public int MaxQty => Mathf.Max(MinQty, maxQty);

        void OnValidate()
        {
            if (minQty < 1) minQty = 1;
            if (maxQty < minQty) maxQty = minQty;
        }
    }
}
