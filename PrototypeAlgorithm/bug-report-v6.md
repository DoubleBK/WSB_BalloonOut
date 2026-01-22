# Bug Report: Generator v6 "Cannot read properties of undefined (reading 'valid')"

## 개요
- **버전**: Generator v6 (Dependency Chain)
- **에러 메시지**: `Cannot read properties of undefined (reading 'valid')`
- **발생 위치**: `generateLevel()` 함수 내 `validation.valid` 접근 시

---

## 에러 발생 코드

```javascript
// generator.js - generateLevel() 함수 내부 (약 385행)
const validation = validateLevel(blocks, lanes, cfg.gridSize);
console.log("Validation:", validation);

if (validation.valid) {  // <-- 여기서 에러 발생
    // ...
}
```

---

## 원인 분석

### 1. `validateLevel()` 함수가 `undefined`를 반환
`validation`이 `undefined`이면 `validation.valid` 접근 시 에러 발생.

### 2. 가능한 원인들

#### 2.1 try-catch 이전의 코드에서 예외 발생
```javascript
function validateLevel(blocks, lanes, gridSize) {
    try {
        // 여기 도달하기 전에 예외가 발생하면?
        // → 함수가 undefined 반환
    } catch (e) {
        return { valid: false, reason: 'exception' };
    }
}
```

#### 2.2 blocks 배열의 cells가 undefined
```javascript
// generateLevel에서 block 생성 시
const block = {
    x: placement.x,
    y: placement.y,
    c: color,
    d: placement.dir,
    l: length,
    cells: placement.cells  // <-- placement.cells가 undefined일 수 있음
};
```

#### 2.3 placement 함수들의 반환값 문제
```javascript
// placeFirstArrow, findBlockedPosition, placeFallback 함수들
function placeFirstArrow(color, length, gridSize, occupiedSet) {
    const candidates = [];
    // ... 후보 수집 ...

    if (candidates.length === 0) return null;  // null 반환
    return randomPick(candidates);
}
```

---

## 코드 흐름 분석

```
generateLevel()
├── lanes = generateQueue(cfg)
├── colors = getColorSequence(lanes, cfg.missArrowCount)
├── for (i = 0; i < colors.length; i++)
│   ├── if (i === 0)
│   │   └── placement = placeFirstArrow(...)  // null 가능
│   ├── else
│   │   ├── placement = findBlockedPosition(...)  // null 가능
│   │   └── if (!placement) placement = placeFallback(...)  // null 가능
│   │
│   ├── if (!placement) { success = false; break; }  // 여기서 break
│   │
│   └── blocks.push({ ..., cells: placement.cells })
│
├── if (!success) continue;  // 다음 시도로
│
└── validation = validateLevel(blocks, lanes, cfg.gridSize)
    └── if (validation.valid)  // <-- 에러 발생 지점
```

---

## 의심되는 시나리오

### 시나리오 1: blocks 배열에 잘못된 데이터
```javascript
// placement가 null이 아니어도 placement.cells가 undefined일 수 있음
const placement = { x: 3, y: 2, dir: 'R' };  // cells 누락!
blocks.push({ ..., cells: placement.cells });  // cells: undefined
```

### 시나리오 2: validateLevel 내부에서 예외
```javascript
// blocks 또는 lanes가 예상치 못한 형태
const remaining = blocks.map(b => ({
    ...b,
    cells: [...b.cells]  // b.cells가 undefined면 여기서 예외
}));
```

---

## 현재 방어 코드

```javascript
function validateLevel(blocks, lanes, gridSize) {
    try {
        if (!blocks || blocks.length === 0) {
            return { valid: false, reason: 'no blocks' };
        }

        for (const b of blocks) {
            if (!b.cells || !Array.isArray(b.cells) || b.cells.length === 0) {
                console.warn("Invalid block:", b);
                return { valid: false, reason: 'invalid block cells' };
            }
            // ...
        }
        // ...
    } catch (e) {
        console.error("validateLevel error:", e);
        return { valid: false, reason: 'exception', error: e.message };
    }
}
```

---

## 디버깅 제안

### 1. generateLevel에서 validateLevel 호출 전 방어 코드 추가
```javascript
// Step 4: 검증
console.log("Blocks before validation:", JSON.stringify(blocks, null, 2));

if (!blocks || blocks.length === 0) {
    console.error("No blocks to validate");
    continue;
}

const validation = validateLevel(blocks, lanes, cfg.gridSize);
if (!validation) {
    console.error("validateLevel returned undefined");
    continue;
}

if (validation.valid) {
    // ...
}
```

### 2. placement 함수에서 cells 반환 확인
```javascript
function placeFirstArrow(color, length, gridSize, occupiedSet) {
    // ...
    for (const dir of dirs) {
        for (let x = 0; x < gridSize; x++) {
            for (let y = 0; y < gridSize; y++) {
                const cells = calculateCells(x, y, dir, length);

                // cells 검증 추가
                if (!cells || cells.length === 0) {
                    console.warn("calculateCells returned invalid:", cells);
                    continue;
                }

                // ...
                candidates.push({ x, y, dir, cells });
            }
        }
    }
    // ...
}
```

### 3. 콘솔 로그로 상태 추적
브라우저 개발자 도구에서 확인해야 할 로그:
- `"=== Generator v6 (Dependency Chain) ==="`
- `"Attempt X/50"`
- `"Arrow X: ..."` 각 화살표 배치 로그
- `"Blocks before validation:"` (추가 필요)
- `"Validation:"` 결과

---

## 재현 단계

1. `index.html` 열기
2. "Generate" 버튼 클릭
3. Generator 모달에서 기본값으로 "Generate" 클릭
4. 에러 발생: `Generation failed: Cannot read properties of undefined (reading 'valid')`

---

## 환경 정보

- **파일 구조**:
  - `index.html` - UI
  - `game.js` - 게임 로직
  - `generator.js` - 레벨 생성기 v6
  - `style.css` - 스타일

- **Generator 설정** (DEFAULT_CONFIG):
  ```javascript
  {
      gridSize: 8,
      laneCount: 2,
      balloonsPerLane: 2,
      missArrowCount: 1,
      minBlockLength: 2,
      maxBlockLength: 3
  }
  ```

---

## 관련 함수 목록

| 함수 | 역할 | 반환값 |
|------|------|--------|
| `generateLevel(config)` | 메인 생성 함수 | Level 객체 또는 throw |
| `validateLevel(blocks, lanes, gridSize)` | 풀 수 있는지 검증 | `{ valid: boolean, ... }` |
| `placeFirstArrow(...)` | 첫 화살표 배치 | `{ x, y, dir, cells }` 또는 `null` |
| `findBlockedPosition(...)` | 막힌 위치 찾기 | `{ x, y, dir, cells }` 또는 `null` |
| `placeFallback(...)` | 폴백 배치 | `{ x, y, dir, cells }` 또는 `null` |
| `calculateCells(x, y, dir, length)` | 셀 좌표 계산 | `[{ x, y }, ...]` |

---

## 요청 사항

1. `validateLevel`이 `undefined`를 반환하는 정확한 원인 파악
2. 방어 코드가 제대로 동작하지 않는 이유 분석
3. 근본적인 해결 방안 제시

---

*작성일: 2026-01-22*
