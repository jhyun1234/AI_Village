// =============================================================================
// DangerRegistry.cs
// 역할  : "unit.fleeing" 이벤트를 수신하여 위험 발생 좌표를 기록한다.
//         Gatherer가 노드를 선택하기 전 DangerRegistry.IsDangerousArea()를 통해
//         과거에 위협이 있었던 구역을 회피할 수 있게 한다.
// 사용법: GameManager와 동일한 GameObject에 추가됨 (GameManager.CacheComponents에서 자동 추가).
//         직접 접근: GameManager.Instance.DangerRegistry
// 의존성: AIVillage.Core.MessageBus, AIVillage.Core.GameManager
// GDD   : §9 DangerRegistry / 위험 구역 회피 / 기록 수명 120초
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 특정 좌표의 위험 정보를 보관하는 읽기 전용 구조체.
    ///
    /// readonly struct를 사용하는 이유:
    ///   - 기록은 생성 후 수정하지 않는다 (불변 데이터).
    ///   - 힙 할당 없이 List에 값 복사로 보관하여 GC 부담을 줄인다.
    /// </summary>
    public readonly struct DangerRecord
    {
        /// <summary>위험이 감지된 월드 좌표.</summary>
        public readonly Vector2 Location;

        /// <summary>위험 수준. 기획서: 몬스터 위협 = 2</summary>
        public readonly int DangerLevel;

        /// <summary>기록 생성 시점의 Time.time 값 (초).</summary>
        public readonly float Timestamp;

        /// <summary>DangerRecord 생성자.</summary>
        /// <param name="location">위험 발생 월드 좌표</param>
        /// <param name="dangerLevel">위험 수준 (몬스터 위협: 2)</param>
        /// <param name="timestamp">기록 생성 시각 (Time.time)</param>
        public DangerRecord(Vector2 location, int dangerLevel, float timestamp)
        {
            Location    = location;
            DangerLevel = dangerLevel;
            Timestamp   = timestamp;
        }
    }

    /// <summary>
    /// 위험 구역 이력을 관리하는 싱글톤 매니저.
    ///
    /// 동작 원리:
    ///   1. "unit.fleeing" 메시지 수신 → 해당 좌표를 DangerRecord로 기록
    ///   2. Gatherer가 노드 선택 시 IsDangerousArea()를 호출하여 위험 구역 필터링
    ///   3. 기록은 _recordLifetime(120초) 후 CleanupRoutine 코루틴에 의해 자동 삭제
    ///
    /// Start()에서 MessageBus를 구독하는 이유:
    ///   GameManager.Awake → CacheComponents → AddComponent 순서로 생성되므로
    ///   Awake 타이밍 충돌을 피하기 위해 Start에서 구독한다.
    /// </summary>
    public sealed class DangerRegistry : MonoBehaviour
    {
        #region ── Singleton ──

        /// <summary>씬 전역 DangerRegistry 인스턴스.</summary>
        public static DangerRegistry Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DangerRegistry] 중복 인스턴스 감지 — 이 컴포넌트를 파괴합니다.");
                Destroy(this);
                return;
            }

            Instance = this;
        }

        #endregion

        #region ── Serialized Fields ──

        [Header("위험 기록 설정 (GDD §9)")]
        [Tooltip("위험 기록의 유효 수명 (초). 이 시간이 지나면 자동 삭제됨.")]
        [SerializeField] private float _recordLifetime = 120f; // 기획서 수치: 120초

        [Tooltip("위험 구역 판정 반경 (타일). Gatherer의 safeNodeRadius와 합산하여 최종 범위 결정.")]
        [SerializeField] private float _dangerRadius = 4f; // 기획서 수치: 4타일

        #endregion

        #region ── Private Fields ──

        /// <summary>활성 및 만료된 위험 기록 목록. CleanupRoutine이 주기적으로 만료 기록을 제거한다.</summary>
        private List<DangerRecord> _records = new List<DangerRecord>();

        #endregion

        #region ── Unity Lifecycle ──

        /// <summary>
        /// Start에서 MessageBus를 구독하고 CleanupRoutine을 시작한다.
        /// Awake가 아닌 Start에서 구독하는 이유:
        ///   MessageBus.Awake가 이 컴포넌트의 Awake보다 늦게 실행될 수 있으므로
        ///   안전한 Start 단계에서 구독한다.
        /// </summary>
        private void Start()
        {
            GameManager.Instance?.MessageBus?.Subscribe("unit.fleeing", OnUnitFleeing);
            StartCoroutine(CleanupRoutine());
        }

        /// <summary>
        /// 오브젝트 파괴 시 구독 해제, 코루틴 정지, 인스턴스 초기화를 수행한다.
        /// </summary>
        private void OnDestroy()
        {
            GameManager.Instance?.MessageBus?.Unsubscribe("unit.fleeing", OnUnitFleeing);
            StopAllCoroutines();

            if (Instance == this)
                Instance = null;
        }

        #endregion

        #region ── Public API ──

        /// <summary>
        /// 지정한 위치에 위험 기록을 추가한다.
        /// 새 기록은 현재 Time.time을 Timestamp로 가지며 _recordLifetime 동안 유효하다.
        /// </summary>
        /// <param name="location">위험 발생 월드 좌표</param>
        /// <param name="dangerLevel">위험 수준 (기획서: 몬스터 위협 = 2)</param>
        public void RecordDanger(Vector2 location, int dangerLevel)
        {
            DangerRecord record = new DangerRecord(location, dangerLevel, Time.time);
            _records.Add(record);
#if UNITY_EDITOR
            Debug.Log($"[DangerRegistry] 위험 기록 추가 — 위치: {location}, 위험도: {dangerLevel}, 총 기록 수: {_records.Count}");
#endif
        }

        /// <summary>
        /// 지정한 위치가 활성화된 위험 기록의 영향권 내에 있는지 확인한다.
        ///
        /// 판정 공식:
        ///   거리(position, record.Location) &lt; (_dangerRadius + checkRadius)
        ///   이고 기록이 만료되지 않은 경우 → 위험 구역 (true 반환)
        ///
        /// Gatherer는 checkRadius에 자신의 _safeNodeRadius를 전달하여
        /// "노드 안전 반경 + 위험 기록 반경"을 합산한 범위로 판정한다.
        /// </summary>
        /// <param name="position">검사할 월드 좌표</param>
        /// <param name="checkRadius">호출자의 추가 안전 반경 (Gatherer._safeNodeRadius 등)</param>
        /// <returns>하나라도 해당 기록이 있으면 true, 없으면 false</returns>
        public bool IsDangerousArea(Vector2 position, float checkRadius)
        {
            float currentTime = Time.time;
            // Formula: totalRadius = _dangerRadius + checkRadius
            float totalRadius = _dangerRadius + checkRadius;

            foreach (DangerRecord record in _records)
            {
                // 만료 기록 제외
                if ((currentTime - record.Timestamp) >= _recordLifetime) continue;

                // 거리가 합산 반경보다 작으면 위험 구역
                if (Vector2.Distance(position, record.Location) < totalRadius)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 만료되지 않은 활성 위험 기록 목록을 반환한다.
        /// UI 디버그 표시 또는 에디터 Gizmo용으로 사용한다.
        /// </summary>
        public IReadOnlyList<DangerRecord> GetAllActiveRecords()
        {
            float currentTime = Time.time;
            var active = new List<DangerRecord>(_records.Count);

            foreach (DangerRecord record in _records)
            {
                if ((currentTime - record.Timestamp) < _recordLifetime)
                    active.Add(record);
            }

            return active;
        }

        #endregion

        #region ── Private Methods ──

        /// <summary>
        /// "unit.fleeing" 메시지 핸들러.
        /// 페이로드(Vector2 도주 위치)를 DangerRecord로 기록한다.
        /// </summary>
        private void OnUnitFleeing(object payload)
        {
            if (payload is Vector2 pos)
            {
                // 기획서 수치: 몬스터 위협으로 인한 도주 = 위험도 2
                RecordDanger(pos, dangerLevel: 2);
            }
            else
            {
                Debug.LogWarning($"[DangerRegistry] OnUnitFleeing — 예상치 못한 페이로드 타입: {payload?.GetType().Name ?? "null"}");
            }
        }

        /// <summary>
        /// 1초마다 만료된 위험 기록을 제거하는 코루틴.
        /// OnDestroy에서 StopAllCoroutines()로 자동 중단된다.
        /// </summary>
        private IEnumerator CleanupRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                RemoveExpiredRecords();
            }
        }

        /// <summary>
        /// 만료된 위험 기록을 _records에서 제거한다.
        /// </summary>
        private void RemoveExpiredRecords()
        {
            float currentTime = Time.time;
            int removedCount = _records.RemoveAll(r => (currentTime - r.Timestamp) >= _recordLifetime);

            if (removedCount > 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[DangerRegistry] 만료 기록 {removedCount}건 삭제. 활성 기록: {_records.Count}건");
#endif
            }
        }

        #endregion

        #region ── Editor Gizmos ──
