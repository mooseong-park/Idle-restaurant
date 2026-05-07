using UnityEngine;
using Project.Domain;
using Project.Data;
using Project.Utils;

namespace Project.Visual
{
    // 손님 머리 위 오버레이 — 인내심 파이 + 주문 말풍선.
    // CustomerView 와 같은 GameObject 에 부착. 슬롯은 prefab 에서 wired 되거나 Awake 에서 procedural fallback.
    [DisallowMultipleComponent]
    public sealed class CustomerUIOverlay : MonoBehaviour
    {
        // PiePatience: 원형 파이 (인내심 잔여 비율). texture 는 매 프레임 procedural 갱신.
        // OrderBubble: 말풍선 (BG + 꼬리 + 메뉴 아이콘 N개). SeatedOrdering/Waiting 동안 표시.
        [SerializeField] SpriteRenderer pieSr;
        [SerializeField] GameObject bubbleGo;
        [SerializeField] SpriteRenderer bubbleBgSr;
        [SerializeField] SpriteRenderer bubbleTailSr;
        [SerializeField] SpriteRenderer[] bubbleIcons = new SpriteRenderer[BubbleMaxItems];

        Texture2D pieTex;
        const int PieTexSize = 64;
        const float PieY     = 1.2f;   // procedural fallback 전용 — Prefab 사용 시 Inspector Transform 이 우선
        const float PieScale = 1.1f;
        float lastPiePct = -2f;

        int lastBubbleItemCount = -1;
        Sprite[] lastBubbleSprites;

        const int   BubbleMaxItems     = 3;
        const float BubbleY            = 2f;     // fallback 전용
        const float BubbleHeight       = 1.6f;
        const float BubbleBaseWidth    = 1.9f;
        const float BubblePerItemWidth = 1.25f;
        const float BubbleIconScale    = 1.05f;  // fallback 전용
        const float BubbleIconSpacing  = 1.25f;
        const float BubbleTailY        = -0.75f; // fallback 전용
        const float BubbleTailScale    = 0.45f;  // fallback 전용

        static readonly Color PatienceHigh = new Color(0.13f, 0.77f, 0.37f);
        static readonly Color PatienceMid  = new Color(0.96f, 0.62f, 0.04f);
        static readonly Color PatienceLow  = new Color(0.94f, 0.27f, 0.27f);

        Customer customer;

        void Awake()
        {
            EnsurePiePatience();
            EnsureOrderBubble();
        }

        public void Bind(Customer c)
        {
            customer = c;
            // 새 손님 bind 시 캐시 초기화 — 이전 손님 잔여 sprite 안 보이게.
            lastPiePct = -2f;
            lastBubbleItemCount = -1;
        }

        // 매 프레임 호출. patiencePct: 0~1 잔여, 음수면 비활성.
        public void UpdateOverlay(float patiencePct)
        {
            if (customer == null) return;
            UpdatePiePatience(patiencePct);
            UpdateOrderBubble();
        }

        // ===== Pie patience =====

        void EnsurePiePatience()
        {
            if (pieSr == null)
            {
                var go = new GameObject("PiePatience");
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

        void UpdatePiePatience(float pct)
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

            Color fill = pct < 0.2f ? PatienceLow : (pct < 0.5f ? PatienceMid : PatienceHigh);
            DrawPieTexture(pieTex, pct, fill);
        }

        // 픽셀 단위 파이 그리기 — 시계방향, 12시부터 시작. pct=1 → 360°, pct=0 → 빈 원.
        static void DrawPieTexture(Texture2D tex, float pct, Color fillColor)
        {
            int size = tex.width;
            float center = (size - 1) / 2f;
            float radius = size / 2f - 1f;
            const float aaBand = 1f;

            Color32 fill = (Color32)fillColor;
            Color32 bg = new Color32(40, 40, 40, 120);
            Color32 outline = new Color32(20, 20, 20, 200);
            Color32 transparent = new Color32(0, 0, 0, 0);

            float fillAngle = Mathf.Clamp01(pct) * 360f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius + aaBand) { pixels[y * size + x] = transparent; continue; }

                    float angle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;

                    Color32 c = (angle <= fillAngle) ? fill : bg;
                    if (dist > radius - 1.2f) c = outline;
                    if (dist > radius)
                    {
                        float t = Mathf.Clamp01((radius - dist) / aaBand + 0.5f);
                        c.a = (byte)(c.a * t);
                    }
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }

