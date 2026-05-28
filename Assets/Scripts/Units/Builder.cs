// =============================================================================
// Builder.cs
// 역할  : 건설 전담 유닛. Idle → Moving → Building → Idle 루프.
//         BuildingManager에서 가장 가까운 미완공 건설지를 자동으로 찾아 건설.
//         Week 8: OnFleeingEnter/Exit 구현으로 Fleeing 진입/복귀 시 상태 정리.
// 의존성: AIUnit, GameManager, BuildingManager, Building
// GDD   : §6 BuilderFSM / Week 6 / §8-2 Fleeing 연동
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
    ///
    /// Week 8 추가:
    ///   - OnFleeingEnter(): 건설 코루틴 중단, 건물 예약 해제, Invoke 취소, 상태 초기화
    ///   - OnFleeingExit(): SearchAndBuild() 재호출로 건설 사이클 재개
    /// </summary>
    public class Builder : AIUnit
    {
        #region ── Serialized Fields ──

        [Header("건설 설정")]
        [Tooltip("건설지를 찾지 못했을 때 재탐색 대기 시간 (초).")]
        [SerializeField] private float _retryDelay = 3f;

        #endregion

        #region ── Private FSM State ──

        private Building  _targetBuilding;
        private bool      _isInBuildCycle;
        private Coroutine _buildCoroutine;

        #endregion

        #region ── Unity Lifecycle ──

        protected override void Start()
        {
            base.Start(); // PopulationManager 등록 (AIUnit.Start)
            SearchAndBuild();
        }

        #endregion

        #region ── AIUnit Callbacks ──

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

        /// <summary>
        /// 경로 실패 시 예약 해제 및 FSM 초기화 후 재탐색.
        /// Fleeing 상태의 경로 실패는 base.OnPathFailed()에서 직선 폴백으로 처리된다.
        /// </summary>
        protected override void OnPathFailed()
        {
            base.OnPathFailed(); // Fleeing이면 직선 폴백 활성화, 아니면 Idle 복귀

            // Fleeing 경로 실패는 base에서 처리됨 — 아래 정리는 일반 이동 실패에만 적용
            if (_currentState == UnitState.Fleeing) return;

            CancelInvoke(nameof(SearchAndBuild));
            _targetBuilding?.ReleaseConstruction();
            ResetCycle();
            Invoke(nameof(SearchAndBuild), _retryDelay);
        }

        /// <summary>
        /// Fleeing 상태 진입 시 호출 (AIUnit.SetFleeing → OnFleeingEnter 경유).
        /// 진행 중인 건설 작업을 모두 정리하여 깨끗한 상태로 도주를 시작한다.
        ///
        /// 정리 순서:
        ///   1. Invoke 예약 취소: SearchAndBuild 지연 재시도가 예약됐을 경우 취소
        ///   2. 건설 코루틴 중단: null 체크 후 StopCoroutine
        ///   3. 건물 예약 해제: 다른 Builder가 즉시 이어받을 수 있도록
        ///   4. FSM 상태 변수 초기화: 복귀 후 SearchAndBuild에서 깨끗하게 시작
        /// </summary>
        protected override void OnFleeingEnter()
        {
            // ── 1. Invoke 예약 취소 ──
            // SearchAndBuild가 _retryDelay / 0.5f 지연으로 Invoke 예약된 경우 취소
            CancelInvoke(nameof(SearchAndBuild));

            // ── 2. 건설 코루틴 중단 ──
            if (_buildCoroutine != null)
            {
                StopCoroutine(_buildCoroutine);
                _buildCoroutine = null;
            }

            // ── 3. 건물 예약 해제 ──
            // 도주 중 다른 Builder가 이 건물을 즉시 이어받을 수 있도록 해제
            _targetBuilding?.ReleaseConstruction();

            // ── 4. FSM 상태 초기화 ──
            _targetBuilding = null;
            _isInBuildCycle = false;

            Debug.Log($"[Builder] '{name}' — OnFleeingEnter: 건설 상태 정리 완료.");
        }

        /// <summary>
        /// 기지 안전 구역 도달 후 Fleeing에서 복귀할 때 호출 (AIUnit.UpdateFleeing 경유).
        /// SearchAndBuild()를 호출하여 건설 사이클을 재개한다.
        /// </summary>
        protected override void OnFleeingExit()
        {
            Debug.Log($"[Builder] '{name}' — OnFleeingExit: 건설 재개.");
            SearchAndBuild();
        }

        #endregion

        #region ── FSM Logic ──

        /// <summary>
        /// 가장 가까운 미완공 건물을 찾아 이동을 시작한다.
        /// 건물이 없으면 _retryDelay 후 재시도한다.
        /// Fleeing 상태이거나 GameManager가 없으면 즉시 반환한다.
        /// </summary>
        private void SearchAndBuild()
        {
            if (GameManager.Instance == null) return;

            // ── Fleeing 상태에서는 탐색 금지 ──
            // Invoke 지연으로 인해 OnFleeingEnter 이후에 이 메서드가 호출될 수 있음
            if (_currentState == UnitState.Fleeing) return;

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

        /// <summary>
        /// 건설 타이머 대기 후 건물을 완공한다.
        /// enabled=false 상태(Idle)에서도 코루틴은 계속 실행된다.
        /// </summary>
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
