# 실패 조건 정의 방안

## 1. 현재 구현된 실패 조건

**파일**: [GameManager.cs:527-542](../Scripts/Core/GameManager.cs#L527-L542)

```csharp
private void CheckFailCondition()
{
    if (_state != GameState.Playing) return;

    // 풍선이 남아있는데 화살표가 없으면 패배
    bool hasRemainingBalloons = _queueUI != null && !_queueUI.IsAllCleared();
    bool hasNoArrows = _arrows.Count == 0;

    if (hasRemainingBalloons && hasNoArrows)
    {
        SetState(GameState.Failed);
        OnLevelFailed?.Invoke();
        Debug.Log("LEVEL FAILED! No more arrows.");
    }
}
```

**간단한 조건**: 풍선이 남았는데 모든 화살표가 탈출함 = 실패

**호출 시점**:
- [GameManager.cs:486](../Scripts/Core/GameManager.cs#L486)
- 모든 화살표 탈출 후, 승리 조건 체크를 먼저 하고 승리가 아니면 실패 조건 확인

---

## 2. 실패 조건 정의 방안

### 방안 1: 현재 방식 유지 (심플) ⭐ **추천**

**조건**: 풍선 남음 + 화살표 모두 사용됨

**장점:**
- 구현 단순함
- 이미 작동 중
- 플레이어가 명확히 이해 가능 ("화살표를 모두 사용했습니다")
- 레벨 검증 시스템 존재: LevelEditor에서 이미 "Solvable" 검증을 하므로, 정상적으로 디자인된 레벨은 deadlock 상황이 발생하지 않아야 함
- 하이퍼캐주얼 게임 장르에 적합: 명확한 실패 조건 > 복잡한 deadlock 감지

**단점:**
- Deadlock 상황 미감지: 화살표가 그리드에 남아있지만 모두 막혀있어도 실패 처리 안됨
- 플레이어가 막힌 화살표를 계속 클릭하며 바운스백만 반복할 수 있음 (하지만 정상 레벨에서는 이런 상황이 발생하지 않아야 함)

**적용 근거:**
1. **레벨 검증 시스템**: LevelEditor의 Solvable 검증으로 deadlock이 없는 레벨만 배포
2. **게임 장르**: 하이퍼캐주얼 퍼즐에서 "명확한 실패 조건"이 우선
3. **개발 우선순위**: 더 중요한 기능 개발에 집중 가능

---

### 방안 2: Deadlock 감지 추가 (고급)

**조건**: 풍선 남음 + (화살표 없음 OR 모든 화살표 이동 불가)

**구현 필요:**
1. 각 화살표마다 CanMove() 체크 (충돌 없이 한 칸이라도 이동 가능한지)
2. 모든 화살표가 막혔는지 확인
3. 막혔으면 실패 처리

**구현 예시:**
```csharp
private void CheckFailCondition()
{
    if (_state != GameState.Playing) return;

    bool hasRemainingBalloons = _queueUI != null && !_queueUI.IsAllCleared();
    bool hasNoArrows = _arrows.Count == 0;

    // 화살표가 없으면 실패
    if (hasRemainingBalloons && hasNoArrows)
    {
        SetState(GameState.Failed);
        OnLevelFailed?.Invoke();
        Debug.Log("LEVEL FAILED! No more arrows.");
        return;
    }

    // 화살표는 있지만 모두 막혔는지 확인 (Deadlock)
    if (hasRemainingBalloons && _arrows.Count > 0)
    {
        bool anyMovable = false;
        foreach (var arrow in _arrows)
        {
            if (arrow.CanMove()) // ArrowMovement에 구현 필요
            {
                anyMovable = true;
                break;
            }
        }

        if (!anyMovable)
        {
            SetState(GameState.Failed);
            OnLevelFailed?.Invoke();
            Debug.Log("LEVEL FAILED! All arrows blocked (Deadlock).");
        }
    }
}
```

**장점:**
- 완벽한 실패 감지
- 플레이어가 무의미한 클릭 반복 안해도 됨
- 레벨 디자인 오류(실제로 풀 수 없는 레벨)도 감지 가능

**단점:**
- 구현 복잡도 증가
- 매 화살표 탈출 후 모든 남은 화살표 검증 필요 (성능 이슈 가능)
- 엣지 케이스: "지금은 막혔지만 다른 화살표 먼저 빼면 길이 열림" 같은 복잡한 상황 처리 어려움
- CanMove() 구현이 복잡: 단순히 다음 칸이 비었는지만 체크? 아니면 탈출 가능한 전체 경로 존재 여부?

---

## 3. 현재 실패 UI 인프라

**파일**: [GameResultUI.cs](../Scripts/UI/GameResultUI.cs)

실패 UI는 이미 구현되어 있음:

- `ShowFailed()` 메서드 존재 ([line 105](../Scripts/UI/GameResultUI.cs#L105))
- `OnLevelFailed` 이벤트 구독 ([line 57](../Scripts/UI/GameResultUI.cs#L57))
- 실패 메시지: "화살표를 모두 사용했습니다." ([line 32](../Scripts/UI/GameResultUI.cs#L32))
- Restart/Menu 버튼 기능 구현됨

**추가 작업이 필요하다면:**
1. 실패 메시지 텍스트 조정
2. 실패 시 애니메이션/연출 추가
3. 실패 통계 표시 (예: "탈출한 화살표: X/Y", "남은 풍선: N개")
4. 실패 사유 구분 (화살표 소진 vs Deadlock) - 방안 2 선택 시

---

## 4. 권장 사항

**방안 1 (현재 방식 유지)을 추천합니다.**

이유:
1. 레벨 검증 시스템으로 deadlock 없는 레벨만 배포
2. 하이퍼캐주얼 게임에 적합한 명확한 실패 조건
3. 단순한 구현으로 유지보수 용이
4. 게임 플레이 경험에 큰 차이 없음 (정상 레벨 기준)

**방안 2가 필요한 경우:**
- 레벨 생성 알고리즘이 불완전하여 실제로 풀 수 없는 레벨이 자주 생성되는 경우
- 플레이테스트에서 "막힌 화살표만 남아서 계속 클릭하게 됨" 불만이 많은 경우
- 동적 레벨 생성이나 사용자 제작 레벨 등 검증되지 않은 레벨을 플레이하는 경우

---

## 5. 구현 단계 (방안 1 유지 시)

실패 조건 자체는 이미 구현되어 있으므로, 실패 팝업 개선 작업만 필요:

1. **실패 메시지 개선** (선택)
   - 현재: "화살표를 모두 사용했습니다."
   - 개선안: "풍선을 모두 터뜨리지 못했습니다!" 등

2. **실패 연출 추가** (선택)
   - 페이드 인 애니메이션
   - 사운드 효과
   - 남은 풍선 하이라이트

3. **실패 통계 표시** (선택)
   - 탈출한 화살표 수 / 전체 화살표 수
   - 터뜨린 풍선 수 / 전체 풍선 수
   - 진행도 표시 (예: 75% 완료)

---

## 6. 참고 사항

### 승리 조건
- 모든 풍선이 팝되면 클리어
- [GameManager.cs:474-482](../Scripts/Core/GameManager.cs#L474-L482)에서 체크

### 실패 조건 체크 흐름
1. 화살표 탈출 완료 (`OnArrowEscapedHandler`)
2. 화살표 리스트에서 제거 (`_arrows.Remove(arrow)`)
3. 승리 조건 체크 (`CheckWinCondition`)
   - 모든 풍선 팝됨 → 승리 → 클리어 시퀀스
   - 풍선 남음 → 실패 조건 체크 (`CheckFailCondition`)
4. 실패 조건 체크
   - 풍선 남음 + 화살표 없음 → 실패
   - 그 외 → 게임 계속

---

작성일: 2026-01-28