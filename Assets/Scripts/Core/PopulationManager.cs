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
            Debug.Log($"[PopulationManager] 유닛 등록: {unit.name} | 인구: {_units.Count}/{_maxPopulation}");
        }

        public void UnregisterUnit(AIUnit unit)
        {
            if (_units.Remove(unit))
                Debug.Log($"[PopulationManager] 유닛 해제: {unit.name} | 인구: {_units.Count}/{_maxPopulation}");
        }

        #endregion

        #region Lifecycle

        private void OnDestroy() => _units.Clear();

        #endregion
    }
}
