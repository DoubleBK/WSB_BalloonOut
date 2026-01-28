# Batch Generation 완화 전략 (Relaxation Strategy)

## 1. 개요

Batch Generate로 레벨을 생성할 때, 파라미터가 까다로워 생성에 실패하는 경우가 발생합니다. 이를 해결하기 위해 단계적 완화 전략을 적용하여 생성 성공률을 높입니다.

**구현 위치**: [LevelEditorWindow.cs:952-1008](../Scripts/Editor/LevelEditorWindow.cs#L952-L1008)

---

## 2. 현재 완화 전략 (3단계)

### Step 0: 원본 Config
- 시도 횟수: **200회**
- 완화 없음, 원본 파라미터로 생성 시도

### Step 1: gridSize 증가
- 완화 내용: `gridSize +1`
- 시도 횟수: **200회**
- 효과: 공간 확보로 화살표 배치가 용이해짐
- 부작용: 레벨 크기 변경 (하지만 밀도는 유지되므로 영향 최소)

**예시:**
```
원본: gridSize=10 → 완화: gridSize=11
```

### Step 2: missArrowCount 감소
- 완화 내용: `missArrowCount -1` (최소 0)
- 시도 횟수: **200회**
- 효과: 배치해야 할 화살표 수 감소
- 부작용: Miss 화살표 감소로 난이도 약간 하락

**예시:**
```
원본: missArrowCount=3 → 완화: missArrowCount=2
```

### Step 3: targetDensity 감소
- 완화 내용: `targetDensity -0.05`
- 시도 횟수: **200회**
- 효과: 밀도 요구사항 완화로 배치 공간 확보
- 부작용: 맵이 비어 보일 수 있음 (90% → 85%)

**예시:**
```
원본: targetDensity=0.90 → 완화: targetDensity=0.85
```

### 모두 실패
- 4단계 모두 실패 시 해당 레벨 생성 실패로 기록
- 총 시도 횟수: 800회 (200회 × 4단계)

---

## 3. 제안된 완화 전략 (4단계) ⭐ 추천

### 완화 전략 순서 변경 이유

**기존 순서:**
```
Step 1: gridSize +1
Step 2: missArrowCount -1
Step 3: targetDensity -0.05
```

**제안 순서:**
```
Step 1: gridSize +1
Step 2: maxBlockLength -1 or -2      ⭐ NEW
Step 3: missArrowCount -1
Step 4: targetDensity -0.05
```

**근거:**
1. **gridSize 증가** - 공간 확보가 가장 근본적인 해결책
2. **maxBlockLength 감소** - 긴 화살표일수록 배치하기 어려움, 길이 제한이 배치 성공률에 직접적 영향
3. **missArrowCount 감소** - 화살표 개수 자체를 줄이는 것
4. **targetDensity 감소** - 레벨의 "느낌"을 변경하는 최후의 수단

---

## 4. 제안된 Step 2: maxBlockLength 완화 (NEW)

### 완화 규칙

```csharp
int originalMaxLength = config.maxBlockLength;
int reduction = config.maxBlockLength >= 10 ? 2 : 1;  // 큰 레벨은 -2, 작은 레벨은 -1
config.maxBlockLength = Mathf.Max(config.minBlockLength + 1, config.maxBlockLength - reduction);
```

**완화 폭:**
- `maxBlockLength >= 10`: **-2** (큰 레벨일수록 더 많이 완화)
- `maxBlockLength < 10`: **-1** (작은 레벨은 소폭 완화)

**최소값 제한:**
- `maxBlockLength`가 `minBlockLength + 1`보다 작아지지 않도록 제한
- 이미 최소값이면 이 단계 스킵

**예시:**
```
원본: minBlockLength=4, maxBlockLength=10
완화: minBlockLength=4, maxBlockLength=8  (-2)

원본: minBlockLength=5, maxBlockLength=6
완화: 불가 (이미 최소값, 이 단계 스킵)
```

### 효과와 부작용

**효과:**
- 긴 화살표 제한으로 배치 난이도 감소
- 화살표 간 막힘 가능성 감소 (짧은 화살표가 공간을 덜 차지)
- 높은 밀도(90%+) 달성이 용이해짐

**부작용:**
- 화살표 최대 길이 제한으로 레벨 디자인 의도와 다를 수 있음
- 하지만 `gridSize` 증가나 `targetDensity` 감소보다는 영향이 적음

---

## 5. 완화 전략 비교표

| Step | 완화 내용 | 완화 폭 | 시도 횟수 | 효과 | 부작용 | 우선순위 |
|------|----------|---------|----------|------|--------|---------|
| 0 | 원본 | - | 200 | - | - | - |
| 1 | gridSize 증가 | +1 | 200 | 공간 확보 ⭐⭐⭐⭐⭐ | 레벨 크기 변경 | 1위 |
| 2 | **maxBlockLength 감소** | **-1 or -2** | **200** | **배치 용이 ⭐⭐⭐⭐** | **최대 길이 제한** | **2위** |
| 3 | missArrowCount 감소 | -1 | 200 | 화살표 수 감소 ⭐⭐⭐ | 난이도 하락 | 3위 |
| 4 | targetDensity 감소 | -0.05 | 200 | 밀도 완화 ⭐⭐ | 맵이 비어 보임 | 4위 |

**총 시도 횟수:** 1000회 (200회 × 5단계)

---

## 6. 구현 코드

### 현재 구현 (LevelEditorWindow.cs)

```csharp
private GenerationResult TryGenerateWithRelaxation(LevelConfigRecord configRecord)
{
    const int attemptsPerStep = 200;

    // Step 0: 원본 Config
    var config = configRecord.ToGeneratorConfig();
    var level = LevelGenerator.GenerateLevel(config, attemptsPerStep);
    if (level != null)
    {
        return new GenerationResult { levelData = level, relaxationStep = 0, relaxationDesc = "" };
    }

    // Step 1: gridSize +1
    var relaxed1 = configRecord.ToGeneratorConfig();
    relaxed1.gridSize += 1;
    level = LevelGenerator.GenerateLevel(relaxed1, attemptsPerStep);
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
    level = LevelGenerator.GenerateLevel(relaxed2, attemptsPerStep);
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
    level = LevelGenerator.GenerateLevel(relaxed3, attemptsPerStep);
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
```

### 제안 구현 (maxBlockLength 추가)

```csharp
private GenerationResult TryGenerateWithRelaxation(LevelConfigRecord configRecord)
{
    const int attemptsPerStep = 200;

    // Step 0: 원본 Config
    var config = configRecord.ToGeneratorConfig();
    var level = LevelGenerator.GenerateLevel(config, attemptsPerStep);
    if (level != null)
    {
        return new GenerationResult { levelData = level, relaxationStep = 0, relaxationDesc = "" };
    }

    // Step 1: gridSize +1
    var relaxed1 = configRecord.ToGeneratorConfig();
    relaxed1.gridSize += 1;
    level = LevelGenerator.GenerateLevel(relaxed1, attemptsPerStep);
    if (level != null)
    {
        return new GenerationResult
        {
            levelData = level,
            relaxationStep = 1,
            relaxationDesc = $"gridSize {configRecord.gridSize}→{relaxed1.gridSize}"
        };
    }

    // Step 2 (NEW): maxBlockLength -1 or -2
    var relaxed2 = configRecord.ToGeneratorConfig();
    int originalMaxLength = relaxed2.maxBlockLength;
    int reduction = relaxed2.maxBlockLength >= 10 ? 2 : 1;
    relaxed2.maxBlockLength = Mathf.Max(relaxed2.minBlockLength + 1, relaxed2.maxBlockLength - reduction);

    // 완화 가능한 경우만 시도 (이미 최소값이면 스킵)
    if (relaxed2.maxBlockLength < originalMaxLength)
    {
        level = LevelGenerator.GenerateLevel(relaxed2, attemptsPerStep);
        if (level != null)
        {
            return new GenerationResult
            {
                levelData = level,
                relaxationStep = 2,
                relaxationDesc = $"maxLength {originalMaxLength}→{relaxed2.maxBlockLength}"
            };
        }
    }

    // Step 3: missArrowCount -1
    var relaxed3 = configRecord.ToGeneratorConfig();
    relaxed3.missArrowCount = Mathf.Max(0, relaxed3.missArrowCount - 1);
    level = LevelGenerator.GenerateLevel(relaxed3, attemptsPerStep);
    if (level != null)
    {
        return new GenerationResult
        {
            levelData = level,
            relaxationStep = 3,
            relaxationDesc = $"missArrow {configRecord.missArrowCount}→{relaxed3.missArrowCount}"
        };
    }

    // Step 4: targetDensity -0.05
    var relaxed4 = configRecord.ToGeneratorConfig();
    relaxed4.targetDensity -= 0.05f;
    level = LevelGenerator.GenerateLevel(relaxed4, attemptsPerStep);
    if (level != null)
    {
        return new GenerationResult
        {
            levelData = level,
            relaxationStep = 4,
            relaxationDesc = $"density {configRecord.targetDensity:F2}→{relaxed4.targetDensity:F2}"
        };
    }

    // 모두 실패
    return new GenerationResult { levelData = null, relaxationStep = -1, relaxationDesc = "all failed" };
}
```

---

## 7. 완화 조건 및 시점

### 완화 조건

각 Step마다:
- **200회 시도** 후 실패하면 다음 Step으로 이동
- 각 Step은 독립적으로 원본 config 기반으로 완화 적용
- Step 2(maxBlockLength)는 이미 최소값(`minBlockLength + 1`)이면 스킵

### 완화 시점

**자동 완화:**
- Batch Generate 버튼 클릭 시 자동으로 완화 전략 적용
- 레벨당 최대 **1000회** 시도 (Step 0~4 × 200회)
- 단, Step 2를 스킵하면 800회

**완화 순서:**
```
원본 Config (200회)
  ↓ 실패
gridSize +1 (200회)
  ↓ 실패
maxBlockLength -1/-2 (200회) ← 스킵 가능
  ↓ 실패
missArrowCount -1 (200회)
  ↓ 실패
targetDensity -0.05 (200회)
  ↓ 실패
생성 실패 기록
```

---

## 8. 다른 완화 방안 고려

### Branching Mode 활성화

**현재 상태:**
```csharp
branchingMode = false,
branchingChance = 0.4f
```

**Branching이란:**
- `branchingMode = true`이면 일부 화살표를 "첫 번째처럼" 즉시 탈출 가능한 위치에 배치
- ReverseGrowth 알고리즘의 "서로 막히는" 구조를 약화시킴
- 배치 성공률 증가, 하지만 레벨 성격 변화

**권장:**
- **Branching은 완화 전략으로 사용하지 않음**
- 레벨 성격이 크게 변하므로, 파라미터 조정으로만 해결
- 만약 Branching을 사용하려면 LevelConfigTable에서 레벨별로 명시적으로 설정

### Bending Chance 조정

**영향:**
- `bendingChance` 낮추기 (1.0 → 0.8) - 직선 화살표 증가
- 배치 성공률 약간 증가 가능하지만 효과 미미
- **권장하지 않음** - maxBlockLength 완화가 더 효과적

---

## 9. 통계 및 로그

### Batch Generate 결과 예시

```
Level 1: Success (Step 0)
Level 2: Success (Step 1: gridSize 4→5)
Level 3: Success (Step 2: maxLength 10→8)
Level 4: Success (Step 0)
Level 5: Success (Step 3: missArrow 3→2)
...
Level 100: Success (Step 0)

총 100개 레벨:
- Step 0 성공: 65개 (65%)
- Step 1 성공: 20개 (20%)
- Step 2 성공: 10개 (10%)
- Step 3 성공: 4개 (4%)
- Step 4 성공: 1개 (1%)
- 실패: 0개
```

### 로그 확인

Unity Console에서 완화 정보 확인 가능:
```
Level 50: Generated with relaxation (Step 2: maxLength 13→11)
```

---

## 10. 참고 사항

### 완화 전략 선택 기준

1. **레벨 크기 우선** - `gridSize` 증가가 가장 안전하고 효과적
2. **화살표 길이 제한** - `maxBlockLength` 감소가 그 다음 효과적
3. **화살표 수 감소** - `missArrowCount` 감소는 난이도에 영향
4. **밀도 완화 최후** - `targetDensity` 감소는 시각적 품질 저하

### Level Balance 문서 연계

[LevelBalance_100.md 섹션 8.3](./LevelBalance_100.md#83-생성-실패-대비)에 명시된 완화 전략과 일치합니다.

**문서 업데이트 필요:**
- `maxBlockLength` 완화 추가 시 LevelBalance_100.md 섹션 8.3 업데이트 권장

---

작성일: 2026-01-28