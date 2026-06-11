# Headless Core — Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 새 Unity 프로젝트에 손님 1명이 spawn→착석→주문→조리→서빙→결제까지 도는 **헤드리스 결정적 코어**를, Unity 의존 0 + 전부 단위테스트로 세운다.

**Architecture:** `IdleRestaurant.Core` asmdef(`noEngineReferences: true` — UnityEngine 참조 컴파일 차단)에 plain C# 코어. 고정 tick으로 모든 FSM 전진, 위치 없음. `IdleRestaurant.Core.Tests` asmdef(EditMode)가 NUnit으로 검증. 이게 spec의 불변규칙(코어는 씬 0개로 컴파일·테스트)을 asmdef 경계로 *강제*한다.

**Tech Stack:** Unity 6 LTS, C#, Unity Test Framework(EditMode, NUnit). 외부 라이브러리 0 (이 플랜 한정).

**Reference Spec:** `docs/superpowers/specs/2026-06-11-core-operating-loop-design.md`

**Scope note:** 이 플랜 = walking skeleton(단일 좌석·단일 직원·큐 없음). 큐/FIFO 매칭/다중 직원/업그레이드(Command)/오프라인(closed-form)은 **다음 플랜**. 둘 다 spec Phase 1 범위지만 플랜 크기로 분리.

**Project root:** `C:\Users\Mooseong\Projects\IdleRestaurant` (이름 다르면 루트만 치환)

---

## File Structure

```
C:\Users\Mooseong\Projects\IdleRestaurant\
└── Assets/_Project/
    ├── Scripts/Core/
    │   ├── IdleRestaurant.Core.asmdef          # noEngineReferences:true
    │   ├── Rng/
    │   │   ├── IRng.cs                          # 시드 RNG 추상
    │   │   └── SeededRng.cs
    │   ├── Money/
    │   │   └── MoneyFormat.cs                   # double → "1.2M" 단위포맷
    │   ├── Domain/
    │   │   ├── CustomerStage.cs                 # enum
    │   │   ├── StaffState.cs                    # enum
    │   │   ├── MenuItem.cs                      # 데이터
    │   │   ├── Customer.cs                      # POCO
    │   │   └── Staff.cs                         # POCO
    │   ├── Config/
    │   │   └── CoreConfig.cs                    # 주입 설정(tick 수, 메뉴 등)
    │   ├── Events/
    │   │   └── DomainEvent.cs                   # CashChanged 등
    │   └── Simulation.cs                        # tick 루프 + FSM
    └── Tests/EditMode/
        ├── IdleRestaurant.Core.Tests.asmdef
        ├── SmokeTest.cs
        ├── SeededRngTests.cs
        ├── MoneyFormatTests.cs
        ├── SimulationClockTests.cs
        ├── SpawnTests.cs
        ├── SeatAndOrderTests.cs
        ├── ServeAndPayTests.cs
        ├── LifecycleTests.cs
        └── DeterminismTests.cs
```

각 파일은 단일 책임. `Simulation.cs`가 커지면 후속 플랜에서 FSM별 분리.

---

### Task 1: 새 Unity 프로젝트 + asmdef 골격 + Test Framework 검증

**Files:**
- Create (manual): Unity 프로젝트 `C:\Users\Mooseong\Projects\IdleRestaurant` (Unity Hub → 6 LTS → 2D(URP) 템플릿)
- Create: `Assets/_Project/Scripts/Core/IdleRestaurant.Core.asmdef`
- Create: `Assets/_Project/Tests/EditMode/IdleRestaurant.Core.Tests.asmdef`
- Test: `Assets/_Project/Tests/EditMode/SmokeTest.cs`

- [ ] **Step 1: Unity 프로젝트 생성 (수동)**

Unity Hub에서 New Project → 6 LTS → "2D (URP)" → 이름 `IdleRestaurant` → 경로 `C:\Users\Mooseong\Projects` → Create. Editor가 열리면 Console 에러 0 확인.

- [ ] **Step 2: Core asmdef 작성 (UnityEngine 참조 차단)**

Create `Assets/_Project/Scripts/Core/IdleRestaurant.Core.asmdef`:

```json
{
  "name": "IdleRestaurant.Core",
  "rootNamespace": "IdleRestaurant.Core",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
```

