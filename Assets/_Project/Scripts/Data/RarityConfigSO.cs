using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Idle Restaurant/Rarity Config", fileName = "Rarity_New")]
    public sealed class RarityConfigSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("코드에서 이 등급을 식별하는 문자열. 한 번 정하면 바꾸지 마세요. 예: novice, pro, expert")]
        [SerializeField] string id = "";

        [Tooltip("UI에 표시할 이름. 예: 초급, 숙련, 전문가")]
        [SerializeField] string displayName = "";

        [Header("표시")]
        [Tooltip("이 등급의 캐릭터/카드 색. 프로토 단계 시각 차별화용.")]
        [SerializeField] Color color = Color.white;

        [Header("능력치")]
        [Tooltip("기본 이동 시간 (밀리초). 직원 트랙 Lv 배율이 곱해져서 최종 이동 시간이 됨.")]
        [SerializeField, Range(300, 5000)] int moveTimeMs = 2000;

        [Tooltip("조리 시간 배율. 1.0 = 기본, 0.5 = 절반(빠름), 2.0 = 두 배(느림). 메뉴 cookTime에 곱함.")]
        [SerializeField, Range(0.1f, 2f)] float cookMult = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public Color Color => color;
        public int MoveTimeMs => moveTimeMs;
        public float CookMult => cookMult;
    }
}
