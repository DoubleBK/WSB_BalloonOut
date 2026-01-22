# Arrow Puzzle - 자동 레벨 생성 알고리즘 개발 히스토리

## 개요
Arrow Puzzle 게임의 자동 레벨 생성기 개발 과정에서 겪은 문제점과 해결 방안을 기록합니다.

---

## 버전별 알고리즘 변천사

### v1: DAG 기반 의존성 설계 (실패)

**접근 방식:**
- Directed Acyclic Graph(DAG)로 화살표 간 의존성 설계
- 위상 정렬(Topological Sort)로 탈출 순서 결정
- 역순 배치: 마지막에 탈출할 화살표부터 배치

**문제점:**
- 의존성 조건을 만족하는 물리적 배치를 찾기 어려움
- "A가 B의 경로를 막아야 한다" 조건이 너무 제약적
- 선형 의존성(A→B→C→D)은 배치 실패율이 높음
- 분기 의존성으로 변경해도 복잡도만 증가

**결과:** 생성 실패율 100%

---

### v2: 분기 의존성 + 완화된 조건 (실패)

**개선 시도:**
- 선형 의존성 → 분기 의존성 (여러 루트 화살표 허용)
- "경로 막기" 조건을 50% 확률로만 적용
- 2단계 배치: 1차 시도(조건 엄격) → 2차 시도(조건 완화)

**문제점:**
- 여전히 복잡한 의존성 로직
- 화살표 수 × 평균 길이 > 그리드 용량 문제 발생
- 15개 화살표 × 2.5칸 = 37.5칸 > 36칸 (6×6 그리드)

**결과:** 생성 실패율 ~95%

---

### v3: 랜덤 배치 + 검증 (부분 성공)

**접근 방식:**
- 복잡한 의존성 로직 제거
- 화살표를 **완전 랜덤 배치**
- 시뮬레이션으로 풀 수 있는지 검증
- 못 풀면 재시도 (최대 100회)

**구현:**
```javascript
function placeArrowsRandomly(colors, config) {
    // 각 화살표를 겹치지 않는 랜덤 위치에 배치
    for (const color of colors) {
        const candidates = []; // 가능한 모든 위치 수집
        // 랜덤 선택
        const chosen = randomPick(candidates);
        blocks.push(block);
    }
}

function validateLevel(blocks, lanes, gridSize) {
    // 게임 시뮬레이션
    while (remaining.length > 0) {
        // 탈출 가능한 블록 찾기
        // Queue 매칭 우선 선택
        // 반복
    }
    return { valid: allPopped };
}
```

**문제점:**
- 랜덤 배치로는 "풀 수 있는" 퍼즐이 잘 안 만들어짐
- 탈출 순서가 Queue pop 순서와 맞지 않으면 실패
- 200회 재시도해도 성공률 낮음
- 밀도가 높을수록 실패율 증가

**결과:** 생성 실패율 ~80% (설정에 따라 다름)

---

### v4: 체인 배치 (실패)

**접근 방식:**
- 모든 화살표가 같은 방향을 향함
- 수직/수평으로 정렬하여 자연스러운 의존성 생성

**문제점:**
- Head 위치와 Body 위치 계산 오류
- `mainDir='R'`일 때 Head가 x=0에서 시작하면 Body가 음수 좌표로 감
- 범위 벗어남 → 검증 실패 → 재시도 반복

**버그 원인:**
```javascript
// calculateCells: Head에서 Body 방향 계산
x: block.x - d.dx * k  // R(dx=1)이면 Body는 왼쪽으로
// Head가 x=0이면 Body는 x=-1, -2 (범위 밖!)
```

**결과:** 생성 실패율 100%

---

### v5: 단순 그리드 배치 (성공)

**접근 방식:**
- **가장 단순한 방식 채택**
- 모든 화살표가 오른쪽(→)을 향함
- 각 행에 하나씩 배치 (row 0, 1, 2, ...)
- Head 위치를 `maxBlockLength` 이상으로 설정하여 Body가 범위 안에 들어오도록 보장

