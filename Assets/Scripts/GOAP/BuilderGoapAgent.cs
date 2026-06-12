// =============================================================================
// BuilderGoapAgent.cs
// 역할  : Builder에 붙이는 GOAP 의사결정 컴포넌트.
//         GoapAgent(Gatherer)와 동일한 구조로 설계됨 — GOAP Phase 3.
//         WorldState 키: HasThreat(0), IsFleeing(6), HasPendingBuilding(7), IsBuildingReserved(8)
//         Goals: Flee(0), BuildBuilding(1), WaitForBuilding(2)
//         Actions: BuilderFleeAction, ConstructAction, SearchBuildingAction, BuilderWaitAction
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Units;
using AIVillage.Core;

namespace AIVillage.GOAP
{
    [RequireComponent(typeof(Builder))]
    public sealed class BuilderGoapAgent : MonoBehaviour
    {
        #region ── Public Output ──

        public GoapGoalType CurrentGoalType { get; private set; } = GoapGoalType.None;

        private int _currentActionIndex = -1;

        public GoapAction CurrentAction =>
            (_currentActionIndex >= 0 && _currentActionIndex < _planBuffer.Count)
            ? _planBuffer[_currentActionIndex] : null;

        #endregion

        #region ── GC-free 버퍼 ──

        private bool[]            _stateData;
        private bool[]            _prevStateData;
        private bool[][]          _depthBuffers;
        private List<GoapAction>  _planBuffer;
        private WorldState        _worldState;
        private GoapGoal[]        _goals;
        private GoapAction[]      _actions;

        #endregion

        #region ── 컴포넌트 참조 ──

        private Builder     _builder;
        private GameManager _gm;

        [Header("위협 감지")]
        [SerializeField, Tooltip("Builder 주변 위협 감지 반경. Gatherer의 safeNodeRadius와 동일하게 설정 권장.")]
        private float _threatDetectionRadius = 4f;

        #endregion

        #region ── Unity Lifecycle ──

        private void Start()
        {
            _builder = GetComponent<Builder>();
            _gm      = GameManager.Instance;

            if (_builder == null)
            {
                Debug.LogError("[BuilderGoapAgent] Builder 컴포넌트를 찾지 못했습니다. 비활성화합니다.");
                enabled = false;
                return;
            }

            int keyCount   = (int)WorldStateKey.Count;
            _stateData     = new bool[keyCount];
            _prevStateData = new bool[keyCount];
            _planBuffer    = new List<GoapAction>(8);

            _depthBuffers = new bool[GoapPlanner.MAX_DEPTH][];
            for (int d = 0; d < GoapPlanner.MAX_DEPTH; d++)
                _depthBuffers[d] = new bool[keyCount];

            _worldState = new WorldState(_stateData);

            InitializeGoalsAndActions();

            _gm?.MessageBus?.Subscribe("game.tick", OnTick);
        }

        private void OnDestroy()
        {
            _gm?.MessageBus?.Unsubscribe("game.tick", OnTick);
        }

        #endregion

        #region ── Tick ──

        private void OnTick(object _)
        {
            if (_builder == null) return;

            UpdateWorldState();

            if (TryAdvanceAction())
            {
                UpdateWorldState();
                _worldState.CopyTo(_prevStateData);
                return;
            }

            bool changed = false;
            for (int i = 0; i < (int)WorldStateKey.Count; i++)
            {
                if (_stateData[i] != _prevStateData[i]) { changed = true; break; }
            }

            if (changed)
            {
                // 활성 플랜 실행 중에는 상태 변화로 인한 Replan 차단 (GoapAgent와 동일 원칙)
                bool planActive = _currentActionIndex >= 0 && _currentActionIndex < _planBuffer.Count;
                if (!planActive)
                {
                    Replan();
                    UpdateWorldState();
                    _worldState.CopyTo(_prevStateData);
                }
            }
        }

        #endregion

        #region ── WorldState 갱신 ──

        private void UpdateWorldState()
        {
            _worldState.Reset();

            ThreatManager tm = _gm?.ThreatManager;

            // HasThreat (공용)
            _worldState.Set(WorldStateKey.HasThreat,
                tm != null && tm.GetNearestMonster(transform.position, _threatDetectionRadius) != null);

            // IsFleeing (공용)
            _worldState.Set(WorldStateKey.IsFleeing, _builder.IsFleeing);

            // HasPendingBuilding: BuildingManager에 Unbuilt 건물 존재 여부
            bool hasPending = _gm?.BuildingManager != null &&
                _gm.BuildingManager.GetNearestPendingBuilding(transform.position) != null;
            _worldState.Set(WorldStateKey.HasPendingBuilding, hasPending);

            // IsBuildingReserved: _targetBuilding != null
            _worldState.Set(WorldStateKey.IsBuildingReserved, _builder.TargetBuilding != null);
        }

        #endregion

        #region ── 플래닝 ──

