using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using Random = UnityEngine.Random;

namespace BalloonOut.Data
{
    /// <summary>
    /// Level Generator v8 (ReverseGrowth + Bending) - Unity Port
    ///
    /// 핵심 아이디어: "먼저 배치한 화살표의 Body가 나중 화살표의 탈출 경로를 막는다"
    /// - 첫 번째 화살표: 즉시 탈출 가능
    /// - 이후 화살표: 이전 화살표의 Body에 의해 막힘
    /// </summary>
    public static class LevelGenerator
    {
        // ========== Constants ==========
        private static readonly string[] COLORS = { "R", "G", "Y", "P", "B", "O" };
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
            { "U", new[] { "U", "L", "R" } },  // 위 → 위/좌/우 (아래 금지)
            { "D", new[] { "D", "R", "L" } },  // 아래 → 아래/우/좌 (위 금지)
            { "L", new[] { "L", "D", "U" } },  // 왼쪽 → 왼/아래/위 (오른쪽 금지)
            { "R", new[] { "R", "U", "D" } }   // 오른쪽 → 오른/위/아래 (왼쪽 금지)
        };

        private static readonly Dictionary<string, string> OPPOSITE = new()
        {
            { "U", "D" }, { "D", "U" }, { "L", "R" }, { "R", "L" }
        };

        /// <summary>
        /// Generator 좌표계의 방향을 Game 좌표계로 변환 (Y축 반전)
        /// Generator: U=(0,-1), D=(0,1) / Game: U=(0,1), D=(0,-1)
        /// </summary>
        private static string FlipYDirection(string dir)
        {
            return dir switch
            {
                "U" => "D",
                "D" => "U",
                _ => dir  // L, R은 그대로
            };
        }

        // ========== Configuration ==========
        [Serializable]
        public class GeneratorConfig
        {
            public int gridSize = 8;
            public int laneCount = 2;
            public int balloonsPerLane = 2;
            public int missArrowCount = 1;
            public int minBlockLength = 3;
            public int maxBlockLength = 8;
            public float targetDensity = 0.5f;
            public bool fillerEnabled = true;
            public int fillerMinLength = 2;
            public int fillerMaxLength = 5;
            public bool bendingEnabled = true;
            public float bendingChance = 1.0f;
            public bool branchingMode = false;
            public float branchingChance = 0.4f;
        }

        // ========== Internal Data Structures ==========
        private class PlacementResult
        {
            public int x;
            public int y;
            public string dir;
            public string headDir;
            public List<Vector2Int> cells;
            public List<Vector2Int> path;
        }

        /// <summary>
        /// 레벨 검증 결과
        /// </summary>
        public class ValidationResult
        {
            public bool valid;
            public string reason;
            public bool queueCleared;
            public List<int> escapeSequence;
            public List<string> escapeColors; // 탈출 순서의 색상 목록
        }

