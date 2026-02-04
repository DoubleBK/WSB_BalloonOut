using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Gimmick.Behaviors;

namespace BalloonOut.Editor
{
    /// <summary>
    /// Level Editor Window - Unity Editor 내에서 레벨 편집
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ========== 설정 상수 ==========
        private const int DEFAULT_GRID_SIZE = 6;
        private const int DEFAULT_GEN_GRID_SIZE = 16;
        private const float DEFAULT_TARGET_DENSITY = 0.9f;
        private const float DEFAULT_BRANCHING_CHANCE = 0.4f;
        private const int DEFAULT_COLOR_COUNT = 6;
        private const int MIN_COLOR_COUNT = 2;
        private const int MAX_COLOR_COUNT = 12;
        private const int TOTAL_COLOR_COUNT = 12;
        private const int INITIAL_COLORS_ENABLED = 6;
        private const int DEFAULT_BATCH_FROM = 1;
        private const int DEFAULT_BATCH_TO = 100;
        private const int GENERATION_ATTEMPTS_PER_STEP = 200;

        // ========== 에디터 상태 ==========
        private LevelData _currentLevel;
        private string _levelName = "NewLevel";
        private int _gridWidth = DEFAULT_GRID_SIZE;
        private int _gridHeight = DEFAULT_GRID_SIZE;

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
        private int _genGridWidth = DEFAULT_GEN_GRID_SIZE;
        private int _genGridHeight = DEFAULT_GEN_GRID_SIZE;
        private float _genTargetDensity = DEFAULT_TARGET_DENSITY;
        private bool _genBendingEnabled = true;
        private float _genBendingChance = 0.5f;  // Bending 확률 (0~1)
        private bool _genFillerEnabled = false;
        private int _genLaneCount = 3;
        private int _genBalloonsPerLane = 2;
        private int _genMissArrowCount = 1;
        private int _genDecoyArrowCount = 0;
        private int _genMinLength = 3;
        private int _genMaxLength = 8;
        private bool _genAutoCalculate = true;
        private bool _genBranchingMode = true;
        private float _genBranchingChance = DEFAULT_BRANCHING_CHANCE;

        // 색상 설정
        private int _genColorCount = DEFAULT_COLOR_COUNT;
        private bool _genUseSpecificColors = false;  // 특정 색상 선택 모드
        private bool[] _genColorEnabled = new bool[11] { true, true, true, true, true, true, false, false, false, false, false };

        // 기믹 생성 설정
        private const float GIMMICK_PANEL_WIDTH = 220f;
        private bool _gimmickPanelFoldout = true;
        private Vector2 _gimmickPanelScroll;
        private List<GimmickGeneratorConfig> _genBalloonGimmicks = new List<GimmickGeneratorConfig>();
        private List<GimmickGeneratorConfig> _genArrowGimmicks = new List<GimmickGeneratorConfig>();
        private bool _gimmicksInitialized = false;
        private static readonly string[] COLOR_CODES = { "R", "G", "B", "Y", "P", "O", "C", "K", "W", "N", "M" };
        private static readonly string[] COLOR_NAMES = { "Red", "Green", "Blue", "Yellow", "Purple", "Orange", "Cyan", "Pink", "Brown", "Navy", "Magenta" };
        private static readonly Color[] COLOR_VALUES = {
            new Color(1f, 0.3f, 0.3f),      // Red
            new Color(0.3f, 0.8f, 0.3f),    // Green
            new Color(0.3f, 0.5f, 1f),      // Blue
            new Color(1f, 0.9f, 0.2f),      // Yellow
            new Color(0.35f, 0f, 1f),       // Purple (진짜 보라)
            new Color(1f, 0.5f, 0.1f),      // Orange (더 진한 주황)
            new Color(0.2f, 0.9f, 0.9f),    // Cyan
            new Color(1f, 0.5f, 0.7f),      // Pink
            new Color(0.6f, 0.4f, 0.2f),    // Brown
            new Color(0.2f, 0.3f, 0.6f),    // Navy
            new Color(1f, 0.3f, 0.8f)       // Magenta
        };

        // Validation 캐시
        private LevelValidator.ValidationResult _cachedValidation;
        private bool _validationDirty = true;

        // Batch Generation 설정
        private TextAsset _batchConfigTable;
        private int _batchFromLevel = DEFAULT_BATCH_FROM;
        private int _batchToLevel = DEFAULT_BATCH_TO;
        private bool _batchOverwrite = false;

        // 레벨 목록
        private List<string> _levelList = new List<string>();
        private int _selectedLevelIndex = -1;

        // Selected lane for balloon tab
        private int _selectedLaneIndex = -1;

        // Preview 설정
        private const float LEFT_PANEL_WIDTH = 400f;
        private const float PREVIEW_CELL_SIZE = 28f;
        private const float LEVEL_LIST_PANEL_WIDTH = 200f;
        private Vector2 _previewScrollPosition;

        // 드래그 상태
        private bool _isDragging = false;
        private Vector2Int _dragStartPos;
        private List<Vector2Int> _dragPath = new List<Vector2Int>();
        private Rect _dragGridRect;

        // LevelList 패널
        private Vector2 _levelListScrollPosition;
        private int _selectedLevelListIndex = -1;
        private List<LevelListEntry> _levelListEntries = new List<LevelListEntry>();

        private class LevelListEntry
        {
            public string assetName;
            public bool isSolvable;
            public bool isQueueCleared;
            public bool isValidated;
        }

        // ========== 메뉴 ==========
        [MenuItem("BalloonOut/Level Editor %#l")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(1050, 550);
            window.Show();
        }

        // ========== 유니티 라이프사이클 ==========
        private void OnEnable()
        {
            RefreshLevelList();
            RefreshLevelListEntries();
            InitializeGimmickConfigs();

            // 윈도우 열릴 때 자동 검증 (delayCall로 에디터 초기화 완료 후 실행)
            EditorApplication.delayCall += AutoValidateOnOpen;
        }

        /// <summary>
        /// 기믹 생성 설정 초기화
        /// </summary>
        private void InitializeGimmickConfigs()
        {
            if (_gimmicksInitialized) return;

            _genBalloonGimmicks = new List<GimmickGeneratorConfig>
            {
                new GimmickGeneratorConfig("surprise"),
                new GimmickGeneratorConfig("number"),
                new GimmickGeneratorConfig("connected")
            };
            _genArrowGimmicks = new List<GimmickGeneratorConfig>();

            _gimmicksInitialized = true;
        }

        private void AutoValidateOnOpen()
        {
            // 검증할 항목이 없거나 이미 모두 검증 완료면 스킵
            if (_levelListEntries.Count == 0) return;
            bool anyUnvalidated = _levelListEntries.Any(e => !e.isValidated);
            if (!anyUnvalidated) return;

            ValidateAllLevels();
        }

