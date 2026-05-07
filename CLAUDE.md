# Idle Restaurant Tycoon — Unity 마이그레이션

HTML 프로토타입 v0.15.1을 Unity 6 LTS로 이식. 첫 마일스톤 **M1: HTML 1:1 재현**.

## 기술 스택

- Unity 6 LTS (Universal 2D / URP 2D)
- Android 타겟
- UI Toolkit (uGUI 아님) — UXML/USS 분리
- TextMeshPro (World Space 텍스트, tip popup용)
- C# (NRT 미사용 — 프로토 단계 학습 부담 줄이기 위해 스킵)
- 단위 테스트 없음 (프로토 단계)
- 도형/색상만 (실제 아트는 후속 단계)

## 프로젝트 철학

- **Less is More** — 미래 가정 기능 금지, 필요할 때 추가
- **구조 탄탄 / 수치 임시** — 클래스 설계는 견고하게, 숫자는 SO Inspector에서 빠르게 튜닝
- **데이터 / 로직 분리** — ScriptableObject = 데이터(.asset, Inspector 편집), C# = 로직
- **공식 한 곳에** — 모든 게임 공식이 `Scripts/Core/Formulas.cs`에 모임
- **정석 패턴** — Hierarchy 노출, Prefab, UXML/USS 분리 (Day 9에서 완전 마이그레이션)
- **하이브리드 아키텍처** — World Space (게임 객체: SpriteRenderer + TMP) + UI Toolkit overlay (HUD/모달)

## 폴더 구조

```
Assets/_Project/
├── Scripts/
│   ├── Core/        # GameManager, GameState, Formulas
│   ├── Systems/     # CustomerSystem, StaffSystem, DaySystem, GachaSystem, TrackSystem
│   ├── Domain/      # Customer, Staff (POCO + enum)
│   ├── Data/        # ScriptableObject 정의 (6종)
│   ├── UI/          # UIController (UXML 기반 HUD/모달)
│   ├── Visual/      # SceneController, CustomerView, StaffView, RevenuePopupView
│   ├── Utils/       # SpriteFactory, SceneCoords, ScenePositions, RandomName
│   └── Editor/      # SceneBuilder (#if UNITY_EDITOR — 메뉴: Idle Restaurant > Build Static Scene)
├── Settings/        # SO 인스턴스 (.asset, 18개)
│   ├── GameConfig.asset          ← 글로벌 진입점
│   ├── Pack_Basic.asset, Pack_Pro.asset
│   ├── MenuItems/        (3)
│   ├── RarityConfigs/    (3)
│   ├── RepLevels/        (1)
│   └── TrackConfigs/     (8)
├── Art/             # SF_Square.png, SF_Circle.png (코드 생성, EditorScript)
├── Prefabs/         # CustomerPrefab.prefab, StaffPrefab.prefab (EditorScript 생성)
├── UI/              # HUD.uxml, HUD.uss (EditorScript 생성)
├── Scenes/Main.unity
└── New Panel Settings.asset    # UI Toolkit PanelSettings (1080×1920 portrait, Width 우선)
```

`Project.Runtime.asmdef`은 `Scripts/` 직하. `references: ["Unity.TextMeshPro"]` 필수.

## 데이터 계층 (`Scripts/Data/`)

| SO | 인스턴스 | 역할 |
|---|---|---|
| `GameConfigSO` | 1 | 글로벌 진입점. 모든 SO 참조 + 모든 상수 + `InitialStaffRarities[]` (초기 직원 등급 배열) |
| `TrackConfigSO` | 8 | 8 트랙 메타+비용 곡선 (Active 4 + Passive 4) |
| `MenuItemSO` | 3 | Hotdog (cook 2500ms, $2, 100%), Cola (500ms, $1, 50%), Salad (1500ms, $2, 30%) |
| `RarityConfigSO` | 3 | Novice (move 2.0s, cookMult 1.0), Pro (1.5s, 0.75), Expert (1.0s, 0.5) |
| `StaffPackSO` | 2 | Basic (💎10, N70/P25/E5), Pro (💎50, P70/E30) — `OnValidate` 확률 합 검증 |
| `RepLevelTableSO` | 1 | Lv1~5 (목표 $20/50/100/300/1000, 별 1/1/3/5/max) |

