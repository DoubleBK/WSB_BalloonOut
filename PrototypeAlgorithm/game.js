const DIRS = {
    U: { dx: 0, dy: -1, deg: 0 },
    D: { dx: 0, dy: 1, deg: 180 },
    L: { dx: -1, dy: 0, deg: 270 },
    R: { dx: 1, dy: 0, deg: 90 }
};

const COLORS = { R: '#ff4757', G: '#2ed573', Y: '#ffa502', D: '#5352ed', P: '#8e44ad' };

// Snake 이동 설정
const SNAKE_CONFIG = {
    stepDuration: 80,      // 한 칸 이동 시간 (ms)
    escapeDuration: 60,    // 탈출 시 한 칸 시간 (ms)
    bounceDuration: 100,   // 바운스 시간 (ms)
    bounceDistance: 0.3    // 바운스 거리 (셀 비율)
};

// 레벨은 Generator로 생성
const LEVELS = [];

let curLevelIdx = 0;
let blocks = [];
let queues = [];
let gridSize = 4;
let isAnim = false;

function validateLevel(lvl, blocks) {
    const occ = new Set();
    for (let b of blocks) {
        if (!b.cells || !Array.isArray(b.cells)) {
            console.warn("Block missing cells:", b);
            continue;
        }
        for (let c of b.cells) {
            const key = `${c.x},${c.y}`;
            if (c.x < 0 || c.x >= lvl.size || c.y < 0 || c.y >= lvl.size) {
                console.error(`OOB Error in ${lvl.name}: Block ${b.id}`, c);
                alert(`Level Design Error: Block Out of Bounds in ${lvl.name}`);
                return;
            }
            if (occ.has(key)) {
                console.error(`Overlap Error in ${lvl.name}: ${key} is taken.`);
                alert(`Level Design Error: Overlap at ${key} in ${lvl.name}`);
                return;
            }
            occ.add(key);
        }
    }
}

let solutionOrder = [];  // 풀이 순서 저장

function initLevel(idx) {
    curLevelIdx = idx % LEVELS.length;
    const lvl = LEVELS[curLevelIdx];
    gridSize = lvl.size;

    document.getElementById('lvl-title').innerText = lvl.name;
    document.getElementById('q-len').innerText = lvl.lanes.flat().length;
    document.getElementById('g-size').innerText = `${gridSize}x${gridSize}`;

    const gBox = document.getElementById('grid-box');
    gBox.style.gridTemplateColumns = `repeat(${gridSize}, var(--cell-size))`;
    gBox.style.gridTemplateRows = `repeat(${gridSize}, var(--cell-size))`;

    queues = JSON.parse(JSON.stringify(lvl.lanes));
    renderQueue();

    blocks = [];
    solutionOrder = [];  // 초기화

    lvl.blocks.forEach((b, i) => {
        let cells;

        if (b.path) {
            // 꺾이는 화살표: path를 직접 cells로 사용
            cells = b.path.map(p => ({ x: p.x, y: p.y }));
        } else {
            // 직선 화살표: 기존 방식으로 계산
            cells = [];
            const dParams = DIRS[b.d];
            for (let k = 0; k < b.l; k++) {
                cells.push({
                    x: b.x - (dParams.dx * k),
                    y: b.y - (dParams.dy * k)
                });
            }
        }

        const block = { ...b, id: i, cells: cells, state: 'normal' };
        blocks.push(block);

        // 순서가 있는 경우 풀이 순서에 추가
        if (b.order) {
            solutionOrder.push({ order: b.order, color: b.c, id: i });
        }
    });

    // 순서대로 정렬
    solutionOrder.sort((a, b) => a.order - b.order);

    validateLevel(lvl, blocks);

    renderGrid();
    renderSolution();
    updateRestartButton();
    updateState();
    document.getElementById('end-screen').style.display = 'none';
}

function renderSolution() {
    const box = document.getElementById('solution-box');
    const seq = document.getElementById('solution-sequence');

    if (solutionOrder.length === 0) {
        box.classList.remove('visible');
        return;
    }

    box.classList.add('visible');
    seq.innerHTML = '';

    solutionOrder.forEach(item => {
        const el = document.createElement('div');
        el.className = `solution-item c-${item.color}`;
        el.id = `sol-${item.id}`;
        el.innerText = item.order;
        seq.appendChild(el);
    });
}

