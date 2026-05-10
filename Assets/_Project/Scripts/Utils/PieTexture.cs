using UnityEngine;

namespace Project.Utils
{
    // 원형 파이 텍스처 그리기 — 12시 시작, 시계방향 진행, pct 1.0 = 360° (완전 채움), 0 = 빈 원.
    // 손님 인내심 / 직원 조리 진행도 등 동일 시각 패턴 공유. 색상만 호출자가 결정.
    // texture size 는 호출자가 결정 (보통 64). 매 프레임 호출 시 SetPixels32 GC 비용 있음 — pct 변화 임계값으로 호출자가 throttle 권장.
    public static class PieTexture
    {
        public static void Draw(Texture2D tex, float pct, Color fillColor)
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
    }
}
