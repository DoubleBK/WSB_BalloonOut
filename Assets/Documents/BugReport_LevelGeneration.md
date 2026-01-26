# Bug Report: Level Generation Failure

## 1. 버그 주제

**"Failed to generate valid level after max attempts"** - 높은 밀도(95%) 설정에서 레벨 생성 실패

### 재현 조건
| 설정 | 값 |
|------|-----|
| Grid Size | 16 |
| Target Density | 95% |
| Bending Enabled | true |
| Filler Enabled | true |
| Branching Mode | true |
| Branching Chance | 0.6 |
| Lanes | 3 |
| Balloons/Lane | 6 |
| Miss Arrows | 3 |
| Min Length | 5 |
| Max Length | 13 |

### 에러 메시지
```
Failed to generate valid level after max attempts
UnityEngine.Debug:LogError (object)
BalloonOut.Data.LevelGenerator:GenerateLevel (at LevelGenerator.cs:1432)
```

---

## 2. 시스템 개요

### 2.1 좌표계
- **Generator 좌표계**: y=0이 상단, y 증가가 하단 (행렬 인덱스 방식)
- **Game 좌표계**: y=0이 하단, y 증가가 상단 (Unity 표준)
- 변환 함수: `FlipYDirection()` - U↔D 교환

### 2.2 Queue 시스템
- **Generator 내부 (LIFO)**: `lane[end]`가 활성 풍선
- **Game 출력 (FIFO)**: `lane[0]`이 활성 풍선
- 변환: `GenerateLevel()`에서 각 Lane을 `Reverse()` 후 저장

### 2.3 화살표 배치 알고리즘
1. **ReverseGrowth**: 첫 화살표는 가장자리에서 즉시 탈출 가능하도록 배치
2. **의존성 배치**: 이후 화살표는 이전 화살표의 Body에 막히는 위치에 배치
3. **Filler**: 빈 공간을 채워 목표 밀도 달성

### 2.4 Validation 흐름
```
ValidateGeneratedLevel()
├── 마주보는 화살표 검사 (CheckFacingArrows)
├── 탈출 시뮬레이션 (Two-pass approach)
│   ├── Pass 0: Main arrows only
│   └── Pass 1: Filler arrows only (풍선 활성화 확인)
├── Queue 완전 소진 확인
└── ValidationResult 반환 (valid, queueCleared, escapeSequence)
```

---

## 3. 예상됐던 버그 원인

### 3.1 원인 가설 1: Filler가 Main 화살표의 탈출 경로를 막음

```
시나리오:
1. Main Arrow A가 배치됨 (탈출 방향: Right)
2. Filler Arrow F가 A의 탈출 경로에 배치됨
3. A는 탈출 불가 (F에 막힘)
4. F도 탈출 불가 (F의 풍선이 아직 활성화되지 않음)
5. 결과: Deadlock → validation 실패
```

### 3.2 원인 가설 2: Filler 풍선 활성화 시점 문제

```
Generator 내부 Queue (LIFO):
  [FillerX, FillerY, MainA, MainB, MainC]
                                    ↑ lane[end] = 활성

Filler 풍선은 lane[0]에 Insert됨
→ Main 풍선이 모두 팝될 때까지 Filler 풍선은 활성화되지 않음
→ Filler는 탈출할 수 없음 (풍선 활성화 조건 미충족)
```

### 3.3 원인 가설 3: Two-pass 접근법의 한계

```
Pass 0 (Main only): Filler에 막힌 Main은 탈출 불가
Pass 1 (Filler only): 풍선이 활성화되지 않은 Filler는 탈출 불가

두 패스 모두 아무것도 탈출시키지 못함 → "deadlock" 반환
```

---

## 4. 시도한 해결 방안

### 4.1 해결 방안 1: Filler 풍선을 Lane 앞에 삽입

**변경 내용**:
```csharp
// 변경 전
lanes[minLaneIdx].Add(color);

// 변경 후
lanes[minLaneIdx].Insert(0, color);
```

**결과**: ❌ 실패
- Filler 풍선이 Lane 앞에 있으면 LIFO에서 가장 마지막에 활성화됨
- Main 화살표가 모두 탈출해야 Filler 풍선이 활성화
- 하지만 Filler가 Main을 막고 있으면 여전히 데드락

---

### 4.2 해결 방안 2: Two-pass Validation 도입

**변경 내용**:
```csharp
// Pass 0: Main arrows only (Skip Fillers)
// Pass 1: Filler arrows only (with balloon activation check)
for (int pass = 0; pass < 2 && !escaped; pass++)
{
    for (int i = 0; i < remaining.Count; i++)
    {
        var b = remaining[i];
        if (pass == 0 && b.isFiller) continue;
        if (pass == 1 && !b.isFiller) continue;

        // Filler: 풍선 활성화 확인
        if (b.isFiller)
        {
            bool balloonActive = /* lane[end] == b.color */;
            if (!balloonActive) continue;
        }

        // 탈출 가능 여부 확인...
    }
}
```

