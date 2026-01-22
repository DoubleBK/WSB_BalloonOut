/**
 * Arrow Puzzle - Level Generator v8 (ReverseGrowth + Bending)
 *
 * 핵심 아이디어: "먼저 배치한 화살표의 Body가 나중 화살표의 탈출 경로를 막는다"
 * - 첫 번째 화살표: 즉시 탈출 가능
 * - 이후 화살표: 이전 화살표의 Body에 의해 막힘
 * - 다양한 방향(U/D/L/R) 사용
 *
 * v7 추가:
 * - 확장된 파라미터 범위
 * - 밀도 목표 기반 생성
 * - 빈 공간 필러
 * - Grid 크기 기반 Auto 계산
 *
 * v8 추가:
 * - ReverseGrowth 알고리즘 (꺾이는 화살표)
 * - "직진 선호 + 막히면 꺾음" 방식
 * - 높은 FillRate 달성 (70-90%)
 */

const GENERATOR_COLORS = ['R', 'G', 'Y', 'P'];
const DIRECTIONS = ['U', 'D', 'L', 'R'];

const DIR_VECTORS = {
    U: { dx: 0, dy: -1 },
    D: { dx: 0, dy: 1 },
    L: { dx: -1, dy: 0 },
    R: { dx: 1, dy: 0 }
};

// ReverseGrowth: 방향 전환 우선순위 (직진 > 좌/우, 뒤로 가기 금지)
const TURN_PRIORITY = {
    U: ['U', 'L', 'R'],  // 위 → 위/좌/우 (아래 금지)
    D: ['D', 'R', 'L'],  // 아래 → 아래/우/좌 (위 금지)
    L: ['L', 'D', 'U'],  // 왼쪽 → 왼/아래/위 (오른쪽 금지)
    R: ['R', 'U', 'D']   // 오른쪽 → 오른/위/아래 (왼쪽 금지)
};

// 반대 방향 매핑
const OPPOSITE = { U: 'D', D: 'U', L: 'R', R: 'L' };

// 확장된 파라미터 범위 (v8: 더 긴 화살표 지원)
const PARAM_RANGES = {
    gridSize: { min: 5, max: 12 },
    laneCount: { min: 1, max: 6 },
    balloonsPerLane: { min: 1, max: 8 },
    missArrowCount: { min: 0, max: 10 },
    minBlockLength: { min: 1, max: 8 },
    maxBlockLength: { min: 2, max: 20 },   // Bending 모드에서 20칸까지 가능
    targetDensity: { min: 0.2, max: 0.8 }
};

const DEFAULT_CONFIG = {
    // 그리드
    gridSize: 8,

    // Queue 설정
    laneCount: 2,
    balloonsPerLane: 2,
    missArrowCount: 1,

    // 화살표 길이 (v8: Bending 모드에서 더 긴 화살표)
    minBlockLength: 3,
    maxBlockLength: 8,

    // 밀도 제어
    targetDensity: 0.5,       // 목표 밀도 (0.2 ~ 0.8)
    densityMode: 'fill',      // 'auto' | 'manual' | 'fill'

    // 필러 설정
    fillerEnabled: true,
    fillerMinLength: 2,
    fillerMaxLength: 5,

    // ReverseGrowth (꺾이는 화살표) 설정
    bendingEnabled: true,     // 꺾이는 화살표 사용 여부
    bendingChance: 1.0,       // 꺾이는 화살표 비율 (0~1, 1.0 = 모두 꺾이는 방식)

    // 기존 설정
    branchingMode: false,
    branchingChance: 0.4
};

/**
 * Grid 크기 기반 Auto 파라미터 계산 (v8: 적은 수의 긴 화살표)
 * @param {number} gridSize - 그리드 크기
 * @param {number} targetDensity - 목표 밀도 (선택적)
 * @param {boolean} bendingEnabled - 꺾이는 화살표 사용 여부
 * @returns {object} 자동 계산된 파라미터
 */