**구현:**
```javascript
function generateLevel(config) {
    const startX = maxLen; // 몸통이 들어갈 공간 확보
    let row = 0;

    for (let i = 0; i < colors.length; i++) {
        const block = {
            x: startX,  // Head 위치
            y: row,
            d: 'R',     // 모두 오른쪽
            l: len
        };
        blocks.push(block);
        row += 1;
    }
}
```

**특징:**
- 100% 생성 성공
- 모든 화살표가 즉시 탈출 가능 (서로 막지 않음)
- 퍼즐 난이도는 낮지만, 기본 동작 검증 가능

**결과:** 생성 성공률 100%

---

### v6: 순차적 의존성 배치 (개선)

**접근 방식:**
- **"먼저 배치한 화살표의 Body가 나중 화살표의 탈출 경로를 막는다"**
- 첫 번째 화살표: 즉시 탈출 가능한 위치에 배치
- 이후 화살표: 이전 화살표의 Body에 의해 막히는 위치에 배치
- 다양한 방향(↑↓←→) 사용

**구현:**
```javascript
function findBlockedPosition(color, length, gridSize, occupiedSet, blockerCells) {
    // blocker의 셀들이 내 탈출 경로에 있는 위치 찾기
    for (dir of ['U', 'D', 'L', 'R']) {
        for (각 그리드 위치) {
            const escapePath = getEscapePath(x, y, dir, gridSize);
            // 탈출 경로가 blocker를 지나가는지 확인
            if (escapePath가 blockerCells를 지나감) {
                return { x, y, dir };
            }
        }
    }
}
```

**핵심 로직:**
1. 첫 번째 화살표: `placeFirstArrow()` - 즉시 탈출 가능
2. 이후 화살표: `findBlockedPosition()` - 이전 화살표에 막힘
3. 폴백: `placeFallback()` - 막힌 위치 못 찾으면 아무 곳에나
4. 검증: `validateLevel()` - 시뮬레이션으로 풀 수 있는지 확인

**특징:**
- 화살표 간 **의존성 체인** 형성
- A → B → C 순서대로 탈출해야 함
- 다양한 방향 사용으로 시각적 다양성
- 폴백 로직으로 생성 실패 최소화

**결과:** 난이도 상승, 다양한 방향 지원

#### v6 버그 수정: 함수명 충돌

**문제:**
```
Generation failed: Cannot read properties of undefined (reading 'valid')
```

**원인:**
- `validateLevel` 함수가 `generator.js`와 `game.js` **두 곳에 모두 존재**
- `game.js`가 나중에 로드되어 `generator.js`의 함수를 덮어씀
- `game.js`의 `validateLevel`은 대부분 `undefined`를 반환
- `validation.valid` 접근 시 에러 발생

**해결:**
```javascript
// generator.js에서 함수명 변경
function validateGeneratedLevel(blocks, lanes, gridSize) {
    // 생성기 전용 검증 로직
}
```

---

### v6.1: 게임 로직 개선 (충돌 시스템)

**변경 사항:**
1. **모든 화살표 클릭 가능** - 비활성화 상태 제거
2. **충돌 시 바운스 백** - 다른 화살표에 막히면 제자리로 돌아옴

**구현:**
```javascript
// 충돌 거리 계산
function getDistanceToCollision(block) {
    const occ = new Set();
    blocks.forEach(b => {
        if (b.id !== block.id && b.cells) {
            b.cells.forEach(c => occ.add(`${c.x},${c.y}`));
        }
    });

    const d = DIR_VECTORS[block.d];
    let head = block.cells[0];
    let distance = 0;
    let cx = head.x + d.dx;
    let cy = head.y + d.dy;

    while (isInBounds(cx, cy)) {
        if (occ.has(`${cx},${cy}`)) {
            return { blocked: true, distance };
        }
        distance++;
        cx += d.dx;
        cy += d.dy;
    }
    return { blocked: false, distance };
}

// 충돌 애니메이션
function tryEscape(block) {
    const collision = getDistanceToCollision(block);

    if (collision.blocked) {
        // 충돌 지점까지 이동 후 되돌아옴
        const moveDistance = (collision.distance + 0.5) * cellSize;
        el.style.transition = 'transform 0.2s ease-out';
        el.style.transform = `translate(${d.dx * moveDistance}px, ${d.dy * moveDistance}px)`;

        setTimeout(() => {
            el.style.transition = 'transform 0.25s ease-in';
            el.style.transform = 'translate(0, 0)';
        }, 200);
        return;
    }
    // 탈출 성공 로직...
}
```

