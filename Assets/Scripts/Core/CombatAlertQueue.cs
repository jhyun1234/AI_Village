// =============================================================================
// CombatAlertQueue.cs
// 역할  : Gatherer가 감지한 위협 경보를 FIFO 큐로 관리하고,
//         플레이어가 Q/W/E/Space 키로 Warrior 파견 또는 스킵을 결정한다.
// 사용법: GameManager.CacheComponents()에서 자동 AddComponent.
//         Gatherer.OnThreatDetected()에서 EnqueueAlert() 호출.
//         Barracks.OnBuilt()에서 RegisterBarracks(), OnDestroy()에서 UnregisterBarracks() 호출.
//         플레이어 입력: Q=1명, W=2명, E=전원, Space=스킵
// 의존성: UnityEngine.InputSystem, AIVillage.Units.Monster, AIVillage.Buildings.Barracks
// GDD   : §v0.2-4 CombatAlertQueue / §v0.2-3 Warrior 파견
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AIVillage.Units;
using AIVillage.Buildings;

namespace AIVillage.Core
{
    /// <summary>몬스터 위협 강도 4단계.</summary>
    public enum ThreatLevel
    {
        /// <summary>Warrior 1명으로 처리 가능.</summary>
        Weak,

        /// <summary>Warrior 2명 필요.</summary>
        Strong,

        /// <summary>Warrior 3명 필요.</summary>
        VeryStrong,

        /// <summary>Warrior 3명도 위험 (50% 미만 승률).</summary>
        AllNeeded
    }

    /// <summary>
    /// 경보 큐 싱글톤. Gatherer가 위협을 감지할 때마다 경보를 등록하고,
    /// 플레이어 키 입력에 따라 front부터 순차 처리한다.
    /// Barracks 목록을 캐싱하여 매 프레임 FindObjectsOfType 호출을 방지한다.
    /// </summary>
    public sealed class CombatAlertQueue : MonoBehaviour
    {
        #region ── Nested Type ──

        private struct CombatAlert
        {
            public Monster     Target;    // null이면 이미 처치됨 → 자동 스킵
            public ThreatLevel Level;
            public Vector2     Position;  // 경보 발생 위치 (Monster 이동 후에도 파견 좌표 보존)
            public float       Timestamp;
        }

        #endregion

        #region ── Singleton ──

        public static CombatAlertQueue Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region ── State ──

        private readonly Queue<CombatAlert>  _alertQueue    = new Queue<CombatAlert>();
        private readonly List<Barracks>      _knownBarracks = new List<Barracks>();

        // 파견 계산용 재사용 버퍼 — 매 키 입력마다 new 하지 않는다 (GC 최적화)
        private readonly List<Warrior> _dispatchBuffer = new List<Warrior>();

        // 활성 경보 콘솔 출력 여부 (동일 경보 반복 출력 방지)
        private bool _alertDisplayed = false;

        #endregion

        #region ── Unity Lifecycle ──

        private void Update()
        {
            SkipDeadAlerts();
            DisplayCurrentAlert();
            HandlePlayerInput();
        }

        #endregion

        #region ── Barracks Registry ──

        /// <summary>Barracks.OnBuilt()에서 호출. FindObjectsOfType 없이 Warrior를 조회하기 위해 등록.</summary>
        public void RegisterBarracks(Barracks b)
        {
            if (b != null && !_knownBarracks.Contains(b))
                _knownBarracks.Add(b);
        }

        /// <summary>Barracks.OnDestroy()에서 호출. 파괴된 Barracks를 목록에서 제거.</summary>
        public void UnregisterBarracks(Barracks b)
        {
            _knownBarracks.Remove(b);
        }

        #endregion

        #region ── Public API ──

        /// <summary>
        /// Gatherer.OnThreatDetected()에서 호출. 경보를 큐에 추가한다.
        /// 기존 큐는 유지한다 (덮어쓰기 없음, FIFO).
        /// </summary>
        public void EnqueueAlert(Monster target, ThreatLevel level, Vector2 position)
        {
            if (target == null) return;

            _alertQueue.Enqueue(new CombatAlert
            {
                Target    = target,
                Level     = level,
                Position  = position,
                Timestamp = Time.time
            });

            _alertDisplayed = false;
#if UNITY_EDITOR
            Debug.Log($"[CombatAlertQueue] 경보 등록 — {LevelToString(level)} | 큐 크기: {_alertQueue.Count}");
#endif
        }

