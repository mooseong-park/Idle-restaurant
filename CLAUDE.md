# Idle Restaurant Tycoon — Unity 마이그레이션

HTML 프로토타입 v0.15.1 → Unity 6 LTS Android. 첫 마일스톤 **M1: HTML 1:1 재현**. 현재 M1 종료 단계 (Day 14 구조 정리 완료).

## 기술 스택

- Unity 6 LTS (URP 2D)
- Android 타겟 / 1080×1920 portrait 고정 가정
- UI Toolkit (uGUI 아님) — UXML/USS 분리
- TextMeshPro (World Space 텍스트 — RevenuePopup 전용)
- C# (NRT 미사용 — 프로토 단계 학습 부담 ↓)
- 단위 테스트 없음 / 도형·색상만 (실제 아트는 M2)

## 프로젝트 철학

- **Less is More** — 미래 가정 기능 금지. 필요할 때 추가
- **구조 탄탄 / 수치 임시** — 클래스 설계는 견고, 숫자는 SO Inspector 튜닝
- **데이터 / 로직 분리** — ScriptableObject = 데이터(.asset), C# = 로직
- **공식 한 곳에** — 모든 게임 공식은 `Scripts/Core/Formulas.cs`
- **이벤트 기반** — UI 폴링 X, GameEventsSO 채널 구독 (Day 14)
- **하이브리드 아키텍처** — World Space (게임 객체) + UI Toolkit (HUD/모달)

## 폴더 구조

```
Assets/_Project/
├── Scripts/
│   ├── Core/           # GameManager, GameState, Formulas, SaveManager, IRandomProvider
│   │   └── Events/     # GameEventsSO (이벤트 채널)
│   ├── Systems/        # Customer/Staff/Day/Gacha/TrackSystem
│   ├── Domain/         # Customer, Staff (POCO + enum)
│   ├── Data/           # ScriptableObject 정의 (7종)
│   ├── UI/             # UIController (UXML 기반)
│   ├── Visual/         # SceneController, *View, CharacterMover, CustomerUIOverlay, SortingOrders
│   ├── Utils/          # SpriteFactory, SceneCoords, ScenePositions, RandomName
│   ├── Editor/         # SceneBuilder, Builders/, SaveDebugMenu, CleanupTools, DebugWindow
│   │   └── Builders/   # SpriteAsset/Prefab/UIAsset/SceneLayout Builder + BuildPaths
│   ├── Project.Runtime.asmdef
│   └── Editor/Project.Editor.asmdef
├── Settings/           # SO 인스턴스 (.asset 19+)
│   ├── GameConfig.asset       ← 글로벌 진입점
│   ├── VisualSettings.asset   ← 시각 튜닝 값
│   ├── Pack_Basic.asset, Pack_Pro.asset, RepLevelTable.asset
│   ├── Events/                ← 이벤트 채널 (GameEvents.asset)
│   ├── MenuItems/, RarityConfigs/, RepLevels/, TrackConfigs/
├── Art/                # SF_*.png (절차 생성), Staff_Chef.png, Customer_*.png (사용자), Menu_*.png
├── Prefabs/            # Customer/StaffPrefab.prefab (Builder 생성)
├── UI/                 # HUD.uxml/uss, TrackCard.uxml (Builder 생성, 사용자 편집 시 보존)
├── Scenes/Main.unity
└── New Panel Settings.asset
```

## 데이터 계층 (`Scripts/Data/`)

| SO | 인스턴스 | 역할 |
|---|---|---|
| `GameConfigSO` | 1 | 글로벌 진입점. 모든 SO 참조 + 모든 상수 + `InitialStaffRarities[]` |
| `TrackConfigSO` | 8 | 8 트랙 메타+비용 곡선 (Active 4 + Passive 4) |
| `MenuItemSO` | 3 | Hotdog (cook 2500ms, $2, 100%), Cola (500ms, $1, 50%), Salad (1500ms, $2, 30%) |
| `RarityConfigSO` | 3 | Novice (move 2.0s), Pro (1.5s, 0.75x cook), Expert (1.0s, 0.5x cook) |
| `StaffPackSO` | 2 | Basic (💎10, N70/P25/E5), Pro (💎50, P70/E30) — `OnValidate` 확률 합 검증 |
| `RepLevelTableSO` | 1 | Lv1~5 (목표 $20/50/100/300/1000) |
| `VisualSettingsSO` | 1 | 손님/직원 movement, popup 시각 튜닝 (Day 14) |