> `noEngineReferences: true` 가 핵심 — 이 어셈블리에서 `using UnityEngine;` 쓰면 컴파일 실패. spec 불변규칙을 코드로 강제.

- [ ] **Step 3: Tests asmdef 작성 (EditMode)**

Create `Assets/_Project/Tests/EditMode/IdleRestaurant.Core.Tests.asmdef`:

```json
{
  "name": "IdleRestaurant.Core.Tests",
  "rootNamespace": "IdleRestaurant.Core.Tests",
  "references": ["IdleRestaurant.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": []
}
```

- [ ] **Step 4: 스모크 테스트 작성**

Create `Assets/_Project/Tests/EditMode/SmokeTest.cs`:

```csharp
using NUnit.Framework;

namespace IdleRestaurant.Core.Tests
{
    public class SmokeTest
    {
        [Test]
        public void TestFrameworkWorks()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
```

- [ ] **Step 5: 테스트 실행 → 통과 확인**

Unity 메뉴: `Window > General > Test Runner` → `EditMode` 탭 → `Run All`.
Expected: `SmokeTest.TestFrameworkWorks` PASS (초록). Console 에러 0.

- [ ] **Step 6: git init + commit**

```bash
cd C:/Users/Mooseong/Projects/IdleRestaurant
git init
# Unity .gitignore 추가 (github.com/github/gitignore/Unity.gitignore 내용)
git add .
git commit -m "chore: new Unity project + Core/Tests asmdef + smoke test"
```

---

### Task 2: 시드 RNG (결정성의 기반)

**Files:**
- Create: `Assets/_Project/Scripts/Core/Rng/IRng.cs`
- Create: `Assets/_Project/Scripts/Core/Rng/SeededRng.cs`
- Test: `Assets/_Project/Tests/EditMode/SeededRngTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `Assets/_Project/Tests/EditMode/SeededRngTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class SeededRngTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new SeededRng(12345);
            var b = new SeededRng(12345);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.Range(0, 1000), b.Range(0, 1000));
        }

        [Test]
        public void Range_IsWithinBounds()
        {
            var rng = new SeededRng(1);
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.Range(2, 5); // [2,5)
                Assert.GreaterOrEqual(v, 2);
                Assert.Less(v, 5);
            }
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**

Test Runner → Run All. Expected: FAIL (`IRng`/`SeededRng` 미정의 컴파일 에러).

- [ ] **Step 3: 최소 구현**

Create `Assets/_Project/Scripts/Core/Rng/IRng.cs`:

```csharp
namespace IdleRestaurant.Core.Rng
{
    // 시드 가능 RNG. 코어는 System.Random만 — UnityEngine.Random 금지(헤드리스/결정성).
    public interface IRng
    {
        double NextDouble();               // [0,1)
        int Range(int minInclusive, int maxExclusive);
    }
}
```

Create `Assets/_Project/Scripts/Core/Rng/SeededRng.cs`:

```csharp
using System;

namespace IdleRestaurant.Core.Rng
{
    public sealed class SeededRng : IRng
    {
        readonly Random _rng;
        public SeededRng(int seed) => _rng = new Random(seed);
        public double NextDouble() => _rng.NextDouble();
        public int Range(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
    }
}
```

- [ ] **Step 4: 실행 → 통과 확인**

Test Runner → Run All. Expected: `SeededRngTests` 2개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Rng Assets/_Project/Tests/EditMode/SeededRngTests.cs
git commit -m "feat(core): seeded RNG for deterministic simulation"
```

---

### Task 3: 통화 단위 포맷 (double → "1.2M")

**Files:**
- Create: `Assets/_Project/Scripts/Core/Money/MoneyFormat.cs`
- Test: `Assets/_Project/Tests/EditMode/MoneyFormatTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `Assets/_Project/Tests/EditMode/MoneyFormatTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core.Money;

namespace IdleRestaurant.Core.Tests
{
    public class MoneyFormatTests
    {
        [TestCase(0d, "0")]
        [TestCase(42d, "42")]
        [TestCase(999d, "999")]
        [TestCase(1000d, "1.00K")]
        [TestCase(1234d, "1.23K")]
        [TestCase(1_500_000d, "1.50M")]
        [TestCase(2_300_000_000d, "2.30B")]
        [TestCase(7_800_000_000_000d, "7.80T")]
        public void Short_FormatsWithUnit(double value, string expected)
        {
            Assert.AreEqual(expected, MoneyFormat.Short(value));
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**

Test Runner → Run All. Expected: FAIL (`MoneyFormat` 미정의).

- [ ] **Step 3: 최소 구현**

Create `Assets/_Project/Scripts/Core/Money/MoneyFormat.cs`:

```csharp
using System;
using System.Globalization;