        #endregion

        #region ── Alert Processing ──

        private void SkipDeadAlerts()
        {
            while (_alertQueue.Count > 0 && _alertQueue.Peek().Target == null)
            {
                _alertQueue.Dequeue();
                _alertDisplayed = false;
#if UNITY_EDITOR
                Debug.Log("[CombatAlertQueue] 처치된 Monster 경보 자동 스킵.");
#endif
            }
        }

        private void DisplayCurrentAlert()
        {
            if (_alertDisplayed || _alertQueue.Count == 0) return;
            _alertDisplayed = true;

            CombatAlert alert = _alertQueue.Peek();

            // 가용 Warrior 목록을 버퍼에 수집 (경보당 1회만 실행)
            CollectAvailableWarriors();
            int available = _dispatchBuffer.Count;

            if (available == 0)
            {
                Debug.Log($"[경보] Warrior 없음 — 파견 불가 ({LevelToString(alert.Level)} 위협 감지)");
            }
            else
            {
                Debug.Log($"[경보 {_alertQueue.Count}건 대기] {LevelToString(alert.Level)} | " +
                          $"Q:1명 W:2명 E:전원 Space:스킵");
            }
        }

        private void HandlePlayerInput()
        {
            if (_alertQueue.Count == 0) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.qKey.wasPressedThisFrame)      DispatchWarriors(1);
            else if (Keyboard.current.wKey.wasPressedThisFrame) DispatchWarriors(2);
            else if (Keyboard.current.eKey.wasPressedThisFrame) DispatchWarriors(int.MaxValue);
            else if (Keyboard.current.spaceKey.wasPressedThisFrame) SkipAlert();
        }

        private void DispatchWarriors(int count)
        {
            if (_alertQueue.Count == 0) return;

            CombatAlert alert = _alertQueue.Dequeue();
            _alertDisplayed = false;

            if (alert.Target == null)
            {
#if UNITY_EDITOR
                Debug.Log("[CombatAlertQueue] 파견 취소 — Monster 이미 처치됨.");
#endif
                return;
            }

            CollectAvailableWarriors();
            int toDispatch = Mathf.Min(count, _dispatchBuffer.Count);

            if (toDispatch == 0)
            {
                Debug.Log("[CombatAlertQueue] 파견 가능한 Warrior 없음.");
                return;
            }

            for (int i = 0; i < toDispatch; i++)
                _dispatchBuffer[i].TryDispatch(alert.Position);

#if UNITY_EDITOR
            Debug.Log($"[CombatAlertQueue] Warrior {toDispatch}명 파견 → {alert.Position}");
#endif
        }

        private void SkipAlert()
        {
            if (_alertQueue.Count == 0) return;
            _alertQueue.Dequeue();
            _alertDisplayed = false;
#if UNITY_EDITOR
            Debug.Log("[CombatAlertQueue] 경보 스킵.");
#endif
        }

        #endregion

        #region ── Warrior Search ──

        /// <summary>
        /// 캐시된 Barracks 목록에서 파견 가능한 Warrior를 _dispatchBuffer에 수집한다.
        /// 매 조회마다 _dispatchBuffer.Clear() 후 재사용하므로 GC 할당이 없다.
        /// </summary>
        private void CollectAvailableWarriors()
        {
            _dispatchBuffer.Clear();
            foreach (Barracks b in _knownBarracks)
            {
                if (b != null && b.IsBuilt)
                    b.CollectAvailableWarriors(_dispatchBuffer);
            }
        }

        #endregion

        #region ── Helpers ──

        private static string LevelToString(ThreatLevel level)
        {
            return level switch
            {
                ThreatLevel.Weak       => "약함",
                ThreatLevel.Strong     => "강함",
                ThreatLevel.VeryStrong => "매우강함",
                ThreatLevel.AllNeeded  => "전부필요",
                _                      => "알수없음"
            };
        }

        #endregion
    }
}
