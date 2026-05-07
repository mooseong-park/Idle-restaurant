#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Project.EditorTools.Builders;

namespace Project.EditorTools
{
    // SceneBuilder — Builders/ 4개 모듈을 묶는 dispatcher.
    //
    // 메뉴:
    //   1. Build > Sync Missing Only (기본, 안전) — 누락된 것만 추가, 사용자 튜닝 보존
    //   2. Build > Initial Setup            — 첫 셋업용. 사용자 튜닝 보존 + Sprite/UI 생성
    //   3. Build > Full Rebuild (Destructive) — 확인 다이얼로그 후 StaticScene + 모든 asset 강제 재생성
    //
    //   Legacy/ — 부분 갱신 메뉴 (사용자 워크플로우 호환):
    //     Rebuild Customer Prefab Only
    //     Configure Customer Sprites
    //     Apply Station Icons
    //
    // 빌더 책임 분할:
    //   - SpriteAssetBuilder : SF_*.png 생성 + Customer_*/Staff_Chef.png import 동기화
    //   - PrefabBuilder      : CustomerPrefab/StaffPrefab.prefab 생성
    //   - UIAssetBuilder     : HUD.uxml/uss seed 작성 (파일 존재 시 보존)
    //   - SceneLayoutBuilder : StaticScene tree + Camera + SceneController/UIController auto-wire
    public static class SceneBuilder
    {
        [MenuItem("Idle Restaurant/Build/Sync Missing Only")]
        public static void SyncMissingOnly()
        {
            // 가장 안전한 모드 — 사용자가 씬/prefab/uxml 에 손댔어도 그대로 보존.
            // 1) Sprite asset 은 파일 없을 때만 생성. import 설정도 사용자 PNG 는 안 건드림.
            SpriteAssetBuilder.EnsureAll(createIfMissing: true);
            var sprites = LoadSprites();

            // 2) Prefab 도 파일 없을 때만 생성 (기존 prefab 의 ref 슬롯/구조 보존).
            PrefabBuilder.EnsureAll(sprites.circle, sprites.square, sprites.chef, sprites.roundedSquare, createIfMissing: true);

            // 3) UI asset 도 파일 없을 때만 작성 (기존 uxml/uss 직접 편집 보존).
            UIAssetBuilder.EnsureAll();
            var (hudUxml, hudUss) = LoadUI();
            var (customerPrefab, staffPrefab) = LoadPrefabs();

            // 4) Scene hierarchy 는 StaticScene 없을 때만 fresh 생성. Camera 도 미설정일 때만.
            //    SceneController/UIController/GameManager ref 슬롯은 비어있을 때만 채움.
            SceneLayoutBuilder.BuildIncremental(sprites.circle, sprites.square, customerPrefab, staffPrefab, hudUxml, hudUss);

            Debug.Log("[SceneBuilder] Sync Missing Only 완료. 기존 씬/prefab/uxml 튜닝 모두 보존. " +
                      "처음 셋업이거나 일부 자산이 누락된 경우만 추가 생성됨.");
        }

        [MenuItem("Idle Restaurant/Build/Initial Setup")]
        public static void InitialSetup()
        {
            // 처음 셋업 — Sync Missing Only 와 거의 같으나 명시적 메뉴로 분리해 새 프로젝트 가이드 명료화.
            SyncMissingOnly();
        }