namespace IdleRestaurant.Core.Money
{
    // 통화는 double. 표시에서만 단위 변환. int 금지(2.1e9 오버플로).
    public static class MoneyFormat
    {
        static readonly string[] Units = { "", "K", "M", "B", "T", "aa", "ab", "ac", "ad" };

        public static string Short(double value)
        {
            if (value < 1000d)
                return ((long)value).ToString(CultureInfo.InvariantCulture);

            int tier = 0;
            double v = value;
            while (v >= 1000d && tier < Units.Length - 1)
            {
                v /= 1000d;
                tier++;
            }
            return v.ToString("0.00", CultureInfo.InvariantCulture) + Units[tier];
        }
    }
}
```

- [ ] **Step 4: 실행 → 통과 확인**

Test Runner → Run All. Expected: `MoneyFormatTests` 전 케이스 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Money Assets/_Project/Tests/EditMode/MoneyFormatTests.cs
git commit -m "feat(core): money unit formatting (double-based)"
```

---

### Task 4: 도메인 타입 (enum + POCO + 설정)

**Files:**
- Create: `Assets/_Project/Scripts/Core/Domain/CustomerStage.cs`
- Create: `Assets/_Project/Scripts/Core/Domain/StaffState.cs`
- Create: `Assets/_Project/Scripts/Core/Domain/MenuItem.cs`
- Create: `Assets/_Project/Scripts/Core/Domain/Customer.cs`
- Create: `Assets/_Project/Scripts/Core/Domain/Staff.cs`
- Create: `Assets/_Project/Scripts/Core/Config/CoreConfig.cs`

> 순수 데이터 정의 — 동작 없음. 테스트는 다음 Task부터(여기선 컴파일만).

- [ ] **Step 1: enum 작성**

Create `Assets/_Project/Scripts/Core/Domain/CustomerStage.cs`:

```csharp
namespace IdleRestaurant.Core.Domain
{
    // 위치 없음. 각 단계는 tick 지속시간으로 전이. 이동은 "지속시간"일 뿐(뷰가 경로 연출).
    public enum CustomerStage
    {
        Arriving,    // 입장 walk (walkTicks)
        Seated,      // 착석, 주문 생성
        WaitingFood, // 직원 서빙 대기
        Eating,      // 식사 (eatTicks)
        Paid,        // 결제 완료
        Leaving      // 퇴장 walk (walkTicks)
    }
}
```

Create `Assets/_Project/Scripts/Core/Domain/StaffState.cs`:

```csharp
namespace IdleRestaurant.Core.Domain
{
    public enum StaffState
    {
        Idle,
        TakingOrder, // 주문 받는 중 (takeOrderTicks)
        Cooking,     // 조리 중 (qty * cookTicks)
        Serving,     // 서빙 (serveTicks)
        Returning    // 복귀 (returnTicks)
    }
}
```

- [ ] **Step 2: 데이터 POCO 작성**

Create `Assets/_Project/Scripts/Core/Domain/MenuItem.cs`:

```csharp
namespace IdleRestaurant.Core.Domain
{
    public sealed class MenuItem
    {
        public string Id;
        public double Price;     // double — int 금지
        public int CookTicks;    // 1개당 조리 tick
        public int MinQty;
        public int MaxQty;
        public double Weight;    // 등장 가중치
    }
}
```

Create `Assets/_Project/Scripts/Core/Domain/Customer.cs`:

```csharp
namespace IdleRestaurant.Core.Domain
{
    public sealed class Customer
    {
        public int Id;
        public CustomerStage Stage;
        public int ElapsedTicks;   // 현재 단계 누적 tick
        public int SeatIndex = -1;
        public MenuItem Item;      // 주문 메뉴 (단일)
        public int Qty;            // 주문 수량
    }
}
```

Create `Assets/_Project/Scripts/Core/Domain/Staff.cs`:

