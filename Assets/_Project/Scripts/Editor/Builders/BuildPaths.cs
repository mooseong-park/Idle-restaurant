#if UNITY_EDITOR
namespace Project.EditorTools.Builders
{
    // 4개 Builder가 공유하는 asset 경로 상수.
    public static class BuildPaths
    {
        public const string ArtFolder    = "Assets/_Project/Art";
        public const string PrefabFolder = "Assets/_Project/Prefabs";
        public const string UIFolder     = "Assets/_Project/UI";
        public const string SettingsFolder = "Assets/_Project/Settings";

        public const string SquarePath        = "Assets/_Project/Art/SF_Square.png";
        public const string CirclePath        = "Assets/_Project/Art/SF_Circle.png";
        public const string RoundedSquarePath = "Assets/_Project/Art/SF_RoundedSquare.png";
        public const string StaffChefPath     = "Assets/_Project/Art/Staff_Chef.png";

        public const string CustomerPrefabPath = "Assets/_Project/Prefabs/CustomerPrefab.prefab";
        public const string StaffPrefabPath    = "Assets/_Project/Prefabs/StaffPrefab.prefab";

        public const string HudUxmlPath = "Assets/_Project/UI/HUD.uxml";
        public const string HudUssPath  = "Assets/_Project/UI/HUD.uss";
        public const string TrackCardUxmlPath = "Assets/_Project/UI/TrackCard.uxml";

        public const string PanelSettingsPath = "Assets/_Project/New Panel Settings.asset";
        public const string GameEventsPath    = "Assets/_Project/Settings/GameEvents.asset";
        public const string VisualSettingsPath = "Assets/_Project/Settings/VisualSettings.asset";
    }
}
#endif
