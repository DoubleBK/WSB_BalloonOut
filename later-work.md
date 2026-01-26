# Later Work - 나중에 확인할 이슈들

이 파일은 나중에 확인하고 수정할 이슈들을 정리합니다.

---

## 1. LevelGenerator Facing Arrow 이슈

### 상태
- **우선순위**: Medium
- **상태**: 진단 코드 추가됨, 근본 원인 미해결
- **관련 파일**: `Assets/Scripts/Data/LevelGenerator.cs`

### 문제 설명
화살표 배치 시점에 `WouldCauseFacing` 로직으로 Facing을 방지하도록 구현했으나, 여전히 `[FacingCheck] FACING DETECTED` 에러가 발생합니다.

**Facing이란?**
- 두 화살표의 Head가 인접하고 서로를 향하는 상태
- 이 상태에서는 어느 쪽을 먼저 움직여도 충돌하여 레벨이 풀리지 않음

```
[Arrow2 Head→] [←Arrow1 Head]
    (1,9) R       (2,9) L
```

### 에러 메시지 예시
```
[FacingCheck] FACING DETECTED: arrows 22 and 28
UnityEngine.Debug:LogError (object)
BalloonOut.Data.LevelGenerator:CheckFacingArrows
BalloonOut.Data.LevelGenerator:ValidateGeneratedLevel
BalloonOut.Data.LevelGenerator:ValidateLevel
```

### 현재 구현된 방지 로직
1. **WouldCauseFacing 헬퍼 함수** (Line ~1024)
   - 새 화살표 배치 시 기존 화살표들과 Facing 여부 검사

2. **배치 함수들에 existingBlocks 파라미터 추가**
   - PlaceFirstArrow, FindBlockedPosition, PlaceFallback
   - PlaceFirstArrowBending, FindBlockedPositionBending, PlaceFallbackBending

3. **Filler 배치 시에도 Facing 검사**
   - `allBlocksForFacing` 리스트로 Main + 이전 Filler 추적

### 추가된 진단 코드 (2026-01-26)
1. **Filler 배치 후 즉시 검증** (Line ~931-937)
   - `[CRITICAL] Filler X CAUSED FACING` 로그 확인

2. **ValidateLevel 진단 로깅** (Line ~1112-1153)
   - 각 화살표의 Game→Generator 변환 과정 추적

3. **Round-trip 검증** (Line ~1730-1742)
   - GenerateLevel 성공 후 ValidateLevel 재호출하여 일관성 확인
   - `[RoundTrip] FAILED` 로그 확인

### GPT/Gemini 피드백 평가
| 제안 | 평가 | 이유 |
|------|------|------|
| ValidateLevel에서 U↔D 방향 플립 | **잘못됨** | Y좌표 플립이 dy도 반전시키므로 방향은 그대로 유지해야 함 |
| Filler에 Facing 체크 누락 | **이미 구현됨** | PlaceFallback 내부에서 WouldCauseFacing 호출 중 |

### 확인해야 할 사항
1. **Console 로그 확인**
   - `[WouldCauseFacing] PREVENTED` - 배치 시점에 Facing 방지됨
   - `[CRITICAL] Filler X CAUSED FACING` - WouldCauseFacing이 놓친 케이스
   - `[RoundTrip] FAILED` - 내부/외부 검증 불일치

2. **arrows 22, 28의 정체**
   - Main 화살표인가, Filler인가?
   - 어느 시점에 배치되었는가?

3. **좌표 변환 일관성**
   - Generator → LevelData → BlockData 변환 과정에서 head 위치/방향 유지되는지

### 가설 (우선순위 순)
1. **배치 시점과 검증 시점의 블록 정보 불일치**
   - cells[0]이 항상 Head인지 확인 필요

2. **특정 엣지 케이스에서 WouldCauseFacing 우회**
   - 모든 candidate가 제외되어 fallback 경로로 facing candidate 선택?

3. **Bending 화살표의 direction 재계산 이슈**
   - path 기반 direction 계산이 원본과 다를 수 있음

### 참고 문서
- `BUG_REPORT_FACING_ISSUE.md` - 상세 버그 리포트

---

## 작성 가이드
새 이슈 추가 시 다음 형식을 따라주세요:

```markdown
## N. 이슈 제목

### 상태
- **우선순위**: High/Medium/Low
- **상태**: 미해결/진행중/진단중
- **관련 파일**: 파일 경로

### 문제 설명
문제에 대한 상세 설명

### 확인해야 할 사항
- [ ] 체크리스트 형태로 작성

### 참고 사항
추가 참고 정보
```