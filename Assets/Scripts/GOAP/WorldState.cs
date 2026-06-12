// =============================================================================
// WorldState.cs
// 역할(Role)  : GOAP 플래너가 읽고 쓰는 세계 상태(World State) 컨테이너.
//               bool 배열을 공유 참조로 감싸 GC 할당 없이 상태를 조회·변경한다.
// 사용법(Usage): GoapAgent.Start()에서 bool[] 배열을 1회 할당한 뒤
//               new WorldState(sharedData) 로 생성한다.
//               배열은 GoapAgent가 소유하며, WorldState 구조체는 그 참조를 감싼다.
// 의존성(Dependencies): 없음 (독립 순수 데이터 구조)
//
// Author: Senior Unity Programmer
// Last Updated: 2026-06-10
// =============================================================================

namespace AIVillage.GOAP
{
    // ───────────────────────────────────────────────────────────────────────────
    // WorldStateKey: 세계 상태의 각 항목을 나타내는 열거형.
    // 정수 인덱스로 bool[] 배열에 직접 접근하므로 순서를 변경하지 말 것.
    // Count는 항상 마지막 항목 — 배열 크기 계산에 사용된다.
    // ───────────────────────────────────────────────────────────────────────────
    public enum WorldStateKey
    {
        // ── Gatherer 전용 (0~6) ──
        HasThreat        = 0, // 주변에 몬스터 위협이 존재하는가          (Gatherer + Builder 공용)
        IsInventoryFull  = 1, // 인벤토리가 가득 찼는가 (수집량 == 1회 채집 한도)
        IsInventoryEmpty = 2, // 인벤토리가 비어있는가 (수집량 == 0)
        HasAvailableNode = 3, // 안전하게 갈 수 있는 자원 노드가 존재하는가
        IsAtBase         = 4, // 현재 기지(안전 구역) 내부에 위치하는가
        IsNodeReserved   = 5, // 현재 목표 노드를 예약 완료한 상태인가
        IsFleeing        = 6, // 현재 도주 중인가                        (Gatherer + Builder 공용)
        // ── Builder 전용 (7~8) ──
        HasPendingBuilding = 7, // 건설 대기 중인 건물이 존재하는가
        IsBuildingReserved = 8, // 현재 목표 건물을 예약 완료한 상태인가
        // ── 항상 마지막 ──
        Count = 9 // 항목 수 — 배열 크기 계산용, 실제 상태 키가 아님
    }

    /// <summary>
    /// GOAP 플래너용 세계 상태 컨테이너 (GC-free 설계).
    ///
    /// 내부적으로 GoapAgent가 소유한 bool[] 배열을 직접 참조하여
    /// 구조체 복사 시에도 힙 할당이 발생하지 않는다.
    /// WorldStateKey 열거형을 인덱스로 사용해 각 상태에 접근한다.
    /// </summary>
    public struct WorldState
    {
        // ── Private Fields ──
        // GoapAgent._stateData 를 직접 참조 — 복사 없이 공유 (GC-free)
        private readonly bool[] _data;

        // ───────────────────────────────────────────────────────────────────────
        // 생성자: GoapAgent가 소유한 bool[] 를 주입받는다.
        // sharedData 크기는 반드시 (int)WorldStateKey.Count 이상이어야 한다.
        // ───────────────────────────────────────────────────────────────────────
        public WorldState(bool[] sharedData)
        {
            _data = sharedData;
        }

        /// <summary>지정한 키의 현재 상태 값을 반환한다.</summary>
        public bool Get(WorldStateKey key)
        {
            return _data[(int)key];
        }

        /// <summary>지정한 키의 상태 값을 설정한다.</summary>
        public void Set(WorldStateKey key, bool value)
        {
            _data[(int)key] = value;
        }

        /// <summary>
        /// 모든 상태 키를 false로 초기화한다.
        /// UpdateWorldState() 시작 시 매 Tick 호출하여 오래된 상태를 제거한다.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < (int)WorldStateKey.Count; i++)
                _data[i] = false;
        }

        /// <summary>
        /// 다른 WorldState와 element-by-element 비교.
        /// 변경 감지(스냅샷 diff)에 사용된다.
        /// </summary>
        public bool Equals(WorldState other)
        {
            for (int i = 0; i < (int)WorldStateKey.Count; i++)
            {
                if (_data[i] != other._data[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 현재 상태를 target 배열에 복사한다 (스냅샷 저장용).
        /// GoapAgent._prevStateData 갱신에 사용된다.
        /// </summary>
        public void CopyTo(bool[] target)
        {
            for (int i = 0; i < (int)WorldStateKey.Count; i++)
                target[i] = _data[i];
        }
    }
}
