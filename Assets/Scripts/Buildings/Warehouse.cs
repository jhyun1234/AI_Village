// =============================================================================
// Warehouse.cs
// 역할  : 자원 창고 건물. Gatherer가 채집한 자원을 이곳으로 납품한다.
//         House(안전 구역)와 분리된 전용 납품 지점으로 기능한다.
//         완공 시 GameManager.RegisterWarehouse()에 위치를 등록한다.
// 사용법: Warehouse 프리팹에 이 컴포넌트를 추가.
//         PlayerController — 7번 키로 배치.
//         씬에 1개만 존재 가능 (static Instance 패턴).
// 의존성: Building (기반 클래스), GameManager
// GDD   : §v0.3 자원 창고 / 납품 분리
// =============================================================================

using UnityEngine;
using AIVillage.Core;

namespace AIVillage.Buildings
{
    /// <summary>
    /// Gatherer가 채집 완료 후 자원을 납품하는 창고 건물.
    /// 완공 전까지 Gatherer는 BasePosition(House)으로 귀환하며,
    /// 완공 후부터 Warehouse 위치로 귀환하여 납품한다.
    /// </summary>
    public class Warehouse : Building
    {
        /// <summary>씬 내 유일한 완공 Warehouse 인스턴스. 미완공이면 null.</summary>
        public static Warehouse Instance { get; private set; }

        #region ── Editor 기본값 ──

        private void Reset()
        {
            _buildCostWood  = 10;
            _buildCostStone = 8;
        }

        #endregion

        #region ── Building 콜백 ──

        protected override void OnBuilt()
        {
            Instance = this;
            GameManager.Instance?.RegisterWarehouse((Vector2)transform.position);
#if UNITY_EDITOR
            Debug.Log($"[Warehouse] 완공 — 자원 납품 지점이 {transform.position}으로 설정됩니다.");
#endif
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameManager.Instance?.UnregisterWarehouse();
            }
            base.OnDestroy();
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.1f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 1.5f, 0f));

            Gizmos.color = new Color(0.9f, 0.7f, 0.1f, 0.15f);
            Gizmos.DrawCube(transform.position, new Vector3(1.5f, 1.5f, 0f));
        }
#endif
    }
}