function calculateAutoParams(gridSize, targetDensity = 0.5, bendingEnabled = true) {
    const totalCells = gridSize * gridSize;

    // 그리드 크기별 기본 설정 (v8: 적은 화살표, 긴 길이)
    const sizeCategory = gridSize <= 6 ? 'small' : gridSize <= 9 ? 'medium' : 'large';

    // Bending 모드: 긴 화살표 + 적당한 개수 (밸런스 조정)
    const presets = bendingEnabled ? {
        small: {  // 5-6 (25-36셀)
            laneCount: 2,
            balloonsPerLane: 2,
            missArrowCount: 1,
            minBlockLength: 3,
            maxBlockLength: 6
        },
        medium: { // 7-9 (49-81셀)
            laneCount: 2,
            balloonsPerLane: 3,
            missArrowCount: 2,
            minBlockLength: 4,
            maxBlockLength: 8
        },
        large: {  // 10-12 (100-144셀)
            laneCount: 3,
            balloonsPerLane: 3,
            missArrowCount: 3,
            minBlockLength: 5,
            maxBlockLength: 10
        }
    } : {
        // 직선 화살표 모드
        small: {  // 5-6
            laneCount: 2,
            balloonsPerLane: 2,
            missArrowCount: 1,
            minBlockLength: 2,
            maxBlockLength: 4
        },
        medium: { // 7-9
            laneCount: 2,
            balloonsPerLane: 3,
            missArrowCount: 2,
            minBlockLength: 2,
            maxBlockLength: 5
        },
        large: {  // 10-12
            laneCount: 3,
            balloonsPerLane: 3,
            missArrowCount: 3,
            minBlockLength: 3,
            maxBlockLength: 6
        }
    };

    const preset = presets[sizeCategory];

    // 목표 밀도에 맞춰 조정
    const avgLength = (preset.minBlockLength + preset.maxBlockLength) / 2;
    const baseArrowCount = preset.laneCount * preset.balloonsPerLane + preset.missArrowCount;
    const baseDensity = (baseArrowCount * avgLength) / totalCells;

    // 밀도 조정이 필요한 경우
    if (targetDensity > baseDensity + 0.15) {
        // 밀도를 높여야 함 - Bending 모드에서는 길이 증가 우선
        if (bendingEnabled) {
            preset.maxBlockLength = Math.min(preset.maxBlockLength + 3, 20);
        } else {
            // 직선 모드에서는 화살표 수 약간 증가
            preset.missArrowCount += Math.min(2, 5 - preset.missArrowCount);
        }
    }

    // 필러 설정도 Bending 모드에서는 더 길게
    const fillerMaxLen = bendingEnabled ? Math.min(6, preset.maxBlockLength - 2) : Math.min(3, preset.maxBlockLength - 1);

    return {
        ...preset,
        targetDensity: targetDensity,
        fillerEnabled: true,
        fillerMinLength: bendingEnabled ? 2 : 1,
        fillerMaxLength: Math.max(2, fillerMaxLen)
    };
}

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

// ============================================================
// ReverseGrowth: 꺾이는 화살표 생성 알고리즘
// "직진을 선호하지만, 막히면 옆으로 꺾는다"
// ============================================================

/**
 * 다음 성장 셀 찾기 (우선순위: 직진 > 좌/우 랜덤 > 실패)
 * @param {number} x - 현재 x 좌표
 * @param {number} y - 현재 y 좌표
 * @param {string} preferredDir - 선호 방향 (현재 진행 방향)
 * @param {Set} occupiedSet - 점유된 셀들
 * @param {number} gridSize - 그리드 크기
 * @returns {object|null} { x, y, dir } 또는 null
 */