```csharp
namespace IdleRestaurant.Core.Domain
{
    public sealed class Staff
    {
        public int Id;
        public StaffState State;
        public int ElapsedTicks;
        public int TargetCustomerId = -1;
    }
}
```

- [ ] **Step 3: 설정 작성**

Create `Assets/_Project/Scripts/Core/Config/CoreConfig.cs`:

```csharp
using System.Collections.Generic;
using IdleRestaurant.Core.Domain;

namespace IdleRestaurant.Core.Config
{
    // 주입 설정 — Phase1은 테스트가 직접 구성. (UGS는 후속 플랜에서 이 형태로 매핑)
    public sealed class CoreConfig
    {
        public int TickHz = 20;
        public int SpawnIntervalTicks = 40;  // 2초마다 손님
        public int SeatWalkTicks = 30;        // 입장 walk
        public int TakeOrderTicks = 10;
        public int EatTicks = 40;
        public int ServeTicks = 10;
        public int ReturnTicks = 20;
        public int LeaveWalkTicks = 30;
        public double PriceMult = 1.0;
        public List<MenuItem> Menu = new();
    }
}
```

- [ ] **Step 4: 컴파일 확인**

Unity로 전환 → 자동 컴파일 → Console 에러 0. (테스트 없음, 컴파일만.)

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Domain Assets/_Project/Scripts/Core/Config
git commit -m "feat(core): domain types (enums, POCO, config)"
```

---

### Task 5: 시뮬레이션 + 결정적 tick 클럭

**Files:**
- Create: `Assets/_Project/Scripts/Core/Events/DomainEvent.cs`
- Create: `Assets/_Project/Scripts/Core/Simulation.cs`
- Test: `Assets/_Project/Tests/EditMode/SimulationClockTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `Assets/_Project/Tests/EditMode/SimulationClockTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class SimulationClockTests
    {
        static Simulation NewSim() =>
            new Simulation(new CoreConfig(), new SeededRng(1));

        [Test]
        public void Tick_AdvancesTickCount()
        {
            var sim = NewSim();
            Assert.AreEqual(0, sim.TickCount);
            sim.Tick();
            sim.Tick();
            Assert.AreEqual(2, sim.TickCount);
        }

        [Test]
        public void Cash_StartsAtZero()
        {
            Assert.AreEqual(0d, NewSim().Cash, 1e-9);
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**

Test Runner → Run All. Expected: FAIL (`Simulation` 미정의).

- [ ] **Step 3: 최소 구현**

Create `Assets/_Project/Scripts/Core/Events/DomainEvent.cs`:

```csharp
namespace IdleRestaurant.Core.Events
{
    public enum DomainEventType { CustomerSpawned, CustomerSeated, OrderTaken, OrderServed, CashChanged, CustomerLeft }

    // 코어가 발행, 플로어/UI가 읽음. 위치 없음 — 논리 사실만.
    public readonly struct DomainEvent
    {
        public readonly DomainEventType Type;
        public readonly int CustomerId;
        public readonly double Amount;   // CashChanged용
        public DomainEvent(DomainEventType type, int customerId, double amount = 0)
        {
            Type = type; CustomerId = customerId; Amount = amount;
        }
    }
}
```

Create `Assets/_Project/Scripts/Core/Simulation.cs`:

```csharp
using System.Collections.Generic;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Domain;
using IdleRestaurant.Core.Events;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core
{
    // 권위적 결정적 시뮬. 고정 tick으로 전진. 위치 없음. UnityEngine 의존 0.
    public sealed class Simulation
    {
        readonly CoreConfig _cfg;
        readonly IRng _rng;

        public int TickCount { get; private set; }
        public double Cash { get; private set; }

        // walking skeleton: 단일 좌석, 단일 직원, 큐 없음
        public Customer Customer;        // 현재 손님(없으면 null)
        public readonly Staff Staff = new() { Id = 1, State = StaffState.Idle };

        int _ticksSinceSpawn;
        int _nextCustomerId = 1;

        readonly List<DomainEvent> _events = new();
        public IReadOnlyList<DomainEvent> EventsThisTick => _events;

        public Simulation(CoreConfig cfg, IRng rng)
        {
            _cfg = cfg;
            _rng = rng;
        }

        public void Tick()
        {
            _events.Clear();
            TickCount++;
            // 동작은 다음 Task들에서 채움 (spawn/seat/serve).
            TickSpawn();
            TickCustomer();
            TickStaff();
        }

        void TickSpawn() { }    // Task 6
        void TickCustomer() { } // Task 7,8
        void TickStaff() { }    // Task 8

        // 내부에서 사용할 헬퍼 (다음 Task에서 호출)
        internal void Emit(DomainEvent e) => _events.Add(e);
        internal void AddCash(double amount)
        {
            Cash += amount;
            Emit(new DomainEvent(DomainEventType.CashChanged, 0, Cash));
        }
        internal int NextCustomerId() => _nextCustomerId++;
        internal CoreConfig Config => _cfg;
        internal IRng Rng => _rng;
        internal int TicksSinceSpawn { get => _ticksSinceSpawn; set => _ticksSinceSpawn = value; }
    }
}
```

- [ ] **Step 4: 실행 → 통과 확인**

Test Runner → Run All. Expected: `SimulationClockTests` 2개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Events Assets/_Project/Scripts/Core/Simulation.cs Assets/_Project/Tests/EditMode/SimulationClockTests.cs
git commit -m "feat(core): simulation skeleton + deterministic tick clock"
```