        [MenuItem("Idle Restaurant/Build/Full Rebuild (Destructive)")]
        public static void FullRebuildDestructive()
        {
            if (!EditorUtility.DisplayDialog(
                "Full Rebuild — 주의",
                "기존 StaticScene 을 모두 제거하고 prefab/uxml/카메라까지 강제 재생성합니다.\n" +
                "사용자가 씬 뷰에서 수동 조정한 anchor 위치/카메라 설정/prefab ref 슬롯이 모두 초기값으로 리셋됩니다.\n\n계속하시겠습니까?",
                "Full Rebuild", "취소")) return;

            // Sprite/Prefab/UI 모두 강제 재생성. 이후 Scene full rebuild.
            SpriteAssetBuilder.EnsureAll(createIfMissing: false);
            var sprites = LoadSprites();

            PrefabBuilder.EnsureAll(sprites.circle, sprites.square, sprites.chef, sprites.roundedSquare, createIfMissing: false);

            // UI: 사용자 직접 편집을 강제 덮어쓰지는 않음 (UIAssetBuilder 정책).
            // Full Rebuild 라도 .uxml/.uss 는 사용자 자산으로 간주 — 필요 시 직접 삭제 후 재실행.
            UIAssetBuilder.EnsureAll();
            var (hudUxml, hudUss) = LoadUI();
            var (customerPrefab, staffPrefab) = LoadPrefabs();

            SceneLayoutBuilder.BuildFullRebuild(sprites.circle, sprites.square, customerPrefab, staffPrefab, hudUxml, hudUss);

            Debug.Log("[SceneBuilder] Full Rebuild 완료. Ctrl+S 로 씬 저장 후 Play.");
        }

        // ===== Legacy 메뉴 (사용자 워크플로우 호환) =====

        [MenuItem("Idle Restaurant/Legacy/Rebuild Customer Prefab Only")]
        public static void RebuildCustomerPrefabOnly()
        {
            SpriteAssetBuilder.EnsureAll(createIfMissing: true);
            var sprites = LoadSprites();
            if (sprites.square == null || sprites.circle == null)
            {
                Debug.LogError("[Legacy] 기본 sprite (square/circle) 로드 실패 — Build > Initial Setup 한 번 실행 후 재시도.");
                return;
            }
            PrefabBuilder.RebuildCustomerOnly(sprites.circle, sprites.square, sprites.roundedSquare);
            Debug.Log("[Legacy] CustomerPrefab.prefab 재생성 완료. 씬/카메라/UI 미터치.");
        }

        [MenuItem("Idle Restaurant/Legacy/Configure Customer Sprites")]
        public static void ConfigureCustomerSprites()
        {
            int n = 0;
            if (System.IO.Directory.Exists(BuildPaths.ArtFolder))
            {
                foreach (var path in System.IO.Directory.GetFiles(BuildPaths.ArtFolder, "Customer_*.png"))
                {
                    SpriteAssetBuilder.ConfigureExternalSprite(path);
                    n++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (n == 0) Debug.LogWarning("[Legacy] Customer_*.png 못 찾음 — Assets/_Project/Art/ 파일명 확인.");
            else Debug.Log($"[Legacy] {n}개 Customer_*.png import 설정 동기화.");
        }

        [MenuItem("Idle Restaurant/Legacy/Apply Station Icons")]
        public static void ApplyStationIcons()
        {
            SceneLayoutBuilder.ApplyStationIcons();
        }

        // ===== 공용 loader =====

        struct LoadedSprites
        {
            public Sprite square;
            public Sprite circle;
            public Sprite roundedSquare;
            public Sprite chef;
        }

        static LoadedSprites LoadSprites() => new LoadedSprites
        {
            square        = AssetDatabase.LoadAssetAtPath<Sprite>(BuildPaths.SquarePath),
            circle        = AssetDatabase.LoadAssetAtPath<Sprite>(BuildPaths.CirclePath),
            roundedSquare = AssetDatabase.LoadAssetAtPath<Sprite>(BuildPaths.RoundedSquarePath),
            chef          = AssetDatabase.LoadAssetAtPath<Sprite>(BuildPaths.StaffChefPath),
        };

        static (VisualTreeAsset uxml, StyleSheet uss) LoadUI()
            => (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildPaths.HudUxmlPath),
                AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildPaths.HudUssPath));

        static (GameObject customer, GameObject staff) LoadPrefabs()
            => (AssetDatabase.LoadAssetAtPath<GameObject>(BuildPaths.CustomerPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(BuildPaths.StaffPrefabPath));
    }
}
#endif
