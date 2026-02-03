# Development History

## 2026-01-23 (오늘)

### 1. Level Generator 구현 (LevelGenerator.cs)
HTML 프로토타입의 레벨 생성 알고리즘을 Unity C#으로 포팅 완료.

**파일**: `Assets/Scripts/Data/LevelGenerator.cs`

**핵심 기능**:
- **ReverseGrowth 알고리즘**: 첫 번째 화살표는 즉시 탈출 가능한 위치에 배치, 이후 화살표들은 이전 화살표의 Body에 의해 막히는 위치에 배치
- **Bending Arrows**: "직진 선호 + 막히면 꺾음" 방식의 꺾이는 화살표 생성
- **Filler System**: 빈 공간을 채워 목표 밀도 달성
- **Validation**: 시뮬레이션 기반 풀이 가능성 검증
- **Branching Mode**: 화살표가 독립적으로 배치될 확률 설정

**주요 클래스**:
- `GeneratorConfig`: 생성 설정 (gridSize, targetDensity, bendingEnabled 등)
- `ValidationResult`: 검증 결과 (valid, reason, escapeSequence, escapeColors)

---

### 2. Level Editor Window 개선 (LevelEditorWindow.cs)
Unity Editor 내 레벨 편집 기능 대폭 확장.

**파일**: `Assets/Scripts/Editor/LevelEditorWindow.cs`

#### 2.1 Generate 탭 추가
- Grid Size, Target Density 설정
- Bending/Filler 토글
- Auto Calculate 기능 (그리드 크기에 따른 자동 파라미터 계산)
- Branching Mode UI 추가

#### 2.2 Preview 패널 추가
- 좌우 분할 레이아웃 (왼쪽: 도구, 오른쪽: 미리보기)
- **Balloon Queue**: 상단에 Lane별 풍선 색상 표시
- **Grid Preview**: 화살표 배치 시각화 (Head에 방향 표시)
- **Solvable Info**: 검증 결과 + Solution Order 표시
- **Statistics**: 화살표 수, 밀도 등 통계

#### 2.3 색상 가시성 개선
- `GUI.backgroundColor` → `EditorGUI.DrawRect`로 변경
- 풍선/화살표 색상이 명확하게 표시됨

#### 2.4 UX 개선
- Generate 성공 시 다이얼로그 제거 (콘솔 로그로 대체)
- Revalidate 버튼 추가

---

### 3. Test Play 기능 수정 (GameManager.cs)
Level Editor에서 생성한 레벨을 테스트 플레이할 때 항상 Test_001이 로드되던 문제 해결.

**파일**: `Assets/Scripts/Core/GameManager.cs`

**변경사항**:
```csharp
// 정적 필드 추가
private static LevelData _editorTestLevel;

// 외부에서 테스트 레벨 설정 가능
public static void SetEditorTestLevel(LevelData levelData)
{
    _editorTestLevel = levelData;
}
```

**동작 방식**:
1. LevelEditorWindow에서 Test Play 버튼 클릭
2. `GameManager.SetEditorTestLevel()`로 현재 레벨 설정
3. Play Mode 진입 시 `_editorTestLevel` 우선 로드

---

### 4. LevelGenerator 외부 검증 API 추가
Level Editor에서 검증 기능을 사용할 수 있도록 public API 추가.

**변경사항**:
- `ValidationResult` 클래스를 public으로 변경
- `escapeColors` 필드 추가 (탈출 순서의 색상 목록)
- `ValidateLevel(LevelData)` public 메서드 추가

---

### 5. Git 이슈 해결
`nul` 파일로 인한 git add 실패 문제 해결.

**원인**: Windows 예약 파일명 `nul`이 실수로 생성됨

**해결**: `.gitignore`에 `nul`, `NUL` 추가

---

## 수정된 파일 목록