**중요**: 명성 Lv은 게임 파라미터에 영향 없음 (목표 $만 변경, 단순화 철학).

## 로직 계층

### `Scripts/Core/`

- **`GameState`** — POCO. cash, gems, rating, 8 트랙 Lv, day, dayRevenue, 누적 stats
- **`Formulas`** — static. 모든 공식. `(Lv, SO)` → 배율/비용/용량
- **`GameManager`** — MonoBehaviour. 시스템 초기화 + Update 루프 (Phase==Playing일 때만 영업) + 디버그 핫키 B/P + `WarnIfSceneIncomplete`

### `Scripts/Systems/`

| 시스템 | 역할 |
|---|---|
| `CustomerSystem` | 손님 spawn + FSM 8 stage (Arriving/Queueing/Seating/SeatedOrdering/SeatedWaiting/Eating/Result/Leaving). `ResetForNewDay`로 Day 전환 정리 |
| `StaffSystem` | 직원 spawn + FSM 9 state (Idle/TakingOrderMove/TakingOrder/ReturningToKitchen/CookingMove/CookingItem/ServingMove/Serving/Returning). `AssignStaffToTakeOrder` FIFO 매칭. `ResetIfTargetLost` 단방향 cleanup. `ResetAllToIdle` 으로 Day 전환 정리 |
| `DaySystem` | 시간 카운트 + endDay 판정/보상 + 3초 transition + 다음 Day reset. `DayPhase` enum (Playing/Transitioning), `DayResult` struct |
| `GachaSystem` | StaffPackSO 1회 구매 (💎 차감 + 누적 확률 추첨 + Spawn). `BuyResult` enum |
| `TrackSystem` | 8 트랙 Lv 조회/업그레이드 결제. `BuyResult` enum |

### `Scripts/Domain/` (POCO + enum)

- **`Customer`** — id, stage, stageElapsed, seatIdx/queueIdx, mood, order, waitElapsed/WaitActive, **LastRevenue** (tip popup용)
- **`Staff`** — id, name, rarity, state, stateElapsed, targetSeat/targetCustomerId, order(사본), cookingStepIdx, currentStation
- **`CustomerStage`** / **`CustomerMood`** / **`StaffState`** enum

### `Scripts/Visual/` (World Space — SpriteRenderer + MonoBehaviour)

- **`SceneController`** — 매 프레임 POCO ↔ View 동기화. 정적 객체(좌석)는 `Find`로 캐시. mood 전이 감지로 tip popup 트리거. `[SerializeField]` customerPrefab/staffPrefab
- **`CustomerView`** — Stage별 위치 + mood 색 + 머리 위 인내심 바. exponential smoothing 보간 (rate 6, ~0.17s 시정수)
- **`StaffView`** — State별 위치 (idle slot ↔ 좌석 ↔ 스테이션 ↔ serve). Idle 시 sin bobbing (4Hz, 0.06unit). 등급 색
- **`RevenuePopupView`** — World Space TextMeshPro "+$N" 1.4초 위로 떠오름 + fade out (손님 주문 결제 = 수익. Passive 트랙 "팁"과 무관)

### `Scripts/UI/` (UI Toolkit)

- **`UIController`** — `[SerializeField]` PanelSettings/hudUxml/hudUss. UXML 정적 구조 + Q<>로 element 캐시 + 트랙 카드 8개/가챠 버튼 N개 동적 생성. 모달 `.visible` 클래스로 fade in transition. 코드 폴백 유지

### `Scripts/Editor/` (Editor only, `#if UNITY_EDITOR`)

