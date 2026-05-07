#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Project.Visual;

namespace Project.EditorTools.Builders
{
    // CustomerPrefab.prefab / StaffPrefab.prefab 생성.
    // - createIfMissing=true: 파일 없을 때만 생성 (Sync Missing Only). 사용자가 Inspector 에서 튜닝한 ref 슬롯/구조 보존.
    // - createIfMissing=false: 항상 재생성 (Full Rebuild / Initial Setup). 기존 prefab 덮어씀.
    public static class PrefabBuilder
    {
        public static void EnsureAll(Sprite circle, Sprite square, Sprite chef, Sprite roundedSquare, bool createIfMissing)
        {
            if (!Directory.Exists(BuildPaths.PrefabFolder)) Directory.CreateDirectory(BuildPaths.PrefabFolder);

            if (!createIfMissing || !File.Exists(BuildPaths.CustomerPrefabPath))
                EnsureCustomerPrefab(circle, square, roundedSquare);

            if (!createIfMissing || !File.Exists(BuildPaths.StaffPrefabPath))
                EnsureStaffPrefab(chef, roundedSquare, circle);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // 외부에서 호출 가능 (Legacy "Rebuild Customer Prefab Only" 메뉴 등).
        public static void RebuildCustomerOnly(Sprite circle, Sprite square, Sprite roundedSquare)
        {
            if (!Directory.Exists(BuildPaths.PrefabFolder)) Directory.CreateDirectory(BuildPaths.PrefabFolder);
            EnsureCustomerPrefab(circle, square, roundedSquare);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // 정석 구조:
        //   CustomerPrefab (root, scale 0.85)
        //   ├── BgFrame       — 둥근 사각형, mood 색 (sortingOrder 4)
        //   ├── Body          — Customer PNG (런타임 교체), sortingOrder 5
        //   ├── PatienceBg    — 비활성, 자식 구조 호환 유지
        //   ├── PiePatience   — 인내심 파이, sortingOrder 9
        //   └── OrderBubble   — 주문 말풍선
        //       ├── Tail / BG / Icon_0/1/2 (sortingOrder 9~10)
        static void EnsureCustomerPrefab(Sprite circle, Sprite square, Sprite roundedSquare)
        {
            var go = new GameObject("CustomerPrefab");
            go.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            var bg = new GameObject("BgFrame");
            bg.transform.SetParent(go.transform, false);
            bg.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = roundedSquare != null ? roundedSquare : circle;
            bgSr.color = new Color(0.23f, 0.51f, 0.96f);
            bgSr.sortingOrder = SortingOrders.CustomerBgFrame;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one;
            var bodyRen = body.AddComponent<SpriteRenderer>();
            bodyRen.sprite = circle;
            bodyRen.color = Color.white;
            bodyRen.sortingOrder = SortingOrders.CustomerBody;

            var pb = new GameObject("PatienceBg");
            pb.transform.SetParent(go.transform, false);
            pb.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            pb.transform.localScale = new Vector3(1.2f, 0.2f, 1f);
            var pbSr = pb.AddComponent<SpriteRenderer>();
            pbSr.sprite = square;
            pbSr.color = new Color(0.85f, 0.85f, 0.85f, 0.95f);
            pbSr.sortingOrder = SortingOrders.CharacterUI;

            var pf = new GameObject("PatienceFg");
            pf.transform.SetParent(pb.transform, false);
            pf.transform.localScale = new Vector3(1f, 0.7f, 1f);
            var pfSr = pf.AddComponent<SpriteRenderer>();
            pfSr.sprite = square;
            pfSr.color = new Color(0.13f, 0.77f, 0.37f);
            pfSr.sortingOrder = SortingOrders.OrderBubbleIcon;

            pb.SetActive(false);

            var pie = new GameObject("PiePatience");
            pie.transform.SetParent(go.transform, false);
            pie.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            pie.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            var pieSrPrefab = pie.AddComponent<SpriteRenderer>();
            pieSrPrefab.sortingOrder = SortingOrders.CharacterUI;
            pie.SetActive(false);

            var bubble = new GameObject("OrderBubble");
            bubble.transform.SetParent(go.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 2f, 0f);
            bubble.transform.localScale = Vector3.one;

            var tail = new GameObject("Tail");
            tail.transform.SetParent(bubble.transform, false);
            tail.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            var bubbleTailPrefab = tail.AddComponent<SpriteRenderer>();
            bubbleTailPrefab.sprite = square;
            bubbleTailPrefab.color = new Color(1f, 1f, 1f, 0.97f);
            bubbleTailPrefab.sortingOrder = SortingOrders.CharacterUI;

            var bubbleBg = new GameObject("BG");
            bubbleBg.transform.SetParent(bubble.transform, false);
            bubbleBg.transform.localPosition = Vector3.zero;
            bubbleBg.transform.localScale = new Vector3(1.9f, 1.6f, 1f);
            var bubbleBgPrefab = bubbleBg.AddComponent<SpriteRenderer>();
            bubbleBgPrefab.sprite = roundedSquare != null ? roundedSquare : square;
            bubbleBgPrefab.color = new Color(1f, 1f, 1f, 0.97f);
            bubbleBgPrefab.sortingOrder = SortingOrders.CharacterUI;

            var iconSrs = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var iconGo = new GameObject($"Icon_{i}");
                iconGo.transform.SetParent(bubble.transform, false);
                iconGo.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
                var iconSr = iconGo.AddComponent<SpriteRenderer>();
                iconSr.sortingOrder = SortingOrders.OrderBubbleIcon;
                iconGo.SetActive(false);
                iconSrs[i] = iconSr;
            }
            bubble.SetActive(false);

            // CustomerView — body/mood 색 + route + arrival flags. patienceRoot 슬롯은 호환 유지(비활성).
            var visualSettings = AssetDatabase.LoadAssetAtPath<Project.Data.VisualSettingsSO>(BuildPaths.VisualSettingsPath);
            var cv = go.AddComponent<CustomerView>();
            var so = new SerializedObject(cv);
            BuilderHelpers.AssignRef(so, "sr", bgSr);
            BuilderHelpers.AssignRef(so, "bodySr", bodyRen);
            BuilderHelpers.AssignRef(so, "patienceRoot", pb);
            BuilderHelpers.AssignRef(so, "visualSettings", visualSettings);
            so.ApplyModifiedPropertiesWithoutUndo();

            // CustomerUIOverlay — pie patience + order bubble (책임 분리, A1 단계).
            var overlay = go.AddComponent<CustomerUIOverlay>();
            var soOv = new SerializedObject(overlay);
            BuilderHelpers.AssignRef(soOv, "pieSr", pieSrPrefab);
            BuilderHelpers.AssignRef(soOv, "bubbleGo", bubble);
            BuilderHelpers.AssignRef(soOv, "bubbleBgSr", bubbleBgPrefab);
            BuilderHelpers.AssignRef(soOv, "bubbleTailSr", bubbleTailPrefab);
            BuilderHelpers.AssignSpriteRendererArray(soOv, "bubbleIcons", iconSrs);
            soOv.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, BuildPaths.CustomerPrefabPath);
            Object.DestroyImmediate(go);
        }

