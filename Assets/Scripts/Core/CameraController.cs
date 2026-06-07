// =============================================================================
// CameraController.cs
// 역할  : 마우스 엣지 스크롤링으로 카메라를 이동시키는 컨트롤러.
//         마우스 커서가 화면 경계(_edgeThreshold px 이내)에 닿으면 카메라가 이동한다.
// 사용법: MainCamera GameObject에 이 컴포넌트를 추가.
//         Inspector에서 _scrollSpeed, _edgeThreshold, 맵 경계 범위를 설정.
// 의존성: UnityEngine.InputSystem
// GDD   : §v0.2-1 카메라 엣지 스크롤링
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace AIVillage.Core
{
    public sealed class CameraController : MonoBehaviour
    {
        #region ── Serialized Fields ──

        [Header("스크롤 설정")]
        [Tooltip("카메라 이동 속도 (유닛/초). Inspector에서 조정 가능.")]
        [SerializeField] private float _scrollSpeed = 8f;

        [Tooltip("스크롤이 시작되는 화면 가장자리 두께 (픽셀).")]
        [SerializeField] private float _edgeThreshold = 20f;

        [Header("카메라 이동 범위 클램핑")]
        [Tooltip("PathfindingGrid._gridOrigin=(-30,-30), 60x60 맵 → 월드 범위 -30~+30")]
        [SerializeField] private float _minX = -30f;
        [SerializeField] private float _maxX = 30f;
        [SerializeField] private float _minY = -30f;
        [SerializeField] private float _maxY = 30f;

        #endregion

        #region ── Unity Lifecycle ──

        private void Update()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            float screenW = Screen.width;
            float screenH = Screen.height;

            float dx = 0f;
            float dy = 0f;

            if (mousePos.x <= _edgeThreshold)                dx = -1f;
            else if (mousePos.x >= screenW - _edgeThreshold) dx =  1f;

            if (mousePos.y <= _edgeThreshold)                dy = -1f;
            else if (mousePos.y >= screenH - _edgeThreshold) dy =  1f;

            if (dx == 0f && dy == 0f) return;

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + dx * _scrollSpeed * Time.deltaTime, _minX, _maxX);
            pos.y = Mathf.Clamp(pos.y + dy * _scrollSpeed * Time.deltaTime, _minY, _maxY);
            transform.position = pos;
        }

        #endregion
    }
}
