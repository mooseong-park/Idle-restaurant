#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Project.Data;
using Project.UI;
using Project.Utils;
using Project.Visual;

namespace Project.EditorTools.Builders
{
    // StaticScene 트리 + Camera + SceneController/UIController 자동 연결.
    //
    // 모드:
    //   - BuildIncremental: StaticScene 이 이미 있으면 손대지 않음 (사용자 튜닝 보존). 없으면 fresh 생성.
    //                       Camera 도 orthographic 미설정일 때만 적용.
    //   - BuildFullRebuild: StaticScene 통째로 DestroyImmediate 후 재생성. Camera 강제 재설정.
    //                       (사용자 anchor 튜닝 모두 초기화 — 명시적 의도일 때만 사용)
    public static class SceneLayoutBuilder
    {
        // ===== Anchor seed positions (초기 layout, 빌드 후엔 Hierarchy가 truth) =====

        static readonly Vector2[] SeatSeeds = {
            new Vector2(85f, 42f), new Vector2(71f, 42f), new Vector2(57f, 42f),
            new Vector2(43f, 42f), new Vector2(29f, 42f), new Vector2(15f, 42f),
        };
        static readonly Vector2 BarSeed = new Vector2(50f, 50f);
        const float BarWidthPct  = 76f;
        const float BarHeightPct = 5f;
        static readonly Vector2[] StaffSlotSeeds = {
            new Vector2(50f, 65f), new Vector2(35f, 65f), new Vector2(65f, 65f),
            new Vector2(25f, 70f), new Vector2(75f, 70f), new Vector2(50f, 88f),
        };
        static readonly Vector2[] QueueSeeds = {
            new Vector2(70f, 17f), new Vector2(58f, 17f), new Vector2(46f, 17f),
            new Vector2(34f, 17f), new Vector2(22f, 17f), new Vector2(10f, 17f),
        };
        static readonly (string menuId, Vector2 pct, Color color)[] StationSeeds = {
            ("hotdog", new Vector2(25f, 78f), new Color(0.95f, 0.42f, 0.31f)),
            ("cola",   new Vector2(50f, 78f), new Color(0.30f, 0.67f, 0.94f)),
            ("salad",  new Vector2(75f, 78f), new Color(0.49f, 0.83f, 0.46f)),
        };
        static readonly Vector2 DoorSeed         = new Vector2(75f, 30f);
        static readonly Vector2 OutsideSpawnSeed = new Vector2(-5f, 17f);
        static readonly Vector2 TopExitSeed      = new Vector2(75f, -10f);
        static readonly Vector2 DoorOutsideSeed  = new Vector2(75f, 25f);
        static readonly Vector2 DoorInsideSeed   = new Vector2(75f, 35f);