        // 새 구조:
        //   StaffPrefab (root, scale 0.85)
        //   ├── BgFrame      — 둥근 사각형, 연한 파랑, sortingOrder 6
        //   └── Body         — 셰프 sprite, sortingOrder 7
        static void EnsureStaffPrefab(Sprite chef, Sprite roundedSquare, Sprite circleFallback)
        {
            var go = new GameObject("StaffPrefab");
            go.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            var bg = new GameObject("BgFrame");
            bg.transform.SetParent(go.transform, false);
            bg.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = roundedSquare != null ? roundedSquare : circleFallback;
            bgSr.color = new Color(0.55f, 0.78f, 0.93f, 1f);
            bgSr.sortingOrder = SortingOrders.StaffBgFrame;

            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one;
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = chef != null ? chef : circleFallback;
            sr.sortingOrder = SortingOrders.StaffBody;
            sr.color = Color.white;
            if (chef == null)
                Debug.LogWarning("[PrefabBuilder] Staff_Chef.png 로드 실패 — circle fallback 사용.");

            var sv = go.AddComponent<StaffView>();
            var soSv = new SerializedObject(sv);
            BuilderHelpers.AssignRef(soSv, "sr", sr);
            var visualSettings = AssetDatabase.LoadAssetAtPath<Project.Data.VisualSettingsSO>(BuildPaths.VisualSettingsPath);
            BuilderHelpers.AssignRef(soSv, "visualSettings", visualSettings);
            soSv.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, BuildPaths.StaffPrefabPath);
            Object.DestroyImmediate(go);
        }
    }

    // SerializedObject 보조 (4개 Builder 공용).
    public static class BuilderHelpers
    {
        public static void AssignRef(SerializedObject so, string propName, Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = value;
        }

        // null 일 때만 채움 — 사용자가 manual 로 다른 값 할당했으면 보존.
        public static void AssignRefIfEmpty(SerializedObject so, string propName, Object value)
        {
            if (value == null) return;
            var p = so.FindProperty(propName);
            if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = value;
        }

        public static void AssignIntField(MonoBehaviour component, string fieldName, int value)
        {
            var so = new SerializedObject(component);
            var p = so.FindProperty(fieldName);
            if (p != null) p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        public static void AssignObjectField(MonoBehaviour component, string fieldName, Object value)
        {
            var so = new SerializedObject(component);
            var p = so.FindProperty(fieldName);
            if (p != null) p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        public static void AssignSpriteRendererArray(SerializedObject so, string propName, SpriteRenderer[] values)
        {
            var p = so.FindProperty(propName);
            if (p == null || !p.isArray) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
