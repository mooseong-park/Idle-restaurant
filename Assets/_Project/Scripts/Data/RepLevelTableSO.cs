using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Idle Restaurant/Rep Level Table", fileName = "RepLevelTable")]
    public sealed class RepLevelTableSO : ScriptableObject
    {
        [Serializable]
        public struct RepLevel
        {
            [Tooltip("이 Lv에서 매일 달성해야 하는 매출 목표 ($).")]
            public int goal;

            [Tooltip("다음 Lv로 가기 위해 누적해야 하는 ⭐ 개수. 0이면 최대 Lv (다음 없음).")]
            public int starsToNext;
        }

        [Header("명성 Lv 표")]
        [Tooltip("리스트 0번 항목이 Lv 1. starsToNext가 0인 항목이 최종 Lv.")]
        [SerializeField] List<RepLevel> levels = new();

        public IReadOnlyList<RepLevel> Levels => levels;
    }
}