function findNextGrowthCell(x, y, preferredDir, occupiedSet, gridSize) {
    const priority = TURN_PRIORITY[preferredDir];
    // 직진 우선, 좌우는 랜덤 순서
    const shuffledTurns = shuffle([priority[1], priority[2]]);
    const searchOrder = [priority[0], ...shuffledTurns];

    for (const dir of searchOrder) {
        const d = DIR_VECTORS[dir];
        const nx = x + d.dx;
        const ny = y + d.dy;

        if (isInBounds(nx, ny, gridSize) && !occupiedSet.has(`${nx},${ny}`)) {
            return { x: nx, y: ny, dir };
        }
    }
    return null;  // 모든 방향 막힘
}

/**
 * ReverseGrowth 방식으로 화살표 경로 생성
 * Head에서 시작하여 반대 방향으로 Body를 성장
 * @param {number} headX - Head의 x 좌표
 * @param {number} headY - Head의 y 좌표
 * @param {string} headDir - Head의 탈출 방향 (U/D/L/R)
 * @param {number} targetLength - 목표 길이
 * @param {Set} occupiedSet - 점유된 셀들
 * @param {number} gridSize - 그리드 크기
 * @returns {object|null} { path: [{x,y}], headDir: string } 또는 null
 */
function growArrowReverse(headX, headY, headDir, targetLength, occupiedSet, gridSize) {
    const path = [{ x: headX, y: headY }];

    // 성장 방향 = 탈출 방향의 반대
    let currentDir = OPPOSITE[headDir];
    let currentX = headX;
    let currentY = headY;

    for (let i = 1; i < targetLength; i++) {
        const next = findNextGrowthCell(currentX, currentY, currentDir, occupiedSet, gridSize);

        if (!next) {
            // 더 이상 성장 불가 - 현재까지의 경로 반환
            break;
        }

        path.push({ x: next.x, y: next.y });
        currentX = next.x;
        currentY = next.y;
        currentDir = next.dir;  // 꺾였으면 방향 변경
    }

    // 최소 2칸 이상이어야 유효
    if (path.length >= 2) {
        return { path, headDir };
    }
    return null;
}

/**
 * 그리드 가장자리 위치 반환 (해당 방향으로 즉시 탈출 가능한 위치들)
 * @param {string} dir - 탈출 방향
 * @param {number} gridSize - 그리드 크기
 * @returns {Array} [{x, y}, ...]
 */
function getEdgePositions(dir, gridSize) {
    const positions = [];

    switch (dir) {
        case 'U':  // 위로 탈출 → y=0 라인
            for (let x = 0; x < gridSize; x++) positions.push({ x, y: 0 });
            break;
        case 'D':  // 아래로 탈출 → y=gridSize-1 라인
            for (let x = 0; x < gridSize; x++) positions.push({ x, y: gridSize - 1 });
            break;
        case 'L':  // 왼쪽으로 탈출 → x=0 라인
            for (let y = 0; y < gridSize; y++) positions.push({ x: 0, y });
            break;
        case 'R':  // 오른쪽으로 탈출 → x=gridSize-1 라인
            for (let y = 0; y < gridSize; y++) positions.push({ x: gridSize - 1, y });
            break;
    }

    return shuffle(positions);
}

/**
 * 꺾이는 첫 번째 화살표 배치 (즉시 탈출 가능)
 */
function placeFirstArrowBending(color, length, gridSize, occupiedSet) {
    const candidates = [];
    const dirs = shuffle(DIRECTIONS);

    for (const headDir of dirs) {
        // 해당 방향으로 탈출 가능한 가장자리 위치들
        const edgePositions = getEdgePositions(headDir, gridSize);

        for (const { x, y } of edgePositions) {
            if (occupiedSet.has(`${x},${y}`)) continue;

            // ReverseGrowth 시도
            const result = growArrowReverse(x, y, headDir, length, occupiedSet, gridSize);

            if (result && result.path.length >= Math.min(length, 2)) {
                // 탈출 경로 확인 (Head 앞에 장애물 없는지)
                const escapePath = getEscapePath(x, y, headDir, gridSize);
                const blocked = escapePath.some(p => occupiedSet.has(`${p.x},${p.y}`));

                if (!blocked) {
                    candidates.push({
                        x, y,
                        headDir: result.headDir,
                        path: result.path,
                        cells: result.path  // cells = path
                    });
                }
            }
        }
    }

    if (candidates.length === 0) return null;
    return randomPick(candidates);
}

