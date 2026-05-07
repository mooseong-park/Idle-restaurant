using System.Collections.Generic;
using UnityEngine;
using Project.Data;
using Project.Domain;

namespace Project.Visual
{
    // 씬 공간 좌표 registry. Hierarchy의 anchor GameObject들을 Awake에 자동 수집,
    // 게임 코드(CustomerView/StaffView/SceneController 등)는 SceneAnchors API 통해 좌표 조회.
    //
    // 핵심 원칙: Scene = 공간 데이터의 truth.
    //   - Hierarchy에서 Seat_2를 드래그 → SeatPos(2)는 즉시 새 좌표 반환 → 손님이 새 위치로 걸어감.
    //   - 코드에 좌표 상수 없음 (좌표 변경 시 코드 재컴파일 불필요).
    //
    // 의존성 주입: SceneAnchors는 SceneController가 [SerializeField]로 보유.
    // Customer/Staff View는 Bind() 인자로 받음 (singleton 회피).
    public sealed class SceneAnchors : MonoBehaviour
    {
        // ===== 인덱스 기반 anchor =====
        Transform[] seats;        // [seatIdx] → Transform
        Transform[] staffSlots;   // [slotIdx] → Transform
        Transform[] queueSlots;   // [queueIdx] → Transform

        // ===== 키 기반 anchor =====
        Dictionary<string, Transform> stationsByMenuId; // MenuItemSO.Id → Transform

        // ===== Singleton anchor =====
        Transform door;
        Transform doorOutside;
        Transform doorInside;
        Transform outsideSpawn;
        Transform topExit;
        Transform bar;

        void Awake()
        {
            BuildLookups();
        }

        // 런타임 도중 anchor가 추가/제거된 경우 호출 (현재는 SceneBuilder 후 1회만 사용 시나리오).
        public void BuildLookups()
        {
            BuildSeatLookup();
            BuildStaffSlotLookup();
            BuildQueueSlotLookup();
            BuildStationLookup();
            BuildSingletonLookups();
        }

        // ===== 공개 API =====

        public Vector3 SeatPos(int seatIdx)
        {
            if (seats == null || seats.Length == 0)
            {
                Debug.LogError("[SceneAnchors] seats 미수집. SceneAnchors.BuildLookups() 호출 확인.");
                return Vector3.zero;
            }
            int i = Mathf.Clamp(seatIdx, 0, seats.Length - 1);
            return seats[i] != null ? seats[i].position : Vector3.zero;
        }

        public int SeatCount => seats != null ? seats.Length : 0;

        public Vector3 StaffSlotPos(int slotIdx)
        {
            if (staffSlots == null || staffSlots.Length == 0) return Vector3.zero;
            int i = Mathf.Clamp(slotIdx, 0, staffSlots.Length - 1);
            return staffSlots[i] != null ? staffSlots[i].position : Vector3.zero;
        }

        public int StaffSlotCount => staffSlots != null ? staffSlots.Length : 0;

        // 큐 idx → Transform. 큐 슬롯 수보다 큰 idx는 마지막 슬롯에 묶임 (스폰 cap 단계에서 막히지만 안전망).
        public Vector3 QueuePos(int queueIdx)
        {
            if (queueSlots == null || queueSlots.Length == 0) return Vector3.zero;
            int i = Mathf.Clamp(queueIdx, 0, queueSlots.Length - 1);
            return queueSlots[i] != null ? queueSlots[i].position : Vector3.zero;
        }

        public Vector3 StationPos(MenuItemSO item)
        {
            if (item == null || stationsByMenuId == null)
            {
                Debug.LogWarning("[SceneAnchors] StationPos: item 또는 lookup null");
                return Vector3.zero;
            }
            if (stationsByMenuId.TryGetValue(item.Id, out var t) && t != null)
                return t.position;
            Debug.LogWarning($"[SceneAnchors] StationAnchor 못 찾음: id={item.Id}");
            return Vector3.zero;
        }

        // Singleton anchor 좌표 — anchor 누락 시 hardcoded fallback 반환 (Vector3.zero 회피).
        // Vector3.zero 는 게임 가운데(원점) 라서 손님이 거기서 spawn 되면 "가게 안에서 등장" 버그 발생.
        // 따라서 누락 시에도 외부/실내가 명확히 구분되는 fallback 위치 사용.
        //
        // 디자이너가 anchor를 옮기거나 일시적으로 누락해도 게임이 망가지지 않도록 안전망.
        public Vector3 DoorPos         => door         != null ? door.position         : new Vector3(2.5f, 1.6f, 0f);
        public Vector3 OutsideSpawnPos => outsideSpawn != null ? outsideSpawn.position : new Vector3(-6f, 3.5f, 0f);
        public Vector3 TopExitPos      => topExit      != null ? topExit.position      : new Vector3(3.5f, 6f, 0f);
        public Vector3 BarPos          => bar          != null ? bar.position          : new Vector3(0f, 0f, 0f);