function renderQueue() {
    const box = document.getElementById('queue-box');
    box.style.display = 'flex';
    box.innerHTML = '';
    queues.forEach(lane => {
        const ln = document.createElement('div');
        ln.className = 'lane';
        lane.forEach((c, idx) => {
            const bal = document.createElement('div');
            bal.className = `balloon c-${c}`;
            if (idx === lane.length - 1) bal.classList.add('target-head');
            ln.appendChild(bal);
        });
        box.appendChild(ln);
    });
}

function renderGrid() {
    const box = document.getElementById('grid-box');
    box.innerHTML = '';

    for (let i = 0; i < gridSize * gridSize; i++) {
        const c = document.createElement('div');
        c.className = 'grid-cell';
        box.appendChild(c);
    }

    const cellSize = 50; // CSS --cell-size

    blocks.forEach(b => {
        const grp = document.createElement('div');
        grp.className = 'block';
        grp.id = `blk-${b.id}`;

        // DOM 요소 참조 배열 초기화
        b.elements = [];

        if (!b.cells || !Array.isArray(b.cells)) return;
        b.cells.forEach((cell, idx) => {
            const u = document.createElement('div');
            u.className = `block-unit c-${b.c} ${idx === 0 ? 'head' : 'body'}`;

            // 위치를 px 단위로 설정 (애니메이션을 위해)
            u.style.left = `${cell.x * cellSize}px`;
            u.style.top = `${cell.y * cellSize}px`;

            if (idx === 0) {
                const d = DIRS[b.d];
                u.style.transform = `rotate(${d.deg}deg)`;

                // 순서 번호 표시 (생성된 레벨만)
                if (b.order) {
                    const num = document.createElement('span');
                    num.className = 'arrow-order';
                    num.innerText = b.order;
                    u.appendChild(num);
                }
            }

            u.onclick = (e) => { e.stopPropagation(); tryEscape(b); };
            grp.appendChild(u);

            // DOM 요소 참조 저장
            b.elements.push(u);
        });
        box.appendChild(grp);
    });
}

function updateState() {
    // 모든 화살표가 상호작용 가능 (비활성화 상태 없음)
    // 충돌 여부만 계산하여 저장
    const occ = new Set();
    blocks.forEach(b => {
        if (b.cells && Array.isArray(b.cells)) {
            b.cells.forEach(c => occ.add(`${c.x},${c.y}`));
        }
    });

    blocks.forEach(b => {
        if (!b.cells || b.cells.length === 0) return;
        const d = DIRS[b.d];
        let cx = b.cells[0].x + d.dx;
        let cy = b.cells[0].y + d.dy;
        let blocked = false;

        while (cx >= 0 && cx < gridSize && cy >= 0 && cy < gridSize) {
            if (occ.has(`${cx},${cy}`)) {
                blocked = true;
                break;
            }
            cx += d.dx;
            cy += d.dy;
        }

        b.blocked = blocked;  // 충돌 여부 저장

        // 모든 화살표 활성화 상태로 표시
        const el = document.getElementById(`blk-${b.id}`);
        if (el) {
            el.querySelectorAll('.block-unit').forEach(u => {
                u.classList.remove('locked');
                u.classList.add('escapable');
            });
        }
    });

    if (queues.flat().length === 0) showEnd(true);
}

// 충돌 지점까지의 거리 계산 (셀 단위)
function getDistanceToCollision(block) {
    const occ = new Set();
    blocks.forEach(b => {
        if (b.id !== block.id && b.cells && Array.isArray(b.cells)) {
            b.cells.forEach(c => occ.add(`${c.x},${c.y}`));
        }
    });

    if (!block.cells || block.cells.length === 0) return { blocked: true, distance: 0 };

    const d = DIRS[block.d];
    const head = block.cells[0];
    let cx = head.x + d.dx;
    let cy = head.y + d.dy;
    let distance = 0;

    while (cx >= 0 && cx < gridSize && cy >= 0 && cy < gridSize) {
        if (occ.has(`${cx},${cy}`)) {
            // 충돌 지점 발견
            return { blocked: true, distance: distance };
        }
        distance++;
        cx += d.dx;
        cy += d.dy;
    }

    // 그리드 끝까지 충돌 없음
    return { blocked: false, distance: distance };
}

