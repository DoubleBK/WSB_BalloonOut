using System;
using System.Collections.Generic;
using System.Linq;
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
        // DIRECTIONS, DIR_VECTORS, TURN_PRIORITY, OPPOSITE는 ArrowPlacer로 이동

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
            public int gridSize = 16;
            public int laneCount = 3;
            public int balloonsPerLane = 2;
            public int missArrowCount = 1;
            public int minBlockLength = 3;
            public int maxBlockLength = 8;
            public float targetDensity = 0.9f;
            public bool fillerEnabled = false;
            public int fillerMinLength = 2;
            public int fillerMaxLength = 5;
            public bool bendingEnabled = true;
            public float bendingChance = 1.0f;
            public bool branchingMode = false;
            public float branchingChance = 0.4f;
            public int decoyArrowCount = 0;  // 함정 화살표 개수 (풍선 없이 탈출하는 화살표)
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

        // ValidationResult는 LevelValidator.ValidationResult 사용

        // ========== Auto Parameter Calculation ==========
        /// <summary>
        /// 밀도 기반 파라미터 자동 계산
        /// Filler 없이 목표 밀도(90%+)를 달성하도록 설계
        /// 지원 Grid Size: 6~30
        /// </summary>
        public static GeneratorConfig CalculateAutoParams(int gridSize, float targetDensity = 0.9f, bool bendingEnabled = true)
        {
            var config = new GeneratorConfig
            {
                gridSize = gridSize,
                targetDensity = targetDensity,
                bendingEnabled = bendingEnabled,
                fillerEnabled = false  // Filler 기본 비활성화 (Facing/Deadlock 이슈 방지)
            };

            int totalCells = gridSize * gridSize;
            int targetOccupied = Mathf.FloorToInt(totalCells * targetDensity);

            // 화살표 길이 설정 (Grid Size와 Bending 여부에 따라)
            // 큰 그리드에서는 더 긴 화살표 허용
            if (bendingEnabled)
            {
                config.minBlockLength = Mathf.Max(3, gridSize / 3);
                config.maxBlockLength = Mathf.Min(gridSize + 6, 35);  // 최대 35까지 허용
            }
            else
            {
                config.minBlockLength = Mathf.Max(2, gridSize / 4);
                config.maxBlockLength = Mathf.Min(gridSize, 15);
            }

            float avgLength = (config.minBlockLength + config.maxBlockLength) / 2f;

            // 필요한 화살표 개수 계산 (목표 밀도 달성)
            int requiredArrows = Mathf.CeilToInt(targetOccupied / avgLength);

            // Lane 개수: Grid Size에 비례 (3~6개)
            config.laneCount = Mathf.Clamp(gridSize / 4, 3, 6);

            // Decoy 개수: Grid Size에 따라 점진적 증가
            // 6 이하: 0, 7-12: 1, 13-20: 2, 21+: 3
            config.decoyArrowCount = gridSize <= 6 ? 0 :
                                     gridSize <= 12 ? 1 :
                                     gridSize <= 20 ? 2 : 3;

            // Balloon/Miss 분배: 필요 화살표에서 Decoy 제외 후 분배
            int mainArrowsNeeded = requiredArrows - config.decoyArrowCount;

            // balloonsPerLane 계산 (최소 2개, 최대 8개)
            config.balloonsPerLane = Mathf.Clamp(mainArrowsNeeded / config.laneCount - 1, 2, 8);

            // 기본 화살표 수 계산
            int baseArrows = config.laneCount * config.balloonsPerLane;

            // missArrowCount: 남은 필요 화살표 수 (최소 1개, 최대 10개)
            config.missArrowCount = Mathf.Clamp(mainArrowsNeeded - baseArrows, 1, 10);

            // Filler 설정 (비활성화되어도 파라미터는 유지)
            config.fillerMinLength = bendingEnabled ? 2 : 1;
            config.fillerMaxLength = Mathf.Max(2, bendingEnabled ?
                Mathf.Min(8, config.maxBlockLength - 2) :
                Mathf.Min(4, config.maxBlockLength - 1));

            // 예상 밀도 로그
            int totalArrows = config.laneCount * config.balloonsPerLane + config.missArrowCount + config.decoyArrowCount;
            float expectedDensity = (totalArrows * avgLength) / totalCells;
            Debug.Log($"[AutoCalc] Grid={gridSize}, Target={targetDensity:P0}, Expected={expectedDensity:P0}");
            Debug.Log($"[AutoCalc] Arrows: {totalArrows} (lanes={config.laneCount} x balloons={config.balloonsPerLane} + miss={config.missArrowCount} + decoy={config.decoyArrowCount})");
            Debug.Log($"[AutoCalc] Length: {config.minBlockLength}-{config.maxBlockLength} (avg={avgLength:F1})");

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
        // ArrowPlacer로 이동 (CalculateCells, HasOverlap, AllCellsInBounds)

        // ========== Arrow Placement (ArrowPlacer 위임) ==========
        private static PlacementResult PlaceFirstArrow(string color, int length, int gridSize, HashSet<string> occupiedSet, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFirstArrow(length, gridSize, occupiedSet, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult FindBlockedPosition(string color, int length, int gridSize,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.FindBlockedPosition(length, gridSize, occupiedSet, blockerCells, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult PlaceFallback(string color, int length, int gridSize, HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null, bool checkCanEscape = false, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFallback(length, gridSize, occupiedSet, forbiddenCells, checkCanEscape, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        // ArrowPlacer.PlacementResult → LevelGenerator.PlacementResult 변환
        private static PlacementResult ConvertPlacementResult(ArrowPlacer.PlacementResult result)
        {
            if (result == null) return null;
            return new PlacementResult
            {
                x = result.x,
                y = result.y,
                dir = result.dir,
                headDir = result.headDir,
                cells = result.cells,
                path = result.path
            };
        }

        // ========== ReverseGrowth (ArrowPlacer 위임) ==========
        private static PlacementResult PlaceFirstArrowBending(string color, int length, int gridSize, HashSet<string> occupiedSet, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFirstArrowBending(length, gridSize, occupiedSet, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult FindBlockedPositionBending(string color, int length, int gridSize,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.FindBlockedPositionBending(length, gridSize, occupiedSet, blockerCells, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult PlaceFallbackBending(string color, int length, int gridSize, HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null, bool checkCanEscape = false, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFallbackBending(length, gridSize, occupiedSet, forbiddenCells, checkCanEscape, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        // ========== Legacy Cell Calculation (Filler용 유지) ==========
        private static List<Vector2Int> GetEscapePath(int x, int y, string dir, int gridSize)
        {
            return ArrowPlacer.GetEscapePath(x, y, dir, gridSize);
        }

        // ========== Filler Placement ==========
        // BlockData는 LevelValidator.BlockData 사용
        private class BlockData : LevelValidator.BlockData { }

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

            // Facing 검사용: Main 화살표 + 이미 배치된 Filler들
            var allBlocksForFacing = new List<BlockData>(blocks);

            while (currentOccupied < targetOccupied && attempts < maxAttempts)
            {
                attempts++;

                // Filler 색상: 랜덤 선택 (배치 성공 후에만 Queue에 추가)
                string color = RandomPick(COLORS);
                int length = RandomInt(cfg.fillerMinLength, cfg.fillerMaxLength);

                // checkCanEscape = true: Filler가 실제로 탈출 가능한 위치에만 배치
                // existingBlocks: Main 화살표 + 이미 배치된 Filler들과 facing 방지
                PlacementResult placement = useBending
                    ? PlaceFallbackBending(color, length, cfg.gridSize, occupiedSet, escapePaths, checkCanEscape: true, existingBlocks: allBlocksForFacing)
                    : PlaceFallback(color, length, cfg.gridSize, occupiedSet, escapePaths, checkCanEscape: true, existingBlocks: allBlocksForFacing);

                if (placement != null)
                {
                    // 배치 성공! 이제 Queue에 풍선 추가
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
                    allBlocksForFacing.Add(fillerBlock);  // 다음 Filler의 facing 검사에 포함

                    // 디버그: Filler의 head와 dir 확인
                    var fillerHead = placement.cells[0];
                    Debug.Log($"  [DEBUG] Filler {fillers.Count - 1} added: head=({fillerHead.x},{fillerHead.y}) dir={fillerDir} allBlocksCount={allBlocksForFacing.Count}");

                    // 즉시 검증: 방금 추가한 Filler가 facing을 유발하는지 확인
                    var immediateCheck = CheckFacingArrows(allBlocksForFacing);
                    if (!immediateCheck.valid)
                    {
                        Debug.LogError($"  [CRITICAL] Filler {fillers.Count - 1} CAUSED FACING despite check! Reason: {immediateCheck.reason}");
                        Debug.LogError($"  [CRITICAL] This should NOT happen - investigate WouldCauseFacing logic");
                    }

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

            Debug.Log($"  Filler: Added {fillers.Count} fillers, final density {(currentOccupied / (float)totalCells * 100):F1}%");

            return fillers;
        }

        // ========== Validation ==========
        // 검증 로직은 LevelValidator로 이동 (Facing 검사, 데드락 감지, 색상 할당 등)

        /// <summary>
        /// LevelData 검증 (LevelValidator 위임)
        /// </summary>
        public static LevelValidator.ValidationResult ValidateLevel(LevelData levelData)
        {
            return LevelValidator.ValidateLevel(levelData);
        }

        /// <summary>
        /// BlockData 리스트 검증 (내부 사용)
        /// </summary>
        private static LevelValidator.ValidationResult ValidateGeneratedLevel(List<BlockData> blocks, List<List<string>> lanes, int gridSize)
        {
            // BlockData를 LevelValidator.BlockData로 변환 (상속 관계이므로 캐스팅 가능)
            var validatorBlocks = blocks.Cast<LevelValidator.BlockData>().ToList();
            return LevelValidator.ValidateBlocks(validatorBlocks, lanes, gridSize);
        }

        /// <summary>
        /// 화살표 탈출 순서에 맞춰 색상을 동적으로 할당 (LevelValidator 위임)
        /// </summary>
        private static bool AssignColorsInEscapeOrder(List<BlockData> blocks, List<List<string>> lanes, int gridSize)
        {
            var validatorBlocks = blocks.Cast<LevelValidator.BlockData>().ToList();
            bool result = LevelValidator.AssignColorsInEscapeOrder(validatorBlocks, lanes, gridSize, COLORS);

            // LevelValidator가 수정한 color/isDecoy 값을 원본 blocks에 복사
            for (int i = 0; i < blocks.Count; i++)
            {
                blocks[i].color = validatorBlocks[i].color;
                blocks[i].isDecoy = validatorBlocks[i].isDecoy;
            }

            return result;
        }

        /// <summary>
        /// 새 화살표 배치 시 Facing 검사 (LevelValidator 위임)
        /// </summary>
        private static bool WouldCauseFacing(int headX, int headY, string headDir, List<BlockData> existingBlocks)
        {
            if (existingBlocks == null || existingBlocks.Count == 0) return false;
            var validatorBlocks = existingBlocks.Cast<LevelValidator.BlockData>().ToList();
            return LevelValidator.WouldCauseFacing(headX, headY, headDir, validatorBlocks);
        }

        /// <summary>
        /// 마주보는 화살표 검사 (LevelValidator 위임)
        /// </summary>
        private static (bool valid, string reason) CheckFacingArrows(List<BlockData> blocks)
        {
            var validatorBlocks = blocks.Cast<LevelValidator.BlockData>().ToList();
            return LevelValidator.CheckFacingArrows(validatorBlocks);
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

                // Step 2: Miss 풍선 추가 (기존 GetColorSequence의 Step 1과 동일)
                for (int mi = 0; mi < config.missArrowCount; mi++)
                {
                    string missColor = RandomPick(COLORS);
                    int laneIdx = RandomInt(0, lanes.Count - 1);
                    int insertPos = RandomInt(0, lanes[laneIdx].Count);
                    lanes[laneIdx].Insert(insertPos, missColor);
                }

                Debug.Log($"Lanes (with miss): {string.Join(", ", lanes.ConvertAll(l => "[" + string.Join(",", l) + "]"))}");

                // Step 3: 화살표 개수 = 총 풍선 개수 + Decoy 화살표
                int mainArrowCount = 0;
                foreach (var lane in lanes)
                {
                    mainArrowCount += lane.Count;
                }
                int decoyCount = config.decoyArrowCount;
                int arrowCount = mainArrowCount + decoyCount;
                Debug.Log($"Arrow count: {arrowCount} (Main: {mainArrowCount}, Decoy: {decoyCount})");

                // Step 4: 화살표 배치 (색상은 나중에 할당)
                var blocks = new List<BlockData>();
                var occupiedSet = new HashSet<string>();
                bool success = true;

                for (int i = 0; i < arrowCount; i++)
                {
                    string placeholderColor = "X"; // 플레이스홀더 색상
                    int length = RandomInt(config.minBlockLength, config.maxBlockLength);

                    PlacementResult placement = null;
                    bool useBending = config.bendingEnabled && Random.value < config.bendingChance;

                    if (i == 0)
                    {
                        placement = useBending
                            ? PlaceFirstArrowBending(placeholderColor, length, config.gridSize, occupiedSet, blocks)
                            : PlaceFirstArrow(placeholderColor, length, config.gridSize, occupiedSet, blocks);
                    }
                    else
                    {
                        bool useBranching = config.branchingMode && Random.value < config.branchingChance;

                        if (useBranching)
                        {
                            placement = useBending
                                ? PlaceFirstArrowBending(placeholderColor, length, config.gridSize, occupiedSet, blocks)
                                : PlaceFirstArrow(placeholderColor, length, config.gridSize, occupiedSet, blocks);
                        }
                        else
                        {
                            var prevBlock = blocks[i - 1];
                            placement = useBending
                                ? FindBlockedPositionBending(placeholderColor, length, config.gridSize, occupiedSet, prevBlock.cells, blocks)
                                : FindBlockedPosition(placeholderColor, length, config.gridSize, occupiedSet, prevBlock.cells, blocks);
                        }

                        if (placement == null)
                        {
                            placement = useBending
                                ? PlaceFallbackBending(placeholderColor, length, config.gridSize, occupiedSet, existingBlocks: blocks)
                                : PlaceFallback(placeholderColor, length, config.gridSize, occupiedSet, existingBlocks: blocks);
                        }
                    }

                    if (placement == null)
                    {
                        Debug.Log($"  Arrow {i}: Failed to place");
                        success = false;
                        break;
                    }

                    bool isBending = placement.path != null;
                    string blockDir = isBending ? placement.headDir : placement.dir;
                    var block = new BlockData
                    {
                        x = placement.x,
                        y = placement.y,
                        color = placeholderColor, // 색상은 나중에 할당
                        dir = blockDir,
                        length = placement.cells.Count,
                        cells = placement.cells,
                        path = isBending ? placement.path : null,
                        isBending = isBending
                    };
                    blocks.Add(block);

                    // 디버그: cells[0]이 HEAD인지 확인
                    var head = placement.cells[0];
                    Debug.Log($"  [DEBUG] Arrow {i} added: head=({head.x},{head.y}) dir={blockDir} cells[0]=({placement.cells[0].x},{placement.cells[0].y})");

                    foreach (var c in placement.cells)
                    {
                        occupiedSet.Add(CellKey(c));
                    }

                    Debug.Log($"  Arrow {i}: at ({placement.x},{placement.y}) dir={block.dir} len={block.length}{(isBending ? " [bending]" : "")}");
                }

                if (!success) continue;

                // Step 5: 탈출 순서에 맞춰 색상 할당
                if (!AssignColorsInEscapeOrder(blocks, lanes, config.gridSize))
                {
                    Debug.Log("  Color assignment failed");
                    continue;
                }
                Debug.Log($"  Colors assigned: {string.Join(",", blocks.ConvertAll(b => b.color))}");

                // Step 6: 필러 추가
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

                // Step 7: 검증
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
                            y = config.gridSize - 1 - b.y,  // Generator → Game 좌표계 변환 (Y 플립)
                            color = b.color,
                            direction = b.dir,  // 방향은 유지 (U/D/L/R은 시각적 의미가 동일)
                            length = b.length,
                            order = orderMap.ContainsKey(idx) ? orderMap[idx] : 0,
                            isFiller = b.isFiller,
                            isDecoy = b.isDecoy
                        };

                        // 꺾이는 화살표는 path 포함
                        if (b.path != null && b.path.Count > 0)
                        {
                            arrowData.path = new List<Vector2IntSerializable>();

                            // path 역순으로 저장 (GrowArrowReverse는 HEAD-first, GetCells는 TAIL-first 기대)
                            // Generator → Game 좌표계 변환 (Y 플립)
                            for (int pi = b.path.Count - 1; pi >= 0; pi--)
                            {
                                var p = b.path[pi];
                                arrowData.path.Add(new Vector2IntSerializable {
                                    x = p.x,
                                    y = config.gridSize - 1 - p.y  // Y 좌표 플립
                                });
                            }

                            // Head 방향 검증: path[last-1] → path[last] 방향 계산
                            // path 좌표는 이미 Game 좌표계로 변환됨 (y=0이 하단, y 증가가 위쪽)
                            if (arrowData.path.Count >= 2)
                            {
                                var secondLast = arrowData.path[arrowData.path.Count - 2];
                                var head = arrowData.path[arrowData.path.Count - 1];

                                int dx = head.x - secondLast.x;
                                int dy = head.y - secondLast.y;

                                // Game 좌표계에서 방향 계산
                                // Game: U=(0,1), D=(0,-1), L=(-1,0), R=(1,0)
                                string gameDir = (dx, dy) switch
                                {
                                    (0, 1) => "U",   // Y 증가 = Up (Game 좌표계)
                                    (0, -1) => "D",  // Y 감소 = Down (Game 좌표계)
                                    (-1, 0) => "L",  // X 감소 = Left
                                    (1, 0) => "R",   // X 증가 = Right
                                    _ => b.dir  // 기본값은 원래 방향 유지
                                };

                                // 이미 Game 좌표계이므로 추가 변환 불필요
                                arrowData.direction = gameDir;
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

                    // Round-trip 검증: LevelData를 다시 BlockData로 변환했을 때 일관성 확인
                    Debug.Log($"[RoundTrip] Verifying LevelData → BlockData conversion consistency...");
                    var roundTripValidation = ValidateLevel(levelData);
                    if (!roundTripValidation.valid)
                    {
                        Debug.LogError($"[RoundTrip] FAILED! Internal validation passed but ValidateLevel failed: {roundTripValidation.reason}");
                        Debug.LogError($"[RoundTrip] This indicates a coordinate/direction transformation issue!");
                        // 상세 진단을 위해 continue하지 않고 일단 반환 (디버그용)
                    }
                    else
                    {
                        Debug.Log($"[RoundTrip] SUCCESS - Both internal and external validation passed");
                    }

                    return levelData;
                }
            }

            Debug.LogError("Failed to generate valid level after max attempts");
            return null;
        }
    }
}