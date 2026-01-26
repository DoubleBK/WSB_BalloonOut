using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Grid;

namespace BalloonOut.Editor
{
    /// <summary>
    /// Level Editor Window - Unity Editor 내에서 레벨 편집
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ========== 에디터 상태 ==========
        private LevelData _currentLevel;
        private string _levelName = "NewLevel";
        private int _gridSize = 6;

        // 도구 설정
        private GameColor _selectedColor = GameColor.Red;
        private ArrowDirection _selectedDirection = ArrowDirection.Right;
        private int _selectedLength = 3;

        // 선택 상태
        private int _selectedArrowIndex = -1;

        // 스크롤
        private Vector2 _scrollPosition;
        private Vector2 _arrowListScroll;
        private Vector2 _laneListScroll;

        // 탭
        private int _currentTab = 0;
        private readonly string[] _tabNames = { "Arrows", "Balloons", "Settings", "Generate" };

        // Generator 설정
        private int _genGridSize = 8;
        private float _genTargetDensity = 0.5f;
        private bool _genBendingEnabled = true;
        private bool _genFillerEnabled = true;
        private int _genLaneCount = 2;
        private int _genBalloonsPerLane = 2;
        private int _genMissArrowCount = 1;
        private int _genMinLength = 3;
        private int _genMaxLength = 8;
        private bool _genAutoCalculate = true;
        private bool _genBranchingMode = false;
        private float _genBranchingChance = 0.4f;

        // Validation 캐시
        private LevelGenerator.ValidationResult _cachedValidation;
        private bool _validationDirty = true;

        // 레벨 목록
        private List<string> _levelList = new List<string>();
        private int _selectedLevelIndex = -1;

        // Scene View 편집
        private bool _isEditingInScene = false;

        // Preview 설정
        private const float LEFT_PANEL_WIDTH = 350f;
        private const float PREVIEW_CELL_SIZE = 28f;
        private Vector2 _previewScrollPosition;

        // ========== 메뉴 ==========
        [MenuItem("BalloonOut/Level Editor %#l")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(750, 550);
            window.Show();
        }

