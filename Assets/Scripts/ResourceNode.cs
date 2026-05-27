// =============================================================================
// ResourceNode.cs
// 역할  : 맵에 배치된 자원 노드(나무/돌) 상태 관리 및 채집 로직
//         Available → Reserved → Depleted → (재생 타이머) → Available 순환
// 사용법 : 자원 오브젝트에 이 컴포넌트를 추가하고 Inspector에서 수치를 설정
// 의존성 : ResourceType.cs (AIVillage.Resources 네임스페이스)
//           Collider 컴포넌트 (OnMouseDown Week 1 테스트용)
// =============================================================================

using UnityEngine;
using AIVillage.Resources;
using AIVillage.Core;

namespace AIVillage.Resources
{
    /// <summary>
    /// 맵에 배치된 단일 자원 노드. 채집 예약/채집/재생 상태 사이클을 관리한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ResourceNode : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[ResourceNode] ";

        #endregion

        #region Nested Types

        /// <summary>
        /// 자원 노드의 내부 상태.
        /// </summary>
        private enum NodeState
        {
            Available,
            Reserved,
            Depleted
        }

        #endregion

        #region Inspector Fields

        [Header("자원 설정")]

        [Tooltip("이 노드의 자원 종류 (WOOD / STONE)")]
        [SerializeField] private ResourceType _resourceType = ResourceType.WOOD;

        [Tooltip("노드의 최대 자원량 (0 이하이면 Awake에서 1로 보정)")]
        [SerializeField] private int _maxAmount = 10;

        [Header("채집 설정")]

        [Tooltip("나무 1회 채집에 걸리는 시간 (초). GDD V01-11 기준 2초")]
        [SerializeField] private float _woodGatherDuration = 2f; // GDD V01-11

        [Tooltip("돌 1회 채집에 걸리는 시간 (초). GDD V01-11 기준 5초")]
        [SerializeField] private float _stoneGatherDuration = 5f; // GDD V01-11

        [Header("재생 설정")]

        [Tooltip("나무 자원 재생에 걸리는 시간 (초). GDD V01-11 기준 30초")]
        [SerializeField] private float _woodRegenDuration = 30f; // GDD V01-11

        [Tooltip("돌 자원 재생에 걸리는 시간 (초). GDD V01-11 기준 60초")]
        [SerializeField] private float _stoneRegenDuration = 60f; // GDD V01-11

        #endregion

        #region Private State

        private int _currentAmount;
        private NodeState _state = NodeState.Available;

        /// <summary>현재 이 노드를 예약한 채집자 오브젝트. null이면 미예약.</summary>
        private GameObject _reservedBy = null;

        /// <summary>Depleted 상태에서만 증가하는 재생 타이머 (초).</summary>
        private float _regenTimer = 0f;

        /// <summary>현재 자원 종류에 맞는 채집 시간 (캐시).</summary>
        private float _gatherDuration;

        /// <summary>현재 자원 종류에 맞는 재생 시간 (캐시).</summary>
        private float _regenDuration;

        // ---- MessageBus (Week 5에서 주입) ----
        private IMessageBus _messageBus = null;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 초기화: 수치 보정 및 상태 설정.
        /// Unity가 이 오브젝트를 씬에 생성할 때 딱 1번 호출된다.
        /// </summary>
        private void Start()
        {
            GameManager.Instance?.ResourceManager?.RegisterNode(this);
        }

        private void Awake()
        {
            if (_maxAmount <= 0)
            {
                Debug.LogWarning(LOG_PREFIX + $"maxAmount가 {_maxAmount}로 유효하지 않아 1로 보정합니다. GameObject: {gameObject.name}");
                _maxAmount = 1;
            }

            _currentAmount = _maxAmount;
            _state = NodeState.Available;
            _reservedBy = null;
            _regenTimer = 0f;

            CacheDurations();
        }

