// =============================================================================
// House.cs
// 역할  : 완공 시 GameManager.BasePosition을 이 건물의 위치로 설정하는 기지 건물.
//         Gatherer들이 이 위치로 귀환한다.
// 사용법: 빈 오브젝트에 House 컴포넌트 추가. SpriteRenderer로 시각화 권장.
// 의존성: Building, GameManager
// GDD   : §6 House / Week 6
// =============================================================================

using UnityEngine;
using AIVillage.Core;

namespace AIVillage.Buildings
{
    /// <summary>
    /// 완공되면 Gatherer 귀환 기점(BasePosition)을 이 위치로 설정하는 건물.
    /// </summary>
    public class House : Building
    {
        protected override void OnBuilt()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.BasePosition = transform.position;
            Debug.Log($"[House] '{name}' 완공 — 기지 위치 설정: {transform.position}");
        }
    }
}