/**
 * 꺾이는 화살표를 이전 화살표에 의해 막히는 위치에 배치
 */
function findBlockedPositionBending(color, length, gridSize, occupiedSet, blockerCells) {
    const candidates = [];
    const blockerSet = new Set(blockerCells.map(c => `${c.x},${c.y}`));

    for (let x = 0; x < gridSize; x++) {
        for (let y = 0; y < gridSize; y++) {
            if (occupiedSet.has(`${x},${y}`)) continue;

            for (const headDir of shuffle(DIRECTIONS)) {
                // ReverseGrowth 시도
                const result = growArrowReverse(x, y, headDir, length, occupiedSet, gridSize);

                if (!result || result.path.length < Math.min(length, 2)) continue;

                // 탈출 경로가 blocker를 지나가는지 확인
                const escapePath = getEscapePath(x, y, headDir, gridSize);
                const blockedByBlocker = escapePath.some(p => blockerSet.has(`${p.x},${p.y}`));

                if (blockedByBlocker) {
                    // 다른 화살표(blocker 제외)에 의해 추가로 막히지 않는지 확인
                    const otherOccupied = new Set([...occupiedSet].filter(k => !blockerSet.has(k)));
                    const blockedByOthers = escapePath.some(p => otherOccupied.has(`${p.x},${p.y}`));

                    if (!blockedByOthers) {
                        candidates.push({
                            x, y,
                            headDir: result.headDir,
                            path: result.path,
                            cells: result.path
                        });
                    }
                }
            }
        }
    }

    if (candidates.length === 0) return null;
    return randomPick(candidates);
}

/**
 * 꺾이는 화살표 폴백: 아무 곳에나 배치
 */
function placeFallbackBending(color, length, gridSize, occupiedSet) {
    const candidates = [];

    for (let x = 0; x < gridSize; x++) {
        for (let y = 0; y < gridSize; y++) {
            if (occupiedSet.has(`${x},${y}`)) continue;

            for (const headDir of shuffle(DIRECTIONS)) {
                const result = growArrowReverse(x, y, headDir, length, occupiedSet, gridSize);

                if (result && result.path.length >= Math.min(length, 2)) {
                    candidates.push({
                        x, y,
                        headDir: result.headDir,
                        path: result.path,
                        cells: result.path
                    });
                }
            }
        }
    }

    if (candidates.length === 0) return null;
    return randomPick(candidates);
}

/**
 * 빈 공간 필러 배치
 * 목표 밀도에 도달할 때까지 빈 공간에 추가 화살표 배치
 * @param {Array} blocks - 기존 배치된 블록들
 * @param {Set} occupiedSet - 점유된 셀 Set
 * @param {object} cfg - 설정
 * @returns {Array} 추가된 필러 블록들
 */