        public static void BuildIncremental(Sprite circle, Sprite square,
            GameObject customerPrefab, GameObject staffPrefab,
            VisualTreeAsset hudUxml, StyleSheet hudUss)
        {
            var existing = GameObject.Find("StaticScene");
            if (existing == null)
            {
                BuildStaticSceneTree(circle, square);
            }
            // Camera: orthographic 인 경우만 보존, 아니면 적용.
            EnsureCamera(force: false);
            EnsureManagedGameObjects(customerPrefab, staffPrefab, hudUxml, hudUss, forceAssign: false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void BuildFullRebuild(Sprite circle, Sprite square,
            GameObject customerPrefab, GameObject staffPrefab,
            VisualTreeAsset hudUxml, StyleSheet hudUss)
        {
            var existing = GameObject.Find("StaticScene");
            if (existing != null) Object.DestroyImmediate(existing);

            BuildStaticSceneTree(circle, square);
            EnsureCamera(force: true);
            EnsureManagedGameObjects(customerPrefab, staffPrefab, hudUxml, hudUss, forceAssign: true);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 기존 씬을 재빌드하지 않고 Station_<id> 의 Icon 자식만 추가/갱신.
        // 외부에서 호출 (Legacy menu 호환).
        public static void ApplyStationIcons()
        {
            int added = 0, updated = 0, skipped = 0;
            for (int i = 0; i < StationSeeds.Length; i++)
            {
                var seed = StationSeeds[i];
                string stationName = $"Station_{seed.menuId}";
                var stationGo = GameObject.Find(stationName);
                if (stationGo == null) { skipped++; continue; }

                var menuItem = LoadMenuItem(seed.menuId);
                if (menuItem == null || menuItem.IconSprite == null) { skipped++; continue; }

                var iconT = stationGo.transform.Find("Icon");
                GameObject iconGo;
                bool existed = iconT != null;
                if (existed) iconGo = iconT.gameObject;
                else
                {
                    iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(stationGo.transform, false);
                    iconGo.transform.localPosition = Vector3.zero;
                    iconGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                }
                var iconSr = iconGo.GetComponent<SpriteRenderer>();
                if (iconSr == null) iconSr = iconGo.AddComponent<SpriteRenderer>();
                iconSr.sprite = menuItem.IconSprite;
                iconSr.sortingOrder = SortingOrders.StationIcon;
                EditorUtility.SetDirty(iconGo);

                if (existed) updated++; else added++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SceneLayoutBuilder] ApplyStationIcons: 추가={added}, 갱신={updated}, 스킵={skipped}.");
        }

        // ===== StaticScene 트리 생성 =====

        static void BuildStaticSceneTree(Sprite circle, Sprite square)
        {
            var root = new GameObject("StaticScene");
            Undo.RegisterCreatedObjectUndo(root, "Build StaticScene");

            root.AddComponent<SceneAnchors>();

            BuildZones(root.transform, square, circle);
            BuildBar(root.transform, square);
            BuildSeats(root.transform, circle);
            BuildStations(root.transform, square);
            BuildHiddenAnchors(root.transform);

            EditorUtility.SetDirty(root);
        }

        static void EnsureCamera(bool force)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[SceneLayoutBuilder] Main Camera 없음. 'MainCamera' 태그 카메라가 있는지 확인.");
                return;
            }
            if (!force && cam.orthographic) return; // 이미 설정됨 — 사용자 튜닝 보존

            Undo.RecordObject(cam, "Configure Camera");
            Undo.RecordObject(cam.transform, "Configure Camera");
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.94f, 0.95f, 0.96f);
            EditorUtility.SetDirty(cam.gameObject);
        }

        static void EnsureManagedGameObjects(GameObject customerPrefab, GameObject staffPrefab,
            VisualTreeAsset hudUxml, StyleSheet hudUss, bool forceAssign)
        {
            var anchorsRegistry = Object.FindFirstObjectByType<SceneAnchors>();

            // SceneController
            var scGo = EnsureGameObjectWithComponent("SceneController", typeof(SceneController));
            var sc = scGo.GetComponent<SceneController>();
            var visualSettings = AssetDatabase.LoadAssetAtPath<Project.Data.VisualSettingsSO>(BuildPaths.VisualSettingsPath);
            if (sc != null)
            {
                var so = new SerializedObject(sc);
                if (forceAssign) BuilderHelpers.AssignRef(so, "customerPrefab", customerPrefab);
                else BuilderHelpers.AssignRefIfEmpty(so, "customerPrefab", customerPrefab);
                if (forceAssign) BuilderHelpers.AssignRef(so, "staffPrefab", staffPrefab);
                else BuilderHelpers.AssignRefIfEmpty(so, "staffPrefab", staffPrefab);
                if (forceAssign) BuilderHelpers.AssignRef(so, "anchors", anchorsRegistry);
                else BuilderHelpers.AssignRefIfEmpty(so, "anchors", anchorsRegistry);
                if (forceAssign) BuilderHelpers.AssignRef(so, "visualSettings", visualSettings);
                else BuilderHelpers.AssignRefIfEmpty(so, "visualSettings", visualSettings);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sc);
            }

