// =============================================================================
// PopulationManager.cs
// 역할  : 씬에 살아있는 AIUnit 인구를 추적한다.
//         GameManager.CheckAutoSpawn이 스폰 가능 여부를 판단할 때 사용.
// 사용법: GameManager와 동일한 GameObject에 추가(자동). 직접 접근은
//         GameManager.Instance.PopulationManager 사용.
// 의존성: AIUnit (AIVillage.Units)
// GDD   : §11 PopulationManager / Week 7
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Units;

namespace AIVillage.Core
{
    /// <summary>
    /// 활성 AIUnit 인구를 관리하는 매니저.
    /// AIUnit.Start()에서 자동 등록, AIUnit.OnDestroy()에서 자동 해제된다.
    /// </summary>
    public sealed class PopulationManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("인구 상한 (GDD §11)")]
        [SerializeField, Tooltip("최대 허용 인구. GameManager.CheckAutoSpawn이 이 값을 참조한다.")]
        private int _maxPopulation = 10;

        #endregion

        #region Private State

        private readonly List<AIUnit> _units = new List<AIUnit>();

        // [PR Fix]: P-001 — GC 최적화: ToArray() 대신 캐시 버퍼 + 더티 플래그 방식으로
        // 매 Tick 힙 할당을 제거한다. _units 내용이 바뀔 때만 버퍼를 재구성한다.
        private AIUnit[] _snapshotBuffer = new AIUnit[0];
        private bool     _isDirty        = true;

        #endregion

        #region Properties

        /// <summary>현재 살아있는 유닛 수.</summary>
        public int CurrentPop => _units.Count;

        /// <summary>최대 허용 인구.</summary>
        public int MaxPop => _maxPopulation;

        /// <summary>스폰 여유 공간이 있으면 true.</summary>
        public bool HasRoom => _units.Count < _maxPopulation;

        #endregion

        #region Registration

        public void RegisterUnit(AIUnit unit)
        {
            if (unit == null || _units.Contains(unit)) return;
            _units.Add(unit);
            // [PR Fix]: P-001 — 유닛 추가 시 더티 플래그 설정 → 다음 스냅샷 요청 시 버퍼 재구성
            _isDirty = true;
            Debug.Log($"[PopulationManager] 유닛 등록: {unit.name} | 인구: {_units.Count}/{_maxPopulation}");
        }

        public void UnregisterUnit(AIUnit unit)
        {
            if (_units.Remove(unit))
            {
                // [PR Fix]: P-001 — 유닛 제거 시 더티 플래그 설정 → 다음 스냅샷 요청 시 버퍼 재구성
                _isDirty = true;
                Debug.Log($"[PopulationManager] 유닛 해제: {unit.name} | 인구: {_units.Count}/{_maxPopulation}");
            }
        }

        /// <summary>
        /// 현재 등록된 모든 유닛의 배열 스냅샷을 반환한다.
        /// _isDirty 플래그를 이용해 _units에 변경이 없으면 기존 버퍼를 그대로 반환하므로
        /// 매 Tick 호출 시 불필요한 힙 할당(ToArray GC)을 방지한다.
        ///
        /// 주의: 반환된 배열을 직접 수정하지 말 것. 읽기 전용 스냅샷으로만 사용한다.
        /// </summary>
        /// <returns>현재 살아있는 AIUnit 배열 (캐시된 스냅샷)</returns>
        public AIUnit[] GetAllUnitsSnapshot()
        {
            // [PR Fix]: P-001 — _isDirty 가 true일 때만 버퍼를 재구성하여 힙 할당 최소화
            if (_isDirty)
            {
                // 버퍼 크기가 맞지 않을 때만 새 배열을 할당한다 (크기가 같으면 재사용)
                if (_snapshotBuffer.Length != _units.Count)
                    _snapshotBuffer = new AIUnit[_units.Count];

                _units.CopyTo(_snapshotBuffer);
                _isDirty = false;
            }
            return _snapshotBuffer;
        }

        #endregion

        #region Lifecycle

        private void OnDestroy() => _units.Clear();

        #endregion
    }
}
