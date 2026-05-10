using System;
using UnityEngine;

namespace Project.Utils
{
    // 큰 수 표기 유틸. 저장 자료형(float/int)은 그대로, UI 표기만 K/M/B/T/Q 단위로 축약.
    // 1000 미만 = 정수, 그 이상은 1.2K / 12.3K / 123K / 1.2M ... 형태.
    // idle 게임 트랙 비용은 지수 곡선 → Lv 10+ 도달 시 7자리 → HUD 깨짐 방지 목적.
    public static class BigNumber
    {
        static readonly string[] Units = { "", "K", "M", "B", "T", "Q" };

        public static string Fmt(float v) => Fmt((double)v);
        public static string Fmt(int v)   => Fmt((double)v);
        public static string Fmt(long v)  => Fmt((double)v);

        public static string Fmt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            bool neg = v < 0;
            double abs = Math.Abs(v);

            if (abs < 1000d) return neg ? $"-{(int)abs}" : $"{(int)abs}";

            int unitIdx = 0;
            double scaled = abs;
            while (scaled >= 1000d && unitIdx < Units.Length - 1)
            {
                scaled /= 1000d;
                unitIdx++;
            }

            // 10 미만 = "1.23", 100 미만 = "12.3", 그 이상 = "123" (다음 단위로 안 넘어가는 잔여)
            string body = scaled < 10d   ? scaled.ToString("0.##")
                        : scaled < 100d  ? scaled.ToString("0.#")
                                         : scaled.ToString("0");
            return (neg ? "-" : "") + body + Units[unitIdx];
        }
    }
}