            // UIController + UIDocument
            var uiGo = EnsureGameObjectWithComponent("UIController", typeof(UIController), typeof(UIDocument));
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(BuildPaths.PanelSettingsPath);
            var gameEvents = AssetDatabase.LoadAssetAtPath<Project.Core.Events.GameEventsSO>(BuildPaths.GameEventsPath);
            var trackCardUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildPaths.TrackCardUxmlPath);
            if (ps != null)
            {
                var doc = uiGo.GetComponent<UIDocument>();
                if (doc.panelSettings == null) { doc.panelSettings = ps; EditorUtility.SetDirty(doc); }
                var uic = uiGo.GetComponent<UIController>();
                if (uic != null)
                {
                    var so = new SerializedObject(uic);
                    if (forceAssign) BuilderHelpers.AssignRef(so, "panelSettings", ps);
                    else BuilderHelpers.AssignRefIfEmpty(so, "panelSettings", ps);
                    if (forceAssign) BuilderHelpers.AssignRef(so, "hudUxml", hudUxml);
                    else BuilderHelpers.AssignRefIfEmpty(so, "hudUxml", hudUxml);
                    if (forceAssign) BuilderHelpers.AssignRef(so, "hudUss", hudUss);
                    else BuilderHelpers.AssignRefIfEmpty(so, "hudUss", hudUss);
                    if (forceAssign) BuilderHelpers.AssignRef(so, "gameEvents", gameEvents);
                    else BuilderHelpers.AssignRefIfEmpty(so, "gameEvents", gameEvents);
                    if (forceAssign) BuilderHelpers.AssignRef(so, "trackCardTemplate", trackCardUxml);
                    else BuilderHelpers.AssignRefIfEmpty(so, "trackCardTemplate", trackCardUxml);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(uic);
                }
            }
            else
            {
                Debug.LogWarning($"[SceneLayoutBuilder] '{BuildPaths.PanelSettingsPath}' 못 찾음. UIController에 직접 드래그.");
            }