- **`SceneBuilder`** — 메뉴 `Idle Restaurant > Build Static Scene`. 1회 클릭으로 모든 자산 생성:
  - Sprite asset (SF_Square.png, SF_Circle.png) PNG 인코딩 + import as Sprite
  - Prefab (CustomerPrefab, StaffPrefab) — 임시 GameObject + SerializedObject로 SerializeField 자동 채움 + `PrefabUtility.SaveAsPrefabAsset`
  - UI asset (HUD.uxml, HUD.uss) — 코드 const 문자열을 `File.WriteAllText`로 작성
  - Hierarchy GameObject 트리 (StaticScene/SceneController/UIController)
  - Camera 설정 (Orthographic size 9, 배경색)
  - PanelSettings/Prefab/UXML/USS 슬롯 자동 연결

## 시각 좌표계

- HTML 프로토 % 좌표 → Unity world `[-5, +5] × [-3, +3]` (가로 10, 세로 6, 비율 1.67:1)
- `Utils/SceneCoords.ToWorld(xPct, yPct)` 변환 (y 반전)
- `Utils/ScenePositions` — Tables, SeatPos, ServePos, StationPos, StaffSlots, Door, OutsideSpawn, RightExit, QueuePos
- Camera Orthographic size 9 (Portrait 9:16에 scene 영역이 잘 들어감 + 위아래 여유)
- Sorting order: zone(-15) < table(-10) < station(-8) < seat outline(-6) < seat(-5) < customer(5) < staff(6) < patience(9~10) < tip popup(30)

## 핵심 결정사항

| 결정 | 이유 |
|---|---|
| NRT 미사용 (csc.rsp 스킵) | 프로토 단계 학습 부담 줄이기 |
| `Formulas.cs` static 통합 | 수식 변경 시 한 파일만. 정석 + 단순 |
| `GameConfigSO` 단일 진입점 | Composition Root. GameManager는 이거 하나만 받음 |
| `OnValidate` 확률 합 검증 | 기획자 입력 실수 방지 (StaffPackSO) |
| `Track_Venue.maxLv = 5` | 좌석 6석 한계 |
| 명성 Lv은 표시만 | 게임 파라미터 영향 X (단순화 철학) |
| 단방향 의존 (Staff → Customer) | Customer.Stage를 Staff가 직접 변경. 한 프레임 내 일관성 |
| `ResetIfTargetLost` 옵션 B | StaffSystem이 매 tick "leaving 손님 타겟"이면 자동 정리. 양방향 의존 회피 |
| 초기 spawn 데이터 = `RarityConfigSO[]` | 길이 = 명수, 슬롯 = 등급. 가챠 미구현 시점의 SoT |
| 정석 패턴 마이그레이션 (Day 9~10) | Hierarchy 노출 + Prefab + UXML/USS. EditorScript로 자동화 |
| World Space (SpriteRenderer + TMP) + UI Toolkit overlay | 정석 하이브리드. 게임 객체는 World, HUD/모달은 UI Toolkit |
| 보간 = exponential smoothing rate 6 | dt-independent, ~0.17s 시정수. 텔레포트 방지 |
| Tip popup = TextMeshPro World Space | 폰트 한계 있지만 숫자 표시 충분. asmdef에 `Unity.TextMeshPro` 추가 |
| `.visible` 클래스로 모달 토글 | USS transition은 display 변경에 작동 안 함. visible 클래스로 display + scale/opacity transition |

## 진행 상황

### Day 1 ✅ 프로젝트 셋업
폴더 트리, Main.unity, Project.Runtime asmdef, NRT 미사용 결정.

### Day 2 ✅ SO 6종 + 인스턴스 18개
6 SO 클래스 + 18 .asset YAML 직접 생성.

### Day 3 ✅ Foundation
GameState/Formulas/GameManager — 부팅 스모크 테스트 통과.

