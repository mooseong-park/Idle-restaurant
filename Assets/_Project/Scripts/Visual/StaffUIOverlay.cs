using UnityEngine;
using Project.Domain;
using Project.Utils;

namespace Project.Visual
{
    // 직원 머리 위 오버레이 — 조리 진행도 파이.
    // CustomerUIOverlay 와 동일 procedural texture 패턴, 단일 파란색(작업 진행 의미) 으로만 구분.
    // CookingItem 상태가 아닐 때는 비활성 (UpdateOverlay 에 음수 pct 전달).
    [DisallowMultipleComponent]
    public sealed class StaffUIOverlay : MonoBehaviour
    {
        [SerializeField] SpriteRenderer pieSr;

        Texture2D pieTex;
        const int PieTexSize = 64;
        const float PieY     = 1.2f;   // procedural fallback 전용 — Prefab 사용 시 Inspector Transform 우선
        const float PieScale = 1.0f;
        float lastPiePct = -2f;

        // 작업 진행 — 손님의 인내심 warning 팔레트와 시각 구분되도록 단일 파란색.
        static readonly Color CookingFill = new Color(0.23f, 0.51f, 0.96f);

        Staff staff;

        void Awake() => EnsurePiePatience();

        public void Bind(Staff s)
        {
            staff = s;
            lastPiePct = -2f;
        }

        // 매 프레임 호출. cookingPct: 0~1 잔여 비율, 음수면 비활성 (CookingItem 외 상태).
        public void UpdateOverlay(float cookingPct)
        {
            if (staff == null) return;
            UpdatePie(cookingPct);
        }

        void EnsurePiePatience()
        {
            if (pieSr == null)
            {
                var go = new GameObject("CookingPie");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, PieY, 0f);
                go.transform.localScale = new Vector3(PieScale, PieScale, 1f);
                pieSr = go.AddComponent<SpriteRenderer>();
                pieSr.sortingOrder = SortingOrders.CharacterUI;
            }
            pieTex = new Texture2D(PieTexSize, PieTexSize, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            pieSr.sprite = Sprite.Create(pieTex,
                new Rect(0, 0, PieTexSize, PieTexSize),
                new Vector2(0.5f, 0.5f),
                PieTexSize);
            pieSr.gameObject.SetActive(false);
        }

        void UpdatePie(float pct)
        {
            if (pieSr == null) return;

            if (pct < 0f)
            {
                if (pieSr.gameObject.activeSelf) pieSr.gameObject.SetActive(false);
                lastPiePct = -2f;
                return;
            }
            if (!pieSr.gameObject.activeSelf) pieSr.gameObject.SetActive(true);

            if (Mathf.Abs(pct - lastPiePct) < 0.01f) return;
            lastPiePct = pct;
            PieTexture.Draw(pieTex, pct, CookingFill);
        }
    }
}