function placeFillersForDensity(blocks, occupiedSet, cfg) {
    const fillers = [];
    const totalCells = cfg.gridSize * cfg.gridSize;
    const targetOccupied = Math.floor(totalCells * cfg.targetDensity);

    let currentOccupied = occupiedSet.size;
    let attempts = 0;
    const maxAttempts = 100;

    const useBending = cfg.bendingEnabled && cfg.bendingChance > 0;

    console.log(`  Filler: Current density ${(currentOccupied / totalCells * 100).toFixed(1)}%, target ${(cfg.targetDensity * 100).toFixed(1)}%`);
    console.log(`  Filler: Using ${useBending ? 'bending' : 'straight'} mode`);

    while (currentOccupied < targetOccupied && attempts < maxAttempts) {
        attempts++;

        // 랜덤 색상, 길이 선택
        const color = randomPick(GENERATOR_COLORS);
        const length = randomInt(cfg.fillerMinLength, cfg.fillerMaxLength);

        // Bending 모드에 따라 다른 배치 함수 사용
        const placement = useBending
            ? placeFallbackBending(color, length, cfg.gridSize, occupiedSet)
            : placeFallback(color, length, cfg.gridSize, occupiedSet);

        if (placement) {
            const isBending = !!placement.path;
            const fillerBlock = {
                x: placement.x,
                y: placement.y,
                c: color,
                d: isBending ? placement.headDir : placement.dir,
                l: placement.cells.length,  // 실제 길이 사용
                cells: placement.cells,
                isFiller: true,
                path: isBending ? placement.path : null,
                isBending: isBending
            };

            fillers.push(fillerBlock);

            // 점유 셀 업데이트
            for (const c of placement.cells) {
                occupiedSet.add(`${c.x},${c.y}`);
            }

            currentOccupied = occupiedSet.size;
            const bendLabel = isBending ? ' [bending]' : '';
            console.log(`  Filler ${fillers.length}: ${color} at (${placement.x},${placement.y}) dir=${fillerBlock.d} len=${fillerBlock.l}${bendLabel}`);
        }
    }

    console.log(`  Filler: Added ${fillers.length} fillers, final density ${(currentOccupied / totalCells * 100).toFixed(1)}%`);

    return fillers;
}

/**
 * 현재 밀도 계산
 */
