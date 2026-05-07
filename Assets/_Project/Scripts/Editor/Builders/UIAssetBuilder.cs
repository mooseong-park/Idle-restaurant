#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools.Builders
{
    // HUD.uxml / HUD.uss 생성. 파일 존재 시 항상 보존 (사용자/Claude 가 직접 편집한 .uxml/.uss 보호).
    // 처음 셋업이거나 파일을 지운 경우에만 const seed 로 새로 작성.
    public static class UIAssetBuilder
    {
        public static void EnsureAll()
        {
            if (!Directory.Exists(BuildPaths.UIFolder)) Directory.CreateDirectory(BuildPaths.UIFolder);

            bool wroteAny = false;
            if (!File.Exists(BuildPaths.HudUxmlPath))
            {
                File.WriteAllText(BuildPaths.HudUxmlPath, HudUxml);
                AssetDatabase.ImportAsset(BuildPaths.HudUxmlPath);
                wroteAny = true;
            }
            if (!File.Exists(BuildPaths.HudUssPath))
            {
                File.WriteAllText(BuildPaths.HudUssPath, HudUss);
                AssetDatabase.ImportAsset(BuildPaths.HudUssPath);
                wroteAny = true;
            }
            if (!File.Exists(BuildPaths.TrackCardUxmlPath))
            {
                File.WriteAllText(BuildPaths.TrackCardUxmlPath, TrackCardUxml);
                AssetDatabase.ImportAsset(BuildPaths.TrackCardUxmlPath);
                wroteAny = true;
            }
            if (wroteAny) Debug.Log("[UIAssetBuilder] HUD UI 파일 신규 작성 (기존 없음).");
        }

        const string TrackCardUxml = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement class=""track-card"">
        <ui:VisualElement class=""track-card-header"">
            <ui:VisualElement name=""icon-box"" class=""track-card-icon-box"">
                <ui:Label name=""icon"" class=""track-card-icon"" text=""?"" />
            </ui:VisualElement>
            <ui:VisualElement class=""track-card-title-col"">
                <ui:Label name=""name"" class=""track-card-name"" text="""" />
                <ui:Label name=""lv-pill"" class=""track-card-lv-pill"" text=""Lv 1"" />
            </ui:VisualElement>
            <ui:Label class=""track-card-info"" text=""ⓘ"" />
        </ui:VisualElement>
        <ui:VisualElement class=""track-card-preview-row"">
            <ui:Label name=""preview-current"" class=""track-card-preview-current"" text="""" />
            <ui:Label class=""track-card-preview-arrow"" text=""→"" />
            <ui:Label name=""preview-next"" class=""track-card-preview-next"" text="""" />
        </ui:VisualElement>
        <ui:VisualElement name=""upgrade-btn"" class=""track-upgrade-btn"">
            <ui:Label class=""track-upgrade-text"" text=""Upgrade"" />
            <ui:Label name=""upgrade-cost"" class=""track-upgrade-cost"" text=""$0"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
";

        const string HudUxml = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement class=""ui-root"" picking-mode=""Ignore"">
        <ui:VisualElement class=""top-card"">
            <ui:VisualElement class=""row counters"">
                <ui:Label name=""cash"" text=""💰 $0"" class=""big cash"" />
                <ui:Label name=""gems"" text=""💎 0"" class=""big gem"" />
                <ui:Label name=""rating"" text=""⭐ 0"" class=""big rating"" />
            </ui:VisualElement>
            <ui:VisualElement class=""row day-row"">
                <ui:Label name=""day"" text=""Day 1"" class=""mid"" />
                <ui:Label name=""day-time"" text=""75s"" class=""mid"" />
            </ui:VisualElement>
            <ui:ProgressBar name=""time-bar"" low-value=""0"" high-value=""100"" value=""0"" class=""time-bar"" />
            <ui:Label name=""goal"" text=""Lv 1  $0 / $20"" class=""mid goal-label"" />
            <ui:ProgressBar name=""goal-bar"" low-value=""0"" high-value=""100"" value=""0"" class=""goal-bar"" />
        </ui:VisualElement>

        <ui:VisualElement name=""scene-area"" class=""scene-area"" picking-mode=""Ignore"" />

        <ui:VisualElement class=""main-content"">
            <ui:ScrollView name=""upgrades-scroll"" class=""tab-content"">
                <ui:VisualElement class=""upgrades-card"">
                    <ui:VisualElement name=""track-grid"" class=""track-grid"" />
                </ui:VisualElement>
            </ui:ScrollView>
            <ui:ScrollView name=""gacha-scroll"" class=""tab-content hidden"">
                <ui:VisualElement class=""gacha-card"">
                    <ui:Label text=""직원 가챠"" class=""tab-section-title"" />
                    <ui:VisualElement name=""gacha-grid"" class=""gacha-grid"" />
                </ui:VisualElement>
            </ui:ScrollView>
        </ui:VisualElement>

        <ui:VisualElement class=""bottom-tabs"">
            <ui:VisualElement name=""tab-upgrades"" class=""tab-btn active"">
                <ui:Label text=""⚒"" class=""tab-btn-icon"" />
                <ui:Label text=""Upgrades"" class=""tab-btn-label"" />
            </ui:VisualElement>
            <ui:VisualElement name=""tab-gacha"" class=""tab-btn"">
                <ui:Label text=""🎰"" class=""tab-btn-icon"" />
                <ui:Label text=""Gacha"" class=""tab-btn-label"" />
            </ui:VisualElement>
        </ui:VisualElement>

        <ui:VisualElement name=""day-end-overlay"" class=""overlay"">
            <ui:VisualElement class=""modal"">
                <ui:Label name=""day-end-title"" text=""Day 1"" class=""modal-title"" />
                <ui:Label name=""day-end-body"" text="""" class=""modal-body"" />
            </ui:VisualElement>
        </ui:VisualElement>

        <ui:VisualElement name=""gacha-overlay"" class=""overlay"">
            <ui:VisualElement class=""modal"">
                <ui:VisualElement name=""gacha-rarity-badge"" class=""rarity-badge"" />
                <ui:Label name=""gacha-title"" text="""" class=""modal-title"" />
                <ui:Label name=""gacha-body"" text="""" class=""modal-body"" />
                <ui:Button name=""gacha-close-btn"" text=""확인"" class=""modal-close-btn"" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
";

        const string HudUss = @"/* 1080x1920 portrait. 모바일 idle 스타일.
   세로: top-card | scene-area (fixed) | main-content (ScrollView) | bottom-tabs (fixed) */

.ui-root {
    flex-direction: column;
    flex-grow: 1;
}

.row { flex-direction: row; justify-content: space-between; }
.counters { justify-content: space-around; }

.top-card {
    padding: 14px 16px;
    margin: 14px 14px 0 14px;
}

.big {
    font-size: 34px;
    -unity-font-style: bold;
    background-color: rgb(255, 255, 255);
    border-radius: 28px;
    padding: 12px 22px;
    border-width: 1px;
    border-color: rgba(0, 0, 0, 0.06);
    margin: 0 4px;
    -unity-text-align: middle-center;
}
.big.cash { color: rgb(23, 165, 75); }
.big.gem { color: rgb(38, 99, 235); }
.big.rating { color: rgb(217, 119, 5); }

.mid { font-size: 26px; -unity-font-style: bold; color: rgb(31, 41, 55); }

.day-row { margin-top: 16px; }
.goal-label { margin-top: 14px; }

.time-bar { height: 12px; margin-top: 6px; }
.goal-bar { height: 18px; margin-top: 6px; }

.scene-area {
    height: 880px;
    margin: 12px 14px;
    border-radius: 22px;
    border-width: 1px;
    border-color: rgba(0, 0, 0, 0.10);
}

.main-content {
    flex-grow: 1;
    flex-direction: column;
    margin: 0 14px;
}
.tab-content {
    flex-grow: 1;
    background-color: rgba(255, 255, 255, 0.92);
    border-radius: 18px;
    border-width: 1px;
    border-color: rgba(0, 0, 0, 0.06);
}
.tab-content.hidden { display: none; }
.tab-section-title {
    font-size: 24px;
    -unity-font-style: bold;
    color: rgb(31, 41, 55);
    margin: 8px 4px 12px 4px;
    -unity-text-align: middle-center;
}

.upgrades-card { padding: 6px 8px 10px 8px; }
.track-grid { flex-direction: row; flex-wrap: wrap; }

.track-card {
    flex-basis: 48%;
    flex-grow: 0;
    flex-shrink: 0;
    background-color: rgb(255, 255, 255);
    border-radius: 22px;
    margin: 6px 1%;
    padding: 14px;
    border-width: 1px;
    border-color: rgba(0, 0, 0, 0.06);
    flex-direction: column;
    transition-property: scale;
    transition-duration: 0.1s;
}
.track-card:hover { scale: 1.01 1.01; }

.track-card-header {
    flex-direction: row;
    align-items: center;
    margin-bottom: 10px;
}
.track-card-icon-box {
    width: 56px; height: 56px; border-radius: 14px;
    background-color: rgb(243, 244, 246);
    align-items: center; justify-content: center;
    margin-right: 10px; flex-shrink: 0;
}
.track-card-icon { font-size: 30px; }

.track-card-title-col { flex-grow: 1; flex-shrink: 1; flex-direction: column; }
.track-card-name {
    font-size: 22px; -unity-font-style: bold; color: rgb(31, 41, 55); white-space: normal;
}
.track-card-lv-pill {
    font-size: 18px; color: rgb(107, 114, 128);
    background-color: rgb(243, 244, 246);
    border-radius: 12px; padding: 3px 12px; margin-top: 4px; align-self: flex-start;
}
.track-card-info {
    font-size: 22px; color: rgb(156, 163, 175);
    -unity-text-align: middle-center; width: 28px; flex-shrink: 0;
}

.track-card-preview-row {
    flex-direction: row; justify-content: center; align-items: center;
    background-color: rgb(249, 250, 251);
    border-radius: 12px; padding: 8px 6px; margin-bottom: 10px;
}
.track-card-preview-current { font-size: 20px; color: rgb(107, 114, 128); }
.track-card-preview-arrow { font-size: 20px; color: rgb(156, 163, 175); margin: 0 8px; }
.track-card-preview-next { font-size: 20px; -unity-font-style: bold; color: rgb(34, 197, 94); }

.track-upgrade-btn {
    flex-direction: row; align-items: center; justify-content: space-between;
    background-color: rgb(37, 99, 235);
    border-radius: 14px; padding: 12px 16px;
    transition-property: scale, background-color;
    transition-duration: 0.1s;
}
.track-upgrade-btn:hover { scale: 1.02 1.02; }
.track-upgrade-btn.disabled { background-color: rgb(209, 213, 219); }
.track-upgrade-btn.disabled:hover { scale: 1 1; }

.track-upgrade-text { font-size: 22px; -unity-font-style: bold; color: rgb(255, 255, 255); }
.track-upgrade-cost {
    font-size: 20px; -unity-font-style: bold; color: rgba(255, 255, 255, 0.95);
    background-color: rgba(0, 0, 0, 0.18); border-radius: 10px; padding: 4px 12px;
}
.track-card.maxed .track-upgrade-cost { background-color: transparent; }

.gacha-card { padding: 14px; }
.gacha-grid { flex-direction: row; }
.gacha-btn {
    flex-basis: 48%; flex-grow: 0; flex-shrink: 0;
    height: 88px; background-color: rgb(31, 41, 55); color: rgb(255, 255, 255);
    -unity-font-style: bold; font-size: 24px;
    border-width: 0; border-radius: 18px; margin: 0 1%;
    white-space: normal; transition-property: scale; transition-duration: 0.1s;
}
.gacha-btn:hover { scale: 1.03 1.03; }
.gacha-btn:disabled { background-color: rgb(209, 213, 219); color: rgb(107, 114, 128); scale: 1 1; }

.bottom-tabs {
    flex-direction: row; height: 110px;
    background-color: rgb(255, 255, 255);
    border-top-width: 1px; border-top-color: rgba(0, 0, 0, 0.10);
    margin-top: 8px;
}
.tab-btn {
    flex-grow: 1; flex-shrink: 1; flex-direction: column;
    align-items: center; justify-content: center;
    background-color: transparent;
    transition-property: background-color; transition-duration: 0.12s;
}
.tab-btn:hover { background-color: rgba(0, 0, 0, 0.03); }
.tab-btn-icon { font-size: 32px; color: rgb(156, 163, 175); }
.tab-btn-label { font-size: 18px; -unity-font-style: bold; color: rgb(156, 163, 175); margin-top: 2px; }
.tab-btn.active .tab-btn-icon { color: rgb(37, 99, 235); }
.tab-btn.active .tab-btn-label { color: rgb(37, 99, 235); }

.overlay {
    position: absolute; top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0, 0, 0, 0.55);
    align-items: center; justify-content: center;
    display: none;
}
.overlay.visible { display: flex; }

.modal {
    background-color: rgb(255, 255, 255);
    padding: 44px 56px; border-radius: 28px;
    align-items: center; min-width: 640px;
    border-width: 1px; border-color: rgba(0, 0, 0, 0.08);
    scale: 0.85 0.85; opacity: 0;
    transition-property: scale, opacity;
    transition-duration: 0.25s; transition-timing-function: ease-out;
}
.overlay.visible .modal { scale: 1 1; opacity: 1; }

.modal-title {
    font-size: 44px; -unity-font-style: bold; color: rgb(31, 41, 55);
    margin-bottom: 22px; -unity-text-align: middle-center;
}
.modal-body {
    font-size: 30px; white-space: normal;
    -unity-text-align: middle-center; color: rgb(31, 41, 55);
}
.modal-close-btn {
    width: 220px; height: 80px;
    background-color: rgb(31, 41, 55); color: rgb(255, 255, 255);
    -unity-font-style: bold; font-size: 26px;
    border-width: 0; border-radius: 16px; margin-top: 32px;
}

.rarity-badge {
    width: 140px; height: 140px; border-radius: 70px;
    margin-bottom: 22px;
    background-color: rgb(200, 200, 200);
    border-width: 4px; border-color: rgba(0, 0, 0, 0.12);
    display: none;
}
.rarity-badge.visible { display: flex; }
";
    }
}
#endif