// ============================================================
// Snake Movement System
// ============================================================

/**
 * 경계 체크
 */
function isInGridBounds(x, y) {
    return x >= 0 && x < gridSize && y >= 0 && y < gridSize;
}

/**
 * 현재 화살표를 제외한 점유 셀 목록
 */
function getOccupiedCells(excludeBlockId) {
    const occ = new Set();
    blocks.forEach(b => {
        if (b.id !== excludeBlockId && b.cells && Array.isArray(b.cells)) {
            b.cells.forEach(c => occ.add(`${c.x},${c.y}`));
        }
    });
    return occ;
}

/**
 * 한 칸 이동 가능 여부 확인 (Snake 방식)
 * @returns {object} { canMove, escaped, blocked, newHead }
 */
function stepMove(block) {
    if (!block.cells || block.cells.length === 0) {
        return { canMove: false, escaped: false, blocked: true };
    }

    const head = block.cells[0];
    const dir = DIRS[block.d];

    // 새 머리 위치
    const newHeadX = head.x + dir.dx;
    const newHeadY = head.y + dir.dy;

    // 1. 경계 체크 - 머리가 밖으로 나가면 탈출 시작
    if (!isInGridBounds(newHeadX, newHeadY)) {
        return { canMove: true, escaped: true, blocked: false };
    }

    // 2. 충돌 체크 - 다른 화살표와 겹치면 막힘
    const occupied = getOccupiedCells(block.id);
    if (occupied.has(`${newHeadX},${newHeadY}`)) {
        return { canMove: false, escaped: false, blocked: true };
    }

    // 3. 이동 가능
    return {
        canMove: true,
        escaped: false,
        blocked: false,
        newHead: { x: newHeadX, y: newHeadY }
    };
}

/**
 * 셀 배열 업데이트 (이동 후)
 * @param {object} block - 블록 객체
 * @param {object} newHead - 새 머리 위치 (탈출 중이면 null)
 * @param {boolean} escaping - 탈출 중 여부
 * @returns {Array} 이전 셀 위치 (애니메이션용)
 */
function updateCellsAfterStep(block, newHead, escaping) {
    const oldCells = block.cells.map(c => ({ x: c.x, y: c.y }));

    if (escaping) {
        // 탈출 중: 꼬리만 제거
        block.cells.pop();
    } else {
        // 일반 이동: 새 머리 추가, 꼬리 제거
        block.cells.unshift(newHead);  // 새 머리 추가 (앞에)
        block.cells.pop();              // 꼬리 제거 (뒤에서)
    }

    return oldCells;
}

/**
 * 단계별 슬라이딩 애니메이션 (Promise 기반)
 */
function animateStepAsync(block, duration) {
    return new Promise(resolve => {
        const cellSize = 50;

        // 각 셀의 DOM 요소 위치 업데이트
        block.cells.forEach((cell, idx) => {
            if (block.elements[idx]) {
                const el = block.elements[idx];
                el.style.transition = `left ${duration}ms ease-out, top ${duration}ms ease-out`;
                el.style.left = `${cell.x * cellSize}px`;
                el.style.top = `${cell.y * cellSize}px`;
            }
        });

        setTimeout(() => {
            // 트랜지션 제거
            block.elements.forEach(el => {
                if (el) el.style.transition = '';
            });
            resolve();
        }, duration);
    });
}

/**
 * 꼬리 제거 애니메이션 (Promise 기반)
 */
function animateTailRemovalAsync(tailElement, duration) {
    return new Promise(resolve => {
        if (!tailElement) {
            resolve();
            return;
        }

        tailElement.style.transition = `opacity ${duration}ms, transform ${duration}ms`;
        tailElement.style.opacity = '0';
        tailElement.style.transform += ' scale(0.5)';

        setTimeout(() => {
            if (tailElement.parentNode) {
                tailElement.parentNode.removeChild(tailElement);
            }
            resolve();
        }, duration);
    });
}

