// =============================================================================
// AIUnit.cs
// 역할  : 모든 AI 유닛의 공통 기반 클래스 (이동, 체력, 상태 관리, 도주 처리)
//         AStar 2D Grid Pathfinding 패키지 기반 비동기 경로 탐색 사용.
//         Week 8: TakeDamage / SetFleeing / Fleeing 상태 처리 추가.
// 사용법: 직접 추가 불가 (abstract). Gatherer, Builder 등 파생 클래스를 추가할 것.
// 의존성: AIVillage.Core.PathfindingGrid, AStar.AStarPathfinding, AIVillage.Units.UnitState
//         AIVillage.Core.GameManager (MessageBus, BasePosition)
// GDD   : §4 AIUnit 공통 데이터 / §8-2 Fleeing / R-002 Tick 기반 FSM
// =============================================================================

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AStar;
using AIVillage.Core;

namespace AIVillage.Units
{
    /// <summary>
    /// HP, 이동, 상태 전환, 도주 등 모든 AI 유닛 공통 로직을 담은 추상 MonoBehaviour.
    /// AStar 2D Grid Pathfinding 패키지를 통해 비동기로 경로를 계산한다.
    ///
    /// Week 8 추가:
    ///   - TakeDamage(float): 피해 처리 및 "unit.died" 이벤트 발행
    ///   - SetFleeing(): 기지로 도주 시작 (OnFleeingEnter 콜백 → 파생 클래스 정리)
    ///   - Fleeing 상태 Update: 기지 도달 시 체력 회복 / Idle 복귀
    ///   - _useDirectMoveToCamp: 경로 실패 시 직선 이동 폴백 (GDD §8 제약사항 7번)
    /// </summary>
    public abstract class AIUnit : MonoBehaviour
    {
        #region ── Constants ──

        // 도착 판정: 0.2f 월드 단위 → 제곱값으로 sqrt 연산 제거
        private const float ARRIVAL_THRESHOLD_SQ = 0.04f; // 0.2f * 0.2f

        #endregion

        #region ── Serialized Fields ──

        [Header("체력 (GDD §4)")]
        [Tooltip("현재 체력 (GDD §4)")]
        [SerializeField] protected float _hp = 100f;

        [Tooltip("최대 체력 (GDD §4)")]
        [SerializeField] protected float _maxHp = 100f;

        [Header("이동")]
        [Tooltip("이동 속도. Inspector에서 유닛별 조정 가능")]
        [SerializeField, Range(0.5f, 20f)] protected float _moveSpeed = 3.5f;

        [Header("도주 설정 (GDD §8-2)")]
        [Tooltip("기지로부터 이 거리 이내이면 '안전 구역'으로 판정. GDD: 5f")]
        [SerializeField] private float _baseSafeWorldRadius = 5f; // 기획서 수치: 기지 안전 반경 5f

        [Tooltip("기지 안전 구역 내 체력 회복 속도 (HP/초). GDD: 5f")]
        [SerializeField] private float _healRate = 5f; // 기획서 수치: 체력 회복 5f HP/초

        #endregion

        #region ── Protected State Fields (GDD §4 미래 대비) ──

        protected UnitState _currentState = UnitState.Idle;
        protected bool  _isEquipped        = false; // TODO: v0.5 무기 장착 (GDD §4)
        protected int   _originalFactionId = 0;     // TODO: v1.0 팩션 (GDD §4)
        protected int   _currentFactionId  = 0;     // TODO: v1.0 팩션 (GDD §4)
        protected float _loyalty           = 100f;  // TODO: v1.0/v2.0 반란 (GDD §4)

        #endregion

        #region ── Private Pathfinding Fields ──

        private Vector3[] _waypoints;
        private int       _waypointIndex;
        private int       _pathCount;
        private bool      _isPathReady;

        private CancellationTokenSource _pathCts;

        // ── Fleeing 직선 이동 폴백 플래그 (GDD §8, 제약사항 7번) ──
        // Fleeing 상태에서 A* 경로 계산이 실패하면 true로 설정된다.
        // Update에서 이 플래그가 true이면 기지를 향해 직선으로 이동한다.
        private bool _useDirectMoveToCamp = false;

        #endregion

        #region ── Unity Lifecycle ──

        protected virtual void Awake() { }

        /// <summary>PopulationManager에 자동 등록한다. 파생 클래스에서 반드시 base.Start() 호출.</summary>
        protected virtual void Start()
        {
            GameManager.Instance?.PopulationManager?.RegisterUnit(this);
        }

