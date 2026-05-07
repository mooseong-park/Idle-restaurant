using UnityEngine;
using TMPro;
using Project.Core;
using Project.Data;

namespace Project.Visual
{
    // success 시 손님 자리 위에 잠깐 떠오르는 "+$N" 텍스트 (주문 결제 = 수익).
    // 주의: Passive 트랙의 "팁(Tip)" 자동 수입과 무관 — 이 popup은 손님당 주문 매출.
    // SceneController가 손님 mood Success 변화를 감지하고 인스턴스화.
    //
    // 튜닝 값(LifeSec/RiseDistance/FontSize)은 VisualSettings.asset 에 외부화 (B1).
    // SceneController 가 Show() 호출 시 settings 도 함께 전달.
    [DisallowMultipleComponent]
    public sealed class RevenuePopupView : MonoBehaviour
    {
        TextMeshPro tmp;
        float elapsed;
        GameManager gm; // gameSpeed 조회용
        VisualSettingsSO settings;

        // 폴백 — settings 가 null 이면 사용.
        const float DefaultLifeSec = 1.4f;
        const float DefaultRiseDistance = 0.9f;
        const float DefaultFontSize = 5f;

        Vector3 startPos;

        float LifeSec      => settings != null ? settings.popupLifeSec      : DefaultLifeSec;
        float RiseDistance => settings != null ? settings.popupRiseDistance : DefaultRiseDistance;
        float FontSize     => settings != null ? settings.popupFontSize     : DefaultFontSize;

        public void Show(Vector3 worldPos, int amount, Color color, VisualSettingsSO visualSettings = null)
        {
            transform.position = worldPos;
            startPos = worldPos;
            settings = visualSettings;

            if (tmp == null)
            {
                tmp = gameObject.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.sortingOrder = SortingOrders.RevenuePopup;
                var rt = tmp.rectTransform;
                rt.sizeDelta = new Vector2(2f, 1f);
            }
            tmp.fontSize = FontSize;
            tmp.text = $"+${amount}";
            tmp.color = color;
            elapsed = 0f;
        }

        void Update()
        {
            if (tmp == null) return;
            if (gm == null) gm = Object.FindFirstObjectByType<GameManager>();
            float speed = gm != null ? gm.GameSpeed : 1f;
            elapsed += Time.deltaTime * speed;
            float lifeSec = LifeSec;
            float t = Mathf.Clamp01(elapsed / lifeSec);

            var p = startPos;
            p.y += Mathf.SmoothStep(0f, RiseDistance, t);
            transform.position = p;

            // alpha fade — 후반 40%만 페이드
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            var c = tmp.color;
            c.a = fade;
            tmp.color = c;

            if (elapsed >= lifeSec) Destroy(gameObject);
        }
    }
}
