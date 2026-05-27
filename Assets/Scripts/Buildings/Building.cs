// =============================================================================
// Building.cs
// 역할  : 건설 가능한 건물의 기반 클래스. Unbuilt → UnderConstruction → Built 상태 관리.
//         ResourceNode 예약 시스템과 동일한 패턴으로 Builder 1기만 작업 가능.
// 사용법: 건물 오브젝트에 이 컴포넌트(또는 파생 클래스)를 추가.
//         Inspector에서 건설 비용과 시간 설정.
// 의존성: GameManager, BuildingManager (AIVillage.Core)
// GDD   : §6 BuildingManager / Week 6
// =============================================================================

using UnityEngine;
using AIVillage.Core;
using AIVillage.Resources;

namespace AIVillage.Buildings
{
    /// <summary>
    /// 건설 가능한 건물의 기반 MonoBehaviour.
    /// Builder FSM이 예약 → 이동 → 건설 → 완공 순서로 호출한다.
    /// </summary>
    public class Building : MonoBehaviour
    {
        #region Nested Types

        public enum BuildingState { Unbuilt, UnderConstruction, Built }

        #endregion

        #region Inspector Fields

        [Header("건설 비용 (GDD §6)")]
        [SerializeField, Tooltip("건설에 필요한 나무 수량")]
        private int _buildCostWood = 5;

        [SerializeField, Tooltip("건설에 필요한 돌 수량")]
        private int _buildCostStone = 3;

        [Header("건설 시간")]
        [SerializeField, Range(1f, 60f), Tooltip("건설 완료까지 걸리는 시간 (초)")]
        private float _buildDuration = 5f;

        #endregion

        #region Private State

        private BuildingState _state     = BuildingState.Unbuilt;
        private GameObject    _reservedBy = null;

        #endregion

        #region Properties

        public BuildingState State          => _state;
        public bool          IsBuilt        => _state == BuildingState.Built;
        public float         BuildDuration  => _buildDuration;

        /// <summary>예약 가능 여부: Unbuilt 상태이며 다른 Builder가 없을 때만 true.</summary>
        public bool IsAvailableForConstruction =>
            _state == BuildingState.Unbuilt && _reservedBy == null;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            GameManager.Instance?.BuildingManager?.RegisterBuilding(this);
        }

        private void OnDestroy()
        {
            GameManager.Instance?.BuildingManager?.UnregisterBuilding(this);
        }

        #endregion

        #region Public API

        /// <summary>이 건물의 월드 좌표(XY 평면)를 반환한다.</summary>
        public Vector2 GetWorldPosition() => transform.position;

        /// <summary>
        /// 건설 예약 시도. IsAvailableForConstruction일 때만 성공한다.
        /// </summary>
        public bool TryReserveConstruction(GameObject builder)
        {
            if (builder == null || !IsAvailableForConstruction) return false;
            _reservedBy = builder;
            return true;
        }

        /// <summary>예약 해제. Unbuilt 상태일 때만 해제한다.</summary>
        public void ReleaseConstruction()
        {
            if (_state == BuildingState.Unbuilt)
                _reservedBy = null;
        }

        /// <summary>
        /// 건설을 시작한다. Builder가 현장 도착 시 호출.
        /// 자원이 충분할 때만 차감하고 UnderConstruction으로 전환.
        /// </summary>
        /// <returns>건설 시작 성공 여부</returns>
        public bool StartConstruction()
        {
            if (_state != BuildingState.Unbuilt || _reservedBy == null) return false;

            GameManager gm = GameManager.Instance;
            if (gm == null) return false;

            // 두 자원을 동시 확인 후 차감 (부분 차감 방지)
            if (gm.GetResource(ResourceType.WOOD)  < _buildCostWood ||
                gm.GetResource(ResourceType.STONE) < _buildCostStone)
            {
                Debug.LogWarning($"[Building] '{name}' 자원 부족 — 나무 {_buildCostWood}, 돌 {_buildCostStone} 필요.");
                return false;
            }

            gm.SpendResource(ResourceType.WOOD,  _buildCostWood);
            gm.SpendResource(ResourceType.STONE, _buildCostStone);
            _state = BuildingState.UnderConstruction;

            Debug.Log($"[Building] '{name}' 건설 시작. 비용: 나무 {_buildCostWood}, 돌 {_buildCostStone}");
            return true;
        }

        /// <summary>
        /// 건설을 완료한다. Builder의 BuildRoutine 종료 시 호출.
        /// </summary>
        public void CompleteConstruction()
        {
            _state      = BuildingState.Built;
            _reservedBy = null;

            Debug.Log($"[Building] '{name}' 건설 완료!");
            OnBuilt();
        }

        #endregion

        #region Virtual Callbacks

        /// <summary>건설 완료 시 파생 클래스에서 재정의. 기본 구현은 비어있다.</summary>
        protected virtual void OnBuilt() { }

        #endregion

        #if UNITY_EDITOR
        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            switch (_state)
            {
                case BuildingState.Unbuilt:
                    Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.6f); // 회색
                    break;
                case BuildingState.UnderConstruction:
                    Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);     // 노랑
                    break;
                case BuildingState.Built:
                    Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.6f); // 초록
                    break;
            }
            Vector3 size = new Vector3(1f, 1f, 0f);
            Gizmos.DrawWireCube(transform.position, size);
        }

        #endregion
        #endif
    }
}
