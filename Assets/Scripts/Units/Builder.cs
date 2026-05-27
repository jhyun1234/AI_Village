// =============================================================================
// Builder.cs
// 역할  : 건설 전담 유닛. Idle → Moving → Building → Idle 루프.
//         BuildingManager에서 가장 가까운 미완공 건설지를 자동으로 찾아 건설.
// 의존성: AIUnit, GameManager, BuildingManager, Building
// GDD   : §6 BuilderFSM / Week 6
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Buildings;
using AIVillage.Core;

namespace AIVillage.Units
{
    /// <summary>
    /// 건설지를 자율적으로 찾아 건물을 완공하는 유닛.
    /// Gatherer FSM과 동일한 구조: 예약 → 이동 → 코루틴 건설 → 반복.
    /// </summary>
    public class Builder : AIUnit
    {
        #region Inspector Fields

        [Header("건설 설정")]
        [SerializeField, Tooltip("건설지를 찾지 못했을 때 재탐색 대기 시간 (초).")]
        private float _retryDelay = 3f;

        #endregion

        #region Private FSM State

        private Building  _targetBuilding;
        private bool      _isInBuildCycle;
        private Coroutine _buildCoroutine;

        #endregion

        #region Unity Lifecycle

        protected override void Start()
        {
            base.Start(); // PopulationManager 등록
            SearchAndBuild();
        }

        #endregion

        #region AIUnit Callbacks

        /// <summary>목적지(건설지) 도착 시 호출. 자원 확인 후 건설 코루틴 시작.</summary>
        protected override void OnReachDestination()
        {
            if (_targetBuilding == null || _targetBuilding.IsBuilt)
            {
                ResetCycle();
                return;
            }

            if (!_targetBuilding.StartConstruction())
            {
                // 자원 부족 — 예약 해제 후 재탐색
                _targetBuilding.ReleaseConstruction();
                ResetCycle();
                Invoke(nameof(SearchAndBuild), _retryDelay);
                return;
            }

            _buildCoroutine = StartCoroutine(BuildRoutine());
        }

        /// <summary>Idle 진입 시 호출. 건설 사이클 중간이 아닐 때만 다음 건설지 탐색.</summary>
        protected override void OnIdle()
        {
            if (!_isInBuildCycle)
                SearchAndBuild();
        }

        /// <summary>경로 실패 시 예약 해제 및 FSM 초기화 후 재탐색.</summary>
        protected override void OnPathFailed()
        {
            base.OnPathFailed();

            CancelInvoke(nameof(SearchAndBuild));
            _targetBuilding?.ReleaseConstruction();
            ResetCycle();
            Invoke(nameof(SearchAndBuild), _retryDelay);
        }

        #endregion

        #region FSM Logic

        private void SearchAndBuild()
        {
            if (GameManager.Instance == null) return;

            Building building = GameManager.Instance.BuildingManager
                .GetNearestPendingBuilding(transform.position);

            if (building == null)
            {
                Invoke(nameof(SearchAndBuild), _retryDelay);
                return;
            }

            if (!building.TryReserveConstruction(gameObject))
            {
                Invoke(nameof(SearchAndBuild), 0.5f);
                return;
            }

            _targetBuilding = building;
            _isInBuildCycle = true;

            Debug.Log($"[Builder] '{name}' — '{building.name}' 예약. 이동 시작.");
            SetDestination(building.transform.position);
        }

        private IEnumerator BuildRoutine()
        {
            yield return new WaitForSeconds(_targetBuilding.BuildDuration);

            if (_targetBuilding == null) yield break;

            _targetBuilding.CompleteConstruction();

            // 코루틴 내부에서 ResetCycle의 StopCoroutine을 피하기 위해 직접 초기화
            _targetBuilding = null;
            _isInBuildCycle = false;
            _buildCoroutine = null;

            SearchAndBuild();
        }

        /// <summary>FSM 상태를 초기화한다. 진행 중인 BuildRoutine도 중단한다.</summary>
        private void ResetCycle()
        {
            _targetBuilding = null;
            _isInBuildCycle = false;

            if (_buildCoroutine != null)
            {
                StopCoroutine(_buildCoroutine);
                _buildCoroutine = null;
            }
        }

        #endregion
    }
}