/**
 * 충돌 바운스 애니메이션 (Promise 기반)
 */
function animateBounce(block) {
    return new Promise(resolve => {
        const dir = DIRS[block.d];
        const cellSize = 50;
        const bounceDistance = cellSize * SNAKE_CONFIG.bounceDistance;
        const duration = SNAKE_CONFIG.bounceDuration;

        const grp = document.getElementById(`blk-${block.id}`);
        if (!grp) {
            resolve();
            return;
        }

        // 살짝 앞으로 이동
        grp.style.transition = `transform ${duration * 0.4}ms ease-out`;
        grp.style.transform = `translate(${dir.dx * bounceDistance}px, ${dir.dy * bounceDistance}px)`;

        setTimeout(() => {
            // 원위치로 복귀
            grp.style.transition = `transform ${duration * 0.6}ms ease-in`;
            grp.style.transform = 'translate(0, 0)';

            setTimeout(() => {
                grp.style.transition = '';
                resolve();
            }, duration * 0.6);
        }, duration * 0.4);
    });
}

/**
 * 탈출 시퀀스 - 셀들이 하나씩 경계 밖으로 나감
 */
async function escapeSequence(block) {
    const cellSize = 50;
    const dir = DIRS[block.d];
    const escapeDuration = SNAKE_CONFIG.escapeDuration;

    // 머리가 밖으로 나간 상태에서 시작
    // 각 셀이 순서대로 밖으로 이동
    while (block.cells.length > 0) {
        // 모든 셀을 한 칸 앞으로 이동
        block.cells.forEach(cell => {
            cell.x += dir.dx;
            cell.y += dir.dy;
        });

        // 애니메이션 (현재 남은 셀들)
        block.cells.forEach((cell, idx) => {
            if (block.elements[idx]) {
                const el = block.elements[idx];
                el.style.transition = `left ${escapeDuration}ms ease-in, top ${escapeDuration}ms ease-in`;
                el.style.left = `${cell.x * cellSize}px`;
                el.style.top = `${cell.y * cellSize}px`;
            }
        });

        await new Promise(r => setTimeout(r, escapeDuration));

        // 마지막 셀(꼬리) 제거
        const tailEl = block.elements.pop();
        block.cells.pop();

        if (tailEl && tailEl.parentNode) {
            tailEl.parentNode.removeChild(tailEl);
        }
    }
}

/**
 * 역방향 이동 애니메이션 (충돌 후 원위치 복귀)
 */
async function animateReverseMovement(block, moveHistory, stepDuration) {
    // 히스토리를 역순으로 순회하며 원위치로 복귀
    for (let i = moveHistory.length - 1; i >= 0; i--) {
        const prevCells = moveHistory[i];

        // cells 배열 복원
        block.cells = prevCells.map(c => ({ x: c.x, y: c.y }));

        // 애니메이션
        await animateStepAsync(block, stepDuration);
    }
}

/**
 * Snake 방식 탈출 시도 (async/await 기반)
 */
async function tryEscapeSnake(b) {
    if (isAnim || b.state === 'moving') return;
    isAnim = true;
    b.state = 'moving';

    const stepDuration = SNAKE_CONFIG.stepDuration;

    // 이동 히스토리 (원위치 복귀용)
    const moveHistory = [];

    // 연속 이동 루프
    while (true) {
        const result = stepMove(b);

        if (result.blocked) {
            // 충돌! 이동한 만큼 역방향으로 복귀
            if (moveHistory.length > 0) {
                // 이동했다면 역방향으로 복귀
                await animateReverseMovement(b, moveHistory, stepDuration);
            } else {
                // 첫 칸에서 바로 막힘 - 바운스만
                await animateBounce(b);
            }
            b.state = 'normal';
            isAnim = false;
            showToast("BLOCKED!");
            return;
        }

        if (result.escaped) {
            // 탈출 시작 - 경계 밖으로 나감
            await escapeSequence(b);
            handleEscapeComplete(b);
            return;
        }

        // 이동 전 위치 저장 (복귀용)
        moveHistory.push(b.cells.map(c => ({ x: c.x, y: c.y })));

        // 한 칸 이동
        updateCellsAfterStep(b, result.newHead, false);
        await animateStepAsync(b, stepDuration);
    }
}

