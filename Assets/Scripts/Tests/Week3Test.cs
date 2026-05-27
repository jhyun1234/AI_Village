// =============================================================================
// Week3Test.cs
// 역할  : Week 3 Gatherer 이동 테스트. 씬 플레이 시 스페이스바로 목적지 설정.
// 사용법: 빈 오브젝트 "Week3Tester"에 추가. Gatherer와 TargetPosition을 Inspector 연결.
// 제거  : Week 4 시작 전 삭제 또는 비활성화.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using AIVillage.Units;

namespace AIVillage.Tests
{
    public class Week3Test : MonoBehaviour
    {
        [Header("테스트 대상")]
        [SerializeField, Tooltip("이동시킬 Gatherer 오브젝트")]
        private Gatherer _gatherer;

        [SerializeField, Tooltip("목적지 월드 좌표 (씬에서 Transform으로 지정)")]
        private Transform _targetPosition;

        private void Update()
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (_gatherer == null)
                {
                    Debug.LogError("[Week3Test] Gatherer가 연결되지 않았습니다.");
                    return;
                }

                if (_targetPosition == null)
                {
                    Debug.LogError("[Week3Test] TargetPosition이 연결되지 않았습니다.");
                    return;
                }

                Debug.Log($"[Week3Test] 목적지 설정: {_targetPosition.position}");
                _gatherer.SetDestination(_targetPosition.position);
            }
        }
    }
}
