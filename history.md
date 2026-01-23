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

## 다음 작업 예정

- [ ] 꺾이는 화살표 수동 편집 기능
- [ ] 레벨 복사/붙여넣기
- [ ] 자동 저장 기능
- [ ] 인게임 Level Editor (빌드 후 사용 가능)