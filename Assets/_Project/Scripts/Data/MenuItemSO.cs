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

        [Tooltip("주문 시 이 메뉴가 포함될 확률. 0 = 절대 안 나옴, 1 = 항상 나옴")]
        [SerializeField, Range(0f, 1f)] float generateProb = 1f;

        public string Id => id;
        public string Icon => icon;
        public Sprite IconSprite => iconSprite;
        public int CookTimeMs => cookTimeMs;
        public int BasePrice => basePrice;
        public float GenerateProb => generateProb;
    }
}