        // ========== Auto Parameter Calculation ==========
        public static GeneratorConfig CalculateAutoParams(int gridSize, float targetDensity = 0.5f, bool bendingEnabled = true)
        {
            var config = new GeneratorConfig
            {
                gridSize = gridSize,
                targetDensity = targetDensity,
                bendingEnabled = bendingEnabled
            };

            string sizeCategory = gridSize <= 6 ? "small" : gridSize <= 9 ? "medium" : "large";

            if (bendingEnabled)
            {
                switch (sizeCategory)
                {
                    case "small":
                        config.laneCount = 2;
                        config.balloonsPerLane = 2;
                        config.missArrowCount = 1;
                        config.minBlockLength = 3;
                        config.maxBlockLength = 6;
                        break;
                    case "medium":
                        config.laneCount = 2;
                        config.balloonsPerLane = 3;
                        config.missArrowCount = 2;
                        config.minBlockLength = 4;
                        config.maxBlockLength = 8;
                        break;
                    case "large":
                        config.laneCount = 3;
                        config.balloonsPerLane = 3;
                        config.missArrowCount = 3;
                        config.minBlockLength = 5;
                        config.maxBlockLength = 10;
                        break;
                }
            }
            else
            {
                switch (sizeCategory)
                {
                    case "small":
                        config.laneCount = 2;
                        config.balloonsPerLane = 2;
                        config.missArrowCount = 1;
                        config.minBlockLength = 2;
                        config.maxBlockLength = 4;
                        break;
                    case "medium":
                        config.laneCount = 2;
                        config.balloonsPerLane = 3;
                        config.missArrowCount = 2;
                        config.minBlockLength = 2;
                        config.maxBlockLength = 5;
                        break;
                    case "large":
                        config.laneCount = 3;
                        config.balloonsPerLane = 3;
                        config.missArrowCount = 3;
                        config.minBlockLength = 3;
                        config.maxBlockLength = 6;
                        break;
                }
            }

            // 목표 밀도에 맞춰 조정
            int totalCells = gridSize * gridSize;
            float avgLength = (config.minBlockLength + config.maxBlockLength) / 2f;
            int baseArrowCount = config.laneCount * config.balloonsPerLane + config.missArrowCount;
            float baseDensity = (baseArrowCount * avgLength) / totalCells;

            if (targetDensity > baseDensity + 0.15f)
            {
                if (bendingEnabled)
                {
                    config.maxBlockLength = Mathf.Min(config.maxBlockLength + 3, 20);
                }
                else
                {
                    config.missArrowCount += Mathf.Min(2, 5 - config.missArrowCount);
                }
            }

            config.fillerEnabled = true;
            config.fillerMinLength = bendingEnabled ? 2 : 1;
            config.fillerMaxLength = Mathf.Max(2, bendingEnabled ? Mathf.Min(6, config.maxBlockLength - 2) : Mathf.Min(3, config.maxBlockLength - 1));

            return config;
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

        private static int RandomInt(int min, int max)
        {
            return Random.Range(min, max + 1);
        }

        private static void Shuffle<T>(T[] arr)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
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

        private static string CellKey(int x, int y) => $"{x},{y}";
        private static string CellKey(Vector2Int v) => $"{v.x},{v.y}";

        // ========== Queue Generation ==========
        private static List<List<string>> GenerateQueue(GeneratorConfig config)
        {
            var lanes = new List<List<string>>();
            for (int i = 0; i < config.laneCount; i++)
            {
                var lane = new List<string>();
                for (int j = 0; j < config.balloonsPerLane; j++)
                {
                    lane.Add(RandomPick(COLORS));
                }
                lanes.Add(lane);
            }
            return lanes;
        }

        /// <summary>
        /// 색상 순서 생성 (탈출 순서)
        /// 핵심: lanes에 먼저 Miss 풍선을 추가한 후, 최종 lanes에서 color sequence를 생성
        /// 이렇게 해야 "화살표 탈출 순서 = 풍선 팝 순서"가 보장됨
        /// </summary>
        private static List<string> GetColorSequence(List<List<string>> lanes, int missCount)
        {
            // Step 1: Miss 풍선을 lanes에 먼저 추가
            for (int i = 0; i < missCount; i++)
            {
                string missColor = RandomPick(COLORS);

                // 랜덤 Lane 선택
                int laneIdx = RandomInt(0, lanes.Count - 1);

                // 랜덤 위치에 삽입 (해당 Lane 내)
                int insertPos = RandomInt(0, lanes[laneIdx].Count);
                lanes[laneIdx].Insert(insertPos, missColor);
            }

            // Step 2: 최종 lanes에서 color sequence 생성 (LIFO: lane[end]부터 팝)
            var colors = new List<string>();
            var lanesCopy = new List<List<string>>();
            foreach (var lane in lanes)
            {
                lanesCopy.Add(new List<string>(lane));
            }

            while (lanesCopy.Exists(l => l.Count > 0))
            {
                var nonEmpty = lanesCopy.FindAll(l => l.Count > 0);
                var lane = RandomPick(nonEmpty);
                colors.Add(lane[lane.Count - 1]);  // LIFO: 끝에서 팝
                lane.RemoveAt(lane.Count - 1);
            }

            return colors;
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

        private static List<Vector2Int> GetEscapePath(int x, int y, string dir, int gridSize)
        {
            var path = new List<Vector2Int>();
            var d = DIR_VECTORS[dir];
            int cx = x + d.x;
            int cy = y + d.y;

            while (IsInBounds(cx, cy, gridSize))
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

        // ========== Arrow Placement (Straight) ==========
        private static PlacementResult PlaceFirstArrow(string color, int length, int gridSize, HashSet<string> occupiedSet)
        {
            var candidates = new List<PlacementResult>();
            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var dir in dirs)
            {
                var d = DIR_VECTORS[dir];

                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        var cells = CalculateCells(x, y, dir, length);

                        if (!AllCellsInBounds(cells, gridSize)) continue;
                        if (HasOverlap(cells, occupiedSet)) continue;

                        var escapePath = GetEscapePath(x, y, dir, gridSize);
                        bool blocked = false;
                        foreach (var p in escapePath)
                        {
                            if (occupiedSet.Contains(CellKey(p)))
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (!blocked)
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

        private static PlacementResult FindBlockedPosition(string color, int length, int gridSize,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells)
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
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        var cells = CalculateCells(x, y, dir, length);

                        if (!AllCellsInBounds(cells, gridSize)) continue;
                        if (HasOverlap(cells, occupiedSet)) continue;

                        var escapePath = GetEscapePath(x, y, dir, gridSize);
                        bool blockedByBlocker = false;
                        foreach (var p in escapePath)
                        {
                            if (blockerSet.Contains(CellKey(p)))
                            {
                                blockedByBlocker = true;
                                break;
                            }
                        }

                        if (blockedByBlocker)
                        {
                            var otherOccupied = new HashSet<string>(occupiedSet);
                            foreach (var k in blockerSet)
                            {
                                otherOccupied.Remove(k);
                            }

                            bool blockedByOthers = false;
                            foreach (var p in escapePath)
                            {
                                if (otherOccupied.Contains(CellKey(p)))
                                {
                                    blockedByOthers = true;
                                    break;
                                }
                            }

                            if (!blockedByOthers)
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

        private static PlacementResult PlaceFallback(string color, int length, int gridSize, HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null, bool checkCanEscape = false)
        {
            var candidates = new List<PlacementResult>();
            var fallbackCandidates = new List<PlacementResult>();  // forbidden cells와 겹치는 후보
            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var dir in dirs)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        var cells = CalculateCells(x, y, dir, length);

                        if (!AllCellsInBounds(cells, gridSize)) continue;
                        if (HasOverlap(cells, occupiedSet)) continue;

                        // Filler용: 이 화살표가 실제로 탈출 가능한지 확인
                        if (checkCanEscape)
                        {
                            var escapePath = GetEscapePath(x, y, dir, gridSize);
                            bool canEscape = true;
                            foreach (var p in escapePath)
                            {
                                if (occupiedSet.Contains(CellKey(p)))
                                {
                                    canEscape = false;
                                    break;
                                }
                            }
                            if (!canEscape) continue;  // 탈출 불가능하면 스킵
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
                                fallbackCandidates.Add(result);  // 후순위
                                continue;
                            }
                        }

                        candidates.Add(result);  // 우선순위
                    }
                }
            }

            // 우선: forbidden cells와 안 겹치는 후보
            if (candidates.Count > 0)
                return RandomPick(candidates);

            // 차선: forbidden cells와 겹치는 후보 (밀도 목표 달성을 위해)
            if (fallbackCandidates.Count > 0)
                return RandomPick(fallbackCandidates);

            return null;
        }

        // ========== ReverseGrowth (Bending Arrow) ==========
        private static (int x, int y, string dir)? FindNextGrowthCell(int x, int y, string preferredDir,
            HashSet<string> occupiedSet, int gridSize, HashSet<string> selfPathSet = null)
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

                // 다른 화살표 셀 검사
                if (!IsInBounds(nx, ny, gridSize) || occupiedSet.Contains(CellKey(nx, ny)))
                    continue;

                // 자기 자신의 경로 검사 (자기 몸 통과 방지)
                if (selfPathSet != null && selfPathSet.Contains(CellKey(nx, ny)))
                    continue;

                return (nx, ny, dir);
            }
            return null;
        }

        private static (List<Vector2Int> path, string headDir)? GrowArrowReverse(int headX, int headY, string headDir,
            int targetLength, HashSet<string> occupiedSet, int gridSize)
        {
            var path = new List<Vector2Int> { new Vector2Int(headX, headY) };

            // 자기 자신의 경로를 추적 (자기 몸 통과 방지용)
            var selfPathSet = new HashSet<string> { CellKey(headX, headY) };

            string currentDir = OPPOSITE[headDir];
            int currentX = headX;
            int currentY = headY;

            for (int i = 1; i < targetLength; i++)
            {
                var next = FindNextGrowthCell(currentX, currentY, currentDir, occupiedSet, gridSize, selfPathSet);

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

        private static List<Vector2Int> GetEdgePositions(string dir, int gridSize)
        {
            var positions = new List<Vector2Int>();

            switch (dir)
            {
                case "U":
                    for (int x = 0; x < gridSize; x++) positions.Add(new Vector2Int(x, 0));
                    break;
                case "D":
                    for (int x = 0; x < gridSize; x++) positions.Add(new Vector2Int(x, gridSize - 1));
                    break;
                case "L":
                    for (int y = 0; y < gridSize; y++) positions.Add(new Vector2Int(0, y));
                    break;
                case "R":
                    for (int y = 0; y < gridSize; y++) positions.Add(new Vector2Int(gridSize - 1, y));
                    break;
            }

            Shuffle(positions);
            return positions;
        }

        private static PlacementResult PlaceFirstArrowBending(string color, int length, int gridSize, HashSet<string> occupiedSet)
        {
            var candidates = new List<PlacementResult>();
            var dirs = new List<string>(DIRECTIONS);
            Shuffle(dirs);

            foreach (var headDir in dirs)
            {
                var edgePositions = GetEdgePositions(headDir, gridSize);

                foreach (var pos in edgePositions)
                {
                    if (occupiedSet.Contains(CellKey(pos))) continue;

                    var result = GrowArrowReverse(pos.x, pos.y, headDir, length, occupiedSet, gridSize);

                    if (result.HasValue && result.Value.path.Count >= Mathf.Min(length, 2))
                    {
                        var escapePath = GetEscapePath(pos.x, pos.y, headDir, gridSize);
                        bool blocked = false;
                        foreach (var p in escapePath)
                        {
                            if (occupiedSet.Contains(CellKey(p)))
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (!blocked)
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

        private static PlacementResult FindBlockedPositionBending(string color, int length, int gridSize,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells)
        {
            var candidates = new List<PlacementResult>();
            var blockerSet = new HashSet<string>();
            foreach (var c in blockerCells)
            {
                blockerSet.Add(CellKey(c));
            }

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (occupiedSet.Contains(CellKey(x, y))) continue;

                    var dirs = new List<string>(DIRECTIONS);
                    Shuffle(dirs);

                    foreach (var headDir in dirs)
                    {
                        var result = GrowArrowReverse(x, y, headDir, length, occupiedSet, gridSize);

                        if (!result.HasValue || result.Value.path.Count < Mathf.Min(length, 2)) continue;

                        var escapePath = GetEscapePath(x, y, headDir, gridSize);
                        bool blockedByBlocker = false;
                        foreach (var p in escapePath)
                        {
                            if (blockerSet.Contains(CellKey(p)))
                            {
                                blockedByBlocker = true;
                                break;
                            }
                        }

                        if (blockedByBlocker)
                        {
                            var otherOccupied = new HashSet<string>(occupiedSet);
                            foreach (var k in blockerSet)
                            {
                                otherOccupied.Remove(k);
                            }

                            bool blockedByOthers = false;
                            foreach (var p in escapePath)
                            {
                                if (otherOccupied.Contains(CellKey(p)))
                                {
                                    blockedByOthers = true;
                                    break;
                                }
                            }

                            if (!blockedByOthers)
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

        private static PlacementResult PlaceFallbackBending(string color, int length, int gridSize, HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null, bool checkCanEscape = false)
        {
            var candidates = new List<PlacementResult>();
            var fallbackCandidates = new List<PlacementResult>();  // forbidden cells와 겹치는 후보

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (occupiedSet.Contains(CellKey(x, y))) continue;

                    var dirs = new List<string>(DIRECTIONS);
                    Shuffle(dirs);

                    foreach (var headDir in dirs)
                    {
                        var result = GrowArrowReverse(x, y, headDir, length, occupiedSet, gridSize);

                        if (result.HasValue && result.Value.path.Count >= Mathf.Min(length, 2))
                        {
                            // Filler용: 이 화살표가 실제로 탈출 가능한지 확인
                            if (checkCanEscape)
                            {
                                var escapePath = GetEscapePath(x, y, headDir, gridSize);
                                bool canEscape = true;
                                foreach (var p in escapePath)
                                {
                                    if (occupiedSet.Contains(CellKey(p)))
                                    {
                                        canEscape = false;
                                        break;
                                    }
                                }
                                if (!canEscape) continue;  // 탈출 불가능하면 스킵
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
                                    fallbackCandidates.Add(placementResult);  // 후순위
                                    continue;
                                }
                            }

                            candidates.Add(placementResult);  // 우선순위
                        }
                    }
                }
            }

            // 우선: forbidden cells와 안 겹치는 후보
            if (candidates.Count > 0)
                return RandomPick(candidates);

            // 차선: forbidden cells와 겹치는 후보 (밀도 목표 달성을 위해)
            if (fallbackCandidates.Count > 0)
                return RandomPick(fallbackCandidates);

            return null;
        }

        // ========== Filler Placement ==========
        private class BlockData
        {
            public int x, y;
            public string color;
            public string dir;
            public int length;
            public List<Vector2Int> cells;
            public List<Vector2Int> path;
            public bool isFiller;
            public bool isBending;
            public int originalIndex;
        }

        private static List<BlockData> PlaceFillersForDensity(List<BlockData> blocks, HashSet<string> occupiedSet, GeneratorConfig cfg, List<List<string>> lanes)
        {
            var fillers = new List<BlockData>();
            int totalCells = cfg.gridSize * cfg.gridSize;
            int targetOccupied = Mathf.FloorToInt(totalCells * cfg.targetDensity);

            int currentOccupied = occupiedSet.Count;
            int attempts = 0;
            int maxAttempts = 100;

            bool useBending = cfg.bendingEnabled && cfg.bendingChance > 0;

            // Main 화살표들의 탈출 경로 계산 (Filler가 이 셀들을 피하도록)
            var escapePaths = new HashSet<string>();
            foreach (var block in blocks)
            {
                if (block.cells == null || block.cells.Count == 0) continue;
                var head = block.cells[0];  // path[0] = HEAD
                var path = GetEscapePath(head.x, head.y, block.dir, cfg.gridSize);
                foreach (var cell in path)
                {
                    escapePaths.Add(CellKey(cell));
                }
            }
            Debug.Log($"  Filler: Main arrows' escape paths contain {escapePaths.Count} cells");

            Debug.Log($"  Filler: Current density {(currentOccupied / (float)totalCells * 100):F1}%, target {cfg.targetDensity * 100:F1}%");

            // 예상 Filler 개수 계산
            int remainingCells = targetOccupied - currentOccupied;
            int avgFillerLength = (cfg.fillerMinLength + cfg.fillerMaxLength) / 2;
            int estimatedFillerCount = Mathf.CeilToInt(remainingCells / (float)Mathf.Max(1, avgFillerLength));

            // Queue에 Filler용 풍선 미리 추가
            var fillerColors = new List<string>();
            for (int i = 0; i < estimatedFillerCount; i++)
            {
                string color = RandomPick(COLORS);
                fillerColors.Add(color);

                // 가장 적은 풍선을 가진 Lane의 앞에 삽입 (Main 화살표 팝 후 활성화됨)
                int minLaneIdx = 0;
                int minCount = int.MaxValue;
                for (int laneIdx = 0; laneIdx < lanes.Count; laneIdx++)
                {
                    if (lanes[laneIdx].Count < minCount)
                    {
                        minCount = lanes[laneIdx].Count;
                        minLaneIdx = laneIdx;
                    }
                }
                lanes[minLaneIdx].Insert(0, color);  // 앞에 삽입
            }

            int fillerColorIdx = 0;

            while (currentOccupied < targetOccupied && attempts < maxAttempts)
            {
                attempts++;

                // Filler 색상: 미리 추가된 풍선 색상 사용
                string color;
                if (fillerColorIdx < fillerColors.Count)
                {
                    color = fillerColors[fillerColorIdx];
                }
                else
                {
                    // 예상보다 많은 Filler가 필요한 경우, 추가 풍선도 추가
                    color = RandomPick(COLORS);
                    fillerColors.Add(color);

                    int minLaneIdx = 0;
                    int minCount = int.MaxValue;
                    for (int laneIdx = 0; laneIdx < lanes.Count; laneIdx++)
                    {
                        if (lanes[laneIdx].Count < minCount)
                        {
                            minCount = lanes[laneIdx].Count;
                            minLaneIdx = laneIdx;
                        }
                    }
                    lanes[minLaneIdx].Insert(0, color);  // 앞에 삽입
                }

                int length = RandomInt(cfg.fillerMinLength, cfg.fillerMaxLength);

                // checkCanEscape = true: Filler가 실제로 탈출 가능한 위치에만 배치
                PlacementResult placement = useBending
                    ? PlaceFallbackBending(color, length, cfg.gridSize, occupiedSet, escapePaths, checkCanEscape: true)
                    : PlaceFallback(color, length, cfg.gridSize, occupiedSet, escapePaths, checkCanEscape: true);

                if (placement != null)
                {
                    fillerColorIdx++;  // 색상 사용 완료

                    bool isBending = placement.path != null;
                    string fillerDir = isBending ? placement.headDir : placement.dir;
                    var fillerBlock = new BlockData
                    {
                        x = placement.x,
                        y = placement.y,
                        color = color,
                        dir = fillerDir,
                        length = placement.cells.Count,
                        cells = placement.cells,
                        isFiller = true,
                        path = isBending ? placement.path : null,
                        isBending = isBending
                    };

                    fillers.Add(fillerBlock);

                    foreach (var c in placement.cells)
                    {
                        occupiedSet.Add(CellKey(c));
                    }

                    // 이 Filler의 탈출 경로도 escapePaths에 추가
                    // 다음 Filler가 이 Filler의 탈출 경로를 막지 않도록
                    var fillerEscapePath = GetEscapePath(placement.x, placement.y, fillerDir, cfg.gridSize);
                    foreach (var cell in fillerEscapePath)
                    {
                        escapePaths.Add(CellKey(cell));
                    }

                    currentOccupied = occupiedSet.Count;
                }
            }

            // 사용하지 않은 풍선 제거 (예상보다 적게 배치된 경우)
            // Filler 풍선은 앞(index 0)에 추가되었으므로 앞에서 제거
            int unusedCount = fillerColors.Count - fillerColorIdx;
            if (unusedCount > 0)
            {
                Debug.Log($"  Filler: Removing {unusedCount} unused balloon(s) from queue");

                // 각 Lane의 앞에서 풍선 제거 (Insert(0)으로 추가했으므로)
                int toRemove = unusedCount;
                for (int laneIdx = lanes.Count - 1; laneIdx >= 0 && toRemove > 0; laneIdx--)
                {
                    while (lanes[laneIdx].Count > 0 && toRemove > 0)
                    {
                        lanes[laneIdx].RemoveAt(0);  // 앞에서 제거
                        toRemove--;
                    }
                }
            }

            Debug.Log($"  Filler: Added {fillers.Count} fillers, final density {(currentOccupied / (float)totalCells * 100):F1}%");

            return fillers;
        }

        // ========== Validation ==========

        /// <summary>
        /// 마주보는 화살표 검사 (Head끼리 인접하고 서로를 향하는 경우)
        /// </summary>
        private static (bool valid, string reason) CheckFacingArrows(List<BlockData> blocks)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    var a = blocks[i];
                    var b = blocks[j];

                    // 각 화살표의 Head 위치 (Generator 내부에서는 cells[0]이 Head)
                    if (a.cells == null || a.cells.Count == 0 || b.cells == null || b.cells.Count == 0)
                        continue;

                    var headA = a.cells[0];
                    var headB = b.cells[0];

                    // Head가 인접한지 확인
                    int dx = headB.x - headA.x;
                    int dy = headB.y - headA.y;

                    // 인접한 경우 (상하좌우로 1칸 차이)
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1)
                    {
                        // A가 B를 향하고, B가 A를 향하는지 확인
                        bool aPointsToB = IsDirectionTowards(a.dir, dx, dy);
                        bool bPointsToA = IsDirectionTowards(b.dir, -dx, -dy);

                        if (aPointsToB && bPointsToA)
                        {
                            return (false, $"facing arrows at ({headA.x},{headA.y}) and ({headB.x},{headB.y})");
                        }
                    }
                }
            }
            return (true, null);
        }

        /// <summary>
        /// 방향이 특정 델타를 향하는지 확인
        /// </summary>
        private static bool IsDirectionTowards(string dir, int dx, int dy)
        {
            return dir switch
            {
                "U" => dy < 0,  // 위쪽 = y 감소
                "D" => dy > 0,  // 아래쪽 = y 증가
                "L" => dx < 0,  // 왼쪽 = x 감소
                "R" => dx > 0,  // 오른쪽 = x 증가
                _ => false
            };
        }

        /// <summary>
        /// Game 좌표계 방향을 Generator 좌표계로 역변환
        /// </summary>
        private static string UnflipYDirection(string dir)
        {
            // FlipYDirection과 동일 (Y 방향 swap은 대칭적)
            return FlipYDirection(dir);
        }

        /// <summary>
        /// LevelData 검증 (외부에서 호출 가능)
        /// </summary>
        public static ValidationResult ValidateLevel(LevelData levelData)
        {
            if (levelData == null)
                return new ValidationResult { valid = false, reason = "null level" };

            // LevelData를 BlockData 리스트로 변환
            var blocks = new List<BlockData>();
            if (levelData.arrows != null)
            {
                foreach (var arrow in levelData.arrows)
                {
                    // ArrowData.GetCells()는 cells[0]=TAIL, cells[last]=HEAD 순서
                    // 검증 함수는 cells[0]=HEAD를 기대하므로 역순으로 변환
                    var cells = arrow.GetCells();
                    cells.Reverse();

                    var block = new BlockData
                    {
                        x = arrow.x,
                        y = arrow.y,
                        color = arrow.color,
                        dir = UnflipYDirection(arrow.direction),  // Game → Generator 좌표계 역변환
                        length = arrow.length,
                        cells = cells,
                        isFiller = arrow.isFiller
                    };
                    blocks.Add(block);
                }
            }

            // Lanes 변환
            var lanes = new List<List<string>>();
            if (levelData.lanes != null)
            {
                foreach (var lane in levelData.lanes)
                {
                    lanes.Add(new List<string>(lane.balloons ?? new List<string>()));
                }
            }

            return ValidateGeneratedLevel(blocks, lanes, levelData.gridSize);
        }

        private static ValidationResult ValidateGeneratedLevel(List<BlockData> blocks, List<List<string>> lanes, int gridSize)
        {
            try
            {
                if (blocks == null || blocks.Count == 0)
                {
                    return new ValidationResult { valid = false, reason = "no blocks" };
                }

                var remaining = new List<BlockData>();
                var colorMap = new Dictionary<int, string>(); // originalIndex -> color
                for (int idx = 0; idx < blocks.Count; idx++)
                {
                    var b = blocks[idx];
                    if (b.cells == null || b.cells.Count == 0)
                    {
                        return new ValidationResult { valid = false, reason = "invalid block cells" };
                    }
                    remaining.Add(new BlockData
                    {
                        x = b.x,
                        y = b.y,
                        color = b.color,
                        dir = b.dir,
                        length = b.length,
                        cells = new List<Vector2Int>(b.cells),
                        originalIndex = idx,
                        isFiller = b.isFiller  // Filler 여부 복사
                    });
                    colorMap[idx] = b.color;
                }

                // 마주보는 화살표 검사
                var facingCheck = CheckFacingArrows(remaining);
                if (!facingCheck.valid)
                {
                    return new ValidationResult { valid = false, reason = facingCheck.reason };
                }

                var queuesCopy = new List<List<string>>();
                foreach (var lane in lanes)
                {
                    queuesCopy.Add(new List<string>(lane));
                }

                var escapeSequence = new List<int>();

                int iterations = 0;
                int maxIterations = 100;

                while (remaining.Count > 0 && iterations < maxIterations)
                {
                    iterations++;

                    var occupied = new HashSet<string>();
                    foreach (var b in remaining)
                    {
                        foreach (var c in b.cells)
                        {
                            occupied.Add(CellKey(c));
                        }
                    }

                    bool escaped = false;

                    // Two-pass approach: Main arrows first, then Fillers
                    // Pass 0: Main arrows only (Fillers can block Main, so Main escapes first)
                    // Pass 1: Filler arrows only (with balloon activation check)
                    for (int pass = 0; pass < 2 && !escaped; pass++)
                    {
                        for (int i = 0; i < remaining.Count; i++)
                        {
                            var b = remaining[i];

                            // Pass 0: Skip Fillers (Main only)
                            // Pass 1: Skip non-Fillers (Filler only)
                            if (pass == 0 && b.isFiller) continue;
                            if (pass == 1 && !b.isFiller) continue;

                            // Filler: 풍선이 활성화(lane[end])되어야만 탈출 가능
                            if (b.isFiller)
                            {
                                bool balloonActive = false;
                                foreach (var lane in queuesCopy)
                                {
                                    if (lane.Count > 0 && lane[lane.Count - 1] == b.color)
                                    {
                                        balloonActive = true;
                                        break;
                                    }
                                }
                                if (!balloonActive) continue;
                            }

                            var d = DIR_VECTORS[b.dir];
                            var head = b.cells[0];

                            int cx = head.x + d.x;
                            int cy = head.y + d.y;
                            bool blocked = false;

                            while (IsInBounds(cx, cy, gridSize))
                            {
                                if (occupied.Contains(CellKey(cx, cy)))
                                {
                                    blocked = true;
                                    break;
                                }
                                cx += d.x;
                                cy += d.y;
                            }

                            if (!blocked)
                            {
                                foreach (var lane in queuesCopy)
                                {
                                    if (lane.Count > 0 && lane[lane.Count - 1] == b.color)
                                    {
                                        lane.RemoveAt(lane.Count - 1);
                                        break;
                                    }
                                }

                                escapeSequence.Add(b.originalIndex);
                                remaining.RemoveAt(i);
                                escaped = true;
                                break;
                            }
                        }
                    }

                    if (!escaped)
                    {
                        return new ValidationResult { valid = false, reason = "deadlock" };
                    }
                }

                if (remaining.Count > 0)
                {
                    return new ValidationResult { valid = false, reason = "timeout" };
                }

                bool queueEmpty = queuesCopy.TrueForAll(l => l.Count == 0);

                // 탈출 순서의 색상 목록 생성
                var escapeColors = new List<string>();
                foreach (var idx in escapeSequence)
                {
                    if (colorMap.TryGetValue(idx, out var color))
                    {
                        escapeColors.Add(color);
                    }
                }

                return new ValidationResult
                {
                    valid = true,
                    queueCleared = queueEmpty,
                    escapeSequence = escapeSequence,
                    escapeColors = escapeColors
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"validateLevel error: {e.Message}");
                return new ValidationResult { valid = false, reason = "exception" };
            }
        }

        // ========== Main Generation Function ==========
        public static LevelData GenerateLevel(GeneratorConfig config = null)
        {
            config ??= new GeneratorConfig();

            Debug.Log($"=== Generator v8 (ReverseGrowth) ===");
            Debug.Log($"Bending: {(config.bendingEnabled ? "enabled" : "disabled")} (chance: {config.bendingChance})");
            Debug.Log($"Target density: {config.targetDensity * 100:F1}%");

            int maxAttempts = 50;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Debug.Log($"Attempt {attempt + 1}/{maxAttempts}");

                // Step 1: Queue 생성
                var lanes = GenerateQueue(config);
                Debug.Log($"Lanes: {string.Join(", ", lanes.ConvertAll(l => "[" + string.Join(",", l) + "]"))}");

                // Step 2: 색상 순서 (탈출 순서)
                var colors = GetColorSequence(lanes, config.missArrowCount);
                Debug.Log($"Colors (escape order): {string.Join(",", colors)}");

                // Step 3: 순차적 의존성 배치
                var blocks = new List<BlockData>();
                var occupiedSet = new HashSet<string>();
                bool success = true;

                for (int i = 0; i < colors.Count; i++)
                {
                    string color = colors[i];
                    int length = RandomInt(config.minBlockLength, config.maxBlockLength);

                    PlacementResult placement = null;
                    bool useBending = config.bendingEnabled && Random.value < config.bendingChance;

                    if (i == 0)
                    {
                        placement = useBending
                            ? PlaceFirstArrowBending(color, length, config.gridSize, occupiedSet)
                            : PlaceFirstArrow(color, length, config.gridSize, occupiedSet);
                    }
                    else
                    {
                        bool useBranching = config.branchingMode && Random.value < config.branchingChance;

                        if (useBranching)
                        {
                            placement = useBending
                                ? PlaceFirstArrowBending(color, length, config.gridSize, occupiedSet)
                                : PlaceFirstArrow(color, length, config.gridSize, occupiedSet);
                        }
                        else
                        {
                            var prevBlock = blocks[i - 1];
                            placement = useBending
                                ? FindBlockedPositionBending(color, length, config.gridSize, occupiedSet, prevBlock.cells)
                                : FindBlockedPosition(color, length, config.gridSize, occupiedSet, prevBlock.cells);
                        }

                        if (placement == null)
                        {
                            placement = useBending
                                ? PlaceFallbackBending(color, length, config.gridSize, occupiedSet)
                                : PlaceFallback(color, length, config.gridSize, occupiedSet);
                        }
                    }

                    if (placement == null)
                    {
                        Debug.Log($"  Arrow {i}: Failed to place");
                        success = false;
                        break;
                    }

                    bool isBending = placement.path != null;
                    var block = new BlockData
                    {
                        x = placement.x,
                        y = placement.y,
                        color = color,
                        dir = isBending ? placement.headDir : placement.dir,
                        length = placement.cells.Count,
                        cells = placement.cells,
                        path = isBending ? placement.path : null,
                        isBending = isBending
                    };
                    blocks.Add(block);

                    foreach (var c in placement.cells)
                    {
                        occupiedSet.Add(CellKey(c));
                    }

                    Debug.Log($"  Arrow {i}: {color} at ({placement.x},{placement.y}) dir={block.dir} len={block.length}{(isBending ? " [bending]" : "")}");
                }

                if (!success) continue;

                // Step 4: 필러 추가
                var allBlocks = new List<BlockData>(blocks);
                int mainBlockCount = blocks.Count;

                if (config.fillerEnabled)
                {
                    float currentDensity = occupiedSet.Count / (float)(config.gridSize * config.gridSize);
                    Debug.Log($"  Current density before filler: {currentDensity * 100:F1}%");

                    if (currentDensity < config.targetDensity)
                    {
                        var fillers = PlaceFillersForDensity(allBlocks, occupiedSet, config, lanes);
                        allBlocks.AddRange(fillers);
                    }
                }

                // Step 5: 검증
                var validation = ValidateGeneratedLevel(allBlocks, lanes, config.gridSize);
                Debug.Log($"Validation: valid={validation.valid}, reason={validation.reason}");

                if (validation.valid)
                {
                    var escapeSequence = validation.escapeSequence ?? new List<int>();

                    var orderMap = new Dictionary<int, int>();
                    for (int seqIdx = 0; seqIdx < escapeSequence.Count; seqIdx++)
                    {
                        orderMap[escapeSequence[seqIdx]] = seqIdx + 1;
                    }

                    // LevelData로 변환
                    var levelData = new LevelData
                    {
                        name = $"Gen_{DateTime.Now:HHmmss}",
                        gridSize = config.gridSize,
                        lanes = new List<LaneData>(),
                        arrows = new List<ArrowData>(),
                        stats = new LevelStats()
                    };

                    // Lanes 변환 (LIFO → FIFO 순서로 역순 변환)
                    // Generator 내부: lane[end]가 활성 풍선, Game: lane[0]이 활성 풍선
                    foreach (var lane in lanes)
                    {
                        var reversed = new List<string>(lane);
                        reversed.Reverse();
                        levelData.lanes.Add(new LaneData { balloons = reversed });
                    }

                    // Arrows 변환
                    for (int idx = 0; idx < allBlocks.Count; idx++)
                    {
                        var b = allBlocks[idx];
                        var arrowData = new ArrowData
                        {
                            x = b.x,
                            y = b.y,
                            color = b.color,
                            direction = FlipYDirection(b.dir),  // Generator → Game 좌표계 변환
                            length = b.length,
                            order = orderMap.ContainsKey(idx) ? orderMap[idx] : 0,
                            isFiller = b.isFiller
                        };

                        // 꺾이는 화살표는 path 포함
                        if (b.path != null && b.path.Count > 0)
                        {
                            arrowData.path = new List<Vector2IntSerializable>();

                            // path 역순으로 저장 (GrowArrowReverse는 HEAD-first, GetCells는 TAIL-first 기대)
                            for (int pi = b.path.Count - 1; pi >= 0; pi--)
                            {
                                var p = b.path[pi];
                                arrowData.path.Add(new Vector2IntSerializable { x = p.x, y = p.y });
                            }

                            // Head 방향 검증: path[last-1] → path[last] 방향 계산
                            // path 좌표는 Generator 좌표계 (y=0이 위쪽, y 증가가 아래쪽)
                            if (arrowData.path.Count >= 2)
                            {
                                var secondLast = arrowData.path[arrowData.path.Count - 2];
                                var head = arrowData.path[arrowData.path.Count - 1];

                                int dx = head.x - secondLast.x;
                                int dy = head.y - secondLast.y;

                                // Generator 좌표계에서 방향 계산 후 Game 좌표계로 변환
                                // Generator: U=(0,-1), D=(0,1), L=(-1,0), R=(1,0)
                                string genDir = (dx, dy) switch
                                {
                                    (0, -1) => "U",  // Y 감소 = Up (Generator 좌표계)
                                    (0, 1) => "D",   // Y 증가 = Down (Generator 좌표계)
                                    (-1, 0) => "L",  // X 감소 = Left
                                    (1, 0) => "R",   // X 증가 = Right
                                    _ => b.dir
                                };

                                // Generator → Game 좌표계 변환
                                arrowData.direction = FlipYDirection(genDir);
                            }
                        }

                        levelData.arrows.Add(arrowData);
                    }

                    // Stats
                    float finalDensity = occupiedSet.Count / (float)(config.gridSize * config.gridSize);
                    levelData.stats = new LevelStats
                    {
                        density = finalDensity,
                        mainArrows = mainBlockCount,
                        fillers = allBlocks.Count - mainBlockCount,
                        totalArrows = allBlocks.Count
                    };

                    Debug.Log($"=== Generation Successful! ===");
                    Debug.Log($"Final density: {finalDensity * 100:F1}%");
                    Debug.Log($"Main arrows: {mainBlockCount}, Fillers: {allBlocks.Count - mainBlockCount}");

                    return levelData;
                }
            }

            Debug.LogError("Failed to generate valid level after max attempts");
            return null;
        }
    }
}