// =============================================================================
// Barracks.cs
// 역할  : 훈련소 건물. 완공 후 Warrior 유닛을 최대 3마리까지 자동 훈련시킨다.
//         슬롯이 비면 자원이 확보되는 즉시 훈련을 재개한다.
//         완공 시 CombatAlertQueue에 등록하여 Warrior 조회를 FindObjectsOfType 없이 지원한다.
//         v0.2-5: CollectUnequippedWarriors() 추가 — Blacksmith가 비무장 Warrior를 수집할 때 사용.
// 사용법: 프리팹에 이 컴포넌트 추가. Inspector에서 _warriorPrefab 할당 필수.
//         PlayerController 4번 키로 건설 배치.
// 의존성: AIVillage.Core.GameManager, AIVillage.Core.CombatAlertQueue, AIVillage.Units.Warrior
// GDD   : §v0.2-3 훈련소 / Warrior 유닛
//         건설 비용: 나무 15, 돌 10 (Reset()에서 자동 세팅)
//         훈련 비용: 나무 5, 돌 5 / 훈련 시간: 10초
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIVillage.Core;
using AIVillage.Resources;
using AIVillage.Units;

namespace AIVillage.Buildings
{
    public sealed class Barracks : Building
    {
        #region ── Serialized Fields ──

        [Header("훈련 설정 (GDD v0.2-3)")]
        [Tooltip("훈련할 Warrior 프리팹. Inspector에서 반드시 할당.")]
        [SerializeField] private GameObject _warriorPrefab;

        [Tooltip("동시에 유지할 수 있는 최대 Warrior 수.")]
        [SerializeField] private int _maxSlots = 3;

        [Tooltip("Warrior 1기 훈련 소요 시간 (초). GDD: 10초")]
        [SerializeField] private float _trainDuration = 10f;

        [Tooltip("Warrior 훈련 비용 — 나무. GDD: 5")]
        [SerializeField] private int _trainCostWood = 5;

        [Tooltip("Warrior 훈련 비용 — 돌. GDD: 5")]
        [SerializeField] private int _trainCostStone = 5;

        #endregion

        #region ── Private State ──

        // PopulationManager에 미등록된 Warrior를 CombatAlertQueue가 조회할 수 있도록 직접 보유
        private readonly List<Warrior> _warriors = new List<Warrior>();

        // [PR Fix]: P-005 — TrainingLoop 코루틴 참조를 필드에 저장.
        // OnDestroy 시 고아 코루틴이 되지 않도록 명시적 StopCoroutine 호출을 위해 필요.
        private Coroutine _trainingCoroutine = null;

        #endregion

        #region ── Unity Editor 기본값 ──

        private void Reset()
        {
            _buildCostWood  = 15;
            _buildCostStone = 10;
        }

        #endregion

        #region ── Building Lifecycle ──

        protected override void OnBuilt()
        {
            // CombatAlertQueue에 등록하여 FindObjectsOfType 없이 Warrior 조회 가능하게 함
            CombatAlertQueue.Instance?.RegisterBarracks(this);

            // [PR Fix]: P-005 — StartCoroutine 반환값을 _trainingCoroutine 필드에 저장
            _trainingCoroutine = StartCoroutine(TrainingLoop());
        }

        protected override void OnDestroy()
        {
            CombatAlertQueue.Instance?.UnregisterBarracks(this);

            // [PR Fix]: P-005 — OnDestroy에서 고아 코루틴 방지를 위해 명시적으로 StopCoroutine 호출
            if (_trainingCoroutine != null)
            {
                StopCoroutine(_trainingCoroutine);
                _trainingCoroutine = null;
            }

            base.OnDestroy(); // BuildingManager 해제
        }

        #endregion

        #region ── Public API ──

        /// <summary>
        /// Warrior가 사망할 때 호출한다. 리스트에서 제거하고 슬롯을 해제한다.
        /// </summary>
        public void ReleaseSlot(Warrior warrior)
        {
            _warriors.Remove(warrior);
#if UNITY_EDITOR
            Debug.Log($"[Barracks] 슬롯 해제 → {_warriors.Count}/{_maxSlots}");
#endif
        }

        /// <summary>현재 훈련된 Warrior 수.</summary>
        public int OccupiedSlots => _warriors.Count;

