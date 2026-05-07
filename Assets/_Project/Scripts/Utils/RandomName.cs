using UnityEngine;
using Project.Data;

namespace Project.Utils
{
    // GameConfig의 firstNames/lastNames 풀에서 랜덤 조합 이름을 만든다.
    // 직원 spawn 시(초기 + 가챠) 두 곳에서 호출되므로 분리.
    public static class RandomName
    {
        public static string Make(GameConfigSO config)
        {
            var fn = config.FirstNames;
            var ln = config.LastNames;
            string first = fn.Count > 0 ? fn[Random.Range(0, fn.Count)] : "Staff";
            string last  = ln.Count > 0 ? ln[Random.Range(0, ln.Count)] : "";
            return string.IsNullOrEmpty(last) ? first : $"{first} {last}";
        }
    }
}
