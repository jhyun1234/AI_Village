/// <summary>
/// Blacksmith.cs - AlloyBlade(합금검)를 제작하여 비무장 Warrior 전원에게 장착시키는 제작 건물.
///
/// 역할(Role)  : 플레이어가 F키를 누르면 PlayerController.HandleWeaponCraft()를 통해
///               TryCraftWeapon()이 호출된다.
///               선행 조건(Forge 완공) + 자원(구리5, 은3) 확인 후 통과 시
///               CombatAlertQueue.CollectUnequippedWarriors()로 비무장 Warrior를 수집하고
///               전원에게 Equip(WeaponType.AlloyBlade)를 호출한다.
/// 사용법(Usage): Blacksmith 프리팹 루트에 이 컴포넌트를 추가.
///               PlayerController 6번 키로 건설 배치. F키로 제작 시도.
///               선행 조건: Forge가 먼저 완공되어 있어야 함.
/// 의존성(Dependencies): AIVillage.Buildings.Building, AIVillage.Buildings.Forge,
///                       AIVillage.Core.GameManager, AIVillage.Core.CombatAlertQueue,
///                       AIVillage.Units.Warrior, AIVillage.Resources.WeaponType
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-09
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Core;
using AIVillage.Resources;
using AIVillage.Units;

namespace AIVillage.Buildings
{
    public sealed class Blacksmith : Building
    {
        #region ── Singleton (P-001) ──

        // [PR Fix]: P-001 — static Instance 패턴 추가. FindObjectsByType 호출 제거.
        /// <summary>
        /// 씬에 존재하는 Blacksmith 싱글톤 인스턴스.
        /// OnBuilt()에서 등록되고 OnDestroy()에서 해제된다.
        /// PlayerController.HandleWeaponCraft()에서 Blacksmith.Instance?.TryCraftWeapon()으로 직접 접근.
        /// </summary>
        public static Blacksmith Instance { get; private set; }

        #endregion

        #region ── Serialized Fields ──

        [Header("제작 비용 (GDD §v0.2-5)")]
        [Tooltip("AlloyBlade 1회 제작에 필요한 구리 수량. GDD: 5")]
        [SerializeField] private int _craftCostCopper = 5; // 기획서 수치: 제작 비용 구리 5

        [Tooltip("AlloyBlade 1회 제작에 필요한 은 수량. GDD: 3")]
        [SerializeField] private int _craftCostSilver = 3; // 기획서 수치: 제작 비용 은 3

        #endregion

        #region ── Private State ──

        // [PR Fix]: P-009 — 초기 용량 16 지정으로 첫 제작 시 불필요한 재할당 방지
        // 비무장 Warrior 수집용 재사용 버퍼 — 매 제작 요청마다 new 하지 않아 GC 할당 없음
        private readonly List<Warrior> _craftBuffer = new List<Warrior>(16);

        #endregion

        #region ── Unity Editor 기본값 ──

        // Reset(): Inspector에서 컴포넌트를 처음 추가하거나 Reset을 클릭할 때 자동 호출됨
        // 기획서 수치: 건설 비용 나무 20, 돌 15 (GDD §v0.2-5)
        private void Reset()
        {
            _buildCostWood  = 20; // 기획서 수치: Blacksmith 건설 비용 나무
            _buildCostStone = 15; // 기획서 수치: Blacksmith 건설 비용 돌
        }

        #endregion

        #region ── Building Lifecycle ──

        protected override void OnBuilt()
        {
            // [PR Fix]: P-001 — OnBuilt에서 static Instance 등록
            Instance = this;

#if UNITY_EDITOR
            Debug.Log("[Blacksmith] 완공 — F키로 AlloyBlade 제작 가능 (Forge 완공 필요).");
#endif
        }

        protected override void OnDestroy()
        {
            // [PR Fix]: P-001 — OnDestroy에서 static Instance 해제 (자신이 등록된 인스턴스일 때만)
            if (Instance == this) Instance = null;

            base.OnDestroy(); // BuildingManager 해제
        }

        #endregion

        #region ── Public API ──

