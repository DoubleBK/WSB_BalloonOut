# Bira - Bug Investigation & Resolution Archive

Balloon Out 프로젝트의 버그 추적 문서입니다.
새로운 버그 발생 시 이 문서를 먼저 확인하여 유사 이슈의 해결 사례를 참고하세요.

---

## 요약 통계

| 상태 | 수량 |
|------|------|
| 해결 완료 | 12 |
| 미해결 | 0 |
| 총 추적 버그 | 12 |

---

## 해결된 버그

### BUG-001: Hint 연출 - DOShakeScale 부자연스러움

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | Hint 부스터 |
| **증상** | Hint로 선택된 화살표의 `DOShakeScale` 애니메이션이 부자연스러움 |
| **원인** | Scale 변경 애니메이션은 화살표 모양을 왜곡시켜 시각적으로 어색함 |
| **해결** | Scale 애니메이션 → 색상 펄스(밝기 0.5~1.0 Yoyo) 애니메이션으로 교체 |
| **수정 파일** | `ArrowController.cs`, `ArrowVisualRenderer.cs` |
| **핵심 코드** | `DOTween.To(() => brightness, x => ApplyBrightness(x), 1f, 0.5f).SetLoops(-1, LoopType.Yoyo)` |

---

### BUG-002: Hint 3초 후 자동 해제 + 중복 사용 가능

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | Hint 부스터 |
| **증상** | (1) 힌트가 3초 후 자동 해제됨 (2) 힌트 활성 중에도 Hint 버튼 재사용 가능 |
| **원인** | `DOVirtual.DelayedCall(3f, ...)` 로 3초 후 해제하는 로직 존재. 버튼 상태에 IsHintActive 조건 누락 |
| **해결** | 3초 자동 해제 제거, 화살표 탈출 시에만 해제. `BoosterManager.IsHintActive` 프로퍼티 추가하여 버튼 비활성화 |
| **수정 파일** | `ArrowController.cs`, `BoosterManager.cs`, `BoosterBottomBarUI.cs` |

---

### BUG-003: Undo 화살표 미생성 (TODO만 존재)

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | Undo 부스터 |
| **증상** | Undo 사용 시 화살표가 복원되지 않음 |
| **원인** | `BoosterManager.RestoreArrow()`에 `// TODO` 주석만 있고 실제 구현 없음 |
| **해결** | `GameManager.RestoreArrow(int id, ArrowData data)` 공개 메서드 추가. `SpawnArrow`와 유사하지만 기존 ID를 유지하며 이벤트 핸들러도 재등록 |
| **수정 파일** | `GameManager.cs`, `BoosterManager.cs` |

---

### BUG-004: Undo 풍선 색상 오류 (스냅샷 타이밍)

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | Undo 부스터 |
| **증상** | Undo로 복원되는 풍선의 색상/레인이 잘못됨 |
| **원인** | `OnHomingHitTargetHandler`에서 `TryPopBalloon()` 호출 **후** 스냅샷 기록 → 팝 이후의 레인 인덱스가 기록됨 |
| **해결** | 팝 **전에** `FindLaneWithActiveBalloon(color)` 호출하여 레인 인덱스를 미리 캡처 |
| **수정 파일** | `GameManager.cs` |
| **교훈** | 상태 변경 작업과 스냅샷 기록의 순서를 항상 확인. "기록 먼저, 변경 나중" 원칙 |

---

### BUG-005: Undo 풍선 초기 위치 오류

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | Undo 부스터 |
| **증상** | 복원된 풍선의 등장 시작 위치가 잘못됨 |
| **원인** | `RestoreBalloon()`에서 새 풍선의 초기 위치를 올바르게 설정하지 않음. `AnimateBalloonsAfterRestore()`가 모든 풍선을 재계산하지만 새 풍선의 시작점이 부정확 |
| **해결** | 새 풍선은 `CalculateBalloonPosition(laneIdx, -1)`로 활성 위치 아래에 배치 후 슬라이드 |
| **수정 파일** | `QueueUI.cs` - `RestoreBalloon()`, `AnimateBalloonsAfterRestore()` |

---

