# 신규 기믹 아이디에이션

신규 기믹 후보들에 대한 기획 및 기술 분석 문서입니다.

---

## 목차

1. [Bomb Balloon (폭탄 풍선)](#1-bomb-balloon-폭탄-풍선)
2. [Color Switch Balloon (색상 전환 풍선)](#2-color-switch-balloon-색상-전환-풍선)
3. [Box (상자)](#3-box-상자)
4. [구현 우선순위](#4-구현-우선순위)

---

## 1. Bomb Balloon (폭탄 풍선)

### 개요

| 항목 | 설명 |
|------|------|
| **컨셉** | N턴 후 폭발하는 시한폭탄 풍선 |
| **카운트다운** | 매 턴마다 -1 |
| **목표** | 카운트가 0이 되기 전에 터뜨려야 함 |
| **시각적** | 숫자 카운트다운 표시 (Number 기믹과 유사) |

### 메커니즘

```
[Bomb R:3] → 화살표 탈출 → [Bomb R:2] → 화살표 탈출 → [Bomb R:1] → 화살표 탈출 → 💥 폭발!
```

- 빨간색 Bomb 풍선, 초기 카운트 3
- 매 턴(화살표 탈출 시) 카운트 -1
- 0이 되면 폭발

### 기획 결정 필요 사항

#### Q1. 폭발 시 결과

| 옵션 | 설명 | 게임플레이 영향 |
|------|------|----------------|
| **A. 게임 오버** | 폭발 = 즉시 실패 | 높은 긴장감, 하드코어 |
| **B. 인접 풍선 파괴** | 주변 풍선도 함께 터짐 | 전략적 활용 가능 |
| **C. 해당 Lane 파괴** | Lane 전체 풍선 제거 | 양날의 검 |
| **D. 그냥 사라짐** | 터뜨리지 못하면 Miss 처리 | 낮은 페널티 |

#### Q2. 턴 정의

| 옵션 | 설명 |
|------|------|
| **A. 모든 화살표 탈출** | Decoy, Miss 포함 |
| **B. 매칭 성공만** | 풍선을 터뜨린 경우만 |

#### Q3. 초기 카운트 범위

- 권장: 3~5턴
- Level Editor에서 설정 가능하게

### 기술 분석

#### 기존 코드 재활용

| 항목 | Number 기믹 | Bomb 기믹 |
|------|------------|----------|
| 카운트 저장 | requiredHits | countdown |
| 트리거 | Hit (맞을 때) | Turn (턴 종료 시) |
| 방향 | 증가 (0 → N) | 감소 (N → 0) |
| 종료 조건 | N 도달 시 Pop | 0 도달 시 폭발 |

**재활용 가능 요소:**
- 숫자 렌더링 UI (약 70%)
- GimmickInstanceData 구조
- Undo 스냅샷 로직

#### 필요한 추가 작업

1. **턴 이벤트 훅 추가**
   ```csharp
   // IGimmickBehavior에 추가 필요
   void OnTurnEnd(BalloonInstance balloon, GimmickInstanceData data);
   ```

2. **폭발 처리 로직**
   - GameManager에 폭발 이벤트 추가
   - 폭발 결과에 따른 분기 처리

### 구현 난이도

| 항목 | 난이도 | 비고 |
|------|--------|------|
| 기믹 로직 | 중 | Number 기믹 참고 |
| 턴 이벤트 연동 | 중 | 새 훅 필요 |
| 시각적 표현 | 하 | 기존 숫자 UI 재활용 |
| Undo 처리 | 중 | 카운트 복원 |
| Level Editor | 하 | Number와 동일 UI |
| 자동 생성 | 중 | 타이밍 검증 필요 |

**총평: ⭐⭐ (구현 용이)**

---

## 2. Color Switch Balloon (색상 전환 풍선)

### 개요

| 항목 | 설명 |
|------|------|
| **컨셉** | 매 턴마다 색상이 바뀌는 풍선 |
| **색상** | Main Color ↔ Sub Color 전환 |
| **규칙** | 현재 Main Color와 동일한 화살표로만 터뜨릴 수 있음 |
| **시각적** | Main/Sub 색상 모두 표시 필요 |

### 메커니즘

```
턴 1: [Main:R / Sub:B] → R 화살표로 터뜨릴 수 있음
       ↓ 화살표 탈출
턴 2: [Main:B / Sub:R] → B 화살표로 터뜨릴 수 있음
       ↓ 화살표 탈출
턴 3: [Main:R / Sub:B] → R 화살표로 터뜨릴 수 있음
```

### 기획 결정 필요 사항

#### Q1. 턴 정의

| 옵션 | 설명 |
|------|------|
| **A. 모든 화살표 탈출** | Decoy, Miss 포함 |
| **B. 매칭 성공만** | 풍선을 터뜨린 경우만 |

#### Q2. 동기화 방식

| 옵션 | 설명 |
|------|------|
| **A. 전역 동기화** | 모든 Color Switch 풍선이 동시에 전환 |
| **B. 개별 카운터** | 각 풍선이 독립적으로 전환 |

#### Q3. 첫 턴 규칙

| 옵션 | 설명 |
|------|------|
| **A. Main으로 시작** | 첫 턴부터 Main Color 활성 |
| **B. 1턴 후 전환 시작** | 첫 턴은 고정, 2턴째부터 전환 |

### 시각적 표현 방안

```
┌─────────────┐
│  ┌───────┐  │  방안 1: 테두리(Sub) + 내부(Main)
│  │ Main  │  │
│  └───────┘  │
│    Sub      │
└─────────────┘

┌──────┬──────┐
│ Main │ Sub  │  방안 2: 좌우 분할 (Sub는 어둡게)
│      │(dim) │
└──────┴──────┘

┌─────────────┐
│    Main     │  방안 3: 메인 + 작은 서브 아이콘
│         [S] │
└─────────────┘
```

### 기술 분석

#### 인터페이스 한계

현재 `IGimmickBehavior`에 없는 훅:
```csharp
void OnTurnEnd();           // 턴 종료 콜백 없음
void OnArrowEscaped();      // 화살표 탈출 콜백 없음
```

#### 해결 방안

| 방안 | 설명 | 장단점 |
|------|------|--------|
| A. 인터페이스 확장 | `OnGlobalEvent(string eventType)` 추가 | 범용적, 다른 기믹도 활용 가능 |
| B. 이벤트 직접 구독 | `GameManager.OnArrowEscaped` 구독 | 빠른 구현, 결합도 높음 |

#### 데이터 구조

```csharp
// GimmickInstanceData 파라미터
_paramKeys: ["mainColor", "subColor", "isMainActive"]
_paramValues: ["R", "B", "true"]
```

### 구현 난이도

| 항목 | 난이도 | 비고 |
|------|--------|------|
| 기믹 로직 | 중 | 색상 전환 자체는 단순 |
| 턴 이벤트 연동 | 중~상 | 글로벌 이벤트 필요 |
| 시각적 표현 | 중 | 새 비주얼 에셋 필요 |
| Undo 처리 | 상 | 전역 상태 스냅샷 필요 |
| Level Editor | 중 | 두 색상 선택 UI |
| 자동 생성 | 상 | 타이밍 + 색상 조합 검증 |

**총평: ⭐⭐⭐ (중간 난이도)**

---

## 3. Box (상자)

### 개요

| 항목 | 설명 |
|------|------|
| **컨셉** | 풍선들을 감싸는 컨테이너 |
| **상자 속성** | 색상 + 카운트 (N번 맞아야 열림) |
| **내용물** | 1개 이상의 풍선 (기믹 적용 가능) |
| **공개 시점** | 상자 파괴 후 내부 풍선 활성화 |

### 메커니즘

```
┌─────────────────┐
│  📦 Box (R, 3)  │  ← 빨간색 상자, 3번 맞아야 열림
│  ┌───┬───┬───┐  │
│  │ B │ Y │ G │  │  ← 안에 숨겨진 풍선들
│  │(2)│   │(S)│  │  ← 내부 풍선도 기믹 가능!
│  └───┴───┴───┘  │
└─────────────────┘
         ↓ R 화살표 3번 히트
┌───┬───┬───┐
│ B │ Y │ G │  ← 풍선들이 Queue에 추가됨
│(2)│   │(S)│
└───┴───┴───┘
```

### 기획 결정 필요 사항

#### Q1. 상자 파괴 후 풍선 위치

| 옵션 | 설명 | 게임플레이 영향 |
|------|------|----------------|
| **A. 제자리 (Queue 동일 위치)** | 상자 위치에서 바로 활성화 | 예측 가능 |
| **B. Queue 맨 뒤로** | 내부 풍선들이 Lane 맨 뒤로 이동 | 시간 벌기 가능 |
| **C. Queue 맨 앞으로** | 내부 풍선들이 Lane 맨 앞으로 | 즉시 처리 필요 |

#### Q2. 상자 타격 규칙

| 옵션 | 설명 |
|------|------|
| **A. Queue 맨 앞일 때만** | 기존 풍선 규칙과 동일 |
| **B. 언제든 타격 가능** | 상자는 특별 규칙 적용 |

#### Q3. 내부 풍선 개수

| 옵션 | 범위 |
|------|------|
| 고정 | 항상 1개 or 항상 3개 |
| 가변 | 1~5개 설정 가능 (권장) |

#### Q4. 중첩 가능 여부

| 옵션 | 설명 |
|------|------|
| **A. 불가** | 상자 안에 상자 없음 |
| **B. 가능** | 마트료시카 스타일 (복잡도 매우 높음) |

### 기술 분석

#### 데이터 구조 변경 필요

**현재 Lane 구조:**
```json
{
  "balloons": ["R", "B", "G"],
  "balloonData": [
    { "color": "R", "gimmicks": [...] },
    { "color": "B", "gimmicks": [...] },
    { "color": "G", "gimmicks": [...] }
  ]
}
```

**Box 지원 시 필요한 구조:**
```json
{
  "items": [
    {
      "type": "balloon",
      "color": "R",
      "gimmicks": [...]
    },
    {
      "type": "box",
      "color": "B",
      "count": 3,
      "contents": [
        { "color": "Y", "gimmicks": [...] },
        { "color": "G", "gimmicks": ["surprise"] }
      ]
    }
  ]
}
```

#### 영향 범위

| 시스템 | 변경 필요 |
|--------|----------|
| LevelData.cs | Lane 구조 변경 |
| StageData.cs | 직렬화 구조 변경 |
| QueueUI.cs | Box 렌더링 + 공개 애니메이션 |
| BalloonTargeting | Box vs Balloon 분기 |
| LevelGenerator | Box 배치 로직 |
| LevelValidator | Box 포함 검증 |
| LevelEditorWindow | 중첩 UI |

### 구현 난이도

| 항목 | 난이도 | 비고 |
|------|--------|------|
| 데이터 구조 | 상 | Lane 구조 전면 변경 |
| Queue UI | 상 | 상자 + 공개 애니메이션 |
| 타격 로직 | 중 | Box/Balloon 분기 |
| Undo 처리 | 상 | 상자 상태 + 내부 풍선 상태 |
| Level Editor | 상 | 중첩 편집 UI |
| 자동 생성 | 최상 | 풀이 가능성 검증 매우 복잡 |

**총평: ⭐⭐⭐⭐⭐ (높은 난이도)**

---

## 4. 구현 우선순위

### 종합 비교

| 기믹 | 구현 난이도 | 기존 코드 재활용 | 게임플레이 임팩트 | 추천 순서 |
|------|------------|----------------|------------------|----------|
| **Bomb** | ⭐⭐ | 70% (Number) | 긴장감 추가 | **1순위** |
| **Color Switch** | ⭐⭐⭐ | 30% | 전략성 추가 | **2순위** |
| **Box** | ⭐⭐⭐⭐⭐ | 10% | 깊이 추가 | **3순위** |

### 권장 개발 순서

```
Phase 1: Bomb Balloon
├── 턴 이벤트 시스템 구축 (OnTurnEnd 훅)
├── 카운트다운 로직 (Number 기믹 참고)
└── 폭발 처리

Phase 2: Color Switch Balloon
├── 턴 이벤트 시스템 활용 (Phase 1에서 구축)
├── 색상 전환 로직
└── 듀얼 컬러 비주얼

Phase 3: Box
├── 데이터 구조 리팩토링
├── 컨테이너 시스템
└── 중첩 UI
```

### 공통 선행 작업

모든 턴 기반 기믹(Bomb, Color Switch)을 위해 필요한 작업:

```csharp
// IGimmickBehavior.cs에 추가
void OnTurnEnd(BalloonInstance balloon, GimmickInstanceData data);

// 또는 범용 이벤트 훅
void OnGlobalEvent(BalloonInstance balloon, GimmickInstanceData data, string eventType);
```

---

## 부록: 기존 기믹 참고

### 현재 구현된 기믹

| 기믹 ID | 설명 | 트리거 |
|---------|------|--------|
| `surprise` | 색상 숨김, 활성화 시 공개 | OnBecomeActive |
| `number` | N번 맞아야 터짐 | OnHit |
| `connected` | 연결된 풍선 동시 터짐 | OnPop |

### IGimmickBehavior 인터페이스

```csharp
public interface IGimmickBehavior
{
    string GimmickId { get; }
    void OnInitialize(BalloonInstance balloon, GimmickInstanceData data);
    void OnBecomeActive(BalloonInstance balloon, GimmickInstanceData data);
    bool OnHit(BalloonInstance balloon, GimmickInstanceData data, GameColor arrowColor, out GimmickHitResult result);
    void OnPop(BalloonInstance balloon, GimmickInstanceData data);
    void OnRestore(BalloonInstance balloon, GimmickInstanceData data, GimmickInstanceData snapshot);
    GimmickInstanceData CreateSnapshot(BalloonInstance balloon, GimmickInstanceData data);
    void OnRender(BalloonInstance balloon, GimmickInstanceData data, BalloonVisual visual);
}
```

---

*문서 작성일: 2026-02-04*