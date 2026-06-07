// =============================================================================
// Warrior.cs
// 역할  : 전투 유닛. Barracks에서 훈련되며 CombatAlertQueue의 파견 명령으로 Monster와 싸운다.
//         Standby(숨김) → Moving(파견) → Fighting(전투) → Moving(귀환) → Standby 순환.
// 사용법: Warrior 프리팹 루트에 이 컴포넌트를 추가.
//         Barracks.TrainingLoop()에서 Instantiate 후 HomeBarracks를 자동 설정.
//         CombatAlertQueue가 TryDispatch()로 파견 명령을 전달한다.
// 의존성: AIVillage.Buildings.Barracks, AIVillage.Units.AIUnit, AIVillage.Units.WarriorHealHelper
// GDD   : §v0.2-3 Warrior 유닛 / §v0.2-4 CombatAlertQueue 연동
//         패배 조건 미포함: base.Start() 미호출 → PopulationManager 미등록
//         대기 은신: SpriteRenderer.enabled=false + Collider2D.enabled=false (SetActive 금지)
//         SetFleeing() 무력화: override 빈 메서드 (Warrior는 도주 없음)
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Buildings;
using AIVillage.Core;

namespace AIVillage.Units
{
    [RequireComponent(typeof(WarriorHealHelper))]
    public sealed class Warrior : AIUnit
    {
        #region ── Serialized Fields ──

        [Header("전투 설정 (GDD v0.2-3)")]
        [Tooltip("공격 1회 피해량.")]
        [SerializeField] private float _attackDamage = 15f;

        [Tooltip("공격이 시작되는 최대 거리 (월드 단위).")]
        [SerializeField] private float _attackRange = 1.2f;

        [Tooltip("공격 간격 (초).")]
        [SerializeField] private float _attackInterval = 1f;

        [Tooltip("도착 후 몬스터 탐색 반경.")]
        [SerializeField] private float _detectionRadius = 5f;

        [Tooltip("파견 허용 최소 체력 비율 (0~1). 0.8 = 최대 체력의 80% 이상이어야 파견.")]
        [SerializeField, Range(0f, 1f)] private float _dispatchHpThreshold = 0.8f;

        #endregion

        #region ── Private State ──

        private SpriteRenderer   _spriteRenderer;
        private Collider2D       _collider2D;
        private WarriorHealHelper _healHelper;

        private Monster   _targetMonster;
        private Coroutine _attackCoroutine        = null;
        private bool      _isReturning            = false;
        private bool      _attackRoutineRunning    = false;

        #endregion

        #region ── Public Fields ──

        /// <summary>이 Warrior를 훈련시킨 훈련소. Barracks.TrainingLoop()에서 할당.</summary>
        [HideInInspector] public Barracks HomeBarracks;

        #endregion

        #region ── Properties ──

        /// <summary>
        /// CombatAlertQueue가 파견 가능 여부를 조회하는 프로퍼티.
        /// Standby 상태이며 체력 80% 이상일 때만 true.
        /// </summary>
        public bool IsAvailableForDispatch =>
            _currentState == UnitState.Standby && Hp >= MaxHp * _dispatchHpThreshold;

        #endregion

        #region ── Unity Lifecycle ──

        protected override void Awake()
        {
            base.Awake();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider2D     = GetComponent<Collider2D>();
            _healHelper     = GetComponent<WarriorHealHelper>();
        }

        // base.Start() 미호출 — Warrior는 PopulationManager에 미등록 (패배 조건 분리)
        protected override void Start()
        {
            EnterStandby();
        }

        protected override void Update()
        {
            // Fighting 상태에서 타겟 사망 감지 (SetState(Fighting) → enabled=true)
            if (_currentState == UnitState.Fighting)
            {
                UpdateFighting();
                return;
            }
            base.Update();
        }

        protected override void OnDestroy()
        {
            HomeBarracks?.ReleaseSlot(this);
            base.OnDestroy(); // UnregisterUnit은 Contains 체크로 미등록 시 무해
        }

        #endregion

        #region ── Public API (CombatAlertQueue) ──

        /// <summary>
        /// CombatAlertQueue가 파견 명령을 전달할 때 호출한다.
        /// Standby 상태이며 체력 조건을 충족할 때만 성공한다.
        /// </summary>
        /// <returns>파견 성공 여부</returns>
        public bool TryDispatch(Vector2 targetPosition)
        {
            if (!IsAvailableForDispatch) return false;

            ExitStandby();
            _isReturning = false;
            SetDestination(new Vector3(targetPosition.x, targetPosition.y, 0f));

            GameManager.Instance?.MessageBus?.Publish("warrior.dispatched", (Vector2)transform.position);
#if UNITY_EDITOR
            Debug.Log($"[Warrior] '{name}' 파견 → {targetPosition}");
#endif
            return true;
        }

        /// <summary>
        /// WarriorHealHelper가 Standby 중 HP 회복을 적용할 때 호출한다.
        /// internal이므로 같은 어셈블리 내에서만 접근 가능.
        /// </summary>
        internal void ApplyHeal(float amount)
        {
            _hp = Mathf.Min(_hp + amount, _maxHp);
        }

        #endregion

        #region ── 위협 반응 무력화 ──

        // Warrior는 도주하지 않는다. 위협 감지는 CombatAlertQueue가 처리한다.
        public override void SetFleeing() { }

        // OnThreatDetected도 무력화: Warrior는 위협에 스스로 반응하지 않는다.
        public override void OnThreatDetected(Monster monster) { }

        #endregion

        #region ── AIUnit Abstract 구현 ──

