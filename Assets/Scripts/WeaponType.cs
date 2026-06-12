/// <summary>
/// WeaponType.cs - Warrior가 장착할 수 있는 무기 종류를 정의하는 열거형.
///
/// 역할(Role)  : 게임 내 무기 종류를 코드 상수로 관리한다.
///               Warrior.Equip(), Blacksmith.TryCraftWeapon() 등에서 사용.
/// 사용법(Usage): AIVillage.Resources 네임스페이스 참조 후 WeaponType.AlloyBlade 등으로 접근.
/// 의존성(Dependencies): 없음 (순수 열거형)
///
/// Author: Senior Unity Programmer
/// Last Updated: 2026-06-09
/// </summary>

namespace AIVillage.Resources
{
    /// <summary>
    /// Warrior 무기 종류.
    /// None = 비무장 (기본 공격력 15),
    /// AlloyBlade = 합금검 (공격력 25, +10 보너스).
    /// GDD §v0.2-5.
    /// </summary>
    public enum WeaponType
    {
        /// <summary>비무장 상태. 기본 공격력 15f 적용.</summary>
        None = 0,

        /// <summary>합금검. 공격력 25f (+10). Blacksmith에서 제작.</summary>
        AlloyBlade = 1
    }
}