        // Door corridor waypoints — 누락 시 DoorPos 기준 ±0.5 unit fallback (도어 통과 corridor 형성).
        public Vector3 DoorOutsidePos
        {
            get
            {
                if (doorOutside != null) return doorOutside.position;
                Vector3 d = DoorPos;
                return new Vector3(d.x, d.y + 0.5f, d.z); // 도어 위쪽 (외부)
            }
        }
        public Vector3 DoorInsidePos
        {
            get
            {
                if (doorInside != null) return doorInside.position;
                Vector3 d = DoorPos;
                return new Vector3(d.x, d.y - 0.5f, d.z); // 도어 아래쪽 (실내)
            }
        }
        public bool HasDoorWaypoints   => doorOutside  != null && doorInside != null;

        // 직원 서빙 위치 — 좌석 x + 바 y. 좌석 수직 라인을 따라 바 카운터 위로.
        public Vector3 ServePos(int seatIdx)
        {
            var s = SeatPos(seatIdx);
            return new Vector3(s.x, BarPos.y, 0f);
        }

        // ===== Customer Route 계산 =====
        //
        // 손님 stage + 현재 위치를 보고 거쳐야 할 waypoint 시퀀스를 반환.
        // 반환된 Vector3[] 의 마지막 원소가 최종 목적지.
        //
        // 두 단계:
        //   1. 기본 route 계산 (도어 corridor 횡단 여부 판정)
        //   2. 외부 구간에 L자 보정 (대각선 회피, 도로를 수평으로 걷는 느낌)
        //
        // Inside/Outside 판정: pos.y < DoorPos.y → 실내 (도어 아래쪽).
        public Vector3[] RouteFor(Customer c, Vector3 currentPos)
        {
            Vector3 dest = ComputeStageDestination(c);
            float boundaryY = DoorPos.y;
            bool currentInside = currentPos.y < boundaryY;
            bool destInside    = dest.y       < boundaryY;

            Vector3[] baseRoute;
            if (currentInside == destInside)
                baseRoute = new[] { dest };                                       // 같은 쪽 직진
            else if (currentInside)
                baseRoute = new[] { DoorInsidePos, DoorOutsidePos, dest };        // 실내 → 외부
            else
                baseRoute = new[] { DoorOutsidePos, DoorInsidePos, dest };        // 외부 → 실내

            // L자 보정 — 외부 구간에서 x·y 둘 다 큰 차이가 나면 중간 waypoint 삽입.
            //   "수평으로 도로 걷다가 도어 앞에서 꺾어짐" 같은 자연스러운 동선.
            //   실내는 보정 X (다이닝 대각선이 더 자연스러움).
            return InsertLShapeForOutdoor(currentPos, baseRoute, boundaryY);
        }

        const float LShapeMinX = 0.25f; // x 차이가 이 이상이어야 L자 적용
        const float LShapeMinY = 0.15f; // y 차이가 이 이상이어야 L자 적용 (둘 다 충족 필요)

        // 외부에서 외부로 이동하는 segment에 한해 (next.x, prev.y) 중간점 삽입.
        //   prev.y, next.y 둘 다 boundaryY 이상 (도어 위쪽 = 외부) 일 때만.
        Vector3[] InsertLShapeForOutdoor(Vector3 startPos, Vector3[] route, float boundaryY)
        {
            if (route == null || route.Length == 0) return route;

            var result = new List<Vector3>(route.Length + 2);
            Vector3 prev = startPos;
            for (int i = 0; i < route.Length; i++)
            {
                Vector3 next = route[i];
                bool prevOutside = prev.y >= boundaryY;
                bool nextOutside = next.y >= boundaryY;

                if (prevOutside && nextOutside
                    && Mathf.Abs(prev.x - next.x) > LShapeMinX
                    && Mathf.Abs(prev.y - next.y) > LShapeMinY)
                {
                    // 수평 우선: 같은 y로 next.x 까지 이동 후 next로
                    result.Add(new Vector3(next.x, prev.y, next.z));
                }

                result.Add(next);
                prev = next;
            }
            return result.ToArray();
        }

