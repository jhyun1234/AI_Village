// =============================================================================
// Quarry.cs
// 역할  : 완공 후 45초마다 돌 1개를 자동 생산하는 채석장 건물.
//         GameManager.AddResource(STONE, 1) 호출로 자원을 추가한다.
// 사용법: 빈 오브젝트에 Quarry 컴포넌트 추가. 컴포넌트 첫 추가 시 Reset()이
//         GDD 기본값(나무 5, 돌 10)을 자동 세팅한다.
// 의존성: Building, GameManager
// GDD   : §6 Quarry / Week 10 — 돌 자동 생산
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Core;
using AIVillage.Resources;

namespace AIVillage.Buildings
{
    /// <summary>
    /// 완공 후 45초마다 돌 1개를 자동으로 생산하는 채석장.
    /// 건설 비용: 나무 5, 돌 10 (GDD Week 10).
    /// </summary>
    public class Quarry : Building
    {
        private const float PRODUCTION_INTERVAL = 45f; // GDD: 45초 주기
        private const int   STONE_PER_CYCLE     = 1;   // GDD: 돌 1개/주기

        #region ── Unity Editor Callback ──

        /// <summary>
        /// 컴포넌트가 Inspector에 처음 추가될 때 Unity Editor가 자동 호출하는 콜백.
        /// Building 기반 클래스의 protected 필드에 GDD 기본값을 설정한다.
        /// 런타임에는 실행되지 않는다.
        /// </summary>
        private void Reset()
        {
            _buildCostWood  = 5;  // GDD: Quarry 건설 비용 나무 5
            _buildCostStone = 10; // GDD: Quarry 건설 비용 돌 10
        }

        #endregion

        #region ── Building Callback ──

        /// <summary>
        /// 건설 완료 시 Building.CompleteConstruction()이 호출하는 콜백.
        /// 돌 자동 생산 코루틴을 시작한다.
        /// 코루틴은 MonoBehaviour 파괴 시 Unity가 자동으로 정지시킨다.
        /// </summary>
        protected override void OnBuilt()
        {
            StartCoroutine(ProductionLoop());
        }

        #endregion

        #region ── Production Logic ──

        /// <summary>
        /// 45초 주기로 돌 1개를 GameManager에 추가하는 생산 루프.
        /// GameManager가 null이 되면(씬 전환 등) 루프를 종료한다.
        /// </summary>
        private IEnumerator ProductionLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(PRODUCTION_INTERVAL);

                if (GameManager.Instance == null) yield break;

                GameManager.Instance.AddResource(ResourceType.STONE, STONE_PER_CYCLE);

#if UNITY_EDITOR
                Debug.Log($"[Quarry] '{name}' 돌 {STONE_PER_CYCLE}개 자동 생산. " +
                          $"현재 돌: {GameManager.Instance.GetResource(ResourceType.STONE)}");
#endif
            }
        }

        #endregion

#if UNITY_EDITOR
        #region ── Editor Gizmos ──

        /// <summary>
        /// Scene 뷰에서 항상 Quarry 범위를 표시한다.
        /// 완공 전: 회색 / 완공 후: 갈색 (채석장 테마)
        /// </summary>
        private void OnDrawGizmos()
        {
            const float GIZMO_RADIUS = 1f;

            bool built = IsBuilt;

            // ── 채움 ──
            Gizmos.color = built
                ? new Color(0.55f, 0.40f, 0.15f, 0.08f)  // 완공: 갈색 반투명
                : new Color(0.60f, 0.60f, 0.60f, 0.04f);  // 미완공: 회색
            Gizmos.DrawSphere(transform.position, GIZMO_RADIUS);

            // ── 외곽선 ──
            Gizmos.color = built
                ? new Color(0.55f, 0.40f, 0.15f, 0.85f)  // 완공: 갈색 실선
                : new Color(0.60f, 0.60f, 0.60f, 0.35f);  // 미완공: 회색 점선
            Gizmos.DrawWireSphere(transform.position, GIZMO_RADIUS);
        }

        #endregion
#endif
    }
}
