# 개발 히스토리

## 2026-02-04 작업 내역

### 1. 색상 일관성 수정

#### 문제
- 화살표와 풍선의 색상이 미묘하게 다름
- `ArrowVisualRenderer.GetUnityColor()`와 `ColorHelper.Colors`가 별도로 정의되어 있었음

#### 해결
- `ArrowVisualRenderer.GetUnityColor()` → `ColorHelper.GetColor()` 호출로 통일

**수정 파일:**
- `Assets/Scripts/Game/Arrow/ArrowVisualRenderer.cs`

---

### 2. 색상 품질 개선

#### 문제
- Red가 Pink처럼 보임 (기존 색상이 coral/pink 계열)
- HDR 효과 제거 후 색상이 덜 세련되어 보임

#### 해결
- `BaseColors`를 표준 게임용 색상으로 변경
- `Colors` 딕셔너리에 HDR ×2 배율 복원

**수정 파일:**
- `Assets/Scripts/Core/GameEnums.cs` (ColorHelper 클래스)

```csharp
// BaseColors - 표준 색상
{ GameColor.Red, new Color(0.95f, 0.2f, 0.2f) }  // 선명한 빨강

// Colors - HDR 적용
{ GameColor.Red, new Color(0.95f * 2f, 0.2f * 2f, 0.2f * 2f) }
```

---

### 3. SFX 추가

#### 요청
- 화살표 탭/클릭 효과음 (이동 시작 시)
- 풍선 HIT 효과음

#### 구현
**SFXManager.cs:**
- `_arrowPickClip` 필드 추가
- `_balloonHitClip` 필드 추가
- `PlayArrowPick()` 메서드 추가
- `PlayBalloonHit()` 메서드 추가
- `LoadDefaultClips()`에 리소스 로드 추가

**ArrowMovement.cs:**
- `StartMove()`에서 `SFXManager.Instance?.PlayArrowPick()` 호출

**QueueUI.cs:**
- `TryPopBalloonWithGimmick()`에서 `SFXManager.Instance?.PlayBalloonHit()` 호출

**리소스 경로:**
- `Sound/SFX/AudioClip/SND_Ballon_pick`
- `Sound/SFX/AudioClip/SND_Hit`

---

### 4. Android/BlueStacks 성능 최적화

#### 문제
- BlueStacks에서 프레임 드랍 발생
- Arrow 이동 시 끊김 현상

#### 원인 분석
1. **Debug.Log 302개** - Stack Trace 수집 오버헤드
2. **VSync 활성화** - 에뮬레이터와 충돌
3. **타겟 프레임 레이트 미설정**
4. **매 프레임 List 할당** - GC 스파이크
5. **DOTween Update 모드** - 프레임 드랍 시 끊김

#### 해결

##### 4.1 GameLogger 유틸리티 생성
**신규 파일:** `Assets/Scripts/Core/GameLogger.cs`

```csharp
public static class GameLogger
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message)
    {
        Debug.Log(message);
    }
}
```
- 릴리스 빌드에서 Log 호출 자체 제거됨

##### 4.2 GameManager 성능 설정
**수정 파일:** `Assets/Scripts/Core/GameManager.cs`

```csharp
private void InitializePerformanceSettings()
{
    // 타겟 프레임 레이트 설정 (60fps)
    Application.targetFrameRate = 60;

    // VSync 비활성화 (모바일/에뮬레이터에서 더 부드러움)
    QualitySettings.vSyncCount = 0;

    // 릴리스 빌드: Stack Trace 비활성화
#if !UNITY_EDITOR
    Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
}
```

##### 4.3 ArrowMovement DOTween 최적화
**수정 파일:** `Assets/Scripts/Game/Arrow/ArrowMovement.cs`

**변경 1: 재사용 리스트 추가**
```csharp
private List<Vector2> _animatedPositions = new List<Vector2>();
private List<Vector2> _fullPath = new List<Vector2>();
```

**변경 2: DOTween Update 모드**
```csharp
.SetUpdate(UpdateType.Late, true)  // LateUpdate + 시간 스케일 독립
```

**변경 3: 애니메이션 메서드에서 리스트 재사용**
```csharp
// Before: new List<Vector2>() 매 프레임 생성
// After: _animatedPositions.Clear() 재사용
```

---

### 5. 최적화 요약 표

| 항목 | 파일 | 내용 | 효과 |
|------|------|------|------|
| 프레임 레이트 | GameManager.cs | 60fps 고정 | 안정적 프레임 |
| VSync | GameManager.cs | 비활성화 | 에뮬레이터 호환성 |
| Stack Trace | GameManager.cs | 릴리스에서 비활성화 | Debug.Log 오버헤드 감소 |
| 조건부 로깅 | GameLogger.cs | Conditional 속성 | 릴리스에서 호출 제거 |
| DOTween | ArrowMovement.cs | LateUpdate 모드 | 부드러운 애니메이션 |
| GC 최적화 | ArrowMovement.cs | 리스트 재사용 | GC 스파이크 방지 |

---

### 6. 수정된 파일 목록

| 파일 | 작업 |
|------|------|
| `Assets/Scripts/Core/GameEnums.cs` | 색상 정의 수정 (HDR ×2) |
| `Assets/Scripts/Game/Arrow/ArrowVisualRenderer.cs` | GetUnityColor() → ColorHelper 위임 |
| `Assets/Scripts/Core/SFXManager.cs` | Arrow Pick, Balloon Hit SFX 추가 |
| `Assets/Scripts/Game/Arrow/ArrowMovement.cs` | SFX 호출 + DOTween 최적화 |
| `Assets/Scripts/UI/QueueUI.cs` | Balloon Hit SFX 호출 추가 |
| `Assets/Scripts/Core/GameManager.cs` | 성능 설정 초기화 추가 |
| `Assets/Scripts/Core/GameLogger.cs` | **신규** - 조건부 로깅 유틸리티 |

---

### 7. 향후 고려 사항

1. **Debug.Log 완전 제거**: 모든 `Debug.Log`를 `GameLogger.Log`로 교체하면 릴리스 빌드에서 완전히 제거됨
2. **Unity Profiler 테스트**: 실제 Android 기기에서 Profiler 연결하여 성능 분석
3. **Quality Settings**: Android용 별도 Quality Level 설정 고려