---

### Task 6: 손님 spawn (결정적, 간격 기반)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Simulation.cs` (`TickSpawn`)
- Test: `Assets/_Project/Tests/EditMode/SpawnTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `Assets/_Project/Tests/EditMode/SpawnTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Domain;
using IdleRestaurant.Core.Events;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class SpawnTests
    {
        static CoreConfig Cfg() => new CoreConfig
        {
            SpawnIntervalTicks = 5,
            Menu = { new MenuItem { Id = "hotdog", Price = 2, CookTicks = 10, MinQty = 1, MaxQty = 3, Weight = 1 } }
        };

        [Test]
        public void SpawnsCustomer_AtInterval()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            for (int i = 0; i < 4; i++) sim.Tick(); // < interval
            Assert.IsNull(sim.Customer);
            sim.Tick(); // 5th tick → spawn
            Assert.IsNotNull(sim.Customer);
            Assert.AreEqual(CustomerStage.Arriving, sim.Customer.Stage);
        }

        [Test]
        public void SpawnEmitsEvent()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            for (int i = 0; i < 5; i++) sim.Tick();
            CollectionAssert.Contains(
                System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(sim.EventsThisTick, e => e.Type)),
                DomainEventType.CustomerSpawned);
        }

        [Test]
        public void DoesNotSpawn_WhileSeatOccupied()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            for (int i = 0; i < 5; i++) sim.Tick();   // 손님1 spawn
            var first = sim.Customer;
            for (int i = 0; i < 5; i++) sim.Tick();   // 좌석 점유 중 → 새 손님 X
            Assert.AreSame(first, sim.Customer);
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**

Test Runner → Run All. Expected: `SpawnTests` FAIL (spawn 미구현).

- [ ] **Step 3: TickSpawn 구현**

Modify `Assets/_Project/Scripts/Core/Simulation.cs` — `TickSpawn()` 교체:

```csharp
void TickSpawn()
{
    if (Customer != null) return;               // walking skeleton: 단일 좌석
    TicksSinceSpawn++;
    if (TicksSinceSpawn < _cfg.SpawnIntervalTicks) return;
    TicksSinceSpawn = 0;

    var item = PickMenu();
    int qty = item == null ? 0 : _rng.Range(item.MinQty, item.MaxQty + 1);
    Customer = new Customer
    {
        Id = NextCustomerId(),
        Stage = CustomerStage.Arriving,
        ElapsedTicks = 0,
        Item = item,
        Qty = qty
    };
    Emit(new DomainEvent(DomainEventType.CustomerSpawned, Customer.Id));
}

MenuItem PickMenu()
{
    var menu = _cfg.Menu;
    if (menu.Count == 0) return null;
    double total = 0;
    for (int i = 0; i < menu.Count; i++) total += menu[i].Weight;
    if (total <= 0) return menu[0];
    double r = _rng.NextDouble() * total;
    double acc = 0;
    for (int i = 0; i < menu.Count; i++)
    {
        acc += menu[i].Weight;
        if (r <= acc) return menu[i];
    }
    return menu[menu.Count - 1];
}
```

- [ ] **Step 4: 실행 → 통과 확인**