        // ===== Order bubble =====

        void EnsureOrderBubble()
        {
            if (bubbleGo != null) return;

            // Procedural fallback — prefab 미사용 시
            bubbleGo = new GameObject("OrderBubble");
            bubbleGo.transform.SetParent(transform, false);
            bubbleGo.transform.localPosition = new Vector3(0f, BubbleY, 0f);
            bubbleGo.transform.localScale = Vector3.one;

            var tailGo = new GameObject("Tail");
            tailGo.transform.SetParent(bubbleGo.transform, false);
            tailGo.transform.localPosition = new Vector3(0f, BubbleTailY, 0f);
            tailGo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailGo.transform.localScale = new Vector3(BubbleTailScale, BubbleTailScale, 1f);
            bubbleTailSr = tailGo.AddComponent<SpriteRenderer>();
            bubbleTailSr.sprite = SpriteFactory.Square();
            bubbleTailSr.color = new Color(1f, 1f, 1f, 0.97f);
            bubbleTailSr.sortingOrder = SortingOrders.CharacterUI;

            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(bubbleGo.transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            bgGo.transform.localScale = new Vector3(BubbleBaseWidth, BubbleHeight, 1f);
            bubbleBgSr = bgGo.AddComponent<SpriteRenderer>();
            bubbleBgSr.sprite = SpriteFactory.RoundedSquare();
            bubbleBgSr.color = new Color(1f, 1f, 1f, 0.97f);
            bubbleBgSr.sortingOrder = SortingOrders.CharacterUI;

            if (bubbleIcons == null || bubbleIcons.Length < BubbleMaxItems)
                bubbleIcons = new SpriteRenderer[BubbleMaxItems];
            for (int i = 0; i < BubbleMaxItems; i++)
            {
                var iconGo = new GameObject($"Icon_{i}");
                iconGo.transform.SetParent(bubbleGo.transform, false);
                iconGo.transform.localScale = new Vector3(BubbleIconScale, BubbleIconScale, 1f);
                var sr2 = iconGo.AddComponent<SpriteRenderer>();
                sr2.sortingOrder = SortingOrders.OrderBubbleIcon;
                iconGo.SetActive(false);
                bubbleIcons[i] = sr2;
            }
            bubbleGo.SetActive(false);
        }

        void UpdateOrderBubble()
        {
            if (bubbleGo == null) return;

            bool show = (customer.Stage == CustomerStage.SeatedOrdering
                      || customer.Stage == CustomerStage.SeatedWaiting)
                     && customer.Order != null && customer.Order.Count > 0;

            if (!show)
            {
                if (bubbleGo.activeSelf) bubbleGo.SetActive(false);
                lastBubbleItemCount = -1;
                return;
            }
            if (!bubbleGo.activeSelf) bubbleGo.SetActive(true);

            int count = Mathf.Min(customer.Order.Count, BubbleMaxItems);

            if (count != lastBubbleItemCount)
            {
                float bgW = BubbleBaseWidth + BubblePerItemWidth * (count - 1);
                var bgScale = bubbleBgSr.transform.localScale;
                bgScale.x = bgW;
                bubbleBgSr.transform.localScale = bgScale;

                float startX = -((count - 1) * BubbleIconSpacing) * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    var sr2 = bubbleIcons[i];
                    if (sr2 == null) continue;
                    sr2.gameObject.SetActive(true);
                    var p = sr2.transform.localPosition;
                    p.x = startX + i * BubbleIconSpacing;
                    p.y = 0f; p.z = 0f;
                    sr2.transform.localPosition = p;
                }
                for (int i = count; i < bubbleIcons.Length; i++)
                    if (bubbleIcons[i] != null) bubbleIcons[i].gameObject.SetActive(false);

                if (lastBubbleSprites == null || lastBubbleSprites.Length < BubbleMaxItems)
                    lastBubbleSprites = new Sprite[BubbleMaxItems];
                for (int i = 0; i < count; i++) lastBubbleSprites[i] = null;

                lastBubbleItemCount = count;
            }

            for (int i = 0; i < count; i++)
            {
                if (bubbleIcons[i] == null) continue;
                var item = customer.Order[i];
                Sprite s = item != null ? item.IconSprite : null;
                if (lastBubbleSprites[i] != s)
                {
                    bubbleIcons[i].sprite = s;
                    bubbleIcons[i].enabled = (s != null);
                    lastBubbleSprites[i] = s;
                }
            }
        }
    }
}
