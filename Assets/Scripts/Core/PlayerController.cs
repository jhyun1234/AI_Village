// =============================================================================
// PlayerController.cs
// 역할  : 플레이어 마우스 입력을 처리하는 컨트롤러.
//         좌클릭: 가장 가까운 파견 가능 유닛에게 이동 명령 (체력 80% 이상, 비도주 조건)
//         우클릭: 선택된 건물 프리팹을 클릭 위치에 배치하고 "building.reserved" 이벤트 발행
//         1~6 키: 배치할 건물 종류 선택 (1=House, 2=Quarry, 3=TownHall, 4=Barracks, 5=Forge, 6=Blacksmith)
//         F 키: 무기 제작 (Blacksmith.TryCraftWeapon() 호출)
// 사용법: 씬 내 빈 GameObject에 이 컴포넌트를 추가.
//         Inspector에서 프리팹 슬롯 할당 필수.
// 의존성: UnityEngine.InputSystem, AIVillage.Core.GameManager,
//         AIVillage.Units.AIUnit, AIVillage.Buildings.Building,
//         AIVillage.Buildings.Forge, AIVillage.Buildings.Blacksmith
// GDD   : §9 PlayerController / Week 9-10 / §v0.2-5 무기 시스템
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using AIVillage.Units;
using AIVillage.Buildings;
using AIVillage.Resources;
using AIVillage.GOAP;

namespace AIVillage.Core
{
    /// <summary>
    /// 플레이어 마우스 입력을 수신하여 파견 및 건설 명령을 처리하는 컨트롤러.
    ///
    /// 건물 선택 (키보드):
    ///   1 → House     (나무 10, 돌 5)
    ///   2 → Quarry    (나무 5, 돌 10)
    ///   3 → TownHall  (나무 30, 돌 20)
    ///   4 → Barracks  (나무 15, 돌 10)
    ///   5 → Forge     (나무 15, 돌 10) — Blacksmith 선행 건물
    ///   6 → Blacksmith(나무 20, 돌 15) — 선행: Forge 완공 필수
    ///
    /// 무기 제작 (키보드):
    ///   F → AlloyBlade 제작 시도 (Blacksmith.TryCraftWeapon())
    ///       선행 조건: Blacksmith 완공 + Forge 완공 + 구리5 + 은3
    ///
    /// 우클릭 건설 흐름:
    ///   1. 선택된 프리팹의 Building 컴포넌트에서 건설 비용을 읽어 사전 체크
    ///   2. Blacksmith 선택 시 Forge 완공 여부 추가 체크
    ///   3. 조건 충족 시 클릭 좌표에 Instantiate
    ///   4. "building.reserved" 이벤트 발행 → Builder.OnBuildingReserved() 트리거
    ///
    /// 실제 자원 차감은 Building.StartConstruction()에서 수행된다 (Builder 도착 시).
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        #region ── Serialized Fields ──

