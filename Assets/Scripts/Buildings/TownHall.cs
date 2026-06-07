// =============================================================================
// TownHall.cs
// 역할  : 완공 시 GameManager에 승리 조건을 활성화하는 핵심 건물.
//         TownHall 완공 + 인구 20명 충족 시 게임 승리로 연결된다.
// 사용법: 빈 오브젝트에 TownHall 컴포넌트 추가. Inspector에서 비용 확인 후 플레이.
//         컴포넌트를 처음 추가하면 Reset()이 GDD 기본값(나무 30, 돌 20)을 자동 세팅한다.
// 의존성: Building, GameManager
// GDD   : §6 TownHall / Week 10 승리 조건
// =============================================================================

using UnityEngine;
using AIVillage.Core;

namespace AIVillage.Buildings
{
    /// <summary>
    /// 완공 시 GameManager.RegisterTownHall()을 호출하여 승리 조건을 활성화하는 건물.
    /// 건설 비용: 나무 30, 돌 20 (GDD Week 10).
    /// </summary>
    public class TownHall : Building
    {
        #region ── Unity Editor Callbacks ──

        /// <summary>
        /// 컴포넌트가 Inspector에 처음 추가될 때 Unity Editor가 자동 호출하는 콜백.
        /// Building 기반 클래스의 protected 필드에 GDD 기본값을 설정한다.
        /// 런타임에는 실행되지 않으므로 게임 로직에 영향을 주지 않는다.
        /// </summary>
        private void Reset()
        {
            _buildCostWood  = 30; // GDD 수치: TownHall 건설 비용 나무 30
            _buildCostStone = 20; // GDD 수치: TownHall 건설 비용 돌 20
        }

        #endregion

        #region ── Building Callback ──

        /// <summary>
        /// 건설 완료 시 Building.CompleteConstruction()이 호출하는 콜백.
        /// GameManager에 TownHall 완공 사실을 등록하여 승리 조건 체크를 활성화한다.
        /// </summary>
        protected override void OnBuilt()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[TownHall] GameManager.Instance가 null입니다 — RegisterTownHall() 호출을 건너뜁니다.");
                return;
            }

            GameManager.Instance.RegisterTownHall();
#if UNITY_EDITOR
            Debug.Log($"[TownHall] '{name}' 완공 — 승리 조건 활성화됨.");
#endif
        }

        #endregion

#if UNITY_EDITOR
        #region ── Editor Gizmos ──

        /// <summary>
        /// Scene 뷰에서 항상 TownHall 범위를 시각적으로 표시한다.
        /// 완공 전: 회색 반투명 WireSphere / 완공 후: 금색 WireSphere
        /// </summary>
        private void OnDrawGizmos()
        {
            const float GIZMO_RADIUS = 1.5f;

            bool built = IsBuilt;

            // ── 채움 (면 강조) ──
            Gizmos.color = built
                ? new Color(1f, 0.84f, 0f, 0.08f)       // 완공: 금색 반투명
                : new Color(0.6f, 0.6f, 0.6f, 0.04f);   // 미완공: 회색
            Gizmos.DrawSphere(transform.position, GIZMO_RADIUS);

            // ── 외곽선 ──
            Gizmos.color = built
                ? new Color(1f, 0.84f, 0f, 0.85f)       // 완공: 금색 실선
                : new Color(0.6f, 0.6f, 0.6f, 0.35f);   // 미완공: 회색 점선
            Gizmos.DrawWireSphere(transform.position, GIZMO_RADIUS);
        }

        #endregion
#endif
    }
}