function calculateDensity(occupiedSet, gridSize) {
    return occupiedSet.size / (gridSize * gridSize);
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
    let cfg = { ...DEFAULT_CONFIG, ...config };

    // densityMode에 따른 처리
    if (cfg.densityMode === 'auto') {
        // Auto 모드: 목표 밀도에 맞춰 화살표 수 자동 조정
        const autoParams = calculateAutoParams(cfg.gridSize, cfg.targetDensity, cfg.bendingEnabled);
        cfg = { ...cfg, ...autoParams };
        console.log("Auto mode: Calculated params", autoParams);
    }

    console.log("=== Generator v8 (ReverseGrowth) ===");
    console.log(`Bending: ${cfg.bendingEnabled ? 'enabled' : 'disabled'} (chance: ${cfg.bendingChance})`);
    console.log("Config:", cfg);
    console.log(`Target density: ${(cfg.targetDensity * 100).toFixed(1)}%`);

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
            const useBending = cfg.bendingEnabled && Math.random() < cfg.bendingChance;

            if (i === 0) {
                // 첫 번째: 즉시 탈출 가능한 위치
                if (useBending) {
                    placement = placeFirstArrowBending(color, length, cfg.gridSize, occupiedSet);
                } else {
                    placement = placeFirstArrow(color, length, cfg.gridSize, occupiedSet);
                }
            } else {
                // 분기 모드: 일정 확률로 폴백(자유 배치) 사용
                const useBranching = cfg.branchingMode && Math.random() < cfg.branchingChance;

                if (useBranching) {
                    // 분기 모드: 막히지 않는 자유로운 위치에 배치
                    console.log(`  Arrow ${i}: Branching mode - free placement`);
                    if (useBending) {
                        placement = placeFirstArrowBending(color, length, cfg.gridSize, occupiedSet);
                    } else {
                        placement = placeFirstArrow(color, length, cfg.gridSize, occupiedSet);
                    }
                } else {
                    // 일반 모드: 이전 화살표에 의해 막히는 위치
                    const prevBlock = blocks[i - 1];
                    if (useBending) {
                        placement = findBlockedPositionBending(color, length, cfg.gridSize, occupiedSet, prevBlock.cells);
                    } else {
                        placement = findBlockedPosition(color, length, cfg.gridSize, occupiedSet, prevBlock.cells);
                    }
                }

                // 못 찾으면 폴백
                if (!placement) {
                    console.log(`  Arrow ${i}: Fallback (couldn't find blocked position)`);
                    if (useBending) {
                        placement = placeFallbackBending(color, length, cfg.gridSize, occupiedSet);
                    } else {
                        placement = placeFallback(color, length, cfg.gridSize, occupiedSet);
                    }
                }
            }

            if (!placement) {
                console.log(`  Arrow ${i}: Failed to place`);
                success = false;
                break;
            }

            // 배치 - 꺾이는 화살표는 headDir을 d로, path를 저장
            const isBending = !!placement.path;
            const block = {
                x: placement.x,
                y: placement.y,
                c: color,
                d: isBending ? placement.headDir : placement.dir,  // headDir을 d로 저장 (호환성)
                l: placement.cells.length,  // 실제 길이
                cells: placement.cells,
                path: isBending ? placement.path : null,  // 꺾이는 화살표만 path 저장
                isBending: isBending
            };
            blocks.push(block);

            // 점유 셀 업데이트
            for (const c of placement.cells) {
                occupiedSet.add(`${c.x},${c.y}`);
            }

            const bendingLabel = isBending ? ' [bending]' : '';
            console.log(`  Arrow ${i}: ${color} at (${placement.x},${placement.y}) dir=${block.d} len=${block.l}${bendingLabel}`);
        }

        if (!success) continue;

        // Step 4: 필러 추가 (fill 모드 또는 fillerEnabled)
        let allBlocks = [...blocks];
        const mainBlockCount = blocks.length;

        if (cfg.fillerEnabled && (cfg.densityMode === 'fill' || cfg.densityMode === 'auto')) {
            const currentDensity = calculateDensity(occupiedSet, cfg.gridSize);
            console.log(`  Current density before filler: ${(currentDensity * 100).toFixed(1)}%`);

            if (currentDensity < cfg.targetDensity) {
                const fillers = placeFillersForDensity(allBlocks, occupiedSet, cfg);
                allBlocks = [...blocks, ...fillers];
            }
        }

        // Step 5: 검증
        const validation = validateGeneratedLevel(allBlocks, lanes, cfg.gridSize);
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

            const cleanBlocks = allBlocks.map((b, idx) => {
                const block = {
                    x: b.x,
                    y: b.y,
                    c: b.c,
                    d: b.d,
                    l: b.l,
                    order: orderMap.get(idx) || 0,  // 실제 탈출 순서
                    isFiller: b.isFiller || false   // 필러 여부
                };

                // 꺾이는 화살표는 path 포함
                if (b.path) {
                    block.path = b.path;
                    block.isBending = true;
                }

                return block;
            });

            const finalDensity = calculateDensity(occupiedSet, cfg.gridSize);
            console.log("=== Generation Successful! ===");
            console.log(`Final density: ${(finalDensity * 100).toFixed(1)}%`);
            console.log(`Main arrows: ${mainBlockCount}, Fillers: ${allBlocks.length - mainBlockCount}`);
            console.log("Escape sequence:", escapeSequence.map((idx, i) =>
                `${i + 1}: Block ${idx} (${allBlocks[idx].c})`
            ).join(', '));

            return {
                name: `Gen.${Date.now() % 10000}`,
                size: cfg.gridSize,
                lanes: lanes,
                blocks: cleanBlocks,
                stats: {
                    density: finalDensity,
                    mainArrows: mainBlockCount,
                    fillers: allBlocks.length - mainBlockCount,
                    totalArrows: allBlocks.length
                }
            };
        }
    }

    throw new Error("Failed to generate valid level after max attempts");
}

// Export
window.LevelGenerator = {
    generateLevel,
    calculateAutoParams,
    growArrowReverse,  // 테스트용 export
    DEFAULT_CONFIG,
    PARAM_RANGES
};

console.log("Generator v8 (ReverseGrowth + Bending) loaded. Use LevelGenerator.generateLevel()");