        private void OnDisable()
        {
            // Cleanup if needed
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

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ========== 기믹 패널 (Generate 탭에서만 표시) ==========
            if (_currentTab == 3)  // Generate 탭
            {
                EditorGUILayout.BeginVertical("box", GUILayout.Width(GIMMICK_PANEL_WIDTH));
                DrawGimmickPanel();
                EditorGUILayout.EndVertical();
            }

            // ========== 중앙 패널 (Preview) ==========
            EditorGUILayout.BeginVertical("box");
            DrawPreviewPanel();
            EditorGUILayout.EndVertical();

            // ========== 오른쪽 패널 (Level List) ==========
            EditorGUILayout.BeginVertical("box", GUILayout.Width(LEVEL_LIST_PANEL_WIDTH));
            DrawLevelListPanel();
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
            string newName = EditorGUILayout.TextField("Name", _levelName);
            if (newName != _levelName)
            {
                _levelName = newName;
                // _currentLevel.name도 동기화 (Save 시 이 이름 사용)
                if (_currentLevel != null)
                {
                    _currentLevel.name = _levelName;
                }
            }

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

            // Renew 버튼 - 에디터 상태 초기화
            if (GUILayout.Button("Renew", GUILayout.Height(30)))
            {
                RenewEditor();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 에디터 상태 초기화 (창 재시작 없이)
        /// </summary>
        private void RenewEditor()
        {
            // 에디터 상태 초기화
            _currentLevel = null;
            _levelName = "NewLevel";
            _gridWidth = 6;
            _gridHeight = 6;
            _selectedArrowIndex = -1;
            _selectedLaneIndex = -1;
            _cachedValidation = null;
            _validationDirty = true;

            // UI 상태 초기화
            _currentTab = 0;
            _scrollPosition = Vector2.zero;
            _arrowListScroll = Vector2.zero;
            _laneListScroll = Vector2.zero;
            _previewScrollPosition = Vector2.zero;
            _selectedLevelIndex = -1;

            // 레벨 목록 새로고침
            RefreshLevelList();

            // Scene View 갱신
            SceneView.RepaintAll();
            Repaint();

            Debug.Log("[LevelEditor] Editor renewed");
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

            GUI.enabled = _currentLevel != null;
            EditorGUILayout.HelpBox("Preview 패널의 그리드를 클릭하여 화살표 배치\n우클릭: 삭제", MessageType.Info);
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

            // 풍선 추가 버튼들 (12색)
            for (int i = 0; i < COLOR_CODES.Length; i++)
            {
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = COLOR_VALUES[i];
                if (GUILayout.Button(COLOR_CODES[i], GUILayout.Width(22)))
                {
                    AddBalloon(lane, COLOR_CODES[i]);
                }
                GUI.backgroundColor = prevColor;
            }

            GUILayout.FlexibleSpace();

            // Lane 삭제
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                _currentLevel.lanes.RemoveAt(laneIdx);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();

            // 풍선 목록 (balloonData 우선, 없으면 balloons 사용)
            var balloonDataList = lane.GetBalloonDataList();
            if (balloonDataList != null && balloonDataList.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < balloonDataList.Count; i++)
                {
                    var balloonData = balloonDataList[i];
                    var colorCode = balloonData.color;
                    GUI.backgroundColor = GetColorForBalloon(colorCode);

                    // 기믹 표시 문자열 생성
                    string displayText = GetBalloonDisplayText(balloonData);

                    // 풍선 버튼 (우클릭 메뉴 지원)
                    var buttonRect = GUILayoutUtility.GetRect(35, 35, GUILayout.Width(35), GUILayout.Height(35));
                    if (GUI.Button(buttonRect, displayText))
                    {
                        // 좌클릭: 컨텍스트 메뉴 표시
                        ShowBalloonContextMenu(lane, i);
                    }

                    // 우클릭도 컨텍스트 메뉴
                    if (Event.current.type == EventType.ContextClick && buttonRect.Contains(Event.current.mousePosition))
                    {
                        ShowBalloonContextMenu(lane, i);
                        Event.current.Use();
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

        /// <summary>
        /// 풍선 표시 텍스트 (기믹 포함)
        /// </summary>
        private string GetBalloonDisplayText(BalloonData data)
        {
            if (data.gimmicks == null || data.gimmicks.Count == 0)
                return data.color;

            // 기믹 표시
            string suffix = "";
            foreach (var gimmick in data.gimmicks)
            {
                if (gimmick.gimmickId == SurpriseGimmickBehavior.GIMMICK_ID)
                {
                    suffix += "?";
                }
                else if (gimmick.gimmickId == NumberGimmickBehavior.GIMMICK_ID)
                {
                    int hits = gimmick.GetParamInt("requiredHits", 2);
                    suffix += hits.ToString();
                }
            }

            return string.IsNullOrEmpty(suffix) ? data.color : $"{data.color}\n{suffix}";
        }

        /// <summary>
        /// 풍선 컨텍스트 메뉴 표시
        /// </summary>
        private void ShowBalloonContextMenu(LaneData lane, int balloonIndex)
        {
            EnsureBalloonData(lane);
            var balloonData = lane.balloonData[balloonIndex];

            var menu = new GenericMenu();

            // 삭제
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                lane.balloonData.RemoveAt(balloonIndex);
                // Legacy list도 동기화
                if (lane.balloons != null && lane.balloons.Count > balloonIndex)
                    lane.balloons.RemoveAt(balloonIndex);
            });

            menu.AddSeparator("");

            // Surprise 기믹 토글
            bool hasSurprise = balloonData.HasGimmick(SurpriseGimmickBehavior.GIMMICK_ID);
            menu.AddItem(new GUIContent("Surprise Gimmick"), hasSurprise, () =>
            {
                ToggleGimmick(balloonData, SurpriseGimmickBehavior.GIMMICK_ID);
            });

            // Number 기믹 토글
            bool hasNumber = balloonData.HasGimmick(NumberGimmickBehavior.GIMMICK_ID);
            menu.AddItem(new GUIContent("Number Gimmick (2 hits)"), hasNumber, () =>
            {
                if (hasNumber)
                {
                    // 제거
                    RemoveGimmick(balloonData, NumberGimmickBehavior.GIMMICK_ID);
                }
                else
                {
                    // 추가 (기본 2번)
                    AddNumberGimmick(balloonData, 2);
                }
            });

            // Number 기믹 히트 수 변경 (이미 있는 경우)
            if (hasNumber)
            {
                menu.AddSeparator("Number Hits/");
                for (int hits = 2; hits <= 5; hits++)
                {
                    int h = hits; // 클로저 캡처용
                    var gimmickData = balloonData.gimmicks.Find(g => g.gimmickId == NumberGimmickBehavior.GIMMICK_ID);
                    bool isCurrentHits = gimmickData != null && gimmickData.GetParamInt("requiredHits", 2) == hits;
                    menu.AddItem(new GUIContent($"Number Hits/{hits} hits"), isCurrentHits, () =>
                    {
                        SetNumberGimmickHits(balloonData, h);
                    });
                }
            }

            menu.AddSeparator("");

            // Connected 기믹 토글
            bool hasConnected = balloonData.HasGimmick("connected");
            menu.AddItem(new GUIContent("Connected Gimmick"), hasConnected, () =>
            {
                if (hasConnected)
                {
                    RemoveGimmick(balloonData, "connected");
                }
                else
                {
                    // 새 그룹으로 추가 (groupId는 임시로 생성)
                    string groupId = $"manual_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                    var gimmick = new GimmickInstanceData("connected");
                    gimmick.SetParam("groupId", groupId);
                    gimmick.SetParam("isMarked", false);
                    gimmick.SetParam("groupSize", 1);
                    balloonData.AddGimmick(gimmick);
                }
            });

            // Connected 그룹 선택 (이미 있는 경우)
            if (hasConnected)
            {
                var connectedGimmick = balloonData.gimmicks.Find(g => g.gimmickId == "connected");
                string currentGroupId = connectedGimmick?.GetParam("groupId", "") ?? "";
                menu.AddSeparator("Connected Group/");
                menu.AddDisabledItem(new GUIContent($"Connected Group/Current: {currentGroupId}"));

                // 기존 그룹 목록 표시 (현재 레벨의 모든 Connected 그룹 ID 수집)
                var existingGroupIds = GetExistingConnectedGroupIds();
                if (existingGroupIds.Count > 0)
                {
                    menu.AddSeparator("Connected Group/");
                    foreach (var groupId in existingGroupIds)
                    {
                        bool isCurrent = groupId == currentGroupId;
                        string gid = groupId; // 클로저 캡처용
                        menu.AddItem(new GUIContent($"Connected Group/Join: {groupId}"), isCurrent, () =>
                        {
                            SetConnectedGroupId(balloonData, gid);
                        });
                    }
                }

                // 새 그룹 생성 옵션
                menu.AddSeparator("Connected Group/");
                menu.AddItem(new GUIContent("Connected Group/Create New Group"), false, () =>
                {
                    string newGroupId = $"group_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                    SetConnectedGroupId(balloonData, newGroupId);
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// 현재 레벨의 모든 Connected 그룹 ID 수집
        /// </summary>
        private HashSet<string> GetExistingConnectedGroupIds()
        {
            var groupIds = new HashSet<string>();
            if (_currentLevel?.lanes == null) return groupIds;

            foreach (var lane in _currentLevel.lanes)
            {
                if (lane.balloonData == null) continue;
                foreach (var balloon in lane.balloonData)
                {
                    var connectedGimmick = balloon.gimmicks?.Find(g => g.gimmickId == "connected");
                    if (connectedGimmick != null)
                    {
                        string groupId = connectedGimmick.GetParam("groupId", "");
                        if (!string.IsNullOrEmpty(groupId))
                        {
                            groupIds.Add(groupId);
                        }
                    }
                }
            }
            return groupIds;
        }

        /// <summary>
        /// Connected 그룹 ID 변경
        /// </summary>
        private void SetConnectedGroupId(BalloonData balloonData, string groupId)
        {
            var connectedGimmick = balloonData.gimmicks?.Find(g => g.gimmickId == "connected");
            if (connectedGimmick != null)
            {
                connectedGimmick.SetParam("groupId", groupId);
                // 그룹 크기 업데이트 (같은 groupId를 가진 풍선 수 계산)
                int groupSize = CountBalloonsInGroup(groupId);
                connectedGimmick.SetParam("groupSize", groupSize);
                UpdateAllConnectedGroupSizes(groupId, groupSize);
            }
        }

        /// <summary>
        /// 특정 그룹의 풍선 수 계산
        /// </summary>
        private int CountBalloonsInGroup(string groupId)
        {
            int count = 0;
            if (_currentLevel?.lanes == null) return count;

            foreach (var lane in _currentLevel.lanes)
            {
                if (lane.balloonData == null) continue;
                foreach (var balloon in lane.balloonData)
                {
                    var connectedGimmick = balloon.gimmicks?.Find(g => g.gimmickId == "connected");
                    if (connectedGimmick != null && connectedGimmick.GetParam("groupId", "") == groupId)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 특정 그룹의 모든 풍선 groupSize 업데이트
        /// </summary>
        private void UpdateAllConnectedGroupSizes(string groupId, int groupSize)
        {
            if (_currentLevel?.lanes == null) return;

            foreach (var lane in _currentLevel.lanes)
            {
                if (lane.balloonData == null) continue;
                foreach (var balloon in lane.balloonData)
                {
                    var connectedGimmick = balloon.gimmicks?.Find(g => g.gimmickId == "connected");
                    if (connectedGimmick != null && connectedGimmick.GetParam("groupId", "") == groupId)
                    {
                        connectedGimmick.SetParam("groupSize", groupSize);
                    }
                }
            }
        }

        /// <summary>
        /// balloonData 리스트 보장 (legacy balloons에서 변환)
        /// </summary>
        private void EnsureBalloonData(LaneData lane)
        {
            if (lane.balloonData != null && lane.balloonData.Count > 0)
                return;

            lane.balloonData = new List<BalloonData>();
            if (lane.balloons != null)
            {
                foreach (var color in lane.balloons)
                {
                    lane.balloonData.Add(new BalloonData(color));
                }
            }
        }

        /// <summary>
        /// 기믹 토글
        /// </summary>
        private void ToggleGimmick(BalloonData data, string gimmickId)
        {
            if (data.HasGimmick(gimmickId))
            {
                RemoveGimmick(data, gimmickId);
            }
            else
            {
                data.AddGimmick(new GimmickInstanceData(gimmickId));
            }
        }

        /// <summary>
        /// 기믹 제거
        /// </summary>
        private void RemoveGimmick(BalloonData data, string gimmickId)
        {
            if (data.gimmicks == null) return;
            data.gimmicks.RemoveAll(g => g.gimmickId == gimmickId);
        }

        /// <summary>
        /// Number 기믹 추가
        /// </summary>
        private void AddNumberGimmick(BalloonData data, int requiredHits)
        {
            RemoveGimmick(data, NumberGimmickBehavior.GIMMICK_ID);
            var gimmick = NumberGimmickBehavior.CreateData(requiredHits);
            data.AddGimmick(gimmick);
        }

        /// <summary>
        /// Number 기믹 히트 수 변경
        /// </summary>
        private void SetNumberGimmickHits(BalloonData data, int requiredHits)
        {
            var gimmick = data.gimmicks?.Find(g => g.gimmickId == NumberGimmickBehavior.GIMMICK_ID);
            if (gimmick != null)
            {
                gimmick.SetParam("requiredHits", requiredHits);
                gimmick.SetParam("remainingHits", requiredHits);
            }
        }

        private void AddBalloon(LaneData lane, string color)
        {
            // balloonData 형식 우선 사용
            EnsureBalloonData(lane);
            lane.balloonData.Add(new BalloonData(color));

            // legacy 호환성을 위해 balloons도 업데이트
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

            int newGridWidth = EditorGUILayout.IntSlider("Grid Width", _currentLevel.GetGridWidth(), 4, 12);
            int newGridHeight = EditorGUILayout.IntSlider("Grid Height", _currentLevel.GetGridHeight(), 4, 12);
            if (newGridWidth != _currentLevel.GetGridWidth() || newGridHeight != _currentLevel.GetGridHeight())
            {
                _currentLevel.gridWidth = newGridWidth;
                _currentLevel.gridHeight = newGridHeight;
                _currentLevel.gridSize = 0;  // width/height 사용 표시
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

        // ========== 색상 토글 UI 헬퍼 ==========
        private void DrawColorToggle(int colorIndex)
        {
            // 체크박스
            _genColorEnabled[colorIndex] = GUILayout.Toggle(_genColorEnabled[colorIndex], "", GUILayout.Width(14));

            // 색상이 적용된 라벨
            var colorStyle = new GUIStyle(EditorStyles.miniLabel);
            colorStyle.normal.textColor = COLOR_VALUES[colorIndex];
            colorStyle.fontStyle = _genColorEnabled[colorIndex] ? FontStyle.Bold : FontStyle.Normal;
            GUILayout.Label(COLOR_NAMES[colorIndex], colorStyle, GUILayout.Width(50));
        }

        // ========== Generate 탭 ==========
        private void DrawGenerateTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("화살표 자동 생성 알고리즘으로 레벨 생성 하..", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 기본 설정
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);

            _genGridWidth = EditorGUILayout.IntSlider("Grid Width", _genGridWidth, 4, 30);
            _genGridHeight = EditorGUILayout.IntSlider("Grid Height", _genGridHeight, 4, 30);
            _genTargetDensity = EditorGUILayout.Slider("Target Density", _genTargetDensity, 0.2f, 1.0f);
            EditorGUILayout.LabelField($"  → {(_genTargetDensity * 100):F0}% of grid will be filled", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            _genBendingEnabled = EditorGUILayout.Toggle("Bending Enabled", _genBendingEnabled);
            GUI.enabled = _genBendingEnabled;
            _genBendingChance = EditorGUILayout.Slider("Bending Chance", _genBendingChance, 0f, 1f);
            GUI.enabled = true;
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

            // 색상 설정
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Color Settings", EditorStyles.boldLabel);

            _genUseSpecificColors = EditorGUILayout.Toggle("Select Specific Colors", _genUseSpecificColors);

            if (_genUseSpecificColors)
            {
                // 특정 색상 선택 모드
                EditorGUILayout.LabelField("Available Colors:", EditorStyles.miniLabel);

                // 첫 번째 줄 (6색)
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 6; i++)
                {
                    DrawColorToggle(i);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                // 두 번째 줄 (5색)
                GUILayout.BeginHorizontal();
                for (int i = 6; i < COLOR_CODES.Length; i++)
                {
                    DrawColorToggle(i);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                int selectedCount = 0;
                for (int i = 0; i < COLOR_CODES.Length; i++) if (_genColorEnabled[i]) selectedCount++;
                EditorGUILayout.LabelField($"Selected: {selectedCount} colors (min 2)", EditorStyles.miniLabel);

                if (selectedCount < 2)
                {
                    EditorGUILayout.HelpBox("최소 2개 이상의 색상을 선택해야 합니다.", MessageType.Warning);
                }
            }
            else
            {
                // 색상 수 선택 모드
                _genColorCount = EditorGUILayout.IntSlider("Color Count", _genColorCount, 2, COLOR_CODES.Length);
                EditorGUILayout.LabelField($"  → {_genColorCount} random colors will be used", EditorStyles.miniLabel);
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
            _genLaneCount = EditorGUILayout.IntSlider(_genLaneCount, 1, 8);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Balloons/Lane", GUILayout.Width(100));
            _genBalloonsPerLane = EditorGUILayout.IntSlider(_genBalloonsPerLane, 1, 15);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Miss Arrows", GUILayout.Width(100));
            _genMissArrowCount = EditorGUILayout.IntSlider(_genMissArrowCount, 0, 20);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Decoy Arrows", GUILayout.Width(100));
            _genDecoyArrowCount = EditorGUILayout.IntSlider(_genDecoyArrowCount, 0, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min Length", GUILayout.Width(100));
            _genMinLength = EditorGUILayout.IntSlider(_genMinLength, 1, 15);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max Length", GUILayout.Width(100));
            _genMaxLength = EditorGUILayout.IntSlider(_genMaxLength, 2, 40);
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

            int totalCells = _genGridWidth * _genGridHeight;
            int targetOccupied = Mathf.FloorToInt(totalCells * _genTargetDensity);
            int mainArrows = _genLaneCount * _genBalloonsPerLane + _genMissArrowCount;
            int totalArrows = mainArrows + _genDecoyArrowCount;

            EditorGUILayout.LabelField($"  Grid: {_genGridWidth}x{_genGridHeight} = {totalCells} cells");
            EditorGUILayout.LabelField($"  Main Arrows: {mainArrows}, Decoy: {_genDecoyArrowCount}, Total: {totalArrows}");
            EditorGUILayout.LabelField($"  Target Cells: {targetOccupied} ({_genTargetDensity * 100:F0}%)");

            EditorGUILayout.EndVertical();

            // ========== Batch Generation 섹션 ==========
            EditorGUILayout.Space(15);
            DrawBatchGenerateSection();
        }

        private void DrawBatchGenerateSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Batch Generation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("LevelConfigTable.json 기반으로 여러 레벨을 일괄 생성합니다.", MessageType.Info);

            EditorGUILayout.Space(5);

            // Config Table 선택
            _batchConfigTable = (TextAsset)EditorGUILayout.ObjectField(
                "Config Table", _batchConfigTable, typeof(TextAsset), false);

            if (_batchConfigTable == null)
            {
                // 기본 테이블 자동 로드 시도
                var defaultTable = Resources.Load<TextAsset>("Tables/LevelConfigTable");
                if (defaultTable != null)
                {
                    _batchConfigTable = defaultTable;
                }
            }

            EditorGUILayout.Space(3);

            // 범위 설정
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("From Level", GUILayout.Width(80));
            _batchFromLevel = EditorGUILayout.IntField(_batchFromLevel, GUILayout.Width(60));
            EditorGUILayout.LabelField("To Level", GUILayout.Width(60));
            _batchToLevel = EditorGUILayout.IntField(_batchToLevel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // 범위 유효성
            _batchFromLevel = Mathf.Max(1, _batchFromLevel);
            _batchToLevel = Mathf.Max(_batchFromLevel, _batchToLevel);

            // 덮어쓰기 토글
            _batchOverwrite = EditorGUILayout.Toggle("Overwrite Existing", _batchOverwrite);

            EditorGUILayout.Space(5);

            // Batch Generate 버튼
            GUI.enabled = _batchConfigTable != null;
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1.0f);
            if (GUILayout.Button($"Batch Generate (Level {_batchFromLevel}~{_batchToLevel})", GUILayout.Height(35)))
            {
                if (EditorUtility.DisplayDialog("Batch Generate",
                    $"Level {_batchFromLevel}~{_batchToLevel}을 일괄 생성합니다.\n" +
                    (_batchOverwrite ? "기존 파일을 덮어씁니다." : "기존 파일은 건너뜁니다.") +
                    "\n\n진행하시겠습니까?", "생성", "취소"))
                {
                    BatchGenerate(_batchFromLevel, _batchToLevel);
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 기믹 설정 패널 (Generate 탭 옆에 표시)
        /// </summary>
        private void DrawGimmickPanel()
        {
            EditorGUILayout.LabelField("Gimmick Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _gimmickPanelScroll = EditorGUILayout.BeginScrollView(_gimmickPanelScroll);

            // ========== Balloon Gimmicks ==========
            _gimmickPanelFoldout = EditorGUILayout.Foldout(_gimmickPanelFoldout, "Balloon Gimmicks", true);
            if (_gimmickPanelFoldout)
            {
                EditorGUI.indentLevel++;

                if (_genBalloonGimmicks == null || _genBalloonGimmicks.Count == 0)
                {
                    InitializeGimmickConfigs();
                }

                foreach (var gimmick in _genBalloonGimmicks)
                {
                    DrawGimmickConfigUI(gimmick);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // ========== Arrow Gimmicks (향후 확장) ==========
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Arrow Gimmicks", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("화살표 기믹은 향후 추가될 예정입니다.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 개별 기믹 설정 UI 그리기 (개수 기반)
        /// </summary>
        private void DrawGimmickConfigUI(GimmickGeneratorConfig config)
        {
            EditorGUILayout.BeginVertical("box");

            // 기믹 이름과 활성화 토글
            EditorGUILayout.BeginHorizontal();
            config.enabled = EditorGUILayout.Toggle(config.enabled, GUILayout.Width(20));
            string displayName = GetGimmickDisplayName(config.gimmickId);
            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            GUI.enabled = config.enabled;

            // 기믹별 설정
            switch (config.gimmickId)
            {
                case "surprise":
                    DrawSurpriseGimmickUI(config);
                    break;

                case "number":
                    DrawNumberGimmickUI(config);
                    break;

                case "connected":
                    DrawConnectedGimmickUI(config);
                    break;

                default:
                    EditorGUILayout.LabelField("  (설정 없음)", EditorStyles.miniLabel);
                    break;
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        /// <summary>
        /// Surprise 기믹 UI (개수 입력)
        /// </summary>
        private void DrawSurpriseGimmickUI(GimmickGeneratorConfig config)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Count", GUILayout.Width(50));
            config.count = EditorGUILayout.IntField(config.count, GUILayout.Width(60));
            config.count = Mathf.Max(0, config.count);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"  → {config.count}개 Surprise 풍선 생성", EditorStyles.miniLabel);
        }

        /// <summary>
        /// Number 기믹 UI (hitCounts 리스트)
        /// </summary>
        private void DrawNumberGimmickUI(GimmickGeneratorConfig config)
        {
            // hitCounts가 null이면 초기화
            if (config.hitCounts == null)
            {
                config.hitCounts = new List<int>();
            }

            EditorGUILayout.LabelField("Hit Counts:", EditorStyles.miniLabel);

            // Add 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[+] Add", GUILayout.Width(80)))
            {
                config.hitCounts.Add(2); // 기본값 2
            }
            EditorGUILayout.EndHorizontal();

            // 각 hitCount 항목
            for (int i = 0; i < config.hitCounts.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  #{i + 1}:", GUILayout.Width(40));
                config.hitCounts[i] = EditorGUILayout.IntField(config.hitCounts[i], GUILayout.Width(40));
                config.hitCounts[i] = Mathf.Max(1, config.hitCounts[i]); // 최소 1
                EditorGUILayout.LabelField("hits", GUILayout.Width(30));

                // 삭제 버튼
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    config.hitCounts.RemoveAt(i);
                    i--;
                    continue;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 요약 정보
            int totalCount = config.hitCounts.Count;
            int extraArrows = config.GetExtraArrowCount();
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField($"  → {totalCount}개 Number 풍선", EditorStyles.miniLabel);
            if (extraArrows > 0)
            {
                EditorGUILayout.LabelField($"  → 추가 화살표: {extraArrows}개 필요", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// Connected 기믹 UI (groupSizes 리스트)
        /// </summary>
        private void DrawConnectedGimmickUI(GimmickGeneratorConfig config)
        {
            // groupSizes가 null이면 초기화
            if (config.groupSizes == null)
            {
                config.groupSizes = new List<int>();
            }

            EditorGUILayout.LabelField("Group Sizes (풍선 수/그룹):", EditorStyles.miniLabel);

            // Add 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[+] Add Group", GUILayout.Width(100)))
            {
                config.groupSizes.Add(2); // 기본값 2개 (최소값)
            }
            EditorGUILayout.EndHorizontal();

            // 각 groupSize 항목
            for (int i = 0; i < config.groupSizes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Group #{i + 1}:", GUILayout.Width(70));
                config.groupSizes[i] = EditorGUILayout.IntField(config.groupSizes[i], GUILayout.Width(40));
                config.groupSizes[i] = Mathf.Clamp(config.groupSizes[i], 2, 5); // 2~5개
                EditorGUILayout.LabelField("balloons", GUILayout.Width(50));

                // 삭제 버튼
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    config.groupSizes.RemoveAt(i);
                    i--;
                    continue;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 요약 정보
            int groupCount = config.groupSizes.Count;
            int totalBalloons = config.GetConnectedTotalBalloonCount();
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField($"  → {groupCount}개 그룹, 총 {totalBalloons}개 Connected 풍선", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  (각 그룹은 서로 다른 레인에 배치됨)", EditorStyles.miniLabel);
        }

        /// <summary>
        /// 기믹 표시 이름 반환
        /// </summary>
        private string GetGimmickDisplayName(string gimmickId)
        {
            switch (gimmickId)
            {
                case "surprise": return "🎁 Surprise";
                case "number": return "🔢 Number";
                case "connected": return "🔗 Connected";
                default: return gimmickId;
            }
        }

        private void ApplyAutoCalculate()
        {
            // Generator는 정사각형만 지원하므로 min(width, height) 사용
            int genGridSize = Mathf.Min(_genGridWidth, _genGridHeight);
            var config = LevelGenerator.CalculateAutoParams(genGridSize, _genTargetDensity, _genBendingEnabled);

            _genLaneCount = config.laneCount;
            _genBalloonsPerLane = config.balloonsPerLane;
            _genMissArrowCount = config.missArrowCount;
            _genDecoyArrowCount = config.decoyArrowCount;
            _genMinLength = config.minBlockLength;
            _genMaxLength = config.maxBlockLength;

            Debug.Log($"[LevelEditor] Auto calculated: lanes={_genLaneCount}, balloons={_genBalloonsPerLane}, miss={_genMissArrowCount}, decoy={_genDecoyArrowCount}, len={_genMinLength}-{_genMaxLength}");
        }

        private void GenerateNewLevel()
        {
            // Auto 계산이 켜져 있으면 먼저 적용
            if (_genAutoCalculate)
            {
                ApplyAutoCalculate();
            }

            // 설정 생성 (직사각형 지원: gridWidth x gridHeight)
            // 색상 목록 생성
            List<string> selectedColors = null;
            if (_genUseSpecificColors)
            {
                selectedColors = new List<string>();
                for (int i = 0; i < COLOR_CODES.Length; i++)
                {
                    if (_genColorEnabled[i])
                    {
                        selectedColors.Add(COLOR_CODES[i]);
                    }
                }
                // 최소 2개 색상 필요
                if (selectedColors.Count < 2)
                {
                    EditorUtility.DisplayDialog("Error", "최소 2개 이상의 색상을 선택해야 합니다.", "OK");
                    return;
                }
            }

            var config = new LevelGenerator.GeneratorConfig
            {
                gridSize = 0,  // gridWidth/gridHeight 사용
                gridWidth = _genGridWidth,
                gridHeight = _genGridHeight,
                targetDensity = _genTargetDensity,
                bendingEnabled = _genBendingEnabled,
                fillerEnabled = _genFillerEnabled,
                laneCount = _genLaneCount,
                balloonsPerLane = _genBalloonsPerLane,
                missArrowCount = _genMissArrowCount,
                decoyArrowCount = _genDecoyArrowCount,
                minBlockLength = _genMinLength,
                maxBlockLength = _genMaxLength,
                bendingChance = _genBendingEnabled ? _genBendingChance : 0f,
                branchingMode = _genBranchingMode,
                branchingChance = _genBranchingChance,
                colorCount = _genColorCount,
                availableColors = selectedColors,
                // 기믹 설정 적용
                balloonGimmicks = new List<GimmickGeneratorConfig>(_genBalloonGimmicks),
                arrowGimmicks = new List<GimmickGeneratorConfig>(_genArrowGimmicks)
            };

            try
            {
                // 레벨 생성 (Progress Bar 표시)
                int maxAttempts = 800;
                bool wasCancelled = false;

                var generatedLevel = LevelGenerator.GenerateLevel(config, maxAttempts, (current, max) =>
                {
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "Level Generate",
                        $"시도 중... ({current}/{max})",
                        (float)current / max);

                    if (cancel)
                    {
                        wasCancelled = true;
                    }
                    return cancel;
                });

                EditorUtility.ClearProgressBar();

                if (wasCancelled)
                {
                    Debug.Log("[LevelEditor] Generation cancelled by user");
                    return;
                }

                if (generatedLevel != null)
                {
                    // 생성된 레벨에 실제 width/height 적용
                    generatedLevel.gridWidth = _genGridWidth;
                    generatedLevel.gridHeight = _genGridHeight;
                    generatedLevel.gridSize = 0;  // width/height 사용 표시

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

                    _gridWidth = generatedLevel.GetGridWidth();
                    _gridHeight = generatedLevel.GetGridHeight();
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
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Generation error: {e.Message}", "OK");
                Debug.LogError($"[LevelEditor] Generation error: {e}");
            }
        }

        // ========== Batch Generation ==========
        private void BatchGenerate(int fromLevel, int toLevel)
        {
            if (_batchConfigTable == null)
            {
                EditorUtility.DisplayDialog("Error", "Config Table이 설정되지 않았습니다.", "OK");
                return;
            }

            // JSON 로드
            List<LevelConfigRecord> allConfigs;
            try
            {
                allConfigs = LevelConfigTableLoader.Load(_batchConfigTable.text);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Config Table 파싱 실패: {e.Message}", "OK");
                return;
            }

            if (allConfigs == null || allConfigs.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Config Table이 비어있습니다.", "OK");
                return;
            }

            // 범위 내 레벨 필터링
            var targetConfigs = allConfigs
                .Where(c => c.level >= fromLevel && c.level <= toLevel)
                .OrderBy(c => c.level)
                .ToList();

            if (targetConfigs.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", $"Level {fromLevel}~{toLevel} 범위에 해당하는 Config가 없습니다.", "OK");
                return;
            }

            string stagesPath = "Assets/Resources/ScriptableObjects/Stages";
            if (!System.IO.Directory.Exists(stagesPath))
            {
                System.IO.Directory.CreateDirectory(stagesPath);
            }

            // 결과 추적
            int successCount = 0;
            int skipCount = 0;
            var relaxedLevels = new List<string>();
            var failedLevels = new List<string>();

            for (int i = 0; i < targetConfigs.Count; i++)
            {
                var configRecord = targetConfigs[i];
                int level = configRecord.level;
                string levelName = level.ToString("D6");
                string fileName = $"stage_{levelName}.asset";
                string assetPath = $"{stagesPath}/{fileName}";

                // 기존 파일 존재 확인 (Progress Bar 표시 전에 체크)
                if (!_batchOverwrite && System.IO.File.Exists(assetPath))
                {
                    skipCount++;
                    Debug.Log($"[BatchGenerate] Level {level}: 이미 존재 (건너뜀)");
                    continue;
                }

                // 생성 시도 (완화 포함) - Progress Bar 콜백 전달
                const int totalSteps = 4;
                bool wasCancelled = false;

                var result = TryGenerateWithRelaxation(configRecord, (attempt, maxAttempt, step, stepDesc) =>
                {
                    // 전체 진행률 계산
                    int attemptInStep = step * GENERATION_ATTEMPTS_PER_STEP + attempt;
                    int totalAttempts = totalSteps * GENERATION_ATTEMPTS_PER_STEP;
                    float levelProgress = (float)attemptInStep / totalAttempts;
                    float overallProgress = ((float)i + levelProgress) / targetConfigs.Count;

                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "Batch Generate",
                        $"Level {level} ({i + 1}/{targetConfigs.Count}) - ({attempt}/{GENERATION_ATTEMPTS_PER_STEP}) Step {step}: {stepDesc}",
                        overallProgress);

                    if (cancel) wasCancelled = true;
                    return cancel;
                });

                if (wasCancelled)
                {
                    Debug.Log("[BatchGenerate] 사용자에 의해 중단됨");
                    break;
                }

                if (result.levelData != null)
                {
                    // 이름 설정
                    result.levelData.name = levelName;

                    // ScriptableObject 저장
                    var stageData = ScriptableObject.CreateInstance<StageData>();
                    stageData.CopyFrom(result.levelData);

                    if (System.IO.File.Exists(assetPath))
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                    AssetDatabase.CreateAsset(stageData, assetPath);

                    successCount++;

                    if (result.relaxationStep > 0)
                    {
                        relaxedLevels.Add($"  Level {level}: {result.relaxationDesc}");
                    }

                    Debug.Log($"[BatchGenerate] Level {level}: 성공 (density={result.levelData.stats?.density ?? 0:P0}{(result.relaxationStep > 0 ? $", {result.relaxationDesc}" : "")})");
                }
                else
                {
                    failedLevels.Add($"  Level {level}: 800회 시도 후 실패");
                    Debug.LogWarning($"[BatchGenerate] Level {level}: 실패 (800회 시도)");
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // StageTable.json 업데이트
            UpdateStageTable(targetConfigs, failedLevels);

            // 리포트
            RefreshLevelList();
            ShowBatchReport(targetConfigs.Count, successCount, skipCount, relaxedLevels, failedLevels);
        }

        private struct GenerationResult
        {
            public LevelData levelData;
            public int relaxationStep;
            public string relaxationDesc;
        }

        /// <summary>
        /// Progress callback: (currentAttempt, maxAttempts, step, stepDesc) -> cancel
        /// </summary>
        private GenerationResult TryGenerateWithRelaxation(
            LevelConfigRecord configRecord,
            System.Func<int, int, int, string, bool> onProgress = null)
        {
            bool cancelled = false;

            // Step 0: 원본 Config
            var config = configRecord.ToGeneratorConfig();
            var level = LevelGenerator.GenerateLevel(config, GENERATION_ATTEMPTS_PER_STEP, (current, max) =>
            {
                if (onProgress != null && onProgress(current, max, 0, "원본"))
                {
                    cancelled = true;
                    return true;
                }
                return false;
            });
            if (cancelled) return new GenerationResult { levelData = null, relaxationStep = -1, relaxationDesc = "cancelled" };
            if (level != null)
            {
                return new GenerationResult { levelData = level, relaxationStep = 0, relaxationDesc = "" };
            }

            // Step 1: gridSize +1
            var relaxed1 = configRecord.ToGeneratorConfig();
            relaxed1.gridSize += 1;
            level = LevelGenerator.GenerateLevel(relaxed1, GENERATION_ATTEMPTS_PER_STEP, (current, max) =>
            {
                if (onProgress != null && onProgress(current, max, 1, "gridSize+1"))
                {
                    cancelled = true;
                    return true;
                }
                return false;
            });
            if (cancelled) return new GenerationResult { levelData = null, relaxationStep = -1, relaxationDesc = "cancelled" };
            if (level != null)
            {
                return new GenerationResult
                {
                    levelData = level,
                    relaxationStep = 1,
                    relaxationDesc = $"gridSize {configRecord.gridSize}→{relaxed1.gridSize}"
                };
            }

            // Step 2: missArrowCount -1
            var relaxed2 = configRecord.ToGeneratorConfig();
            relaxed2.missArrowCount = Mathf.Max(0, relaxed2.missArrowCount - 1);
            level = LevelGenerator.GenerateLevel(relaxed2, GENERATION_ATTEMPTS_PER_STEP, (current, max) =>
            {
                if (onProgress != null && onProgress(current, max, 2, "missArrow-1"))
                {
                    cancelled = true;
                    return true;
                }
                return false;
            });
            if (cancelled) return new GenerationResult { levelData = null, relaxationStep = -1, relaxationDesc = "cancelled" };
            if (level != null)
            {
                return new GenerationResult
                {
                    levelData = level,
                    relaxationStep = 2,
                    relaxationDesc = $"missArrow {configRecord.missArrowCount}→{relaxed2.missArrowCount}"
                };
            }

            // Step 3: targetDensity -0.05
            var relaxed3 = configRecord.ToGeneratorConfig();
            relaxed3.targetDensity -= 0.05f;
            level = LevelGenerator.GenerateLevel(relaxed3, GENERATION_ATTEMPTS_PER_STEP, (current, max) =>
            {
                if (onProgress != null && onProgress(current, max, 3, "density-5%"))
                {
                    cancelled = true;
                    return true;
                }
                return false;
            });
            if (cancelled) return new GenerationResult { levelData = null, relaxationStep = -1, relaxationDesc = "cancelled" };
            if (level != null)
            {
                return new GenerationResult
                {
                    levelData = level,
                    relaxationStep = 3,
                    relaxationDesc = $"density {configRecord.targetDensity:F2}→{relaxed3.targetDensity:F2}"
                };
            }

            // 모두 실패
            return new GenerationResult { levelData = null, relaxationStep = -1, relaxationDesc = "all failed" };
        }

        private void UpdateStageTable(List<LevelConfigRecord> configs, List<string> failedLevels)
        {
            string tablePath = "Assets/Resources/Tables/StageTable.json";

            // 기존 StageTable 로드
            List<StageTableEntry> entries = new List<StageTableEntry>();
            if (System.IO.File.Exists(tablePath))
            {
                string existingJson = System.IO.File.ReadAllText(tablePath);
                try
                {
                    string wrapped = "{\"entries\":" + existingJson + "}";
                    var wrapper = JsonUtility.FromJson<StageTableWrapper>(wrapped);
                    if (wrapper?.entries != null)
                    {
                        entries = wrapper.entries;
                    }
                }
                catch
                {
                    Debug.LogWarning("[BatchGenerate] 기존 StageTable.json 파싱 실패. 새로 생성합니다.");
                }
            }

            // 실패한 레벨 번호 수집
            var failedLevelNums = new HashSet<int>();
            foreach (var f in failedLevels)
            {
                var parts = f.Trim().Split(':');
                if (parts.Length > 0)
                {
                    string numStr = parts[0].Replace("Level", "").Trim();
                    if (int.TryParse(numStr, out int num))
                    {
                        failedLevelNums.Add(num);
                    }
                }
            }

            // Config 항목 업데이트/추가
            foreach (var config in configs)
            {
                if (failedLevelNums.Contains(config.level)) continue;

                string difficulty = config.difficultyScore <= 33 ? "Normal" :
                                    config.difficultyScore <= 66 ? "Hard" : "Nightmare";

                var existing = entries.Find(e => e.LevelIdx == config.level);
                if (existing != null)
                {
                    existing.StageIdx = config.level;
                    existing.Difficulty = difficulty;
                }
                else
                {
                    entries.Add(new StageTableEntry
                    {
                        LevelIdx = config.level,
                        StageIdx = config.level,
                        Difficulty = difficulty
                    });
                }
            }

            // 정렬 후 저장
            entries.Sort((a, b) => a.LevelIdx.CompareTo(b.LevelIdx));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.Append($"\t{{\"LevelIdx\":{e.LevelIdx},\"StageIdx\":{e.StageIdx},\"Difficulty\":\"{e.Difficulty}\"}}");
                if (i < entries.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.Append("]");

            System.IO.File.WriteAllText(tablePath, sb.ToString());
            Debug.Log($"[BatchGenerate] StageTable.json 업데이트 완료 ({entries.Count}개 항목)");
        }

        [System.Serializable]
        private class StageTableEntry
        {
            public int LevelIdx;
            public int StageIdx;
            public string Difficulty;
        }

        [System.Serializable]
        private class StageTableWrapper
        {
            public List<StageTableEntry> entries;
        }

        private void ShowBatchReport(int total, int success, int skipped,
            List<string> relaxed, List<string> failed)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Batch Generation Report ===");
            sb.AppendLine($"Total: {total} levels");
            sb.AppendLine($"Success: {success}" + (relaxed.Count > 0 ? $" ({relaxed.Count} relaxed)" : ""));
            if (skipped > 0) sb.AppendLine($"Skipped: {skipped} (already exist)");
            sb.AppendLine($"Failed: {failed.Count}");

            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Failed ---");
                foreach (var f in failed) sb.AppendLine(f);
            }

            if (relaxed.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- Relaxed ---");
                foreach (var r in relaxed) sb.AppendLine(r);
            }

            string report = sb.ToString();
            Debug.Log(report);

            string summary = $"성공: {success}개" +
                             (relaxed.Count > 0 ? $" (완화: {relaxed.Count})" : "") +
                             (skipped > 0 ? $"\n건너뜀: {skipped}개" : "") +
                             (failed.Count > 0 ? $"\n실패: {failed.Count}개" : "") +
                             "\n\n상세 내용은 Console 로그를 확인하세요.";

            EditorUtility.DisplayDialog("Batch Generation Complete", summary, "OK");
        }

        // ========== Level List 패널 ==========
        private void DrawLevelListPanel()
        {
            EditorGUILayout.LabelField("Level List", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            // 버튼 영역
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(22)))
            {
                RefreshLevelListEntries();
            }
            if (GUILayout.Button("Validate All", GUILayout.Height(22)))
            {
                ValidateAllLevels();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // 스크롤 목록
            _levelListScrollPosition = EditorGUILayout.BeginScrollView(_levelListScrollPosition);

            for (int i = 0; i < _levelListEntries.Count; i++)
            {
                var entry = _levelListEntries[i];
                bool isSelected = (i == _selectedLevelListIndex);

                // 선택 하이라이트
                if (isSelected)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.selectionRect);
                }
                else
                {
                    EditorGUILayout.BeginVertical("box");
                }

                // 클릭 가능한 레벨 이름
                if (GUILayout.Button(entry.assetName, EditorStyles.miniLabel))
                {
                    _selectedLevelListIndex = i;
                    LoadLevel(entry.assetName);
                }

                // 검증 상태 표시
                if (entry.isValidated)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Solvable 상태
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = entry.isSolvable ? Color.green : Color.red;
                    EditorGUILayout.LabelField(
                        entry.isSolvable ? "  S" : "  S",
                        entry.isSolvable ? EditorStyles.boldLabel : EditorStyles.miniLabel,
                        GUILayout.Width(30));

                    // Queue 상태
                    GUI.contentColor = entry.isQueueCleared ? Color.green : Color.red;
                    EditorGUILayout.LabelField(
                        entry.isQueueCleared ? "  Q" : "  Q",
                        entry.isQueueCleared ? EditorStyles.boldLabel : EditorStyles.miniLabel,
                        GUILayout.Width(30));

                    GUI.contentColor = prevColor;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = Color.gray;
                    EditorGUILayout.LabelField("  ? Not validated", EditorStyles.miniLabel);
                    GUI.contentColor = prevColor;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            // 요약 통계
            EditorGUILayout.Space(5);
            int totalCount = _levelListEntries.Count;
            int validatedCount = _levelListEntries.Count(e => e.isValidated);
            int solvableCount = _levelListEntries.Count(e => e.isValidated && e.isSolvable);
            int queueClearCount = _levelListEntries.Count(e => e.isValidated && e.isQueueCleared);

            EditorGUILayout.LabelField($"Total: {totalCount} levels", EditorStyles.miniLabel);
            if (validatedCount > 0)
            {
                EditorGUILayout.LabelField($"Solvable: {solvableCount}/{validatedCount}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Queue Clear: {queueClearCount}/{validatedCount}", EditorStyles.miniLabel);
            }
        }

        private void RefreshLevelListEntries()
        {
            RefreshLevelList();
            _levelListEntries.Clear();

            foreach (var levelName in _levelList)
            {
                _levelListEntries.Add(new LevelListEntry
                {
                    assetName = levelName,
                    isSolvable = false,
                    isQueueCleared = false,
                    isValidated = false
                });
            }

            _selectedLevelListIndex = -1;
        }

        private void ValidateAllLevels()
        {
            if (_levelListEntries.Count == 0)
            {
                RefreshLevelListEntries();
            }

            string stagesPath = "Assets/Resources/ScriptableObjects/Stages";

            for (int i = 0; i < _levelListEntries.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Validating Levels...",
                    $"{_levelListEntries[i].assetName} ({i + 1}/{_levelListEntries.Count})",
                    (float)i / _levelListEntries.Count);

                string assetPath = $"{stagesPath}/{_levelListEntries[i].assetName}.asset";
                var stageData = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);

                if (stageData != null)
                {
                    var levelData = stageData.ToLevelData();
                    var result = LevelValidator.ValidateLevel(levelData);

                    _levelListEntries[i].isSolvable = result.valid;
                    _levelListEntries[i].isQueueCleared = result.queueCleared;
                    _levelListEntries[i].isValidated = true;
                }
            }

            EditorUtility.ClearProgressBar();
            Repaint();
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

            int gridWidth = _currentLevel.GetGridWidth();
            int gridHeight = _currentLevel.GetGridHeight();
            float totalWidth = gridWidth * PREVIEW_CELL_SIZE;
            float totalHeight = gridHeight * PREVIEW_CELL_SIZE;

            EditorGUILayout.LabelField($"Grid ({gridWidth}x{gridHeight}) - Drag to draw, Ctrl+Click direction, Right-click delete", EditorStyles.boldLabel);

            // 그리드 영역 확보
            Rect gridRect = GUILayoutUtility.GetRect(totalWidth + 20, totalHeight + 20);
            gridRect.x += 10;
            gridRect.y += 5;

            // 클릭 이벤트 처리 (그리드 렌더링 전에 처리)
            HandlePreviewGridInput(gridRect, gridWidth, gridHeight);

            // 점유된 셀 계산
            var occupiedCells = new Dictionary<Vector2Int, (Color color, bool isHead, string dir)>();

            if (_currentLevel.arrows != null)
            {
                for (int arrowIdx = 0; arrowIdx < _currentLevel.arrows.Count; arrowIdx++)
                {
                    var arrow = _currentLevel.arrows[arrowIdx];
                    var cells = arrow.GetCells();
                    Color arrowColor = GetPreviewColor(arrow.color);

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
            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = new Vector3(gridRect.x + x * PREVIEW_CELL_SIZE, gridRect.y, 0);
                Vector3 end = new Vector3(gridRect.x + x * PREVIEW_CELL_SIZE, gridRect.y + gridHeight * PREVIEW_CELL_SIZE, 0);
                Handles.DrawLine(start, end);
            }

            // 수평선
            for (int y = 0; y <= gridHeight; y++)
            {
                Vector3 start = new Vector3(gridRect.x, gridRect.y + y * PREVIEW_CELL_SIZE, 0);
                Vector3 end = new Vector3(gridRect.x + gridWidth * PREVIEW_CELL_SIZE, gridRect.y + y * PREVIEW_CELL_SIZE, 0);
                Handles.DrawLine(start, end);
            }

            // 빈 셀만 그리기 (화살표는 별도로 그림)
            // Y좌표 반전: 인게임에서는 Y=0이 아래, 에디터 GUI에서는 Y=0이 위
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // 점유되지 않은 빈 셀만 표시
                    if (!occupiedCells.ContainsKey(pos))
                    {
                        // Y좌표 반전하여 그리기
                        int flippedY = gridHeight - 1 - y;
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

            // 선택된 화살표 셀 하이라이트 (반투명 색상으로 배경 채우기)
            if (_currentLevel.arrows != null && _selectedArrowIndex >= 0 && _selectedArrowIndex < _currentLevel.arrows.Count)
            {
                var selectedArrow = _currentLevel.arrows[_selectedArrowIndex];
                var selectedCells = selectedArrow.GetCells();
                Color highlightColor = GetPreviewColor(selectedArrow.color);
                highlightColor.a = 0.4f; // 반투명

                foreach (var cell in selectedCells)
                {
                    int flippedY = gridHeight - 1 - cell.y;
                    Rect cellRect = new Rect(
                        gridRect.x + cell.x * PREVIEW_CELL_SIZE,
                        gridRect.y + flippedY * PREVIEW_CELL_SIZE,
                        PREVIEW_CELL_SIZE,
                        PREVIEW_CELL_SIZE
                    );
                    EditorGUI.DrawRect(cellRect, highlightColor);
                }
            }

            // 화살표 그리기 (직선+원+삼각형 형태)
            if (_currentLevel.arrows != null)
            {
                for (int arrowIdx = 0; arrowIdx < _currentLevel.arrows.Count; arrowIdx++)
                {
                    var arrow = _currentLevel.arrows[arrowIdx];
                    Color arrowColor = GetPreviewColor(arrow.color);

                    DrawArrowInPreview(gridRect, arrow, arrowColor, gridWidth, gridHeight);
                }
            }

            // 드래그 중인 경로 미리보기
            if (_isDragging && _dragPath.Count > 0)
            {
                DrawDragPreview(gridRect, gridWidth, gridHeight);
            }
        }

        /// <summary>
        /// 드래그 중인 화살표 경로 미리보기 렌더링
        /// </summary>
        private void DrawDragPreview(Rect gridRect, int gridWidth, int gridHeight)
        {
            Color previewColor = GetPreviewColor(ColorHelper.ToString(_selectedColor));
            previewColor.a = 0.5f;

            // 경로 셀 하이라이트
            foreach (var cell in _dragPath)
            {
                int flippedY = gridHeight - 1 - cell.y;
                Rect cellRect = new Rect(
                    gridRect.x + cell.x * PREVIEW_CELL_SIZE + 1,
                    gridRect.y + flippedY * PREVIEW_CELL_SIZE + 1,
                    PREVIEW_CELL_SIZE - 2,
                    PREVIEW_CELL_SIZE - 2);
                EditorGUI.DrawRect(cellRect, previewColor);
            }

            // 경로 라인 연결
            Handles.color = previewColor;
            List<Vector3> positions = new List<Vector3>();
            foreach (var cell in _dragPath)
            {
                int flippedY = gridHeight - 1 - cell.y;
                float cx = gridRect.x + cell.x * PREVIEW_CELL_SIZE + PREVIEW_CELL_SIZE * 0.5f;
                float cy = gridRect.y + flippedY * PREVIEW_CELL_SIZE + PREVIEW_CELL_SIZE * 0.5f;
                positions.Add(new Vector3(cx, cy, 0));
            }

            for (int i = 0; i < positions.Count - 1; i++)
            {
                Handles.DrawAAPolyLine(3f, positions[i], positions[i + 1]);
            }

            // Tail 표시 (동그라미)
            if (positions.Count > 0)
                Handles.DrawSolidDisc(positions[0], Vector3.forward, 5f);

            // Head 방향 표시 (삼각형) - 2셀 이상일 때
            if (positions.Count >= 2)
            {
                var lastCell = _dragPath[_dragPath.Count - 1];
                var prevCell = _dragPath[_dragPath.Count - 2];
                string headDir = GetDirectionFromCells(prevCell, lastCell);
                DrawArrowHeadTriangle(positions[positions.Count - 1], headDir, previewColor);
            }
        }

        private void DrawArrowInPreview(Rect gridRect, ArrowData arrow, Color arrowColor, int gridWidth, int gridHeight)
        {
            var cells = arrow.GetCells();
            if (cells.Count == 0) return;

            // 셀 좌표를 화면 좌표로 변환 (Y좌표 반전)
            List<Vector3> screenPositions = new List<Vector3>();
            foreach (var cell in cells)
            {
                // Y좌표 반전: 인게임에서는 Y=0이 아래, 에디터 GUI에서는 Y=0이 위
                int flippedY = gridHeight - 1 - cell.y;
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

        /// <summary>
        /// Preview 그리드에서 클릭/드래그 이벤트 처리
        /// </summary>
        private void HandlePreviewGridInput(Rect gridRect, int gridWidth, int gridHeight)
        {
            Event e = Event.current;

            // 그리드 영역
            Rect clickableArea = new Rect(gridRect.x, gridRect.y, gridWidth * PREVIEW_CELL_SIZE, gridHeight * PREVIEW_CELL_SIZE);

            // 좌클릭 Down: 드래그 시작 또는 Ctrl+클릭
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (!clickableArea.Contains(e.mousePosition))
                    return;

                Vector2Int gridPos = ScreenToGridPosition(e.mousePosition, gridRect, gridWidth, gridHeight);
                if (!IsValidGridPosition(gridPos, gridWidth, gridHeight))
                    return;

                // Ctrl+클릭: 기존 화살표 Head 방향 변경
                if (e.control)
                {
                    HandleCtrlClick(gridPos);
                    e.Use();
                    Repaint();
                    return;
                }

                // 기존 화살표가 있는 셀: 선택
                var existingArrow = FindArrowAt(gridPos);
                if (existingArrow != null)
                {
                    _selectedArrowIndex = _currentLevel.arrows.IndexOf(existingArrow);
                    Debug.Log($"[LevelEditor] Arrow selected at ({gridPos.x}, {gridPos.y})");
                }
                else
                {
                    // 빈 셀: 드래그 시작
                    _isDragging = true;
                    _dragStartPos = gridPos;
                    _dragPath.Clear();
                    _dragPath.Add(gridPos);
                    _dragGridRect = gridRect;
                    _selectedArrowIndex = -1;
                }

                e.Use();
                Repaint();
            }
            // 드래그 중: 경로 추가
            else if (e.type == EventType.MouseDrag && e.button == 0 && _isDragging)
            {
                int dragGridWidth = _currentLevel.GetGridWidth();
                int dragGridHeight = _currentLevel.GetGridHeight();
                Vector2Int gridPos = ScreenToGridPosition(e.mousePosition, _dragGridRect, dragGridWidth, dragGridHeight);
                if (!IsValidGridPosition(gridPos, dragGridWidth, dragGridHeight))
                    return;

                Vector2Int lastPos = _dragPath[_dragPath.Count - 1];
                if (gridPos == lastPos)
                    return; // 같은 셀이면 무시

                if (!IsAdjacentCell(lastPos, gridPos))
                    return; // 인접하지 않으면 무시

                // 되돌아가기: 이미 경로에 있는 셀이면 그 위치까지 자르기
                int existingIndex = _dragPath.IndexOf(gridPos);
                if (existingIndex >= 0)
                {
                    // 해당 위치 다음의 모든 셀 제거 (backtrack)
                    _dragPath.RemoveRange(existingIndex + 1, _dragPath.Count - existingIndex - 1);
                }
                else
                {
                    // 다른 화살표와 겹치는지 확인
                    var occupied = FindArrowAt(gridPos);
                    if (occupied != null)
                        return; // 다른 화살표 셀에는 진입 불가

                    _dragPath.Add(gridPos);
                }

                e.Use();
                Repaint();
            }
            // 마우스 Up: 화살표 생성
            else if (e.type == EventType.MouseUp && e.button == 0 && _isDragging)
            {
                _isDragging = false;

                if (_dragPath.Count > 0)
                {
                    CreateArrowFromDragPath();
                }

                _dragPath.Clear();
                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }
            // 우클릭: 삭제 (기존 유지)
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                if (!clickableArea.Contains(e.mousePosition))
                    return;

                Vector2Int gridPos = ScreenToGridPosition(e.mousePosition, gridRect, gridWidth, gridHeight);

                if (IsValidGridPosition(gridPos, gridWidth, gridHeight))
                {
                    RemoveArrowAt(gridPos);
                    e.Use();
                    Repaint();
                }
            }
        }

        /// <summary>
        /// Ctrl+클릭: 화살표 Head 방향 순환 변경 (R→D→L→U)
        /// </summary>
        private void HandleCtrlClick(Vector2Int gridPos)
        {
            var arrow = FindArrowAt(gridPos);
            if (arrow == null) return;

            _selectedArrowIndex = _currentLevel.arrows.IndexOf(arrow);

            // 방향 순환: R → D → L → U → R
            arrow.direction = arrow.direction switch
            {
                "R" => "D",
                "D" => "L",
                "L" => "U",
                "U" => "R",
                _ => "R"
            };

            _validationDirty = true;
            Debug.Log($"[LevelEditor] Arrow direction changed to {arrow.direction} at ({gridPos.x}, {gridPos.y})");
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 두 셀이 상하좌우 인접한지 확인
        /// </summary>
        private bool IsAdjacentCell(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) == 1;
        }

        /// <summary>
        /// 두 셀 간의 방향 문자열 반환
        /// </summary>
        private string GetDirectionFromCells(Vector2Int from, Vector2Int to)
        {
            Vector2Int diff = to - from;
            if (diff.x > 0) return "R";
            if (diff.x < 0) return "L";
            if (diff.y > 0) return "U";
            if (diff.y < 0) return "D";
            return "R";
        }

        /// <summary>
        /// 드래그 경로로부터 화살표 생성
        /// </summary>
        private void CreateArrowFromDragPath()
        {
            if (_dragPath.Count == 0) return;
            if (_currentLevel == null) return;

            if (_currentLevel.arrows == null)
                _currentLevel.arrows = new List<ArrowData>();

            Vector2Int headPos = _dragPath[_dragPath.Count - 1];

            // Head 방향 결정
            string headDirection;
            if (_dragPath.Count >= 2)
            {
                var prevCell = _dragPath[_dragPath.Count - 2];
                headDirection = GetDirectionFromCells(prevCell, headPos);
            }
            else
            {
                // 단일 클릭: 현재 선택된 방향 사용
                headDirection = DirectionHelper.ToString(_selectedDirection);
            }

            var newArrow = new ArrowData
            {
                x = headPos.x,
                y = headPos.y,
                color = ColorHelper.ToString(_selectedColor),
                direction = headDirection,
                length = _dragPath.Count,
                order = _currentLevel.arrows.Count + 1,
                isFiller = false
            };

            // path 설정 (2셀 이상이면 path 저장)
            if (_dragPath.Count >= 2)
            {
                newArrow.path = new List<Vector2IntSerializable>();
                foreach (var cell in _dragPath)
                {
                    newArrow.path.Add(new Vector2IntSerializable(cell));
                }
            }

            _currentLevel.arrows.Add(newArrow);
            _selectedArrowIndex = _currentLevel.arrows.Count - 1;
            _validationDirty = true;

            Debug.Log($"[LevelEditor] Arrow drawn: {_dragPath.Count} cells, head=({headPos.x},{headPos.y}), dir={headDirection}");
        }

        /// <summary>
        /// 화면 좌표를 그리드 좌표로 변환
        /// </summary>
        private Vector2Int ScreenToGridPosition(Vector2 mousePos, Rect gridRect, int gridWidth, int gridHeight)
        {
            int cellX = Mathf.FloorToInt((mousePos.x - gridRect.x) / PREVIEW_CELL_SIZE);
            // Y좌표 반전: 에디터 GUI에서는 Y=0이 위, 인게임에서는 Y=0이 아래
            int screenY = Mathf.FloorToInt((mousePos.y - gridRect.y) / PREVIEW_CELL_SIZE);
            int cellY = gridHeight - 1 - screenY;

            return new Vector2Int(cellX, cellY);
        }

        /// <summary>
        /// 그리드 좌표 유효성 검사
        /// </summary>
        private bool IsValidGridPosition(Vector2Int pos, int gridWidth, int gridHeight)
        {
            return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
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
                    EditorGUILayout.LabelField($"Solution Order: ({_cachedValidation.escapeColors.Count})", EditorStyles.boldLabel);

                    GUIStyle orderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 10,
                        normal = { textColor = Color.white }
                    };

                    // 한 줄에 표시할 아이템 수 (박스 24px + 화살표 12px + 여유)
                    int itemsPerRow = 10;
                    int totalCount = _cachedValidation.escapeColors.Count;

                    for (int rowStart = 0; rowStart < totalCount; rowStart += itemsPerRow)
                    {
                        EditorGUILayout.BeginHorizontal();

                        int rowEnd = Mathf.Min(rowStart + itemsPerRow, totalCount);
                        for (int i = rowStart; i < rowEnd; i++)
                        {
                            string colorCode = _cachedValidation.escapeColors[i];
                            Color arrowColor = GetPreviewColor(colorCode);

                            // 현재 선택된 화살표인지 확인
                            bool isSelected = false;
                            int arrowIdx = -1;
                            if (_cachedValidation.escapeSequence != null && i < _cachedValidation.escapeSequence.Count)
                            {
                                arrowIdx = _cachedValidation.escapeSequence[i];
                                isSelected = (arrowIdx == _selectedArrowIndex);
                            }

                            // 순서 번호 + 색상 박스
                            Rect rect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));

                            // 선택된 경우 흰색 테두리 먼저 그리기
                            if (isSelected)
                            {
                                Rect borderRect = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                                EditorGUI.DrawRect(borderRect, Color.white);
                            }

                            EditorGUI.DrawRect(rect, arrowColor);
                            GUI.Label(rect, (i + 1).ToString(), orderStyle);

                            // 클릭 감지 - 해당 화살표 선택
                            if (Event.current.type == EventType.MouseDown &&
                                Event.current.button == 0 &&
                                rect.Contains(Event.current.mousePosition))
                            {
                                if (arrowIdx >= 0)
                                {
                                    _selectedArrowIndex = arrowIdx;
                                    GUI.changed = true;
                                    Repaint();
                                }
                                Event.current.Use();
                            }

                            // 화살표 표시 (줄 끝과 전체 마지막 제외)
                            if (i < rowEnd - 1)
                            {
                                GUILayout.Label("→", GUILayout.Width(12));
                            }
                        }

                        // 줄 끝에 다음 줄로 연결되는 화살표 표시
                        if (rowEnd < totalCount)
                        {
                            GUILayout.Label("→", GUILayout.Width(12));
                        }

                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
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
                int gridWidth = _currentLevel.GetGridWidth();
                int gridHeight = _currentLevel.GetGridHeight();
                int totalCells = gridWidth * gridHeight;
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
                "P" => new Color(0.35f, 0f, 1f),      // 진짜 보라
                "O" => new Color(1f, 0.5f, 0.1f),    // 더 진한 주황
                "C" => new Color(0.2f, 0.9f, 0.9f),
                "K" => new Color(1f, 0.5f, 0.7f),
                "W" => new Color(0.6f, 0.4f, 0.2f),  // Brown
                "N" => new Color(0.2f, 0.3f, 0.6f),  // Navy
                "M" => new Color(1f, 0.3f, 0.8f),    // Magenta
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
        }

        // ========== 레벨 관리 ==========
        private void CreateNewLevel()
        {
            _currentLevel = LevelSaver.CreateNew(_levelName, _gridWidth, _gridHeight);
            _selectedArrowIndex = -1;
            _validationDirty = true; // 검증 갱신 필요
            Debug.Log($"[LevelEditor] Created new level: {_levelName}");
            SceneView.RepaintAll();
        }

        private void LoadLevel(string levelName)
        {
            // ScriptableObject에서 로드
            string assetPath = $"Assets/Resources/ScriptableObjects/Stages/{levelName}.asset";
            var stageData = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);

            if (stageData != null)
            {
                _currentLevel = stageData.ToLevelData();
                _levelName = ExtractLevelNameFromAsset(levelName);
                _gridWidth = _currentLevel.GetGridWidth();
                _gridHeight = _currentLevel.GetGridHeight();
                _selectedArrowIndex = -1;
                _validationDirty = true;

                // StageData에 저장된 Generator 설정이 있으면 우선 사용
                if (stageData.HasGeneratorConfig)
                {
                    LoadGeneratorConfigFromStageData(stageData);
                    Debug.Log($"[LevelEditor] Loaded generator config from StageData");
                }
                else
                {
                    // 폴백: LevelConfigTable에서 Params 복원
                    ApplyConfigFromTable(levelName);
                }

                Debug.Log($"[LevelEditor] Loaded stage: {levelName}");
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogError($"[LevelEditor] Failed to load stage: {assetPath}");
            }
        }

        /// <summary>
        /// StageData에서 Generator 설정을 에디터로 복원
        /// </summary>
        private void LoadGeneratorConfigFromStageData(StageData stageData)
        {
            // Generator 파라미터 복원
            _genGridWidth = stageData.gridWidth > 0 ? stageData.gridWidth : stageData.gridSize;
            _genGridHeight = stageData.gridHeight > 0 ? stageData.gridHeight : stageData.gridSize;
            _genLaneCount = stageData.genLaneCount;
            _genBalloonsPerLane = stageData.genBalloonsPerLane;
            _genMissArrowCount = stageData.genMissArrowCount;
            _genDecoyArrowCount = stageData.genDecoyArrowCount;
            _genMinLength = stageData.genMinLength;
            _genMaxLength = stageData.genMaxLength;
            _genTargetDensity = stageData.genTargetDensity;
            _genBendingEnabled = stageData.genBendingEnabled;
            _genBendingChance = stageData.genBendingChance;
            _genBranchingMode = stageData.genBranchingMode;
            _genBranchingChance = stageData.genBranchingChance;
            _genColorCount = stageData.genColorCount > 0 ? stageData.genColorCount : 6;
            _genFillerEnabled = stageData.genFillerEnabled;
            _genAutoCalculate = false; // 수동 모드로 전환

            // Gimmick 설정 복원 (딥 카피)
            _genBalloonGimmicks = new List<GimmickGeneratorConfig>();
            if (stageData.genBalloonGimmicks != null && stageData.genBalloonGimmicks.Count > 0)
            {
                foreach (var gimmick in stageData.genBalloonGimmicks)
                {
                    _genBalloonGimmicks.Add(gimmick.Clone());
                }
            }
            else
            {
                // 기본 기믹 설정 초기화
                InitializeDefaultGimmickConfigs();
            }

            _genArrowGimmicks = new List<GimmickGeneratorConfig>();
            if (stageData.genArrowGimmicks != null && stageData.genArrowGimmicks.Count > 0)
            {
                foreach (var gimmick in stageData.genArrowGimmicks)
                {
                    _genArrowGimmicks.Add(gimmick.Clone());
                }
            }

            Debug.Log($"[LevelEditor] Generator config loaded - Grid: {_genGridWidth}x{_genGridHeight}, Lanes: {_genLaneCount}, BalloonGimmicks: {_genBalloonGimmicks?.Count ?? 0}");
        }

        /// <summary>
        /// 기본 기믹 설정 초기화 (Surprise, Number, Connected)
        /// </summary>
        private void InitializeDefaultGimmickConfigs()
        {
            _genBalloonGimmicks = new List<GimmickGeneratorConfig>
            {
                new GimmickGeneratorConfig { gimmickId = "surprise", enabled = false, count = 0 },
                new GimmickGeneratorConfig { gimmickId = "number", enabled = false, hitCounts = new List<int>() },
                new GimmickGeneratorConfig { gimmickId = "connected", enabled = false, groupSizes = new List<int>() }
            };
        }

        /// <summary>
        /// 레벨 번호를 기반으로 LevelConfigTable에서 파라미터를 가져와 Generate 탭에 반영
        /// </summary>
        private void ApplyConfigFromTable(string assetName)
        {
            int levelNumber = ExtractLevelNumber(assetName);
            if (levelNumber < 0) return;

            // LevelConfigTable.json 로드
            var configTableAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Resources/Tables/LevelConfigTable.json");
            if (configTableAsset == null) return;

            var records = LevelConfigTableLoader.Load(configTableAsset.text);
            var record = records.Find(r => r.level == levelNumber);

            if (record != null)
            {
                // Config Table이 아직 width/height를 지원하지 않으므로 gridSize를 양쪽에 적용
                _genGridWidth = record.gridSize;
                _genGridHeight = record.gridSize;
                _genLaneCount = record.laneCount;
                _genBalloonsPerLane = record.balloonsPerLane;
                _genMissArrowCount = record.missArrowCount;
                _genDecoyArrowCount = record.decoyArrowCount;
                _genMinLength = record.minBlockLength;
                _genMaxLength = record.maxBlockLength;
                _genBendingEnabled = record.bendingEnabled;
                _genTargetDensity = record.targetDensity;
                _genAutoCalculate = false; // 수동 모드로 전환

                Debug.Log($"[LevelEditor] Applied config from table for level {levelNumber}");
            }
        }

        /// <summary>
        /// asset 이름에서 레벨 번호 추출: "stage_000047" → 47
        /// </summary>
        private int ExtractLevelNumber(string assetName)
        {
            if (assetName.StartsWith("stage_"))
            {
                string numPart = assetName.Substring(6);
                if (int.TryParse(numPart, out int level))
                    return level;
            }
            return -1;
        }

        /// <summary>
        /// asset 파일명에서 레벨 이름 추출 (stage_XXX -> XXX)
        /// </summary>
        private string ExtractLevelNameFromAsset(string assetName)
        {
            if (assetName.StartsWith("stage_"))
            {
                return assetName.Substring(6); // "stage_" 제거
            }
            return assetName;
        }

        private void SaveLevel()
        {
            if (_currentLevel == null)
                return;

            if (string.IsNullOrEmpty(_levelName))
            {
                EditorUtility.DisplayDialog("Error", "Level name is empty", "OK");
                return;
            }

            // stage_{Name}.asset 형식으로 저장
            string fileName = $"stage_{_levelName}.asset";

            // ScriptableObject로 저장
            bool success = SaveAsScriptableObject(_currentLevel, fileName);
            if (success)
            {
                Debug.Log($"[LevelEditor] Stage saved: {fileName}");
                EditorUtility.DisplayDialog("Save", $"Stage '{fileName}' saved successfully!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to save stage.", "OK");
            }
        }

        private bool SaveAsScriptableObject(LevelData levelData, string fileName)
        {
            try
            {
                string stagesPath = "Assets/Resources/ScriptableObjects/Stages";

                if (!System.IO.Directory.Exists(stagesPath))
                {
                    System.IO.Directory.CreateDirectory(stagesPath);
                }

                // StageData ScriptableObject 생성
                var stageData = ScriptableObject.CreateInstance<StageData>();
                stageData.CopyFrom(levelData);

                // Generator 설정 저장 (로드 시 복원용)
                SaveGeneratorConfigToStageData(stageData);

                // 파일 저장
                string assetPath = $"{stagesPath}/{fileName}";

                // 기존 파일이 있으면 삭제 (덮어쓰기)
                if (System.IO.File.Exists(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                AssetDatabase.CreateAsset(stageData, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[LevelEditor] ScriptableObject saved: {assetPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelEditor] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 현재 에디터의 Generator 설정을 StageData에 저장
        /// </summary>
        private void SaveGeneratorConfigToStageData(StageData stageData)
        {
            // Generator 파라미터 저장
            stageData.genLaneCount = _genLaneCount;
            stageData.genBalloonsPerLane = _genBalloonsPerLane;
            stageData.genMissArrowCount = _genMissArrowCount;
            stageData.genDecoyArrowCount = _genDecoyArrowCount;
            stageData.genMinLength = _genMinLength;
            stageData.genMaxLength = _genMaxLength;
            stageData.genTargetDensity = _genTargetDensity;
            stageData.genBendingEnabled = _genBendingEnabled;
            stageData.genBendingChance = _genBendingChance;
            stageData.genBranchingMode = _genBranchingMode;
            stageData.genBranchingChance = _genBranchingChance;
            stageData.genColorCount = _genColorCount;
            stageData.genFillerEnabled = _genFillerEnabled;

            // Gimmick 설정 저장 (딥 카피)
            stageData.genBalloonGimmicks = new List<GimmickGeneratorConfig>();
            if (_genBalloonGimmicks != null)
            {
                foreach (var gimmick in _genBalloonGimmicks)
                {
                    stageData.genBalloonGimmicks.Add(gimmick.Clone());
                }
            }

            stageData.genArrowGimmicks = new List<GimmickGeneratorConfig>();
            if (_genArrowGimmicks != null)
            {
                foreach (var gimmick in _genArrowGimmicks)
                {
                    stageData.genArrowGimmicks.Add(gimmick.Clone());
                }
            }

            Debug.Log($"[LevelEditor] Generator config saved - Lanes: {stageData.genLaneCount}, BalloonGimmicks: {stageData.genBalloonGimmicks?.Count ?? 0}");
        }

        private void RefreshLevelList()
        {
            _levelList = new List<string>();

            string stagesPath = "Assets/Resources/ScriptableObjects/Stages";

            if (!System.IO.Directory.Exists(stagesPath))
            {
                return;
            }

            // .asset 파일들 검색
            var assetFiles = System.IO.Directory.GetFiles(stagesPath, "*.asset");

            foreach (var filePath in assetFiles)
            {
                // 파일명만 추출 (확장자 제외)
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                _levelList.Add(fileName);
            }

            // 정렬
            _levelList.Sort();

            Debug.Log($"[LevelEditor] Found {_levelList.Count} stage assets");
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
