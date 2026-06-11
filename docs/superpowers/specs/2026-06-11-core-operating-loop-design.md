# Idle Restaurant — 코어 영업 루프 설계 (Spec)

**날짜**: 2026-06-11
**작성**: mspark + Claude
**상태**: Approved (베테랑 리뷰 #1~#4 반영 완료)
**스코프**: 첫 수직 슬라이스 — 손님 spawn → 주문 → 조리 → 결제 → HUD 현금↑ **+ 업그레이드 1개**
**레퍼런스**: `Theme-Park-Idle` (실게임 `_Project` + `YYHLib`). ※ `docs/simulator` JS 시뮬은 참고 제외.

---

## 0. 확정 결정 (변경 시 고비용 — 못 박음)

| 항목 | 결정 | 이유 |
|---|---|---|
| 통화 타입 | **`double` + 단위 포맷**(K/M/B/T/aa/ab) | `int`는 2.1e9 오버플로(즉사). double=1e308이면 캐주얼 충분 |
| 오프라인 시간원 | **서버/PlayFab 시간** | 기기 시계 조작 방어. PlayFab 이미 사용 → 일관 |
| 게임 로직(FSM) 위치 | **이코노미 코어**(plain C#, tick) | 버그나는 안무를 테스트 가능하게 + 옛 `VisuallyAtSeat` 결합 차단 |
| 이동/위치 | **플로어(뷰)가 소유**, 2D waypoint | 아이콘은 보이게 이동. 코어는 위치 모름(지속시간만) |
| DI | **Service Locator**(`SystemManager`/`SingletonBehaviour`) | 테마파크 동형. 단 코어만 생성자 주입(테스트 격리) |
| 데이터 | **UGS**(Sheets→코드생성) | 테마파크 동형. 단 Phase1은 주입 config |
| 세이브 | **ES3+PlayFab+ObscuredTypes** | 테마파크 동형 |

---

## 1. 배경 / 목적

옛 Idle-Restaurant 구조는 모바일 캐주얼에 부적합 — UI Toolkit(장르 부적합), 검증 안 된 구조 과투자(에디터 43%, SO 20개), **시뮬이 렌더러에 종속**(`VisuallyAtSeat`).

**북극성**: 팀이 개발·라이브운영하기 좋은 **업계 표준 토대**를 구체적 기능 하나를 관통시키며 빚는다. 재미는 HTML 프로토로 검증됨 — 죽일 미지수는 **"구조가 라이브에서 버티나 + 팀이 개발하기 좋은가"**.

**핵심 방침**: `Theme-Park-Idle`의 하우스 스타일(YYHLib)을 **인프라 전 영역에서 그대로 채택**. 단 두 프로젝트가 공유하는 약점(**게임 로직이 MonoBehaviour에 묶여 테스트 0개**)은 고친다 — **논리 전체를 plain C# 코어로** 뺀다.

> ⚠️ 베테랑 리뷰 교훈: 옛 spec은 *트리비얼한 이코노미만* 코어로 빼고 **버그나는 안무(큐/좌석/매칭/조리 FSM)는 플로어에 남겨** 옛 결합을 재이식할 뻔했다. 그건 테마파크의 *약점*(`Attraction` MonoBehaviour)을 베끼는 것. **인프라는 테마파크 동형, 로직 위치는 테마파크를 베끼지 않는 유일한 한 곳.**

---

## 2. 아키텍처 — 코어(논리 전부) / 플로어(연출만)

| | **이코노미 코어** | **라이브 플로어** |
|---|---|---|
| 책임 | **손님/직원/큐/좌석/조리 FSM 전부** + 현금·가격·결제·오프라인 | 손님 걷기·아이콘 애니·연출 |
| 상태 | 논리(좌석 인덱스, tick 카운트). **위치 없음** | 실좌표·애니메이션 |
| 성격 | 헤드리스·plain C#·결정적·테스트됨 | 프레임 종속·시각적 |
| 시간 | 고정 20Hz tick (지속시간으로 전이) | 프레임 보간 |
| 오프라인 | closed-form 수식 | **실행 안 함** |
| 의존 | `SystemManager` 미참조(생성자 주입) | YYHLib 인프라 사용 |

### 불변 규칙 (asmdef 경계 + 테스트로 강제)

> 1. 코어는 **씬 0개로 컴파일·단위테스트**된다. `UnityEngine`/`SystemManager` 의존 0.
> 2. 코어가 **모든 게임 로직을 소유**한다 — "손님3 tick T에 논리 착석, 직원1 T+5에 조리 시작, 주문 T+N 서빙". 위치가 아니라 **지속시간(ticks)** 으로 전이.
> 3. 플로어는 코어의 논리 상태를 **읽어 애니메이션만** 한다 — 좌석 인덱스 → 월드 anchor, walk 지속시간 동안 아이콘 이동.
> 4. 플로어는 코어에 **플레이어 입력(Command)만** 넣는다. **"도착했다"로 로직을 gate 하지 않는다** → 옛 `VisuallyAtSeat` 종속 제거.
> 5. 코어는 플로어를 안 기다린다. 오프라인은 플로어를 건너뛴다.

---

## 3. 모듈 (asmdef) — 린 분할

```
Game.Core      ★ 논리+이코노미: plain C#, UnityEngine 의존 0          ← 자체 asmdef
  ├ Domain        Customer/Staff/Order FSM (enum, tick) · 큐 · 좌석배정 · 직원매칭
  ├ Economy       현금(double)·가격·결제 · 오프라인 closed-form
  ├ Events        도메인 이벤트(CashChanged, OrderServed…)
  └ Commands      플레이어 의도(BuyUpgrade, …) — 코어 유일 입구
Game.Tests     ★ 코어 단위 테스트                                    ← 자체 asmdef
(나머지는 테마파크처럼 기본 어셈블리 — 과분할 회피)
  Floor           2D 손님/직원 에이전트(이동·애니), 코어 논리 상태 읽어 연출
  Presentation    UGUI 반응형 뷰(EventMonoBehaviour) + 주스 + 숫자 단위포맷
  Data            UGS 생성 데이터(메뉴/밸런스) — Phase5
  App             부트스트랩(SystemManager 활성화, 코어 조립, 틱 드라이버)
```

> `Game.Core` + `Game.Tests`만 asmdef 강제(코어를 UnityEngine 없이 컴파일+테스트 참조). 나머지는 테마파크 컨벤션(단일 어셈블리).

---

## 4. 이코노미 코어 (`Game.Core`)

- **고정 20Hz tick**, `Core.Tick()` = 상태의 순수 함수. *결정적 테스트를 위한 고정틱*(리플레이/넷코드 목적 아님). 플레이 중엔 드라이버가 프레임당 고정 dt로 step, 뷰는 보간.
- **논리 FSM**: 손님(도착→착석→주문→대기→식사→결제→퇴장) · 직원(매칭→이동→조리×N→서빙→복귀) · 큐/좌석배정/FIFO매칭 — **전부 tick 카운트로 전이, 위치 없음**. 이동 단계는 **지속시간(`walkTicks`)** 만 가짐(플로어가 실제 경로에 매핑).
- **통화 = `double`**. `int` 금지. 포맷은 프레젠테이션에서 단위 변환(K/M/B/aa…).
- **결제**: 직원 FSM이 서빙 완료 tick 도달 → 코어가 `price×qty×배율` 가산 → `CashChanged` emit.
- **오프라인**: 복귀 시 **서버시간** 기준 `passiveRate × min(경과, 상한) × incomeRate` **closed-form**. 손님 재생 ❌. **전이 엔티티(손님/큐) 비저장** — 재구성 0, 현금만 공식 가산.
- **업그레이드(슬라이스 포함)**: `BuyUpgrade` Command → 코어가 현금 차감 + 레이트 재계산 + 이벤트. → Command 경로·재계산·세이브 왕복을 *실제로* 검증.
- **결정성**: 시드 RNG(옛 `IRandomProvider` 이식 — 드디어 테스트에서 사용).
- **수식**: 옛 `Formulas.cs` 검증분 이식.

---

## 5. 라이브 플로어 (Floor)

- **2D 탑다운**, NavMesh 미적용 — **단순 waypoint/steering**(옛 `SceneCoords`/anchor 재활용).
- 코어의 논리 상태를 읽어 **연출만**: "손님3 = 좌석2 배정, Walking(`walkTicks`)" → 아이콘을 좌석2 anchor로 `walkTicks` 동안 이동. 위치·bob·애니는 **전부 여기**.
- **코어를 gate 안 함** — 도착 여부로 로직 전이 안 시킴(코어가 이미 tick으로 안다). 플로어는 코어에 **Command만** 보냄(탭 등).
- **손님 풀링**: 테마파크 `PoolManager` 동형(2D 다량 스폰).
- **포그라운드 전용**. 백그라운드/오프라인엔 멈춤.

---

## 6. 프레젠테이션 (UGUI, 테마파크 동형)

- 인프라 100% 테마파크: `EventMonoBehaviour`(pub/sub) + `UIHandler`(Addressable 팝업+스택).
- HUD: `CashChanged` 구독 → 뷰가 **단위포맷**(`$1.2M`) 갱신 → 숫자팝(DamageNumbersPro/DOTween).
- 업그레이드 버튼: 탭 → `BuyUpgrade` Command 발행.

---

## 7. DI / 데이터 / 세이브 (테마파크 동형)

- **DI**: Service Locator(`SystemManager` 정적 + `SingletonBehaviour<T>`). **코어만 예외**(생성자 주입).
- **데이터**: UGS(Sheets→코드생성). 메뉴(조리시간·가격·등장가중·수량범위)가 첫 시트. **단 Phase1은 주입된 plain config(테스트 데이터)로 — UGS가 Phase1을 막지 않음**(생성자 주입이 공짜로 해결).
- **세이브**: ES3 로컬 + PlayFab 클라우드 + **ObscuredTypes(현금 등 암호화)** + 타임스탬프 충돌 해결. **서버시간**으로 오프라인 계산.

---

## 8. 테스트 (`Game.Tests`) — 이제 *버그나는 안무*를 테스트

| 테스트 | 증명 |
|---|---|
| 결정성 | 같은 시드+tick 수 → 두 번 실행 일치 |
| 안무 정합성 | 직원 FIFO 매칭·좌석 배정·큐 순서가 기대대로(옛 "빈 좌석 응대" 류 회귀 차단) |
| 조리 타이밍 | qty N → 조리 tick = N×cookTicks, 서빙 tick 정확 |
| 결제 정합성 | 손님 1명 완주 → 현금 `price×qty×배율` (double) |
| 업그레이드 | BuyUpgrade → 현금 차감 + 레이트 재계산 정확 |
| 오프라인 | closed-form = 기대 공식값(상한 클램프, 서버시간 델타) |
| 불변 규칙 | 코어가 씬/`SystemManager` 없이 컴파일·실행(asmdef 강제) |

---

## 9. 빌드 순서

| Phase | 내용 | Done |
|---|---|---|
| 1 | 코어(논리 FSM + 이코노미 + 업그레이드 + 오프라인) + 테스트, **주입 config**, Unity 0줄 | 헤드리스 테스트 전부 통과 |
| 2 | App 부트스트랩(SystemManager) + 틱 드라이버 + 코어 조립 | Play 시 코어 작동(로그) |
| 3 | HUD(EventMonoBehaviour) 현금 단위포맷 + 업그레이드 버튼(Command) | 표시 + 숫자팝 + 업그레이드 동작 |
| 4 | 2D 플로어(손님 이동·조리 연출, **풀링**) | 코어 논리→아이콘 연출 일치, 5배속에도 안 깨짐 |
| 5 | 세이브(ES3+PlayFab, 서버시간) + UGS 데이터 + 주스 | 재진입 보존 + 오프라인 보상 + 피드백 |

---

## 10. 이식 자산 (옛 Idle-Restaurant → 신규)

- `Formulas.cs` 수식 → `Game.Core/Economy`
- 손님 8단계 / 직원 9상태 FSM 분해 → 코어 enum FSM (이번엔 위치 없이 tick)
- 튜닝 숫자값(가격/조리/수량범위) → UGS 메뉴 시트
- `IRandomProvider` 시드 RNG → 코어 + 테스트
- 2D anchor/`SceneCoords` → 플로어 경로

---

## 11. 리스크

| 리스크 | 완화 |
|---|---|
| 코어/플로어 경계 흐려져 옛 `VisuallyAtSeat` 재발 | **불변규칙(§2)을 asmdef + 테스트로 강제** (특히 §8 안무 테스트) |
| DI/데이터를 테마파크와 다르게 | Service Locator + UGS 동형 확정(§0) |
| 통화 `int` 실수 재발 | `double` 확정 + 테스트가 큰 수 검증 |
| 오프라인 시계 조작 | 서버/PlayFab 시간 확정 |
| 2D 이동 과설계 | NavMesh 미도입, 단순 waypoint부터 |

---

## 12. 구현 전 확인 (Plan 단계, 테마파크 실코드 1:1 참조)

- `YYHLib/System/SystemManager.cs` — Service Locator 셋업
- `YYHLib/System/Event/EventMonobehaviour.cs` — pub/sub 등록
- `YYHLib/UGS/` + `UGS.Generated/` — Sheets→코드생성 신규 시트 추가법
- `_Project/02.Scripts/Manager/IdleRewardManager.cs` — closed-form + 시간원
- `_Project/02.Scripts/Business/BusinessFSM/` — enum FSM 형태
- `YYHLib/System/UserData/UserDataManager.cs` — ES3+PlayFab 세이브 + ObscuredTypes
- `YYHLib/Game/PoolManager.cs` (또는 상응) — 풀링 셋업
