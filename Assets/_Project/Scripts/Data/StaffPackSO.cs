using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Idle Restaurant/Staff Pack", fileName = "Pack_New")]
    public sealed class StaffPackSO : ScriptableObject
    {
        [Serializable]
        public struct RarityWeight
        {
            [Tooltip("어떤 등급에 대한 확률인지")]
            public RarityConfigSO rarity;

            [Tooltip("이 등급이 뽑힐 확률 (0~1). 모든 항목 합이 1이 되어야 함.")]
            [Range(0f, 1f)] public float weight;
        }

        [Header("식별")]
        [Tooltip("코드에서 이 팩을 식별하는 문자열. 예: basic, pro")]
        [SerializeField] string id = "";

        [Tooltip("UI 표시 이름. 예: Basic Pack, Pro Pack")]
        [SerializeField] string displayName = "";

        [Header("비용")]
        [Tooltip("이 팩 1회 구매에 필요한 💎 (gem) 수")]
        [SerializeField, Range(1, 500)] int gemCost = 10;

        [Header("등급별 확률 (합 = 1.0)")]
        [Tooltip("리스트의 weight 합이 정확히 1이어야 함. OnValidate가 합을 체크해서 Console에 경고.")]
        [SerializeField] List<RarityWeight> weights = new();

        public string Id => id;
        public string DisplayName => displayName;
        public int GemCost => gemCost;
        public IReadOnlyList<RarityWeight> Weights => weights;

        // OnValidate: Inspector에서 값 변경할 때마다 Unity가 자동으로 호출.
        // 잘못된 데이터를 사전에 잡아내는 패턴.
        void OnValidate()
        {
            if (weights == null || weights.Count == 0) return;

            float sum = 0f;
            foreach (var w in weights) sum += w.weight;

            // 부동소수점 오차 허용치 0.001
            if (Mathf.Abs(sum - 1f) > 0.001f)
            {
                Debug.LogWarning(
                    $"[StaffPackSO:{name}] 등급 확률 합이 1.0이 아닙니다 (현재: {sum:F3}). " +
                    "weight 값들이 합쳐서 1.0이 되도록 조정해주세요.",
                    this);
            }
        }
    }
}