/**
 * 탈출 완료 처리
 */
function handleEscapeComplete(b) {
    // 블록 제거
    const el = document.getElementById(`blk-${b.id}`);
    if (el && el.parentNode) {
        el.parentNode.removeChild(el);
    }

    blocks = blocks.filter(x => x.id !== b.id);

    // 풀이 순서 UI 업데이트
    const solEl = document.getElementById(`sol-${b.id}`);
    if (solEl) solEl.classList.add('popped');

    // 큐에서 풍선 팝
    let hit = false;
    for (let lane of queues) {
        if (lane.length > 0) {
            if (lane[lane.length - 1] === b.c) {
                lane.pop();
                hit = true;
                break;
            }
        }
    }

    renderQueue();
    updateState();
    showToast(hit ? "POP!" : "FLY AWAY");
    isAnim = false;
}

function tryEscape(b) {
    // Snake 방식 이동 사용
    tryEscapeSnake(b);
}

function showToast(msg) {
    const t = document.getElementById('toast');
    t.innerText = msg;
    t.style.opacity = 1;
    setTimeout(() => t.style.opacity = 0, 800);
}
function showEnd() { document.getElementById('end-screen').style.display = 'flex'; }

// 레벨 재시작
function restartLevel() {
    if (LEVELS.length === 0) {
        showGeneratorUI();
        return;
    }
    initLevel(curLevelIdx);
    showToast("Restarted!");
}

// Restart 버튼 표시/숨김
function updateRestartButton() {
    const btn = document.getElementById('btn-restart');
    // 생성된 레벨인 경우에만 표시 (order가 있는 블록이 있으면 생성된 레벨)
    if (solutionOrder.length > 0) {
        btn.classList.add('visible');
    } else {
        btn.classList.remove('visible');
    }
}

// ============================================================
// Level Generator UI Integration (v7)
// ============================================================

function showGeneratorUI() {
    document.getElementById('gen-modal').style.display = 'flex';
    updateDensityLabel();
}

function closeGeneratorUI() {
    // 레벨이 없으면 모달을 닫지 않음
    if (LEVELS.length === 0) {
        showToast("Generate a puzzle first!");
        return;
    }
    document.getElementById('gen-modal').style.display = 'none';
}

// Density 슬라이더 라벨 업데이트
function updateDensityLabel() {
    const slider = document.getElementById('gen-density');
    const label = document.getElementById('density-label');
    label.textContent = `${slider.value}%`;
}

// Grid 크기 변경 시 Auto 값 업데이트
function onGridSizeChange() {
    updateAutoValues();
}

// 개별 Auto 체크박스 변경
function onAutoChange(param) {
    const checkbox = document.getElementById(`auto-${param}`);
    const inputMap = {
        'lanes': 'gen-lanes',
        'balloons': 'gen-balloons',
        'miss': 'gen-miss',
        'minLen': 'gen-min-len',
        'maxLen': 'gen-max-len'
    };

    const input = document.getElementById(inputMap[param]);
    if (input) {
        input.disabled = checkbox.checked;
    }

    if (checkbox.checked) {
        updateAutoValues();
    }

    // Auto All 상태 업데이트
    updateAutoAllState();
}

// Auto All 토글
function toggleAutoAll() {
    const autoAll = document.getElementById('auto-all').checked;
    const autoCheckboxes = ['auto-lanes', 'auto-balloons', 'auto-miss', 'auto-min-len', 'auto-max-len'];

    autoCheckboxes.forEach(id => {
        const cb = document.getElementById(id);
        if (cb) {
            cb.checked = autoAll;
            // 연결된 input 비활성화/활성화
            const param = id.replace('auto-', '');
            onAutoChange(param.replace('-', ''));
        }
    });

    if (autoAll) {
        updateAutoValues();
    }
}