        [Header("건설 프리팹 (GDD §9, §10, §v0.2-5)")]
        [Tooltip("1번 키: House 프리팹. Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _housePrefab;

        [Tooltip("2번 키: Quarry 프리팹. Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _quarryPrefab;

        [Tooltip("3번 키: TownHall 프리팹. Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _townHallPrefab;

        [Tooltip("4번 키: Barracks 프리팹. Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _barracksPrefab;

        [Tooltip("5번 키: Forge(용광로) 프리팹. Blacksmith의 선행 건물. " +
                 "Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _forgePrefab;     // v0.2-5 추가

        [Tooltip("6번 키: Blacksmith(대장간) 프리팹. 선행 조건: Forge 완공 필수. " +
                 "Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _blacksmithPrefab; // v0.2-5 추가

        [Tooltip("7번 키: Warehouse(창고) 프리팹. Gatherer가 채집 자원을 납품하는 건물. " +
                 "1개 제한. Building 컴포넌트(Unbuilt 상태)가 있어야 함.")]
        [SerializeField] private GameObject _warehousePrefab;

        [Header("파견 설정 (GDD §9)")]
        [Tooltip("파견 허용 최소 체력 비율 (0~1). GDD: 0.8 = 최대 체력의 80% 이상이어야 파견 허용.")]
        [SerializeField] private float _dispatchHpThreshold = 0.8f; // GDD: 80%

        #endregion

        #region ── Private Fields ──

        // Camera.main은 내부적으로 FindWithTag("MainCamera")를 호출하므로
        // Awake에서 한 번 캐싱하여 Update에서 반복 호출하지 않는다.
        private Camera _mainCamera;

        // 0=House, 1=Quarry, 2=TownHall, 3=Barracks, 4=Forge, 5=Blacksmith
        // 1~6 키로 변경된다.
        private int _selectedBuildingIndex = 0;

        #endregion

        #region ── Unity Lifecycle ──

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
                Debug.LogError("[PlayerController] Camera.main을 찾을 수 없습니다. " +
                               "씬에 'MainCamera' 태그가 달린 카메라가 있는지 확인하세요.");
        }

        private void Update()
        {
            // [PR Fix]: P-006 — Update 상단에 Keyboard/Mouse null 가드 추가.
            // InputSystem 디바이스가 초기화되기 전에 접근하면 NullReferenceException 발생 가능.
            if (Keyboard.current == null || Mouse.current == null) return;

            UpdateBuildingSelection();  // 1~6 키로 건물 종류 선택
            HandleDangerousDispatch();  // 좌클릭: 유닛 파견
            HandleConstructionOrder();  // 우클릭: 건물 배치
            HandleWeaponCraft();        // F키: 무기 제작 (v0.2-5)
        }

        #endregion

        #region ── Building Selection ──

        /// <summary>
        /// 1~6 키로 배치할 건물 종류를 선택한다.
        /// 1=House, 2=Quarry, 3=TownHall, 4=Barracks, 5=Forge, 6=Blacksmith
        /// </summary>
        private void UpdateBuildingSelection()
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) _selectedBuildingIndex = 0;
            if (Keyboard.current.digit2Key.wasPressedThisFrame) _selectedBuildingIndex = 1;
            if (Keyboard.current.digit3Key.wasPressedThisFrame) _selectedBuildingIndex = 2;
            if (Keyboard.current.digit4Key.wasPressedThisFrame) _selectedBuildingIndex = 3;
            if (Keyboard.current.digit5Key.wasPressedThisFrame) _selectedBuildingIndex = 4; // v0.2-5: Forge
            if (Keyboard.current.digit6Key.wasPressedThisFrame) _selectedBuildingIndex = 5; // v0.2-5: Blacksmith
            if (Keyboard.current.digit7Key.wasPressedThisFrame) _selectedBuildingIndex = 6; // Warehouse
        }

        /// <summary>현재 선택된 프리팹을 반환한다. null이면 해당 슬롯 미할당.</summary>
        private GameObject GetSelectedPrefab()
        {
            return _selectedBuildingIndex switch
            {
                0 => _housePrefab,
                1 => _quarryPrefab,
                2 => _townHallPrefab,
                3 => _barracksPrefab,
                4 => _forgePrefab,      // v0.2-5
                5 => _blacksmithPrefab, // v0.2-5
                6 => _warehousePrefab,  // Warehouse
                _ => null
            };
        }

        #endregion

        #region ── Input Handlers ──

        /// <summary>
        /// 좌클릭 파견 명령 처리.
        /// 클릭 좌표 주변에서 파견 가능한 가장 가까운 유닛을 찾아 이동 명령을 내린다.
        /// </summary>
        private void HandleDangerousDispatch()
        {
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (_mainCamera == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector2 worldPos  = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, 0f));

            AIUnit target = FindNearestDispatchableUnit(worldPos);

            if (target == null)
            {
#if UNITY_EDITOR
                Debug.Log("[PlayerController] 파견 가능한 유닛 없음. (모든 유닛 도주 중이거나 유닛 없음)");
#endif
                return;
            }

            if (target.Hp < target.MaxHp * _dispatchHpThreshold)
            {
#if UNITY_EDITOR
                Debug.Log($"[PlayerController] '{target.name}' 파견 거부 — 체력 부족 " +
                          $"({target.Hp:F0}/{target.MaxHp:F0}, 최소 필요: {target.MaxHp * _dispatchHpThreshold:F0})");
#endif
                return;
            }

            target.SetDestination(new Vector3(worldPos.x, worldPos.y, 0f));
#if UNITY_EDITOR
            Debug.Log($"[PlayerController] '{target.name}' 파견 → 월드 좌표 {worldPos}");
#endif
        }

        /// <summary>
        /// 우클릭 건설 명령 처리.
        /// 현재 선택된 건물 프리팹을 클릭 좌표에 생성하고 "building.reserved" 이벤트를 발행한다.
        ///
        /// 건설 거부 조건:
        ///   - 선택된 프리팹 미할당
        ///   - GameManager.Instance가 null
        ///   - 선택된 프리팹에 Building 컴포넌트 없음
        ///   - 자원 부족 (Building.BuildCostWood / BuildCostStone 기준)
        ///   - Blacksmith 선택 시: Forge가 완공되어 있지 않음 (v0.2-5)
        /// </summary>
        private void HandleConstructionOrder()
        {
            if (!Mouse.current.rightButton.wasPressedThisFrame) return;

            GameObject prefab = GetSelectedPrefab();

            if (prefab == null)
            {
                Debug.LogWarning($"[PlayerController] 선택된 건물({_selectedBuildingIndex})의 프리팹이 " +
                                 "Inspector에 할당되지 않았습니다.");
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[PlayerController] HandleConstructionOrder — GameManager.Instance가 null입니다.");
                return;
            }

            // 프리팹에서 비용 읽기 (Building.BuildCostWood/BuildCostStone public getter 사용)
            Building template = prefab.GetComponent<Building>();
            if (template == null)
            {
                Debug.LogWarning($"[PlayerController] '{prefab.name}'에 Building 컴포넌트가 없습니다.");
                return;
            }

            // ── v0.2-5: Blacksmith 선행 조건 체크 ──
            if (_selectedBuildingIndex == 5) // 5 = Blacksmith
            {
                bool forgeReady = gm.BuildingManager != null && gm.BuildingManager.HasBuiltForge;
                if (!forgeReady)
                {
                    Debug.Log("[PlayerController] Blacksmith 건설 거부 — " +
                              "Forge(용광로)가 먼저 완공되어 있어야 합니다.");
                    return;
                }
            }

            // ── Warehouse 1개 제한 체크 ──
            if (_selectedBuildingIndex == 6 && AIVillage.Buildings.Warehouse.Instance != null)
            {
                Debug.Log("[PlayerController] Warehouse 건설 거부 — 이미 완공된 창고가 있습니다.");
                return;
            }

            int costWood  = template.BuildCostWood;
            int costStone = template.BuildCostStone;
            int haveWood  = gm.GetResource(ResourceType.WOOD);
            int haveStone = gm.GetResource(ResourceType.STONE);

            if (haveWood < costWood || haveStone < costStone)
            {
#if UNITY_EDITOR
                Debug.Log($"[PlayerController] 건설 거부 — 자원 부족. " +
                          $"필요: 나무 {costWood}(보유 {haveWood}), 돌 {costStone}(보유 {haveStone})");
#endif
                return;
            }

            if (_mainCamera == null) return;

            Vector2 screenPos  = Mouse.current.position.ReadValue();
            Vector2 worldPos2D = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, 0f));
            Vector3 spawnPos   = new Vector3(worldPos2D.x, worldPos2D.y, 0f);

            GameObject buildingObj = Instantiate(prefab, spawnPos, Quaternion.identity);

            Building building = buildingObj.GetComponent<Building>();
            if (building == null)
            {
                Debug.LogWarning($"[PlayerController] Instantiate 후 '{prefab.name}'에 Building 컴포넌트가 없습니다.");
                Destroy(buildingObj);
                return;
            }

            // Builder에게 새 건물 알림
            gm.MessageBus?.Publish("building.reserved", building);

#if UNITY_EDITOR
            Debug.Log($"[PlayerController] '{prefab.name}' 건설 지시 → 월드 좌표 {spawnPos}");
#endif
        }

        /// <summary>
        /// F키 무기 제작 명령 처리 (GDD §v0.2-5).
        ///
        /// Blacksmith.Instance를 통해 직접 TryCraftWeapon()을 호출한다.
        /// 실제 선행 조건(Forge 완공), 자원 확인, 비무장 Warrior 수집은
        /// Blacksmith.TryCraftWeapon() 내부에서 처리된다.
        /// </summary>
        private void HandleWeaponCraft()
        {
            // F키가 눌린 프레임에만 실행 (Update 매 프레임 호출 방지)
            if (!Keyboard.current.fKey.wasPressedThisFrame) return;

            // [PR Fix]: P-001 — FindBuiltBlacksmith() 헬퍼 제거. Blacksmith.Instance로 직접 접근.
            // Blacksmith.OnBuilt()에서 Instance가 등록되므로 null이면 미완공 상태를 의미한다.
            if (Blacksmith.Instance == null)
            {
                Debug.Log("[PlayerController] 무기 제작 불가 — 완공된 Blacksmith가 없습니다. " +
                          "6번 키로 Blacksmith를 건설한 뒤 시도하세요.");
                return;
            }

            // 실제 제작 로직은 Blacksmith 내부에 위임 (단일 책임 원칙)
            Blacksmith.Instance.TryCraftWeapon();
        }

        #endregion

        #region ── Helper Methods ──

        /// <summary>
        /// 지정한 월드 좌표에서 가장 가까운 파견 가능 유닛을 반환한다.
        /// 파견 가능 조건: null이 아님 + IsFleeing == false
        /// 체력 조건은 호출 측에서 처리한다.
        /// </summary>
        private AIUnit FindNearestDispatchableUnit(Vector2 worldPos)
        {
            AIUnit[] units = GameManager.Instance?.PopulationManager?.GetAllUnitsSnapshot();

            if (units == null || units.Length == 0) return null;

            AIUnit best     = null;
            float  bestDist = float.MaxValue;

            foreach (AIUnit unit in units)
            {
                if (unit == null)   continue;
                if (unit.IsFleeing) continue;
                // GoapAgent 부착 유닛(Gatherer)과 Builder는 자율 AI가 제어하므로 수동 파견 대상에서 제외한다.
                if (unit.GetComponent<GoapAgent>() != null) continue;
                if (unit is Builder) continue;

                float dist = Vector2.Distance(worldPos, unit.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = unit;
                }
            }

            return best;
        }

        #endregion
    }
}
