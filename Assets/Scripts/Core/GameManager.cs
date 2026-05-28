// =============================================================================
// GameManager.cs
// 역할  : 씬 루트 싱글톤. 시작 자원 세팅, Tick 루프, 승리/패배 구조, 자원 입출금 담당.
// 사용법: 빈 GameObject "GameManager"에 컴포넌트로 추가. Inspector에서 시작 자원 수치 확인.
// 의존성: ResourceManager (동일 GameObject에 자동 추가됨)
// GDD   : §11 GameManager / R-002 Tick 0.5s / Week 2 시작 자원 세팅
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Resources;

namespace AIVillage.Core
{
    /// <summary>
    /// 씬 전체 흐름을 관리하는 싱글톤 루트 매니저.
    /// 시작 자원 세팅, Tick 기반 자동 생성 체크, 승리/패배 조건 체크,
    /// 자원 입출금(AddResource / SpendResource)을 담당한다.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>씬 전역 GameManager 인스턴스.</summary>
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] 중복 인스턴스 감지 — 이 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CacheComponents();
            InitializeResources();
        }

        #endregion

        #region Inspector Fields

        [Header("시작 자원 (GDD)")]
        [SerializeField, Tooltip("게임 시작 시 보유 나무 수량. GDD: 15")]
        private int _startingWood = 15; // GDD

        [SerializeField, Tooltip("게임 시작 시 보유 돌 수량. GDD: 8")]
        private int _startingStone = 8; // GDD

        [Header("Tick 설정 (GDD R-002)")]
        [SerializeField, Tooltip("Tick 간격(초). GDD R-002: 0.5s")]
        private float _tickInterval = 0.5f; // GDD R-002

        [Header("승리 조건 (GDD Week 10)")]
        [SerializeField, Tooltip("승리에 필요한 인구 수. GDD: 20")]
        private int _victoryPopulation = 20; // GDD

        [Header("기지 위치")]
        [SerializeField, Tooltip("House가 배치된 월드 좌표. Week 6에서 House가 설정한다.")]
        private Vector2 _basePosition = Vector2.zero;

        [Header("유닛 자동 스폰 (GDD Week 7)")]
        [SerializeField, Tooltip("자동 스폰할 Gatherer 프리팹. Inspector에서 할당 필요.")]
        private GameObject _gathererPrefab;

        [SerializeField, Tooltip("Gatherer 1기 스폰에 필요한 나무 수량.")]
        private int _gathererSpawnCostWood = 10;

        [SerializeField, Tooltip("스폰 위치 랜덤 오프셋 반경 (월드 단위).")]
        private float _spawnRadius = 1f;

        [SerializeField, Tooltip("연속 스폰 방지 쿨다운 (초). 최소 1Tick 이상 권장.")]
        private float _spawnCooldown = 5f;

        [Header("위협 감지 (GDD §8)")]
        [Tooltip("유닛 주변 위협 감지 반경 (월드 단위). GDD: 3f (3타일)")]
        [SerializeField] private float _threatDetectionRadius = 3f; // 기획서 수치: 위협 감지 반경 3f

        #endregion

        #region Private State

        private int _currentWood;
        private int _currentStone;
        private bool  _isGameOver;
        private float _lastSpawnTime = -999f;
        private Coroutine _tickCoroutine;

        #endregion

        #region Public Properties

        /// <summary>현재 나무 보유량.</summary>
        public int CurrentWood => _currentWood;

        /// <summary>현재 돌 보유량.</summary>
        public int CurrentStone => _currentStone;

        /// <summary>기지(House) 월드 좌표. Week 6에서 House가 설정.</summary>
        public Vector2 BasePosition
        {
            get => _basePosition;
            set => _basePosition = value;
        }

        /// <summary>ResourceManager 참조 (GameManager.Instance.ResourceManager 로 접근).</summary>
        public ResourceManager ResourceManager { get; private set; }

        /// <summary>MessageBus 참조 (GameManager.Instance.MessageBus 로 접근).</summary>
        public MessageBus MessageBus { get; private set; }

        /// <summary>BuildingManager 참조 (GameManager.Instance.BuildingManager 로 접근).</summary>
        public BuildingManager BuildingManager { get; private set; }

        /// <summary>PopulationManager 참조 (GameManager.Instance.PopulationManager 로 접근).</summary>
        public PopulationManager PopulationManager { get; private set; }

        /// <summary>ThreatManager 참조 (GameManager.Instance.ThreatManager 로 접근).</summary>
        public ThreatManager ThreatManager { get; private set; }

        #endregion

        #region Initialization

        /// <summary>Awake에서 컴포넌트를 캐싱한다. Update/FixedUpdate 내 호출 금지.</summary>
        private void CacheComponents()
        {
            ResourceManager = GetComponent<ResourceManager>();
            if (ResourceManager == null)
            {
                Debug.LogWarning("[GameManager] ResourceManager 컴포넌트를 찾지 못했습니다 — 자동으로 추가합니다.");
                ResourceManager = gameObject.AddComponent<ResourceManager>();
            }

            MessageBus = GetComponent<MessageBus>();
            if (MessageBus == null)
                MessageBus = gameObject.AddComponent<MessageBus>();

            BuildingManager = GetComponent<BuildingManager>();
            if (BuildingManager == null)
                BuildingManager = gameObject.AddComponent<BuildingManager>();

            PopulationManager = GetComponent<PopulationManager>();
            if (PopulationManager == null)
                PopulationManager = gameObject.AddComponent<PopulationManager>();

            ThreatManager = GetComponent<ThreatManager>();
            if (ThreatManager == null)
                ThreatManager = gameObject.AddComponent<ThreatManager>();
        }

        /// <summary>Start에서 MessageBus 이벤트를 구독한다 (Awake 시점에는 MessageBus가 미준비).</summary>
        private void Start()
        {
            MessageBus.Subscribe("resource.node.depleted",   OnNodeDepleted);
            MessageBus.Subscribe("resource.node.regenerated", OnNodeRegenerated);
        }

        private void OnNodeDepleted(object payload)
        {
            if (payload is AIVillage.Resources.ResourceNode node)
                Debug.Log($"[GameManager] 노드 고갈 감지: {node.name}");
        }

        private void OnNodeRegenerated(object payload)
        {
            if (payload is AIVillage.Resources.ResourceNode node)
                Debug.Log($"[GameManager] 노드 재생성 감지: {node.name}");
        }

        /// <summary>GDD 수치로 시작 자원을 초기화하고 Tick 루프를 시작한다.</summary>
        private void InitializeResources()
        {
            _currentWood  = _startingWood;
            _currentStone = _startingStone;
            _isGameOver   = false;

            Debug.Log($"[GameManager] 시작 자원 세팅 완료 — 나무: {_currentWood}, 돌: {_currentStone}");

            StartTick();
        }

        #endregion

        #region Tick Loop

        /// <summary>Tick 코루틴을 시작한다.</summary>
        private void StartTick()
        {
            if (_tickCoroutine != null)
                StopCoroutine(_tickCoroutine);

            _tickCoroutine = StartCoroutine(TickLoop());
        }

        /// <summary>GDD R-002: 0.5초마다 Tick을 발생시킨다.</summary>
        private IEnumerator TickLoop()
        {
            while (!_isGameOver)
            {
                yield return new WaitForSeconds(_tickInterval);
                OnTick();
            }
        }

        /// <summary>
        /// 매 Tick 호출 진입점.
        /// 유닛 자동 생성 체크 → 위협 감지 체크 → 승리/패배 조건 체크 순서로 실행한다.
        /// </summary>
        private void OnTick()
        {
            CheckAutoSpawn();
            CheckThreatForAllUnits(); // Week 8: 몬스터 위협 감지
            CheckWinLoseCondition();
        }

        #endregion

        #region Auto Spawn (Week 7)

        private void CheckAutoSpawn()
        {
            if (_gathererPrefab == null) return;
            if (PopulationManager == null || !PopulationManager.HasRoom) return;
            if (_currentWood < _gathererSpawnCostWood) return;
            if (Time.time - _lastSpawnTime < _spawnCooldown) return;

            SpendResource(ResourceType.WOOD, _gathererSpawnCostWood);
            _lastSpawnTime = Time.time;

            Vector2 offset   = Random.insideUnitCircle * _spawnRadius;
            Vector3 spawnPos = new Vector3(_basePosition.x + offset.x,
                                           _basePosition.y + offset.y, 0f);

            Instantiate(_gathererPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"[GameManager] Gatherer 스폰 — 인구: {PopulationManager.CurrentPop}/{PopulationManager.MaxPop}");
        }

        #endregion

        #region Threat Detection (Week 8)

        /// <summary>
        /// 매 Tick 모든 살아있는 유닛의 주변에 몬스터가 있는지 확인하고,
        /// 위협이 감지되면 해당 유닛에게 SetFleeing()을 호출한다.
        ///
        /// 스냅샷 순회를 사용하여 SetFleeing() 내부에서 _units가 변경되어도 안전하다.
        /// </summary>
        private void CheckThreatForAllUnits()
        {
            if (ThreatManager == null || PopulationManager == null) return;

            // 스냅샷: foreach 순회 중 유닛 추가/제거가 발생해도 안전
            AIVillage.Units.AIUnit[] snapshot = PopulationManager.GetAllUnitsSnapshot();

            foreach (AIVillage.Units.AIUnit unit in snapshot)
            {
                // 파괴된 유닛은 건너뜀 (OnDestroy 이전에 스냅샷에 포함된 경우)
                if (unit == null) continue;

                Vector2 unitPos = unit.transform.position;

                // 이 유닛 주변 _threatDetectionRadius 이내에 몬스터가 있으면 도주 지시
                AIVillage.Units.Monster nearest =
                    ThreatManager.GetNearestMonster(unitPos, _threatDetectionRadius);

                if (nearest != null)
                    unit.SetFleeing();
            }
        }

        #endregion

        #region Win / Lose Condition (Week 10)

        // TODO: Week 10 — 실제 인구, TownHall 완성, 유닛 생존 여부 연동
        private void CheckWinLoseCondition()
        {
            // TODO: Week 10
        }

        /// <summary>게임 종료 상태로 전환하고 Tick 루프를 멈춘다.</summary>
        private void TriggerGameOver(bool isVictory)
        {
            if (_isGameOver) return;

            _isGameOver = true;
            string result = isVictory ? "승리" : "패배";
            Debug.Log($"[GameManager] 게임 종료 — {result}");

            // TODO: Week 10 — UI 연동, 씬 전환 등
        }

        #endregion

        #region Resource API

        /// <summary>
        /// 자원을 보유량에 추가한다. GathererFSM의 자원 반납 시 호출.
        /// </summary>
        /// <param name="type">자원 종류</param>
        /// <param name="amount">추가할 양 (양수)</param>
        public void AddResource(ResourceType type, int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[GameManager] AddResource — 유효하지 않은 amount: {amount}");
                return;
            }

            switch (type)
            {
                case ResourceType.WOOD:
                    _currentWood += amount;
                    Debug.Log($"[GameManager] 나무 +{amount} → 총 {_currentWood}");
                    break;
                case ResourceType.STONE:
                    _currentStone += amount;
                    Debug.Log($"[GameManager] 돌 +{amount} → 총 {_currentStone}");
                    break;
                default:
                    Debug.LogWarning($"[GameManager] AddResource — 알 수 없는 ResourceType: {type}");
                    break;
            }

            MessageBus?.Publish("resource.deposited", new ResourceDepositedEvent(type, amount));
        }

        /// <summary>
        /// 자원을 소비한다. 보유량이 부족하면 false를 반환하고 자원을 차감하지 않는다.
        /// </summary>
        /// <param name="type">자원 종류</param>
        /// <param name="amount">소비할 양 (양수)</param>
        /// <returns>소비 성공 여부</returns>
        public bool SpendResource(ResourceType type, int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[GameManager] SpendResource — 유효하지 않은 amount: {amount}");
                return false;
            }

            switch (type)
            {
                case ResourceType.WOOD:
                    if (_currentWood < amount)
                    {
                        Debug.LogWarning($"[GameManager] 나무 부족 — 필요: {amount}, 보유: {_currentWood}");
                        return false;
                    }
                    _currentWood -= amount;
                    Debug.Log($"[GameManager] 나무 -{amount} → 총 {_currentWood}");
                    return true;

                case ResourceType.STONE:
                    if (_currentStone < amount)
                    {
                        Debug.LogWarning($"[GameManager] 돌 부족 — 필요: {amount}, 보유: {_currentStone}");
                        return false;
                    }
                    _currentStone -= amount;
                    Debug.Log($"[GameManager] 돌 -{amount} → 총 {_currentStone}");
                    return true;

                default:
                    Debug.LogWarning($"[GameManager] SpendResource — 알 수 없는 ResourceType: {type}");
                    return false;
            }
        }

        /// <summary>특정 자원의 현재 보유량을 반환한다.</summary>
        public int GetResource(ResourceType type)
        {
            return type switch
            {
                ResourceType.WOOD  => _currentWood,
                ResourceType.STONE => _currentStone,
                _                  => 0
            };
        }

        #endregion

        #region Lifecycle

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        #endregion
    }

    /// <summary>resource.deposited 이벤트 페이로드.</summary>
    public readonly struct ResourceDepositedEvent
    {
        public readonly ResourceType Type;
        public readonly int          Amount;

        public ResourceDepositedEvent(ResourceType type, int amount)
        {
            Type   = type;
            Amount = amount;
        }
    }
}