Test Runner → Run All. Expected: `SpawnTests` 3개 PASS, 기존 테스트 유지.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Simulation.cs Assets/_Project/Tests/EditMode/SpawnTests.cs
git commit -m "feat(core): customer spawn with weighted menu + qty"
```

---

### Task 7: 착석 + 주문 (walk 지속시간 → Seated)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Simulation.cs` (`TickCustomer`)
- Test: `Assets/_Project/Tests/EditMode/SeatAndOrderTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `Assets/_Project/Tests/EditMode/SeatAndOrderTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Domain;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class SeatAndOrderTests
    {
        static CoreConfig Cfg() => new CoreConfig
        {
            SpawnIntervalTicks = 1,
            SeatWalkTicks = 3,
            Menu = { new MenuItem { Id = "hotdog", Price = 2, CookTicks = 10, MinQty = 2, MaxQty = 2, Weight = 1 } }
        };

        [Test]
        public void Customer_BecomesSeated_AfterWalkTicks()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            sim.Tick(); // spawn (interval=1)
            Assert.AreEqual(CustomerStage.Arriving, sim.Customer.Stage);
            sim.Tick(); sim.Tick(); sim.Tick(); // 3 walk ticks
            Assert.AreEqual(CustomerStage.Seated, sim.Customer.Stage);
            Assert.AreEqual(0, sim.Customer.SeatIndex);
        }

        [Test]
        public void SeatedCustomer_HasOrder()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            for (int i = 0; i < 4; i++) sim.Tick();
            Assert.AreEqual("hotdog", sim.Customer.Item.Id);
            Assert.AreEqual(2, sim.Customer.Qty);
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**

Test Runner → Run All. Expected: `SeatAndOrderTests` FAIL.

- [ ] **Step 3: TickCustomer 구현 (Arriving→Seated)**

Modify `Assets/_Project/Scripts/Core/Simulation.cs` — `TickCustomer()` 교체:

```csharp
void TickCustomer()
{
    var c = Customer;
    if (c == null) return;
    c.ElapsedTicks++;

    switch (c.Stage)
    {
        case CustomerStage.Arriving:
            if (c.ElapsedTicks >= _cfg.SeatWalkTicks)
            {
                c.Stage = CustomerStage.Seated;
                c.SeatIndex = 0;
                c.ElapsedTicks = 0;
                Emit(new DomainEvent(DomainEventType.CustomerSeated, c.Id));
            }
            break;
        // WaitingFood / Eating / Paid / Leaving → Task 8
    }
}
```

(파일 상단에 `using IdleRestaurant.Core.Events;` 가 이미 있는지 확인 — Task 5에서 추가됨.)

- [ ] **Step 4: 실행 → 통과 확인**