명성 Lv은 게임 파라미터에 영향 없음 (목표 $만 변경).

## 로직 계층

### `Scripts/Core/`
- `GameState` — POCO. Cash/Gems/Rating/DayRevenue 는 property (set 시 GameEventsSO raise). 그 외 8 트랙 Lv/누적 stats
- `Formulas` — static. 모든 공식. `(Lv, SO)` → 배율/비용/용량
- `GameManager` — MonoBehaviour. 시스템 초기화 + Update 루프 + 디버그 핫키 + Auto Save (30초 + focus/quit)
- `SaveManager` — static. PlayerPrefs JSON. version 체크 + 직원 명단 복원
- `IRandomProvider` — UnityRandomProvider(default) / SeededRandomProvider (시드 디버그)
- `Events/GameEventsSO` — Action 이벤트 8종 (Cash/Gems/Rating/DayRevenue/TrackUpgraded/DayPhaseChanged/DayChanged/StaffChanged). OnDisable 자동 cleanup

### `Scripts/Systems/`
| 시스템 | 역할 |
|---|---|
| `CustomerSystem` | spawn + FSM 8 stage. Tick<Stage> 메서드 추출. `Dictionary<int, Customer>` O(1) FindById. `ResetForNewDay` |
| `StaffSystem` | spawn + FSM 9 state. Tick<State> 메서드 추출. FIFO 매칭. `ResetIfTargetLost` 단방향 cleanup. `ResetAllToIdle` |
| `DaySystem` | endDay 판정/보상 + 3초 transition. Phase 변화 시 `OnDayPhaseChanged` raise |
| `GachaSystem` | StaffPackSO 1회 구매. `IRandomProvider` 주입. Success 시 `OnStaffChanged` raise |
| `TrackSystem` | 8 트랙 Lv 조회/업그레이드. Success 시 `OnTrackUpgraded` raise |

### `Scripts/Domain/` (POCO + enum)
- `Customer` — Id, Stage, StageElapsed, SeatIdx/QueueIdx, Mood, Order, WaitElapsed/WaitActive, LastRevenue, SpriteIdx, VisuallyAt(Seat/Queue/Exit), SettleElapsed
- `Staff` — Id, Name, Rarity, State, StateElapsed, TargetSeat/CustomerId, Order, CookingStepIdx, CurrentStation
- enums: `CustomerStage` (8), `CustomerMood`, `StaffState` (9)

### `Scripts/Visual/` (World Space)
- `SceneController` — POCO ↔ View 동기화. Customer/Staff Instantiate. mood 전이 → RevenuePopup. `[SerializeField]` prefabs/anchors/visualSettings
- `CharacterMover` (POCO) — 이동(가속/감속 v²/2a) + bob/sway (누적 거리 기반 hop, 절반 주파수 sway). idle bob 옵션
- `CustomerView` (slim) — Customer bind + body sprite + mood 색 + route follower + arrival flags
- `CustomerUIOverlay` (sibling) — 인내심 pie (procedural texture) + 주문 말풍선 (icon 1~3)
- `StaffView` (slim) — Staff bind + State별 target world + idle bob mode
- `RevenuePopupView` — TextMeshPro World Space "+$N" 떠오름 + fade out. `VisualSettings.popup*` 참조
- `SortingOrders` (static class) — 모든 sortingOrder 매직 넘버 통합 (-25 ~ 30)

### `Scripts/UI/` (UI Toolkit)
- `UIController` — `[SerializeField]` PanelSettings/UXML/USS/GameEvents/TrackCardTemplate. GameEventsSO 채널 구독 (OnEnable/OnDisable). `Update()` 는 timeBar/dayTime 만 매 프레임. 트랙 카드는 TrackCard.uxml Instantiate + Q<>(). 가챠 버튼 코드 생성

### `Scripts/Editor/` (Editor only)
- `SceneBuilder` — orchestrator (메뉴 dispatcher만)
- `Builders/SpriteAssetBuilder` — SF_*.png 절차 생성 + 외부 PNG import 동기화
- `Builders/PrefabBuilder` — Customer/StaffPrefab 생성 + 컴포넌트 ref 자동 wire
- `Builders/UIAssetBuilder` — HUD.uxml/uss + TrackCard.uxml seed (파일 존재 시 보존)
- `Builders/SceneLayoutBuilder` — StaticScene tree + Camera + SceneController/UIController/GameManager auto-wire. Incremental / Full Rebuild 모드
- `SaveDebugMenu` — Reset Save / Print Save Json / Set RNG Seed
- `CleanupTools` — Missing scripts 진단/제거, 메뉴 아이콘 procedural PNG 생성/적용
- `DebugWindow` — Play 모드 POCO 상태 실시간 뷰 + 속도 컨트롤