        private void Replan()
        {
            GoapGoalType newGoal = GoapPlanner.Plan(
                _worldState, _goals, _actions, _planBuffer, _depthBuffers);

            bool goalChanged = (newGoal != CurrentGoalType);

#if UNITY_EDITOR
            if (goalChanged)
                Debug.Log($"[BuilderGoapAgent] '{name}' Goal 변경: {CurrentGoalType} → {newGoal}");
#endif

            CurrentGoalType = newGoal;

            if (goalChanged || _currentActionIndex < 0 || _currentActionIndex >= _planBuffer.Count)
            {
                _currentActionIndex = 0;
                if (_planBuffer.Count > 0)
                    _planBuffer[0].Execute();
            }
        }

        public void ForceReplanOnThreat()
        {
            UpdateWorldState();
            Replan();
            _worldState.CopyTo(_prevStateData);
        }

        #endregion

        #region ── Action 진행 ──

        private bool TryAdvanceAction()
        {
            if (_currentActionIndex < 0 || _currentActionIndex >= _planBuffer.Count) return false;

            GoapAction current = _planBuffer[_currentActionIndex];

            // SearchBuildingAction: 조기 완료 리스크 → NotifyArrival 경로만 허용
            if (current is SearchBuildingAction) return false;

            if (!current.IsComplete(_worldState)) return false;

#if UNITY_EDITOR
            Debug.Log($"[BuilderGoapAgent] '{name}' Action 완료: {current.Name}");
#endif

            _currentActionIndex++;

            if (_currentActionIndex < _planBuffer.Count)
                _planBuffer[_currentActionIndex].Execute();
            else
                Replan();

            return true;
        }

        /// <summary>
        /// Builder.OnReachDestination()에서 건설지 도착 신호 전달.
        /// SearchBuildingAction → ConstructAction 전환 처리.
        /// </summary>
        internal void NotifyArrival()
        {
            if (_currentActionIndex < 0 || _currentActionIndex >= _planBuffer.Count) return;

            GoapAction current = _planBuffer[_currentActionIndex];

            if (current is SearchBuildingAction)
            {
                _currentActionIndex++;

                if (_currentActionIndex >= _planBuffer.Count ||
                    !(_planBuffer[_currentActionIndex] is ConstructAction))
                {
                    Debug.LogWarning($"[BuilderGoapAgent] '{name}' NotifyArrival: ConstructAction을 찾을 수 없습니다. Replan.");
                    _currentActionIndex = -1;
                    Replan();
                    return;
                }

#if UNITY_EDITOR
                Debug.Log($"[BuilderGoapAgent] '{name}' NotifyArrival: SearchBuilding 완료 → Construct 시작");
#endif
                // ConstructAction.Execute()는 no-op — BuildRoutine은 OnReachDestination에서 이미 시작
            }
            // ConstructAction 도착(기지 귀환 없음): 다음 Tick의 TryAdvanceAction이 처리
        }

        /// <summary>도주 해제 후 즉시 재플래닝.</summary>
        internal void NotifyFleeingExit()
        {
            UpdateWorldState();
            _currentActionIndex = -1;
            Replan();
            _worldState.CopyTo(_prevStateData);
        }

        /// <summary>경로 실패 후 즉시 재플래닝.</summary>
        internal void NotifyPathFailed()
        {
            _currentActionIndex = -1;
            UpdateWorldState();
            Replan();
            _worldState.CopyTo(_prevStateData);
        }

        /// <summary>
        /// building.reserved 이벤트 수신 시 호출.
        /// 현재 건설 중(_targetBuilding != null)이면 무시, 대기 중이면 즉시 재플래닝.
        /// </summary>
        internal void NotifyBuildingReserved()
        {
            // 이미 건물을 예약하고 작업 중이면 무시
            if (_builder.TargetBuilding != null) return;

            _currentActionIndex = -1;
            UpdateWorldState();
            Replan();
            _worldState.CopyTo(_prevStateData);
        }

        #endregion

        #region ── 초기화 ──

        private void InitializeGoalsAndActions()
        {
            // Goals: Priority 오름차순 (0=Flee, 1=Build, 2=Wait)
            _goals = new GoapGoal[]
            {
                GoapGoal.CreateFlee(),            // Priority 0
                GoapGoal.CreateBuildBuilding(),   // Priority 1
                GoapGoal.CreateWaitForBuilding()  // Priority 2
            };

            // Actions: ConstructAction을 SearchBuildingAction 앞에 두어
            // DFS가 [SearchBuilding, Construct] 2단 플랜을 반환하도록 한다.
            // (SearchBuildingAction이 앞이면 depth 1에서 SearchBuilding 재시도 → 3단 플랜 발생)
            _actions = new GoapAction[]
            {
                new BuilderFleeAction(),
                new ConstructAction(),
                new SearchBuildingAction(),
                new BuilderWaitAction()
            };

            foreach (GoapAction action in _actions)
                action.InitializeForBuilder(this);
        }

        #endregion

        #region ── Internal Builder Accessor ──

        internal Builder Builder => _builder;

        #endregion
    }
}