| 파일 | 작업 |
|------|------|
| `Assets/Scripts/Data/LevelGenerator.cs` | 신규 생성 |
| `Assets/Scripts/Data/LevelSaver.cs` | 신규 생성 |
| `Assets/Scripts/Editor/LevelEditorWindow.cs` | 신규 생성 |
| `Assets/Scripts/Core/GameManager.cs` | 수정 (Test Play 기능) |
| `Assets/Scripts/Core/GameEnums.cs` | 수정 |
| `.gitignore` | 수정 (nul 추가) |

---

---

## 2026-01-26

### 1. CameraController 구현 (CameraController.cs)

Grid 크기에 따른 자동 줌 및 드래그/핀치 기능 구현.

**파일**: `Assets/Scripts/Core/CameraController.cs`

**핵심 기능**:
- **자동 줌 조절**: Grid Size에 맞춰 카메라 orthographicSize 자동 계산
- **드래그 이동**: 마우스/터치로 카메라 위치 이동
- **핀치 줌**: 모바일 2손가락 줌 인/아웃
- **드래그 임계값**: 탭과 드래그 구분 (dragThreshold 픽셀)
- **경계 제한**: 그리드 영역 밖으로 카메라 이동 제한

**주요 메서드**:
```csharp
// Grid 크기에 맞춰 카메라 크기 자동 조절
public void AdjustToGrid(int gridSize, float cellSize)

// 드래그 상태 확인 (화살표 터치와 구분용)
public bool HasDragged { get; }
```

**GameManager 연동**:
```csharp
// InitializeLevel에서 카메라 조절 호출
if (_cameraController != null && _gridSystem != null)
{
    _cameraController.AdjustToGrid(levelData.gridSize, _gridSystem.CellSize);
}
```

---

### 2. 화살표 입력 버그 수정 (Critical Bug Fix)

**문제**: 첫 번째 화살표 탈출 후 다른 화살표가 입력에 반응하지 않음

**원인 분석**:
- `_isProcessing` 플래그가 `true`인 상태로 유지됨
- `OnExtracted` 이벤트에서 `_isProcessing = false` 처리하도록 되어있었음
- HomingArrowSpawner가 화살표를 파괴하여 `OnExtracted` 이벤트가 발생하지 않음

**디버그 로그로 확인**:
```
[GameManager] OnArrowTapped: Arrow=9, State=Playing, IsProcessing=True, CanLaunch=True
```
→ `IsProcessing=True`가 두 번째 화살표 터치 시에도 유지되어 입력 무시됨

**수정 내용** (`GameManager.cs`):
```csharp
private void OnArrowExtractionStartedHandler(ArrowController arrow, Vector2 headPos, ArrowDirection exitDir)
{
    // 탈출 시작 시 즉시 다음 입력 허용
    // (OnExtracted 이벤트는 화살표 파괴로 인해 호출되지 않을 수 있음)
    _isProcessing = false;
    Debug.Log($"[GameManager] Arrow extraction started, _isProcessing reset to false");

    if (_homingArrowSpawner != null)
    {
        _homingArrowSpawner.HandleArrowExtractionStarted(arrow, headPos, exitDir);
    }
}
```

**핵심 포인트**:
- `OnExtractionStarted` 이벤트에서 `_isProcessing` 리셋 (기존: `OnExtracted`에서 리셋)
- HomingArrow가 화살표를 파괴해도 다음 입력이 즉시 가능

---

### 3. 디버그 로깅 추가 (ArrowController.cs)

화살표 입력 문제 추적을 위한 디버그 로깅 추가.

**추가된 로그**:
```csharp
// 활성 화살표 수 추적
private void OnEnable()
{
    _activeArrowCount++;
    Debug.Log($"[ArrowController] Arrow {_id} OnEnable, active count: {_activeArrowCount}");
}

private void OnDisable()
{
    _activeArrowCount--;
    Debug.Log($"[ArrowController] Arrow {_id} OnDisable, active count: {_activeArrowCount}");
}

// OnTapped 이벤트 구독자 수 로깅
Debug.Log($"[ArrowController] Arrow {_id} invoking OnTapped. Subscribers: {OnTapped?.GetInvocationList().Length ?? 0}");
```

