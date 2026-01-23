# WSB Level Editor - 알고리즘 정리

## 개요

HTML 프로토타입에서 개발한 레벨 생성 알고리즘(v1~v8)을 정리합니다.
Unity Level Editor 포팅 시 참고 자료로 활용됩니다.

---

## 버전별 알고리즘 요약

| 버전 | 알고리즘 | 핵심 개념 | 성공률 |
|------|----------|-----------|--------|
| v1 | DAG 기반 의존성 | 위상 정렬 + 역순 배치 | 0% |
| v2 | 분기 DAG + 완화 | 복잡도 낮춤 | 5% |
| v3 | 랜덤 + 검증 | 시뮬레이션 검증 | 20% |
| v4 | 체인 배치 | 좌표 계산 오류 | 0% |
| v5 | 단순 그리드 | 모든 화살표 → 방향 | 100% |
| **v6** | **순차적 의존성** | 먼저 배치 = 나중 탈출 | ~90% |
| v7 | 밀도 제어 + Auto | Filler, Auto 파라미터 | ~90% |
| **v8** | **ReverseGrowth** | 꺾이는 화살표, 미로 형태 | ~85% |

---

## 최종 알고리즘: ReverseGrowth (v8)

### 핵심 아이디어

```
"직진을 선호하지만, 막히면 옆으로 꺾는다"

우선순위:
1순위: 현재 방향으로 직진
2순위: 직진 불가 → 좌/우 중 랜덤 선택
금지:  왔던 방향으로 되돌아가기 (지그재그 방지)
```

### 방향 전환 규칙

```javascript
// 방향 전환 우선순위 (직진 > 좌/우, 뒤로 가기 금지)
const TURN_PRIORITY = {
    U: ['U', 'L', 'R'],  // 위 → 위/좌/우 (아래 금지)
    D: ['D', 'R', 'L'],  // 아래 → 아래/우/좌 (위 금지)
    L: ['L', 'D', 'U'],  // 왼쪽 → 왼/아래/위 (오른쪽 금지)
    R: ['R', 'U', 'D']   // 오른쪽 → 오른/위/아래 (왼쪽 금지)
};

const OPPOSITE = { U: 'D', D: 'U', L: 'R', R: 'L' };
```

### 성장 예시

```
●●●→        (직진만)

●●●
  ↓→        (한 번 꺾임)

●
↓
●●→        (두 번 꺾임)
```

---

## 핵심 함수 명세

### 1. growArrowReverse()

Head에서 시작하여 반대 방향으로 Body를 성장시킵니다.

**입력:**
- `headX`, `headY`: Head 좌표
- `headDir`: 탈출 방향 (U/D/L/R)
- `targetLength`: 목표 길이
- `occupiedSet`: 점유된 셀 Set
- `gridSize`: 그리드 크기

**출력:**
```javascript
{
    path: [{x, y}, ...],  // 셀 경로 배열
    headDir: string       // 탈출 방향
}
```

**알고리즘:**
```
1. path = [Head 위치]
2. currentDir = OPPOSITE[headDir]  (성장 방향)
3. for i = 1 to targetLength:
     next = findNextGrowthCell(current, currentDir, occupied)
     if !next: break
     path.push(next)
     currentDir = next.dir  (꺾였으면 방향 변경)
4. return path.length >= 2 ? { path, headDir } : null
```

### 2. findNextGrowthCell()

다음 성장 셀을 찾습니다. 직진 우선, 막히면 좌/우 시도.

**입력:**
- `x`, `y`: 현재 위치
- `preferredDir`: 선호 방향 (현재 진행 방향)
- `occupiedSet`: 점유된 셀들
- `gridSize`: 그리드 크기

**출력:**
```javascript
{
    x: number,
    y: number,
    dir: string  // 이동한 방향
}
// 또는 null (모든 방향 막힘)
```

