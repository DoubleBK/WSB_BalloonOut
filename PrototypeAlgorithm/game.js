const DIRS = {
    U: { dx: 0, dy: -1, deg: 0 },
    D: { dx: 0, dy: 1, deg: 180 },
    L: { dx: -1, dy: 0, deg: 270 },
    R: { dx: 1, dy: 0, deg: 90 }
};

const COLORS = { R: '#ff4757', G: '#2ed573', Y: '#ffa502', D: '#5352ed', P: '#8e44ad' };

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
        const cells = [];
        const dParams = DIRS[b.d];
        for (let k = 0; k < b.l; k++) {
            cells.push({
                x: b.x - (dParams.dx * k),
                y: b.y - (dParams.dy * k)
            });
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

    blocks.forEach(b => {
        const grp = document.createElement('div');
        grp.className = 'block';
        grp.id = `blk-${b.id}`;

        if (!b.cells || !Array.isArray(b.cells)) return;
        b.cells.forEach((cell, idx) => {
            const u = document.createElement('div');
            u.className = `block-unit c-${b.c} ${idx === 0 ? 'head' : 'body'}`;
            u.style.left = `calc(${cell.x} * var(--cell-size))`;
            u.style.top = `calc(${cell.y} * var(--cell-size))`;

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

function tryEscape(b) {
    if (isAnim) return;
    isAnim = true;

    const el = document.getElementById(`blk-${b.id}`);
    const d = DIRS[b.d];
    const cellSize = 50; // CSS의 --cell-size와 동일

    // 충돌 여부와 거리 계산
    const collision = getDistanceToCollision(b);

    if (collision.blocked) {
        // 충돌: 충돌 지점까지 이동 후 제자리로 돌아옴
        const moveDistance = (collision.distance + 0.5) * cellSize; // 충돌 지점 직전까지

        // 전진 애니메이션
        el.style.transition = 'transform 0.2s ease-out';
        el.style.transform = `translate(${d.dx * moveDistance}px, ${d.dy * moveDistance}px)`;

        setTimeout(() => {
            // 후진 애니메이션 (제자리로)
            el.style.transition = 'transform 0.25s ease-in';
            el.style.transform = 'translate(0, 0)';

            setTimeout(() => {
                el.style.transition = '';
                showToast("BLOCKED!");
                isAnim = false;
            }, 250);
        }, 200);
    } else {
        // 탈출 가능: 날아감
        const dist = 800;

        el.classList.add('flying');
        el.style.transform = `translate(${d.dx * dist}px, ${d.dy * dist}px)`;
        el.style.opacity = '0';

        setTimeout(() => {
            blocks = blocks.filter(x => x.id !== b.id);
            if (el.parentNode) el.parentNode.removeChild(el);

            // 풀이 순서 UI 업데이트
            const solEl = document.getElementById(`sol-${b.id}`);
            if (solEl) solEl.classList.add('popped');

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
        }, 400);
    }
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
// Level Generator UI Integration
// ============================================================

function showGeneratorUI() {
    document.getElementById('gen-modal').style.display = 'flex';
}

function closeGeneratorUI() {
    // 레벨이 없으면 모달을 닫지 않음
    if (LEVELS.length === 0) {
        showToast("Generate a puzzle first!");
        return;
    }
    document.getElementById('gen-modal').style.display = 'none';
}

function runGenerator() {
    const branchingMode = document.getElementById('gen-branching').checked;

    const config = {
        gridSize: parseInt(document.getElementById('gen-size').value) || 8,
        laneCount: parseInt(document.getElementById('gen-lanes').value) || 2,
        balloonsPerLane: parseInt(document.getElementById('gen-balloons').value) || 2,
        missArrowCount: parseInt(document.getElementById('gen-miss').value) || 1,
        minBlockLength: 2,
        maxBlockLength: 3,
        branchingMode: branchingMode,
        branchingChance: branchingMode ? 0.5 : 0  // 분기 모드일 때 50% 확률로 자유 배치
    };

    try {
        const level = LevelGenerator.generateLevel(config);

        // LEVELS 배열에 추가
        LEVELS.push(level);

        // 생성된 레벨로 이동
        initLevel(LEVELS.length - 1);
        closeGeneratorUI();

        showToast("Generated!");
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
