using System.Collections.Generic;
using UnityEngine;

namespace Project.Data
{
    [CreateAssetMenu(menuName = "Idle Restaurant/Game Config", fileName = "GameConfig")]
    public sealed class GameConfigSO : ScriptableObject
    {
        // ============================================================
        // 데이터 참조 — 다른 SO들의 통합 진입점
        // ============================================================
        [Header("데이터 참조")]
        [Tooltip("8개 트랙 (Active 4 + Passive 4)을 모두 드래그")]
        [SerializeField] List<TrackConfigSO> tracks = new();

        [Tooltip("메뉴 아이템 (Hotdog/Cola/Salad)")]
        [SerializeField] List<MenuItemSO> menuItems = new();

        [Tooltip("직원 등급 (Novice/Pro/Expert)")]
        [SerializeField] List<RarityConfigSO> rarities = new();

        [Tooltip("가챠 팩 (Basic/Pro)")]
        [SerializeField] List<StaffPackSO> staffPacks = new();

        [Tooltip("명성 Lv 표")]
        [SerializeField] RepLevelTableSO repLevelTable;

        [Tooltip("게임 시작 시 자동 spawn할 초기 직원 등급. 배열 길이 = 직원 수, 각 슬롯 = 그 직원의 등급 SO.\n예: [Novice] → 1명, [Novice, Novice] → 2명, [Novice, Pro] → Novice 1 + Pro 1.")]
        [SerializeField] RarityConfigSO[] initialStaffRarities;

        [Tooltip("손님 Body sprite 풀. Spawn 시 랜덤 1개 선택해 손님 외형으로 사용. 비어있으면 폴백 도형(원).")]
        [SerializeField] Sprite[] customerBodySprites;

        // ============================================================
        // 하루 주기
        // ============================================================
        [Header("하루 주기")]
        [Tooltip("하루 길이 (밀리초). 75000 = 75초.")]
        [SerializeField, Range(10000, 600000)] int dayLengthMs = 75000;

        [Tooltip("Day End 모달 표시 시간 (밀리초).")]
        [SerializeField, Range(500, 10000)] int dayTransitionMs = 3000;

        [Tooltip("손님 도착 간격 (밀리초). 5000 = 5초마다 1명.")]
        [SerializeField, Range(500, 30000)] int arrivalIntervalMs = 5000;

        [Tooltip("게임 루프 주기 (밀리초). 100 = 초당 10번 갱신.")]
        [SerializeField, Range(20, 500)] int tickMs = 100;

        // ============================================================
        // 손님 단계별 타이밍 (모두 ms)
        // ============================================================
        [Header("손님 단계 타이밍 (ms)")]
        [Tooltip("문 앞 도착 → 입장 판단까지")]
        [SerializeField, Range(0, 10000)] int tArriving = 2000;

        [Tooltip("좌석 이동 → 자리 잡기")]
        [SerializeField, Range(0, 10000)] int tSeatingSettle = 1900;

        [Tooltip("직원이 주문 받는 시간")]
        [SerializeField, Range(0, 5000)] int tTakingOrder = 600;

        [Tooltip("직원이 음식 내려놓는 시간")]
        [SerializeField, Range(0, 5000)] int tServeAction = 400;

        [Tooltip("식사 후 표정 표시 시간")]
        [SerializeField, Range(0, 10000)] int tResult = 1800;

        [Tooltip("자리에서 일어나 퇴장까지")]
        [SerializeField, Range(0, 10000)] int tLeaving = 2500;

        [Tooltip("음식 받고 식사하는 시간 (서비스 트랙으로 단축됨)")]
        [SerializeField, Range(1000, 30000)] int tEating = 5500;

        [Tooltip("줄에서 기다릴 수 있는 최대 시간 (인내심 트랙으로 늘어남)")]
        [SerializeField, Range(3000, 60000)] int tPatienceQueue = 20000;

