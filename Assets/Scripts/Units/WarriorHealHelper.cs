// =============================================================================
// WarriorHealHelper.cs
// 역할  : Warrior가 Standby 상태(this.enabled=false)일 때도 HP를 회복시키는 보조 컴포넌트.
//         Warrior 컴포넌트와 동일한 GameObject에 자동 추가된다.
//         Warrior.enabled=false여도 이 컴포넌트는 독립적으로 Update()를 실행한다.
// 사용법: Warrior.cs의 [RequireComponent]로 자동 추가됨. 직접 추가하지 말 것.
//         Warrior.EnterStandby() → StartHealing() / ExitStandby() → StopHealing()
// 의존성: AIVillage.Units.Warrior
// GDD   : §v0.2-3 Warrior 대기 상태 HP 회복
// =============================================================================

using UnityEngine;

namespace AIVillage.Units
{
    public sealed class WarriorHealHelper : MonoBehaviour
    {
        [Tooltip("Standby 중 HP 회복 속도 (HP/초). GDD: 5f")]
        [SerializeField] private float _healRate = 5f;

        private Warrior _warrior;
        private bool    _isHealing = false;

        private void Awake()
        {
            _warrior = GetComponent<Warrior>();
#if UNITY_EDITOR
            if (_warrior == null)
                Debug.LogWarning($"[WarriorHealHelper] '{name}' — Warrior 컴포넌트를 찾을 수 없습니다. " +
                                 "이 컴포넌트는 Warrior와 동일한 GameObject에 있어야 합니다.");
#endif
        }

        private void Update()
        {
            if (!_isHealing || _warrior == null) return;
            _warrior.ApplyHeal(_healRate * Time.deltaTime);
        }

        /// <summary>Standby 진입 시 호출. HP 회복을 시작한다.</summary>
        public void StartHealing() => _isHealing = true;

        /// <summary>Standby 이탈 시 호출. HP 회복을 중단한다.</summary>
        public void StopHealing()  => _isHealing = false;
    }
}