            // GameManager (씬에 있으면 gameEvents 자동 연결)
            var gm = Object.FindFirstObjectByType<Project.Core.GameManager>();
            if (gm != null && gameEvents != null)
            {
                var soGm = new SerializedObject(gm);
                if (forceAssign) BuilderHelpers.AssignRef(soGm, "gameEvents", gameEvents);
                else BuilderHelpers.AssignRefIfEmpty(soGm, "gameEvents", gameEvents);
                soGm.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gm);
            }
            if (gameEvents == null)
                Debug.LogWarning($"[SceneLayoutBuilder] '{BuildPaths.GameEventsPath}' 못 찾음. UIController/GameManager에 직접 드래그.");
        }

        // ===== Bar / Seats / Stations / Anchors 빌드 =====

        static void BuildBar(Transform parent, Sprite square)
        {
            var bar = CreateRect("Bar", parent, BarSeed,
                new Vector2(SceneCoords.Width * BarWidthPct / 100f, SceneCoords.Height * BarHeightPct / 100f),
                new Color(0.55f, 0.38f, 0.22f), SortingOrders.Bar, square);
            bar.AddComponent<BarAnchor>();

            CreateRect("BarTop", parent,
                new Vector2(BarSeed.x, BarSeed.y - BarHeightPct * 0.45f),
                new Vector2(SceneCoords.Width * BarWidthPct / 100f, SceneCoords.Height * BarHeightPct * 0.10f / 100f),
                new Color(0.78f, 0.55f, 0.32f), SortingOrders.BarTop, square);
        }

        static void BuildSeats(Transform parent, Sprite circle)
        {
            for (int i = 0; i < SeatSeeds.Length; i++)
            {
                var go = CreateCircle($"Seat_{i}", parent, SeatSeeds[i], 0.35f,
                    new Color(0.85f, 0.83f, 0.80f), SortingOrders.Seat, circle);
                var anchor = go.AddComponent<SeatAnchor>();
                BuilderHelpers.AssignIntField(anchor, "seatIdx", i);
            }
        }

        static void BuildStations(Transform parent, Sprite square)
        {
            for (int i = 0; i < StationSeeds.Length; i++)
            {
                var seed = StationSeeds[i];
                var go = CreateRect($"Station_{seed.menuId}", parent, seed.pct,
                    new Vector2(0.7f, 0.7f), seed.color, SortingOrders.StationBg, square);
                var anchor = go.AddComponent<StationAnchor>();
                var menuItem = LoadMenuItem(seed.menuId);
                if (menuItem == null)
                {
                    Debug.LogError($"[SceneLayoutBuilder] MenuItemSO id='{seed.menuId}' 못 찾음. Settings/MenuItems/ 확인.");
                    continue;
                }
                BuilderHelpers.AssignObjectField(anchor, "menuItem", menuItem);

                if (menuItem.IconSprite != null)
                {
                    var iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(go.transform, false);
                    iconGo.transform.localPosition = Vector3.zero;
                    iconGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    var iconSr = iconGo.AddComponent<SpriteRenderer>();
                    iconSr.sprite = menuItem.IconSprite;
                    iconSr.sortingOrder = SortingOrders.StationIcon;
                }
                else
                    Debug.LogWarning($"[SceneLayoutBuilder] MenuItem '{seed.menuId}' IconSprite 미할당.");
            }
        }

        static void BuildHiddenAnchors(Transform parent)
        {
            var anchorRoot = new GameObject("Anchors");
            anchorRoot.transform.SetParent(parent, false);

            for (int i = 0; i < StaffSlotSeeds.Length; i++)
            {
                var go = CreateAnchorGo($"StaffSlot_{i}", anchorRoot.transform, StaffSlotSeeds[i]);
                var anchor = go.AddComponent<StaffSlotAnchor>();
                BuilderHelpers.AssignIntField(anchor, "slotIdx", i);
            }
            for (int i = 0; i < QueueSeeds.Length; i++)
            {
                var go = CreateAnchorGo($"QueueSlot_{i}", anchorRoot.transform, QueueSeeds[i]);
                var anchor = go.AddComponent<QueueSlotAnchor>();
                BuilderHelpers.AssignIntField(anchor, "queueIdx", i);
            }
            CreateAnchorGo("DoorAnchor", anchorRoot.transform, DoorSeed).AddComponent<DoorAnchor>();
            CreateAnchorGo("DoorOutsideAnchor", anchorRoot.transform, DoorOutsideSeed).AddComponent<DoorOutsideAnchor>();
            CreateAnchorGo("DoorInsideAnchor", anchorRoot.transform, DoorInsideSeed).AddComponent<DoorInsideAnchor>();
            CreateAnchorGo("OutsideSpawnAnchor", anchorRoot.transform, OutsideSpawnSeed).AddComponent<OutsideSpawnAnchor>();
            CreateAnchorGo("TopExitAnchor", anchorRoot.transform, TopExitSeed).AddComponent<TopExitAnchor>();
        }

        static void BuildZones(Transform parent, Sprite square, Sprite circle)
        {
            var zones = new GameObject("Zones");
            zones.transform.SetParent(parent, false);

            CreateRect("OutdoorFloor", zones.transform, new Vector2(50f, 50f),
                new Vector2(20f, 20f), new Color(0.66f, 0.85f, 0.54f), SortingOrders.OutdoorFloor, square);

            CreateRect("RoadFloor", zones.transform, new Vector2(50f, 5f),
                new Vector2(SceneCoords.Width * 1.4f, SceneCoords.Height * 0.10f),
                new Color(0.61f, 0.61f, 0.61f), SortingOrders.RoadFloor, square);

            float[] stripeXs = { 25f, 38f, 50f, 62f, 75f };
            for (int i = 0; i < stripeXs.Length; i++)
                CreateRect($"RoadStripe_{i}", zones.transform, new Vector2(stripeXs[i], 5f),
                    new Vector2(0.3f, 0.55f), new Color(0.95f, 0.95f, 0.95f), SortingOrders.RoadStripe, square);

            CreateRect("DiningFloor", zones.transform, new Vector2(50f, 42.5f),
                new Vector2(SceneCoords.Width * 0.80f, SceneCoords.Height * 0.25f),
                new Color(0.94f, 0.88f, 0.78f), SortingOrders.Floor, square);

            CreateRect("KitchenFloor", zones.transform, new Vector2(50f, 75f),
                new Vector2(SceneCoords.Width * 0.80f, SceneCoords.Height * 0.40f),
                new Color(0.86f, 0.86f, 0.86f), SortingOrders.Floor, square);

            CreateCircle("GrassDecor_L", zones.transform, new Vector2(5f, 50f), 0.5f,
                new Color(0.40f, 0.62f, 0.32f), SortingOrders.GrassDecor, circle);
            CreateCircle("GrassDecor_R", zones.transform, new Vector2(95f, 50f), 0.5f,
                new Color(0.40f, 0.62f, 0.32f), SortingOrders.GrassDecor, circle);

            CreateRect("KitchenDivider", zones.transform, new Vector2(50f, 55f),
                new Vector2(SceneCoords.Width * 0.80f, 0.04f),
                new Color(0.66f, 0.52f, 0.34f), SortingOrders.KitchenDivider, square);

            const float wallThick = 0.10f;
            var wallColor = new Color(0.42f, 0.29f, 0.17f);
            CreateRect("Wall_TopLeft", zones.transform, new Vector2(40f, 30f),
                new Vector2(SceneCoords.Width * 0.60f, wallThick), wallColor, SortingOrders.BuildingWall, square);
            CreateRect("Wall_TopRight", zones.transform, new Vector2(85f, 30f),
                new Vector2(SceneCoords.Width * 0.10f, wallThick), wallColor, SortingOrders.BuildingWall, square);
            CreateRect("Wall_Bottom", zones.transform, new Vector2(50f, 95f),
                new Vector2(SceneCoords.Width * 0.80f, wallThick), wallColor, SortingOrders.BuildingWall, square);
            CreateRect("Wall_Left", zones.transform, new Vector2(10f, 62.5f),
                new Vector2(wallThick, SceneCoords.Height * 0.65f), wallColor, SortingOrders.BuildingWall, square);
            CreateRect("Wall_Right", zones.transform, new Vector2(90f, 62.5f),
                new Vector2(wallThick, SceneCoords.Height * 0.65f), wallColor, SortingOrders.BuildingWall, square);

            CreateRect("DoorFrame", zones.transform, new Vector2(75f, 30f),
                new Vector2(SceneCoords.Width * 0.10f, wallThick * 1.5f),
                new Color(0.67f, 0.45f, 0.27f), SortingOrders.DoorFrame, square);
        }

        // ===== GameObject helpers =====

        static GameObject CreateAnchorGo(string name, Transform parent, Vector2 pct)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = SceneCoords.ToWorld(pct);
            return go;
        }

        static GameObject CreateRect(string name, Transform parent, Vector2 pct, Vector2 sizeWorld, Color color, int sortingOrder, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = SceneCoords.ToWorld(pct);
            go.transform.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        static GameObject CreateCircle(string name, Transform parent, Vector2 pct, float diameter, Color color, int sortingOrder, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = SceneCoords.ToWorld(pct);
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        static GameObject EnsureGameObjectWithComponent(string name, params System.Type[] components)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            }
            foreach (var t in components)
                if (go.GetComponent(t) == null) go.AddComponent(t);
            EditorUtility.SetDirty(go);
            return go;
        }

        static MenuItemSO LoadMenuItem(string id)
        {
            var guids = AssetDatabase.FindAssets("t:MenuItemSO");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<MenuItemSO>(path);
                if (so != null && so.Id == id) return so;
            }
            return null;
        }
    }
}
#endif
