// =============================================================================
// Builder.cs
// 역할  : 건설 전담 유닛. Idle → Moving → Building → Idle 루프.
//         BuildingManager에서 가장 가까운 미완공 건설지를 자동으로 찾아 건설.
//         Week 8: OnFleeingEnter/Exit 구현으로 Fleeing 진입/복귀 시 상태 정리.
//         GOAP 3단계: BuilderGoapAgent 부착 시 GOAP가 의사결정 담당.
//                     미부착 시 기존 Legacy FSM 동작 그대로 (Regression 없음).
// 의존성: AIUnit, GameManager, BuildingManager, Building, BuilderGoapAgent
// GDD   : §6 BuilderFSM / Week 6 / §8-2 Fleeing 연동 / §GOAP-3단계
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Buildings;
using AIVillage.Core;
using AIVillage.Resources;
using AIVillage.GOAP;

namespace AIVillage.Units
{
    /// <summary>
    /// 건설지를 자율적으로 찾아 건물을 완공하는 유닛.
    /// Gatherer FSM과 동일한 구조: 예약 → 이동 → 코루틴 건설 → 반복.
    ///
    /// Week 8 추가:
    ///   - OnFleeingEnter(): 건설 코루틴 중단, 건물 예약 해제, Invoke 취소, 상태 초기화
    ///   - OnFleeingExit(): SearchAndBuild() 재호출로 건설 사이클 재개
    ///
    /// GOAP 3단계:
    ///   - BuilderGoapAgent 부착 시 GOAP가 의사결정 담당 (언제 SearchAndBuild를 호출할지)
    ///   - 미부착 시 기존 Legacy FSM 완전 동작 (Regression 없음)
    /// </summary>
    public class Builder : AIUnit
    {
        #region ── Serialized Fields ──

        [Header("건설 설정")]
        [Tooltip("건설지를 찾지 못했을 때 재탐색 대기 시간 (초).")]
        [SerializeField] private float _retryDelay = 3f;

        #endregion

        #region ── Private FSM State ──

        private Building          _targetBuilding;
        private bool              _isInBuildCycle;
        private Coroutine         _buildCoroutine;
        private BuilderGoapAgent  _builderGoapAgent; // GOAP 3단계: null이면 Legacy FSM 동작

        #endregion

        #region ── Unity Lifecycle ──

        protected override void Start()
        {
            base.Start(); // AIUnit.Start → PopulationManager 등록

            _builderGoapAgent = GetComponent<BuilderGoapAgent>(); // null 허용

            GameManager.Instance?.MessageBus?.Subscribe("building.reserved", OnBuildingReserved);

            // GOAP 모드: BuilderGoapAgent의 첫 Tick에서 플래닝이 시작되므로 직접 호출 안 함
            if (_builderGoapAgent == null)
                SearchAndBuild();
        }

        protected override void OnDestroy()
        {
            GameManager.Instance?.MessageBus?.Unsubscribe("building.reserved", OnBuildingReserved);
            base.OnDestroy();
        }

        #endregion

        #region ── AIUnit Callbacks ──

        /// <summary>목적지(건설지) 도착 시 호출. 자원 확인 후 건설 코루틴 시작.</summary>
        protected override void OnReachDestination()
        {
            if (_targetBuilding == null || _targetBuilding.IsBuilt)
            {
                ResetCycle();
                _builderGoapAgent?.NotifyArrival(); // GOAP: SearchBuilding 완료 신호 (건물 없음/완공)
                return;
            }

            if (!_targetBuilding.StartConstruction())
            {
                // 자원 부족 — Gatherer 채집 우선순위 갱신
                GameManager gm = GameManager.Instance;
                if (gm != null)
                {
                    int woodDeficit  = Mathf.Max(0, _targetBuilding.BuildCostWood  - gm.GetResource(ResourceType.WOOD));
                    int stoneDeficit = Mathf.Max(0, _targetBuilding.BuildCostStone - gm.GetResource(ResourceType.STONE));
                    gm.RequestResource(woodDeficit >= stoneDeficit ? ResourceType.WOOD : ResourceType.STONE);
                }

                _targetBuilding.ReleaseConstruction();
                _targetBuilding = null;
                // _isInBuildCycle 유지 → OnIdle() 즉시 재탐색 차단 (무한루프 방지)
                // GOAP 모드: SearchBuildingAction이 현재 Action이므로 Invoke 재시도가 P-004 가드를 통과
                Invoke(nameof(SearchAndBuild), _retryDelay);
                return;
            }

            _buildCoroutine = StartCoroutine(BuildRoutine());
            _builderGoapAgent?.NotifyArrival(); // GOAP: SearchBuilding → Construct 전환
        }