        [Tooltip("자리에서 주문/서빙 기다릴 수 있는 최대 시간 (인내심 트랙으로 늘어남)")]
        [SerializeField, Range(5000, 120000)] int tPatienceSeated = 35000;

        // ============================================================
        // 매장 용량 (매장 트랙 Lv에 따라 동적 변화)
        // ============================================================
        [Header("매장 용량")]
        [Tooltip("물리적 테이블 개수")]
        [SerializeField, Range(1, 6)] int tableCount = 3;

        [Tooltip("테이블당 좌석 수")]
        [SerializeField, Range(1, 4)] int seatsPerTable = 2;

        [Tooltip("매장 Lv 1일 때 사용 가능한 좌석 수")]
        [SerializeField, Range(1, 6)] int seatingBase = 2;

        [Tooltip("매장 Lv당 추가되는 좌석 수")]
        [SerializeField, Range(0, 4)] int seatingPerLv = 1;

        [Tooltip("좌석 최대치 (물리 한계)")]
        [SerializeField, Range(2, 12)] int seatingMax = 6;

        [Tooltip("매장 Lv 1일 때 줄 자리 수")]
        [SerializeField, Range(0, 8)] int queueBase = 2;

        [Tooltip("매장 Lv당 추가되는 줄 자리 수")]
        [SerializeField, Range(0, 4)] int queuePerLv = 1;

        [Tooltip("줄 자리 최대치")]
        [SerializeField, Range(1, 16)] int queueMax = 8;

        [Tooltip("최대 직원 수")]
        [SerializeField, Range(1, 12)] int maxStaff = 6;

        // ============================================================
        // Active Track 효과 계수
        // ============================================================
        [Header("Active Track 효과 계수")]
        [Tooltip("메뉴: Lv당 가격 +N%. 0.05 = +5%/Lv. 공식: priceMult = 1 + (Lv-1) × 이값")]
        [SerializeField, Range(0f, 0.5f)] float priceMultPerLv = 0.05f;

        [Tooltip("직원: Lv당 이동·조리 속도 +N%. 0.12 = +12%/Lv. 공식: spdMult = 1 / (1 + (Lv-1) × 이값)")]
        [SerializeField, Range(0f, 0.5f)] float staffSpdGrowth = 0.12f;

        [Tooltip("서비스: Lv당 식사 시간 -N%. 0.08 = -8%/Lv (점근). 공식: eatMult = 1 / (1 + (Lv-1) × 이값)")]
        [SerializeField, Range(0f, 0.5f)] float eatingSpdGrowth = 0.08f;

        [Tooltip("서비스: Lv당 인내심 +N%. 0.05 = +5%/Lv. 공식: patienceMult = 1 + (Lv-1) × 이값")]
        [SerializeField, Range(0f, 0.5f)] float patiencePerLv = 0.05f;

        // ============================================================
        // Passive Track 효과 계수 (Lv당 초당 $)
        // ============================================================
        [Header("Passive Track 효과 계수 (Lv당 $/s)")]
        [Tooltip("배달: Lv당 초당 +$N")]
        [SerializeField, Range(0f, 2f)] float takeoutPerLv = 0.10f;

        [Tooltip("광고판: Lv당 초당 +$N")]
        [SerializeField, Range(0f, 2f)] float adsPerLv = 0.10f;

        [Tooltip("팁 수입: Lv당 초당 +$N")]
        [SerializeField, Range(0f, 2f)] float tipPerLv = 0.10f;

        [Tooltip("굿즈: Lv당 초당 +$N")]
        [SerializeField, Range(0f, 2f)] float merchPerLv = 0.10f;

        // ============================================================
        // 일일 보상
        // ============================================================
        [Header("일일 보상")]
        [Tooltip("Day 종료 시 무조건 지급되는 💎")]
        [SerializeField, Range(0, 10)] int gemBaseReward = 1;