        /// <summary>
        /// PlayerController.HandleWeaponCraft()에서 F키 입력 시 호출된다.
        ///
        /// 제작 성공 흐름:
        ///   1. 건물 완공 여부 확인
        ///   2. Forge 선행 조건 확인 (Forge.Instance != null)
        ///   3. 자원 확인 (구리 _craftCostCopper, 은 _craftCostSilver)
        ///   4. 비무장 Warrior 목록 수집 (CombatAlertQueue.CollectUnequippedWarriors)
        ///   5. 자원 차감 및 전원 장착
        /// </summary>
        /// <returns>제작 성공 여부 (자원 부족 / 선행 미충족 / 비무장 Warrior 없음 시 false)</returns>
        public bool TryCraftWeapon()
        {
            // ① 건물 완공 여부 확인
            if (!IsBuilt)
            {
                Debug.LogWarning("[Blacksmith] TryCraftWeapon — Blacksmith가 아직 완공되지 않았습니다.");
                return false;
            }

            // ② Forge 선행 조건 확인
            // Forge.Instance는 Forge.OnBuilt()에서 등록된다. null이면 Forge 미완공 상태.
            if (Forge.Instance == null)
            {
                // [PR Fix]: P-003 — Debug.Log (1) Forge 미완공 안내 로그를 #if UNITY_EDITOR로 래핑
#if UNITY_EDITOR
                Debug.Log("[Blacksmith] 제작 거부 — Forge(용광로)가 완공되어 있어야 합니다.");
#endif
                return false;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[Blacksmith] TryCraftWeapon — GameManager.Instance가 null입니다.");
                return false;
            }

            // ③ 자원 확인
            int haveCopper = gm.GetResource(ResourceType.COPPER);
            int haveSilver = gm.GetResource(ResourceType.SILVER);

            if (haveCopper < _craftCostCopper || haveSilver < _craftCostSilver)
            {
                // [PR Fix]: P-003 — Debug.Log (2) 자원 부족 안내 로그를 #if UNITY_EDITOR로 래핑
#if UNITY_EDITOR
                Debug.Log($"[Blacksmith] 제작 거부 — 자원 부족. " +
                          $"필요: 구리 {_craftCostCopper}(보유 {haveCopper}), " +
                          $"은 {_craftCostSilver}(보유 {haveSilver})");
#endif
                return false;
            }

            // ④ 비무장 Warrior 수집
            // CombatAlertQueue를 통해 모든 Barracks의 비무장 Warrior를 _craftBuffer에 수집
            _craftBuffer.Clear();
            CombatAlertQueue combatQueue = CombatAlertQueue.Instance;
            if (combatQueue != null)
            {
                combatQueue.CollectUnequippedWarriors(_craftBuffer);
            }
            else
            {
                Debug.LogWarning("[Blacksmith] TryCraftWeapon — CombatAlertQueue.Instance가 null입니다.");
            }

            if (_craftBuffer.Count == 0)
            {
                // [PR Fix]: P-003 — Debug.Log (3) 비무장 Warrior 없음 안내 로그를 #if UNITY_EDITOR로 래핑
#if UNITY_EDITOR
                Debug.Log("[Blacksmith] 제작 거부 — 비무장 Warrior가 없습니다 (전원 이미 장착 완료).");
#endif
                return false;
            }

            // ⑤ 자원 차감 (비용은 Warrior 수와 무관하게 1회 소비 — 전원 일괄 무장)
            gm.SpendResource(ResourceType.COPPER, _craftCostCopper);
            gm.SpendResource(ResourceType.SILVER, _craftCostSilver);

            // ⑥ 비무장 Warrior 전원에게 AlloyBlade 장착
            int equipped = 0;
            foreach (Warrior w in _craftBuffer)
            {
                if (w == null) continue;
                w.Equip(WeaponType.AlloyBlade);
                equipped++;
            }

#if UNITY_EDITOR
            Debug.Log($"[Blacksmith] AlloyBlade 제작 완료! " +
                      $"Warrior {equipped}명 장착 | " +
                      $"구리 -{_craftCostCopper}, 은 -{_craftCostSilver}");
#endif

            gm.MessageBus?.Publish("weapon.crafted", WeaponType.AlloyBlade);
            return true;
        }

        #endregion

        #region ── Gizmos ──

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 완공: 청록색 / 미완공: 회색
            Gizmos.color = IsBuilt
                ? new Color(0f, 0.8f, 0.7f, 0.6f)    // 청록: 완공
                : new Color(0.6f, 0.6f, 0.6f, 0.4f);  // 회색: 건설 중
            Gizmos.DrawWireCube(transform.position, Vector3.one * 1.2f);
        }
#endif

        #endregion
    }
}
