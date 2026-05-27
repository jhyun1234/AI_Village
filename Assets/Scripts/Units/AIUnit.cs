// =============================================================================
// AIUnit.cs
// 역할  : 모든 AI 유닛의 공통 기반 클래스 (이동, 체력, 상태 관리)
//         AStar 2D Grid Pathfinding 패키지 기반 비동기 경로 탐색 사용
// 사용법: 직접 추가 불가 (abstract). Gatherer 등 파생 클래스를 추가할 것.
// 의존성: AIVillage.Core.PathfindingGrid, AStar.AStarPathfinding, AIVillage.Units.UnitState
// GDD   : §4 AIUnit 공통 데이터 / R-002 Tick 기반 FSM
// =============================================================================

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AStar;
using AIVillage.Core;

namespace AIVillage.Units
{
    /// <summary>
    /// HP, 이동, 상태 전환 등 모든 AI 유닛 공통 로직을 담은 추상 MonoBehaviour.
    /// AStar 2D Grid Pathfinding 패키지를 통해 비동기로 경로를 계산한다.
    /// </summary>
    public abstract class AIUnit : MonoBehaviour
    {
        #region Serialized Fields

        [Header("체력 (GDD §4)")]
        [SerializeField, Tooltip("현재 체력 (GDD §4)")]
        protected float _hp = 100f;

        [SerializeField, Tooltip("최대 체력 (GDD §4)")]
        protected float _maxHp = 100f;

        [Header("이동")]
        [SerializeField, Range(0.5f, 20f), Tooltip("이동 속도. Inspector에서 유닛별 조정 가능")]
        protected float _moveSpeed = 3.5f;

        #endregion

        #region Protected State Fields (GDD §4 미래 대비)

        protected UnitState _currentState = UnitState.Idle;
        protected bool  _isEquipped        = false; // TODO: v0.5 무기 장착 (GDD §4)
        protected int   _originalFactionId = 0;     // TODO: v1.0 팩션 (GDD §4)
        protected int   _currentFactionId  = 0;     // TODO: v1.0 팩션 (GDD §4)
        protected float _loyalty           = 100f;  // TODO: v1.0/v2.0 반란 (GDD §4)

        #endregion

        #region Private Pathfinding Fields

        private Vector3[] _waypoints;
        private int       _waypointIndex;
        private int       _pathCount;
        private bool      _isPathReady;

        private CancellationTokenSource _pathCts;

        // 도착 판정: 0.2f 월드 단위 → 제곱값으로 sqrt 연산 제거
        private const float ARRIVAL_THRESHOLD_SQ = 0.04f; // 0.2f * 0.2f

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake() { }

        /// <summary>PopulationManager에 자동 등록한다. 파생 클래스에서 반드시 base.Start() 호출.</summary>
        protected virtual void Start()
        {
            GameManager.Instance?.PopulationManager?.RegisterUnit(this);
        }

        /// <summary>
        /// Moving 상태일 때만 enabled = true이므로 항상 FollowPath를 호출해도 안전하다.
        /// </summary>
        protected virtual void Update()
        {
            FollowPath();
        }

        protected virtual void OnDestroy()
        {
            _pathCts?.Cancel();
            _pathCts?.Dispose();
            GameManager.Instance?.PopulationManager?.UnregisterUnit(this);
        }

        #endregion

        #region Editor Validation

        /// <summary>Inspector에서 HP 값 수정 시 유효 범위로 즉시 교정한다.</summary>
        protected virtual void OnValidate()
        {
            _maxHp = Mathf.Max(1f, _maxHp);
            _hp    = Mathf.Clamp(_hp, 0f, _maxHp);
        }

        #endregion

        #region State Management

