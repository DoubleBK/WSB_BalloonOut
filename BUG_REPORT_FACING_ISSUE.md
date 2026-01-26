# Bug Report: LevelGenerator Facing Issue

## Summary
화살표 배치 시점에 Facing 방지 로직을 추가했으나, 여전히 `[FacingCheck] FACING DETECTED` 에러가 발생함.

## Error Message
```
[FacingCheck] FACING DETECTED: arrows 22 and 28
UnityEngine.Debug:LogError (object)
BalloonOut.Data.LevelGenerator:CheckFacingArrows (at Assets/Scripts/Data/LevelGenerator.cs:991)
BalloonOut.Data.LevelGenerator:ValidateGeneratedLevel (at Assets/Scripts/Data/LevelGenerator.cs:1279)
BalloonOut.Data.LevelGenerator:ValidateLevel (at Assets/Scripts/Data/LevelGenerator.cs:1107)
```

## What is Facing?
두 화살표의 **Head가 인접**하고 **서로를 향하는** 상태. 이 상태에서는 어느 쪽을 먼저 움직여도 충돌하여 레벨이 풀리지 않음.

```
[Arrow2 Head→] [←Arrow1 Head]
    (1,9) R       (2,9) L
```

## Current Implementation

### 1. WouldCauseFacing Helper (Lines 993-1021)
```csharp
private static bool WouldCauseFacing(int headX, int headY, string headDir, List<BlockData> existingBlocks)
{
    if (existingBlocks == null || existingBlocks.Count == 0) return false;

    foreach (var existing in existingBlocks)
    {
        if (existing.cells == null || existing.cells.Count == 0) continue;

        var existingHead = existing.cells[0];  // cells[0] = Head
        int dx = existingHead.x - headX;
        int dy = existingHead.y - headY;

        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;  // 인접 검사

        bool newPointsToExisting = IsDirectionTowards(headDir, dx, dy);
        bool existingPointsToNew = IsDirectionTowards(existing.dir, -dx, -dy);

        if (newPointsToExisting && existingPointsToNew)
            return true;  // Facing!
    }
    return false;
}
```

### 2. Modified Placement Functions
모든 배치 함수에 `existingBlocks` 파라미터 추가 및 `WouldCauseFacing` 검사:
- PlaceFirstArrow (Line 341)
- FindBlockedPosition (Line 385)
- PlaceFallback (Line 453)
- PlaceFirstArrowBending (Line 614)
- FindBlockedPositionBending (Line 661)
- PlaceFallbackBending (Line 734)

### 3. GenerateLevel Calls (Lines 1471-1501)
모든 배치 함수 호출 시 `blocks` 리스트 전달.

### 4. PlaceFillersForDensity (Lines 858-903)
```csharp
var allBlocksForFacing = new List<BlockData>(blocks);  // Main 화살표로 초기화

// 배치 후
fillers.Add(fillerBlock);
allBlocksForFacing.Add(fillerBlock);  // 다음 Filler의 facing 검사에 포함
```

## Potential Root Causes

### Hypothesis 1: ValidateLevel Coordinate Transformation Issue
`ValidateLevel`에서 LevelData(Game 좌표계) → BlockData(Generator 좌표계) 변환 시:

```csharp
// Lines 1073-1095
var cells = arrow.GetCells();
cells.Reverse();  // TAIL-first → HEAD-first

for (int i = 0; i < cells.Count; i++)
{
    cells[i] = new Vector2Int(cells[i].x, gridSize - 1 - cells[i].y);  // Y 플립
}

var block = new BlockData
{
    ...
    dir = arrow.direction,  // 방향은 유지 (문제 가능?)
    cells = cells,
    ...
};
```

**문제점**: Y 좌표를 플립했지만, U/D 방향은 플립하지 않음.
- Game 좌표계: U = y 증가, D = y 감소
- Generator 좌표계: U = y 감소, D = y 증가

**그러나**: IsDirectionTowards에서 dy도 플립되므로 상쇄될 수 있음. 추가 검증 필요.

### Hypothesis 2: Filler Block cells[0] Mismatch
`PlaceFillersForDensity`에서 `fillerBlock.cells`가 `placement.cells`로 설정됨.
`placement.cells`는 `GrowArrowReverse`의 `path`인데, `path[0]`이 정말 Head인지 확인 필요.

```csharp
// GrowArrowReverse (Lines 557-588)
var path = new List<Vector2Int> { new Vector2Int(headX, headY) };  // path[0] = Head
```

**확인 결과**: path[0] = Head가 맞음. 문제 없어 보임.

### Hypothesis 3: arrows 22, 28 are Fillers
22와 28은 Main 화살표 개수보다 큼 → **Filler들 사이의 Facing** 가능성.

`allBlocksForFacing` 업데이트 타이밍 문제?
- 배치 성공 후 `allBlocksForFacing.Add(fillerBlock)` 호출
- 다음 루프에서 이전 Filler와 facing 검사

**확인 결과**: 코드상 올바름. 타이밍 문제 없어 보임.

### Hypothesis 4: Race Condition in Shuffle
`Shuffle(dirs)` 호출로 방향 순서가 랜덤화됨. 특정 순서에서 모든 non-facing candidate가 다른 조건으로 제외되고, facing candidate만 남을 수 있음?

