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
        /// <summary>
        /// 사용 가능한 전체 색상 (12색, Black 제외)
        /// R=Red, G=Green, B=Blue, Y=Yellow, P=Purple, O=Orange
        /// C=Cyan, K=Pink, W=Brown, L=Lime, N=Navy, M=Magenta
        /// </summary>
        private static readonly string[] ALL_COLORS = { "R", "G", "B", "Y", "P", "O", "C", "K", "W", "L", "N", "M" };

        // ========== Auto-Calc Constants ==========
        private const int MIN_BLOCK_LENGTH_BENDING = 3;
        private const int MIN_BLOCK_LENGTH_STRAIGHT = 2;
        private const int MAX_BLOCK_LENGTH_BENDING = 25;
        private const int MAX_BLOCK_LENGTH_STRAIGHT = 15;
        private const int MIN_LANE_COUNT = 2;
        private const int MAX_LANE_COUNT = 6;
        private const int LANE_COUNT_DIVISOR = 5;
        private const int MAX_DECOY_ARROWS = 5;
        private const int MAX_MISS_ARROWS = 20;
        private const int MAX_BALLOONS_PER_LANE = 15;
        private const int MIN_BALLOONS_PER_LANE = 2;
        private const int DECOY_PERCENTAGE = 10;
        private const int MATCH_ARROW_PERCENTAGE = 75;
        private const int FILLER_MAX_ATTEMPTS = 100;
        private const int MIN_COLOR_COUNT = 2;
        private const int DEFAULT_COLOR_COUNT = 6;

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
            public int gridSize = 16;      // 기존 호환용 (정사각형)
            public int gridWidth = 0;      // 직사각형용 (0이면 gridSize 사용)
            public int gridHeight = 0;     // 직사각형용 (0이면 gridSize 사용)
            public int laneCount = 3;

            /// <summary>실제 그리드 너비 반환</summary>
            public int GetGridWidth() => gridWidth > 0 ? gridWidth : gridSize;
            /// <summary>실제 그리드 높이 반환</summary>
            public int GetGridHeight() => gridHeight > 0 ? gridHeight : gridSize;
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
            public int colorCount = 6;       // 사용할 색상 수 (4~12, 기본 6)
            public List<string> availableColors = null;  // 사용할 색상 목록 (null이면 colorCount만큼 랜덤 선택)
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
        /// GridSize에 비례하여 모든 파라미터가 스케일링됨
        /// 목표: 맵을 가득 채우면서도 Solvable하고 QueueClear가 가능한 레벨 생성
        /// 지원 Grid Size: 4~30
        /// </summary>
        public static GeneratorConfig CalculateAutoParams(int gridSize, float targetDensity = 0.9f, bool bendingEnabled = true)
        {
            var config = new GeneratorConfig
            {
                gridSize = gridSize,
                targetDensity = targetDensity,
                bendingEnabled = bendingEnabled,
                fillerEnabled = false
            };

            int totalCells = gridSize * gridSize;
            int targetOccupied = Mathf.FloorToInt(totalCells * targetDensity);

            // ── 화살표 길이 설정 ──
            // 큰 그리드에서는 더 긴 화살표 허용하되, 너무 길면 배치 실패 증가
            if (bendingEnabled)
            {
                config.minBlockLength = Mathf.Max(MIN_BLOCK_LENGTH_BENDING, gridSize / 4);
                config.maxBlockLength = Mathf.Clamp(gridSize, 6, MAX_BLOCK_LENGTH_BENDING);
            }
            else
            {
                config.minBlockLength = Mathf.Max(MIN_BLOCK_LENGTH_STRAIGHT, gridSize / 5);
                config.maxBlockLength = Mathf.Clamp(gridSize - 1, 4, MAX_BLOCK_LENGTH_STRAIGHT);
            }

            float avgLength = (config.minBlockLength + config.maxBlockLength) / 2f;

            // ── 필요한 총 화살표 수 ──
            int requiredArrows = Mathf.CeilToInt(targetOccupied / avgLength);

            // ── Lane 수: gridSize에 비례 ──
            config.laneCount = Mathf.Clamp(Mathf.CeilToInt(gridSize / (float)LANE_COUNT_DIVISOR), MIN_LANE_COUNT, MAX_LANE_COUNT);

            // ── Decoy: 전체의 일정 비율 ──
            config.decoyArrowCount = Mathf.Clamp(requiredArrows / DECOY_PERCENTAGE, 0, MAX_DECOY_ARROWS);

            // ── Match/Miss 분배 ──
            int mainArrowsNeeded = requiredArrows - config.decoyArrowCount;

            // Match 화살표: main의 일정 비율 (최소 laneCount개)
            int matchArrows = Mathf.Max(mainArrowsNeeded * MATCH_ARROW_PERCENTAGE / 100, config.laneCount);
            config.balloonsPerLane = Mathf.Clamp(
                Mathf.CeilToInt((float)matchArrows / config.laneCount),
                MIN_BALLOONS_PER_LANE, MAX_BALLOONS_PER_LANE
            );

            int baseArrows = config.laneCount * config.balloonsPerLane;

            // Miss: 남은 화살표
            config.missArrowCount = Mathf.Clamp(mainArrowsNeeded - baseArrows, 0, MAX_MISS_ARROWS);

            // ── Filler 설정 (비활성화되어도 파라미터 유지) ──
            config.fillerMinLength = bendingEnabled ? 2 : 1;
            config.fillerMaxLength = Mathf.Max(2, bendingEnabled ?
                Mathf.Min(8, config.maxBlockLength - 2) :
                Mathf.Min(4, config.maxBlockLength - 1));

            // ── 로그 ──
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

        private static bool IsInBounds(int x, int y, int gridWidth, int gridHeight)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        // 호환성 오버로드 (정사각형)
        private static bool IsInBounds(int x, int y, int gridSize)
        {
            return x >= 0 && x < gridSize && y >= 0 && y < gridSize;
        }

        private static string CellKey(int x, int y) => $"{x},{y}";
        private static string CellKey(Vector2Int v) => $"{v.x},{v.y}";

        // ========== Color Selection ==========
        /// <summary>
        /// 레벨 생성에 사용할 색상 목록 반환
        /// 1) availableColors가 지정되어 있으면 해당 색상 사용
        /// 2) 그렇지 않으면 colorCount 개수만큼 ALL_COLORS에서 랜덤 선택
        /// </summary>
        private static string[] GetColorsForConfig(GeneratorConfig config)
        {
            // availableColors가 지정되어 있으면 해당 색상 사용
            if (config.availableColors != null && config.availableColors.Count > 0)
            {
                return config.availableColors.ToArray();
            }

            // colorCount 검증
            int count = Mathf.Clamp(config.colorCount, MIN_COLOR_COUNT, ALL_COLORS.Length);

            // 전체 색상에서 count개 랜덤 선택
            var shuffled = new List<string>(ALL_COLORS);
            Shuffle(shuffled);
            return shuffled.GetRange(0, count).ToArray();
        }

        // ========== Queue Generation ==========
        private static List<List<string>> GenerateQueue(GeneratorConfig config, string[] colors)
        {
            var lanes = new List<List<string>>();
            for (int i = 0; i < config.laneCount; i++)
            {
                var lane = new List<string>();
                for (int j = 0; j < config.balloonsPerLane; j++)
                {
                    lane.Add(RandomPick(colors));
                }
                lanes.Add(lane);
            }
            return lanes;
        }

        /// <summary>
        /// Miss 풍선을 lanes의 랜덤 위치에 삽입
        /// </summary>
        private static void InsertMissArrows(List<List<string>> lanes, int missCount, string[] colors)
        {
            for (int i = 0; i < missCount; i++)
            {
                string missColor = RandomPick(colors);
                int laneIdx = RandomInt(0, lanes.Count - 1);
                int insertPos = RandomInt(0, lanes[laneIdx].Count);
                lanes[laneIdx].Insert(insertPos, missColor);
            }
        }

        // ========== Cell Calculation ==========
        // ArrowPlacer로 이동 (CalculateCells, HasOverlap, AllCellsInBounds)

        // ========== Arrow Placement (ArrowPlacer 위임) ==========
        private static PlacementResult PlaceFirstArrow(string color, int length, int gridWidth, int gridHeight, HashSet<string> occupiedSet, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFirstArrow(length, gridWidth, gridHeight, occupiedSet, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult FindBlockedPosition(string color, int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.FindBlockedPosition(length, gridWidth, gridHeight, occupiedSet, blockerCells, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult PlaceFallback(string color, int length, int gridWidth, int gridHeight, HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null, bool checkCanEscape = false, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFallback(length, gridWidth, gridHeight, occupiedSet, forbiddenCells, checkCanEscape, validatorBlocks);
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
        private static PlacementResult PlaceFirstArrowBending(string color, int length, int gridWidth, int gridHeight, HashSet<string> occupiedSet, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFirstArrowBending(length, gridWidth, gridHeight, occupiedSet, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult FindBlockedPositionBending(string color, int length, int gridWidth, int gridHeight,
            HashSet<string> occupiedSet, List<Vector2Int> blockerCells, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.FindBlockedPositionBending(length, gridWidth, gridHeight, occupiedSet, blockerCells, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        private static PlacementResult PlaceFallbackBending(string color, int length, int gridWidth, int gridHeight, HashSet<string> occupiedSet, HashSet<string> forbiddenCells = null, bool checkCanEscape = false, List<BlockData> existingBlocks = null)
        {
            var validatorBlocks = existingBlocks?.Cast<LevelValidator.BlockData>().ToList();
            var result = ArrowPlacer.PlaceFallbackBending(length, gridWidth, gridHeight, occupiedSet, forbiddenCells, checkCanEscape, validatorBlocks);
            return ConvertPlacementResult(result);
        }

        // ========== Legacy Cell Calculation (Filler용 유지) ==========
        private static List<Vector2Int> GetEscapePath(int x, int y, string dir, int gridWidth, int gridHeight)
        {
            return ArrowPlacer.GetEscapePath(x, y, dir, gridWidth, gridHeight);
        }

        // ========== Filler Placement ==========
        // BlockData는 LevelValidator.BlockData 사용
        private class BlockData : LevelValidator.BlockData { }

        private static List<BlockData> PlaceFillersForDensity(List<BlockData> blocks, HashSet<string> occupiedSet, GeneratorConfig cfg, List<List<string>> lanes, string[] colors)
        {
            var fillers = new List<BlockData>();
            int gridWidth = cfg.GetGridWidth();
            int gridHeight = cfg.GetGridHeight();
            int totalCells = gridWidth * gridHeight;
            int targetOccupied = Mathf.FloorToInt(totalCells * cfg.targetDensity);

            int currentOccupied = occupiedSet.Count;
            int attempts = 0;

            bool useBending = cfg.bendingEnabled && cfg.bendingChance > 0;

            // Main 화살표들의 탈출 경로 계산 (Filler가 이 셀들을 피하도록)
            var escapePaths = new HashSet<string>();
            foreach (var block in blocks)
            {
                if (block.cells == null || block.cells.Count == 0) continue;
                var head = block.cells[0];  // path[0] = HEAD
                var path = GetEscapePath(head.x, head.y, block.dir, gridWidth, gridHeight);
                foreach (var cell in path)
                {
                    escapePaths.Add(CellKey(cell));
                }
            }
            Debug.Log($"  Filler: Main arrows' escape paths contain {escapePaths.Count} cells");

            Debug.Log($"  Filler: Current density {(currentOccupied / (float)totalCells * 100):F1}%, target {cfg.targetDensity * 100:F1}%");

            // Facing 검사용: Main 화살표 + 이미 배치된 Filler들
            var allBlocksForFacing = new List<BlockData>(blocks);

            while (currentOccupied < targetOccupied && attempts < FILLER_MAX_ATTEMPTS)
            {
                attempts++;

                // Filler 색상: 랜덤 선택 (배치 성공 후에만 Queue에 추가)
                string color = RandomPick(colors);
                int length = RandomInt(cfg.fillerMinLength, cfg.fillerMaxLength);

                // checkCanEscape = true: Filler가 실제로 탈출 가능한 위치에만 배치
                // existingBlocks: Main 화살표 + 이미 배치된 Filler들과 facing 방지
                PlacementResult placement = useBending
                    ? PlaceFallbackBending(color, length, gridWidth, gridHeight, occupiedSet, escapePaths, checkCanEscape: true, existingBlocks: allBlocksForFacing)
                    : PlaceFallback(color, length, gridWidth, gridHeight, occupiedSet, escapePaths, checkCanEscape: true, existingBlocks: allBlocksForFacing);

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

#if DEBUG_LEVEL_GENERATOR
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
#endif

                    foreach (var c in placement.cells)
                    {
                        occupiedSet.Add(CellKey(c));
                    }

                    // 이 Filler의 탈출 경로도 escapePaths에 추가
                    // 다음 Filler가 이 Filler의 탈출 경로를 막지 않도록
                    var fillerEscapePath = GetEscapePath(placement.x, placement.y, fillerDir, gridWidth, gridHeight);
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
        private static LevelValidator.ValidationResult ValidateGeneratedLevel(List<BlockData> blocks, List<List<string>> lanes, int gridWidth, int gridHeight)
        {
            // BlockData를 LevelValidator.BlockData로 변환 (상속 관계이므로 캐스팅 가능)
            var validatorBlocks = blocks.Cast<LevelValidator.BlockData>().ToList();
            return LevelValidator.ValidateBlocks(validatorBlocks, lanes, gridWidth, gridHeight);
        }

        /// <summary>
        /// 화살표 탈출 순서에 맞춰 색상을 동적으로 할당 (LevelValidator 위임)
        /// </summary>
        private static bool AssignColorsInEscapeOrder(List<BlockData> blocks, List<List<string>> lanes, int gridWidth, int gridHeight, string[] colors)
        {
            var validatorBlocks = blocks.Cast<LevelValidator.BlockData>().ToList();
            bool result = LevelValidator.AssignColorsInEscapeOrder(validatorBlocks, lanes, gridWidth, gridHeight, colors);

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
        /// <summary>
        /// 레벨 생성
        /// </summary>
        /// <param name="config">생성 설정</param>
        /// <param name="maxAttempts">최대 시도 횟수</param>
        /// <param name="onProgress">진행 상황 콜백 (currentAttempt, maxAttempts) -> 취소 시 true 반환</param>
        public static LevelData GenerateLevel(
            GeneratorConfig config = null,
            int maxAttempts = 50,
            System.Func<int, int, bool> onProgress = null)
        {
            config ??= new GeneratorConfig();

            // 이번 레벨에 사용할 색상 목록 결정
            string[] colors = GetColorsForConfig(config);
            Debug.Log($"=== Generator v8 (ReverseGrowth) ===");
            Debug.Log($"Colors: {string.Join(",", colors)} ({colors.Length} colors)");
            Debug.Log($"Bending: {(config.bendingEnabled ? "enabled" : "disabled")} (chance: {config.bendingChance})");
            Debug.Log($"Target density: {config.targetDensity * 100:F1}%");

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Progress callback 호출 - 취소 시 null 반환
                if (onProgress != null && onProgress(attempt + 1, maxAttempts))
                {
                    Debug.Log($"Generation cancelled at attempt {attempt + 1}");
                    return null;
                }

                Debug.Log($"Attempt {attempt + 1}/{maxAttempts}");

                // Step 1: Queue 생성
                var lanes = GenerateQueue(config, colors);

                // Step 2: Miss 풍선 추가
                InsertMissArrows(lanes, config.missArrowCount, colors);

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

                int gridWidth = config.GetGridWidth();
                int gridHeight = config.GetGridHeight();

                for (int i = 0; i < arrowCount; i++)
                {
                    string placeholderColor = "X"; // 플레이스홀더 색상
                    int length = RandomInt(config.minBlockLength, config.maxBlockLength);

                    PlacementResult placement = null;
                    bool useBending = config.bendingEnabled && Random.value < config.bendingChance;

                    if (i == 0)
                    {
                        placement = useBending
                            ? PlaceFirstArrowBending(placeholderColor, length, gridWidth, gridHeight, occupiedSet, blocks)
                            : PlaceFirstArrow(placeholderColor, length, gridWidth, gridHeight, occupiedSet, blocks);
                    }
                    else
                    {
                        bool useBranching = config.branchingMode && Random.value < config.branchingChance;

                        if (useBranching)
                        {
                            placement = useBending
                                ? PlaceFirstArrowBending(placeholderColor, length, gridWidth, gridHeight, occupiedSet, blocks)
                                : PlaceFirstArrow(placeholderColor, length, gridWidth, gridHeight, occupiedSet, blocks);
                        }
                        else
                        {
                            var prevBlock = blocks[i - 1];
                            placement = useBending
                                ? FindBlockedPositionBending(placeholderColor, length, gridWidth, gridHeight, occupiedSet, prevBlock.cells, blocks)
                                : FindBlockedPosition(placeholderColor, length, gridWidth, gridHeight, occupiedSet, prevBlock.cells, blocks);
                        }

                        if (placement == null)
                        {
                            placement = useBending
                                ? PlaceFallbackBending(placeholderColor, length, gridWidth, gridHeight, occupiedSet, existingBlocks: blocks)
                                : PlaceFallback(placeholderColor, length, gridWidth, gridHeight, occupiedSet, existingBlocks: blocks);
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

#if DEBUG_LEVEL_GENERATOR
                    // 디버그: cells[0]이 HEAD인지 확인
                    var head = placement.cells[0];
                    Debug.Log($"  [DEBUG] Arrow {i} added: head=({head.x},{head.y}) dir={blockDir} cells[0]=({placement.cells[0].x},{placement.cells[0].y})");
#endif

                    foreach (var c in placement.cells)
                    {
                        occupiedSet.Add(CellKey(c));
                    }

                    Debug.Log($"  Arrow {i}: at ({placement.x},{placement.y}) dir={block.dir} len={block.length}{(isBending ? " [bending]" : "")}");
                }

                if (!success) continue;

                // Step 5: 탈출 순서에 맞춰 색상 할당
                if (!AssignColorsInEscapeOrder(blocks, lanes, gridWidth, gridHeight, colors))
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
                    float currentDensity = occupiedSet.Count / (float)(gridWidth * gridHeight);
                    Debug.Log($"  Current density before filler: {currentDensity * 100:F1}%");

                    if (currentDensity < config.targetDensity)
                    {
                        var fillers = PlaceFillersForDensity(allBlocks, occupiedSet, config, lanes, colors);
                        allBlocks.AddRange(fillers);
                    }
                }

                // Step 7: 검증
                var validation = ValidateGeneratedLevel(allBlocks, lanes, gridWidth, gridHeight);
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
                        gridSize = 0,  // width/height 사용 표시
                        gridWidth = gridWidth,
                        gridHeight = gridHeight,
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
                            y = gridHeight - 1 - b.y,  // Generator → Game 좌표계 변환 (Y 플립)
                            color = b.color,
                            // 직선 화살표: Y축 반전에 따라 U↔D 플립
                            // Bending 화살표: path에서 방향 재계산되므로 원본 유지 (나중에 덮어씌워짐)
                            direction = (b.path != null && b.path.Count > 0) ? b.dir : FlipYDirection(b.dir),
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
                                    y = gridHeight - 1 - p.y  // Y 좌표 플립
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
                    float finalDensity = occupiedSet.Count / (float)(gridWidth * gridHeight);
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

#if DEBUG_LEVEL_GENERATOR
                    // Round-trip 검증: LevelData를 다시 BlockData로 변환했을 때 일관성 확인
                    Debug.Log($"[RoundTrip] Verifying LevelData → BlockData conversion consistency...");
                    var roundTripValidation = ValidateLevel(levelData);
                    if (!roundTripValidation.valid)
                    {
                        Debug.LogError($"[RoundTrip] FAILED! Internal validation passed but ValidateLevel failed: {roundTripValidation.reason}");
                        Debug.LogError($"[RoundTrip] This indicates a coordinate/direction transformation issue!");
                    }
                    else
                    {
                        Debug.Log($"[RoundTrip] SUCCESS - Both internal and external validation passed");
                    }
#endif

                    return levelData;
                }
            }

            Debug.LogError("Failed to generate valid level after max attempts");
            return null;
        }
    }
}