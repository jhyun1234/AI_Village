// =============================================================================
// PathfindingGrid.cs
// 역할  : 60x60 타일 그리드 싱글톤. 월드 좌표 ↔ 그리드 좌표 변환,
//         AStar 패키지용 walkableMap 제공.
// 사용법: Hierarchy에 빈 오브젝트 "PathfindingGrid" 생성 후 컴포넌트 추가.
//         AIUnit이 SetDestination 시 자동으로 참조한다.
// 의존성: 없음 (순수 그리드 관리)
// GDD   : §3 맵 60x60 타일 / Week 9 DangerRegistry 연동 예정
// =============================================================================

using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// 게임 맵의 타일 그리드를 관리하는 싱글톤.
    /// AStar 2D Grid Pathfinding 패키지에 전달할 bool[,] walkableMap을 보관하며,
    /// 월드 좌표와 그리드 좌표 간 변환을 담당한다.
    /// </summary>
    public sealed class PathfindingGrid : MonoBehaviour
    {
        #region Singleton

        public static PathfindingGrid Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PathfindingGrid] 중복 인스턴스 감지 — 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeGrid();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region Inspector Fields

        [Header("그리드 크기 (GDD §3)")]
        [SerializeField, Tooltip("가로 타일 수. GDD: 60")]
        private int _width = 60;  // GDD §3

        [SerializeField, Tooltip("세로 타일 수. GDD: 60")]
        private int _height = 60; // GDD §3

        [SerializeField, Tooltip("타일 1개의 Unity 단위 크기")]
        private float _tileSize = 1f;

        [SerializeField, Tooltip("그리드 좌하단 월드 좌표. 60x60 그리드를 중앙(0,0) 기준으로 배치하려면 (-30,-30)")]
        private Vector2 _gridOrigin = new Vector2(-30f, -30f);

        #endregion

        #region Private State

        // AStar 패키지 요구사항: [행(y), 열(x)] 순서
        private bool[,] _walkableMap;

        #endregion

        #region Initialization

        /// <summary>그리드를 초기화한다. 기본값: 모든 타일 이동 가능.</summary>
        private void InitializeGrid()
        {
            _walkableMap = new bool[_height, _width]; // [행, 열] = [y, x]

            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    _walkableMap[y, x] = true;

            Debug.Log($"[PathfindingGrid] {_width}x{_height} 그리드 초기화 완료. 원점: {_gridOrigin}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// AStar 패키지에 전달할 walkableMap의 복사본을 반환한다.
        /// 비동기 Task가 라이브 배열을 직접 참조하지 않도록 복사본을 사용한다.
        /// </summary>
        public bool[,] GetWalkableMapCopy()
        {
            bool[,] copy = new bool[_height, _width];
            System.Array.Copy(_walkableMap, copy, _walkableMap.Length);
            return copy;
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표 (열, 행)으로 변환한다.
        /// 그리드 범위를 벗어난 값은 경계로 클램프된다.
        /// </summary>
        public Vector2Int WorldToGrid(Vector2 worldPos)
        {
            int col = Mathf.FloorToInt((worldPos.x - _gridOrigin.x) / _tileSize);
            int row = Mathf.FloorToInt((worldPos.y - _gridOrigin.y) / _tileSize);

            col = Mathf.Clamp(col, 0, _width - 1);
            row = Mathf.Clamp(row, 0, _height - 1);

            return new Vector2Int(col, row); // x=열, y=행
        }

        /// <summary>
        /// 그리드 좌표 (열, 행)를 타일 중심의 월드 좌표로 변환한다.
        /// </summary>
        public Vector3 GridToWorld(int gridCol, int gridRow)
        {
            float worldX = _gridOrigin.x + (gridCol + 0.5f) * _tileSize;
            float worldY = _gridOrigin.y + (gridRow + 0.5f) * _tileSize;
            return new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// 특정 월드 좌표의 타일 이동 가능 여부를 설정한다.
        /// Week 9 DangerRegistry가 위험 지역 타일을 막을 때 사용한다.
        /// </summary>
        // TODO: Week 9 — DangerRegistry에서 호출
        public void SetWalkable(Vector2 worldPos, bool walkable)
        {
            Vector2Int grid = WorldToGrid(worldPos);
            _walkableMap[grid.y, grid.x] = walkable; // [행, 열]
        }

        #endregion

        #if UNITY_EDITOR
        #region Gizmos

        /// <summary>Scene 뷰에서 그리드 경계를 노란 와이어 박스로 표시한다.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 center = new Vector3(
                _gridOrigin.x + _width * _tileSize * 0.5f,
                _gridOrigin.y + _height * _tileSize * 0.5f,
                0f);
            Vector3 size = new Vector3(_width * _tileSize, _height * _tileSize, 0f);
            Gizmos.DrawWireCube(center, size);
        }

        #endregion
        #endif
    }
}