        /// <summary>최대 슬롯 수.</summary>
        public int MaxSlots => _maxSlots;

        /// <summary>
        /// CombatAlertQueue가 파견 가능한 Warrior를 수집할 때 호출한다.
        /// 중간 List 할당 없이 전달받은 result 리스트에 직접 추가한다.
        /// 조건: Standby 상태 + 체력 80% 이상 (IsAvailableForDispatch).
        /// </summary>
        public void CollectAvailableWarriors(List<Warrior> result)
        {
            foreach (Warrior w in _warriors)
            {
                if (w != null && w.IsAvailableForDispatch)
                    result.Add(w);
            }
        }

        /// <summary>
        /// Blacksmith.TryCraftWeapon()에서 비무장 Warrior를 수집할 때 호출한다.
        /// 조건: Standby 상태 + 미장착(!IsEquipped).
        ///
        /// v0.2-5: 파견 중인 Warrior는 전투 중이므로 제외하고 Standby만 장착.
        ///         파견 중 Warrior는 귀환 후 다음 제작 시 장착된다.
        /// </summary>
        /// <param name="result">결과를 추가할 버퍼 리스트. 호출 전 Clear()는 호출 측 책임.</param>
        public void CollectUnequippedWarriors(List<Warrior> result)
        {
            foreach (Warrior w in _warriors)
            {
                // null 가드: Warrior가 파괴된 경우 스킵
                if (w == null) continue;

                // Standby 상태이면서 아직 무기를 장착하지 않은 Warrior만 수집
                // (CurrentState == Standby 조건으로 파견 중인 Warrior 제외)
                if (w.CurrentState == UnitState.Standby && !w.IsEquipped)
                    result.Add(w);
            }
        }

        /// <summary>전체 Warrior 수 (상태 무관). 슬롯 현황 파악용.</summary>
        public int WarriorCount => _warriors.Count;

        #endregion

        #region ── Training Loop ──

        private IEnumerator TrainingLoop()
        {
            while (true)
            {
                // ① 슬롯이 찰 때까지 대기
                while (_warriors.Count >= _maxSlots)
                    yield return new WaitForSeconds(1f);

                // ② 자원이 확보될 때까지 대기
                GameManager gm = GameManager.Instance;
                if (gm == null) yield break;

                while (gm.GetResource(ResourceType.WOOD)  < _trainCostWood ||
                       gm.GetResource(ResourceType.STONE) < _trainCostStone)
                {
                    yield return new WaitForSeconds(2f);
                    if (GameManager.Instance == null) yield break;
                }

                // ③ 자원 차감 (훈련 시작 확정)
                // TODO: 훈련 중 건물 파괴 시 차감된 자원 환불 필요 (건물 파괴 기능 추가 시)
                gm.SpendResource(ResourceType.WOOD,  _trainCostWood);
                gm.SpendResource(ResourceType.STONE, _trainCostStone);

#if UNITY_EDITOR
                Debug.Log($"[Barracks] Warrior 훈련 시작 ({_warriors.Count + 1}/{_maxSlots})");
#endif

                // ④ 훈련 대기
                yield return new WaitForSeconds(_trainDuration);

                // ⑤ Warrior 생성
                if (_warriorPrefab == null)
                {
                    Debug.LogWarning("[Barracks] _warriorPrefab이 할당되지 않았습니다. Inspector를 확인하세요.");
                    yield return new WaitForSeconds(5f);
                    continue;
                }

                GameObject warriorObj = Instantiate(_warriorPrefab, transform.position, Quaternion.identity);
                Warrior warrior = warriorObj.GetComponent<Warrior>();
                if (warrior != null)
                {
                    warrior.HomeBarracks = this;
                    _warriors.Add(warrior);
                }

#if UNITY_EDITOR
                Debug.Log($"[Barracks] Warrior 훈련 완료! ({_warriors.Count}/{_maxSlots})");
#endif
            }
        }

        #endregion

        #region ── Gizmos ──

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsBuilt
                ? new Color(0.8f, 0.4f, 0f, 0.5f)
                : new Color(0.6f, 0.6f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, 1.2f);
        }
#endif

        #endregion
    }
}