**특징:**
- 클릭 시 화살표가 진행 방향으로 이동 시도
- 앞에 다른 화살표가 있으면 충돌 후 원위치로 복귀
- 경로가 비어있으면 정상적으로 탈출

---

### v6.2: UI 개선

#### 1. 화살표 순서 번호 표시

생성된 화살표에 탈출 순서 번호 표시:

```javascript
// generator.js - 블록에 order 필드 추가
const cleanBlocks = blocks.map((b, idx) => ({
    x: b.x, y: b.y, c: b.c, d: b.d, l: b.l,
    order: idx + 1  // 1부터 시작하는 탈출 순서
}));

// game.js - 렌더링 시 번호 표시
if (block.order) {
    const orderEl = document.createElement('div');
    orderEl.className = 'arrow-order';
    orderEl.textContent = block.order;
    headEl.appendChild(orderEl);
}
```

```css
.arrow-order {
    position: absolute;
    font-size: 0.7rem;
    font-weight: bold;
    color: rgba(0, 0, 0, 0.7);
    background: rgba(255, 255, 255, 0.9);
    border-radius: 50%;
    width: 18px;
    height: 18px;
}
```

#### 2. Solution Order 표시

화면 하단에 정답 순서 표시:

```html
<div class="solution-box" id="solution-box">
    <div class="solution-title">Solution Order</div>
    <div class="solution-sequence" id="solution-sequence"></div>
</div>
```

```javascript
function renderSolution() {
    if (!solutionOrder || solutionOrder.length === 0) {
        solutionBox.classList.remove('visible');
        return;
    }

    solutionBox.classList.add('visible');
    solSeq.innerHTML = solutionOrder.map((item, idx) => {
        const popped = idx < poppedCount;
        return `<div class="solution-item c-${item.color} ${popped ? 'popped' : ''}">${item.order}</div>`;
    }).join('');
}
```

#### 3. Restart 버튼

생성된 레벨 재시작 기능:

```html
<button class="btn btn-restart" id="btn-restart" onclick="restartLevel()">↻ Restart</button>
```

```javascript
let generatedLevelData = null;

function restartLevel() {
    if (generatedLevelData) {
        initLevel(curLevelIdx);  // 저장된 레벨 데이터로 재초기화
    }
}

function updateRestartButton() {
    const btn = document.getElementById('btn-restart');
    if (generatedLevelData) {
        btn.classList.add('visible');
    } else {
        btn.classList.remove('visible');
    }
}
```

---

### v6.3: Solution Order 버그 수정

**문제:**
- Solution Order가 "배치 순서"를 표시 (order = idx + 1)
- 실제 "클릭 순서"와 다를 수 있음
- `findBlockedPosition` 실패 시 `placeFallback` 사용 → 의존성 체인이 깨짐

**원인:**
```javascript
// 이전 코드: 배치 순서를 그대로 order로 사용
const cleanBlocks = blocks.map((b, idx) => ({
    ...
    order: idx + 1  // 배치 순서 (≠ 실제 클릭 순서)
}));
```