**결과**: ❌ 실패
- Main이 Filler에 막혀있으면 Pass 0에서 아무것도 탈출 못함
- Pass 1에서 Filler 풍선이 활성화되지 않아 탈출 못함
- 결과: "deadlock"

---

### 4.3 해결 방안 3: Filler 배치 시 Main 탈출 경로 회피

**변경 내용**:
```csharp
// Main 화살표들의 탈출 경로 계산
var escapePaths = new HashSet<string>();
foreach (var block in blocks)
{
    var head = block.cells[0];
    var path = GetEscapePath(head.x, head.y, block.dir, cfg.gridSize);
    foreach (var cell in path)
    {
        escapePaths.Add(CellKey(cell));
    }
}

// Filler 배치 시 escapePaths를 피함
PlacementResult placement = useBending
    ? PlaceFallbackBending(color, length, cfg.gridSize, occupiedSet, escapePaths)
    : PlaceFallback(color, length, cfg.gridSize, occupiedSet, escapePaths);
```

**PlaceFallback 수정**:
```csharp
private static PlacementResult PlaceFallback(..., HashSet<string> forbiddenCells = null)
{
    var candidates = new List<PlacementResult>();           // 우선순위
    var fallbackCandidates = new List<PlacementResult>();   // 후순위

    // forbiddenCells와 겹치면 fallbackCandidates에 추가
    // 겹치지 않으면 candidates에 추가

    // 우선: forbiddenCells와 안 겹치는 후보
    if (candidates.Count > 0) return RandomPick(candidates);

    // 차선: forbiddenCells와 겹치는 후보
    if (fallbackCandidates.Count > 0) return RandomPick(fallbackCandidates);

    return null;
}
```

**결과**: ❌ 여전히 실패
- 높은 밀도(95%)에서는 forbiddenCells를 피할 공간이 부족
- fallbackCandidates에서 선택되면 여전히 Main을 막을 수 있음
- 근본적으로 배치 가능한 공간 자체가 부족

---

### 4.4 해결 방안 4: Filler 배치 시 탈출 가능성 검증 (checkCanEscape)

**핵심 아이디어**:
- Filler 배치 시 "이 위치에서 실제로 탈출할 수 있는가?"를 검증
- 탈출 불가능한 위치에는 Filler를 배치하지 않음
- 배치된 Filler의 탈출 경로도 escapePaths에 추가하여 Filler-Filler 블로킹 방지

**변경 내용 1: PlaceFallback에 checkCanEscape 파라미터 추가**:
```csharp
private static PlacementResult PlaceFallback(
    string color, int length, int gridSize,
    HashSet<string> occupiedSet,
    HashSet<string> forbiddenCells = null,
    bool checkCanEscape = false)  // 추가
{
    // ... 기존 코드 ...

    foreach (var dir in dirs)
    {
        // ... 셀 계산 ...

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

        // ... 나머지 코드 ...
    }
}
```

**변경 내용 2: PlaceFallbackBending에도 동일 적용**

**변경 내용 3: PlaceFillersForDensity에서 checkCanEscape=true 전달 및 escapePaths 업데이트**:
```csharp
// checkCanEscape = true: Filler가 실제로 탈출 가능한 위치에만 배치
PlacementResult placement = useBending
    ? PlaceFallbackBending(color, length, cfg.gridSize, occupiedSet, escapePaths, checkCanEscape: true)
    : PlaceFallback(color, length, cfg.gridSize, occupiedSet, escapePaths, checkCanEscape: true);

if (placement != null)
{
    // ... 기존 Filler 추가 코드 ...

    // 이 Filler의 탈출 경로도 escapePaths에 추가
    // 다음 Filler가 이 Filler의 탈출 경로를 막지 않도록
    var fillerEscapePath = GetEscapePath(placement.x, placement.y, fillerDir, cfg.gridSize);
    foreach (var cell in fillerEscapePath)
    {
        escapePaths.Add(CellKey(cell));
    }
}
```

**결과**: 🔄 테스트 필요
- Filler는 반드시 탈출 가능한 위치에만 배치됨
- Filler-Filler 상호 블로킹도 방지됨
- 밀도 목표 미달성 가능성 (탈출 가능한 위치가 부족할 수 있음)

---

## 5. 현재 상태 분석

### 5.1 최신 구현 상태 (Solution 4 적용 후)

```
적용된 변경사항:
1. PlaceFallback/PlaceFallbackBending에 checkCanEscape 파라미터 추가
2. Filler 배치 시 탈출 가능 여부를 먼저 검증
3. 배치된 Filler의 탈출 경로를 escapePaths에 동적 추가
4. Filler-Filler 상호 블로킹 방지

테스트 필요:
- 높은 밀도(95%) 설정에서 정상 생성되는지 확인
- 밀도 목표 달성률 확인 (탈출 가능 위치 부족 시 미달성 가능)
```

### 5.2 핵심 딜레마 (여전히 존재)