**가능성 낮음**: facing candidate는 continue로 스킵되므로 선택되지 않아야 함.

### Hypothesis 5: CheckFacingArrows vs WouldCauseFacing Logic Mismatch
두 함수의 로직이 정확히 일치하는지 재검토 필요.

```csharp
// CheckFacingArrows
int dx = headB.x - headA.x;
int dy = headB.y - headA.y;
bool aPointsToB = IsDirectionTowards(a.dir, dx, dy);
bool bPointsToA = IsDirectionTowards(b.dir, -dx, -dy);

// WouldCauseFacing
int dx = existingHead.x - headX;  // existing - new
int dy = existingHead.y - headY;
bool newPointsToExisting = IsDirectionTowards(headDir, dx, dy);
bool existingPointsToNew = IsDirectionTowards(existing.dir, -dx, -dy);
```

**매핑**:
- CheckFacingArrows: A=a, B=b, dx=B-A
- WouldCauseFacing: A=new, B=existing, dx=B-A=existing-new

**결론**: 로직 일치함.

## Recommended Debug Steps

1. **WouldCauseFacing에 로그 추가**:
```csharp
private static bool WouldCauseFacing(int headX, int headY, string headDir, List<BlockData> existingBlocks)
{
    if (existingBlocks == null || existingBlocks.Count == 0) return false;

    Debug.Log($"[WouldCauseFacing] Checking ({headX},{headY}) dir={headDir} against {existingBlocks.Count} blocks");

    foreach (var existing in existingBlocks)
    {
        if (existing.cells == null || existing.cells.Count == 0) continue;

        var existingHead = existing.cells[0];
        int dx = existingHead.x - headX;
        int dy = existingHead.y - headY;

        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;

        bool newPointsToExisting = IsDirectionTowards(headDir, dx, dy);
        bool existingPointsToNew = IsDirectionTowards(existing.dir, -dx, -dy);

        Debug.Log($"[WouldCauseFacing] Adjacent: existing({existingHead.x},{existingHead.y}) dir={existing.dir}");
        Debug.Log($"[WouldCauseFacing]   dx={dx}, dy={dy}, new→existing={newPointsToExisting}, existing→new={existingPointsToNew}");

        if (newPointsToExisting && existingPointsToNew)
        {
            Debug.LogWarning($"[WouldCauseFacing] WOULD CAUSE FACING! Skipping this candidate.");
            return true;
        }
    }
    return false;
}
```

2. **CheckFacingArrows에서 감지된 화살표 정보 출력**:
이미 존재하는 로그 확인:
```
[FacingCheck] Adjacent {i}-{j}: A({headA}, dir={a.dir}) → B({headB}, dir={b.dir})
[FacingCheck]   dx={dx}, dy={dy}, aPointsToB={aPointsToB}, bPointsToA={bPointsToA}
```

3. **ValidateLevel vs GenerateLevel 경로 확인**:
facing이 감지된 레벨이 GenerateLevel에서 막 생성된 것인지, 저장 후 로드된 것인지 확인.

## Files to Review
- `Assets/Scripts/Data/LevelGenerator.cs`
  - WouldCauseFacing: Lines 993-1021
  - CheckFacingArrows: Lines 954-999
  - PlaceFillersForDensity: Lines 830-942
  - ValidateLevel: Lines 1058-1108
  - GenerateLevel: Lines 1422-1720

## Critical Hypothesis: Different Code Paths

**배치 시점**과 **검증 시점**의 코드 경로가 다릅니다:

### GenerateLevel 경로 (배치 시점)
```
GenerateLevel
├─ PlaceFirstArrow/FindBlockedPosition/PlaceFallback (blocks 리스트 전달)
├─ WouldCauseFacing 검사 (Generator 좌표계)
├─ blocks 리스트에 추가
└─ ValidateGeneratedLevel 호출 (Generator 좌표계의 blocks 직접 사용)
```

### LevelEditorWindow 경로 (검증 시점)
```
LevelEditorWindow.DrawSolvableInfo
└─ ValidateLevel 호출 (LevelData를 BlockData로 변환)
   ├─ Y 좌표 플립
   ├─ cells 역순으로 변환
   └─ ValidateGeneratedLevel → CheckFacingArrows
```

**핵심 질문**: LevelEditorWindow에서 ValidateLevel을 호출할 때, 이 레벨은:
1. 방금 GenerateLevel로 생성된 것인가? (LevelData로 변환 후 다시 검증)
2. 이전에 저장된 레벨을 로드한 것인가?

만약 (1)이라면, GenerateLevel → LevelData 변환 → ValidateLevel 과정에서 좌표/방향 정보가 왜곡되었을 수 있음.

## Questions for Further Investigation

1. facing이 감지된 레벨은 새로 생성된 것인가, 저장 후 로드된 것인가?
2. arrows 22와 28은 Main 화살표인가, Filler인가?
3. `[WouldCauseFacing]` 로그가 배치 시점에 출력되는가?
4. 배치 시점에 facing이 감지되어 스킵되는 경우가 있는가?
5. GenerateLevel 내부의 ValidateGeneratedLevel에서는 facing이 감지되지 않았는가?

## Environment
- Unity 2022.3.69f1
- File: `Assets/Scripts/Data/LevelGenerator.cs`
