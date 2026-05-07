using UnityEngine;

namespace Project.Utils
{
    // 런타임에 단순 도형 sprite를 생성/캐시. PNG asset 없이 Color tint + scale로 모든 시각 표현.
    // Pixels Per Unit = texture 크기로 설정 → SpriteRenderer scale 1 = world 1 unit 크기.
    public static class SpriteFactory
    {
        static Sprite cachedSquare;
        static Sprite cachedLeftSquare;
        static Sprite cachedCircle;
        static Sprite cachedRoundedSquare;

        public static Sprite Square()
        {
            if (cachedSquare != null) return cachedSquare;
            var tex = MakeSquareTex();
            cachedSquare = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
            cachedSquare.name = "SF_Square";
            return cachedSquare;
        }

        // 좌측 중앙 pivot (0, 0.5) — scale.x 변경 시 좌측 anchor 유지 → 우→좌 줄어드는 progress bar에 사용.
        public static Sprite LeftSquare()
        {
            if (cachedLeftSquare != null) return cachedLeftSquare;
            var tex = MakeSquareTex();
            cachedLeftSquare = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 0.5f), 1f);
            cachedLeftSquare.name = "SF_LeftSquare";
            return cachedLeftSquare;
        }

        public static Sprite Circle()
        {
            if (cachedCircle != null) return cachedCircle;
            var tex = MakeCircleTex();
            cachedCircle = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            cachedCircle.name = "SF_Circle";
            return cachedCircle;
        }

        public static Sprite RoundedSquare()
        {
            if (cachedRoundedSquare != null) return cachedRoundedSquare;
            var tex = MakeRoundedSquareTex(128, 24);
            cachedRoundedSquare = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            cachedRoundedSquare.name = "SF_RoundedSquare";
            return cachedRoundedSquare;
        }

        // EditorScript에서 PNG asset으로 인코딩하기 위해 노출.
        public static Texture2D MakeSquareTex()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.name = "SF_SquareTex";
            return tex;
        }

        public static Texture2D MakeCircleTex()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
            float radius = size / 2f - 1f;
            float aaBand = 1.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01((radius - dist) / aaBand + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(t * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = "SF_CircleTex";
            return tex;
        }

        // 둥근 사각형 (캐릭터 BgFrame 용). cornerRadiusPx가 0이면 일반 square와 동일.
        public static Texture2D MakeRoundedSquareTex(int size = 256, int cornerRadiusPx = 36)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = Mathf.Min(cornerRadiusPx, size / 2 - 1);
            float aaBand = 1.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 각 코너 중심 (사각형 안쪽으로 r 만큼 들여진 위치)
                    float cx = Mathf.Clamp(x, r, size - 1 - r);
                    float cy = Mathf.Clamp(y, r, size - 1 - r);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float t = Mathf.Clamp01((r - dist) / aaBand + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(t * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = "SF_RoundedSquareTex";
            return tex;
        }
    }
}