        /// <summary>
        /// 상태를 변경하고 Moving 여부에 따라 Update를 토글한다.
        /// Moving이 아닐 때 enabled = false로 Update 호출 비용을 완전히 제거한다.
        /// </summary>
        private void SetState(UnitState newState)
        {
            _currentState = newState;
            enabled = (newState == UnitState.Moving);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 목적지를 설정하고 A* 경로 계산을 시작한다.
        /// 이전 요청이 진행 중이면 취소하고 새 요청을 시작한다 (Race Condition 방지).
        /// </summary>
        /// <param name="target">이동할 월드 좌표</param>
        public void SetDestination(Vector3 target)
        {
            // 이전 비동기 요청 취소
            _pathCts?.Cancel();
            _pathCts?.Dispose();
            _pathCts = new CancellationTokenSource();

            _isPathReady   = false;
            _waypoints     = null;
            _waypointIndex = 0;
            _pathCount     = 0;

            SetState(UnitState.Moving);

            // fire-and-forget: 완료 시 _waypoints와 _isPathReady가 자동으로 세팅됨
            _ = RequestPath(target, _pathCts.Token);
        }

        #endregion

        #region Pathfinding

        /// <summary>
        /// AStar 패키지를 통해 비동기로 경로를 계산하고 _waypoints를 세팅한다.
        /// </summary>
        private async Task RequestPath(Vector3 target, CancellationToken token)
        {
            PathfindingGrid grid = PathfindingGrid.Instance;

            if (grid == null)
            {
                Debug.LogWarning($"[AIUnit] '{name}' — PathfindingGrid.Instance가 null입니다. 씬에 PathfindingGrid 오브젝트가 있는지 확인하세요.");
                OnPathFailed();
                return;
            }

            // 월드 좌표 → 그리드 좌표 변환 (메인 스레드에서 수행)
            Vector2Int start = grid.WorldToGrid(transform.position);
            Vector2Int goal  = grid.WorldToGrid(target);

            // 스레드 안전: Task.Run 내부에서 사용할 맵 복사본 생성
            bool[,] mapCopy = grid.GetWalkableMapCopy();

            // AStar 비동기 경로 계산
            // GeneratePath(열, 행, 열, 행, [행,열] 맵)
            (int, int)[] tilePath = await AStarPathfinding.GeneratePath(
                start.x, start.y,
                goal.x,  goal.y,
                mapCopy);

            // 취소 여부 재확인 (await 사이에 취소됐을 수 있음)
            if (token.IsCancellationRequested) return;

            if (tilePath == null || tilePath.Length == 0)
            {
                Debug.LogWarning($"[AIUnit] '{name}' — 경로를 찾을 수 없습니다. 목표: Grid({goal.x},{goal.y})");
                OnPathFailed();
                return;
            }

            // 그리드 좌표 → 월드 좌표로 변환
            _waypoints = new Vector3[tilePath.Length];
            for (int i = 0; i < tilePath.Length; i++)
            {
                // AStar 반환값: (열(x), 행(y))
                _waypoints[i] = grid.GridToWorld(tilePath[i].Item1, tilePath[i].Item2);
            }

            _pathCount     = _waypoints.Length;
            _waypointIndex = 0;
            _isPathReady   = true;
        }

        /// <summary>
        /// 매 프레임 웨이포인트를 순서대로 따라 이동한다.
        /// Moving 상태(enabled=true)일 때만 Update에서 호출된다.
        /// </summary>
        private void FollowPath()
        {
            if (!_isPathReady || _waypoints == null) return;

            if (_waypointIndex >= _pathCount)
            {
                OnArrival();
                return;
            }

            Vector3 waypoint = _waypoints[_waypointIndex];
            Vector3 position = transform.position;

            float dx     = waypoint.x - position.x;
            float dy     = waypoint.y - position.y;
            float distSq = dx * dx + dy * dy;

            if (distSq < ARRIVAL_THRESHOLD_SQ)
            {
                _waypointIndex++;
                return;
            }

            // sqrt 1회로 정규화 후 이동
            float invDist = 1f / Mathf.Sqrt(distSq);
            transform.position = new Vector3(
                position.x + dx * invDist * _moveSpeed * Time.deltaTime,
                position.y + dy * invDist * _moveSpeed * Time.deltaTime,
                0f);
        }

        private void OnArrival()
        {
            SetState(UnitState.Idle);
            _isPathReady = false;
            OnReachDestination();
            OnIdle(); // TODO: Week 5 — MessageBus 이벤트 발행
        }

        #endregion

        #region Abstract / Virtual Callbacks

        /// <summary>목적지 도달 시 파생 클래스에서 구현.</summary>
        protected abstract void OnReachDestination();

        /// <summary>Idle 상태 진입 시 파생 클래스에서 구현.</summary>
        protected abstract void OnIdle();

        /// <summary>경로 계산 실패 시 호출. 기본 구현은 비어있으며 파생 클래스에서 재정의 가능.</summary>
        protected virtual void OnPathFailed()
        {
            Debug.LogWarning($"[AIUnit] '{name}' — 경로 실패. Idle 상태로 복귀.");
            SetState(UnitState.Idle);
        }

        #endregion
    }
}