        /// <summary>
        /// 매 프레임 호출. Depleted 상태일 때만 재생 타이머를 증가시킨다.
        /// </summary>
        private void Update()
        {
            // Depleted 상태가 아니면 즉시 종료 — 불필요한 연산 방지
            if (_state != NodeState.Depleted)
                return;

            _regenTimer += Time.deltaTime;

            if (_regenTimer >= _regenDuration)
            {
                Regenerate();
            }
        }

        private void OnDestroy()
        {
            GameManager.Instance?.ResourceManager?.UnregisterNode(this);
        }

        /// <summary>
        /// Week 1 테스트용: 마우스 클릭 시 현재 노드 상태를 콘솔에 출력.
        /// 동작 조건: Camera가 씬에 있고, 이 오브젝트의 Collider가 활성화 상태여야 함.
        /// </summary>
        private void OnMouseDown()
        {
            Debug.Log(LOG_PREFIX +
                $"[{gameObject.name}] Type={_resourceType} | State={_state} | " +
                $"Amount={_currentAmount}/{_maxAmount} | " +
                $"ReservedBy={(_reservedBy != null ? _reservedBy.name : "None")} | " +
                $"RegenTimer={_regenTimer:F1}/{_regenDuration:F1}s");
        }

        #endregion

        #region Public API

        /// <summary>
        /// MessageBus 의존성 주입. Week 5에서 ResourceManager가 호출한다.
        /// </summary>
        /// <param name="bus">발행에 사용할 IMessageBus 구현체</param>
        public void InjectMessageBus(IMessageBus bus)
        {
            _messageBus = bus;
        }

        /// <summary>
        /// 자원 노드 예약 시도. Available 상태일 때만 성공한다.
        /// GathererFSM이 이동 시작 시 호출한다 (Week 4).
        /// </summary>
        /// <param name="gatherer">예약을 시도하는 채집자 GameObject</param>
        /// <returns>예약 성공 여부</returns>
        public bool TryReserve(GameObject gatherer)
        {
            if (gatherer == null)
            {
                Debug.LogWarning(LOG_PREFIX + $"TryReserve: gatherer가 null입니다. Node: {gameObject.name}");
                return false;
            }

            if (_state != NodeState.Available)
                return false;

            _state = NodeState.Reserved;
            _reservedBy = gatherer;

            _messageBus?.Publish("resource.node.reserved", this);

            return true;
        }

        /// <summary>
        /// 예약 해제. Reserved → Available 로 전환한다.
        /// GathererFSM이 기지 귀환 완료 시 호출한다 (Week 4).
        /// Depleted 상태인 경우엔 상태를 변경하지 않고 예약자 참조만 정리한다.
        /// </summary>
        public void ReleaseReservation()
        {
            if (_state == NodeState.Reserved)
            {
                _state = NodeState.Available;
                _reservedBy = null;

                _messageBus?.Publish("resource.node.released", this);
            }
            else if (_state == NodeState.Depleted)
            {
                // Depleted 재생 사이클은 유지, 예약자 참조만 정리
                _reservedBy = null;
            }
            // Available 상태에서 호출 시 무시
        }

        /// <summary>
        /// 채집 실행. Reserved 상태일 때만 동작하며 요청량(또는 잔여량 전량)을 반환한다.
        /// 자원이 고갈되면 자동으로 Depleted 상태로 전환한다.
        /// GathererFSM이 gatherDuration 경과 후 1회 호출한다 (Week 4).
        /// </summary>
        /// <param name="requested">요청하는 채집량</param>
        /// <returns>실제로 채집된 자원량 (0 이상)</returns>
        public int Gather(int requested)
        {
            if (_state != NodeState.Reserved)
            {
                Debug.LogWarning(LOG_PREFIX +
                    $"Gather: state가 Reserved가 아닙니다 (현재: {_state}). Node: {gameObject.name}");
                return 0;
            }

            if (requested <= 0)
            {
                Debug.LogWarning(LOG_PREFIX + $"Gather: requested가 {requested}로 유효하지 않습니다. Node: {gameObject.name}");
                return 0;
            }

            // 잔여량보다 많이 요청하면 잔여량만 반환 (부분 수집 허용)
            int gathered = Mathf.Min(requested, _currentAmount);
            _currentAmount -= gathered;

            if (_currentAmount <= 0)
            {
                _currentAmount = 0;
                _state = NodeState.Depleted;
                _reservedBy = null;  // 예약자 참조 누수 방지
                _regenTimer = 0f;

                _messageBus?.Publish("resource.node.depleted", this);

                Debug.Log(LOG_PREFIX + $"[{gameObject.name}] {_resourceType} 노드 고갈. {_regenDuration}초 후 재생성.");
            }

            return gathered;
        }