        /// <summary>Idle 진입 시 호출.</summary>
        protected override void OnIdle()
        {
            if (_builderGoapAgent != null) return; // GOAP 모드: Agent가 결정
            if (!_isInBuildCycle)
                SearchAndBuild();
        }

        protected override void OnPathFailed()
        {
            base.OnPathFailed();
            if (_currentState == UnitState.Fleeing) return;

            CancelInvoke(nameof(SearchAndBuild));
            _targetBuilding?.ReleaseConstruction();
            ResetCycle();

            if (_builderGoapAgent != null)
            {
                _builderGoapAgent.NotifyPathFailed();
                return;
            }

            Invoke(nameof(SearchAndBuild), _retryDelay);
        }

        /// <summary>위협 감지 시 호출. GOAP 모드에서는 ForceReplanOnThreat()도 호출.</summary>
        public override void OnThreatDetected(Monster monster)
        {
            _builderGoapAgent?.ForceReplanOnThreat();
            base.OnThreatDetected(monster); // SetFleeing()
        }

        protected override void OnFleeingEnter()
        {
            CancelInvoke(nameof(SearchAndBuild));

            if (_buildCoroutine != null)
            {
                StopCoroutine(_buildCoroutine);
                _buildCoroutine = null;
            }

            _targetBuilding?.ReleaseConstruction();
            _targetBuilding = null;
            _isInBuildCycle = false;

#if UNITY_EDITOR
            Debug.Log($"[Builder] '{name}' — OnFleeingEnter: 건설 상태 정리 완료.");
#endif
        }

        protected override void OnFleeingExit()
        {
#if UNITY_EDITOR
            Debug.Log($"[Builder] '{name}' — OnFleeingExit: 건설 재개.");
#endif
            if (_builderGoapAgent != null)
            {
                _builderGoapAgent.NotifyFleeingExit();
                return;
            }
            SearchAndBuild();
        }

        #endregion

        #region ── FSM Logic ──

        private void SearchAndBuild()
        {
            if (GameManager.Instance == null) return;
            if (_currentState == UnitState.Fleeing) return;

            // [GOAP P-004] GOAP 모드에서 현재 Action이 SearchBuildingAction이 아니면 무시.
            // 도중에 Goal이 바뀐 뒤 지연 발화된 Invoke가 잘못된 탐색을 시작하는 것을 방지.
            if (_builderGoapAgent != null && !(_builderGoapAgent.CurrentAction is SearchBuildingAction))
                return;

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

#if UNITY_EDITOR
            Debug.Log($"[Builder] '{name}' — '{building.name}' 예약. 이동 시작.");
#endif
            SetDestination(building.transform.position);
        }

        private IEnumerator BuildRoutine()
        {
            yield return new WaitForSeconds(_targetBuilding.BuildDuration);

            if (_targetBuilding == null) yield break;

            _targetBuilding.CompleteConstruction();

            _targetBuilding = null;
            _isInBuildCycle = false;
            _buildCoroutine = null;

            // GOAP 모드: 다음 Tick에서 IsBuildingReserved=false → ConstructAction.IsComplete → Replan
            if (_builderGoapAgent == null)
                SearchAndBuild();
        }

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

        #region ── MessageBus Handlers ──

        private void OnBuildingReserved(object payload)
        {
            if (_currentState == UnitState.Fleeing) return;

            if (_builderGoapAgent != null)
            {
                _builderGoapAgent.NotifyBuildingReserved();
                return;
            }

            // Legacy 모드
            if (_isInBuildCycle && _targetBuilding != null) return;

            CancelInvoke(nameof(SearchAndBuild));
            _isInBuildCycle = false;
            SearchAndBuild();

#if UNITY_EDITOR
            Debug.Log($"[Builder] '{name}' — building.reserved 수신: 즉시 재탐색 시작.");
#endif
        }

        #endregion

        #region ── GOAP 3단계: BuilderGoapAgent 전용 internal 접근자 ──

        /// <summary>현재 예약된 목표 건물 (BuilderGoapAgent용).</summary>
        internal Building TargetBuilding => _targetBuilding;

        /// <summary>SearchBuildingAction.Execute()가 호출. SearchAndBuild() 위임.</summary>
        internal void ExecuteSearchBuilding() => SearchAndBuild();

        #endregion
    }
}