        // Stage 별 최종 목적지 (waypoint 없는 단순 좌표).
        // Arriving non-qb: DoorOutside (도어 앞 대기) — 이전엔 DoorPos 였으나 의미 명확화.
        Vector3 ComputeStageDestination(Customer c)
        {
            switch (c.Stage)
            {
                case CustomerStage.Arriving:
                    return c.IsQueueBound
                        ? QueuePos(Mathf.Max(0, c.QueueIdx))
                        : DoorOutsidePos;
                case CustomerStage.Queueing:
                    return QueuePos(Mathf.Max(0, c.QueueIdx));
                case CustomerStage.Seating:
                case CustomerStage.SeatedOrdering:
                case CustomerStage.SeatedWaiting:
                case CustomerStage.Eating:
                case CustomerStage.Result:
                    return c.SeatIdx >= 0 ? SeatPos(c.SeatIdx) : DoorOutsidePos;
                case CustomerStage.Leaving:
                    return TopExitPos;
                default:
                    return DoorOutsidePos;
            }
        }

        // ===== 내부 lookup 빌드 =====

        // Scene 전체에서 marker 검색 (SceneAnchors가 어느 GameObject에 붙어있어도 무관).
        // Awake 호출 시 active scene의 모든 marker 수집 → registry 빌드.
        T[] FindAll<T>() where T : MonoBehaviour =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        void BuildSeatLookup()
        {
            var found = FindAll<SeatAnchor>();
            int max = -1;
            for (int i = 0; i < found.Length; i++)
                if (found[i].SeatIdx > max) max = found[i].SeatIdx;
            seats = new Transform[max + 1];
            for (int i = 0; i < found.Length; i++)
            {
                int idx = found[i].SeatIdx;
                if (idx < 0)
                {
                    Debug.LogError($"[SceneAnchors] SeatAnchor 음수 idx: {found[i].name}");
                    continue;
                }
                if (seats[idx] != null)
                    Debug.LogError($"[SceneAnchors] SeatAnchor 중복 idx={idx}: {found[i].name} vs {seats[idx].name}");
                seats[idx] = found[i].transform;
            }
            for (int i = 0; i < seats.Length; i++)
                if (seats[i] == null) Debug.LogError($"[SceneAnchors] SeatAnchor 누락: idx {i}");
        }

        void BuildStaffSlotLookup()
        {
            var found = FindAll<StaffSlotAnchor>();
            int max = -1;
            for (int i = 0; i < found.Length; i++)
                if (found[i].SlotIdx > max) max = found[i].SlotIdx;
            staffSlots = new Transform[max + 1];
            for (int i = 0; i < found.Length; i++)
            {
                int idx = found[i].SlotIdx;
                if (idx >= 0 && idx < staffSlots.Length) staffSlots[idx] = found[i].transform;
            }
        }

        void BuildQueueSlotLookup()
        {
            var found = FindAll<QueueSlotAnchor>();
            int max = -1;
            for (int i = 0; i < found.Length; i++)
                if (found[i].QueueIdx > max) max = found[i].QueueIdx;
            queueSlots = new Transform[max + 1];
            for (int i = 0; i < found.Length; i++)
            {
                int idx = found[i].QueueIdx;
                if (idx >= 0 && idx < queueSlots.Length) queueSlots[idx] = found[i].transform;
            }
        }

        void BuildStationLookup()
        {
            var found = FindAll<StationAnchor>();
            stationsByMenuId = new Dictionary<string, Transform>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                var item = found[i].MenuItem;
                if (item == null)
                {
                    Debug.LogError($"[SceneAnchors] StationAnchor의 MenuItem 미설정: {found[i].name}");
                    continue;
                }
                if (stationsByMenuId.ContainsKey(item.Id))
                    Debug.LogError($"[SceneAnchors] StationAnchor 중복 menuId={item.Id}: {found[i].name}");
                else
                    stationsByMenuId[item.Id] = found[i].transform;
            }
        }

        void BuildSingletonLookups()
        {
            door         = FindFirstAnchor<DoorAnchor>();
            doorOutside  = FindFirstAnchorOptional<DoorOutsideAnchor>();
            doorInside   = FindFirstAnchorOptional<DoorInsideAnchor>();
            outsideSpawn = FindFirstAnchor<OutsideSpawnAnchor>();
            topExit      = FindFirstAnchor<TopExitAnchor>();
            bar          = FindFirstAnchor<BarAnchor>();
        }

        // 누락 시 LogError 안 내는 버전 (waypoint는 사용자가 직접 추가하는 옵션 anchor)
        Transform FindFirstAnchorOptional<T>() where T : MonoBehaviour
        {
            var found = FindAll<T>();
            return found.Length > 0 ? found[0].transform : null;
        }

        Transform FindFirstAnchor<T>() where T : MonoBehaviour
        {
            var found = FindAll<T>();
            if (found.Length == 0) Debug.LogError($"[SceneAnchors] {typeof(T).Name} 누락");
            else if (found.Length > 1) Debug.LogWarning($"[SceneAnchors] {typeof(T).Name} 중복 ({found.Length}개) — 첫 번째 사용");
            return found.Length > 0 ? found[0].transform : null;
        }
    }
}