// Auto All 체크박스 상태 동기화
function updateAutoAllState() {
    const autoCheckboxes = ['auto-lanes', 'auto-balloons', 'auto-miss', 'auto-min-len', 'auto-max-len'];
    const allChecked = autoCheckboxes.every(id => {
        const cb = document.getElementById(id);
        return cb && cb.checked;
    });
    document.getElementById('auto-all').checked = allChecked;
}

// Auto 값 계산 및 UI 업데이트
function updateAutoValues() {
    const gridSize = parseInt(document.getElementById('gen-size').value) || 8;
    const targetDensity = parseInt(document.getElementById('gen-density').value) / 100;
    const bendingEnabled = document.getElementById('gen-bending').checked;

    // Generator의 Auto 계산 함수 사용 (v8: bendingEnabled 전달)
    const autoParams = LevelGenerator.calculateAutoParams(gridSize, targetDensity, bendingEnabled);

    // Auto가 체크된 필드만 업데이트
    if (document.getElementById('auto-lanes').checked) {
        document.getElementById('gen-lanes').value = autoParams.laneCount;
    }
    if (document.getElementById('auto-balloons').checked) {
        document.getElementById('gen-balloons').value = autoParams.balloonsPerLane;
    }
    if (document.getElementById('auto-miss').checked) {
        document.getElementById('gen-miss').value = autoParams.missArrowCount;
    }
    if (document.getElementById('auto-min-len').checked) {
        document.getElementById('gen-min-len').value = autoParams.minBlockLength;
    }
    if (document.getElementById('auto-max-len').checked) {
        document.getElementById('gen-max-len').value = autoParams.maxBlockLength;
    }
}

function runGenerator() {
    const branchingMode = document.getElementById('gen-branching').checked;
    const fillerEnabled = document.getElementById('gen-filler').checked;
    const bendingEnabled = document.getElementById('gen-bending').checked;
    const targetDensity = parseInt(document.getElementById('gen-density').value) / 100;

    const gridSize = parseInt(document.getElementById('gen-size').value) || 8;

    const maxLen = parseInt(document.getElementById('gen-max-len').value) || 8;

    // Bending 모드에서는 더 긴 필러 사용
    const fillerMin = bendingEnabled ? 2 : 1;
    const fillerMax = bendingEnabled ? Math.min(6, maxLen - 2) : Math.min(3, maxLen - 1);

    const config = {
        gridSize: gridSize,
        laneCount: parseInt(document.getElementById('gen-lanes').value) || 2,
        balloonsPerLane: parseInt(document.getElementById('gen-balloons').value) || 2,
        missArrowCount: parseInt(document.getElementById('gen-miss').value) || 1,
        minBlockLength: parseInt(document.getElementById('gen-min-len').value) || 3,
        maxBlockLength: maxLen,
        targetDensity: targetDensity,
        densityMode: fillerEnabled ? 'fill' : 'manual',
        fillerEnabled: fillerEnabled,
        fillerMinLength: fillerMin,
        fillerMaxLength: Math.max(2, fillerMax),
        bendingEnabled: bendingEnabled,
        bendingChance: bendingEnabled ? 1.0 : 0,
        branchingMode: branchingMode,
        branchingChance: branchingMode ? 0.5 : 0
    };

    try {
        const level = LevelGenerator.generateLevel(config);

        // LEVELS 배열에 추가
        LEVELS.push(level);

        // 생성된 레벨로 이동
        initLevel(LEVELS.length - 1);
        closeGeneratorUI();

        // 통계 표시
        if (level.stats) {
            const msg = `Density: ${(level.stats.density * 100).toFixed(0)}%`;
            showToast(msg);
        } else {
            showToast("Generated!");
        }
    } catch (e) {
        console.error("Generation failed:", e);
        alert("Generation failed: " + e.message);
    }
}

window.onload = () => {
    // 초기 상태 설정
    document.getElementById('lvl-title').innerText = 'Arrow Puzzle';
    document.getElementById('q-len').innerText = '0';
    document.getElementById('g-size').innerText = '-';
    document.getElementById('queue-box').style.display = 'none';
    document.getElementById('solution-box').classList.remove('visible');
    document.getElementById('btn-restart').classList.remove('visible');

    // Generator UI 바로 표시
    showGeneratorUI();
};
