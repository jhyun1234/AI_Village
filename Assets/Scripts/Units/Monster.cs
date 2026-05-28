// =============================================================================
// Monster.cs
// 역할  : AIUnit과 독립된 적 유닛. 순찰 → 추적 → 공격 FSM으로 마을을 위협한다.
//         AIUnit 상속 없음 — Vector3.MoveTowards로만 이동 (A* 미사용).
// 사용법: Monster 프리팹 루트에 이 컴포넌트를 추가. Inspector에서 웨이포인트 배열 설정.
//         씬에 GameManager가 있어야 ThreatManager 자동 등록 가능.
//         Layer: Monster 오브젝트에는 별도 레이어 설정 불필요.
//         AIUnit 오브젝트에 'Unit' 레이어 설정 후 _unitLayerMask에 할당 필수.
// 의존성: ThreatManager (AIVillage.Core), AIUnit (AIVillage.Units)
// GDD   : §8 Monster FSM 3상태 / Week 8
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Core;
using AIVillage.Units;

namespace AIVillage.Units
{
    /// <summary>
    /// Monster의 행동 상태를 나타내는 열거형.
    /// Monster 클래스 파일 내에 함께 선언하여 관련 코드를 한 곳에 모은다 (GDD §8 제약사항 8번).
    /// </summary>
    public enum MonsterState
    {
        /// <summary>웨이포인트를 순환하며 순찰하는 상태.</summary>
        Patrolling,

        /// <summary>감지한 AIUnit을 추적하는 상태.</summary>
        Chasing,

        /// <summary>공격 사거리 내 타겟을 공격하는 상태.</summary>
        Attacking,
    }

    /// <summary>
    /// 순찰 → 추적 → 공격을 반복하는 적 유닛.
    /// Physics2D.OverlapCircle로 AIUnit을 감지하고, Vector3.MoveTowards로 이동한다.
    /// ThreatManager에 자동으로 등록/해제된다.
    /// </summary>
    public class Monster : MonoBehaviour
    {
        #region ── Constants ──

        // 공격 사거리 이탈 판정 여유 배율 — 1.2배까지는 공격 유지 (GDD §8)
        // 히스테리시스: 진입(attackRange)과 이탈(attackRange * 1.2)을 달리하여
        // Attacking ↔ Chasing 경계에서의 Flicker 현상을 방지한다
        private const float ATTACK_RANGE_HYSTERESIS = 1.2f;

        // 웨이포인트 도착 판정 제곱 거리 (0.1f * 0.1f)
        private const float WAYPOINT_ARRIVAL_SQ = 0.01f;

        #endregion

        #region ── Serialized Fields ──

        [Header("FSM 이동 설정 (GDD §8)")]
        [Tooltip("순찰/추적 이동 속도. GDD: 2f")]
        [SerializeField] private float _moveSpeed = 2f; // 기획서 수치: 이동속도 2.0f

        [Header("탐지 설정 (GDD §8)")]
        [Tooltip("AIUnit 감지 반경 (월드 단위). GDD: 3f (3타일)")]
        [SerializeField] private float _detectionRange = 3f; // 기획서 수치: 감지 범위 3f

        [Tooltip("AIUnit이 감지될 레이어 마스크. Inspector에서 'Unit' 레이어로 설정.")]
        [SerializeField] private LayerMask _unitLayerMask;

        [Header("공격 설정 (GDD §8)")]
        [Tooltip("공격이 시작되는 최대 거리. GDD: 0.6f")]
        [SerializeField] private float _attackRange = 0.6f; // 기획서 수치: 공격 사거리 0.6f

        [Tooltip("공격 1회 피해량. GDD: 10f")]
        [SerializeField] private float _attackDamage = 10f; // 기획서 수치: 공격력 10f

        [Tooltip("공격 간격 (초). GDD: 1초")]
        [SerializeField] private float _attackInterval = 1f; // 기획서 수치: 공격 간격 1s

        [Header("추적 포기 설정 (GDD §8)")]
        [Tooltip("이 거리 이상 벌어지면 추적을 포기하고 순찰로 복귀. GDD: 8f")]
        [SerializeField] private float _chaseAbandonRange = 8f; // 기획서 수치: 추적 포기 거리 8f