        /// <summary>
        /// 노드가 예약 가능한(Available) 상태인지 반환한다.
        /// ResourceManager가 비예약 노드 목록 필터링 시 사용한다 (Week 2).
        /// </summary>
        public bool IsAvailable()
        {
            return _state == NodeState.Available;
        }

        /// <summary>이 노드의 자원 종류를 반환한다.</summary>
        public ResourceType GetResourceType() => _resourceType;

        /// <summary>
        /// 이 노드의 월드 좌표(XY 평면)를 반환한다.
        /// GathererFSM이 A* 경로 거리 계산 시 사용한다 (Week 3).
        /// </summary>
        public Vector2 GetWorldPosition() => new Vector2(transform.position.x, transform.position.y);

        /// <summary>현재 남아있는 자원량 (읽기 전용).</summary>
        public int CurrentAmount => _currentAmount;

        /// <summary>
        /// 현재 자원 종류에 맞는 채집 소요 시간 (초).
        /// GathererFSM이 채집 타이머 설정 시 사용한다 (Week 4).
        /// </summary>
        public float GatherDuration => _gatherDuration;

        #endregion

        #region Private Helpers

        /// <summary>
        /// 자원 종류에 따라 채집/재생 시간을 캐시한다. Awake에서 1회만 호출.
        /// </summary>
        private void CacheDurations()
        {
            switch (_resourceType)
            {
                case ResourceType.WOOD:
                    _gatherDuration = _woodGatherDuration;
                    _regenDuration  = _woodRegenDuration;
                    break;
                case ResourceType.STONE:
                    _gatherDuration = _stoneGatherDuration;
                    _regenDuration  = _stoneRegenDuration;
                    break;
                default:
                    Debug.LogWarning(LOG_PREFIX + $"CacheDurations: 알 수 없는 ResourceType({_resourceType}). 나무 기본값 적용. Node: {gameObject.name}");
                    _gatherDuration = _woodGatherDuration;
                    _regenDuration  = _woodRegenDuration;
                    break;
            }
        }

        /// <summary>
        /// 재생 타이머 만료 시 자원을 최대량으로 복구하고 Available 상태로 전환한다.
        /// </summary>
        private void Regenerate()
        {
            _currentAmount = _maxAmount;
            _state         = NodeState.Available;
            _reservedBy    = null;
            _regenTimer    = 0f;

            _messageBus?.Publish("resource.node.regenerated", this);

            Debug.Log(LOG_PREFIX + $"[{gameObject.name}] {_resourceType} 노드 재생성 완료.");
        }

        #endregion

#if UNITY_EDITOR
        #region Editor Gizmos

        /// <summary>
        /// Scene 뷰에서 선택된 노드의 상태를 색상 와이어구로 시각화.
        /// 초록=Available, 노랑=Reserved, 갈색=Depleted
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            switch (_state)
            {
                case NodeState.Available:
                    Gizmos.color = _resourceType == ResourceType.WOOD
                        ? new Color(0.2f, 0.8f, 0.2f, 0.8f)   // 나무: 초록
                        : new Color(0.6f, 0.6f, 0.6f, 0.8f);  // 돌: 회색
                    break;
                case NodeState.Reserved:
                    Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f);         // 노랑
                    break;
                case NodeState.Depleted:
                    Gizmos.color = new Color(0.4f, 0.2f, 0.1f, 0.8f);    // 갈색
                    break;
            }
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        #endregion
#endif
    }
}