```
목표 충돌:
1. 높은 밀도(95%) 달성 → 많은 Filler 필요
2. Filler가 Main을 막지 않아야 함 → Filler 배치 위치 제한
3. 16x16 그리드에서 95% = 243/256 셀 점유 필요
4. Main 화살표의 탈출 경로는 대부분 비어있어야 함
→ 상충되는 요구사항

Solution 4 접근:
- 밀도 목표보다 풀이 가능성을 우선시
- 탈출 가능한 위치에만 Filler 배치 → 밀도 미달성 허용
```

### 5.2 Validation 로그 분석

```
=== Generator v8 (ReverseGrowth) ===
Attempt 1/50
  Filler: Main arrows' escape paths contain XX cells
  Filler: Current density XX%, target 95%
  Filler: Added N fillers, final density XX%
Validation: valid=false, reason=deadlock
Attempt 2/50
...
(50번 반복)
Failed to generate valid level after max attempts
```

### 5.3 데드락 발생 패턴

```
Case 1: Filler → Main 블로킹
  Main A (dir=R) ← Filler F (A의 탈출 경로에 배치)
  A 탈출 불가, F 풍선 비활성 → 데드락

Case 2: Filler → Filler 블로킹
  Filler F1 ← Filler F2 (F1의 탈출 경로에 배치)
  둘 다 풍선 비활성 상태면 → 데드락

Case 3: 복합 블로킹
  Main A ← Main B ← Filler F
  B는 A가 탈출해야 탈출 가능
  A는 F에 막혀 탈출 불가
  F는 풍선 비활성 → 전체 데드락
```

---

## 6. 추가 고려 사항

### 6.1 가능한 해결 방향

1. **Filler 풍선 활성화 규칙 변경**
   - Filler 풍선을 Lane 끝에 추가 (즉시 활성화)
   - 하지만 이러면 Main 화살표 탈출 전에 Filler가 먼저 팝됨
   - 게임플레이 측면에서 의도한 동작인지 확인 필요

2. **Validation 완화**
   - "deadlock"을 실패로 처리하지 않고 경고로만 표시
   - 하지만 이러면 플레이 불가능한 레벨이 생성될 수 있음

3. **밀도 목표 하향**
   - 95%는 현실적으로 달성 불가능한 목표일 수 있음
   - 75-80% 정도로 제한

4. **Filler 배치 전략 개선**
   - Filler를 "안전한" 위치에만 배치
   - "안전한" = 어떤 화살표도 막지 않는 위치
   - 밀도 목표 미달성 허용

5. **의존성 그래프 기반 검증**
   - 배치 시점에 의존성 그래프 구축
   - 순환 의존성 발생 시 해당 배치 거부
   - 더 복잡하지만 근본적 해결 가능

### 6.2 관련 코드 파일

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/Data/LevelGenerator.cs` | 레벨 생성 알고리즘 |
| `Assets/Scripts/Data/LevelData.cs` | 레벨 데이터 구조 |
| `Assets/Scripts/Data/ArrowData.cs` | 화살표 데이터 구조 |
| `Assets/Scripts/Editor/LevelEditorWindow.cs` | 에디터 UI |

### 6.3 핵심 함수

```csharp
// 레벨 생성 메인 함수
public static LevelData GenerateLevel(GeneratorConfig config)

// Filler 배치
private static List<BlockData> PlaceFillersForDensity(
    List<BlockData> blocks,
    HashSet<string> occupiedSet,
    GeneratorConfig cfg,
    List<List<string>> lanes)

// 검증
private static ValidationResult ValidateGeneratedLevel(
    List<BlockData> blocks,
    List<List<string>> lanes,
    int gridSize)

// 탈출 경로 계산
private static List<Vector2Int> GetEscapePath(int x, int y, string dir, int gridSize)
```

---

## 7. 확정된 설계 결정

1. **Filler의 역할**: ✅ **Filler는 반드시 풍선을 터뜨려야 함**
   - Filler도 Queue에 해당 색상 풍선이 추가됨
   - 단순 장애물이 아닌, 게임플레이에 참여하는 화살표

2. **풍선 팝 순서**: Main 화살표가 먼저 탈출 → 이후 Filler 활성화 → Filler 탈출
   - LIFO Queue에서 Filler 풍선은 앞(index 0)에 삽입됨
   - Main 풍선이 모두 팝된 후 Filler 풍선이 활성화됨

3. **밀도 vs 풀이 가능성**: **풀이 가능성 우선**
   - 95% 밀도를 달성하지 못하더라도 풀이 가능한 레벨 생성이 더 중요
   - checkCanEscape로 인해 밀도 목표 미달성 가능

4. **Validation 기준**: "모든 화살표 탈출 가능" + "모든 풍선 팝 가능" 모두 확인

---

## 8. 참고 자료

- [CLAUDE.md](../CLAUDE.md): 프로젝트 전체 구조 및 게임 메커니즘
- [Plan File](C:\Users\DG-2507-PC-061\.claude\plans\floating-twirling-balloon.md): 상세 구현 계획
