using System;

namespace Project.Core
{
    // 시드 가능 RNG 추상화. UnityEngine.Random 직접 호출 → 가챠 분포 검증/버그 재현 불가.
    // GachaSystem/CustomerSystem 이 IRandomProvider 를 주입 받음 — 시드 모드에서 재현 가능 시퀀스.
    public interface IRandomProvider
    {
        // [0, 1) 균등 분포
        float Value();
        // [min, max) 정수
        int Range(int minInclusive, int maxExclusive);
    }

    // 기본 — UnityEngine.Random 위임. 매 실행마다 다른 시퀀스.
    public sealed class UnityRandomProvider : IRandomProvider
    {
        public float Value() => UnityEngine.Random.value;
        public int Range(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    // 시드 모드 — System.Random. 같은 seed = 같은 시퀀스 (가챠 분포/스폰 검증).
    public sealed class SeededRandomProvider : IRandomProvider
    {
        readonly Random rng;
        public SeededRandomProvider(int seed) => rng = new Random(seed);
        public float Value() => (float)rng.NextDouble();
        public int Range(int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);
    }
}