## 시각 좌표계

- HTML 프로토 % 좌표 → Unity world (xPct 0~100 → x [-5, +5], yPct 0~100 → y [+4, -4]). `Utils/SceneCoords.ToWorld(xPct, yPct)`
- Scene anchor (SceneAnchors registry) 가 truth — Hierarchy의 anchor GameObject 위치 변경 시 즉시 반영
- Camera Orthographic size 8, Y −1.5 (SceneBuilder 기본값. 사용자 Inspector 튜닝)
- sortingOrder: `Scripts/Visual/SortingOrders.cs` static class 참조

## 메뉴 구조 (Editor)

```
Idle Restaurant/
├── Build/
│   ├── Sync Missing Only        ← 기본, 안전 (누락만 추가, 사용자 튜닝 보존)
│   ├── Initial Setup            ← 첫 셋업
│   └── Full Rebuild (Destructive) ← 확인 다이얼로그 후 강제 재생성
├── Cleanup Missing Scripts (active scene)
├── Diagnose Scene (count missing scripts)
├── Debug Window                 ← Play 모드 POCO 실시간 뷰
├── Debug/
│   ├── Reset Save / Print Save Json
│   └── Set RNG Seed / Clear RNG Seed
└── Legacy/
    ├── Rebuild Customer Prefab Only
    ├── Configure Customer Sprites
    ├── Apply Station Icons
    ├── Generate Menu Icon Placeholders (procedural)
    └── Add Station Icons (non-destructive)
```

## 핵심 결정사항

| 결정 | 이유 |
|---|---|
| NRT 미사용 | 프로토 단계 학습 부담 ↓ |
| `Formulas.cs` static 통합 | 수식 변경 시 한 파일만 |
| `GameConfigSO` 단일 진입점 | Composition Root |
| 명성 Lv은 표시만 | 게임 파라미터 영향 X (단순화) |
| 단방향 의존 (Staff → Customer) | 한 프레임 내 일관성 |
| `ResetIfTargetLost` 옵션 B | 양방향 의존 회피 |
| World Space + UI Toolkit overlay | 정석 하이브리드 |
| 이벤트 기반 UI 갱신 (Day 14) | 매 프레임 폴링 제거 + 확장성 |
| `.visible` 클래스로 모달 토글 | USS transition은 display 변경에 작동 X |
| Sync Missing Only 기본 메뉴 (Day 14) | 사용자 수동 튜닝 보존 — 자동화 신뢰 회복 |
| 라이브 인프라 명시 배제 (Day 14) | Firebase/Addressables/Audio/i18n/DOTween 등 M2 진입 전 도입은 안티패턴 |

## 진행 상황 (M1 마일스톤)

| Day | 요약 |
|---|---|
| 1~3 | 셋업 + SO 6종 + GameState/Formulas/GameManager Foundation |
| 4~6 | CustomerSystem (8 stage FSM) → StaffSystem (9 state FSM) → DaySystem (endDay/transition) |
| 7 | GachaSystem + 디버그 핫키 (B/P) |
| 8 | 시각 골격 — SpriteFactory, SceneCoords, *View, SceneController |
| 9 | HUD + 트랙 카드 + 모달 (UI Toolkit) |
| 10 | 정석 패턴 마이그레이션 — Hierarchy 노출 + Prefab + UXML/USS 분리 |
| 11 | 시각 폴리시 1 — PanelSettings, Idle bob, RevenuePopup, 좌석 외곽선, 모달 fade |
| 12 | Sizing Pass + 카드 hover, 가챠 등급 badge, RevenuePopup 확대 |
| 13 | 모바일 idle 스타일 전면 리뉴얼 — Customer.IsQueueBound, UI 3-zone HUD + 탭, 셰프 sprite, 가속/감속 이동 + hop bob, 인내심 좌→우 |
| **14** | **M1 종료 전 구조 정리** (아래 상세) |

### Day 14 ✅ M1 종료 전 구조 정리 (2026-05-08)

베테랑 모바일 클라 관점 ROI 있는 항목만 선별. 라이브 운영 인프라는 명시적 배제.

