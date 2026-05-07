using UnityEngine;

namespace Project.Utils
{
    // HTML 프로토의 % 좌표 → Unity World 좌표 변환 유틸리티.
    // Scene 영역: [-5, +5] × [-4, +4] (가로 10, 세로 8). HTML (top=0) → Unity (y=+max) y 반전.
    //
    // === Scene-driven 좌표 시스템 ===
    //   런타임 코드는 SceneAnchors API 통해 좌표 조회 — 더 이상 % 상수에 의존 X.
    //   ToWorld()는 SceneBuilder의 Editor-time 초기 layout seed 배치 + RevenuePopup 등 디버그용으로만 사용.
    public static class SceneCoords
    {
        public const float HalfWidth = 5f;
        public const float HalfHeight = 4f;
        public const float Width = HalfWidth * 2f;
        public const float Height = HalfHeight * 2f;

        public static Vector3 ToWorld(float xPct, float yPct, float z = 0f)
            => new Vector3((xPct - 50f) / 100f * Width, (50f - yPct) / 100f * Height, z);

        public static Vector3 ToWorld(Vector2 pct, float z = 0f) => ToWorld(pct.x, pct.y, z);
    }
}