        [Header("기지 안전 구역 설정 (GDD §8-2)")]
        [Tooltip("이 반경 내 유닛은 감지/추적하지 않는다. _detectionRange(3f)보다 크게 유지 권장. AIUnit._baseSafeWorldRadius와 값을 맞춰야 Gatherer가 기지 안에서 무시된다.")]
        [SerializeField] private float _baseAbandonRadius = 5f;

        [Header("순찰 설정")]
        [Tooltip("순환 순찰할 웨이포인트 배열. Inspector에서 Transform 할당.")]
        [SerializeField] private Transform[] _waypoints;

        #endregion

        #region ── Private FSM State ──

        private MonsterState _currentState  = MonsterState.Patrolling;
        private AIUnit       _target;          // 현재 추적/공격 대상 유닛
        private int          _waypointIndex;   // 현재 순찰 웨이포인트 인덱스
        private Coroutine    _attackCoroutine; // 진행 중인 공격 코루틴

        #endregion

        #region ── Unity Lifecycle ──

        private void Start()
        {
            // ThreatManager에 자동 등록. GameManager나 ThreatManager가 없으면 조용히 무시
            GameManager.Instance?.ThreatManager?.RegisterMonster(this);

            // 웨이포인트가 없으면 경고
            if (_waypoints == null || _waypoints.Length == 0)
                Debug.LogWarning($"[Monster] '{name}' — 웨이포인트가 설정되지 않았습니다. 순찰 불가.");
        }

        /// <summary>
        /// 매 프레임 현재 FSM 상태에 맞는 동작을 수행한다.
        /// Monster는 항상 활성 상태이므로 enabled 토글을 사용하지 않는다.
        /// Update 내부에서 GetComponent 호출이 없도록 Collider2D hit에서만 조회한다.
        /// </summary>
        private void Update()
        {
            switch (_currentState)
            {
                case MonsterState.Patrolling:
                    UpdatePatrolling();
                    break;

                case MonsterState.Chasing:
                    UpdateChasing();
                    break;

                case MonsterState.Attacking:
                    UpdateAttacking();
                    break;
            }
        }

        private void OnDestroy()
        {
            // 씬 종료 시 GameManager가 먼저 파괴될 수 있으므로 ?. 연산자 사용
            GameManager.Instance?.ThreatManager?.UnregisterMonster(this);
        }

        #endregion

        #region ── FSM: Patrolling ──

        /// <summary>
        /// 웨이포인트를 순환하며 이동하고, 매 프레임 AIUnit 감지를 시도한다.
        /// 감지 성공 시 Chasing 상태로 전환한다.
        /// </summary>
        private void UpdatePatrolling()
        {
            // ── 순찰 이동 ──
            MoveTowardsWaypoint();

            // ── AIUnit 감지 ──
            // Physics2D.OverlapCircle: 지정 위치 반경 내 첫 번째 Collider를 반환
            // _unitLayerMask로 AIUnit 레이어만 필터링하여 불필요한 GetComponent 호출 최소화
            Collider2D hit = Physics2D.OverlapCircle(transform.position, _detectionRange, _unitLayerMask);

            if (hit != null)
            {
                // OverlapCircle이 레이어 필터 적용 완료 → GetComponent는 1회만 호출됨
                AIUnit unit = hit.GetComponent<AIUnit>();
                // 기지 안전 구역 내 유닛은 추적하지 않는다
                // IsTargetNearBase()와 동일 조건을 사전에 체크하여
                // Chasing → IsTargetNearBase → Patrolling → 즉시 재감지 루프를 방지한다
                if (unit != null && !IsUnitNearBase(unit))
                {
                    _target = unit;
                    TransitionToChasing();
                }
            }
        }

        /// <summary>
        /// 현재 웨이포인트를 향해 MoveTowards로 이동하고, 도착하면 다음 인덱스로 넘어간다.
        /// </summary>
        private void MoveTowardsWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            // null인 웨이포인트 슬롯은 건너뜀 (Inspector 미할당 방어)
            if (_waypoints[_waypointIndex] == null)
            {
                AdvanceWaypoint();
                return;
            }

            Vector3 waypointPos = _waypoints[_waypointIndex].position;

            // Formula: newPos = MoveTowards(current, target, speed * deltaTime)
            transform.position = Vector3.MoveTowards(
                transform.position,
                waypointPos,
                _moveSpeed * Time.deltaTime);