### Day 4 ✅ Customer 도메인 + CustomerSystem
8 stage FSM, spawn/인내심 timeout/좌석·큐 관리. Day 4 시점에 직원 미구현이라 모든 손님 pfail로 떠나는 게 정상.

### Day 5 ✅ StaffSystem
9 state FSM, FIFO 매칭, 단방향 의존 + ResetIfTargetLost. `GameConfigSO.InitialStaffRarities[]` 추가.

### Day 6 ✅ DaySystem
endDay 판정/보상 (💎 base+bonus, ⭐+1), 3초 transition, 다음 Day reset, 명성 Lv up 검출. `DayPhase`/`DayResult`.

### Day 7 ✅ GachaSystem + 디버그 핫키 (B/P)
StaffPackSO 1회 구매. 누적 확률 추첨. `Utils/RandomName` 분리.

### Day 8 ✅ 시각 표현 골격
SpriteFactory (코드 생성), SceneCoords, ScenePositions, CustomerView/StaffView, SceneController. exponential smoothing 보간 + 인내심 바.

### Day 9 ✅ HUD + 트랙 카드 + 모달 (UI Toolkit)
UIController. Top HUD (cash/gems/rating, Day/시간 바, 목표 바). 트랙 카드 8개 (Active 4 + Passive 4 그리드). 가챠 버튼. Day End 모달 + 가챠 결과 모달.

### Day 10 ✅ 정석 패턴 마이그레이션 (Step 1+2+3)
- **Step 1**: 정적 씬 + UIDocument를 Hierarchy에 노출 (EditorScript SceneBuilder)
- **Step 2**: Customer/Staff Prefab 생성. SceneController가 `Instantiate(prefab)` 사용. CustomerView/StaffView에 `[SerializeField]` + 코드 폴백
- **Step 3**: HUD.uxml + HUD.uss 분리. UIController는 Q<>로 query

### Day 11 ✅ 시각 폴리시 묶음 1
- PanelSettings 1080×1920 portrait
- Staff Idle 시 bobbing (sin oscillation)
- Revenue popup ("+$N" TextMeshPro World Space, 1.4초) — 손님 주문 결제 = 수익. Passive 트랙 "팁"과 무관
- 좌석 cap 외곽선 (활성 진한색 / 잠금 옅은색)
- 모달 fade in (USS scale + opacity transition, `.visible` 클래스 토글)

### Day 13 ✅ 모바일 Idle 스타일 전면 리뉴얼 (2026-05-04~05)

#### 손님 흐름 자연화
- `Customer.IsQueueBound` 추가 — spawn 시 좌석 여유 없으면 true. Arriving 단계에서 Door 안 거치고 **큐 위치로 직행** (이전엔 Door까지 갔다가 백트래킹)
- `EffectiveQueueOccupancy` (queueing + arriving qb 합산) 으로 spawn cap 검사 강화
- `ReassignQueueIndices`도 arriving qb 통합 인덱싱

#### UI 전면 재구성 (HUD.uxml/uss)
- **레이아웃**: top-card | scene-area (transparent, **flex-grow**) | main-content (**고정 480px**, ScrollView 안에 탭 콘텐츠) | bottom-tabs (Upgrades / Gacha)
- **중요**: ScrollView 무너짐 방지 — `main-content` 고정 height + `scene-area` flex-grow 조합 (반대로 두면 콘텐츠 큰 탭에서 sibling 밀어냄)
- **HUD 3-zone pill**: cash/gems/rating 흰 pill `width: 31%` 고정 (Label flex-grow가 잘 안 먹어 명시적 width). top-card는 회색 카드 bg
- **트랙 카드 재설계**: 가로 헤더(icon-box + name + Lv pill + ⓘ info) + preview row("현재 → 다음(녹색)") + 풀너비 Upgrade 버튼(우측 cost pill)
- **가챠 별도 탭**: 이전엔 upgrades 안 → 이제 하단 탭 전환