        [Tooltip("Day 목표 달성 시 추가 💎")]
        [SerializeField, Range(0, 20)] int gemGoalBonus = 2;

        [Tooltip("팁 발생 시 1회 금액 (현재 미사용, 향후 확장용)")]
        [SerializeField, Range(1, 100)] int tipAmount = 1;

        // ============================================================
        // 기록 / 디버그
        // ============================================================
        [Header("기록 / 디버그")]
        [Tooltip("스냅샷 저장 주기 (밀리초). 분석용 로그.")]
        [SerializeField, Range(1000, 60000)] int snapshotMs = 10000;

        // ============================================================
        // 손님/직원 이름 풀
        // ============================================================
        [Header("이름 풀 (랜덤 생성)")]
        [Tooltip("이름 (First name) 후보. Inspector에서 +/- 로 추가/삭제.")]
        [SerializeField] string[] firstNames = {
            "Mario","Emma","Kenji","Sofia","Luca","Yuki","Diego","Chen",
            "Anja","Rafael","Mei","Oscar","Priya","Finn","Noa"
        };

        [Tooltip("성 (Last name) 후보.")]
        [SerializeField] string[] lastNames = {
            "Rossi","Schmidt","Tanaka","Garcia","Kim","Martin","Chen","Silva",
            "Larsen","Khan","Park","Weber","Patel","Olsen","Ito"
        };

        // ============================================================
        // Properties (외부 읽기 전용 노출)
        // ============================================================
        public IReadOnlyList<TrackConfigSO> Tracks => tracks;
        public IReadOnlyList<MenuItemSO> MenuItems => menuItems;
        public IReadOnlyList<RarityConfigSO> Rarities => rarities;
        public IReadOnlyList<StaffPackSO> StaffPacks => staffPacks;
        public RepLevelTableSO RepLevelTable => repLevelTable;
        public IReadOnlyList<RarityConfigSO> InitialStaffRarities => initialStaffRarities ?? System.Array.Empty<RarityConfigSO>();
        public IReadOnlyList<Sprite> CustomerBodySprites => customerBodySprites ?? System.Array.Empty<Sprite>();

        public int DayLengthMs => dayLengthMs;
        public int DayTransitionMs => dayTransitionMs;
        public int ArrivalIntervalMs => arrivalIntervalMs;
        public int TickMs => tickMs;

        public int TArriving => tArriving;
        public int TSeatingSettle => tSeatingSettle;
        public int TTakingOrder => tTakingOrder;
        public int TServeAction => tServeAction;
        public int TResult => tResult;
        public int TLeaving => tLeaving;
        public int TEating => tEating;
        public int TPatienceQueue => tPatienceQueue;
        public int TPatienceSeated => tPatienceSeated;

        public int TableCount => tableCount;
        public int SeatsPerTable => seatsPerTable;
        public int SeatingBase => seatingBase;
        public int SeatingPerLv => seatingPerLv;
        public int SeatingMax => seatingMax;
        public int QueueBase => queueBase;
        public int QueuePerLv => queuePerLv;
        public int QueueMax => queueMax;
        public int MaxStaff => maxStaff;

        public float PriceMultPerLv => priceMultPerLv;
        public float StaffSpdGrowth => staffSpdGrowth;
        public float EatingSpdGrowth => eatingSpdGrowth;
        public float PatiencePerLv => patiencePerLv;

        public float TakeoutPerLv => takeoutPerLv;
        public float AdsPerLv => adsPerLv;
        public float TipPerLv => tipPerLv;
        public float MerchPerLv => merchPerLv;

        public int GemBaseReward => gemBaseReward;
        public int GemGoalBonus => gemGoalBonus;
        public int TipAmount => tipAmount;

        public int SnapshotMs => snapshotMs;

        public IReadOnlyList<string> FirstNames => firstNames;
        public IReadOnlyList<string> LastNames => lastNames;
    }
}
