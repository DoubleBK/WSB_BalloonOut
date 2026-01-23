# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

**Balloon Out** - Unity 2D 하이퍼캐주얼 퍼즐 게임
- 화살표를 터치하여 Snake처럼 이동시키고, 그리드 밖으로 탈출시켜 동일 색상 풍선을 터뜨리는 게임
- Unity 2022.3.69f1 / URP 14.0.12
- 타겟 플랫폼: Android (세로 화면)

## 핵심 게임 메커니즘

1. **Snake 이동**: 화살표 터치 시 Head 방향으로 이동, 머리가 먼저 이동하고 몸통이 따라옴
2. **충돌 판정**: 다른 화살표 Body와 겹치면 충돌 → 원위치로 복귀 (바운스백)
3. **탈출 판정**: Head가 그리드 밖으로 나가면 탈출, 모든 셀이 순차적으로 빠져나감
4. **호밍 이동**: 탈출한 화살표가 Bezier 곡선으로 풍선 영역으로 이동
5. **풍선 팝**: 동일 색상 풍선과 매칭 시 팝 (Lane 좌→우 순서로 탐색)
6. **승리/패배**: 모든 풍선 팝 = 클리어 / 이동 가능한 화살표 없이 풍선 남음 = 실패

## 코드 아키텍처

### 핵심 시스템 (Assets/Scripts/)

| 시스템 | 파일 | 역할 |
|--------|------|------|
| **GameManager** | Core/GameManager.cs | 게임 상태 관리, 레벨 로드, 승패 판정 |
| **GridSystem** | Game/Grid/GridSystem.cs | 그리드 좌표 변환, 셀 점유 관리 |
| **ArrowController** | Game/Arrow/ArrowController.cs | 화살표 이동/충돌/탈출 로직 |
| **ArrowVisualRenderer** | Game/Arrow/ArrowVisualRenderer.cs | LineRenderer/SpriteShape 렌더링 |
| **ArrowAnimationHelper** | Game/Arrow/ArrowAnimationHelper.cs | 등장/실패/페이드 애니메이션 |
| **HomingArrow** | Game/Arrow/HomingArrow.cs | 탈출 후 풍선으로 날아가는 투사체 |
| **QueueUI** | UI/QueueUI.cs | 풍선 Queue 표시 및 팝 처리 |
| **LevelLoader** | Data/LevelLoader.cs | JSON 레벨 데이터 로드 |

### 데이터 구조

- **GameEnums.cs**: ArrowDirection, GameColor(13색), ArrowState, GameState 등
- **LevelData.cs**: 레벨 정의 (gridSize, lanes, arrows)
- **ArrowData.cs**: 화살표 속성 (position, color, direction, length, path)

### 이벤트 시스템

GameManager가 브로드캐스트하는 주요 이벤트:
- `OnGameStateChanged(GameState)` - 상태 전환
- `OnArrowEscaped(GameColor, wasMatch)` - 화살표 탈출
- `OnLevelCleared()` / `OnLevelFailed()` - 레벨 종료

## 레벨 데이터 형식 (JSON)

레벨 파일 위치: `Assets/Resources/Levels/`

```json
{
  "name": "Level_001",
  "gridSize": 6,
  "lanes": [{ "balloons": ["R", "G", "B"] }],
  "arrows": [{
    "x": 2, "y": 2,
    "color": "R",
    "direction": "L",
    "length": 3,
    "path": [{"x":2,"y":2}, {"x":1,"y":2}, {"x":0,"y":2}]
  }]
}
```

- **color**: R(빨강)/G(초록)/B(파랑)/Y(노랑)/P(보라)/O(주황)/C(청록)/K(분홍)/W(갈색)/L(라임)/N(네이비)/M(마젠타)/X(검정)
- **direction**: U(위)/D(아래)/L(왼쪽)/R(오른쪽)
- **path**: 꺾이는 화살표용 경로 (없으면 직선)
- **balloons**: FIFO Queue (마지막 요소가 활성화된 풍선)

## 외부 의존성

- **DOTween**: 애니메이션 라이브러리 (Punch scale, DOFade, Bezier path)
- **2D SpriteShape**: 부드러운 곡선 화살표 렌더링

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── Core/          # GameManager, GameEnums
│   ├── Data/          # LevelData, ArrowData, LevelLoader
│   ├── Game/
│   │   ├── Arrow/     # 화살표 관련 컴포넌트
│   │   └── Grid/      # 그리드 시스템
│   └── UI/            # QueueUI, GameResultUI
├── Prefabs/
│   ├── Arrow/         # 화살표 프리팹
│   ├── Balloon/       # 풍선 프리팹
│   └── UI/            # UI 프리팹
├── Resources/Levels/  # JSON 레벨 파일
└── Scenes/
    └── GameScene.unity # 메인 게임 씬
```

## 기획 문서

- [Assets/Documents/WSB_MVP.md](Assets/Documents/WSB_MVP.md): 전체 기획서 (게임 룰, 메커니즘, BM, KPI)
- [PrototypeAlgorithm/PRD.md](PrototypeAlgorithm/PRD.md): 레벨 생성 알고리즘 상세