            // 도착 판정: 이동 후 위치 기준 제곱 거리 비교 (sqrt 제거)
            float dx = waypointPos.x - transform.position.x;
            float dy = waypointPos.y - transform.position.y;
            if (dx * dx + dy * dy < WAYPOINT_ARRIVAL_SQ)
                AdvanceWaypoint();
        }

        /// <summary>웨이포인트 인덱스를 순환 증가시킨다 (마지막 → 첫 번째).</summary>
        private void AdvanceWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;
            _waypointIndex = (_waypointIndex + 1) % _waypoints.Length;
        }

        /// <summary>
        /// 현재 위치에서 가장 가까운 웨이포인트 인덱스를 찾아 _waypointIndex를 초기화한다.
        /// 추적 포기 후 순찰 복귀 시 호출하여 자연스러운 재개 지점을 설정한다.
        /// </summary>
        private void ResetToNearestWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0)
            {
                _waypointIndex = 0;
                return;
            }

            float nearestSq    = float.MaxValue;
            int   nearestIndex = 0;

            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == null) continue;

                float dx     = _waypoints[i].position.x - transform.position.x;
                float dy     = _waypoints[i].position.y - transform.position.y;
                float distSq = dx * dx + dy * dy;

                if (distSq < nearestSq)
                {
                    nearestSq    = distSq;
                    nearestIndex = i;
                }
            }

            _waypointIndex = nearestIndex;
        }

        #endregion

        #region ── FSM: Chasing ──

        /// <summary>
        /// 타겟을 향해 이동한다. 매 프레임 다음 조건을 체크하여 상태를 전환한다.
        /// 1. 타겟 null → Patrolling 복귀
        /// 2. 거리 > _chaseAbandonRange → Patrolling 복귀
        /// 3. 타겟이 기지 반경(BASE_ABANDON_RADIUS) 내부 → Patrolling 복귀 (GDD §8)
        /// 4. 거리 <= _attackRange → Attacking 전환
        /// </summary>
        private void UpdateChasing()
        {
            // ── 타겟 유효성 검사 ──
            if (_target == null)
            {
                TransitionToPatrolling();
                return;
            }

            float dx     = _target.transform.position.x - transform.position.x;
            float dy     = _target.transform.position.y - transform.position.y;
            float distSq = dx * dx + dy * dy;

            // ── 추적 포기 판정 1: 너무 멀리 도망침 ──
            if (distSq > _chaseAbandonRange * _chaseAbandonRange)
            {
                TransitionToPatrolling();
                return;
            }

            // ── 추적 포기 판정 2: 타겟이 기지 반경 안으로 진입 ──
            // 기지 주변은 몬스터가 진입하지 않도록 설계됨 (GDD §8)
            if (IsTargetNearBase())
            {
                TransitionToPatrolling();
                return;
            }

            // ── 공격 사거리 진입 → Attacking ──
            if (distSq <= _attackRange * _attackRange)
            {
                TransitionToAttacking();
                return;
            }

            // ── 타겟 방향으로 이동 ──
            // Formula: newPos = MoveTowards(current, target.pos, speed * deltaTime)
            transform.position = Vector3.MoveTowards(
                transform.position,
                _target.transform.position,
                _moveSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 타겟이 기지 반경 내부에 있는지 확인한다.
        /// 몬스터는 기지 반경 내 유닛을 추적하지 않는다 (GDD §8).
        /// </summary>
        private bool IsTargetNearBase()
        {
            return IsUnitNearBase(_target);
        }

        /// <summary>
        /// 임의의 유닛이 기지 반경 내부에 있는지 확인한다.
        /// UpdatePatrolling 감지 단계에서 기지 안 유닛을 사전 필터링하는 데 사용한다.
        /// </summary>
        private bool IsUnitNearBase(AIUnit unit)
        {
            if (unit == null) return false;

            GameManager gm = GameManager.Instance;
            if (gm == null) return false;

            Vector2 basePos = gm.BasePosition;
            float dx = unit.transform.position.x - basePos.x;
            float dy = unit.transform.position.y - basePos.y;

            return (dx * dx + dy * dy) <= _baseAbandonRadius * _baseAbandonRadius;
        }

        #endregion

        #region ── FSM: Attacking ──

        /// <summary>
        /// 공격 상태: 이동하지 않고 AttackRoutine 코루틴이 주기적으로 피해를 준다.
        /// 매 프레임 타겟 유효성 및 거리를 체크하여 Chasing으로 복귀할지 결정한다.
        /// </summary>
        private void UpdateAttacking()
        {
            // ── 타겟 유효성 검사 ──
            if (_target == null)
            {
                StopAttackCoroutine();
                // [PR Fix]: R-001 — 타겟이 null이면 Chasing이 아닌 Patrolling으로 전환 (추적할 대상이 없으므로 순찰 복귀)
                TransitionToPatrolling();
                return;
            }

            float dx     = _target.transform.position.x - transform.position.x;
            float dy     = _target.transform.position.y - transform.position.y;
            float distSq = dx * dx + dy * dy;

            // ── 이탈 판정: 공격 사거리 * ATTACK_RANGE_HYSTERESIS 이상이면 다시 추적 ──
            // 진입(attackRange)과 이탈(attackRange * 1.2)을 달리하여 Flicker 방지
            float abandonRangeSq = (_attackRange * ATTACK_RANGE_HYSTERESIS)
                                 * (_attackRange * ATTACK_RANGE_HYSTERESIS);

            if (distSq > abandonRangeSq)
            {
                StopAttackCoroutine();
                TransitionToChasing();
            }
        }

        /// <summary>
        /// 일정 간격으로 타겟에게 피해를 주는 공격 코루틴.
        /// Attacking 상태로 전환될 때 시작되고, 이탈 또는 타겟 사망 시 중단된다.
        /// </summary>
        private IEnumerator AttackRoutine()
        {
            while (true)
            {
                // 다음 공격까지 대기
                yield return new WaitForSeconds(_attackInterval);

                // 코루틴 재개 시점에 타겟이 파괴됐을 수 있으므로 재확인
                if (_target == null) yield break;

                // 타겟에게 피해 적용 (AIUnit.TakeDamage)
                _target.TakeDamage(_attackDamage);
                // [PR Fix]: R-003 — null 체크 직후이므로 ?. 불필요. _target.name 으로 직접 참조
                Debug.Log($"[Monster] '{name}' → '{_target.name}' 에게 {_attackDamage} 피해!");
            }
        }

        /// <summary>진행 중인 공격 코루틴을 안전하게 중단한다.</summary>
        private void StopAttackCoroutine()
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
        }

        #endregion

        #region ── FSM: State Transitions ──

        /// <summary>
        /// Patrolling 상태로 전환한다.
        /// 타겟을 해제하고 가장 가까운 웨이포인트부터 순찰을 재개한다.
        /// </summary>
        private void TransitionToPatrolling()
        {
            _target       = null;
            _currentState = MonsterState.Patrolling;
            ResetToNearestWaypoint();
            Debug.Log($"[Monster] '{name}' → Patrolling 복귀.");
        }

        /// <summary>Chasing 상태로 전환한다.</summary>
        private void TransitionToChasing()
        {
            _currentState = MonsterState.Chasing;
            Debug.Log($"[Monster] '{name}' → Chasing: '{_target?.name}'");
        }

        /// <summary>
        /// Attacking 상태로 전환하고 공격 코루틴을 시작한다.
        /// 이미 코루틴이 실행 중이라면 중복 시작을 방지한다.
        /// </summary>
        private void TransitionToAttacking()
        {
            _currentState = MonsterState.Attacking;

            // 코루틴 중복 실행 방지: null일 때만 시작
            if (_attackCoroutine == null)
                _attackCoroutine = StartCoroutine(AttackRoutine());

            Debug.Log($"[Monster] '{name}' → Attacking: '{_target?.name}'");
        }

        #endregion

#if UNITY_EDITOR
        #region ── Editor Gizmos ──

        /// <summary>씬 뷰에서 감지 범위, 공격 범위, 추적 포기 범위를 시각적으로 표시한다.</summary>
        private void OnDrawGizmosSelected()
        {
            // 감지 범위: 노란색
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            // 공격 범위: 빨간색
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            // 추적 포기 범위: 파란색 (반투명)
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _chaseAbandonRange);

            // 현재 타겟으로 선 표시 (추적/공격 중)
            if (_target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _target.transform.position);
            }
        }

        #endregion
#endif
    }
}
