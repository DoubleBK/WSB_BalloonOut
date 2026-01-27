using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// 레벨 검증 시스템
    /// 풀이 가능성 검사, 마주보기(Facing) 검사, 데드락 감지
    /// </summary>
    public static class LevelValidator
    {
        // ========== Constants ==========
        private static readonly Dictionary<string, Vector2Int> DIR_VECTORS = new()
        {
            { "U", new Vector2Int(0, -1) },
            { "D", new Vector2Int(0, 1) },
            { "L", new Vector2Int(-1, 0) },
            { "R", new Vector2Int(1, 0) }
        };

        // ========== Data Structures ==========
        /// <summary>
        /// 레벨 검증 결과
        /// </summary>
        public class ValidationResult
        {
            public bool valid;
            public string reason;
            public bool queueCleared;
            public List<int> escapeSequence;
            public List<string> escapeColors;
        }

        /// <summary>
        /// 검증용 블록 데이터
        /// </summary>
        public class BlockData
        {
            public int x, y;
            public string color;
            public string dir;
            public int length;
            public List<Vector2Int> cells;
            public List<Vector2Int> path;
            public bool isFiller;
            public bool isBending;
            public bool isDecoy;
            public int originalIndex;
        }

        // ========== Utility Functions ==========
        private static bool IsInBounds(int x, int y, int gridSize)
        {
            return x >= 0 && x < gridSize && y >= 0 && y < gridSize;
        }

        private static string CellKey(int x, int y) => $"{x},{y}";
        private static string CellKey(Vector2Int v) => $"{v.x},{v.y}";

        // ========== Public API ==========
        /// <summary>
        /// LevelData 검증 (외부에서 호출 가능)
        /// </summary>
        public static ValidationResult ValidateLevel(LevelData levelData)
        {
            if (levelData == null)
                return new ValidationResult { valid = false, reason = "null level" };

            int gridSize = levelData.gridSize;

            // LevelData를 BlockData 리스트로 변환
            var blocks = new List<BlockData>();
            if (levelData.arrows != null)
            {
                Debug.Log($"[ValidateLevel] Converting {levelData.arrows.Count} arrows from LevelData");

                for (int arrowIdx = 0; arrowIdx < levelData.arrows.Count; arrowIdx++)
                {
                    var arrow = levelData.arrows[arrowIdx];

                    // ArrowData.GetCells()는 cells[0]=TAIL, cells[last]=HEAD 순서
                    // 검증 함수는 cells[0]=HEAD를 기대하므로 역순으로 변환
                    var cells = arrow.GetCells();

                    cells.Reverse();

                    // Game → Generator 좌표계 변환: Y 좌표 플립
                    for (int i = 0; i < cells.Count; i++)
                    {
                        cells[i] = new Vector2Int(cells[i].x, gridSize - 1 - cells[i].y);
                    }

                    var block = new BlockData
                    {
                        x = arrow.x,
                        y = gridSize - 1 - arrow.y,
                        color = arrow.color,
                        dir = arrow.direction,
                        length = arrow.length,
                        cells = cells,
                        isFiller = arrow.isFiller
                    };
                    blocks.Add(block);

                    var head = cells.Count > 0 ? cells[0] : default;
                    Debug.Log($"[ValidateLevel] Arrow {arrowIdx}: Game({arrow.x},{arrow.y}) dir={arrow.direction} → Gen head=({head.x},{head.y}) dir={block.dir} isFiller={arrow.isFiller}");
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

            return ValidateBlocks(blocks, lanes, levelData.gridSize);
        }

        /// <summary>
        /// BlockData 리스트 검증 (LevelGenerator에서 호출)
        /// </summary>
        public static ValidationResult ValidateBlocks(List<BlockData> blocks, List<List<string>> lanes, int gridSize)
        {
            try
            {
                if (blocks == null || blocks.Count == 0)
                {
                    return new ValidationResult { valid = false, reason = "no blocks" };
                }

                var remaining = new List<BlockData>();
                var colorMap = new Dictionary<int, string>();
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
                        isFiller = b.isFiller,
                        isDecoy = b.isDecoy
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

                    // 탈출 가능한 화살표 목록 수집
                    var canEscape = new List<(int index, BlockData block, bool canPop)>();

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var b = remaining[i];

                        // Filler: 풍선이 활성화되어야만 탈출 가능
                        if (b.isFiller)
                        {
                            bool balloonActive = false;
                            foreach (var lane in queuesCopy)
                            {
                                if (lane.Count > 0 && lane[0] == b.color)
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
                            bool canPop = false;
                            foreach (var lane in queuesCopy)
                            {
                                if (lane.Count > 0 && lane[0] == b.color)
                                {
                                    canPop = true;
                                    break;
                                }
                            }
                            canEscape.Add((i, b, canPop));
                        }
                    }

                    // 스마트 선택: 다른 화살표를 언블록하는 화살표 우선
                    var blockedArrows = remaining.Where((b, idx) => !canEscape.Any(e => e.index == idx)).ToList();

                    var escapePriority = new List<(int index, BlockData block, bool canPop, int unblockCount)>();
                    foreach (var escape in canEscape)
                    {
                        int unblockCount = 0;
                        foreach (var blockedArrow in blockedArrows)
                        {
                            var d = DIR_VECTORS[blockedArrow.dir];
                            var head = blockedArrow.cells[0];
                            int cx = head.x + d.x;
                            int cy = head.y + d.y;

                            while (IsInBounds(cx, cy, gridSize))
                            {
                                string cellKey = CellKey(cx, cy);
                                if (escape.block.cells.Any(c => CellKey(c) == cellKey))
                                {
                                    unblockCount++;
                                    break;
                                }
                                cx += d.x;
                                cy += d.y;
                            }
                        }
                        escapePriority.Add((escape.index, escape.block, escape.canPop, unblockCount));
                    }

                    var selectedEscape = escapePriority
                        .OrderByDescending(e => e.canPop && e.unblockCount > 0 ? 2 : 0)
                        .ThenByDescending(e => e.unblockCount)
                        .ThenByDescending(e => e.canPop ? 1 : 0)
                        .Select(e => (e.index, e.block, e.canPop))
                        .FirstOrDefault();

                    if (selectedEscape.block == null && canEscape.Count > 0)
                    {
                        selectedEscape = canEscape[0];
                    }

                    if (selectedEscape.block != null)
                    {
                        var b = selectedEscape.block;
                        int i = selectedEscape.index;

                        foreach (var lane in queuesCopy)
                        {
                            if (lane.Count > 0 && lane[0] == b.color)
                            {
                                lane.RemoveAt(0);
                                break;
                            }
                        }

                        escapeSequence.Add(b.originalIndex);
                        remaining.RemoveAt(i);
                        escaped = true;
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
                Debug.LogError($"ValidateBlocks error: {e.Message}");
                return new ValidationResult { valid = false, reason = "exception" };
            }
        }

        // ========== Facing Check ==========
        /// <summary>
        /// 마주보는 화살표 검사 (Head끼리 인접하고 서로를 향하는 경우)
        /// </summary>
        public static (bool valid, string reason) CheckFacingArrows(List<BlockData> blocks)
        {
            Debug.Log($"[FacingCheck] Checking {blocks.Count} arrows for facing pairs");

            for (int i = 0; i < blocks.Count; i++)
            {
                var a = blocks[i];
                if (a.cells == null || a.cells.Count == 0) continue;
                Debug.Log($"[FacingCheck] Arrow {i}: cells[0]={a.cells[0]}, dir={a.dir}, cellsCount={a.cells.Count}, isBending={a.isBending}");

                for (int j = i + 1; j < blocks.Count; j++)
                {
                    var b = blocks[j];

                    if (b.cells == null || b.cells.Count == 0)
                        continue;

                    var headA = a.cells[0];
                    var headB = b.cells[0];

                    int dx = headB.x - headA.x;
                    int dy = headB.y - headA.y;

                    if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1)
                    {
                        bool aPointsToB = IsDirectionTowards(a.dir, dx, dy);
                        bool bPointsToA = IsDirectionTowards(b.dir, -dx, -dy);

                        Debug.Log($"[FacingCheck] Adjacent {i}-{j}: A({headA}, dir={a.dir}) → B({headB}, dir={b.dir})");
                        Debug.Log($"[FacingCheck]   dx={dx}, dy={dy}, aPointsToB={aPointsToB}, bPointsToA={bPointsToA}");

                        if (aPointsToB && bPointsToA)
                        {
                            Debug.LogError($"[FacingCheck] FACING DETECTED: arrows {i} and {j}");
                            return (false, $"facing arrows at ({headA.x},{headA.y}) and ({headB.x},{headB.y})");
                        }
                    }
                }
            }
            Debug.Log($"[FacingCheck] No facing arrows found");
            return (true, null);
        }

        /// <summary>
        /// 새 화살표를 배치할 때 기존 화살표들과 Facing 관계가 형성되는지 검사
        /// </summary>
        public static bool WouldCauseFacing(int headX, int headY, string headDir, List<BlockData> existingBlocks)
        {
            if (existingBlocks == null || existingBlocks.Count == 0) return false;

            foreach (var existing in existingBlocks)
            {
                if (existing.cells == null || existing.cells.Count == 0) continue;

                var existingHead = existing.cells[0];
                int dx = existingHead.x - headX;
                int dy = existingHead.y - headY;

                if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;

                bool newPointsToExisting = IsDirectionTowards(headDir, dx, dy);
                bool existingPointsToNew = IsDirectionTowards(existing.dir, -dx, -dy);

                if (newPointsToExisting && existingPointsToNew)
                {
                    Debug.LogWarning($"[WouldCauseFacing] PREVENTED: new({headX},{headY}) dir={headDir} ↔ existing({existingHead.x},{existingHead.y}) dir={existing.dir}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 방향이 특정 델타를 향하는지 확인
        /// </summary>
        public static bool IsDirectionTowards(string dir, int dx, int dy)
        {
            return dir switch
            {
                "U" => dy < 0,
                "D" => dy > 0,
                "L" => dx < 0,
                "R" => dx > 0,
                _ => false
            };
        }

        // ========== Color Assignment ==========
        /// <summary>
        /// 화살표 탈출 순서에 맞춰 색상을 동적으로 할당
        /// </summary>
        public static bool AssignColorsInEscapeOrder(List<BlockData> blocks, List<List<string>> lanes, int gridSize, string[] availableColors)
        {
            if (blocks == null || blocks.Count == 0 || lanes == null)
                return false;

            var lanesCopy = new List<List<string>>();
            foreach (var lane in lanes)
            {
                lanesCopy.Add(new List<string>(lane));
            }

            var originalQueueColors = new List<string>();
            foreach (var lane in lanes)
            {
                foreach (var c in lane)
                {
                    originalQueueColors.Add(c);
                }
            }

            var remaining = new List<BlockData>();
            for (int idx = 0; idx < blocks.Count; idx++)
            {
                var b = blocks[idx];
                remaining.Add(new BlockData
                {
                    x = b.x,
                    y = b.y,
                    color = "",
                    dir = b.dir,
                    length = b.length,
                    cells = new List<Vector2Int>(b.cells),
                    path = b.path,
                    isBending = b.isBending,
                    isFiller = b.isFiller,
                    originalIndex = idx
                });
            }

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

                for (int i = 0; i < remaining.Count; i++)
                {
                    var b = remaining[i];

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
                        var poppableColors = new List<string>();
                        for (int laneIdx = 0; laneIdx < lanesCopy.Count; laneIdx++)
                        {
                            var lane = lanesCopy[laneIdx];
                            if (lane.Count > 0)
                            {
                                poppableColors.Add(lane[lane.Count - 1]);
                            }
                        }

                        string assignedColor;
                        bool isDecoy = false;

                        if (poppableColors.Count == 0)
                        {
                            if (originalQueueColors.Count > 0)
                            {
                                assignedColor = originalQueueColors[UnityEngine.Random.Range(0, originalQueueColors.Count)];
                            }
                            else
                            {
                                assignedColor = availableColors[UnityEngine.Random.Range(0, availableColors.Length)];
                            }
                            isDecoy = true;
                            Debug.Log($"[AssignColors] Block {b.originalIndex} is DECOY with color {assignedColor}");
                        }
                        else
                        {
                            assignedColor = poppableColors[UnityEngine.Random.Range(0, poppableColors.Count)];

                            for (int laneIdx = 0; laneIdx < lanesCopy.Count; laneIdx++)
                            {
                                var lane = lanesCopy[laneIdx];
                                if (lane.Count > 0 && lane[lane.Count - 1] == assignedColor)
                                {
                                    lane.RemoveAt(lane.Count - 1);
                                    break;
                                }
                            }
                        }

                        blocks[b.originalIndex].color = assignedColor;
                        blocks[b.originalIndex].isDecoy = isDecoy;

                        remaining.RemoveAt(i);
                        escaped = true;
                        break;
                    }
                }

                if (!escaped)
                {
                    Debug.LogWarning($"[AssignColors] Deadlock: {remaining.Count} blocks cannot escape");
                    return false;
                }
            }

            if (remaining.Count > 0)
            {
                Debug.LogWarning($"[AssignColors] Timeout: {remaining.Count} blocks remaining");
                return false;
            }

            return true;
        }

        // ========== Diagnostic Utilities ==========
        /// <summary>
        /// 두 BlockData 리스트가 Facing 측면에서 동일한지 검증
        /// </summary>
        public static void VerifyBlocksConsistency(List<BlockData> originalBlocks, List<BlockData> convertedBlocks, string context)
        {
            Debug.Log($"[VerifyBlocks] {context}: Comparing {originalBlocks.Count} original vs {convertedBlocks.Count} converted blocks");

            int minCount = Mathf.Min(originalBlocks.Count, convertedBlocks.Count);
            for (int i = 0; i < minCount; i++)
            {
                var orig = originalBlocks[i];
                var conv = convertedBlocks[i];

                var origHead = orig.cells != null && orig.cells.Count > 0 ? orig.cells[0] : default;
                var convHead = conv.cells != null && conv.cells.Count > 0 ? conv.cells[0] : default;

                if (origHead.x != convHead.x || origHead.y != convHead.y || orig.dir != conv.dir)
                {
                    Debug.LogWarning($"[VerifyBlocks] MISMATCH at index {i}:");
                    Debug.LogWarning($"  Original: head=({origHead.x},{origHead.y}) dir={orig.dir}");
                    Debug.LogWarning($"  Converted: head=({convHead.x},{convHead.y}) dir={conv.dir}");
                }
            }

            var origFacing = CheckFacingArrows(originalBlocks);
            var convFacing = CheckFacingArrows(convertedBlocks);

            if (origFacing.valid != convFacing.valid)
            {
                Debug.LogError($"[VerifyBlocks] FACING INCONSISTENCY!");
                Debug.LogError($"  Original blocks facing check: valid={origFacing.valid}, reason={origFacing.reason}");
                Debug.LogError($"  Converted blocks facing check: valid={convFacing.valid}, reason={convFacing.reason}");
            }
        }
    }
}