#### 월드 좌표계 portrait 적합화
- `SceneCoords.HalfHeight`: 3 → **4** (캔버스 6→8, 1.67:1 가로 → 1.25:1)
- `ScenePositions` yPct **5~95 분산** — Grill (10, 12), Drink (20, 18), Salad (14, 88), Tables (37/50/63, 50), Door (83, 88), Queue (92, 85-idx*15), StaffSlots y 25~75
- 월드 콘텐츠 vertical span: 3.3 → 6 unit (거의 2배), 카메라 가시 영역 fill 21% → 38%
- 카메라: ortho 9 → **8**, Y 0 → **−1.5** (SceneBuilder 기본값. Inspector에서 사용자 튜닝)

#### Staff 셰프 sprite 적용
- `Assets/_Project/Art/Staff_Chef.png` (사용자 제공 PNG, transparent bg, ~512px)
- StaffPrefab 새 구조: root (scale 0.85) → BgFrame (둥근 사각형 1.2 scale, 연한 하늘색, sortingOrder 6) + Body (셰프 sprite, scale 1, sortingOrder 7)
- `SpriteFactory.MakeRoundedSquareTex` 추가 (BgFrame 절차 생성)
- `SceneBuilder.ConfigureExternalSprite` — 사용자 PNG는 덮어쓰지 않고 importer 설정만 동기화 (PPU 등)
- 등급 색 sprite 곱셈 **비활성** (`Bind`에서 `sr.color = Rarity.Color` 제거 — 셰프 얼굴 변색 회피). 차후 BgFrame 색 변경 또는 badge로 표시 예정

#### 이동 시스템 전면 재작성 (CustomerView/StaffView)
- 지수 보간 → **가속/감속 (`v²/2a` 정지거리 기반)**, 시작/정지 부드러움
- bob phase 시간 기반 → **누적 이동 거리 기반** (`StepsPerUnit`) — 빠르면 bob 빠르고 느리면 bob 느림
- abs(sin) hop 패턴 — 위로만 튀는 "뚜까뚜까"
- 수평 sway 추가 (절반 주파수, 펜듈럼)
- 진폭은 `currentSpeed/MaxSpeed` 비율로 스케일 — 가속/감속 시 자연스러움
- 현재값: Customer maxSpeed 0.7 / accel 2.5 / BobAmpY 0.20 / SwayAmpX 0.02. Staff 0.9 / 3 / 0.19 / 0.02
- Idle bob (Staff Idle 상태일 때만) 별도 유지 (호흡 1.6Hz, amp 0.04)

#### 인내심 바 우→좌 줄어듦 수정
- `SpriteFactory.LeftSquare()` 추가 — pivot (0, 0.5) 좌측 중앙
- `CustomerView.Awake`에서 fg sprite를 LeftSquare로 교체 + `localPosition.x = −0.5` (BG 좌측 끝)
- `UpdatePatienceBar`는 `localScale.x = pct`만 — pivot이 left라 scale 줄여도 좌측 anchor 유지

#### 작업 스타일 메모 (2026-05-04)
- 사용자가 "효율적으로 작업" 명시 → 프로토타입 단계 자동화 금지, 수치 튜닝은 사용자 Inspector 직접
- 메모리 `feedback_efficient_prototyping.md` 저장됨

### Day 12 ✅ Sizing Pass + UI Polish A
- **사이즈 일괄 확대 (~×2.2)**: HTML 460px 프로토를 1080 ref로 그대로 복사한 px 사이즈가 2~3% 폭으로 너무 작던 문제 해결
  - HUD 22→40 / 14→28, 모달 24→44 / 16→30, 트랙카드 11→24 / 12→26, 가챠 14→28
  - 패딩/마진/border-radius/bar 두께 모두 비례 확대
