using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BalloonOut.Data
{
    /// <summary>
    /// 화살표 배치 로직
    /// 직선/꺾이는 화살표 배치, ReverseGrowth 알고리즘
    /// </summary>
    public static class ArrowPlacer
    {
        // ========== Constants ==========
        private static readonly string[] DIRECTIONS = { "U", "D", "L", "R" };

        private static readonly Dictionary<string, Vector2Int> DIR_VECTORS = new()
        {
            { "U", new Vector2Int(0, -1) },
            { "D", new Vector2Int(0, 1) },
            { "L", new Vector2Int(-1, 0) },
            { "R", new Vector2Int(1, 0) }
        };

        // ReverseGrowth: 방향 전환 우선순위 (직진 > 좌/우, 뒤로 가기 금지)
        private static readonly Dictionary<string, string[]> TURN_PRIORITY = new()
        {
            { "U", new[] { "U", "L", "R" } },
            { "D", new[] { "D", "R", "L" } },
            { "L", new[] { "L", "D", "U" } },
            { "R", new[] { "R", "U", "D" } }
        };

        private static readonly Dictionary<string, string> OPPOSITE = new()
        {
            { "U", "D" }, { "D", "U" }, { "L", "R" }, { "R", "L" }
        };

        // ========== Data Structures ==========
        /// <summary>
        /// 배치 결과
        /// </summary>
        public class PlacementResult
        {
            public int x;
            public int y;
            public string dir;
            public string headDir;
            public List<Vector2Int> cells;
            public List<Vector2Int> path;
        }

        // ========== Utility Functions ==========
        private static T RandomPick<T>(T[] arr)
        {
            return arr[Random.Range(0, arr.Length)];
        }

        private static T RandomPick<T>(List<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static bool IsInBounds(int x, int y, int gridSize)
        {
            return x >= 0 && x < gridSize && y >= 0 && y < gridSize;
        }

        // 직사각형 그리드용 오버로드
        private static bool IsInBounds(int x, int y, int gridWidth, int gridHeight)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        private static string CellKey(int x, int y) => $"{x},{y}";
        private static string CellKey(Vector2Int v) => $"{v.x},{v.y}";

        /// <summary>
        /// 경로가 지정된 셀 집합에 의해 차단되었는지 확인
        /// </summary>
        private static bool IsPathBlockedBySet(List<Vector2Int> path, HashSet<string> cellSet)
        {
            foreach (var p in path)
            {
                if (cellSet.Contains(CellKey(p)))
                    return true;
            }
            return false;
        }

        // ========== Cell Calculation ==========
        private static List<Vector2Int> CalculateCells(int x, int y, string dir, int length)
        {
            var cells = new List<Vector2Int>();
            var d = DIR_VECTORS[dir];

            for (int k = 0; k < length; k++)
            {
                cells.Add(new Vector2Int(x - d.x * k, y - d.y * k));
            }
            return cells;
        }

        /// <summary>
        /// 탈출 경로 계산 (외부에서도 사용 가능)
        /// </summary>
        public static List<Vector2Int> GetEscapePath(int x, int y, string dir, int gridSize)
        {
            return GetEscapePath(x, y, dir, gridSize, gridSize);
        }

        /// <summary>
        /// 탈출 경로 계산 (직사각형 그리드)
        /// </summary>
        public static List<Vector2Int> GetEscapePath(int x, int y, string dir, int gridWidth, int gridHeight)
        {
            var path = new List<Vector2Int>();
            var d = DIR_VECTORS[dir];
            int cx = x + d.x;
            int cy = y + d.y;

            while (IsInBounds(cx, cy, gridWidth, gridHeight))
            {
                path.Add(new Vector2Int(cx, cy));
                cx += d.x;
                cy += d.y;
            }
            return path;
        }

        private static bool HasOverlap(List<Vector2Int> cells, HashSet<string> occupiedSet)
        {
            foreach (var c in cells)
            {
                if (occupiedSet.Contains(CellKey(c)))
                    return true;
            }
            return false;
        }

        private static bool AllCellsInBounds(List<Vector2Int> cells, int gridSize)
        {
            foreach (var c in cells)
            {
                if (!IsInBounds(c.x, c.y, gridSize))
                    return false;
            }
            return true;
        }

        // 직사각형 그리드용 오버로드
        private static bool AllCellsInBounds(List<Vector2Int> cells, int gridWidth, int gridHeight)
        {
            foreach (var c in cells)
            {
                if (!IsInBounds(c.x, c.y, gridWidth, gridHeight))
                    return false;
            }
            return true;
        }

        // ========== Straight Arrow Placement ==========
        /// <summary>
        /// 첫 번째 화살표 배치 (즉시 탈출 가능한 위치) - 정사각형 호환
        /// </summary>
        public static PlacementResult PlaceFirstArrow(int length, int gridSize,
            HashSet<string> occupiedSet, List<LevelValidator.BlockData> existingBlocks = null)
        {
            return PlaceFirstArrow(length, gridSize, gridSize, occupiedSet, existingBlocks);
        }

        /// <summary>
        /// 첫 번째 화살표 배치 (즉시 탈출 가능한 위치) - 직사각형 지원
        /// </summary>
        public static PlacementResult PlaceFirstArrow(int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, List<LevelValidator.BlockData> existingBlocks = null)
        {
            var candidates = new List<PlacementResult>();
            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var dir in dirs)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        var cells = CalculateCells(x, y, dir, length);

                        if (!AllCellsInBounds(cells, gridWidth, gridHeight)) continue;
                        if (HasOverlap(cells, occupiedSet)) continue;

                        // Facing 검사
                        if (LevelValidator.WouldCauseFacing(x, y, dir, existingBlocks)) continue;

                        var escapePath = GetEscapePath(x, y, dir, gridWidth, gridHeight);
                        if (!IsPathBlockedBySet(escapePath, occupiedSet))
                        {
                            candidates.Add(new PlacementResult
                            {
                                x = x, y = y, dir = dir, cells = cells
                            });
                        }
                    }
                }
            }

            return candidates.Count > 0 ? RandomPick(candidates) : null;
        }

        /// <summary>
        /// 이전 화살표에 의해 막히는 위치에 배치 - 정사각형 호환
        /// </summary>
        public static PlacementResult FindBlockedPosition(int length, int gridSize,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells,
            List<LevelValidator.BlockData> existingBlocks = null)
        {
            return FindBlockedPosition(length, gridSize, gridSize, occupiedSet, blockerCells, existingBlocks);
        }

        /// <summary>
        /// 이전 화살표에 의해 막히는 위치에 배치 - 직사각형 지원
        /// </summary>
        public static PlacementResult FindBlockedPosition(int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells,
            List<LevelValidator.BlockData> existingBlocks = null)
        {
            var candidates = new List<PlacementResult>();
            var blockerSet = new HashSet<string>();
            foreach (var c in blockerCells)
            {
                blockerSet.Add(CellKey(c));
            }

            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var dir in dirs)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        var cells = CalculateCells(x, y, dir, length);

                        if (!AllCellsInBounds(cells, gridWidth, gridHeight)) continue;
                        if (HasOverlap(cells, occupiedSet)) continue;

                        // Facing 검사
                        if (LevelValidator.WouldCauseFacing(x, y, dir, existingBlocks)) continue;

                        var escapePath = GetEscapePath(x, y, dir, gridWidth, gridHeight);

                        // blocker에 의해 막혔는지 확인
                        if (IsPathBlockedBySet(escapePath, blockerSet))
                        {
                            // blocker 제외한 다른 화살표에 의해 막혔는지 확인
                            var otherOccupied = new HashSet<string>(occupiedSet);
                            otherOccupied.ExceptWith(blockerSet);

                            if (!IsPathBlockedBySet(escapePath, otherOccupied))
                            {
                                candidates.Add(new PlacementResult
                                {
                                    x = x, y = y, dir = dir, cells = cells
                                });
                            }
                        }
                    }
                }
            }

            return candidates.Count > 0 ? RandomPick(candidates) : null;
        }

        /// <summary>
        /// Fallback 배치 (빈 공간에 배치) - 정사각형 호환
        /// </summary>
        public static PlacementResult PlaceFallback(int length, int gridSize,
            HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null,
            bool checkCanEscape = false, List<LevelValidator.BlockData> existingBlocks = null)
        {
            return PlaceFallback(length, gridSize, gridSize, occupiedSet, forbiddenCells, checkCanEscape, existingBlocks);
        }

        /// <summary>
        /// Fallback 배치 (빈 공간에 배치) - 직사각형 지원
        /// </summary>
        public static PlacementResult PlaceFallback(int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null,
            bool checkCanEscape = false, List<LevelValidator.BlockData> existingBlocks = null)
        {
            var candidates = new List<PlacementResult>();
            var fallbackCandidates = new List<PlacementResult>();
            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var dir in dirs)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        var cells = CalculateCells(x, y, dir, length);

                        if (!AllCellsInBounds(cells, gridWidth, gridHeight)) continue;
                        if (HasOverlap(cells, occupiedSet)) continue;

                        // Facing 검사
                        if (LevelValidator.WouldCauseFacing(x, y, dir, existingBlocks)) continue;

                        // 탈출 가능 여부 확인
                        if (checkCanEscape)
                        {
                            var escapePath = GetEscapePath(x, y, dir, gridWidth, gridHeight);
                            if (IsPathBlockedBySet(escapePath, occupiedSet)) continue;
                        }

                        var result = new PlacementResult
                        {
                            x = x, y = y, dir = dir, cells = cells
                        };

                        // forbidden cells와 겹치는지 확인
                        if (forbiddenCells != null)
                        {
                            bool overlapsWithForbidden = false;
                            foreach (var cell in cells)
                            {
                                if (forbiddenCells.Contains(CellKey(cell)))
                                {
                                    overlapsWithForbidden = true;
                                    break;
                                }
                            }

                            if (overlapsWithForbidden)
                            {
                                fallbackCandidates.Add(result);
                                continue;
                            }
                        }

                        candidates.Add(result);
                    }
                }
            }

            if (candidates.Count > 0)
                return RandomPick(candidates);

            if (fallbackCandidates.Count > 0)
                return RandomPick(fallbackCandidates);

            return null;
        }

        // ========== Bending Arrow Placement (ReverseGrowth) ==========
        // 정사각형 호환 오버로드
        private static (int x, int y, string dir)? FindNextGrowthCell(int x, int y, string preferredDir,
            HashSet<string> occupiedSet, int gridSize, HashSet<string> selfPathSet = null)
        {
            return FindNextGrowthCell(x, y, preferredDir, occupiedSet, gridSize, gridSize, selfPathSet);
        }

        // 직사각형 지원
        private static (int x, int y, string dir)? FindNextGrowthCell(int x, int y, string preferredDir,
            HashSet<string> occupiedSet, int gridWidth, int gridHeight, HashSet<string> selfPathSet = null)
        {
            var priority = TURN_PRIORITY[preferredDir];
            var shuffledTurns = new List<string> { priority[1], priority[2] };
            Shuffle(shuffledTurns);
            var searchOrder = new List<string> { priority[0] };
            searchOrder.AddRange(shuffledTurns);

            foreach (var dir in searchOrder)
            {
                var d = DIR_VECTORS[dir];
                int nx = x + d.x;
                int ny = y + d.y;

                if (!IsInBounds(nx, ny, gridWidth, gridHeight) || occupiedSet.Contains(CellKey(nx, ny)))
                    continue;

                if (selfPathSet != null && selfPathSet.Contains(CellKey(nx, ny)))
                    continue;

                return (nx, ny, dir);
            }
            return null;
        }

        // 정사각형 호환 오버로드
        private static (List<Vector2Int> path, string headDir)? GrowArrowReverse(int headX, int headY, string headDir,
            int targetLength, HashSet<string> occupiedSet, int gridSize)
        {
            return GrowArrowReverse(headX, headY, headDir, targetLength, occupiedSet, gridSize, gridSize);
        }

        // 직사각형 지원
        private static (List<Vector2Int> path, string headDir)? GrowArrowReverse(int headX, int headY, string headDir,
            int targetLength, HashSet<string> occupiedSet, int gridWidth, int gridHeight)
        {
            var path = new List<Vector2Int> { new Vector2Int(headX, headY) };
            var selfPathSet = new HashSet<string> { CellKey(headX, headY) };

            string currentDir = OPPOSITE[headDir];
            int currentX = headX;
            int currentY = headY;

            for (int i = 1; i < targetLength; i++)
            {
                var next = FindNextGrowthCell(currentX, currentY, currentDir, occupiedSet, gridWidth, gridHeight, selfPathSet);

                if (!next.HasValue)
                    break;

                path.Add(new Vector2Int(next.Value.x, next.Value.y));
                selfPathSet.Add(CellKey(next.Value.x, next.Value.y));
                currentX = next.Value.x;
                currentY = next.Value.y;
                currentDir = next.Value.dir;
            }

            if (path.Count >= 2)
            {
                return (path, headDir);
            }
            return null;
        }

        // 정사각형 호환 오버로드
        private static List<Vector2Int> GetEdgePositions(string dir, int gridSize)
        {
            return GetEdgePositions(dir, gridSize, gridSize);
        }

        // 직사각형 지원
        private static List<Vector2Int> GetEdgePositions(string dir, int gridWidth, int gridHeight)
        {
            var positions = new List<Vector2Int>();

            switch (dir)
            {
                case "U":  // 위쪽 가장자리: y=0, x는 width 범위
                    for (int x = 0; x < gridWidth; x++) positions.Add(new Vector2Int(x, 0));
                    break;
                case "D":  // 아래쪽 가장자리: y=height-1, x는 width 범위
                    for (int x = 0; x < gridWidth; x++) positions.Add(new Vector2Int(x, gridHeight - 1));
                    break;
                case "L":  // 왼쪽 가장자리: x=0, y는 height 범위
                    for (int y = 0; y < gridHeight; y++) positions.Add(new Vector2Int(0, y));
                    break;
                case "R":  // 오른쪽 가장자리: x=width-1, y는 height 범위
                    for (int y = 0; y < gridHeight; y++) positions.Add(new Vector2Int(gridWidth - 1, y));
                    break;
            }

            Shuffle(positions);
            return positions;
        }

        /// <summary>
        /// 첫 번째 꺾이는 화살표 배치 - 정사각형 호환
        /// </summary>
        public static PlacementResult PlaceFirstArrowBending(int length, int gridSize,
            HashSet<string> occupiedSet, List<LevelValidator.BlockData> existingBlocks = null)
        {
            return PlaceFirstArrowBending(length, gridSize, gridSize, occupiedSet, existingBlocks);
        }

        /// <summary>
        /// 첫 번째 꺾이는 화살표 배치 - 직사각형 지원
        /// </summary>
        public static PlacementResult PlaceFirstArrowBending(int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, List<LevelValidator.BlockData> existingBlocks = null)
        {
            var candidates = new List<PlacementResult>();
            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var headDir in dirs)
            {
                var edgePositions = GetEdgePositions(headDir, gridWidth, gridHeight);

                foreach (var pos in edgePositions)
                {
                    if (occupiedSet.Contains(CellKey(pos))) continue;

                    // Facing 검사
                    if (LevelValidator.WouldCauseFacing(pos.x, pos.y, headDir, existingBlocks)) continue;

                    var result = GrowArrowReverse(pos.x, pos.y, headDir, length, occupiedSet, gridWidth, gridHeight);

                    if (result.HasValue && result.Value.path.Count >= Mathf.Min(length, 2))
                    {
                        var escapePath = GetEscapePath(pos.x, pos.y, headDir, gridWidth, gridHeight);
                        if (!IsPathBlockedBySet(escapePath, occupiedSet))
                        {
                            candidates.Add(new PlacementResult
                            {
                                x = pos.x,
                                y = pos.y,
                                headDir = result.Value.headDir,
                                path = result.Value.path,
                                cells = result.Value.path
                            });
                        }
                    }
                }
            }

            return candidates.Count > 0 ? RandomPick(candidates) : null;
        }

        /// <summary>
        /// 꺾이는 화살표 - 이전 화살표에 의해 막히는 위치에 배치 - 정사각형 호환
        /// </summary>
        public static PlacementResult FindBlockedPositionBending(int length, int gridSize,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells,
            List<LevelValidator.BlockData> existingBlocks = null)
        {
            return FindBlockedPositionBending(length, gridSize, gridSize, occupiedSet, blockerCells, existingBlocks);
        }

        /// <summary>
        /// 꺾이는 화살표 - 이전 화살표에 의해 막히는 위치에 배치 - 직사각형 지원
        /// </summary>
        public static PlacementResult FindBlockedPositionBending(int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells,
            List<LevelValidator.BlockData> existingBlocks = null)
        {
            var candidates = new List<PlacementResult>();
            var blockerSet = new HashSet<string>();
            foreach (var c in blockerCells)
            {
                blockerSet.Add(CellKey(c));
            }

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (occupiedSet.Contains(CellKey(x, y))) continue;

                    var dirs = new List<string>(DIRECTIONS);
                    Shuffle(dirs);

                    foreach (var headDir in dirs)
                    {
                        // Facing 검사
                        if (LevelValidator.WouldCauseFacing(x, y, headDir, existingBlocks)) continue;

                        var result = GrowArrowReverse(x, y, headDir, length, occupiedSet, gridWidth, gridHeight);

                        if (!result.HasValue || result.Value.path.Count < Mathf.Min(length, 2)) continue;

                        var escapePath = GetEscapePath(x, y, headDir, gridWidth, gridHeight);

                        // blocker에 의해 막혔는지 확인
                        if (IsPathBlockedBySet(escapePath, blockerSet))
                        {
                            // blocker 제외한 다른 화살표에 의해 막혔는지 확인
                            var otherOccupied = new HashSet<string>(occupiedSet);
                            otherOccupied.ExceptWith(blockerSet);

                            if (!IsPathBlockedBySet(escapePath, otherOccupied))
                            {
                                candidates.Add(new PlacementResult
                                {
                                    x = x,
                                    y = y,
                                    headDir = result.Value.headDir,
                                    path = result.Value.path,
                                    cells = result.Value.path
                                });
                            }
                        }
                    }
                }
            }

            return candidates.Count > 0 ? RandomPick(candidates) : null;
        }

        /// <summary>
        /// 꺾이는 화살표 Fallback 배치 - 정사각형 호환
        /// </summary>
        public static PlacementResult PlaceFallbackBending(int length, int gridSize,
            HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null,
            bool checkCanEscape = false, List<LevelValidator.BlockData> existingBlocks = null)
        {
            return PlaceFallbackBending(length, gridSize, gridSize, occupiedSet, forbiddenCells, checkCanEscape, existingBlocks);
        }

        /// <summary>
        /// 꺾이는 화살표 Fallback 배치 - 직사각형 지원
        /// </summary>
        public static PlacementResult PlaceFallbackBending(int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null,
            bool checkCanEscape = false, List<LevelValidator.BlockData> existingBlocks = null)
        {
            var candidates = new List<PlacementResult>();
            var fallbackCandidates = new List<PlacementResult>();

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (occupiedSet.Contains(CellKey(x, y))) continue;

                    var dirs = new List<string>(DIRECTIONS);
                    Shuffle(dirs);

                    foreach (var headDir in dirs)
                    {
                        // Facing 검사
                        if (LevelValidator.WouldCauseFacing(x, y, headDir, existingBlocks)) continue;

                        var result = GrowArrowReverse(x, y, headDir, length, occupiedSet, gridWidth, gridHeight);

                        if (result.HasValue && result.Value.path.Count >= Mathf.Min(length, 2))
                        {
                            // 탈출 가능 여부 확인
                            if (checkCanEscape)
                            {
                                var escapePath = GetEscapePath(x, y, headDir, gridWidth, gridHeight);
                                if (IsPathBlockedBySet(escapePath, occupiedSet)) continue;
                            }

                            var placementResult = new PlacementResult
                            {
                                x = x,
                                y = y,
                                headDir = result.Value.headDir,
                                path = result.Value.path,
                                cells = result.Value.path
                            };

                            // forbidden cells와 겹치는지 확인
                            if (forbiddenCells != null)
                            {
                                bool overlapsWithForbidden = false;
                                foreach (var cell in result.Value.path)
                                {
                                    if (forbiddenCells.Contains(CellKey(cell)))
                                    {
                                        overlapsWithForbidden = true;
                                        break;
                                    }
                                }

                                if (overlapsWithForbidden)
                                {
                                    fallbackCandidates.Add(placementResult);
                                    continue;
                                }
                            }

                            candidates.Add(placementResult);
                        }
                    }
                }
            }

            if (candidates.Count > 0)
                return RandomPick(candidates);

            if (fallbackCandidates.Count > 0)
                return RandomPick(fallbackCandidates);

            return null;
        }
    }
}
