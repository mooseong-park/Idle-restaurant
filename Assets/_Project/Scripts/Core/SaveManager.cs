using System;
using UnityEngine;
using Project.Data;
using Project.Domain;
using Project.Systems;

namespace Project.Core
{
    // PlayerPrefs + JsonUtility 기반 단일 슬롯 세이브.
    // 저장 대상:
    //   - 진행: Day, Cash, Gems, Rating, 8 트랙 Lv
    //   - 누적 통계: Successes, PatienceFails, DayAchieved, TotalRevenue, PassiveRevenue
    //   - 직원 명단 (이름 + 등급 SO 이름)
    // 저장 안 함:
    //   - 손님 transient state (Day reset 시 어차피 사라짐)
    //   - DayRevenue / dayElapsed (load 시 fresh day로 시작 — 부분 진행 보존은 복잡도 ↑)
    //   - DayPhase (load 직후엔 항상 Playing)
    public static class SaveManager
    {
        const string SaveKey = "save_v1";

        public static bool HasSave() => PlayerPrefs.HasKey(SaveKey);

        public static void Save(GameState state, StaffSystem staffSystem)
        {
            var data = new GameStateSaveData
            {
                version = 1,
                day = state.Day,
                cash = state.Cash,
                gems = state.Gems,
                rating = state.Rating,
                menuLv = state.MenuLv,
                staffLv = state.StaffLv,
                serviceLv = state.ServiceLv,
                venueLv = state.VenueLv,
                takeoutLv = state.TakeoutLv,
                adsLv = state.AdsLv,
                tipLv = state.TipLv,
                merchLv = state.MerchLv,
                successes = state.Successes,
                patienceFails = state.PatienceFails,
                dayAchieved = state.DayAchieved,
                totalRevenue = state.TotalRevenue,
                passiveRevenue = state.PassiveRevenue,
            };

            var staffs = staffSystem.Staffs;
            data.staff = new StaffSaveEntry[staffs.Count];
            for (int i = 0; i < staffs.Count; i++)
            {
                var s = staffs[i];
                data.staff[i] = new StaffSaveEntry
                {
                    name = s.Name,
                    rarityAsset = s.Rarity != null ? s.Rarity.name : "",
                };
            }

            try
            {
                var json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save 실패: {e.Message}");
            }
        }

        // load 성공 시 staff list까지 복원하고 true 반환. 호출자는 SpawnInitialStaff 스킵.
        public static bool TryLoad(GameState state, GameConfigSO config, StaffSystem staffSystem)
        {
            if (!HasSave()) return false;
            try
            {
                var json = PlayerPrefs.GetString(SaveKey);
                if (string.IsNullOrEmpty(json)) return false;
                var data = JsonUtility.FromJson<GameStateSaveData>(json);
                if (data == null || data.version != 1) return false;

                state.Day = Mathf.Max(1, data.day);
                state.Cash = data.cash;
                state.Gems = data.gems;
                state.Rating = data.rating;
                state.MenuLv = Mathf.Max(1, data.menuLv);
                state.StaffLv = Mathf.Max(1, data.staffLv);
                state.ServiceLv = Mathf.Max(1, data.serviceLv);
                state.VenueLv = Mathf.Max(1, data.venueLv);
                state.TakeoutLv = Mathf.Max(1, data.takeoutLv);
                state.AdsLv = Mathf.Max(1, data.adsLv);
                state.TipLv = Mathf.Max(1, data.tipLv);
                state.MerchLv = Mathf.Max(1, data.merchLv);
                state.Successes = data.successes;
                state.PatienceFails = data.patienceFails;
                state.DayAchieved = data.dayAchieved;
                state.TotalRevenue = data.totalRevenue;
                state.PassiveRevenue = data.passiveRevenue;
                state.DayRevenue = 0f;

                if (data.staff != null)
                {
                    for (int i = 0; i < data.staff.Length; i++)
                    {
                        var entry = data.staff[i];
                        if (entry == null) continue;
                        var rarity = FindRarity(config, entry.rarityAsset);
                        if (rarity == null)
                        {
                            Debug.LogWarning($"[SaveManager] 직원 '{entry.name}' 등급 SO '{entry.rarityAsset}' 못 찾음 — 스킵.");
                            continue;
                        }
                        staffSystem.SpawnStaff(rarity, entry.name);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load 실패: {e.Message}. 새 게임으로 시작.");
                return false;
            }
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        static RarityConfigSO FindRarity(GameConfigSO config, string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            var list = config.Rarities;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].name == assetName) return list[i];
            return null;
        }

        // JsonUtility 직렬화 대상 — public 필드만 (속성 X).
        [Serializable]
        public class GameStateSaveData
        {
            public int version;
            public int day;
            public float cash;
            public int gems;
            public int rating;
            public int menuLv;
            public int staffLv;
            public int serviceLv;
            public int venueLv;
            public int takeoutLv;
            public int adsLv;
            public int tipLv;
            public int merchLv;
            public int successes;
            public int patienceFails;
            public int dayAchieved;
            public float totalRevenue;
            public float passiveRevenue;
            public StaffSaveEntry[] staff = Array.Empty<StaffSaveEntry>();
        }

        [Serializable]
        public class StaffSaveEntry
        {
            public string name;
            public string rarityAsset; // RarityConfigSO 의 asset name (e.g. "Novice", "Pro", "Expert")
        }
    }
}
