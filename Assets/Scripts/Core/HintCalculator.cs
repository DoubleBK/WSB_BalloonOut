using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BalloonOut.Data;
using BalloonOut.Game.Arrow;
using BalloonOut.Game.Grid;
using BalloonOut.UI;

namespace BalloonOut.Core
{
    /// <summary>
    /// 힌트 계산기 - 현재 게임 상태에서 최적의 다음 수 계산
    /// </summary>
    public class HintCalculator
    {
        private static readonly Dictionary<ArrowDirection, Vector2Int> DIR_VECTORS = new()
        {
            { ArrowDirection.Up, new Vector2Int(0, 1) },
            { ArrowDirection.Down, new Vector2Int(0, -1) },
            { ArrowDirection.Left, new Vector2Int(-1, 0) },
            { ArrowDirection.Right, new Vector2Int(1, 0) }
        };

        /// <summary>
        /// 현재 게임 상태에서 힌트 화살표 계산
        /// </summary>
        public ArrowController CalculateHint()
        {
            if (GameManager.Instance == null) return null;

            // 현재 활성 화살표 목록 가져오기
            var arrows = GetActiveArrows();
            if (arrows == null || arrows.Count == 0)
            {
                Debug.Log("[HintCalculator] No active arrows");
                return null;
            }

            // 현재 풍선 상태 가져오기
            var queueUI = FindQueueUI();
            var activeBalloonColors = GetActiveBalloonColors(queueUI);

            // 1. 탈출 가능하고 풍선을 터뜨릴 수 있는 화살표 찾기
            var bestArrow = FindBestArrow(arrows, activeBalloonColors);
            if (bestArrow != null)
            {
                Debug.Log($"[HintCalculator] Found best arrow: ID={bestArrow.Id}, Color={bestArrow.Color}");
                return bestArrow;
            }

            // 2. 탈출 가능한 아무 화살표 (다른 화살표의 경로를 열어줄 수 있음)
            var anyEscapable = FindAnyEscapableArrow(arrows);
            if (anyEscapable != null)
            {
                Debug.Log($"[HintCalculator] Found escapable arrow (no match): ID={anyEscapable.Id}, Color={anyEscapable.Color}");
                return anyEscapable;
            }

            Debug.Log("[HintCalculator] No valid hint found");
            return null;
        }

        /// <summary>
        /// 최적의 화살표 찾기 (탈출 가능 + 풍선 매칭)
        /// </summary>
        private ArrowController FindBestArrow(List<ArrowController> arrows, HashSet<GameColor> activeBalloonColors)
        {
            foreach (var arrow in arrows)
            {
                if (!arrow.CanLaunch) continue;
                if (!CanEscape(arrow, arrows)) continue;
                if (!activeBalloonColors.Contains(arrow.Color)) continue;

                return arrow;
            }
            return null;
        }

        /// <summary>
        /// 탈출 가능한 아무 화살표 찾기
        /// </summary>
        private ArrowController FindAnyEscapableArrow(List<ArrowController> arrows)
        {
            foreach (var arrow in arrows)
            {
                if (!arrow.CanLaunch) continue;
                if (!CanEscape(arrow, arrows)) continue;

                return arrow;
            }
            return null;
        }

        /// <summary>
        /// 화살표가 탈출 가능한지 확인
        /// 경로에 다른 화살표가 없어야 함
        /// </summary>
        private bool CanEscape(ArrowController arrow, List<ArrowController> allArrows)
        {
            if (GridSystem.Instance == null) return false;

            var headPos = arrow.HeadPosition;
            var direction = DIR_VECTORS[arrow.HeadDirection];
            int gridSize = GridSystem.Instance.GridSize;

            // 다른 화살표들이 차지하는 셀들
            var occupiedCells = new HashSet<Vector2Int>();
            foreach (var other in allArrows)
            {
                if (other == arrow) continue;

                var positions = other.GetOccupiedPositions();
                foreach (var pos in positions)
                {
                    occupiedCells.Add(pos);
                }
            }

            // 탈출 경로 시뮬레이션
            var currentPos = headPos + direction;

            while (IsInBounds(currentPos, gridSize))
            {
                if (occupiedCells.Contains(currentPos))
                {
                    // 경로에 다른 화살표가 있음
                    return false;
                }
                currentPos += direction;
            }

            // 그리드 밖으로 나갈 수 있음 = 탈출 가능
            return true;
        }

        /// <summary>
        /// 활성 화살표 목록 가져오기
        /// </summary>
        private List<ArrowController> GetActiveArrows()
        {
            // GameManager의 _arrows 필드에 접근 필요
            // 리플렉션 또는 공개 메서드 사용
            var arrowContainer = GameObject.Find("ArrowContainer");
            if (arrowContainer == null)
            {
                arrowContainer = GameObject.Find("Arrows");
            }

            if (arrowContainer == null)
            {
                // 모든 ArrowController 찾기
                return Object.FindObjectsOfType<ArrowController>()
                    .Where(a => a.State == ArrowState.Idle)
                    .ToList();
            }

            return arrowContainer.GetComponentsInChildren<ArrowController>()
                .Where(a => a.State == ArrowState.Idle)
                .ToList();
        }

        /// <summary>
        /// QueueUI 찾기
        /// </summary>
        private QueueUI FindQueueUI()
        {
            return Object.FindObjectOfType<QueueUI>();
        }

        /// <summary>
        /// 활성 풍선 색상 목록 가져오기
        /// </summary>
        private HashSet<GameColor> GetActiveBalloonColors(QueueUI queueUI)
        {
            var colors = new HashSet<GameColor>();

            if (queueUI == null) return colors;

            // QueueUI에서 활성 풍선 색상 가져오기
            var activeBalloons = queueUI.GetActiveBalloonColors();
            if (activeBalloons != null)
            {
                foreach (var color in activeBalloons)
                {
                    colors.Add(color);
                }
            }

            return colors;
        }

        private bool IsInBounds(Vector2Int pos, int gridSize)
        {
            return pos.x >= 0 && pos.x < gridSize && pos.y >= 0 && pos.y < gridSize;
        }
    }
}