### BUG-006: 카메라 드래그 경계 부족 (BottomUIBar 뒤로 이동 불가)

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | 카메라 컨트롤러 |
| **증상** | 카메라 드래그 시 그리드가 BottomUIBar 뒤로 이동하지 못함 |
| **원인** | `_verticalExtraPadding = 3f`가 BottomUIBar의 실제 화면 높이를 반영하지 못함 |
| **해결** | `_bottomExtraPadding = 5f` 필드를 분리 추가하여 하단 전용 패딩 설정 |
| **수정 파일** | `CameraController.cs` |
| **후속 이슈** | BUG-006b 참조 - 패딩을 잘못된 경계에 적용하여 드래그 방향이 반대로 동작 |

---

### BUG-006b: 카메라 드래그 방향 반전 (TopUI 위로 이동)

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | 카메라 컨트롤러 |
| **증상** | 드래그 시 그리드가 BottomUIBar 뒤로 가는 대신 TopUI 위로 올라감 |
| **원인** | 카메라-월드 좌표 관계를 반대로 적용. `_bottomExtraPadding`을 `_worldBoundsMin.y`(하단)에 추가했으나, 카메라가 **위로** 이동해야 그리드가 **아래로** 내려감 → `_worldBoundsMax.y`(상단)에 추가해야 함 |
| **해결** | `topPadding = _boundaryPadding + _verticalExtraPadding + _bottomExtraPadding`, `bottomPadding = _boundaryPadding + _verticalExtraPadding` |
| **수정 파일** | `CameraController.cs` - `SetWorldBounds()` |
| **교훈** | 카메라 좌표계: 카메라 y 증가 = 화면상 콘텐츠 아래로 이동. 드래그 코드 `newPos = _cameraStartPos - worldDelta`이므로 위로 드래그 → 카메라 y 감소 → 콘텐츠 위로 이동 |

---

### BUG-007: Hint→Escape→Undo→Hint 시 다른 화살표 선택

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | Hint 부스터 + Undo |
| **증상** | Hint로 화살표 A 선택 → A 탈출 → Undo로 A 복원 → 다시 Hint → 화살표 B가 선택됨 |
| **원인** | `HintCalculator.GetActiveArrows()`가 `GetComponentsInChildren` 순서 의존. Undo로 `Instantiate`된 화살표는 hierarchy 끝에 추가되어 탐색 순서 변경 |
| **해결** | `arrows.Sort((a, b) => a.Id.CompareTo(b.Id))` ID 순 정렬 추가 |
| **수정 파일** | `HintCalculator.cs` - `GetActiveArrows()` |
| **교훈** | `FindObjectsOfType`/`GetComponentsInChildren` 순서에 의존하지 말 것. 항상 명시적 정렬 사용 |

---

### BUG-008: Undo 복원 풍선이 LanesContainer 밖에 표시

> **이 버그는 4차례 수정을 거쳐 해결됨. 아래 시도 이력 참고.**

| 항목 | 내용 |
|------|------|
| **상태** | 해결 (4차 시도) |
| **시스템** | Undo 부스터 + QueueUI |
| **증상** | Undo로 복원된 풍선이 LanesContainer 영역 밖 엉뚱한 위치에 표시됨 |
| **근본 원인** | LayoutGroup이 설정한 앵커와 RestoreBalloon에서 새로 생성한 풍선의 앵커가 불일치. `_laneBasePositions`는 LayoutGroup 앵커 기준으로 캡처된 값인데, 새 풍선은 다른 앵커를 사용하여 같은 anchoredPosition 값이 완전히 다른 화면 위치를 가리킴 |
| **최종 해결** | (1) `CreateBalloon()`에서 앵커 강제 설정 제거 (2) `RestoreBalloon()`에서 기존 풍선의 앵커/피벗 복사 (3) `Clear()`에서 `DestroyImmediate()` 사용 |
| **수정 파일** | `QueueUI.cs` - `CreateBalloon()`, `RestoreBalloon()`, `Clear()` |

#### 시도 이력

