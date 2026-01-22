# 프로토타입 요구사항 문서 (PRD)
## 화살표 퍼즐: 고밀도 애로우 메이즈 (Arrow Puzzle: High-Density & Solution-First)

### 문서 정보
* **버전:** 0.2.0 (Major Update)
* **최종 수정일:** 2026-01-22
* **프로젝트명:** Arrow Puzzle: Arrow Maze (가제)
* **핵심 컨셉:** 95% 꽉 찬 화살표 미로를 풀어내는 Solution-first Puzzle

---

### 1. 개요 및 핵심 철학
#### 1.1. 한 줄 요약
하단 Grid는 벽이나 통로 없이 **화살표로만 95% 내외로 채워진 ‘화살표 미로’**이며, 각 화살표는 **방향(dir) + 길이(len)**를 가집니다. 앞이 비면(자신의 몸통 길이만큼 연장된 공간이 확보되면) 화면 밖으로 탈출합니다.

#### 1.2. 목표 (Intention)
* **Visual Impact:** 화면을 가득 채운 화살표들이 주는 압도감과, 이를 하나씩 풀어내며 비워가는 시각적 만족감(Cleaning) 극대화.
* **Puzzle Density:** "단순히 비어있는 칸으로 이동"하는 것이 아니라, "긴 몸통(Body)이 서로 얽히고설킨" 구조를 해제하는 연쇄 작용의 쾌감.
* **Stability:** 고밀도일수록 불가능한 레벨이 되기 쉬우므로, **반드시 해답(Solution)을 먼저 설계하고 Grid를 채우는 방식(Solution-first)**으로 개발한다.

#### 1.3. 가정 (Assumptions)
* **Grid:** W×H (예: 6×6 ~ 10×10).
* **Target Fill Rate:** **Grid Occupancy 95% 이상** (빈 공간이 거의 없음).
* **Arrow Properties:**
    * `dir`: Up, Down, Left, Right
    * `color`: Red, Blue, Yellow (초기 3색)
    * `len`: 1 ~ Lmax (랜덤 분포, 길수록 장애물 역할 강화)

---

### 2. 핵심 메카닉 (Game Mechanics)
#### 2.1. Arrow Modeling: Body Occupancy
* **길이(len)의 정의:** 이동 거리가 아니라 **'몸통의 점유 길이(Hitbox/Occupancy)'**로 정의한다.
* **점유(Occupied):** 
    * Arrow는 자신의 머리(Head) 위치로부터 반대 방향으로 `len-1`만큼의 셀을 추가로 점유한다.
    * 총 점유 셀 수 = `len`.
    * **Blocking 판정:** 다른 Arrow가 탈출하려 할 때, 내 **몸통(Body)이 그 경로에 있다면** 나는 장애물이다.

#### 2.2. Escape Logic (탈출 조건)
* Arrow A가 탈출하기 위해서는 `Head`로부터 `dir` 방향으로 화면 끝까지의 경로(LOS)에 **어떤 장애물(다른 Arrow의 Head 또는 Body)**도 없어야 한다.
* **긴 Arrow의 특징:**
    * 탈출 시 더 긴 경로를 비워야 하므로(자신의 몸통이 빠져나가야 함) 시각적으로 더 큰 보상을 준다.
    * 제자리에 있을 때는 더 많은 셀을 막고 있는 강력한 장애물이다.

#### 2.3. Queue & Targeting Logic (유지)
* **Queue Area:** 좌→우(L2R) 순서로 배치된 수직 Lane들.
* **Targeting:** 
    * Arrow 탈출 시, Queue의 `Lane[0]`부터 검색하여 첫 번째로 만나는 동일 색상 `Head`(최하단 풍선)를 Pop한다.
    * 매칭되는 Head가 없으면 **Miss** 처리 (화살표 소모, 효과 없음).

---

### 3. 레벨 디자인 및 생성 파이프라인
**"랜덤 생성 후 검증"이 아니라, "해답을 먼저 만들고 나머지를 채운다".**