**알고리즘:**
```
1. priority = TURN_PRIORITY[preferredDir]
2. searchOrder = [직진, shuffle([좌, 우])]
3. for dir of searchOrder:
     nx, ny = current + DIR_VECTORS[dir]
     if inBounds(nx, ny) && !occupied(nx, ny):
       return { x: nx, y: ny, dir }
4. return null
```

### 3. placeFillersForDensity()

목표 밀도 달성까지 빈 공간에 Filler 화살표를 배치합니다.

**입력:**
- `blocks`: 기존 배치된 블록들
- `occupiedSet`: 점유된 셀 Set
- `cfg`: 설정 (targetDensity, fillerMinLength, fillerMaxLength 등)

**출력:**
- 추가된 Filler 블록 배열

**알고리즘:**
```
1. targetOccupied = totalCells * targetDensity
2. while currentOccupied < targetOccupied:
     color = randomPick(colors)
     length = randomInt(fillerMin, fillerMax)
     placement = placeFallbackBending(color, length, gridSize, occupiedSet)
     if placement:
       fillers.push(placement)
       occupiedSet.add(placement.cells)
3. return fillers
```

### 4. validateGeneratedLevel()

시뮬레이션으로 레벨이 풀 수 있는지 검증합니다.

**입력:**
- `blocks`: 배치된 블록들
- `lanes`: Queue 구성
- `gridSize`: 그리드 크기

**출력:**
```javascript
{
    valid: boolean,
    queueCleared: boolean,
    escapeSequence: [originalIndex, ...]  // 실제 탈출 순서
}
```

**알고리즘:**
```
1. remaining = blocks.copy()
2. escapeSequence = []
3. while remaining.length > 0:
     occupied = remaining.flatMap(b => b.cells)
     for block in remaining:
       escapePath = getEscapePath(block.head, block.dir)
       if !escapePath.intersects(occupied):
         escapeSequence.push(block.originalIndex)
         remaining.remove(block)
         break
     if !escaped: return { valid: false, reason: 'deadlock' }
4. return { valid: true, escapeSequence }
```

---

## 데이터 구조

### Block (화살표)

```javascript
const block = {
    x: number,           // Head X 좌표
    y: number,           // Head Y 좌표
    c: string,           // 색상 (R/G/Y/D/P)
    d: string,           // 탈출 방향 (U/D/L/R)
    l: number,           // 길이
    path: [{x,y}, ...],  // 꺾이는 화살표: 셀 경로 배열
    isBending: boolean,  // 꺾이는 화살표 여부
    order: number,       // 탈출 순서 (1부터 시작)
    isFiller: boolean    // Filler 여부
};
```

### Level

```javascript
const level = {
    name: string,
    size: number,                    // Grid 크기
    lanes: [[color, ...], ...],      // Queue 구성
    blocks: [block, ...],            // 화살표 배열
    stats: {
        density: number,             // 실제 밀도
        mainArrows: number,          // 메인 화살표 수
        fillers: number,             // Filler 수
        totalArrows: number
    }
};
```

### 방향 벡터

```javascript
const DIR_VECTORS = {
    U: { dx: 0, dy: -1 },  // 위
    D: { dx: 0, dy: 1 },   // 아래
    L: { dx: -1, dy: 0 },  // 왼쪽
    R: { dx: 1, dy: 0 }    // 오른쪽
};
```

---

## 생성 파라미터

### 기본 파라미터

```javascript
const PARAM_RANGES = {
    gridSize:        { min: 5, max: 12 },
    laneCount:       { min: 1, max: 6 },
    balloonsPerLane: { min: 1, max: 8 },
    missArrowCount:  { min: 0, max: 10 },
    minBlockLength:  { min: 1, max: 8 },
    maxBlockLength:  { min: 2, max: 20 },  // Bending 모드에서 20칸까지
    targetDensity:   { min: 0.2, max: 0.8 }
};
```

### Grid 크기별 Auto 프리셋