        // ========== 유니티 라이프사이클 ==========
        private void OnEnable()
        {
            RefreshLevelList();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            // 좌우 분할 레이아웃
            EditorGUILayout.BeginHorizontal();

            // ========== 왼쪽 패널 (도구) ==========
            EditorGUILayout.BeginVertical(GUILayout.Width(LEFT_PANEL_WIDTH));
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawLevelSelector();
            EditorGUILayout.Space(10);

            DrawToolbar();
            EditorGUILayout.Space(10);

            _currentTab = GUILayout.Toolbar(_currentTab, _tabNames);
            EditorGUILayout.Space(5);

            switch (_currentTab)
            {
                case 0:
                    DrawArrowTab();
                    break;
                case 1:
                    DrawBalloonTab();
                    break;
                case 2:
                    DrawSettingsTab();
                    break;
                case 3:
                    DrawGenerateTab();
                    break;
            }

            EditorGUILayout.Space(10);
            DrawSceneEditToggle();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ========== 오른쪽 패널 (Preview) ==========
            EditorGUILayout.BeginVertical("box");
            DrawPreviewPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // 변경 사항 감지 시 Repaint
            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        // ========== 헤더 ==========
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Level Editor", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (_currentLevel != null)
            {
                GUILayout.Label($"[{_currentLevel.name}]", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ========== 레벨 선택 ==========
        private void DrawLevelSelector()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // 새 레벨
            _levelName = EditorGUILayout.TextField("Name", _levelName);

            if (GUILayout.Button("New", GUILayout.Width(50)))
            {
                CreateNewLevel();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // 레벨 목록
            if (_levelList.Count > 0)
            {
                int newIndex = EditorGUILayout.Popup("Load", _selectedLevelIndex, _levelList.ToArray());
                if (newIndex != _selectedLevelIndex)
                {
                    _selectedLevelIndex = newIndex;
                    if (_selectedLevelIndex >= 0 && _selectedLevelIndex < _levelList.Count)
                    {
                        LoadLevel(_levelList[_selectedLevelIndex]);
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("No levels found");
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshLevelList();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ========== 툴바 ==========
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _currentLevel != null;

            if (GUILayout.Button("Save", GUILayout.Height(30)))
            {
                SaveLevel();
            }

            if (GUILayout.Button("Test Play", GUILayout.Height(30)))
            {
                TestPlayLevel();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        // ========== Arrow 탭 ==========
        private void DrawArrowTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Arrow Tool", EditorStyles.boldLabel);

            // 색상 선택
            _selectedColor = (GameColor)EditorGUILayout.EnumPopup("Color", _selectedColor);

            // 방향 선택
            _selectedDirection = (ArrowDirection)EditorGUILayout.EnumPopup("Direction", _selectedDirection);

            // 길이
            _selectedLength = EditorGUILayout.IntSlider("Length", _selectedLength, 1, 10);

            EditorGUILayout.Space(5);

            GUI.enabled = _currentLevel != null && _isEditingInScene;
            EditorGUILayout.HelpBox("Scene View에서 셀을 클릭하여 화살표 배치", MessageType.Info);
            GUI.enabled = true;

            EditorGUILayout.EndVertical();

            // 화살표 목록
            DrawArrowList();
        }

        private void DrawArrowList()
        {
            if (_currentLevel == null || _currentLevel.arrows == null)
                return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Arrows ({_currentLevel.arrows.Count})", EditorStyles.boldLabel);

            _arrowListScroll = EditorGUILayout.BeginScrollView(_arrowListScroll, GUILayout.MaxHeight(200));

            for (int i = 0; i < _currentLevel.arrows.Count; i++)
            {
                var arrow = _currentLevel.arrows[i];
                bool isSelected = i == _selectedArrowIndex;

                EditorGUILayout.BeginHorizontal(isSelected ? "selectionRect" : "box");

                // 선택
                if (GUILayout.Button(isSelected ? "●" : "○", GUILayout.Width(25)))
                {
                    _selectedArrowIndex = isSelected ? -1 : i;
                    SceneView.RepaintAll();
                }

                // 정보
                EditorGUILayout.LabelField($"{arrow.color} | {arrow.direction} | Len:{arrow.length}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"({arrow.x}, {arrow.y})", GUILayout.Width(60));

                // 삭제
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    _currentLevel.arrows.RemoveAt(i);
                    if (_selectedArrowIndex == i)
                        _selectedArrowIndex = -1;
                    else if (_selectedArrowIndex > i)
                        _selectedArrowIndex--;
                    _validationDirty = true; // 검증 갱신 필요
                    SceneView.RepaintAll();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ========== Balloon 탭 ==========
        private void DrawBalloonTab()
        {
            if (_currentLevel == null)
            {
                EditorGUILayout.HelpBox("먼저 레벨을 생성하거나 불러오세요.", MessageType.Warning);
                return;
            }

            if (_currentLevel.lanes == null)
            {
                _currentLevel.lanes = new List<LaneData>();
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Balloon Queue", EditorStyles.boldLabel);

            // Lane 추가
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Lane"))
            {
                _currentLevel.lanes.Add(new LaneData());
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Lane 목록
            _laneListScroll = EditorGUILayout.BeginScrollView(_laneListScroll, GUILayout.MaxHeight(250));

            for (int laneIdx = 0; laneIdx < _currentLevel.lanes.Count; laneIdx++)
            {
                DrawLaneEditor(laneIdx);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLaneEditor(int laneIdx)
        {
            var lane = _currentLevel.lanes[laneIdx];

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Lane {laneIdx + 1}", EditorStyles.boldLabel, GUILayout.Width(60));

            // 풍선 추가 버튼들
            if (GUILayout.Button("R", GUILayout.Width(25))) AddBalloon(lane, "R");
            if (GUILayout.Button("G", GUILayout.Width(25))) AddBalloon(lane, "G");
            if (GUILayout.Button("B", GUILayout.Width(25))) AddBalloon(lane, "B");
            if (GUILayout.Button("Y", GUILayout.Width(25))) AddBalloon(lane, "Y");
            if (GUILayout.Button("P", GUILayout.Width(25))) AddBalloon(lane, "P");
            if (GUILayout.Button("O", GUILayout.Width(25))) AddBalloon(lane, "O");

            GUILayout.FlexibleSpace();

            // Lane 삭제
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                _currentLevel.lanes.RemoveAt(laneIdx);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();

            // 풍선 목록
            if (lane.balloons != null && lane.balloons.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < lane.balloons.Count; i++)
                {
                    var color = lane.balloons[i];
                    GUI.backgroundColor = GetColorForBalloon(color);

                    if (GUILayout.Button(color, GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        // 클릭 시 삭제
                        lane.balloons.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void AddBalloon(LaneData lane, string color)
        {
            if (lane.balloons == null)
                lane.balloons = new List<string>();
            lane.balloons.Add(color);
        }

        private Color GetColorForBalloon(string code)
        {
            return code switch
            {
                "R" => new Color(1f, 0.3f, 0.3f),
                "G" => new Color(0.3f, 0.8f, 0.3f),
                "B" => new Color(0.3f, 0.5f, 1f),
                "Y" => new Color(1f, 0.9f, 0.3f),
                "P" => new Color(0.7f, 0.3f, 0.8f),
                "O" => new Color(1f, 0.6f, 0.2f),
                _ => Color.white
            };
        }

        // ========== Settings 탭 ==========
        private void DrawSettingsTab()
        {
            if (_currentLevel == null)
            {
                EditorGUILayout.HelpBox("먼저 레벨을 생성하거나 불러오세요.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

            _currentLevel.name = EditorGUILayout.TextField("Level Name", _currentLevel.name);

            int newGridSize = EditorGUILayout.IntSlider("Grid Size", _currentLevel.gridSize, 4, 10);
            if (newGridSize != _currentLevel.gridSize)
            {
                _currentLevel.gridSize = newGridSize;
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(10);

            // 통계
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Arrows: {_currentLevel.arrows?.Count ?? 0}");

            int totalBalloons = 0;
            if (_currentLevel.lanes != null)
            {
                foreach (var lane in _currentLevel.lanes)
                {
                    totalBalloons += lane.balloons?.Count ?? 0;
                }
            }
            EditorGUILayout.LabelField($"Balloons: {totalBalloons}");
            EditorGUILayout.LabelField($"Lanes: {_currentLevel.lanes?.Count ?? 0}");

            EditorGUILayout.EndVertical();
        }

        // ========== Generate 탭 ==========
        private void DrawGenerateTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ReverseGrowth 알고리즘으로 자동 레벨 생성", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 기본 설정
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);

            _genGridSize = EditorGUILayout.IntSlider("Grid Size", _genGridSize, 5, 12);
            _genTargetDensity = EditorGUILayout.Slider("Target Density", _genTargetDensity, 0.2f, 0.8f);
            EditorGUILayout.LabelField($"  → {(_genTargetDensity * 100):F0}% of grid will be filled", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            _genBendingEnabled = EditorGUILayout.Toggle("Bending Enabled", _genBendingEnabled);
            _genFillerEnabled = EditorGUILayout.Toggle("Filler Enabled", _genFillerEnabled);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);

            _genBranchingMode = EditorGUILayout.Toggle("Branching Mode", _genBranchingMode);
            GUI.enabled = _genBranchingMode;
            _genBranchingChance = EditorGUILayout.Slider("Branching Chance", _genBranchingChance, 0f, 1f);
            GUI.enabled = true;

            if (_genBranchingMode)
            {
                EditorGUILayout.HelpBox("Branching: 화살표가 이전 화살표에 막히지 않고 독립적으로 배치될 확률", MessageType.None);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Auto Calculate 토글
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            _genAutoCalculate = EditorGUILayout.Toggle("Auto Calculate", _genAutoCalculate);
            if (GUILayout.Button("Calculate Now", GUILayout.Width(100)))
            {
                ApplyAutoCalculate();
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = !_genAutoCalculate;

            // 세부 설정
            EditorGUILayout.LabelField("Arrow Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Lanes", GUILayout.Width(100));
            _genLaneCount = EditorGUILayout.IntSlider(_genLaneCount, 1, 6);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Balloons/Lane", GUILayout.Width(100));
            _genBalloonsPerLane = EditorGUILayout.IntSlider(_genBalloonsPerLane, 1, 8);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Miss Arrows", GUILayout.Width(100));
            _genMissArrowCount = EditorGUILayout.IntSlider(_genMissArrowCount, 0, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min Length", GUILayout.Width(100));
            _genMinLength = EditorGUILayout.IntSlider(_genMinLength, 1, 8);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max Length", GUILayout.Width(100));
            _genMaxLength = EditorGUILayout.IntSlider(_genMaxLength, 2, 20);
            EditorGUILayout.EndHorizontal();

            // minLength <= maxLength 보장
            if (_genMinLength > _genMaxLength)
            {
                _genMinLength = _genMaxLength;
            }

            GUI.enabled = true;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Generate 버튼
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("🎲 Generate Level", GUILayout.Height(40)))
            {
                GenerateNewLevel();
            }
            GUI.backgroundColor = Color.white;

            // 예상 통계
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Estimated Stats", EditorStyles.miniLabel);

            int totalCells = _genGridSize * _genGridSize;
            int targetOccupied = Mathf.FloorToInt(totalCells * _genTargetDensity);
            int estimatedArrows = _genLaneCount * _genBalloonsPerLane + _genMissArrowCount;

            EditorGUILayout.LabelField($"  Grid: {_genGridSize}x{_genGridSize} = {totalCells} cells");
            EditorGUILayout.LabelField($"  Main Arrows: {estimatedArrows}");
            EditorGUILayout.LabelField($"  Target Cells: {targetOccupied} ({_genTargetDensity * 100:F0}%)");

            EditorGUILayout.EndVertical();
        }

        private void ApplyAutoCalculate()
        {
            var config = LevelGenerator.CalculateAutoParams(_genGridSize, _genTargetDensity, _genBendingEnabled);

            _genLaneCount = config.laneCount;
            _genBalloonsPerLane = config.balloonsPerLane;
            _genMissArrowCount = config.missArrowCount;
            _genMinLength = config.minBlockLength;
            _genMaxLength = config.maxBlockLength;

            Debug.Log($"[LevelEditor] Auto calculated: lanes={_genLaneCount}, balloons={_genBalloonsPerLane}, miss={_genMissArrowCount}, len={_genMinLength}-{_genMaxLength}");
        }

        private void GenerateNewLevel()
        {
            // Auto 계산이 켜져 있으면 먼저 적용
            if (_genAutoCalculate)
            {
                ApplyAutoCalculate();
            }

            // 설정 생성
            var config = new LevelGenerator.GeneratorConfig
            {
                gridSize = _genGridSize,
                targetDensity = _genTargetDensity,
                bendingEnabled = _genBendingEnabled,
                fillerEnabled = _genFillerEnabled,
                laneCount = _genLaneCount,
                balloonsPerLane = _genBalloonsPerLane,
                missArrowCount = _genMissArrowCount,
                minBlockLength = _genMinLength,
                maxBlockLength = _genMaxLength,
                bendingChance = _genBendingEnabled ? 1.0f : 0f,
                branchingMode = _genBranchingMode,
                branchingChance = _genBranchingChance
            };

            try
            {
                // 레벨 생성
                var generatedLevel = LevelGenerator.GenerateLevel(config);

                if (generatedLevel != null)
                {
                    _currentLevel = generatedLevel;

                    // 사용자가 입력한 이름이 있으면 적용, 없으면 생성된 이름 사용
                    if (!string.IsNullOrEmpty(_levelName) && _levelName != "NewLevel")
                    {
                        generatedLevel.name = _levelName;
                    }
                    else
                    {
                        _levelName = generatedLevel.name;
                    }

                    _gridSize = generatedLevel.gridSize;
                    _selectedArrowIndex = -1;
                    _validationDirty = true; // 검증 갱신 필요

                    // 콘솔 로그로 통계 표시 (다이얼로그 제거)
                    string stats = $"[LevelEditor] Generated: {generatedLevel.name} | " +
                                   $"Arrows: {generatedLevel.arrows?.Count ?? 0} | " +
                                   $"Density: {(generatedLevel.stats?.density ?? 0) * 100:F0}%";
                    Debug.Log(stats);

                    SceneView.RepaintAll();
                }
                else
                {
                    EditorUtility.DisplayDialog("Generation Failed", "Could not generate a valid level. Try different parameters.", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Generation error: {e.Message}", "OK");
                Debug.LogError($"[LevelEditor] Generation error: {e}");
            }
        }

        // ========== Preview 패널 ==========
        private void DrawPreviewPanel()
        {
            _previewScrollPosition = EditorGUILayout.BeginScrollView(_previewScrollPosition);

            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (_currentLevel == null)
            {
                EditorGUILayout.HelpBox("레벨을 생성하거나 불러오면 여기에 미리보기가 표시됩니다.", MessageType.Info);
            }
            else
            {
                // 순서 변경: Queue → Grid → Solvable → Stats
                DrawQueuePreview();
                EditorGUILayout.Space(10);
                DrawGridPreview();
                EditorGUILayout.Space(10);
                DrawSolvableInfo();
                EditorGUILayout.Space(10);
                DrawStatsPreview();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGridPreview()
        {
            if (_currentLevel == null) return;

            int gridSize = _currentLevel.gridSize;
            float totalSize = gridSize * PREVIEW_CELL_SIZE;

            EditorGUILayout.LabelField($"Grid ({gridSize}x{gridSize})", EditorStyles.boldLabel);

            // 그리드 영역 확보
            Rect gridRect = GUILayoutUtility.GetRect(totalSize + 20, totalSize + 20);
            gridRect.x += 10;
            gridRect.y += 5;

            // 점유된 셀 계산
            var occupiedCells = new Dictionary<Vector2Int, (Color color, bool isHead, string dir)>();

            if (_currentLevel.arrows != null)
            {
                for (int arrowIdx = 0; arrowIdx < _currentLevel.arrows.Count; arrowIdx++)
                {
                    var arrow = _currentLevel.arrows[arrowIdx];
                    var cells = arrow.GetCells();
                    Color arrowColor = GetPreviewColor(arrow.color);

                    // 선택된 화살표는 하이라이트
                    if (arrowIdx == _selectedArrowIndex)
                    {
                        arrowColor = Color.white;
                    }

                    for (int i = 0; i < cells.Count; i++)
                    {
                        bool isHead = (i == cells.Count - 1); // 마지막이 Head
                        occupiedCells[cells[i]] = (arrowColor, isHead, arrow.direction);
                    }
                }
            }

            // 그리드 라인 그리기
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

            // 수직선
            for (int x = 0; x <= gridSize; x++)
            {
                Vector3 start = new Vector3(gridRect.x + x * PREVIEW_CELL_SIZE, gridRect.y, 0);
                Vector3 end = new Vector3(gridRect.x + x * PREVIEW_CELL_SIZE, gridRect.y + gridSize * PREVIEW_CELL_SIZE, 0);
                Handles.DrawLine(start, end);
            }

            // 수평선
            for (int y = 0; y <= gridSize; y++)
            {
                Vector3 start = new Vector3(gridRect.x, gridRect.y + y * PREVIEW_CELL_SIZE, 0);
                Vector3 end = new Vector3(gridRect.x + gridSize * PREVIEW_CELL_SIZE, gridRect.y + y * PREVIEW_CELL_SIZE, 0);
                Handles.DrawLine(start, end);
            }

            // 빈 셀만 그리기 (화살표는 별도로 그림)
            // Y좌표 반전: 인게임에서는 Y=0이 아래, 에디터 GUI에서는 Y=0이 위
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // 점유되지 않은 빈 셀만 표시
                    if (!occupiedCells.ContainsKey(pos))
                    {
                        // Y좌표 반전하여 그리기
                        int flippedY = gridSize - 1 - y;
                        Rect cellRect = new Rect(
                            gridRect.x + x * PREVIEW_CELL_SIZE + 1,
                            gridRect.y + flippedY * PREVIEW_CELL_SIZE + 1,
                            PREVIEW_CELL_SIZE - 2,
                            PREVIEW_CELL_SIZE - 2
                        );
                        EditorGUI.DrawRect(cellRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
                    }
                }
            }

            // 화살표 그리기 (직선+원+삼각형 형태)
            if (_currentLevel.arrows != null)
            {
                for (int arrowIdx = 0; arrowIdx < _currentLevel.arrows.Count; arrowIdx++)
                {
                    var arrow = _currentLevel.arrows[arrowIdx];
                    Color arrowColor = GetPreviewColor(arrow.color);

                    // 선택된 화살표는 하이라이트
                    if (arrowIdx == _selectedArrowIndex)
                    {
                        arrowColor = Color.white;
                    }

                    DrawArrowInPreview(gridRect, arrow, arrowColor, gridSize);
                }
            }
        }

        private void DrawArrowInPreview(Rect gridRect, ArrowData arrow, Color arrowColor, int gridSize)
        {
            var cells = arrow.GetCells();
            if (cells.Count == 0) return;

            // 셀 좌표를 화면 좌표로 변환 (Y좌표 반전)
            List<Vector3> screenPositions = new List<Vector3>();
            foreach (var cell in cells)
            {
                // Y좌표 반전: 인게임에서는 Y=0이 아래, 에디터 GUI에서는 Y=0이 위
                int flippedY = gridSize - 1 - cell.y;
                float cx = gridRect.x + cell.x * PREVIEW_CELL_SIZE + PREVIEW_CELL_SIZE * 0.5f;
                float cy = gridRect.y + flippedY * PREVIEW_CELL_SIZE + PREVIEW_CELL_SIZE * 0.5f;
                screenPositions.Add(new Vector3(cx, cy, 0));
            }

            // Body: 직선으로 연결
            Handles.color = arrowColor;
            for (int i = 0; i < screenPositions.Count - 1; i++)
            {
                Handles.DrawAAPolyLine(3f, screenPositions[i], screenPositions[i + 1]);
            }

            // Tail: 동그라미 (첫 번째 셀)
            Handles.DrawSolidDisc(screenPositions[0], Vector3.forward, 5f);

            // Head: 삼각형 화살표 (마지막 셀)
            DrawArrowHeadTriangle(screenPositions[screenPositions.Count - 1], arrow.direction, arrowColor);
        }

        private void DrawArrowHeadTriangle(Vector3 position, string direction, Color color)
        {
            Handles.color = color;
            float size = PREVIEW_CELL_SIZE * 0.35f;

            Vector2 dir = direction switch
            {
                "U" => Vector2.up,
                "D" => Vector2.down,
                "L" => Vector2.left,
                "R" => Vector2.right,
                _ => Vector2.right
            };

            // Unity Editor에서 Y축은 아래로 증가하므로 반전
            dir.y = -dir.y;

            Vector2 perp = new Vector2(-dir.y, dir.x);

            Vector3 tip = position + (Vector3)(dir * size);
            Vector3 left = position - (Vector3)(dir * size * 0.3f) + (Vector3)(perp * size * 0.6f);
            Vector3 right = position - (Vector3)(dir * size * 0.3f) - (Vector3)(perp * size * 0.6f);

            Handles.DrawAAConvexPolygon(tip, left, right);
        }

        private void DrawQueuePreview()
        {
            if (_currentLevel == null || _currentLevel.lanes == null) return;

            EditorGUILayout.LabelField("Balloon Queue", EditorStyles.boldLabel);

            // 중앙 정렬 스타일
            GUIStyle centeredStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = Color.white }
            };

            // 활성 풍선 표시 스타일 (하단 = balloons[0])
            GUIStyle activeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = Color.yellow }
            };

            // 모든 Lane 중 가장 긴 것 찾기 (세로 높이 결정)
            int maxBalloons = 0;
            foreach (var lane in _currentLevel.lanes)
            {
                if (lane.balloons != null && lane.balloons.Count > maxBalloons)
                    maxBalloons = lane.balloons.Count;
            }

            if (maxBalloons == 0)
            {
                EditorGUILayout.LabelField("(No balloons)", EditorStyles.miniLabel);
                return;
            }

            // Lane 헤더 (가로로 나열)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            for (int laneIdx = 0; laneIdx < _currentLevel.lanes.Count; laneIdx++)
            {
                EditorGUILayout.LabelField($"Lane {laneIdx + 1}", EditorStyles.boldLabel, GUILayout.Width(35));
                GUILayout.Space(5);
            }
            EditorGUILayout.EndHorizontal();

            // 풍선 그리기 (위에서 아래로: balloons[last] → balloons[0])
            // balloons[0]이 맨 아래 (활성), balloons[last]가 맨 위
            for (int row = maxBalloons - 1; row >= 0; row--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);

                for (int laneIdx = 0; laneIdx < _currentLevel.lanes.Count; laneIdx++)
                {
                    var lane = _currentLevel.lanes[laneIdx];

                    if (lane.balloons != null && row < lane.balloons.Count)
                    {
                        string colorCode = lane.balloons[row];
                        Color balloonColor = GetPreviewColor(colorCode);
                        bool isActive = (row == 0); // balloons[0]이 활성 풍선

                        // 풍선 박스 그리기
                        Rect balloonRect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
                        EditorGUI.DrawRect(balloonRect, balloonColor);

                        // 테두리 (활성 풍선은 노란색 테두리)
                        Color borderColor = isActive ? Color.yellow : new Color(0.2f, 0.2f, 0.2f);
                        Handles.DrawSolidRectangleWithOutline(balloonRect, Color.clear, borderColor);

                        // 색상 코드 라벨
                        GUI.Label(balloonRect, colorCode, isActive ? activeStyle : centeredStyle);
                    }
                    else
                    {
                        // 빈 공간
                        GUILayout.Space(32);
                    }

                    GUILayout.Space(8);
                }

                EditorGUILayout.EndHorizontal();
            }

            // 활성 풍선 표시
            EditorGUILayout.LabelField("  ↑ Active (can be popped)", EditorStyles.miniLabel);
        }

        private void DrawSolvableInfo()
        {
            if (_currentLevel == null) return;

            EditorGUILayout.LabelField("Solvability", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            // 캐시된 검증 결과 사용 또는 새로 검증
            if (_validationDirty || _cachedValidation == null)
            {
                _cachedValidation = LevelGenerator.ValidateLevel(_currentLevel);
                _validationDirty = false;
            }

            if (_cachedValidation == null)
            {
                EditorGUILayout.LabelField("⚠️ Validation Error", EditorStyles.boldLabel);
            }
            else if (_cachedValidation.valid)
            {
                // 검증 성공
                EditorGUILayout.BeginHorizontal();
                GUIStyle successStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.2f, 0.8f, 0.3f) }
                };
                EditorGUILayout.LabelField("✓ Solvable", successStyle, GUILayout.Width(80));

                if (_cachedValidation.queueCleared)
                {
                    EditorGUILayout.LabelField("(Queue Cleared)", EditorStyles.miniLabel);
                }
                else
                {
                    GUIStyle warningStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(1f, 0.7f, 0.2f) }
                    };
                    EditorGUILayout.LabelField("(Queue not fully cleared)", warningStyle);
                }
                EditorGUILayout.EndHorizontal();

                // Solution Order 표시
                if (_cachedValidation.escapeColors != null && _cachedValidation.escapeColors.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Solution Order:", EditorStyles.boldLabel);

                    // 색상과 순서 표시
                    EditorGUILayout.BeginHorizontal();

                    GUIStyle orderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 10,
                        normal = { textColor = Color.white }
                    };

                    int displayCount = Mathf.Min(_cachedValidation.escapeColors.Count, 15); // 최대 15개만 표시
                    for (int i = 0; i < displayCount; i++)
                    {
                        string colorCode = _cachedValidation.escapeColors[i];
                        Color arrowColor = GetPreviewColor(colorCode);

                        // 순서 번호 + 색상 박스
                        Rect rect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
                        EditorGUI.DrawRect(rect, arrowColor);
                        GUI.Label(rect, (i + 1).ToString(), orderStyle);

                        // 화살표 표시 (마지막 제외)
                        if (i < displayCount - 1)
                        {
                            GUILayout.Label("→", GUILayout.Width(12));
                        }
                    }

                    if (_cachedValidation.escapeColors.Count > displayCount)
                    {
                        EditorGUILayout.LabelField($"...+{_cachedValidation.escapeColors.Count - displayCount}", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                // 검증 실패
                GUIStyle failStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(1f, 0.3f, 0.3f) }
                };
                EditorGUILayout.LabelField($"✗ Not Solvable", failStyle);

                if (!string.IsNullOrEmpty(_cachedValidation.reason))
                {
                    EditorGUILayout.LabelField($"  Reason: {_cachedValidation.reason}", EditorStyles.miniLabel);
                }
            }

            // 다시 검증 버튼
            EditorGUILayout.Space(3);
            if (GUILayout.Button("Revalidate", GUILayout.Width(80)))
            {
                _validationDirty = true;
                Repaint();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatsPreview()
        {
            if (_currentLevel == null) return;

            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");

            // 화살표 수
            int arrowCount = _currentLevel.arrows?.Count ?? 0;
            int mainArrows = 0;
            int fillers = 0;

            if (_currentLevel.arrows != null)
            {
                foreach (var arrow in _currentLevel.arrows)
                {
                    if (arrow.isFiller)
                        fillers++;
                    else
                        mainArrows++;
                }
            }

            EditorGUILayout.LabelField($"Total Arrows: {arrowCount}");
            EditorGUILayout.LabelField($"  - Main: {mainArrows}");
            EditorGUILayout.LabelField($"  - Fillers: {fillers}");

            // 풍선 수
            int totalBalloons = 0;
            if (_currentLevel.lanes != null)
            {
                foreach (var lane in _currentLevel.lanes)
                {
                    totalBalloons += lane.balloons?.Count ?? 0;
                }
            }
            EditorGUILayout.LabelField($"Total Balloons: {totalBalloons}");

            // 밀도
            if (_currentLevel.stats != null)
            {
                EditorGUILayout.LabelField($"Density: {_currentLevel.stats.density * 100:F1}%");
            }
            else
            {
                // 밀도 직접 계산
                int gridSize = _currentLevel.gridSize;
                int totalCells = gridSize * gridSize;
                int occupiedCells = 0;

                if (_currentLevel.arrows != null)
                {
                    foreach (var arrow in _currentLevel.arrows)
                    {
                        occupiedCells += arrow.GetCells().Count;
                    }
                }

                float density = totalCells > 0 ? (float)occupiedCells / totalCells : 0f;
                EditorGUILayout.LabelField($"Density: {density * 100:F1}%");
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetPreviewColor(string colorCode)
        {
            return colorCode switch
            {
                "R" => new Color(1f, 0.3f, 0.3f),
                "G" => new Color(0.3f, 0.8f, 0.4f),
                "B" => new Color(0.3f, 0.5f, 1f),
                "Y" => new Color(1f, 0.85f, 0.2f),
                "P" => new Color(0.7f, 0.3f, 0.9f),
                "O" => new Color(1f, 0.6f, 0.2f),
                "C" => new Color(0.2f, 0.9f, 0.9f),
                "K" => new Color(1f, 0.5f, 0.7f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
        }

        // ========== Scene 편집 토글 ==========
        private void DrawSceneEditToggle()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            _isEditingInScene = EditorGUILayout.Toggle("Scene Edit Mode", _isEditingInScene);

            if (_isEditingInScene)
            {
                GUILayout.Label("🔵 Active", EditorStyles.boldLabel);
            }

            EditorGUILayout.EndHorizontal();

            if (_isEditingInScene)
            {
                EditorGUILayout.HelpBox("Scene View에서 그리드 셀을 클릭하여 화살표를 배치합니다.\n좌클릭: 배치 | Shift+클릭: 삭제", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        // ========== Scene GUI ==========
        private void OnSceneGUI(SceneView sceneView)
        {
            if (_currentLevel == null)
                return;

            // GridSystem 참조 가져오기
            var gridSystem = FindObjectOfType<GridSystem>();
            if (gridSystem == null)
                return;

            int gridSize = _currentLevel.gridSize;
            float cellSize = gridSystem.CellSize;
            Vector2 gridOrigin = gridSystem.GridOrigin;

            // 그리드 그리기
            DrawGrid(gridSize, cellSize, gridOrigin);

            // 화살표 그리기
            DrawArrows(gridSystem);

            // 편집 모드일 때 입력 처리
            if (_isEditingInScene)
            {
                HandleSceneInput(gridSystem, gridSize, cellSize, gridOrigin);
            }
        }

        private void DrawGrid(int gridSize, float cellSize, Vector2 origin)
        {
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            for (int x = 0; x <= gridSize; x++)
            {
                Vector3 start = new Vector3(origin.x + x * cellSize - cellSize * 0.5f, origin.y - cellSize * 0.5f, 0);
                Vector3 end = new Vector3(origin.x + x * cellSize - cellSize * 0.5f, origin.y + gridSize * cellSize - cellSize * 0.5f, 0);
                Handles.DrawLine(start, end);
            }

            for (int y = 0; y <= gridSize; y++)
            {
                Vector3 start = new Vector3(origin.x - cellSize * 0.5f, origin.y + y * cellSize - cellSize * 0.5f, 0);
                Vector3 end = new Vector3(origin.x + gridSize * cellSize - cellSize * 0.5f, origin.y + y * cellSize - cellSize * 0.5f, 0);
                Handles.DrawLine(start, end);
            }
        }

        private void DrawArrows(GridSystem gridSystem)
        {
            if (_currentLevel.arrows == null)
                return;

            for (int i = 0; i < _currentLevel.arrows.Count; i++)
            {
                var arrow = _currentLevel.arrows[i];
                var cells = arrow.GetCells();

                bool isSelected = i == _selectedArrowIndex;
                Color arrowColor = GetArrowColor(arrow.color);

                if (isSelected)
                {
                    arrowColor = Color.white;
                }

                // 셀 그리기
                foreach (var cell in cells)
                {
                    Vector2 worldPos = gridSystem.GridToWorld(cell);
                    float size = gridSystem.CellSize * 0.8f;

                    Handles.color = arrowColor;
                    Handles.DrawSolidRectangleWithOutline(
                        new Rect(worldPos.x - size * 0.5f, worldPos.y - size * 0.5f, size, size),
                        new Color(arrowColor.r, arrowColor.g, arrowColor.b, 0.5f),
                        arrowColor
                    );
                }

                // Head 표시 (방향 화살표)
                if (cells.Count > 0)
                {
                    Vector2 headPos = gridSystem.GridToWorld(cells[cells.Count - 1]);
                    Vector2 dir = GetDirectionVector(arrow.Direction);

                    Handles.color = Color.white;
                    Handles.DrawLine(
                        headPos,
                        headPos + dir * gridSystem.CellSize * 0.4f
                    );
                }
            }
        }

        private Color GetArrowColor(string colorCode)
        {
            return colorCode switch
            {
                "R" => new Color(1f, 0.2f, 0.2f),
                "G" => new Color(0.2f, 0.8f, 0.2f),
                "B" => new Color(0.2f, 0.4f, 1f),
                "Y" => new Color(1f, 0.9f, 0.2f),
                "P" => new Color(0.6f, 0.2f, 0.8f),
                "O" => new Color(1f, 0.5f, 0.1f),
                _ => Color.white
            };
        }

        private Vector2 GetDirectionVector(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => Vector2.up,
                ArrowDirection.Down => Vector2.down,
                ArrowDirection.Left => Vector2.left,
                ArrowDirection.Right => Vector2.right,
                _ => Vector2.up
            };
        }

        private void HandleSceneInput(GridSystem gridSystem, int gridSize, float cellSize, Vector2 origin)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Vector2 mousePos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                Vector2Int gridPos = gridSystem.WorldToGrid(mousePos);

                if (gridPos.x >= 0 && gridPos.x < gridSize && gridPos.y >= 0 && gridPos.y < gridSize)
                {
                    if (e.shift)
                    {
                        // Shift + 클릭: 해당 위치의 화살표 삭제
                        RemoveArrowAt(gridPos);
                    }
                    else
                    {
                        // 클릭: 화살표 배치
                        PlaceArrow(gridPos);
                    }

                    e.Use();
                    Repaint();
                }
            }

            // Scene View가 이 이벤트를 처리하지 않도록
            if (_isEditingInScene)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
        }

        // ========== 레벨 관리 ==========
        private void CreateNewLevel()
        {
            _currentLevel = LevelSaver.CreateNew(_levelName, _gridSize);
            _selectedArrowIndex = -1;
            _validationDirty = true; // 검증 갱신 필요
            Debug.Log($"[LevelEditor] Created new level: {_levelName}");
            SceneView.RepaintAll();
        }

        private void LoadLevel(string levelName)
        {
            _currentLevel = LevelSaver.Load(levelName);
            if (_currentLevel != null)
            {
                _levelName = _currentLevel.name;
                _gridSize = _currentLevel.gridSize;
                _selectedArrowIndex = -1;
                _validationDirty = true; // 검증 갱신 필요
                Debug.Log($"[LevelEditor] Loaded level: {levelName}");
                SceneView.RepaintAll();
            }
        }

        private void SaveLevel()
        {
            if (_currentLevel == null)
                return;

            if (LevelSaver.Save(_currentLevel))
            {
                RefreshLevelList();
                EditorUtility.DisplayDialog("Save", $"Level '{_currentLevel.name}' saved successfully!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to save level.", "OK");
            }
        }

        private void RefreshLevelList()
        {
            _levelList = LevelSaver.GetLevelList();
        }

        private void TestPlayLevel()
        {
            if (_currentLevel == null)
                return;

            // 에디터 테스트 레벨로 설정 (Play Mode 시작 시 GameManager가 이 레벨을 로드)
            GameManager.SetEditorTestLevel(_currentLevel);
            EditorApplication.isPlaying = true;
        }

        // ========== 화살표 편집 ==========
        private void PlaceArrow(Vector2Int gridPos)
        {
            if (_currentLevel == null)
                return;

            if (_currentLevel.arrows == null)
                _currentLevel.arrows = new List<ArrowData>();

            // 해당 위치에 이미 화살표가 있는지 확인
            var existingArrow = FindArrowAt(gridPos);
            if (existingArrow != null)
            {
                // 이미 있으면 선택
                _selectedArrowIndex = _currentLevel.arrows.IndexOf(existingArrow);
                return;
            }

            // 새 화살표 생성
            var newArrow = new ArrowData
            {
                x = gridPos.x,
                y = gridPos.y,
                color = ColorHelper.ToString(_selectedColor),
                direction = DirectionHelper.ToString(_selectedDirection),
                length = _selectedLength,
                order = _currentLevel.arrows.Count + 1,
                isFiller = false
            };

            _currentLevel.arrows.Add(newArrow);
            _selectedArrowIndex = _currentLevel.arrows.Count - 1;
            _validationDirty = true; // 검증 갱신 필요

            Debug.Log($"[LevelEditor] Arrow placed at ({gridPos.x}, {gridPos.y})");
            SceneView.RepaintAll();
        }

        private void RemoveArrowAt(Vector2Int gridPos)
        {
            if (_currentLevel?.arrows == null)
                return;

            var arrow = FindArrowAt(gridPos);
            if (arrow != null)
            {
                int index = _currentLevel.arrows.IndexOf(arrow);
                _currentLevel.arrows.Remove(arrow);

                if (_selectedArrowIndex == index)
                    _selectedArrowIndex = -1;
                else if (_selectedArrowIndex > index)
                    _selectedArrowIndex--;

                _validationDirty = true; // 검증 갱신 필요
                Debug.Log($"[LevelEditor] Arrow removed at ({gridPos.x}, {gridPos.y})");
                SceneView.RepaintAll();
            }
        }

        private ArrowData FindArrowAt(Vector2Int gridPos)
        {
            if (_currentLevel?.arrows == null)
                return null;

            foreach (var arrow in _currentLevel.arrows)
            {
                var cells = arrow.GetCells();
                foreach (var cell in cells)
                {
                    if (cell == gridPos)
                        return arrow;
                }
            }

            return null;
        }
    }
}