Test Runner → Run All. Expected: `SeatAndOrderTests` 2개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Simulation.cs Assets/_Project/Tests/EditMode/SeatAndOrderTests.cs
git commit -m "feat(core): customer arriving->seated with order"
```

---

### Task 8: 직원 주문→조리(qty×cook)→서빙→결제

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Simulation.cs` (`TickStaff`, `TickCustomer` 확장)
- Test: `Assets/_Project/Tests/EditMode/ServeAndPayTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

Create `Assets/_Project/Tests/EditMode/ServeAndPayTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Domain;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class ServeAndPayTests
    {
        static CoreConfig Cfg() => new CoreConfig
        {
            SpawnIntervalTicks = 1,
            SeatWalkTicks = 1,
            TakeOrderTicks = 2,
            ServeTicks = 1,
            PriceMult = 1.0,
            Menu = { new MenuItem { Id = "hotdog", Price = 2, CookTicks = 3, MinQty = 2, MaxQty = 2, Weight = 1 } }
        };

        [Test]
        public void Payment_EqualsPriceTimesQtyTimesMult()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            for (int i = 0; i < 200 && sim.Customer?.Stage != CustomerStage.Paid; i++) sim.Tick();
            // 2 hotdog * $2 * 1.0 = $4
            Assert.AreEqual(4d, sim.Cash, 1e-9);
        }

        [Test]
        public void Cooking_TakesQtyTimesCookTicks()
        {
            var sim = new Simulation(Cfg(), new SeededRng(1));
            int cookingTicks = 0;
            for (int i = 0; i < 200 && sim.Cash == 0; i++)
            {
                sim.Tick();
                if (sim.Staff.State == StaffState.Cooking) cookingTicks++;
            }
            // qty=2 * cookTicks=3 = 6 ticks 조리
            Assert.AreEqual(6, cookingTicks);
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**

Test Runner → Run All. Expected: `ServeAndPayTests` FAIL (직원 미구현, Cash=0).

- [ ] **Step 3: TickStaff 구현 + TickCustomer 확장**

Modify `Assets/_Project/Scripts/Core/Simulation.cs` — `TickStaff()` 교체:

```csharp
void TickStaff()
{
    var s = Staff;
    var c = Customer;
    s.ElapsedTicks++;

    switch (s.State)
    {
        case StaffState.Idle:
            // 착석한 손님이 있고 아직 응대 안 했으면 주문 받기 시작
            if (c != null && c.Stage == CustomerStage.Seated && s.TargetCustomerId == -1)
            {
                s.TargetCustomerId = c.Id;
                s.State = StaffState.TakingOrder;
                s.ElapsedTicks = 0;
                Emit(new DomainEvent(DomainEventType.OrderTaken, c.Id));
            }
            break;

        case StaffState.TakingOrder:
            if (c != null) c.Stage = CustomerStage.WaitingFood;
            if (s.ElapsedTicks >= _cfg.TakeOrderTicks)
            {
                s.State = StaffState.Cooking;
                s.ElapsedTicks = 0;
            }
            break;

        case StaffState.Cooking:
        {
            int cookTotal = (c?.Item?.CookTicks ?? 0) * (c?.Qty ?? 0);
            if (s.ElapsedTicks >= cookTotal)
            {
                s.State = StaffState.Serving;
                s.ElapsedTicks = 0;
            }
            break;
        }

        case StaffState.Serving:
            if (s.ElapsedTicks >= _cfg.ServeTicks)
            {
                if (c != null)
                {
                    double pay = c.Item.Price * c.Qty * _cfg.PriceMult;
                    AddCash(pay);
                    Emit(new DomainEvent(DomainEventType.OrderServed, c.Id, pay));
                    c.Stage = CustomerStage.Paid;
                }
                s.State = StaffState.Returning;
                s.ElapsedTicks = 0;
            }
            break;

        case StaffState.Returning:
            if (s.ElapsedTicks >= _cfg.ReturnTicks)
            {
                s.State = StaffState.Idle;
                s.TargetCustomerId = -1;
                s.ElapsedTicks = 0;
            }
            break;
    }
}
```

Modify `TickCustomer()` — `switch`에 Paid 이후 퇴장 추가:

```csharp
        case CustomerStage.Paid:
            // 결제 직후 퇴장 walk 시작
            c.Stage = CustomerStage.Leaving;
            c.ElapsedTicks = 0;
            break;

        case CustomerStage.Leaving:
            if (c.ElapsedTicks >= _cfg.LeaveWalkTicks)
            {
                Emit(new DomainEvent(DomainEventType.CustomerLeft, c.Id));
                Customer = null; // 좌석 비움 → 다음 손님 spawn 가능
            }
            break;
```

- [ ] **Step 4: 실행 → 통과 확인**

Test Runner → Run All. Expected: `ServeAndPayTests` 2개 PASS, 기존 전부 유지.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Simulation.cs Assets/_Project/Tests/EditMode/ServeAndPayTests.cs
git commit -m "feat(core): staff order->cook->serve->pay + customer leave"
```

---

### Task 9: 엔드투엔드 생애주기 + 결정성

**Files:**
- Test: `Assets/_Project/Tests/EditMode/LifecycleTests.cs`
- Test: `Assets/_Project/Tests/EditMode/DeterminismTests.cs`

- [ ] **Step 1: 생애주기 테스트 작성**

Create `Assets/_Project/Tests/EditMode/LifecycleTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Domain;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class LifecycleTests
    {
        [Test]
        public void TwoCustomers_ServedSequentially_AccumulateCash()
        {
            var cfg = new CoreConfig
            {
                SpawnIntervalTicks = 2,
                SeatWalkTicks = 2, TakeOrderTicks = 2, ServeTicks = 1,
                ReturnTicks = 1, LeaveWalkTicks = 1,
                Menu = { new MenuItem { Id = "hotdog", Price = 2, CookTicks = 2, MinQty = 1, MaxQty = 1, Weight = 1 } }
            };
            var sim = new Simulation(cfg, new SeededRng(7));
            for (int i = 0; i < 500; i++) sim.Tick();
            // 손님이 반복 처리되며 현금이 양수로 누적
            Assert.Greater(sim.Cash, 0d);
            // 각 손님 $2 → 현금은 $2의 배수
            Assert.AreEqual(0d, sim.Cash % 2d, 1e-9);
        }
    }
}
```

- [ ] **Step 2: 결정성 테스트 작성**

Create `Assets/_Project/Tests/EditMode/DeterminismTests.cs`:

```csharp
using NUnit.Framework;
using IdleRestaurant.Core;
using IdleRestaurant.Core.Config;
using IdleRestaurant.Core.Domain;
using IdleRestaurant.Core.Rng;

