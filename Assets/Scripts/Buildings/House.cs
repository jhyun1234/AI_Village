// =============================================================================
// House.cs
// 역할  : 완공 시 기지 위치와 안전 구역 반경을 GameManager에 등록하는 기지 건물.
//         Gatherer들이 이 위치로 귀환하며, Monster는 안전 구역 내 유닛을 공격·추적하지 못한다.
// 사용법: 빈 오브젝트에 House 컴포넌트 추가. SpriteRenderer로 시각화 권장.
//         Inspector의 _safeZoneRadius 값으로 안전 구역 크기를 조정.
// 의존성: Building, GameManager
// GDD   : §6 House / §8-2 기지 안전 구역 / Week 6
// =============================================================================

using UnityEngine;
using AIVillage.Core;

namespace AIVillage.Buildings
{
    /// <summary>
    /// 완공되면 Gatherer 귀환 기점(BasePosition)과 기지 안전 구역을 GameManager에 등록하는 건물.
    /// Monster는 이 안전 구역 안에 있는 유닛을 감지하거나 공격하지 못한다.
    /// </summary>
    public class House : Building
    {
        #region ── Serialized Fields ──

        [Header("기지 안전 구역 (GDD §8-2)")]
        [Tooltip("Monster가 이 반경 내 유닛을 추적·공격하지 못한다. Scene 뷰에서 하늘색 원으로 표시됨.")]
        [SerializeField, Range(1f, 20f)] private float _safeZoneRadius = 5f;

        #endregion

        #region ── Building Callback ──

        protected override void OnBuilt()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.RegisterSafeZone(transform.position, _safeZoneRadius);
#if UNITY_EDITOR
            Debug.Log($"[House] '{name}' 완공 — 기지 위치: {transform.position}, 안전구역 반경: {_safeZoneRadius}f");
#endif
        }

        #endregion

#if UNITY_EDITOR
        #region ── Editor Gizmos ──

        /// <summary>
        /// Scene 뷰에서 항상 안전 구역을 시각적으로 표시한다.
        /// 완공 전: 회색 반투명 / 완공 후: 하늘색 반투명
        /// </summary>
        private void OnDrawGizmos()
        {
            bool built = IsBuilt;

            // ── 채움 (면 강조) ──
            Gizmos.color = built
                ? new Color(0.2f, 0.8f, 1f, 0.07f)    // 완공: 하늘색 반투명
                : new Color(0.6f, 0.6f, 0.6f, 0.04f);  // 미완공: 회색
            Gizmos.DrawSphere(transform.position, _safeZoneRadius);

            // ── 외곽선 ──
            Gizmos.color = built
                ? new Color(0.2f, 0.8f, 1f, 0.85f)    // 완공: 하늘색 실선
                : new Color(0.6f, 0.6f, 0.6f, 0.35f);  // 미완공: 회색 점선
            Gizmos.DrawWireSphere(transform.position, _safeZoneRadius);
        }

        #endregion
#endif
    }
}