**해결:**
```javascript
// validateGeneratedLevel에서 실제 탈출 순서 반환
function validateGeneratedLevel(blocks, lanes, gridSize) {
    const escapeSequence = [];  // 실제 탈출 순서 기록

    while (remaining.length > 0) {
        for (let i = 0; i < remaining.length; i++) {
            if (!blocked) {
                escapeSequence.push(b.originalIndex);  // 탈출한 블록 기록
                remaining.splice(i, 1);
                break;
            }
        }
    }

    return { valid: true, escapeSequence: escapeSequence };
}

// generateLevel에서 실제 순서 사용
const orderMap = new Map();
escapeSequence.forEach((originalIdx, seqIdx) => {
    orderMap.set(originalIdx, seqIdx + 1);  // 실제 탈출 순서
});

const cleanBlocks = blocks.map((b, idx) => ({
    ...
    order: orderMap.get(idx) || 0  // 실제 클릭 순서
}));
```

**결과:**
- Solution Order가 정확한 클릭 순서를 표시
- 1번 클릭 → 2번 클릭 → 3번 클릭 순서대로 따라가면 퍼즐 해결

---

## 핵심 교훈

### 1. 단순함이 최선
복잡한 DAG 의존성 설계보다 단순한 그리드 배치가 더 안정적으로 동작함.

### 2. 제약 조건의 균형
- 너무 많은 제약: 배치 불가능
- 너무 적은 제약: 퍼즐이 너무 쉬움
- 적절한 균형점 찾기가 핵심

### 3. 물리적 한계 고려
```
총 필요 셀 = 화살표 수 × 평균 길이
그리드 용량 = gridSize × gridSize
필요 셀 < 그리드 용량 × 0.5 (여유 필요)
```

### 4. 좌표 계산 주의
- Head 위치와 Body 방향 관계 명확히
- 경계 조건(0, gridSize-1) 항상 검증

---

## 향후 개선 방향

### 1. 의존성 있는 배치
현재는 모든 화살표가 즉시 탈출 가능하여 퍼즐 난이도가 낮음.
일부 화살표가 다른 화살표의 경로를 막도록 배치하면 난이도 상승.

### 2. 다양한 방향
현재는 모두 오른쪽(→)만 향함.
여러 방향(↑↓←→)을 섞으면 시각적으로 더 풍부해짐.

### 3. 밀도 조절
현재 밀도가 낮음 (~20%).
PRD에서 목표한 70%+ 밀도를 위해서는 더 정교한 배치 알고리즘 필요.

### 4. 검증 기반 접근 강화
랜덤 배치 + 시뮬레이션 검증 방식을 개선하여:
- 배치 시 "이 화살표를 탈출시킬 수 있는 경로가 있는가" 확인
- 백트래킹으로 실패 시 이전 단계로 돌아가기

---

## 버전 요약표

| 버전 | 알고리즘 | 성공률 | 문제점 |
|------|----------|--------|--------|
| v1 | DAG + 역순 배치 | 0% | 의존성 조건 만족 불가 |
| v2 | 분기 DAG + 완화 | 5% | 여전히 복잡, 용량 초과 |
| v3 | 랜덤 + 검증 | 20% | 풀 수 있는 배치 희귀 |
| v4 | 체인 배치 | 0% | 좌표 계산 버그 |
| v5 | 단순 그리드 | 100% | 난이도 낮음 |
| v6 | 순차적 의존성 | ~90% | 함수명 충돌 버그 (수정됨) |
| v6.1 | 충돌 바운스 시스템 | - | 게임 로직 개선 |
| v6.2 | UI 개선 | - | 순서 번호, 솔루션 박스, 재시작 |
| v6.3 | Solution Order 수정 | - | 실제 클릭 순서 표시 |

---

## 파일 구조

```
arrow-puzzle/
├── index.html       # 메인 HTML (UI 요소)
├── style.css        # 스타일시트
├── game.js          # 게임 로직 (렌더링, 상호작용, validateLevel)
├── generator.js     # 레벨 생성기 (validateGeneratedLevel)
├── algorithm-history.md  # 개발 히스토리
└── bug-report-v6.md # v6 버그 리포트
```

---

*문서 작성일: 2026-01-22*
*v6 추가: 2026-01-22*
*v6.1, v6.2, v6.3 추가: 2026-01-22*