| 차수 | 시도 내용 | 결과 | 실패 이유 |
|------|----------|------|----------|
| 1차 | `CreateBalloon()`에서 앵커를 (0.5, 0)으로 강제 설정 | 실패 | `Instantiate(_balloonPrefab, parent)`가 `worldPositionStays=true`로 동작하여 월드 위치가 유지됨 |
| 2차 | `Instantiate(_balloonPrefab)` + `SetParent(parent, false)` 분리 | 실패 | `SetParent` 후 앵커를 변경하면 Unity가 anchoredPosition을 자동 재계산하여 예상과 다른 값이 됨 |
| 3차 | 앵커 변경 후 `anchoredPosition = Vector2.zero` 리셋 | 실패 | 근본적으로 앵커 자체가 불일치. LayoutGroup이 (0.5, 1) 등으로 설정한 앵커와 강제로 (0.5, 0)으로 설정한 앵커가 다름 |
| **4차** | **앵커 강제 설정 제거 + 기존 풍선에서 앵커 복사 + DestroyImmediate** | **성공** | LayoutGroup이 설정한 앵커를 그대로 사용하여 `_laneBasePositions` 기반 위치 계산이 정확해짐 |

#### 교훈

> **Unity RectTransform 핵심 규칙:**
> - `anchoredPosition`은 앵커 기준 상대값. 앵커가 다르면 같은 값이 완전히 다른 위치를 가리킴
> - LayoutGroup은 내부적으로 `SetInsetAndSizeFromParentEdge()`를 호출하여 자식의 앵커를 변경함
> - `Instantiate(prefab, parent)` = `worldPositionStays=true` → UI 프리팹에서는 `Instantiate(prefab)` + `SetParent(parent, false)` 사용 권장
> - `Destroy()`는 프레임 끝까지 지연됨 → 즉시 제거가 필요하면 `DestroyImmediate()` 사용 (컨테이너 자식 정리 시)
> - LayoutGroup 제거 후 수동 배치할 때는 기존 요소의 앵커/피벗을 반드시 유지

---

### BUG-009: Grid가 BottomUI 버튼 위에 렌더링됨

| 항목 | 내용 |
|------|------|
| **상태** | 해결 |
| **시스템** | 렌더링 / UI 레이어 |
| **증상** | 카메라 드래그 시 화살표가 BottomUIBar 버튼들 **위에** 그려짐. 버튼이 화살표에 가려짐 |
| **원인** | GameScene Canvas가 `Screen Space - Camera` 모드(RenderMode=1), `sortingOrder=0`. 화살표 SpriteRenderer의 sortingOrder가 1-2로 Canvas보다 높음 |
| **해결** | `BoosterBottomBarUI`에 `Canvas` 컴포넌트 추가 (`overrideSorting=true`, `sortingOrder=110`). `GraphicRaycaster`도 추가하여 버튼 클릭 유지 |
| **수정 파일** | `BoosterBottomBarUI.cs` - `SetupSortingOrder()` |
| **교훈** | `Screen Space - Camera` 모드에서 SpriteRenderer와 UI의 렌더링 순서는 sortingOrder로 결정됨. 특정 UI 요소만 높은 sortingOrder가 필요하면 `overrideSorting` 사용 |

---

## sortingOrder 체계

현재 프로젝트의 렌더링 레이어 구조:

| sortingOrder | 대상 | 비고 |
|-------------|------|------|
| 0 | GameScene Canvas (기본) | Screen Space - Camera |
| 1 | Arrow Body (SpriteRenderer) | |
| 2 | Arrow Head (SpriteRenderer) | |
| 100 | HomingArrow | 탈출 후 풍선으로 이동하는 투사체 |
| 110 | BottomUIBar (overrideSorting) | 화살표/HomingArrow 위에 렌더링 |

---

## Scene 잔여 오브젝트 관련 주의사항

### 문제

GameScene.unity의 LanesContainer에 에디터 Play 모드에서 생성된 잔여 오브젝트가 존재:

```
LanesContainer
├── Lane_0 (3개 Balloon(Clone))
└── Lane_1 (3개 Balloon(Clone))
```

### 영향