        /// <summary>
        /// Moving 또는 Fleeing 상태일 때만 enabled = true이므로 이 메서드는 해당 상태에서만 호출된다.
        /// Fleeing일 때는 직선 이동 폴백과 기지 도달 판정을 추가로 처리한다.
        /// </summary>
        protected virtual void Update()
        {
            // ── Fleeing 상태 전용 처리 ──
            if (_currentState == UnitState.Fleeing)
            {
                UpdateFleeing();
                return; // Fleeing 중에는 일반 FollowPath를 호출하지 않는다
            }

            // ── Moving 상태: 경로 추종 ──
            FollowPath();
        }

        protected virtual void OnDestroy()
        {
            _pathCts?.Cancel();
            _pathCts?.Dispose();
            GameManager.Instance?.PopulationManager?.UnregisterUnit(this);
        }

        #endregion

        #region ── Editor Validation ──

        /// <summary>Inspector에서 HP 값 수정 시 유효 범위로 즉시 교정한다.</summary>
        protected virtual void OnValidate()
        {
            _maxHp = Mathf.Max(1f, _maxHp);
            _hp    = Mathf.Clamp(_hp, 0f, _maxHp);
        }

        #endregion

        #region ── State Management ──

        /// <summary>
        /// 상태를 변경하고 Update 실행 여부를 토글한다.
        /// Moving 또는 Fleeing 상태일 때만 enabled = true로 Update가 활성화된다.
        ///
        /// Week 8 변경:
        ///   - private → protected: 파생 클래스에서 직접 접근할 필요가 생긴 경우 대비
        ///   - enabled 조건에 UnitState.Fleeing 추가
        /// </summary>
        /// <param name="newState">전환할 새 상태</param>
        protected void SetState(UnitState newState)
        {
            _currentState = newState;
            // Moving 또는 Fleeing 상태일 때만 Update를 활성화한다
            enabled = (newState == UnitState.Moving || newState == UnitState.Fleeing);
        }

        #endregion

        #region ── Public API ──

        /// <summary>
        /// 목적지를 설정하고 A* 경로 계산을 시작한다.
        /// 이전 요청이 진행 중이면 취소하고 새 요청을 시작한다 (Race Condition 방지).
        /// Fleeing 상태에서 호출 시 상태를 Moving으로 변경하지 않고 경로만 갱신한다.
        /// </summary>
        /// <param name="target">이동할 월드 좌표</param>
        public void SetDestination(Vector3 target)
        {
            // 이전 비동기 요청 취소
            _pathCts?.Cancel();
            _pathCts?.Dispose();
            _pathCts = new CancellationTokenSource();

            _isPathReady         = false;
            _waypoints           = null;
            _waypointIndex       = 0;
            _pathCount           = 0;
            _useDirectMoveToCamp = false; // 새 이동 요청 시 폴백 플래그 초기화

            // Fleeing 중 SetDestination 호출 시 상태를 Moving으로 덮어쓰지 않는다
            // (기지로 이동 중이므로 Fleeing 상태를 유지해야 함)
            if (_currentState != UnitState.Fleeing)
                SetState(UnitState.Moving);

            _ = RequestPath(target, _pathCts.Token);
        }

