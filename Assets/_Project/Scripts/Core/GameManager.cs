using UnityEngine;
using Project.Core.Events;
using Project.Data;
using Project.Systems;
using Project.UI;
using Project.Utils;
using Project.Visual;

namespace Project.Core
{
    // 게임의 진입점. Main 씬의 GameObject에 붙여서 사용.
    // GameConfig.asset 하나만 Inspector로 연결하면 모든 데이터에 접근 가능.
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("GameConfig.asset을 드래그하세요. 모든 설정과 데이터가 여기 연결됨.")]
        [SerializeField] GameConfigSO config;

        [Tooltip("GameEvents.asset을 드래그하세요. UI/시스템 간 변경 알림 채널 (없어도 동작은 함).")]
        [SerializeField] GameEventsSO gameEvents;

        [Header("Game Speed")]
        [Tooltip("게임 진행 속도 배율. 0 = 일시정지. 1 = 보통. 2~5 = 가속 (디버그). Inspector/코드에서 조정.")]
        [Range(0f, 5f)]
        [SerializeField] float gameSpeed = 1f;

        GameState state;
        CustomerSystem customerSystem;
        StaffSystem staffSystem;
        DaySystem daySystem;
        GachaSystem gachaSystem;
        TrackSystem trackSystem;

        public GameConfigSO Config => config;
        public GameState State => state;
        public CustomerSystem CustomerSystem => customerSystem;
        public StaffSystem StaffSystem => staffSystem;
        public DaySystem DaySystem => daySystem;
        public GachaSystem GachaSystem => gachaSystem;
        public TrackSystem TrackSystem => trackSystem;

        // 게임 속도 제어 API. 0 = 일시정지. Time.timeScale 안 건드림 — Unity Animation/Physics에 영향 X.
        public float GameSpeed
        {
            get => gameSpeed;
            set => gameSpeed = Mathf.Clamp(value, 0f, 5f);
        }
        public bool IsPaused => gameSpeed <= 0f;
        public void Pause() => gameSpeed = 0f;
        public void Resume(float speed = 1f) => gameSpeed = Mathf.Clamp(speed, 0f, 5f);

        const float AutoSaveIntervalSec = 30f;
        float autoSaveTimer;

        void Awake()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[GameManager] GameConfigSO가 연결되지 않았습니다. " +
                    "Inspector의 Config 슬롯에 GameConfig.asset을 드래그하세요.",
                    this);
                enabled = false;
                return;
            }

            state = new GameState();
            // Events 채널 바인딩 — null 이어도 안전 (raise 호출 시 자동 스킵).
            // 잔여 listener 제거 (Domain reload / Play stop 후 재진입 시 이중 구독 방지).
            if (gameEvents != null)
            {
                gameEvents.ClearAllSubscribers();
                state.BindEvents(gameEvents);
            }

            // RNG provider — PlayerPrefs 에 "rng_seed" 가 있으면 시드 모드 (재현 디버그). 없으면 Unity Random.
            IRandomProvider random;
            if (PlayerPrefs.HasKey("rng_seed"))
            {
                int seed = PlayerPrefs.GetInt("rng_seed");
                random = new SeededRandomProvider(seed);
                Debug.Log($"[GameManager] Seeded RNG (seed={seed}). 분포 검증/재현 모드.");
            }
            else random = new UnityRandomProvider();

            customerSystem = new CustomerSystem(state, config, random);
            staffSystem = new StaffSystem(state, config, customerSystem);
            daySystem = new DaySystem(state, config, customerSystem, staffSystem);
            gachaSystem = new GachaSystem(state, config, staffSystem, random);
            trackSystem = new TrackSystem(state, config);

            // 세이브 로드 시도. 성공 시 직원 명단까지 복원되므로 InitialStaff spawn 스킵.
            // 실패/없음 시 fresh state + InitialStaff spawn (기존 동작).
            bool loaded = SaveManager.TryLoad(state, config, staffSystem);
            if (!loaded) SpawnInitialStaff();
            else Debug.Log($"[GameManager] 세이브 로드 완료 — Day {state.Day} | 💰 ${state.Cash:F0} | 💎 {state.Gems} | ⭐ {state.Rating} | 직원 {staffSystem.Staffs.Count}명");

            WarnIfSceneIncomplete();
            LogStartupSnapshot();
        }

        // 씬에 SceneController/UIController가 없으면 EditorScript 실행을 안내.
        // (정석 패턴: 두 컴포넌트는 Hierarchy에 미리 배치)
        void WarnIfSceneIncomplete()
        {
            bool hasScene = Object.FindFirstObjectByType<SceneController>() != null;
            bool hasUI    = Object.FindFirstObjectByType<UIController>() != null;
            if (hasScene && hasUI) return;
            Debug.LogWarning(
                "[GameManager] 씬 셋업이 불완전합니다. Unity 메뉴 'Idle Restaurant > Build Static Scene' 실행 후 Ctrl+S로 저장하세요.\n" +
                $"  - SceneController: {(hasScene ? "OK" : "없음")}\n" +
                $"  - UIController:    {(hasUI ? "OK" : "없음")}",
                this);
        }

        void Update()
        {
            // gameSpeed 0이면 일시정지 (Tick 호출 X). 1 이상이면 dt 곱해서 가속.
            // Time.timeScale을 건드리지 않는 이유: Animator/Physics 영향 + UI fade transition 멈춤 회피.
            float dt = Time.deltaTime * gameSpeed;

            // Phase가 Playing일 때만 영업 로직. Transitioning에는 모두 정지.
            if (daySystem.Phase == DayPhase.Playing && dt > 0f)
            {
                float perSec = Formulas.PassiveTotalPerSec(state, config);
                if (perSec > 0f)
                {
                    float delta = perSec * dt;
                    state.Cash += delta;
                    state.TotalRevenue += delta;
                    state.PassiveRevenue += delta;
                }

                customerSystem.Tick(dt);
                staffSystem.Tick(dt);
            }

            // DaySystem은 일시정지 영향 받음 (Day 길이도 멈춤). transition도 같이.
            if (dt > 0f) daySystem.Tick(dt);

            // 디버그 핫키 (Day 8 UI 도입 시 버튼으로 대체).
            // B = Basic Pack, P = Pro Pack. 인덱스로 StaffPacks를 찾기 때문에 GameConfig.asset의 순서 = [Basic, Pro].
            if (Input.GetKeyDown(KeyCode.B)) TryBuyPack(0);
            if (Input.GetKeyDown(KeyCode.P)) TryBuyPack(1);

            // 속도 디버그 핫키 (Space=일시정지, 1~5=배속). 모바일 빌드에선 UI 버튼으로 대체 예정.
            if (Input.GetKeyDown(KeyCode.Space)) gameSpeed = gameSpeed > 0f ? 0f : 1f;
            if (Input.GetKeyDown(KeyCode.Alpha1)) gameSpeed = 1f;
            if (Input.GetKeyDown(KeyCode.Alpha2)) gameSpeed = 2f;
            if (Input.GetKeyDown(KeyCode.Alpha3)) gameSpeed = 3f;
            if (Input.GetKeyDown(KeyCode.Alpha5)) gameSpeed = 5f;

            // 30초 주기 자동 세이브. 큰 변경(가챠/트랙) 직후엔 OnApplicationPause/Quit 가 backup.
            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= AutoSaveIntervalSec)
            {
                autoSaveTimer = 0f;
                SaveManager.Save(state, staffSystem);
            }
        }

        void OnApplicationPause(bool paused) { if (paused) SaveSafely(); }
        void OnApplicationFocus(bool focused) { if (!focused) SaveSafely(); }
        void OnApplicationQuit() { SaveSafely(); }
        void OnDisable() { SaveSafely(); }

        void SaveSafely()
        {
            if (state == null || staffSystem == null) return;
            SaveManager.Save(state, staffSystem);
        }

        void TryBuyPack(int packIndex)
        {
            if (packIndex < 0 || packIndex >= config.StaffPacks.Count) return;
            var pack = config.StaffPacks[packIndex];
            var result = gachaSystem.Buy(pack, out var drawn, out var name);
            switch (result)
            {
                case GachaSystem.BuyResult.Success:
                    Debug.Log($"[Gacha] {pack.DisplayName} 구매 성공 → {drawn.DisplayName} {name} (잔여 💎 {state.Gems})");
                    break;
                case GachaSystem.BuyResult.NotEnoughGems:
                    Debug.Log($"[Gacha] {pack.DisplayName} 💎 부족 (보유 {state.Gems} < 비용 {pack.GemCost})");
                    break;
                case GachaSystem.BuyResult.MaxStaffReached:
                    Debug.Log($"[Gacha] {pack.DisplayName} 직원 최대치({config.MaxStaff}명) 도달");
                    break;
                case GachaSystem.BuyResult.InvalidPack:
                    Debug.LogWarning($"[Gacha] 잘못된 팩 (index={packIndex})");
                    break;
            }
        }

        void SpawnInitialStaff()
        {
            var rarities = config.InitialStaffRarities;
            for (int i = 0; i < rarities.Count; i++)
            {
                if (rarities[i] == null) continue;
                staffSystem.SpawnStaff(rarities[i], RandomName.Make(config));
            }
        }

        // 부팅 시 데이터 흐름 검증용 스모크 테스트.
        void LogStartupSnapshot()
        {
            var rep = Formulas.ComputeRep(state.Rating, config.RepLevelTable);

            Debug.Log(
                "=== Idle Restaurant 부팅 ===\n" +
                $"Day {state.Day} | 💰 ${state.Cash:F0} | 💎 {state.Gems} | ⭐ {state.Rating}\n" +
                $"명성 Lv {rep.Lv} (목표 ${rep.Goal})\n" +
                $"하루 길이: {config.DayLengthMs / 1000f:F0}초 | 손님 도착 간격: {config.ArrivalIntervalMs / 1000f:F0}초\n" +
                $"좌석: {Formulas.SeatingCap(state.VenueLv, config)}석 | 줄: {Formulas.QueueCap(state.VenueLv, config)}자리\n" +
                $"메뉴 가격 배율: ×{Formulas.PriceMult(state.MenuLv, config):F2}\n" +
                $"직원 속도 배율: ×{1f / Formulas.StaffSpdMult(state.StaffLv, config):F2}\n" +
                $"Passive 자동 수입: ${Formulas.PassiveTotalPerSec(state, config):F2}/s\n" +
                $"초기 직원: {staffSystem.Staffs.Count}명 | 데이터 로드: 트랙 {config.Tracks.Count} | 메뉴 {config.MenuItems.Count} | 등급 {config.Rarities.Count} | 팩 {config.StaffPacks.Count}\n" +
                "ⓘ 디버그 핫키: B = Basic Pack 가챠, P = Pro Pack 가챠 (Day 8 UI 도입 시 버튼으로 대체)",
                this);
        }
    }
}