| 크기 | Grid | Lanes | Balloons/Lane | Miss | MinLen | MaxLen |
|------|------|-------|---------------|------|--------|--------|
| Small | 5-6 | 2 | 2 | 1 | 3 | 6 |
| Medium | 7-9 | 2 | 3 | 2 | 4 | 8 |
| Large | 10-12 | 3 | 3 | 3 | 5 | 10 |

---

## Snake 이동 시스템

### 이동 규칙

1. **머리 이동**: Head가 탈출 방향으로 한 칸 이동
2. **몸통 따라오기**: 각 Body 셀이 앞 셀 위치로 이동
3. **꼬리 사라짐**: 가장 마지막 셀 제거

```
Before:  ●●●→    (Head at right)
After:    ●●●→   (moved right by 1 cell)
```

### 충돌 처리

1. **충돌 감지**: Head 진행 방향에 다른 화살표가 있으면 충돌
2. **바운스백**: 이동했던 만큼 역방향으로 복귀
3. **첫 칸 충돌**: 바운스 애니메이션만 (살짝 앞으로 갔다가 복귀)

### 탈출 처리

1. **탈출 시작**: Head가 그리드 경계 밖으로 나감
2. **순차 이동**: 모든 셀이 한 칸씩 앞으로 이동
3. **꼬리 제거**: 마지막 셀부터 순차적으로 사라짐
4. **완료**: 모든 셀이 사라지면 탈출 완료

---

## 순차적 의존성 (v6)

### 원리

"먼저 배치한 화살표의 Body가 나중 화살표의 탈출 경로를 막는다"

```
배치 순서: A → B → C
탈출 순서: C → B → A (역순)
```

### 배치 알고리즘

1. **첫 번째 화살표**: 즉시 탈출 가능한 위치 (그리드 가장자리)
2. **이후 화살표**: 이전 화살표의 Body에 의해 막히는 위치
3. **폴백**: 막힌 위치를 못 찾으면 아무 곳에나 배치

```javascript
// 의존성 배치 로직
function findBlockedPosition(color, length, gridSize, occupiedSet, blockerCells) {
    // blockerCells: 이전 화살표의 셀들
    // 탈출 경로가 blockerCells를 지나가는 위치 찾기
    for (각 가능한 위치) {
        const escapePath = getEscapePath(x, y, dir, gridSize);
        if (escapePath가 blockerCells를 지나감) {
            return { x, y, dir };
        }
    }
    return null;
}
```

---

## 밀도 제어 (v7)

### 목표

- 기본 화살표만으로는 밀도가 낮음 (~20%)
- Filler 시스템으로 50-80% 밀도 달성

### Filler 배치 규칙

1. 메인 화살표 배치 후 빈 공간 확인
2. 목표 밀도까지 Filler 화살표 추가
3. Filler도 Bending 모드 지원 (꺾이는 화살표)
4. 색상은 랜덤, 길이는 fillerMinLength~fillerMaxLength

---

## 알려진 이슈

1. **Overlap 에러**: Filler 배치 시 간헐적 겹침 감지 실패
2. **성능**: 복잡한 path로 인해 렌더링 약간 저하 (큰 그리드에서)
3. **밸런스**: 밀도가 높을수록 풀이 경로가 제한적

---

## Unity 포팅 고려사항

### 좌표계

- HTML: 좌상단 (0,0), Y 아래로 증가
- Unity 2D: 좌하단 (0,0), Y 위로 증가
- 포팅 시 Y 좌표 변환 필요: `unityY = gridSize - 1 - htmlY`

### 데이터 저장

- JSON 형식 그대로 사용 가능
- ScriptableObject로 래핑하여 Unity Asset으로 관리

### 클래스 구조 제안

```
LevelGeneratorConfig.cs   - 생성 파라미터 (ScriptableObject)
LevelGenerator.cs         - 메인 생성 로직
ArrowGrowthService.cs     - ReverseGrowth 알고리즘
LevelValidator.cs         - 해결 가능 여부 검증
LevelEditorWindow.cs      - Editor UI
```

---

*문서 작성일: 2026-01-23*
*기반: HTML 프로토타입 v8 (ReverseGrowth + Bending)*