---

### 4. Auto Calculate 최적화 계획 수립

Filler 이슈 해결 및 밀도 최적화를 위한 계획 작성.

**문제점**:
- Filler 배치 시 Facing 에러, Deadlock 발생
- 8x8 기본 밀도가 75%로 목표(90-95%)에 미달

**계획된 변경사항**:
- `fillerEnabled` 기본값 `false`로 변경
- 밀도 기반 화살표 개수 계산 공식 적용
- Grid Size별 최적화된 파라미터 테이블

**계획 파일**: `C:\Users\...\plans\generic-dazzling-teapot.md`

---

## 수정된 파일 목록 (2026-01-26)

| 파일 | 작업 |
|------|------|
| `Assets/Scripts/Core/CameraController.cs` | 신규 생성 |
| `Assets/Scripts/Core/GameManager.cs` | 수정 (_isProcessing 리셋 위치 변경) |
| `Assets/Scripts/Game/Arrow/ArrowController.cs` | 수정 (디버그 로깅 추가) |

---

## 다음 작업 예정

- [ ] Auto Calculate 최적화 (밀도 기반 계산)
- [ ] Filler 기본값 false로 변경
- [ ] 꺾이는 화살표 수동 편집 기능
- [ ] 레벨 복사/붙여넣기
- [ ] 자동 저장 기능
- [ ] 인게임 Level Editor (빌드 후 사용 가능)

---

## 2026-02-02

### 1. 기믹 자동 생성 시스템 (개수 기반)

기존 확률 기반 기믹 생성을 **개수 기반**으로 변경.

#### 1.1 GimmickGeneratorConfig 수정
**파일**: `Assets/Scripts/Data/GimmickGeneratorConfig.cs`

- `chance` (확률) 제거
- `count` 추가: Surprise 풍선 개수 지정
- `hitCounts` 리스트 추가: Number 풍선별 hit count 개별 지정
- `GetExtraArrowCount()`: Number 기믹의 추가 화살표 수 계산
- `CreateSurpriseInstance()`, `CreateNumberInstance()`: 기믹 인스턴스 생성 헬퍼

#### 1.2 Level Editor UI 변경
**파일**: `Assets/Scripts/Editor/LevelEditorWindow.cs`

- Surprise: Count 입력 필드
- Number: hitCounts 리스트 (Add/Remove 버튼)
- 추가 화살표 개수 실시간 표시

#### 1.3 LevelGenerator 개수 기반 적용
**파일**: `Assets/Scripts/Data/LevelGenerator.cs`

- `GenerateQueueWithBalloonData()`: BalloonData 포함 Queue 생성
- `ApplyGimmicksAndInsertExtraColors()`: 개수 기반 기믹 적용 + Number 추가 색상 삽입
- Number 풍선의 hitCount-1 만큼 동일 색상 풍선 추가 삽입 (화살표 수 맞춤)

---

### 2. Surprise 풍선 활성 위치 버그 수정

**문제**: Surprise 풍선이 맨 첫줄(활성 위치)에 생성됨

**원인**: 기믹 적용 시 모든 풍선이 대상에 포함됨

**해결**: `ApplyGimmicksAndInsertExtraColors()`에서 활성 위치(각 레인 마지막 풍선) 제외
- Surprise: 활성 위치 제외 (`nonActiveBalloons`)
- Number: 전체 풍선 대상 (활성 위치 포함 가능)
- Surprise + Number 조합 지원

---

### 3. 기믹 PRD 문서 추가

**파일**: `Assets/Documents/WSB_Gimmick_PRD.md`

