/// <summary>
/// Forge.cs - AlloyBlade 제작의 선행 건물(용광로).
///
/// 역할(Role)  : Blacksmith가 무기를 제작하기 위해 반드시 먼저 완공되어야 하는 건물.
///               완공(OnBuilt) 시 static Instance를 등록하여
///               BuildingManager.HasBuiltForge 프로퍼티와 PlayerController의 선행 조건 체크에 사용된다.
/// 사용법(Usage): Forge 프리팹 루트에 이 컴포넌트를 추가.
///               PlayerController 5번 키로 건설 배치.
///               Inspector에서 별도 할당 항목 없음 (Building 기본값 Reset에서 자동 세팅).
/// 의존성(Dependencies): AIVillage.Buildings.Building, AIVillage.Core.GameManager
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-09
/// </summary>

using UnityEngine;

namespace AIVillage.Buildings
{
    /// <summary>
    /// 용광로 건물. 완공 후 static Instance를 통해 씬 내 존재 여부를 제공한다.
    /// Blacksmith 제작 가능 여부(선행 조건)는 Instance != null 로 확인한다.
    /// </summary>
    public sealed class Forge : Building
    {
        #region ── Singleton-like Instance ──

        // 씬에 Forge는 1개만 존재해야 한다.
        // static 참조로 BuildingManager/PlayerController가 FindObjectOfType 없이 Forge 완공 여부를 확인한다.
        /// <summary>씬 내 완공된 Forge 인스턴스. 완공 전이거나 파괴 후에는 null.</summary>
        public static Forge Instance { get; private set; }

        #endregion

        #region ── Unity Editor 기본값 ──

        // Reset()은 Inspector에서 컴포넌트를 처음 추가하거나 Reset을 클릭할 때 호출된다.
        // 기획서 수치: 건설 비용 나무 15, 돌 10 (GDD §v0.2-5)
        private void Reset()
        {
            _buildCostWood  = 15; // 기획서 수치: Forge 건설 비용 나무
            _buildCostStone = 10; // 기획서 수치: Forge 건설 비용 돌
        }

        #endregion

        #region ── Building Lifecycle ──

        /// <summary>
        /// 완공(공사 완료) 시 호출된다.
        /// static Instance를 등록하여 이후 Blacksmith / PlayerController가
        /// Forge 완공 여부를 O(1)로 확인할 수 있게 한다.
        /// </summary>
        protected override void OnBuilt()
        {
            // 씬에 이미 완공된 Forge가 있으면 경고 후 교체
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Forge] 이미 완공된 Forge가 존재합니다. " +
                                 "씬에 Forge는 1개만 배치하세요. Instance를 교체합니다.");
            }

            Instance = this;

#if UNITY_EDITOR
            Debug.Log("[Forge] 완공 — Instance 등록 완료. Blacksmith 건설 가능.");
#endif
        }

        protected override void OnDestroy()
        {
            // 파괴 시 Instance 해제 (다음 Forge 완공 전까지 선행 조건 불충족 상태로 복귀)
            if (Instance == this)
                Instance = null;

            base.OnDestroy(); // BuildingManager 해제
        }

        #endregion

        #region ── Gizmos ──

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 완공: 주황색 / 미완공: 회색 와이어프레임으로 씬 뷰에서 식별
            Gizmos.color = IsBuilt
                ? new Color(1f, 0.5f, 0f, 0.6f)   // 주황: 완공
                : new Color(0.6f, 0.6f, 0.6f, 0.4f); // 회색: 건설 중
            Gizmos.DrawWireCube(transform.position, Vector3.one * 1.2f);
        }
#endif

        #endregion
    }
}
