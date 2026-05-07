#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.EditorTools
{
    // 씬 진단 + Menu Station 아이콘 PNG 생성/적용 헬퍼.
    //
    // Day 14 정리 후 잔존 기능:
    //   - Cleanup/Diagnose Missing Scripts (씬 진단)
    //   - Generate Menu Icon Placeholders / Add Station Icons (procedural PNG → station 아이콘 적용)
    //
    // 제거된 obsolete 기능 (Day 14):
    //   - AddDoorWaypointsIfMissing: SceneBuilder 가 Door 관련 anchor 항상 생성
    //   - RestoreUserLayout: 일회성 하드코딩 좌표. Build > Sync Missing Only 가 사용자 튜닝 보존
    //   - FixLayoutGeometry: 일회성 기하 보정
    public static class CleanupTools
    {
        [MenuItem("Idle Restaurant/Cleanup Missing Scripts (active scene)")]
        public static void CleanupMissingScripts()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            int totalRemoved = 0;
            int affectedGameObjectCount = 0;

            foreach (var root in rootObjects)
            {
                var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var t in transforms)
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (missingCount > 0)
                    {
                        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                        totalRemoved += removed;
                        affectedGameObjectCount++;
                        Debug.Log($"[Cleanup] {GetHierarchyPath(t)} → missing script {removed}개 제거", t.gameObject);
                    }
                }
            }

            if (totalRemoved > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[Cleanup] 완료. {affectedGameObjectCount}개 GameObject에서 총 {totalRemoved}개 missing script 제거. " +
                          "Ctrl+S로 씬 저장하세요.");
            }
            else
            {
                Debug.Log("[Cleanup] missing script 없음.");
            }
        }

        [MenuItem("Idle Restaurant/Diagnose Scene (count missing scripts)")]
        public static void DiagnoseScene()
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            int totalMissing = 0;
            int affectedCount = 0;

            foreach (var root in rootObjects)
            {
                var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
                foreach (var t in transforms)
                {
                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (missingCount > 0)
                    {
                        totalMissing += missingCount;
                        affectedCount++;
                        Debug.LogWarning($"[Diagnose] {GetHierarchyPath(t)} → missing script {missingCount}개", t.gameObject);
                    }
                }
            }

            if (totalMissing > 0)
                Debug.LogWarning($"[Diagnose] 총 {affectedCount}개 GameObject에 {totalMissing}개 missing script. " +
                                 "'Idle Restaurant > Cleanup Missing Scripts' 실행해 정리하세요.");
            else
                Debug.Log("[Diagnose] missing script 없음.");
        }

        static string GetHierarchyPath(Transform t)
        {
            if (t.parent == null) return t.name;
            return GetHierarchyPath(t.parent) + "/" + t.name;
        }

        // ===== Menu 아이콘 procedural PNG 생성 =====
        //
        // SpriteFactory 의 도형 생성 패턴과 동일하게 픽셀 단위로 그려서 PNG 로 저장.
        // 결과물 = 프로그래머 아트 수준 (도형 합성). 일러스트 풍은 외부 art 필요.
        // 사용자가 직접 art 가져오면 동일 경로의 PNG 만 덮어쓰면 됨.
        [MenuItem("Idle Restaurant/Legacy/Generate Menu Icon Placeholders (procedural)")]
        public static void GenerateMenuIconPlaceholders()
        {
            const string ArtFolder = "Assets/_Project/Art";
            if (!System.IO.Directory.Exists(ArtFolder))
                System.IO.Directory.CreateDirectory(ArtFolder);

            SaveIconPng($"{ArtFolder}/Menu_Hotdog.png", MakeBurgerIconTex());
            SaveIconPng($"{ArtFolder}/Menu_Cola.png",   MakeColaIconTex());
            SaveIconPng($"{ArtFolder}/Menu_Salad.png",  MakeSaladIconTex());

            AssetDatabase.Refresh();

            Debug.Log("[Icons] 메뉴 아이콘 PNG 3개 생성 완료 (Menu_Hotdog/Cola/Salad). " +
                      "'Idle Restaurant > Legacy > Add Station Icons' 메뉴 실행해 적용하세요. " +
                      "마음에 안 들면 같은 경로의 PNG 만 외부 art로 덮어쓰면 됨.");
        }

        static void SaveIconPng(string path, Texture2D tex)
        {
            var bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);
        }

        // 햄버거 — 5단 가로 띠 (위에서: 빵→양상추→토마토→패티→빵)
        static Texture2D MakeBurgerIconTex()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];

            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outline    = new Color32(45, 25, 10, 255);
            Color32 bunTop     = new Color32(220, 158, 82, 255);
            Color32 lettuce    = new Color32(140, 198, 82, 255);
            Color32 tomato     = new Color32(220, 80, 70, 255);
            Color32 patty      = new Color32(120, 70, 40, 255);
            Color32 bunBottom  = new Color32(200, 138, 70, 255);
            Color32 sesame     = new Color32(255, 240, 200, 255);

            Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / (size * 0.42f);
                float dy = (y - center.y) / (size * 0.40f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                if (r > 1f) { pixels[y * size + x] = transparent; continue; }

                float yPct = y / (float)(size - 1);
                Color32 c;
                if      (yPct > 0.65f) c = bunTop;
                else if (yPct > 0.55f) c = lettuce;
                else if (yPct > 0.45f) c = tomato;
                else if (yPct > 0.30f) c = patty;
                else                   c = bunBottom;

                if (yPct > 0.78f && yPct < 0.92f)
                {
                    int hash = (x * 13 + y * 7) % 41;
                    if (hash < 2) c = sesame;
                }

                if (r > 0.94f) c = outline;
                pixels[y * size + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = "Menu_Hotdog_Gen";
            return tex;
        }

        // 콜라 — 컵에 콜라 + 위 거품 + 빨대
        static Texture2D MakeColaIconTex()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];

            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outline = new Color32(30, 15, 8, 255);
            Color32 cola    = new Color32(80, 35, 15, 255);
            Color32 foam    = new Color32(225, 205, 175, 255);
            Color32 straw   = new Color32(225, 60, 60, 255);

            float cupCenterX = size / 2f;
            float cupTop     = size * 0.85f;
            float cupBot     = size * 0.10f;
            float cupHalfW   = size * 0.22f;
            float cupBotR    = size * 0.10f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - cupCenterX);
                bool insideCup;

                if (y < cupBot + cupBotR)
                {
                    float dy = (cupBot + cupBotR) - y;
                    float dxClamped = Mathf.Max(0, dx - (cupHalfW - cupBotR));
                    insideCup = (dxClamped * dxClamped + dy * dy) <= (cupBotR * cupBotR) || (dx <= cupHalfW - cupBotR && y >= cupBot);
                }
                else
                {
                    insideCup = dx <= cupHalfW && y >= cupBot && y <= cupTop;
                }

                if (!insideCup) { pixels[y * size + x] = transparent; continue; }

                float yPctInCup = (y - cupBot) / (cupTop - cupBot);
                Color32 c;
                if (yPctInCup > 0.85f) c = foam;
                else c = cola;

                int strawX = (int)(cupCenterX - cupHalfW * 0.55f + (yPctInCup - 0.7f) * 6f);
                if (yPctInCup > 0.75f && Mathf.Abs(x - strawX) < 3) c = straw;

                if (Mathf.Abs(dx - cupHalfW) < 1.5f && y >= cupBot && y <= cupTop) c = outline;
                if (Mathf.Abs(y - cupTop) < 1.5f && dx <= cupHalfW)                c = outline;

                pixels[y * size + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = "Menu_Cola_Gen";
            return tex;
        }

        // 샐러드 — 둥근 그릇에 잎 + 토마토 한 조각
        static Texture2D MakeSaladIconTex()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];

            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outline    = new Color32(30, 50, 20, 255);
            Color32 leafLight  = new Color32(140, 198, 82, 255);
            Color32 leafDark   = new Color32(85, 145, 55, 255);
            Color32 tomato     = new Color32(220, 80, 70, 255);
            Color32 tomatoSeed = new Color32(255, 220, 100, 255);

            Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / (size * 0.42f);
                float dy = (y - center.y) / (size * 0.38f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                if (r > 1f) { pixels[y * size + x] = transparent; continue; }

                int patternIdx = ((x / 9) + (y / 7)) % 4;
                Color32 c = (patternIdx == 0 || patternIdx == 2) ? leafLight : leafDark;

                float tomatoCx = size * 0.65f;
                float tomatoCy = size * 0.65f;
                float td = Vector2.Distance(new Vector2(x, y), new Vector2(tomatoCx, tomatoCy));
                if (td < 8) c = tomato;
                if (td < 2) c = tomatoSeed;

                if (r > 0.93f) c = outline;
                pixels[y * size + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = "Menu_Salad_Gen";
            return tex;
        }

        // ===== Menu Station 아이콘 PNG 적용 =====
        //
        // Station_hotdog / cola / salad 에 아이콘 PNG 자식 GameObject (IconBody) 추가.
        // PNG 없으면 procedural placeholder 자동 생성.
        // 비파괴 — 기존 Station 색깔 사각형은 그대로 두고 IconBody만 추가/갱신.
        // 동시에 MenuItemSO.iconSprite 도 자동 연결 (말풍선 등에서 같은 sprite 사용).
        [MenuItem("Idle Restaurant/Legacy/Add Station Icons (non-destructive)")]
        public static void AddStationIcons()
        {
            (string menuId, string fileName)[] entries =
            {
                ("hotdog", "Menu_Hotdog.png"),
                ("cola",   "Menu_Cola.png"),
                ("salad",  "Menu_Salad.png"),
            };

            int updated = 0;

            foreach (var entry in entries)
            {
                string iconPath = $"Assets/_Project/Art/{entry.fileName}";

                if (!System.IO.File.Exists(iconPath))
                {
                    Texture2D placeholder = entry.menuId switch
                    {
                        "hotdog" => MakeBurgerIconTex(),
                        "cola"   => MakeColaIconTex(),
                        "salad"  => MakeSaladIconTex(),
                        _ => null
                    };
                    if (placeholder == null) continue;
                    if (!System.IO.Directory.Exists("Assets/_Project/Art"))
                        System.IO.Directory.CreateDirectory("Assets/_Project/Art");
                    SaveIconPng(iconPath, placeholder);
                    AssetDatabase.ImportAsset(iconPath);
                    Debug.Log($"[Icons] {iconPath} placeholder 자동 생성");
                }

                ConfigureMenuIconImport(iconPath);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[Icons] Sprite import 실패: {iconPath}");
                    continue;
                }

                AssignMenuItemIcon(entry.menuId, sprite);

                var station = GameObject.Find($"Station_{entry.menuId}");
                if (station == null)
                {
                    Debug.LogWarning($"[Icons] Station_{entry.menuId} 못 찾음 — 스킵");
                    continue;
                }

                if (EnsureStationIcon(station, sprite)) updated++;
            }

            if (updated > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Icons] {updated}개 station 아이콘 적용 완료. Ctrl+S 저장.");
            }
        }

        static void AssignMenuItemIcon(string menuId, Sprite sprite)
        {
            var guids = AssetDatabase.FindAssets("t:MenuItemSO");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<Project.Data.MenuItemSO>(path);
                if (so == null || so.Id != menuId) continue;
                var sObj = new SerializedObject(so);
                var prop = sObj.FindProperty("iconSprite");
                if (prop != null && prop.objectReferenceValue != sprite)
                {
                    prop.objectReferenceValue = sprite;
                    sObj.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(so);
                    Debug.Log($"[Icons] MenuItemSO '{menuId}' iconSprite 자동 연결", so);
                }
                break;
            }
        }

        static bool EnsureStationIcon(GameObject station, Sprite icon)
        {
            var stationSr = station.GetComponent<SpriteRenderer>();
            int bgSortingOrder = stationSr != null ? stationSr.sortingOrder : Project.Visual.SortingOrders.StationBg;

            Transform iconT = station.transform.Find("IconBody");
            if (iconT == null)
            {
                var iconGo = new GameObject("IconBody");
                iconGo.transform.SetParent(station.transform, false);
                iconGo.transform.localPosition = Vector3.zero;
                iconGo.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                var sr = iconGo.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                sr.color = Color.white;
                sr.sortingOrder = bgSortingOrder + 1;
                Undo.RegisterCreatedObjectUndo(iconGo, "Add Station Icon");
                Debug.Log($"[Icons] {station.name} IconBody 신규 생성", station);
                return true;
            }
            else
            {
                var sr = iconT.GetComponent<SpriteRenderer>();
                if (sr == null) return false;
                if (sr.sprite == icon) return false;
                Undo.RecordObject(sr, "Update Station Icon");
                sr.sprite = icon;
                sr.sortingOrder = bgSortingOrder + 1;
                EditorUtility.SetDirty(sr);
                Debug.Log($"[Icons] {station.name} IconBody sprite 갱신", station);
                return true;
            }
        }

        static void ConfigureMenuIconImport(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float targetPpu = tex != null ? Mathf.Max(tex.width, tex.height) : 512f;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - targetPpu) > 0.5f) { importer.spritePixelsPerUnit = targetPpu; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }
    }
}
#endif