#### 3.1. 생성 단계 (Generation Pipeline)
1. **파라미터 설정:** Grid 크기, 목표 Fill Rate(95%), 길이 분포(1~4), 색상 수.
2. **해답 시퀀스(Solution Sequence) 생성:** 
    * `S = [(Red, 3), (Blue, 2), ...]` 와 같이 탈출 순서와 속성을 미리 정의.
    * 초반 튜토리얼 레벨은 Miss가 발생하지 않도록 상단 Queue 색상과 동기화.
3. **Grid 역설계 배치 (Reverse Engineering):**
    * `S`의 **마지막** 화살표부터 역순으로 배치.
    * 배치 조건: "이 화살표가 탈출하기 위해 필요한 경로가, 이전 화살표들에 의해 막혀있지 않도록(혹은 막혀있도록)" 논리적 배치.
4. **Auto Fill (Filler Placement):**
    * 해답 경로를 방해하지 않는 선에서 남은 빈 칸을 **필러(Filler) 화살표**로 채움.
    * 목표 점유율 95% 달성.
    * 주의: 필러 배치로 인해 의도치 않은 '조기 탈출(Shortcut)'이 너무 많이 생기지 않도록 제어.
5. **Verifier (유효성 검증):**
    * 시작 시 Escapable Arrow 개수 확인 (1~3개 권장).
    * Deadlock(소프트락) 존재 여부 확인.

#### 3.2. 95% Fill Rate 전략
* "화살표 머리 개수"가 아니라 **"점유된 셀(Occupied Cells)의 비율"**을 기준으로 95%를 맞춘다.
* 길이가 긴 화살표(3~4칸)를 적절히 배치하면 적은 수의 화살표로도 높은 밀도를 구현할 수 있다.

---

### 4. 에디터 및 툴 요구사항 (Level Editor Specs)
Unity 에디터 확장을 통해 아래 기능을 필수적으로 구현해야 한다.

#### 4.1. Grid Edit Mode
* **기본 조작:** 셀 클릭(배치/삭제), 드래그(방향/길이 조정).
* **시각화:** 
    * 선택된 Arrow의 `Body` 점유 구간 표시.
    * Escapable 상태 실시간 하이라이트.

#### 4.2. Solution & Auto Fill Panel
* **Solution List:** 기획자가 의도한 해답 순서를 리스트로 관리.
* **Generate Grid:** Solution List 기반으로 Grid 역설계 자동 배치 버튼.
* **Fill Remaining:** 빈 공간을 필러로 채우는 버튼 (Fill Rate 목표치 설정 가능).

#### 4.3. Validation Panel (Verifier)
* **Analyze:** 현재 레벨의 상태를 분석하여 리포트 출력.
    * **Solvable:** True/False
    * **Deadlock Risk:** 언제 발생하는지 턴 수 표시.
    * **Miss Count:** 미스 발생 예상 횟수.
    * **Fill Rate:** 현재 점유율 표기.

---

### 5. 데이터 스키마 (JSON Schema Proposal)
```json
{
  "level_id": "lvl_001",
  "grid": {
    "width": 6,
    "height": 6
  },
  "solution_sequence": [ // 검증 및 힌트용
    { "color": "R", "id_ref": 12 },
    { "color": "B", "id_ref": 4 }
  ],
  "arrows": [
    {
      "id": 1,
      "x": 2, "y": 3,
      "dir": "UP",
      "len": 3,
      "color": "R",
      "is_filler": false // 필러 여부 (난이도 조절 시 제거 대상)
    },
    ...
  ],
  "queues": [
    ["R", "R", "B"], // Index 0: Top, Last: Head
    ["Y", "G"]
  ]
}
```

---

### 6. QA 체크리스트 (Revised)
1. **밀도:** 시작 시 Grid가 화살표로 꽉 차 보이는가? (빈 공간이 거의 없어야 함)
2. **Body 충돌:** 긴 화살표의 몸통이 정확히 장애물 역할을 하는가?
3. **Solvability:** 무작위로 누르는 것이 아니라, 논리적인 순서(앞의 것을 치워야 뒤의 것이 나감)로 풀리는가?
4. **Verifier:** 검증기가 통과한 레벨은 실제로 100% 클리어 가능한가?