        protected override void OnReachDestination()
        {
            if (_isReturning)
            {
                // 기지 도달 → 대기 복귀
                EnterStandby();
                return;
            }

            // 파견 좌표 도달 → 근처 몬스터 탐색
            ThreatManager tm = GameManager.Instance?.ThreatManager;
            Monster nearby = tm?.GetNearestMonster(transform.position, _detectionRadius);

            if (nearby != null)
                EnterFighting(nearby);
            else
                StartReturning(); // 몬스터 없으면 귀환
        }

        protected override void OnIdle()
        {
            // AIUnit.OnArrival()은 OnReachDestination() 후 OnIdle()을 항상 호출한다.
            // OnReachDestination()에서 이미 상태가 변경됐을 수 있으므로
            // 여전히 Idle 상태일 때만 Standby로 전환한다.
            if (_currentState == UnitState.Idle)
                EnterStandby();
        }

        protected override void OnFleeingEnter()
        {
            // Warrior는 도주하지 않으므로 비어있어야 한다
            // SetFleeing()이 override되어 진입 자체가 불가하지만 안전 장치로 유지
        }

        protected override void OnPathFailed()
        {
            // Warrior는 Fleeing 상태가 없으므로 경로 실패 시 무조건 Standby로 복귀.
            // base는 Fleeing일 때 직선이동 폴백을 활성화하지만 Warrior에는 불필요하다.
            EnterStandby();
        }

        #endregion

        #region ── Fighting Update ──

        private void UpdateFighting()
        {
            // 타겟 파괴 감지 → 귀환 시작. StartReturning()이 AttackRoutine을 명시적 중단한다.
            if (_targetMonster == null)
                StartReturning();
        }

        #endregion

        #region ── State Transitions ──

        private void EnterStandby()
        {
            // 실행 중인 AttackRoutine 명시적 중단 (OnPathFailed 등 직접 진입 경로 대비)
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
            _attackRoutineRunning = false;
            _targetMonster        = null;

            // SetState(Standby) → enabled = false (Moving/Fleeing/Fighting 아님)
            SetState(UnitState.Standby);

            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            if (_collider2D     != null) _collider2D.enabled     = false;

            _healHelper?.StartHealing();

            // 기지 위치로 이동 (텔레포트: 대기 중에는 어디에 있어도 무관)
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                Vector2 home = gm.BasePosition;
                transform.position = new Vector3(home.x, home.y, 0f);
            }

            GameManager.Instance?.MessageBus?.Publish("warrior.returned", (Vector2)transform.position);
#if UNITY_EDITOR
            Debug.Log($"[Warrior] '{name}' → Standby (대기 / HP: {_hp:F1}/{_maxHp:F1})");
#endif
        }

        private void ExitStandby()
        {
            _healHelper?.StopHealing();
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            if (_collider2D     != null) _collider2D.enabled     = true;
        }

        private void EnterFighting(Monster target)
        {
            // 재진입 방지: 이미 AttackRoutine이 실행 중이면 무시
            if (_attackRoutineRunning) return;

            _targetMonster = target;
            SetState(UnitState.Fighting); // enabled = true
            _attackCoroutine = StartCoroutine(AttackRoutine());
#if UNITY_EDITOR
            Debug.Log($"[Warrior] '{name}' → Fighting (대상: {target.name})");
#endif
        }

        private void StartReturning()
        {
            // 실행 중인 AttackRoutine을 명시적으로 중단 (지연된 StartReturning 중복 방지)
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }
            _attackRoutineRunning = false;
            _isReturning          = true;
            _targetMonster        = null;

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                EnterStandby();
                return;
            }

            Vector2 home = gm.BasePosition;
            SetDestination(new Vector3(home.x, home.y, 0f));
#if UNITY_EDITOR
            Debug.Log($"[Warrior] '{name}' → Moving (귀환)");
#endif
        }

        #endregion

        #region ── Attack Coroutine ──

        private IEnumerator AttackRoutine()
        {
            _attackRoutineRunning = true;

            while (_currentState == UnitState.Fighting && _targetMonster != null)
            {
                float dist = Vector2.Distance(transform.position, _targetMonster.transform.position);

                if (dist <= _attackRange)
                {
                    // TakeDamage 내부에서 HP 0 시 Destroy(gameObject)를 호출한다.
                    // Destroy는 프레임 끝에 실행되므로 이 라인 직후에도 _targetMonster 참조는 유효하다.
                    // UpdateFighting()의 null 체크가 다음 프레임에 사망을 감지하여 StartReturning()을 호출한다.
                    _targetMonster.TakeDamage(_attackDamage);
#if UNITY_EDITOR
                    Debug.Log($"[Warrior] '{name}' 공격 → '{_targetMonster.name}' ({_attackDamage} 피해)");
#endif
                }

                yield return new WaitForSeconds(_attackInterval);
            }

            _attackRoutineRunning = false;
            _attackCoroutine      = null;
            // UpdateFighting()이 _targetMonster == null 감지의 단일 책임자이므로
            // 여기서 StartReturning()을 중복 호출하지 않는다.
        }

        #endregion

        #region ── Gizmos ──

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 공격 범위
            Gizmos.color = _currentState == UnitState.Fighting
                ? new Color(1f, 0.2f, 0.2f, 0.6f)  // 빨강: 전투 중
                : new Color(0.2f, 0.5f, 1f, 0.4f);  // 파랑: 대기/이동
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            // 탐지 범위
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            // 타겟 연결선
            if (_targetMonster != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _targetMonster.transform.position);
            }
        }
#endif

        #endregion
    }
}