기믹 시스템 설계 문서 작성:
- Surprise/Number 기믹 동작 정의
- 아키텍처 설계 (IGimmickBehavior 인터페이스)
- 개수 기반 자동 생성 UI/로직 설계
- Validation 시 기믹 고려 사항

---

## 수정된 파일 목록 (2026-02-02)

| 파일 | 작업 |
|------|------|
| `Assets/Scripts/Data/GimmickGeneratorConfig.cs` | 수정 (개수 기반) |
| `Assets/Scripts/Data/LevelGenerator.cs` | 수정 (기믹 적용 로직) |
| `Assets/Scripts/Editor/LevelEditorWindow.cs` | 수정 (기믹 UI) |
| `Assets/Documents/WSB_Gimmick_PRD.md` | 신규 생성 |

---

## 2026-02-03

### 1. 버그 수정: 화살표 겹침 및 그리드 위치 오류

**증상**:
- Level Editor에서 생성된 레벨에서 화살표들이 겹쳐 표시됨
- 인게임에서 그리드가 화면 우측 상단에 배치됨 (중앙이 아님)

**원인 분석**:
- `StageData.cs`에 `gridWidth`/`gridHeight` 필드가 없어서 직사각형 그리드 정보 손실
- LevelGenerator가 `gridSize=0, gridWidth=X, gridHeight=Y`로 생성
- StageData.CopyFrom()이 `gridSize=0`만 복사 (width/height 무시)
- 로드 시 `GetGridWidth()` → `0` 반환
- GridSystem이 0x0 그리드로 초기화 → 원점 계산 오류

**해결**:
`StageData.cs` 수정:
- `gridWidth`, `gridHeight` 필드 추가
- `CopyFrom()`: gridWidth/gridHeight 복사 추가
- `ToLevelData()`: 하위 호환성 폴백 (gridWidth/gridHeight가 0이면 gridSize 사용)

---

### 2. 버그 수정: 화살표 좌표계 변환 오류 (FlipYDirection 제거)

**증상**:
- 레벨 생성 시 화살표들이 겹쳐서 생성됨
- 일부 화살표가 그리드 외부에 생성됨

**원인 분석**:
Generator 좌표계와 Game 좌표계의 방향 변환 로직 오류:

1. **ArrowPlacer (Generator 좌표계)**: `"U"=(0,-1)`, `"D"=(0,1)` - Y=0이 상단
2. **DirectionHelper (Game 좌표계)**: `Up=(0,+1)`, `Down=(0,-1)` - Y=0이 하단
3. **LevelGenerator**: 직선 화살표에 `FlipYDirection()`을 적용해서 U↔D 변환

**문제 예시**:
- Generator: HEAD(3,1), direction="U", length=3 → 셀 (3,1), (3,2), (3,3)
- Y-flip 후: HEAD(3,5), direction="D" (잘못된 플립!)
- `ArrowData.GetCells()` 계산 시 (direction="D", dir=(0,-1)):
  - i=2: (3, 5+2) = **(3, 7)** ← 그리드 외부!

**핵심 인사이트**:
- Generator "U" = 상단 가장자리(y=-1)로 탈출
- Game "U" = 상단 가장자리(y=+max)로 탈출
- **둘 다 "상단으로 탈출"의 의미**이므로 방향을 플립하면 안 됨!

**해결**:
`LevelGenerator.cs` line 917:
```csharp
// Before (버그):
direction = (b.path != null && b.path.Count > 0) ? b.dir : FlipYDirection(b.dir),

// After (수정):
direction = b.dir,  // 방향 플립 제거 - Game 좌표계에서도 동일한 탈출 방향
```

---

## 수정된 파일 목록 (2026-02-03)

| 파일 | 작업 |
|------|------|
| `Assets/Scripts/Data/StageData.cs` | 수정 (gridWidth/gridHeight 추가) |
| `Assets/Scripts/Data/LevelGenerator.cs` | 수정 (FlipYDirection 제거) |