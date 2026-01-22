/**
 * Arrow Puzzle - Level Generator v6 (Dependency Chain)
 *
 * 핵심 아이디어: "먼저 배치한 화살표의 Body가 나중 화살표의 탈출 경로를 막는다"
 * - 첫 번째 화살표: 즉시 탈출 가능
 * - 이후 화살표: 이전 화살표의 Body에 의해 막힘
 * - 다양한 방향(U/D/L/R) 사용
 */

const GENERATOR_COLORS = ['R', 'G', 'Y', 'P'];
const DIRECTIONS = ['U', 'D', 'L', 'R'];

const DIR_VECTORS = {
    U: { dx: 0, dy: -1 },
    D: { dx: 0, dy: 1 },
    L: { dx: -1, dy: 0 },
    R: { dx: 1, dy: 0 }
};

const DEFAULT_CONFIG = {
    gridSize: 8,
    laneCount: 2,
    balloonsPerLane: 2,
    missArrowCount: 1,
    minBlockLength: 2,
    maxBlockLength: 3,
    branchingMode: false,  // 분기형 레벨 생성 모드
    branchingChance: 0.4   // 분기 확률 (0~1)
};

function randomPick(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

function randomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function shuffle(arr) {
    const result = [...arr];
    for (let i = result.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [result[i], result[j]] = [result[j], result[i]];
    }
    return result;
}

// Queue 생성
function generateQueue(config) {
    const lanes = [];
    for (let i = 0; i < config.laneCount; i++) {
        const lane = [];
        for (let j = 0; j < config.balloonsPerLane; j++) {
            lane.push(randomPick(GENERATOR_COLORS));
        }
        lanes.push(lane);
    }
    return lanes;
}

// 탈출 색상 순서 (Queue + Miss)
function getColorSequence(lanes, missCount) {
    const colors = [];
    const lanesCopy = lanes.map(l => [...l]);

    while (lanesCopy.some(l => l.length > 0)) {
        const nonEmpty = lanesCopy.filter(l => l.length > 0);
        const lane = randomPick(nonEmpty);
        colors.push(lane.pop());
    }

    // Miss 색상 추가
    for (let i = 0; i < missCount; i++) {
        const pos = randomInt(0, colors.length);
        colors.splice(pos, 0, randomPick(GENERATOR_COLORS));
    }

    return colors;
}

// 화살표의 모든 셀 계산 (Head + Body)
function calculateCells(x, y, dir, length) {
    const cells = [];
    const d = DIR_VECTORS[dir];

    for (let k = 0; k < length; k++) {
        cells.push({
            x: x - d.dx * k,
            y: y - d.dy * k
        });
    }
    return cells;
}

// 화살표의 탈출 경로 계산 (Head에서 그리드 끝까지)
function getEscapePath(x, y, dir, gridSize) {
    const path = [];
    const d = DIR_VECTORS[dir];
    let cx = x + d.dx;
    let cy = y + d.dy;

    while (cx >= 0 && cx < gridSize && cy >= 0 && cy < gridSize) {
        path.push({ x: cx, y: cy });
        cx += d.dx;
        cy += d.dy;
    }
    return path;
}

// 셀이 범위 안에 있는지 확인
function isInBounds(x, y, gridSize) {
    return x >= 0 && x < gridSize && y >= 0 && y < gridSize;
}

// 셀들이 겹치는지 확인
function hasOverlap(cells, occupiedSet) {
    for (const c of cells) {
        if (occupiedSet.has(`${c.x},${c.y}`)) {
            return true;
        }
    }
    return false;
}

// 셀들이 모두 범위 안에 있는지 확인
function allCellsInBounds(cells, gridSize) {
    for (const c of cells) {
        if (!isInBounds(c.x, c.y, gridSize)) {
            return false;
        }
    }
    return true;
}

// 첫 번째 화살표 배치 (즉시 탈출 가능한 위치)
function placeFirstArrow(color, length, gridSize, occupiedSet) {
    const candidates = [];
    const dirs = shuffle(DIRECTIONS);

    for (const dir of dirs) {
        const d = DIR_VECTORS[dir];

        // 그리드 전체 탐색
        for (let x = 0; x < gridSize; x++) {
            for (let y = 0; y < gridSize; y++) {
                const cells = calculateCells(x, y, dir, length);

                // 범위 체크
                if (!allCellsInBounds(cells, gridSize)) continue;

                // 겹침 체크
                if (hasOverlap(cells, occupiedSet)) continue;

                // 탈출 경로가 막히지 않았는지 확인
                const escapePath = getEscapePath(x, y, dir, gridSize);
                const blocked = escapePath.some(p => occupiedSet.has(`${p.x},${p.y}`));

                if (!blocked) {
                    candidates.push({ x, y, dir, cells });
                }
            }
        }
    }

    if (candidates.length === 0) return null;
    return randomPick(candidates);
}

// 이전 화살표에 의해 막히는 위치 찾기
function findBlockedPosition(color, length, gridSize, occupiedSet, blockerCells) {
    const candidates = [];
    const dirs = shuffle(DIRECTIONS);

    // blocker의 셀들을 Set으로 변환
    const blockerSet = new Set(blockerCells.map(c => `${c.x},${c.y}`));

    for (const dir of dirs) {
        for (let x = 0; x < gridSize; x++) {
            for (let y = 0; y < gridSize; y++) {
                const cells = calculateCells(x, y, dir, length);

                // 범위 체크
                if (!allCellsInBounds(cells, gridSize)) continue;

                // 겹침 체크 (전체 점유 셀과)
                if (hasOverlap(cells, occupiedSet)) continue;

                // 탈출 경로 계산
                const escapePath = getEscapePath(x, y, dir, gridSize);

                // 탈출 경로가 blocker의 셀을 지나가는지 확인
                const blockedByBlocker = escapePath.some(p => blockerSet.has(`${p.x},${p.y}`));

                if (blockedByBlocker) {
                    // 다른 화살표에 의해 추가로 막히지 않는지 확인
                    // (blocker 셀 제외한 나머지 점유 셀로 막히면 안됨)
                    const otherOccupied = new Set([...occupiedSet].filter(k => !blockerSet.has(k)));
                    const blockedByOthers = escapePath.some(p => otherOccupied.has(`${p.x},${p.y}`));

                    if (!blockedByOthers) {
                        candidates.push({ x, y, dir, cells });
                    }
                }
            }
        }
    }

    if (candidates.length === 0) return null;
    return randomPick(candidates);
}

// 폴백: 아무 곳에나 배치 (겹치지 않게)
function placeFallback(color, length, gridSize, occupiedSet) {
    const candidates = [];
    const dirs = shuffle(DIRECTIONS);

    for (const dir of dirs) {
        for (let x = 0; x < gridSize; x++) {
            for (let y = 0; y < gridSize; y++) {
                const cells = calculateCells(x, y, dir, length);

                if (!allCellsInBounds(cells, gridSize)) continue;
                if (hasOverlap(cells, occupiedSet)) continue;

                candidates.push({ x, y, dir, cells });
            }
        }
    }

    if (candidates.length === 0) return null;
    return randomPick(candidates);
}

// 레벨 검증: 시뮬레이션으로 풀 수 있는지 확인 + 실제 탈출 순서 반환
function validateGeneratedLevel(blocks, lanes, gridSize) {
    try {
        // 블록 검증
        if (!blocks || blocks.length === 0) {
            return { valid: false, reason: 'no blocks' };
        }

        // 복사본 생성 (cells 검증 포함) - 원본 인덱스도 저장
        const remaining = [];
        for (let idx = 0; idx < blocks.length; idx++) {
            const b = blocks[idx];
            if (!b.cells || !Array.isArray(b.cells) || b.cells.length === 0) {
                console.warn("Invalid block:", b);
                return { valid: false, reason: 'invalid block cells' };
            }
            remaining.push({
                ...b,
                cells: [...b.cells],
                originalIndex: idx  // 원본 배열에서의 인덱스 저장
            });
        }

        const queuesCopy = lanes.map(l => [...l]);
        const escapeSequence = [];  // 실제 탈출 순서 기록

        let iterations = 0;
        const maxIterations = 100;

        while (remaining.length > 0 && iterations < maxIterations) {
            iterations++;

            // 점유 셀 계산
            const occupied = new Set();
            for (const b of remaining) {
                for (const c of b.cells) {
                    occupied.add(`${c.x},${c.y}`);
                }
            }

            // 탈출 가능한 화살표 찾기
            let escaped = false;

            for (let i = 0; i < remaining.length; i++) {
                const b = remaining[i];
                const d = DIR_VECTORS[b.d];
                if (!d) {
                    console.warn("Invalid direction:", b.d);
                    return { valid: false, reason: 'invalid direction' };
                }
                const head = b.cells[0];

                // 탈출 경로 체크
                let cx = head.x + d.dx;
                let cy = head.y + d.dy;
                let blocked = false;

                while (cx >= 0 && cx < gridSize && cy >= 0 && cy < gridSize) {
                    if (occupied.has(`${cx},${cy}`)) {
                        blocked = true;
                        break;
                    }
                    cx += d.dx;
                    cy += d.dy;
                }

                if (!blocked) {
                    // Queue 매칭 확인
                    for (const lane of queuesCopy) {
                        if (lane.length > 0 && lane[lane.length - 1] === b.c) {
                            lane.pop();
                            break;
                        }
                    }

                    // 탈출 순서 기록 (원본 인덱스 저장)
                    escapeSequence.push(b.originalIndex);

                    // 탈출
                    remaining.splice(i, 1);
                    escaped = true;
                    break;
                }
            }

            if (!escaped) {
                // 아무도 탈출 못함 = 데드락
                return { valid: false, reason: 'deadlock' };
            }
        }

        if (remaining.length > 0) {
            return { valid: false, reason: 'timeout' };
        }

        // Queue가 비었는지 확인
        const queueEmpty = queuesCopy.every(l => l.length === 0);

        return { valid: true, queueCleared: queueEmpty, escapeSequence: escapeSequence };
    } catch (e) {
        console.error("validateLevel error:", e);
        return { valid: false, reason: 'exception', error: e.message };
    }
}

// 메인 레벨 생성 함수
function generateLevel(config = {}) {
    const cfg = { ...DEFAULT_CONFIG, ...config };

    console.log("=== Generator v6 (Dependency Chain) ===");
    console.log("Config:", cfg);

    const maxAttempts = 50;

    for (let attempt = 0; attempt < maxAttempts; attempt++) {
        console.log(`Attempt ${attempt + 1}/${maxAttempts}`);

        // Step 1: Queue 생성
        const lanes = generateQueue(cfg);
        console.log("Lanes:", lanes);

        // Step 2: 색상 순서 (탈출 순서)
        const colors = getColorSequence(lanes, cfg.missArrowCount);
        console.log("Colors (escape order):", colors);

        // Step 3: 순차적 의존성 배치
        const blocks = [];
        const occupiedSet = new Set();
        let success = true;

        for (let i = 0; i < colors.length; i++) {
            const color = colors[i];
            const length = randomInt(cfg.minBlockLength, cfg.maxBlockLength);

            let placement = null;

            if (i === 0) {
                // 첫 번째: 즉시 탈출 가능한 위치
                placement = placeFirstArrow(color, length, cfg.gridSize, occupiedSet);
            } else {
                // 분기 모드: 일정 확률로 폴백(자유 배치) 사용
                const useBranching = cfg.branchingMode && Math.random() < cfg.branchingChance;

                if (useBranching) {
                    // 분기 모드: 막히지 않는 자유로운 위치에 배치
                    console.log(`  Arrow ${i}: Branching mode - free placement`);
                    placement = placeFirstArrow(color, length, cfg.gridSize, occupiedSet);
                } else {
                    // 일반 모드: 이전 화살표에 의해 막히는 위치
                    const prevBlock = blocks[i - 1];
                    placement = findBlockedPosition(color, length, cfg.gridSize, occupiedSet, prevBlock.cells);
                }

                // 못 찾으면 폴백
                if (!placement) {
                    console.log(`  Arrow ${i}: Fallback (couldn't find blocked position)`);
                    placement = placeFallback(color, length, cfg.gridSize, occupiedSet);
                }
            }

            if (!placement) {
                console.log(`  Arrow ${i}: Failed to place`);
                success = false;
                break;
            }

            // 배치
            const block = {
                x: placement.x,
                y: placement.y,
                c: color,
                d: placement.dir,
                l: length,
                cells: placement.cells
            };
            blocks.push(block);

            // 점유 셀 업데이트
            for (const c of placement.cells) {
                occupiedSet.add(`${c.x},${c.y}`);
            }

            console.log(`  Arrow ${i}: ${color} at (${placement.x},${placement.y}) dir=${placement.dir} len=${length}`);
        }

        if (!success) continue;

        // Step 4: 검증
        const validation = validateGeneratedLevel(blocks, lanes, cfg.gridSize);
        console.log("Validation:", validation);

        if (validation.valid) {
            // 성공! blocks에서 cells 제거 (game.js가 다시 계산함)
            // 실제 시뮬레이션 결과의 탈출 순서 사용
            const escapeSequence = validation.escapeSequence || [];

            // 각 블록에 실제 탈출 순서 할당
            const orderMap = new Map();
            escapeSequence.forEach((originalIdx, seqIdx) => {
                orderMap.set(originalIdx, seqIdx + 1);  // 1부터 시작
            });

            const cleanBlocks = blocks.map((b, idx) => ({
                x: b.x,
                y: b.y,
                c: b.c,
                d: b.d,
                l: b.l,
                order: orderMap.get(idx) || 0  // 실제 탈출 순서
            }));

            console.log("=== Generation Successful! ===");
            console.log("Escape sequence:", escapeSequence.map((idx, i) =>
                `${i + 1}: Block ${idx} (${blocks[idx].c})`
            ).join(', '));

            return {
                name: `Gen.${Date.now() % 10000}`,
                size: cfg.gridSize,
                lanes: lanes,
                blocks: cleanBlocks
            };
        }
    }

    throw new Error("Failed to generate valid level after max attempts");
}

// Export
window.LevelGenerator = {
    generateLevel,
    DEFAULT_CONFIG
};

console.log("Generator v6 (Dependency Chain) loaded. Use LevelGenerator.generateLevel()");