#if UNITY_EDITOR

        /// <summary>
        /// Scene 뷰에서 활성 위험 기록을 시각화한다.
        /// 위험도 2 = 주황색 / 기타 = 노랑색
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_records == null) return;

            float currentTime = Time.time;

            foreach (DangerRecord record in _records)
            {
                if ((currentTime - record.Timestamp) >= _recordLifetime) continue;

                Vector3 gizmoPos = new Vector3(record.Location.x, record.Location.y, 0f);

                Color fillColor;
                Color outlineColor;

                if (record.DangerLevel == 2)
                {
                    fillColor    = new Color(1f, 0.5f, 0f, 0.08f); // 주황 반투명 채움
                    outlineColor = new Color(1f, 0.5f, 0f, 0.9f);  // 주황 테두리
                }
                else
                {
                    fillColor    = new Color(1f, 0.95f, 0f, 0.08f); // 노랑 반투명 채움
                    outlineColor = new Color(1f, 0.95f, 0f, 0.9f);  // 노랑 테두리
                }

                Gizmos.color = fillColor;
                Gizmos.DrawSphere(gizmoPos, _dangerRadius);

                Gizmos.color = outlineColor;
                Gizmos.DrawWireSphere(gizmoPos, _dangerRadius);
            }
        }

#endif
        #endregion
    }
}