- `Clear()`에서 `Destroy()` 사용 시: 프레임 끝까지 오브젝트가 존재 → `CreateUI()`에서 LayoutGroup이 old+new 자식을 모두 계산
- 현재 `DestroyImmediate()`로 수정하여 즉시 제거됨

### 권장 사항

- 에디터에서 LanesContainer의 불필요한 자식 오브젝트 수동 삭제 권장
- Play 모드 종료 후 씬 변경사항 저장하지 않도록 주의

---

## Unity 좌표계 & RectTransform 참고

### 카메라 좌표계

```
카메라 y 증가 → 화면상 콘텐츠가 아래로 이동
카메라 y 감소 → 화면상 콘텐츠가 위로 이동

드래그 코드: newPos = _cameraStartPos - worldDelta
→ 위로 드래그 = delta.y 양수 = 카메라 y 감소 = 콘텐츠 위로 이동
→ 아래로 드래그 = delta.y 음수 = 카메라 y 증가 = 콘텐츠 아래로 이동
```

### RectTransform 앵커 규칙

```
anchoredPosition = 앵커 기준 상대 좌표
→ 앵커 (0.5, 0): 부모 하단 중앙 기준, y+ = 위
→ 앵커 (0.5, 1): 부모 상단 중앙 기준, y- = 아래
→ 같은 anchoredPosition 값이라도 앵커가 다르면 완전히 다른 화면 위치

LayoutGroup이 앵커를 변경할 수 있음 (SetInsetAndSizeFromParentEdge)
→ LayoutGroup 제거 후 수동 배치 시, 기존 요소의 앵커를 반드시 유지해야 함
```

### Instantiate & SetParent

```
Instantiate(prefab, parent)     → worldPositionStays = true (월드 위치 유지)
Instantiate(prefab)             → 독립 생성
SetParent(parent, false)        → worldPositionStays = false (로컬 위치 유지)
SetParent(parent, true)         → worldPositionStays = true (월드 위치 유지)

UI 프리팹 권장: Instantiate(prefab) + SetParent(parent, false)
```

---

## 자주 발생하는 패턴

### 1. 상태 변경 전 스냅샷 캡처

**문제**: 상태를 변경한 후 스냅샷을 기록하면 변경된 상태가 캡처됨
**규칙**: 항상 "캡처 먼저, 변경 나중" 순서 유지

```csharp
// BAD
_queueUI.TryPopBalloon(color);
int laneIndex = _queueUI.FindLane(color);  // 이미 팝된 후

// GOOD
int laneIndex = _queueUI.FindLane(color);  // 팝 전 캡처
_queueUI.TryPopBalloon(color);
```

### 2. Unity 오브젝트 순서 의존 금지

**문제**: `FindObjectsOfType`, `GetComponentsInChildren` 등의 반환 순서는 보장되지 않음
**규칙**: 비즈니스 로직에 순서가 중요하면 항상 명시적 정렬 추가

```csharp
arrows.Sort((a, b) => a.Id.CompareTo(b.Id));
```

### 3. LayoutGroup → 수동 배치 전환 시 주의

**문제**: LayoutGroup이 설정한 RectTransform 속성(앵커, 피벗, 위치)이 제거 후에도 유지됨
**규칙**: 수동 배치로 전환 후 새 요소 추가 시, 기존 요소의 앵커/피벗을 복사하여 일관성 유지

```csharp
// 기존 형제 요소에서 앵커 복사
newRect.anchorMin = existingRect.anchorMin;
newRect.anchorMax = existingRect.anchorMax;
newRect.pivot = existingRect.pivot;
```

---

## 변경 이력

| 날짜 | 작업 |
|------|------|
| 2026-01 | 부스터 시스템 구현 (Undo, Hint) |
| 2026-01 | BUG-001 ~ BUG-005 수정 (1차 버그 수정) |
| 2026-01 | BUG-006 ~ BUG-008 수정 (2차 버그 수정) |
| 2026-01 | BUG-006b, BUG-008b 수정 (3차 버그 수정) |
| 2026-01 | BUG-009, BUG-008c 수정 (4차 버그 수정) |
| 2026-01 | BUG-008d 최종 수정 - 앵커 불일치 근본 원인 해결 |