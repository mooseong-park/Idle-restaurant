#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Visual;

namespace Project.EditorTools
{
    // Scene 파괴 없이 Missing Script(깨진 스크립트 참조) 컴포넌트만 제거하는 헬퍼.
    // SceneBuilder 재실행 = StaticScene 통째 재생성 (사용자 수동 작업 손실) 이라 사용 X.
    // 이 도구는 만약 anchor/scene 컴포넌트 중 깨진 게 있으면 그것만 골라서 제거.
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
                Debug.Log("[Cleanup] missing script 없음. (에러 원인이 다른 곳일 수 있음 — 보고서 참조)");
            }
        }

        // missing script 개수만 보고 (제거 X) — 진단용
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
                Debug.Log("[Diagnose] missing script 없음 — 에러는 다른 원인 (stale Inspector 등). " +
                          "Hierarchy 빈 영역 클릭 또는 Inspector 닫았다 열기 시도.");
        }

        static string GetHierarchyPath(Transform t)
        {
            if (t.parent == null) return t.name;
            return GetHierarchyPath(t.parent) + "/" + t.name;
        }

        // ===== Door Waypoint 비파괴 셋업 =====
        //
        // 기존 씬에 DoorOutsideAnchor / DoorInsideAnchor 가 없으면 추가.
        // 이미 있으면 그대로 둠 (사용자가 옮긴 위치 보존).
        // SceneBuilder 재실행 없이 waypoint 도입할 때 사용.
        [MenuItem("Idle Restaurant/Legacy/Add Door Waypoints (non-destructive)")]
        public static void AddDoorWaypointsIfMissing()
        {
            var doorAnchor = Object.FindFirstObjectByType<DoorAnchor>();
            if (doorAnchor == null)
            {
                Debug.LogError("[Setup] DoorAnchor 미발견. 먼저 'Build Static Scene' 또는 수동으로 DoorAnchor 추가 필요.");
                return;
            }
            Vector3 doorPos = doorAnchor.transform.position;

            // Anchors 컨테이너 찾기 (DoorAnchor의 부모로 추정)
            Transform anchorParent = doorAnchor.transform.parent;
            if (anchorParent == null)
            {
                Debug.LogWarning("[Setup] DoorAnchor가 root에 있음. waypoint도 root에 생성.");
            }

            int created = 0;
            created += EnsureWaypoint<DoorOutsideAnchor>(
                "DoorOutsideAnchor", anchorParent,
                doorPos + new Vector3(0f, 0.4f, 0f));  // 도어 위 (외부 측)

            created += EnsureWaypoint<DoorInsideAnchor>(
                "DoorInsideAnchor", anchorParent,
                doorPos + new Vector3(0f, -0.4f, 0f)); // 도어 아래 (실내 측)

            if (created > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Setup] Door Waypoint {created}개 생성. " +
                          "Scene view에서 위치 조정 가능. 도어 corridor (외부 측 ↔ 실내 측) 으로 손님이 통과합니다. Ctrl+S 저장.");
            }
            else
            {
                Debug.Log("[Setup] Door Waypoint 모두 이미 존재 — 변경 없음. " +
                          "위치는 사용자 조정값 보존됨.");
            }
        }

        static int EnsureWaypoint<T>(string name, Transform parent, Vector3 worldPos) where T : MonoBehaviour
        {
            // 이미 있으면 skip
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null) return 0;

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, $"Add {name}");
            Debug.Log($"[Setup] {name} 생성 @ {worldPos}", go);
            return 1;
        }

        // ===== 사용자 커스텀 레이아웃 복원 =====
        //
        // Build Static Scene이 실행되어 사용자 커스텀 위치가 SceneBuilder 기본값으로 리셋됐을 때,
        // 대화 도중 기록된 사용자 커스텀 좌표로 transform.position만 되돌림 (비파괴).
        //
        // 복원 대상 (사용자 스크린샷 + 이전 파일 read 기록 기반):
        //   - 도어/벽/스폰/퇴장: 천장(top wall)을 y=2.6으로 올림 (기본 1.6 → 2.6)
        //   - 좌석: y=1.64 (기본 0.64 → 1.64) — 천장 바로 아래 한 줄
        //   - 바: y=1.0 (기본 0 → 1.0) — 좌석 아래 가운데
        //   - 큐: y=3 (기본 2.64 → 3.0) — 도어 위쪽 잔디
        //
        // 알려지지 않은 위치 (스테이션, 직원슬롯, 일부 벽)는 변경 X.
        [MenuItem("Idle Restaurant/Legacy/Restore User Custom Layout")]
        public static void RestoreUserLayout()
        {
            int updated = 0;

            // 도어 + 외부 anchor
            updated += SetPos("DoorAnchor",         new Vector3(2.5f,  2.6f,  0f));
            updated += SetPos("OutsideSpawnAnchor", new Vector3(-5.5f, 3.64f, 0f));
            updated += SetPos("TopExitAnchor",      new Vector3(3.5f,  5.8f,  0f));
            updated += SetPos("DoorOutsideAnchor",  new Vector3(2.5f,  3.0f,  0f)); // 도어 위 (큐 라인과 비슷)
            updated += SetPos("DoorInsideAnchor",   new Vector3(2.5f,  2.2f,  0f)); // 도어 아래 (실내 진입)

            // 천장(top wall) y=2.6으로 (기본 1.6 → 2.6)
            updated += SetPos("Wall_TopLeft",  new Vector3(-1f,  2.6f, 0f));
            updated += SetPos("Wall_TopRight", new Vector3(3.5f, 2.6f, 0f));

            // 좌석 6개 — y=1.64, 기본 x 유지 (3.5/2.1/0.7/-0.7/-2.1/-3.5)
            updated += SetPos("Seat_0", new Vector3( 3.5f, 1.64f, 0f));
            updated += SetPos("Seat_1", new Vector3( 2.1f, 1.64f, 0f));
            updated += SetPos("Seat_2", new Vector3( 0.7f, 1.64f, 0f));
            updated += SetPos("Seat_3", new Vector3(-0.7f, 1.64f, 0f));
            updated += SetPos("Seat_4", new Vector3(-2.1f, 1.64f, 0f));
            updated += SetPos("Seat_5", new Vector3(-3.5f, 1.64f, 0f));

            // 바 — y=1.0 (좌석 바로 아래)
            updated += SetPos("Bar",    new Vector3(0f, 1f,    0f));
            updated += SetPos("BarTop", new Vector3(0f, 1.18f, 0f)); // 바 위 하이라이트 (살짝 위)

            // 큐 6슬롯 — y=3, x는 도어(2.5) 우측부터 좌측으로
            updated += SetPos("QueueSlot_0", new Vector3( 2f,    3f,    0f));
            updated += SetPos("QueueSlot_1", new Vector3( 0.85f, 3f,    0f));
            updated += SetPos("QueueSlot_2", new Vector3(-0.30f, 3f,    0f));
            updated += SetPos("QueueSlot_3", new Vector3(-1.45f, 3f,    0f));
            updated += SetPos("QueueSlot_4", new Vector3(-2.60f, 3f,    0f));
            updated += SetPos("QueueSlot_5", new Vector3(-3.49f, 3.02f, 0f));

            if (updated > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Restore] 사용자 커스텀 레이아웃 복원 완료. {updated}개 GameObject 위치 변경. " +
                          "Ctrl+S로 씬 저장하세요.\n" +
                          "참고: 스테이션/직원슬롯/하단·좌우 벽/Floor zones 는 변경하지 않음 (이전 커스텀 정보 없음). " +
                          "필요 시 Scene view에서 직접 조정.");
            }
            else
            {
                Debug.LogWarning("[Restore] 복원 대상 GameObject 못 찾음. SceneBuilder 미실행 상태일 가능성.");
            }
        }

        // 이름으로 GameObject 찾아 위치 변경. 못 찾으면 0 반환 (count 누적).
        static int SetPos(string goName, Vector3 worldPos)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                Debug.LogWarning($"[Restore] '{goName}' 못 찾음 — 스킵");
                return 0;
            }
            Undo.RecordObject(go.transform, $"Restore {goName}");
            go.transform.position = worldPos;
            EditorUtility.SetDirty(go);
            return 1;
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
                      "'Idle Restaurant > Add Station Icons (non-destructive)' 메뉴 실행해 적용하세요. " +
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
                if      (yPct > 0.65f) c = bunTop;     // top bun
                else if (yPct > 0.55f) c = lettuce;
                else if (yPct > 0.45f) c = tomato;
                else if (yPct > 0.30f) c = patty;
                else                   c = bunBottom;

                // 깨알 sesame on top bun
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
            float cupBotR    = size * 0.10f; // bottom corner radius

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - cupCenterX);
                bool insideCup;

                if (y < cupBot + cupBotR)
                {
                    // rounded bottom
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

                // 빨대 — 컵 좌측에 살짝 기울어진 막대
                int strawX = (int)(cupCenterX - cupHalfW * 0.55f + (yPctInCup - 0.7f) * 6f);
                if (yPctInCup > 0.75f && Mathf.Abs(x - strawX) < 3) c = straw;

                // 외곽선
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

                // 토마토 (오른쪽 위)
                float tomatoCx = size * 0.65f;
                float tomatoCy = size * 0.65f;
                float td = Vector2.Distance(new Vector2(x, y), new Vector2(tomatoCx, tomatoCy));
                if (td < 8) c = tomato;
                if (td < 2) c = tomatoSeed;

                // 외곽선
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
        // 셰프(StaffPrefab)가 BgFrame + Body sprite 패턴인 것처럼,
        // Station_hotdog / cola / salad 에 아이콘 PNG 자식 GameObject 추가.
        // PNG 는 사용자가 Assets/_Project/Art/Menu_*.png 에 미리 저장.
        // 비파괴 — 기존 Station 색깔 사각형은 그대로 두고 IconBody만 추가/갱신.
        [MenuItem("Idle Restaurant/Legacy/Add Station Icons (non-destructive)")]
        public static void AddStationIcons()
        {
            // 메뉴 id → PNG 파일명 매핑
            (string menuId, string fileName)[] entries =
            {
                ("hotdog", "Menu_Hotdog.png"),
                ("cola",   "Menu_Cola.png"),
                ("salad",  "Menu_Salad.png"),
            };

            int updated = 0;
            int missingPng = 0;

            foreach (var entry in entries)
            {
                string iconPath = $"Assets/_Project/Art/{entry.fileName}";

                // PNG 없으면 procedural placeholder 자동 생성 (사용자 별도 메뉴 실행 부담 제거)
                if (!System.IO.File.Exists(iconPath))
                {
                    Texture2D placeholder = entry.menuId switch
                    {
                        "hotdog" => MakeBurgerIconTex(),
                        "cola"   => MakeColaIconTex(),
                        "salad"  => MakeSaladIconTex(),
                        _ => null
                    };
                    if (placeholder == null)
                    {
                        Debug.LogWarning($"[Icons] '{entry.menuId}' 의 placeholder 생성기 없음 — 스킵");
                        missingPng++;
                        continue;
                    }
                    if (!System.IO.Directory.Exists("Assets/_Project/Art"))
                        System.IO.Directory.CreateDirectory("Assets/_Project/Art");
                    SaveIconPng(iconPath, placeholder);
                    AssetDatabase.ImportAsset(iconPath);
                    Debug.Log($"[Icons] {iconPath} placeholder 자동 생성");
                }

                // import 설정 (PPU 자동, sprite 모드, alpha 투명)
                ConfigureMenuIconImport(iconPath);

                // sprite asset 로드
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[Icons] Sprite import 실패: {iconPath}");
                    continue;
                }

                // MenuItemSO.iconSprite 자동 연결 (말풍선 등 다른 곳에서 같은 아이콘 사용)
                AssignMenuItemIcon(entry.menuId, sprite);

                // Station GameObject 찾기
                var station = GameObject.Find($"Station_{entry.menuId}");
                if (station == null)
                {
                    Debug.LogWarning($"[Icons] Station_{entry.menuId} 못 찾음 — 스킵");
                    continue;
                }

                // IconBody 자식 추가/갱신
                if (EnsureStationIcon(station, sprite)) updated++;
            }

            if (updated > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Icons] {updated}개 station 아이콘 적용 완료. Ctrl+S 저장.");
            }
            if (missingPng > 0)
            {
                Debug.LogWarning(
                    $"[Icons] {missingPng}개 PNG 누락. Assets/_Project/Art/ 폴더에 다음 파일 저장 필요:\n" +
                    "  - Menu_Hotdog.png\n  - Menu_Cola.png\n  - Menu_Salad.png");
            }
        }

        // MenuItemSO id 매칭으로 iconSprite 필드 채움 (말풍선 등에서 같은 sprite 사용 위해)
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

        // Station GameObject 에 IconBody 자식 (있으면 갱신, 없으면 생성). true 반환 = 변경 있음.
        static bool EnsureStationIcon(GameObject station, Sprite icon)
        {
            // 기존 SpriteRenderer 의 sortingOrder (= 색깔 사각형 bg) — icon은 그 위에 놓임
            var stationSr = station.GetComponent<SpriteRenderer>();
            int bgSortingOrder = stationSr != null ? stationSr.sortingOrder : -8;

            Transform iconT = station.transform.Find("IconBody");
            if (iconT == null)
            {
                // 신규 생성
                var iconGo = new GameObject("IconBody");
                iconGo.transform.SetParent(station.transform, false);
                iconGo.transform.localPosition = Vector3.zero;
                // 자식 scale 0.85 — 부모 scale 영향 받아 station 안에 살짝 padding 두고 들어감
                iconGo.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                var sr = iconGo.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                sr.color = Color.white;
                sr.sortingOrder = bgSortingOrder + 1; // bg 위
                Undo.RegisterCreatedObjectUndo(iconGo, "Add Station Icon");
                Debug.Log($"[Icons] {station.name} IconBody 신규 생성", station);
                return true;
            }
            else
            {
                // sprite 갱신만
                var sr = iconT.GetComponent<SpriteRenderer>();
                if (sr == null) return false;
                if (sr.sprite == icon) return false; // 이미 같음
                Undo.RecordObject(sr, "Update Station Icon");
                sr.sprite = icon;
                sr.sortingOrder = bgSortingOrder + 1;
                EditorUtility.SetDirty(sr);
                Debug.Log($"[Icons] {station.name} IconBody sprite 갱신", station);
                return true;
            }
        }

        // 메뉴 아이콘 PNG 의 import 설정 표준화 (sprite, PPU=texture max, bilinear, alpha 투명).
        static void ConfigureMenuIconImport(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            // PPU = texture 의 큰 변 (sprite 가 scale 1 일 때 1 world unit 안에 들어가게)
            float targetPpu = tex != null ? Mathf.Max(tex.width, tex.height) : 512f;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - targetPpu) > 0.5f) { importer.spritePixelsPerUnit = targetPpu; dirty = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        // 위치 + 스케일 동시 변경 (벽/floor 등 size 까지 조정 필요할 때)
        static int SetPosScale(string goName, Vector3 worldPos, Vector3 scale)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                Debug.LogWarning($"[Layout] '{goName}' 못 찾음 — 스킵");
                return 0;
            }
            Undo.RecordObject(go.transform, $"Layout {goName}");
            go.transform.position = worldPos;
            go.transform.localScale = scale;
            EditorUtility.SetDirty(go);
            return 1;
        }

        // ===== 레이아웃 기하 정합 (Geometric Fixup) =====
        //
        // 사용자가 일부 anchor 위치를 옮긴 후 (특히 Wall_Top y=2.6) 다른 요소들이
        // 옛 좌표에 머물러 시각적으로 어긋나는 부분을 일괄 정렬.
        //
        // 가정:
        //   - 천장 (Wall_TopLeft / TopRight) 은 y=2.6 (사용자가 위치시킨 값)
        //   - 바닥 (Wall_Bottom) 은 y=-3.6 (SceneBuilder 기본값)
        //   - 다이닝/주방 경계 (KitchenDivider) 은 y=-0.4 (기본값)
        //
        // 위 가정 기반으로 좌·우 벽 + DiningFloor + spawn/exit anchor 를 재정렬.
        // 사용자가 천장 y를 다르게 설정한 경우엔 메뉴 코드 상수 (TopWallY) 만 수정하면 됨.
        [MenuItem("Idle Restaurant/Legacy/Fix Layout Geometry")]
        public static void FixLayoutGeometry()
        {
            const float TopWallY    = 2.6f;   // 사용자가 옮긴 천장 y
            const float BottomWallY = -3.6f;  // 바닥 (기본)
            const float DividerY    = -0.4f;  // 다이닝/주방 경계 (기본)
            const float BuildingW   = 8f;     // 건물 가로 폭 (xPct 80%)

            int updated = 0;

            // 1) 좌·우 벽 — 천장과 바닥 사이를 꽉 채움
            float sideWallH = TopWallY - BottomWallY;          // 6.2
            float sideWallCY = (TopWallY + BottomWallY) * 0.5f; // -0.5
            updated += SetPosScale("Wall_Left",  new Vector3(-4f, sideWallCY, 0f), new Vector3(0.1f, sideWallH, 1f));
            updated += SetPosScale("Wall_Right", new Vector3( 4f, sideWallCY, 0f), new Vector3(0.1f, sideWallH, 1f));

            // 2) DiningFloor — 다이닝 zone (천장 ~ 경계). 새 천장 y 따라 확장.
            float diningH  = TopWallY - DividerY;              // 3.0
            float diningCY = (TopWallY + DividerY) * 0.5f;     // 1.1
            updated += SetPosScale("DiningFloor", new Vector3(0f, diningCY, 0f), new Vector3(BuildingW, diningH, 1f));

            // 3) KitchenFloor — 주방 zone (경계 ~ 바닥). 변경 없으면 좋겠지만 명시적 유지.
            float kitchenH  = DividerY - BottomWallY;          // 3.2
            float kitchenCY = (DividerY + BottomWallY) * 0.5f; // -2.0
            updated += SetPosScale("KitchenFloor", new Vector3(0f, kitchenCY, 0f), new Vector3(BuildingW, kitchenH, 1f));

            // 4) KitchenDivider — 다이닝/주방 경계선
            updated += SetPosScale("KitchenDivider", new Vector3(0f, DividerY, 0f), new Vector3(BuildingW, 0.04f, 1f));

            // 5) Wall_Bottom — 바닥 (위치만 명시)
            updated += SetPosScale("Wall_Bottom", new Vector3(0f, BottomWallY, 0f), new Vector3(BuildingW, 0.1f, 1f));

            // 6) OutsideSpawnAnchor — 화면 밖 좌측 (x=-7) 으로 더 이동, y=3.64 유지
            updated += SetPos("OutsideSpawnAnchor", new Vector3(-7f, 3.64f, 0f));

            // 7) TopExitAnchor — 화면 밖 위쪽 (y=10) 으로 더 이동, x 유지 (사용자 도어 배치 따라감)
            var topExit = GameObject.Find("TopExitAnchor");
            if (topExit != null)
            {
                Undo.RecordObject(topExit.transform, "Layout TopExitAnchor");
                var p = topExit.transform.position;
                topExit.transform.position = new Vector3(p.x, 10f, p.z);
                EditorUtility.SetDirty(topExit);
                updated++;
            }

            if (updated > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log(
                    $"[Layout] 기하 정합 완료. {updated}개 GameObject 업데이트.\n" +
                    $"  - 좌·우 벽: 천장(y={TopWallY}) ~ 바닥(y={BottomWallY}) 까지 연결\n" +
                    $"  - DiningFloor: 천장 ~ 경계(y={DividerY})\n" +
                    $"  - KitchenFloor: 경계 ~ 바닥\n" +
                    $"  - OutsideSpawn: 화면 밖 (x=-7)\n" +
                    $"  - TopExit: 화면 밖 (y=10)\n" +
                    "Ctrl+S 저장. 미세조정은 Scene view 에서 직접.");
            }
            else
            {
                Debug.LogWarning("[Layout] 대상 GameObject 못 찾음 — Build Static Scene 미실행 상태?");
            }
        }
    }
}
#endif
