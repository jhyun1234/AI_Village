// =============================================================================
// BuildingManager.cs
// 역할  : 씬의 모든 Building을 등록/관리하고, Builder FSM에 미완공 건설지를 제공한다.
// 사용법: GameManager와 동일한 GameObject에 추가(자동). 직접 접근은
//         GameManager.Instance.BuildingManager 사용.
// 의존성: GameManager, Building (AIVillage.Buildings)
// GDD   : §6 BuildingManager / Week 6
//         §v0.2-5 HasBuiltForge / HasBuiltBlacksmith 프로퍼티 추가 (무기 시스템 선행 조건)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Buildings;

namespace AIVillage.Core
{
    /// <summary>
    /// Building 목록을 관리하는 매니저.
    /// Builder FSM이 가장 가까운 미완공 건설지를 요청할 때 사용한다.
    /// v0.2-5: Forge/Blacksmith 완공 여부를 static Instance 패턴으로 O(1)에 제공한다.
    ///         PlayerController의 건설 선행 조건 체크에 사용된다.
    /// </summary>
    public sealed class BuildingManager : MonoBehaviour
    {
        #region Private State

        private readonly List<Building> _allBuildings = new List<Building>();

        #endregion

        #region Registration

        public void RegisterBuilding(Building building)
        {
            if (building == null || _allBuildings.Contains(building)) return;
            _allBuildings.Add(building);
#if UNITY_EDITOR
            Debug.Log($"[BuildingManager] 건물 등록: {building.name} | 총 {_allBuildings.Count}개");
#endif
        }

        public void UnregisterBuilding(Building building)
        {
            if (_allBuildings.Remove(building))
            {
#if UNITY_EDITOR
                Debug.Log($"[BuildingManager] 건물 제거: {building.name} | 총 {_allBuildings.Count}개");
#endif
            }
        }

        #endregion

        #region Query API

        /// <summary>
        /// 지정 위치에서 가장 가까운 예약 가능한(Unbuilt) 건설지를 반환한다.
        /// 없으면 null.
        /// </summary>
        public Building GetNearestPendingBuilding(Vector2 from)
        {
            Building nearest = null;
            float    minDist = float.MaxValue;

            foreach (Building b in _allBuildings)
            {
                if (b == null || !b.IsAvailableForConstruction) continue;

                float dist = Vector2.Distance(from, b.GetWorldPosition());
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = b;
                }
            }

            return nearest;
        }

        /// <summary>전체 등록 건물 수.</summary>
        public int TotalCount => _allBuildings.Count;

        /// <summary>완공된 건물 수.</summary>
        public int BuiltCount
        {
            get
            {
                int count = 0;
                foreach (Building b in _allBuildings)
                    if (b != null && b.IsBuilt) count++;
                return count;
            }
        }

        // ── v0.2-5 추가: 무기 시스템 선행 조건 조회 프로퍼티 ──

        /// <summary>
        /// 씬에 완공된 Forge(용광로)가 있으면 true.
        /// Blacksmith 건설 허용 여부 및 PlayerController 선행 조건 체크에 사용된다.
        ///
        /// [PR Fix]: P-008 — O(n) 순회 제거. Forge.Instance 싱글톤으로 O(1) 조회.
        /// Forge.OnBuilt()에서 Instance가 등록되므로 null이면 미완공 상태를 의미한다.
        /// </summary>
        public bool HasBuiltForge =>
            Forge.Instance != null && Forge.Instance.IsBuilt;

        /// <summary>
        /// 씬에 완공된 Blacksmith(대장간)가 있으면 true.
        /// 동일한 Blacksmith를 중복 배치 방지할 때 PlayerController에서 사용한다.
        ///
        /// [PR Fix]: P-008 — O(n) 순회 제거. Blacksmith.Instance 싱글톤으로 O(1) 조회.
        /// Blacksmith.OnBuilt()에서 Instance가 등록되므로 null이면 미완공 상태를 의미한다.
        /// </summary>
        public bool HasBuiltBlacksmith =>
            Blacksmith.Instance != null && Blacksmith.Instance.IsBuilt;

        #endregion

        #region Lifecycle

        private void OnDestroy() => _allBuildings.Clear();

        #endregion
    }
}