        /// <summary>
        /// 피해를 받는다. HP가 0이 되면 "unit.died" 이벤트를 발행하고 오브젝트를 파괴한다.
        /// Monster.AttackRoutine()에서 호출된다.
        /// </summary>
        /// <param name="amount">받은 피해량 (양수)</param>
        public void TakeDamage(float amount)
        {
            // [PR Fix]: R-004 — 가드 주석 추가: Destroy는 다음 프레임에 실제 파괴되므로,
            // 같은 프레임 안에 TakeDamage가 재진입할 수 있다. 이 가드로 중복 사망 처리를 방지한다.
            if (_hp <= 0f) return;

            _hp -= amount;

            if (_hp <= 0f)
            {
                _hp = 0f; // 음수 HP 방지

                // "unit.died" 이벤트 발행: Week 10 승리/패배 조건 체크에 활용됨
                GameManager.Instance?.MessageBus?.Publish("unit.died", gameObject);
                Debug.Log($"[AIUnit] '{name}' 사망!");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 몬스터 위협 감지 시 호출하여 기지로 도주 상태를 시작한다.
        /// 이미 Fleeing 중이거나 기지 안전 구역 내에 있으면 무시한다 (재진입 방지).
        ///
        /// 호출 순서:
        ///   1. 재진입 방지 체크 (_currentState == Fleeing)
        ///   2. 기지 안전 구역 내 체크 (이미 안전하면 도주 불필요)
        ///   3. SetState(Fleeing) → enabled = true → Update 활성화
        ///   4. OnFleeingEnter() → 파생 클래스에서 진행 중인 작업 정리
        ///   5. "unit.fleeing" 이벤트 발행 (Week 9 DangerRegistry에서 수신 예정)
        ///   6. SetDestination(기지) → A* 경로 계산 시작
        /// </summary>
        public void SetFleeing()
        {
            // ── 재진입 방지: 이미 도주 중이면 무시 ──
            if (_currentState == UnitState.Fleeing) return;

            // ── 기지 안전 구역 내에 이미 있으면 도주 불필요 ──
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                Vector2 basePos = gm.BasePosition;
                float dx = transform.position.x - basePos.x;
                float dy = transform.position.y - basePos.y;
                float distSq = dx * dx + dy * dy;

                if (distSq <= _baseSafeWorldRadius * _baseSafeWorldRadius)
                {
                    // 이미 기지 안전 구역 내에 있음 — 도주할 필요 없음
                    return;
                }
            }

            // ── Fleeing 상태로 전환 (enabled = true → Update 활성화) ──
            SetState(UnitState.Fleeing);

            // ── 파생 클래스 정리 콜백 호출 ──
            // Gatherer: 채집 코루틴 중단, 노드 예약 해제, SearchAndGo Invoke 취소
            // Builder: 건설 코루틴 중단, 건물 예약 해제, SearchAndBuild Invoke 취소
            OnFleeingEnter();

            // ── "unit.fleeing" 이벤트 발행 ──
            // Week 9 DangerRegistry에서 수신하여 위험 좌표를 기록할 예정 (GDD §9)
            gm?.MessageBus?.Publish("unit.fleeing", (Vector2)transform.position);

            // ── 기지를 목적지로 설정 ──
            if (gm != null)
            {
                Vector2 home = gm.BasePosition;
                SetDestination(new Vector3(home.x, home.y, 0f));
            }
            else
            {
                Debug.LogWarning($"[AIUnit] '{name}' — SetFleeing: GameManager.Instance가 null. 기지 좌표를 알 수 없음.");
            }

            Debug.Log($"[AIUnit] '{name}' → Fleeing 시작!");
        }

        #endregion

        #region ── Fleeing Update ──

        /// <summary>
        /// Fleeing 상태에서 매 프레임 호출된다.
        ///
        /// 처리 순서:
        ///   1. 기지 안전 구역 도달 체크 → 도달 시 체력 회복 + Idle 복귀
        ///   2. 기지 반경 밖: _useDirectMoveToCamp true이면 직선 이동
        ///   3. 기지 반경 밖: false이면 FollowPath() (A* 경로 추종)
        ///
        /// GDD §8-2: 기지 반경 내에서만 체력 회복 — 반경 밖에서는 회복 없음.
        /// </summary>
        private void UpdateFleeing()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            Vector2 basePos    = gm.BasePosition;
            float   dx         = transform.position.x - basePos.x;
            float   dy         = transform.position.y - basePos.y;
            float   distSqToBase  = dx * dx + dy * dy;
            float   safeRadiusSq  = _baseSafeWorldRadius * _baseSafeWorldRadius;

            // ── 기지 안전 구역 도달 판정 ──
            if (distSqToBase <= safeRadiusSq)
            {
                // 체력 회복: GDD §8-2 — 기지 반경 내에서만 회복
                // Formula: hp = Min(hp + healRate * deltaTime, maxHp)
                _hp = Mathf.Min(_hp + _healRate * Time.deltaTime, _maxHp);

                // 경로 계산이 아직 진행 중이어도 기지에 도달했으므로 즉시 종료
                _pathCts?.Cancel();
                // [PR Fix]: R-002 — Cancel 후 Dispose 및 null 대입으로 메모리 누수 방지
                _pathCts?.Dispose();
                _pathCts             = null;
                _isPathReady         = false;
                _useDirectMoveToCamp = false;

                // Idle 상태로 복귀하고 파생 클래스에 복귀 알림
                SetState(UnitState.Idle);
                OnFleeingExit();

                Debug.Log($"[AIUnit] '{name}' — 기지 도달. Fleeing 해제. HP: {_hp:F1}/{_maxHp:F1}");
                return;
            }

            // ── 기지 반경 밖: 이동 처리 ──
            if (_useDirectMoveToCamp)
            {
                // A* 경로 실패 폴백 — 기지를 향해 직선으로 이동 (장애물 무시)
                // 생존 우선 정책: 경로가 없어도 기지 방향으로 계속 이동
                Vector3 targetPos = new Vector3(basePos.x, basePos.y, 0f);
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    _moveSpeed * Time.deltaTime);
            }
            else
            {
                // A* 경로를 따라 이동 (경로 준비 전에는 FollowPath 내부에서 early return)
                FollowPath();
            }
        }