**Tier S (인프라)**
- **S1 SaveManager** — PlayerPrefs JSON. Day/Cash/Gems/Rating/8 트랙 Lv/누적 stats/직원 명단. 30초 자동 + focus/quit.
- **S2 ScriptableObject Event Channel** — `GameEventsSO` + `Settings/GameEvents.asset`. UIController.Update 폴링 제거. GameState property 자동 raise.
- **S3 asmdef 분할** — `Project.Editor.asmdef`. Editor/Runtime 컴파일 도메인 분리.
- **S4 SceneBuilder 분할** — 4개 Builder + thin orchestrator + 메뉴 3분할 (Sync Missing Only / Initial Setup / Full Rebuild Destructive).

**Tier A (코드 정리)**
- **A1 View 책임 분리** — `CharacterMover` (POCO), `CustomerUIOverlay` (sibling). CustomerView 520→200줄, StaffView 140→80줄.
- **A2 ProcessStage 메서드 추출** — 거대 switch → case별 private Tick 메서드.
- **A3 시드 가능 RNG** — `IRandomProvider` 주입. 가챠 분포 검증/디버그 재현.
- **A4 ID Lookup Dictionary** — Customer FindById O(n) → O(1).

**Tier B (시각 튜닝 외부화)**
- **B1 VisualSettings SO** — movement / popup 매직 넘버 외부화.
- **B2 SortingOrders 상수** — 매직 넘버 (-25 ~ 30) 통합.
- **B3 UXML Template** — TrackCard.uxml. 코드 생성 → Instantiate + Q<>().

**기타 정리 (post-Day 14)**
- CleanupTools obsolete 함수 3개 제거 (AddDoorWaypointsIfMissing / RestoreUserLayout / FixLayoutGeometry — Sync Missing Only로 대체)
- CustomerView dead 필드 제거 (patienceFgT/patienceFgSr)

**명시적 배제 (M2/라이브 진입 시 재검토)**
Firebase Analytics/Crash, Addressables, Localization, Audio, DOTween, Animator/Timeline, ECS, Rich Domain Model, Optional/NRT, State Pattern 풀 도입, 객체 풀, Safe Area, 다중 해상도

## 다음 단계 후보 (M2)

| 묶음 | 항목 |
|---|---|
| **실제 아트** | 도형 → sprite/spine 마이그레이션. 캐릭터/배경/메뉴 |
| **사운드** | BGM + SFX + AudioMixer. 모바일 캐주얼 필수 |
| **로컬라이제이션** | Unity.Localization. 한/영 우선 |
| **라이브 인프라** | Addressables (패치 무중단), Firebase Analytics/Crash |
| **콘텐츠 확장** | 추가 메뉴/트랙/등급, Day 진행 곡선 튜닝 |
| **UI 폴리시** | DOTween 통합, 모달 풍부화, 안전영역(notch) |

## 외부 참고 자료

- **HTML 프로토** (살아있는 디자인 문서): `C:\Users\Mooseong\Desktop\Claude\Prototype\idle001_prototype_v015.html` (~92KB, JS 956~2520)
- **Notion 기획서**:
  - [03-1. 기획서 v1.1](https://www.notion.so/349a6051cf6a8175bc25f1abc5c8c1d0)
  - [04. 4-Quality 안 적용](https://www.notion.so/34aa6051cf6a810bb928f590b92cbf84)
  - [05. Unity 작업 설계](https://www.notion.so/354a6051cf6a8143bfd4df56cb4d07ae)

## 새 세션 시작 체크리스트

1. **이 파일 읽기** — 30초 스캔으로 현재 상태 파악
2. **Unity Editor 활성화** — 자동 재컴파일 → Console 에러 0 확인
3. **(미설치 시) TMP Essential Resources** — `Window > TextMeshPro > Import TMP Essential Resources`
4. **(필요 시) Build > Sync Missing Only 실행** — 누락 자산만 자동 wire. 기존 씬/카메라/prefab 튜닝 보존
5. **Inspector 슬롯 확인** — GameManager의 `Game Events`, SceneController의 `Visual Settings`, UIController의 `Game Events` + `Track Card Template`
6. **Ctrl+S → Play** — HUD/사이클/모달/RevenuePopup 정상 + Stop → Play 재진입 시 Day/Cash/Gems 보존 (S1)
7. **다음 단계** — 사용자가 위 "다음 단계 후보" 중 선택 → 자율 진행