- **트랙 카드 hover** scale 1.04, 가챠 버튼 hover scale 1.03 (USS transition)
- **카드/모달 subtle border** (UI Toolkit는 box-shadow 미지원 → 1px rgba 0,0,0,0.06~0.08로 그림자 대용)
- **가챠 결과 모달**: 성공 시 등급 색 큰 원형 badge (140×140) title 위에 표시
- **RevenuePopup** fontSize 4→5 + RiseDistance 0.7→0.9
- **SceneBuilder 동작 개선**: `EnsureUIAssets`가 파일 존재 시 덮어쓰지 않음 (사용자/Claude의 .uxml/.uss 직접 편집 보존). const는 fresh setup용 fallback으로 동기화
- **TipPopupView → RevenuePopupView 리네임** + 메모리에 "수익 vs 팁" 개념 분리 저장 (CookieClicker도 잠재적 영향)

**검증 안내 (사용자)**:
1. Game view 해상도 → **1080×1920 Portrait** 확인 (잘못된 aspect면 비율 왜곡)
2. Console 에러 0
3. Play → 모든 텍스트 가독성 OK / 트랙 카드 hover 시 살짝 커짐 / 가챠 성공 모달에 등급 색 큰 원형 노출 / Revenue popup 적당히 큼

## 다음 단계 후보

| 묶음 | 항목 |
|---|---|
| **2. UI 폴리시** | HTML 톤 정밀화 (그림자/색감), 트랙 카드 hover, 가챠 결과 모달 풍부 (등급 색 큰 배경) |
| **3. 직원/손님 디테일** | 직원 cooking 시 스테이션 active 펄스, 직원 상태 emoji (TMP), 손님 주문 버블 |
| **4. 디버그 패널** | 시간 가속/일시정지, stats 표시, snapshot 다운로드 |
| **5. 카메라/시각** | World 영역만 보기 좋게 카메라 zoom, 안전영역(notch 대응) |
| **M2 시작** | 실제 아트 도입 (도형 → sprite/spine), 사운드, 사용자 정의 폰트 |

## 외부 참고 자료