        #endregion

        #region ── Pathfinding ──

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
        /// Moving 상태의 Update와 Fleeing 상태의 UpdateFleeing (폴백 미사용 시)에서 호출된다.
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
            // Formula: velocity = direction.normalized * speed * deltaTime
            float invDist = 1f / Mathf.Sqrt(distSq);
            transform.position = new Vector3(
                position.x + dx * invDist * _moveSpeed * Time.deltaTime,
                position.y + dy * invDist * _moveSpeed * Time.deltaTime,
                0f);
        }

        /// <summary>
        /// 경로의 마지막 웨이포인트에 도달했을 때 호출된다.
        /// Fleeing 상태에서는 UpdateFleeing이 거리 기반 판정을 직접 처리하므로
        /// 상태를 변경하지 않고 _isPathReady만 초기화한다.
        /// </summary>
        private void OnArrival()
        {
            // Fleeing 중 경로 끝 도달: UpdateFleeing이 기지 거리 기반으로 판정하므로
            // 여기서 상태를 변경하지 않는다
            if (_currentState == UnitState.Fleeing)
            {
                _isPathReady = false;
                return;
            }

            SetState(UnitState.Idle);
            _isPathReady = false;
            OnReachDestination();
            OnIdle();
        }

        #endregion

        #region ── Abstract / Virtual Callbacks ──

        /// <summary>목적지 도달 시 파생 클래스에서 구현. Moving 상태 전용.</summary>
        protected abstract void OnReachDestination();

        /// <summary>Idle 상태 진입 시 파생 클래스에서 구현.</summary>
        protected abstract void OnIdle();

        /// <summary>
        /// Fleeing 상태 진입 시 파생 클래스에서 반드시 구현해야 하는 정리 콜백.
        ///
        /// Gatherer 구현:
        ///   - CancelInvoke(nameof(SearchAndGo))
        ///   - _gatherCoroutine null 체크 후 StopCoroutine
        ///   - _targetNode?.ReleaseReservation()
        ///   - 상태 변수 초기화
        ///
        /// Builder 구현:
        ///   - CancelInvoke(nameof(SearchAndBuild))
        ///   - _buildCoroutine null 체크 후 StopCoroutine
        ///   - _targetBuilding?.ReleaseConstruction()
        ///   - 상태 변수 초기화
        /// </summary>
        protected abstract void OnFleeingEnter();

        /// <summary>
        /// 기지 안전 구역 도달 후 Fleeing에서 복귀할 때 호출되는 콜백.
        /// 기본 구현은 비어있다. 파생 클래스에서 override하여 작업 재개를 트리거한다.
        ///
        /// Gatherer: override → SearchAndGo()
        /// Builder:  override → SearchAndBuild()
        /// </summary>
        protected virtual void OnFleeingExit() { }

        /// <summary>
        /// 경로 계산 실패 시 호출.
        /// Fleeing 상태에서 실패하면 직선 이동 폴백을 활성화한다 (GDD §8 제약사항 7번).
        /// 일반 Moving 상태에서 실패하면 Idle로 복귀한다.
        /// 파생 클래스에서 override 시 반드시 base.OnPathFailed() 먼저 호출할 것.
        /// </summary>
        protected virtual void OnPathFailed()
        {
            if (_currentState == UnitState.Fleeing)
            {
                // ── Fleeing 경로 실패 폴백 ──
                // A*로 기지 경로를 찾지 못했을 때 직선으로 이동하도록 플래그 설정
                // 장애물이 있어도 기지 방향으로 이동을 포기하지 않는 것이 생존 우선 정책
                _useDirectMoveToCamp = true;
                Debug.LogWarning($"[AIUnit] '{name}' — Fleeing 경로 실패. 직선 이동 폴백 활성화.");
                return;
            }

            Debug.LogWarning($"[AIUnit] '{name}' — 경로 실패. Idle 상태로 복귀.");
            SetState(UnitState.Idle);
        }

        #endregion
    }
}
