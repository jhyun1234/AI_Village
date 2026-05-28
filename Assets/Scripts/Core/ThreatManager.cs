// =============================================================================
// ThreatManager.cs
// 역할  : 씬에 활성화된 모든 Monster를 추적하고, 위치 기반으로 가장 가까운 몬스터를 조회.
//         GameManager.Instance.ThreatManager 를 통해 외부에서 접근.
// 사용법: GameManager와 동일한 GameObject에 자동으로 추가됨 (CacheComponents).
//         직접 추가 불필요.
// 의존성: Monster (AIVillage.Units), GameManager (AIVillage.Core)
// GDD   : §8 ThreatManager / Week 8
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Units;

namespace AIVillage.Core
{
    /// <summary>
    /// 씬에 살아있는 Monster 인스턴스를 추적하는 매니저.
    /// Monster.Start()에서 자동 등록, Monster.OnDestroy()에서 자동 해제된다.
    /// GameManager.CheckThreatForAllUnits()에서 매 Tick마다 위협 감지에 활용된다.
    /// </summary>
    public sealed class ThreatManager : MonoBehaviour
    {
        #region ── Private Fields ──

        // 씬에 살아있는 Monster 목록. Monster.Start/OnDestroy가 자동으로 관리한다.
        private readonly List<Monster> _monsters = new List<Monster>();

        #endregion

        #region ── Public Registration API ──

        /// <summary>
        /// Monster가 씬에 스폰될 때(Start) 호출하여 추적 목록에 추가한다.
        /// 중복 등록은 자동으로 무시된다.
        /// </summary>
        /// <param name="monster">등록할 Monster 인스턴스</param>
        public void RegisterMonster(Monster monster)
        {
            if (monster == null)
            {
                Debug.LogWarning("[ThreatManager] RegisterMonster — null Monster 전달됨. 무시.");
                return;
            }

            if (_monsters.Contains(monster)) return;

            _monsters.Add(monster);
            Debug.Log($"[ThreatManager] 몬스터 등록: '{monster.name}' | 총 {_monsters.Count}마리");
        }

        /// <summary>
        /// Monster가 파괴될 때(OnDestroy) 호출하여 추적 목록에서 제거한다.
        /// </summary>
        /// <param name="monster">해제할 Monster 인스턴스</param>
        public void UnregisterMonster(Monster monster)
        {
            if (_monsters.Remove(monster))
                Debug.Log($"[ThreatManager] 몬스터 해제. 남은 몬스터: {_monsters.Count}마리");
        }

        #endregion

        #region ── Public Query API ──

        /// <summary>
        /// 지정한 위치에서 radius 이내에 있는 Monster 중 가장 가까운 것을 반환한다.
        /// null 참조(파괴된 Monster)를 순회 전에 정리하여 안정성을 보장한다.
        /// </summary>
        /// <param name="position">탐색 중심 월드 좌표</param>
        /// <param name="radius">탐색 반경 (월드 단위)</param>
        /// <returns>가장 가까운 Monster, 없으면 null</returns>
        public Monster GetNearestMonster(Vector2 position, float radius)
        {
            // ── null 참조 정리 ──
            // Monster가 Destroy됐지만 OnDestroy 호출 전 참조가 남아있을 수 있음
            // Unity에서 파괴된 오브젝트는 == null 비교가 true를 반환한다
            _monsters.RemoveAll(m => m == null);

            if (_monsters.Count == 0) return null;

            // ── 가장 가까운 몬스터 탐색 ──
            // 제곱 거리 비교로 sqrt 연산 비용을 제거한다
            // Formula: distSq = (dx*dx + dy*dy), radiusSq = radius*radius
            float radiusSq  = radius * radius;
            Monster nearest   = null;
            float   nearestSq = float.MaxValue;

            foreach (Monster m in _monsters)
            {
                float dx     = m.transform.position.x - position.x;
                float dy     = m.transform.position.y - position.y;
                float distSq = dx * dx + dy * dy;

                if (distSq <= radiusSq && distSq < nearestSq)
                {
                    nearestSq = distSq;
                    nearest   = m;
                }
            }

            return nearest;
        }

        /// <summary>현재 추적 중인 몬스터 수를 반환한다. 디버그/UI용.</summary>
        public int MonsterCount => _monsters.Count;

        #endregion

        #region ── Unity Lifecycle ──

        private void OnDestroy()
        {
            _monsters.Clear();
        }

        #endregion

#if UNITY_EDITOR
        #region ── Editor Gizmos ──

        /// <summary>씬 뷰에서 등록된 모든 몬스터 위치를 빨간 원으로 표시한다.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
            foreach (Monster m in _monsters)
            {
                if (m == null) continue;
                Gizmos.DrawWireSphere(m.transform.position, 0.3f);
            }
        }

        #endregion
#endif
    }
}