- **HTML 프로토 (살아있는 디자인 문서)**: `C:\Users\Mooseong\Desktop\Claude\Prototype\idle001_prototype_v015.html` (~92KB, JS 956~2520)
- **Notion 기획서**:
  - [03-1. 기획서 v1.1 (v0.14 반영)](https://www.notion.so/349a6051cf6a8175bc25f1abc5c8c1d0)
  - [04. 4-Quality 안 적용 (v0.15)](https://www.notion.so/34aa6051cf6a810bb928f590b92cbf84)
  - [05. Unity 작업 설계](https://www.notion.so/354a6051cf6a8143bfd4df56cb4d07ae)

## Day 14 ✅ M1 종료 전 구조 정리 (2026-05-08)

베테랑 모바일 클라 관점에서 다음 단계 진입 전 코드 구조 정리. 라이브 운영 인프라(Firebase/Addressables/Audio/i18n/DOTween)는 시기상조 제외.

### Tier S — 인프라
- **S1 SaveManager** (`Scripts/Core/SaveManager.cs`) — PlayerPrefs JSON. Day/Cash/Gems/Rating/8 트랙 Lv/누적 stats/직원 명단 보존. Application.focusChanged + 30초 자동 세이브. 디버그: `Idle Restaurant > Debug > Reset Save | Print Save Json`
- **S2 ScriptableObject Event Channel** (`Scripts/Core/Events/GameEventsSO.cs` + `Settings/GameEvents.asset`) — Cash/Gems/Rating/DayRevenue/TrackUpgraded/DayPhaseChanged/DayChanged/StaffChanged 채널. UIController.Update 폴링 제거 (timeBar/dayTime 만 매 프레임). GameState property 전환 — set 시 자동 raise.
- **S3 asmdef 분할** (`Scripts/Editor/Project.Editor.asmdef`) — Editor 코드 변경 시 Runtime 컴파일 안 됨. Android 빌드에 Editor 코드 미포함.
- **S4 SceneBuilder 분할** (`Scripts/Editor/Builders/`) — 4개 Builder + thin orchestrator. **새 메뉴 구조**:
  - `Idle Restaurant > Build > Sync Missing Only` (기본, 안전 — 누락만 추가, 사용자 튜닝 보존)
  - `Idle Restaurant > Build > Initial Setup` (첫 셋업)
  - `Idle Restaurant > Build > Full Rebuild (Destructive)` (확인 다이얼로그 → StaticScene + 모든 asset 강제 재생성)
  - `Idle Restaurant > Legacy > Rebuild Customer Prefab Only | Configure Customer Sprites | Apply Station Icons`

### Tier A — 코드 정리
- **A1 View 책임 분리** — `Scripts/Visual/CharacterMover.cs` (POCO, 이동/bob/sway), `CustomerUIOverlay.cs` (sibling 컴포넌트, 인내심 파이 + 주문 말풍선). CustomerView 520→200줄, StaffView 140→80줄.
- **A2 ProcessStage 메서드 추출** — CustomerSystem/StaffSystem 거대 switch → case 별 private Tick<Stage> 메서드.
- **A3 시드 가능 RNG** (`Scripts/Core/IRandomProvider.cs`) — `UnityRandomProvider`(default) / `SeededRandomProvider`. GachaSystem/CustomerSystem 주입. 디버그: `Idle Restaurant > Debug > Set RNG Seed | Clear RNG Seed`
- **A4 ID Lookup Dictionary** — CustomerSystem.FindById O(n) → O(1).

### Tier B — 시각 튜닝 외부화
- **B1 VisualSettings SO** (`Scripts/Data/VisualSettingsSO.cs` + `Settings/VisualSettings.asset`) — 손님/직원 maxSpeed/accel/bobAmpY/swayAmpX, idleBobFreq, popup lifeSec/riseDistance/fontSize/color. 슬롯 비어있으면 const 폴백.
- **B2 SortingOrders 상수** (`Scripts/Visual/SortingOrders.cs`) — 매직 넘버 (-25 ~ 30) 통합.
- **B3 UXML Template** (`UI/TrackCard.uxml`) — UIController.BuildTrackCard 동적 코드 생성 → template.Instantiate + Q<>().

### 명시적 배제 (M2/라이브 진입 시 재검토)
Firebase Analytics/Crash, Addressables, Localization, Audio, DOTween, Animator/Timeline, ECS, Rich Domain Model, Optional/NRT, State Pattern 풀 도입, 객체 풀 재도입, Safe Area, 다중 해상도 지원

## 새 세션 시작 체크리스트

1. **이 파일 읽기** — 진행 상황 / 결정사항 / 폴더 구조 즉시 파악
2. **Unity Editor 활성화** — 자동 재컴파일 (Console 에러 0 확인)
3. **(미설치 시) TMP Essential Resources 임포트** — `Window > TextMeshPro > Import TMP Essential Resources`
4. **(첫 적용 시) Build > Sync Missing Only 실행** — `Idle Restaurant > Build > Sync Missing Only` (Day 14 신규 자산 자동 wire: GameEvents.asset, VisualSettings.asset, TrackCard.uxml). 기존 씬/카메라/prefab 튜닝 보존.
5. **Inspector 확인** — Hierarchy GameManager 의 `Game Events` 슬롯, SceneController 의 `Visual Settings` 슬롯이 채워졌는지 (Sync Missing Only 가 자동 처리하지만 누락 시 Settings/ 에서 직접 드래그)
6. **Ctrl+S 저장** → **Play** → HUD/게임 사이클/모달/RevenuePopup 정상 동작 확인. Stop → Play 재진입 시 Day/Cash/Gems 보존 확인 (S1).
7. **다음 단계** — M2 진입. 위 "다음 단계 후보" 중 사용자 선택 → 자율 진행