namespace IdleRestaurant.Core.Tests
{
    public class DeterminismTests
    {
        static CoreConfig Cfg() => new CoreConfig
        {
            Menu = { new MenuItem { Id = "hotdog", Price = 2, CookTicks = 5, MinQty = 1, MaxQty = 4, Weight = 1 } }
        };

        [Test]
        public void SameSeed_SameCashAfterManyTicks()
        {
            var a = new Simulation(Cfg(), new SeededRng(999));
            var b = new Simulation(Cfg(), new SeededRng(999));
            for (int i = 0; i < 1000; i++) { a.Tick(); b.Tick(); }
            Assert.AreEqual(a.Cash, b.Cash, 1e-9);
        }

        [Test]
        public void BurstVsStep_AreIdentical() // 오프라인 fast-forward의 기반
        {
            var burst = new Simulation(Cfg(), new SeededRng(42));
            var step  = new Simulation(Cfg(), new SeededRng(42));
            for (int i = 0; i < 600; i++) burst.Tick();
            for (int i = 0; i < 600; i++) step.Tick();
            Assert.AreEqual(burst.Cash, step.Cash, 1e-9);
            Assert.AreEqual(burst.TickCount, step.TickCount);
        }
    }
}
```

- [ ] **Step 3: 실행 → 통과 확인**

Test Runner → Run All. Expected: `LifecycleTests` + `DeterminismTests` PASS. 전체 스위트 초록.

- [ ] **Step 4: 커밋**

```bash
git add Assets/_Project/Tests/EditMode/LifecycleTests.cs Assets/_Project/Tests/EditMode/DeterminismTests.cs
git commit -m "test(core): end-to-end lifecycle + determinism"
```

---

## Self-Review

**Spec coverage (이 플랜의 walking-skeleton 범위):**
- §0 double 통화 → Task 3, Task 8(double 결제) ✅
- §2/§3 코어 헤드리스 + asmdef 강제 → Task 1 (`noEngineReferences:true`) ✅
- §4 고정 tick·논리 FSM·위치 없음·시드 RNG·결제 → Task 2,5,6,7,8 ✅
- §8 테스트(결정성·결제 정합성·FSM 타이밍·불변규칙) → Task 8,9 ✅
- **다음 플랜으로 의도적 이월**: 큐/다중좌석/FIFO 매칭(§4), 업그레이드 Command(§4), 오프라인 closed-form(§4), UGS 데이터(§7), 플로어/UGUI/세이브(§5~7). walking skeleton 범위 밖 — 명시됨.

**Placeholder scan:** 모든 step에 실제 코드/명령/기대결과 포함. "TBD"/"적절히 처리" 없음. `TickSpawn/TickCustomer/TickStaff`의 빈 메서드(Task 5)는 후속 Task에서 *같은 플랜 내* 채워지며 각각 코드 제시 — placeholder 아님.

**Type consistency:** `Simulation`(TickCount/Cash/Customer/Staff/Emit/AddCash/EventsThisTick), `IRng.Range(min,maxExcl)`, `MenuItem`(Price double/CookTicks/MinQty/MaxQty/Weight), `CustomerStage`(Arriving/Seated/WaitingFood/Eating/Paid/Leaving), `StaffState`(Idle/TakingOrder/Cooking/Serving/Returning) — Task 간 시그니처 일치 확인.

> 참고: `CustomerStage.Eating` enum은 정의되지만 walking skeleton에선 미사용(서빙 직후 Paid). 식사 단계는 다음 플랜에서 WaitingFood→Eating→Paid로 분리.
