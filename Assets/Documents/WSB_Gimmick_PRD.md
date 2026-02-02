# Balloon Gimmick System PRD

## 개요
풍선에 다양한 특수 효과(기믹)를 부여하는 확장 가능한 시스템 구현.
향후 새로운 기믹 추가가 용이하도록 **인터페이스 기반 설계** 적용.

---

## 1. 핵심 요구사항

### 1.1 첫 번째 기믹: Surprise Balloon
| 항목 | 설명 |
|------|------|
| **외형** | 회색 + "?" 표시 (실제 색상 숨김) |
| **동작** | Queue 맨 아래(활성 위치)에 도달하면 실제 색상 공개 + 파티클 효과 |
| **제약** | 공개되는 색상의 화살표가 그리드에 반드시 존재해야 함 |
| **Undo** | 한번 공개되면 Undo해도 공개 상태 유지 |

### 1.2 두 번째 기믹: Number Balloon
| 항목 | 설명 |
|------|------|
| **외형** | 풍선 중앙에 숫자(N) 표시 |
| **동작** | 같은 색상 화살표 N번 맞춰야 터짐, 매 hit마다 count -1 |
| **제약** | count 값만큼 추가 화살표 필요 (solvability) |
| **Undo** | 스냅샷 기반으로 count 값 복원 |

### 1.3 조합 지원
- Surprise + Number 조합 가능 (예: 숨겨진 색상 + 3번 맞춰야 함)

---

## 2. 아키텍처 설계 (확장성 중심)

### 2.1 설계 원칙
- **Open-Closed Principle**: 새 기믹 추가 시 기존 코드 수정 불필요
- **Strategy Pattern**: 각 기믹이 공통 인터페이스 구현
- **Composition**: 하나의 풍선에 여러 기믹 조합 가능

### 2.2 핵심 컴포넌트

```
┌─────────────────────────┐
│  GimmickDefinitionSO    │  (ScriptableObject)
│  - gimmickId            │
│  - displayName, icon    │
│  - behaviorInstance     │
└───────────┬─────────────┘
            │ references
┌───────────▼─────────────┐
│  IGimmickBehavior       │  (Interface)
│  + OnInitialize()       │
│  + OnBecomeActive()     │
│  + OnHit() → bool       │
│  + OnPop()              │
│  + OnRestore()          │
│  + CreateSnapshot()     │
│  + OnRender()           │
└───────────┬─────────────┘
            │ implements
    ┌───────┴───────┐
    ▼               ▼
┌─────────┐   ┌─────────────┐
│Surprise │   │NumberBalloon│
│Behavior │   │Behavior     │
└─────────┘   └─────────────┘
```

### 2.3 기믹 생명주기 이벤트
| 이벤트 | 호출 시점 | 용도 |
|--------|----------|------|
| `OnInitialize` | 풍선 생성 시 | 초기 상태 설정 |
| `OnBecomeActive` | Queue 맨 앞 도달 시 | Surprise 공개 트리거 |
| `OnHit` | 화살표 맞았을 때 | Number count 감소, 팝 여부 결정 |
| `OnPop` | 터지기 직전 | 정리 작업 |
| `OnRestore` | Undo 시 | 상태 복원 |
| `OnRender` | UI 렌더링 시 | 커스텀 시각 효과 |

---

## 3. 데이터 구조

### 3.1 GimmickInstanceData (기믹 상태)
```csharp
[Serializable]
public class GimmickInstanceData
{
    public string gimmickId;                    // "surprise", "number"
    public Dictionary<string, string> parameters; // 유연한 키-값 저장
}
```

### 3.2 BalloonData (풍선 데이터)
```csharp
[Serializable]
public class BalloonData
{
    public string color;                        // "R", "G", "B" 등
    public List<GimmickInstanceData> gimmicks;  // 적용된 기믹들
}
```

### 3.3 LaneData 확장 (하위 호환성 유지)
```csharp
[Serializable]
public class LaneData
{
    public List<string> balloons;           // 기존 레거시 형식
    public List<BalloonData> balloonData;   // 새 형식 (기믹 포함)

    public List<BalloonData> GetBalloonDataList()
    {
        // balloonData 있으면 사용, 없으면 레거시 변환
    }
}
```

---

## 4. 시각적 표현 (Visual Representation)

### 4.1 구현 방식: 런타임 동적 생성

**선택 이유:**
- 새 기믹 추가 시 기존 Prefab 수정 불필요 (확장성)
- 조합 기믹 처리 용이 (여러 오버레이 동적 추가)
- GimmickDefinitionSO에서 시각 설정 정의 가능

**기존 구조 유지:**
- `Balloon.prefab`: RectTransform + Image (변경 없음)
- 색상 표현: `Image.color = ColorHelper.GetColor(color)` (틴트 방식)

### 4.2 BalloonVisual 컴포넌트

```csharp
// Assets/Scripts/Game/Balloon/BalloonVisual.cs
public class BalloonVisual : MonoBehaviour
{
    [SerializeField] private Image _baseImage;           // 기본 풍선 이미지

    // 동적 생성되는 자식 요소들
    private Image _overlayImage;                          // 기믹 오버레이 (회색 등)
    private TextMeshProUGUI _overlayText;                 // "?" 또는 숫자

    public void SetColor(GameColor color) { ... }
    public void SetGrayOverlay(bool show) { ... }         // Surprise용
    public void SetOverlayText(string text) { ... }       // "?" 또는 숫자
    public void PlayRevealAnimation() { ... }             // 색상 공개
    public void PlayHitAnimation() { ... }                // 숫자 감소
}
```

### 4.3 기믹별 시각 표현

| 기믹 | 오버레이 | 텍스트 | 애니메이션 |
|------|---------|--------|-----------|
| **Surprise** | 회색 반투명 | "?" | 공개 시 페이드아웃 + 파티클 |
| **Number** | 없음 | 숫자 (N) | hit 시 펀치 스케일 + 숫자 감소 |
| **Surprise+Number** | 회색 반투명 | "?" → 숫자 | 공개 후 숫자 표시 |

---

## 5. 파일 구조

```
Assets/Scripts/
├── Game/
│   ├── Gimmick/
│   │   ├── IGimmickBehavior.cs           # 인터페이스
│   │   ├── GimmickRegistry.cs            # 싱글톤 매니저
│   │   ├── GimmickHitResult.cs           # 결과 구조체
│   │   └── Behaviors/
│   │       ├── SurpriseGimmickBehavior.cs
│   │       ├── NumberGimmickBehavior.cs
│   │       └── [향후 기믹 추가]
│   └── Balloon/
│       ├── BalloonInstance.cs            # 런타임 인스턴스
│       └── BalloonVisual.cs              # UI 컴포넌트
├── Data/
│   ├── BalloonData.cs                    # 직렬화 데이터
│   ├── GimmickInstanceData.cs            # 기믹 상태
│   └── GimmickDefinitionSO.cs            # ScriptableObject

Assets/Resources/ScriptableObjects/Gimmicks/
├── Gimmick_Surprise.asset
├── Gimmick_Number.asset
└── [향후 기믹 정의]
```

---

## 6. 기믹 자동 생성 시스템 - 개수 기반 방식

### 6.1 설계 목표
- **카테고리 분리**: Balloon 기믹 / Arrow 기믹 분리
- **확장성**: 새 기믹 추가 시 GeneratorConfig 수정 최소화
- **개수 기반 생성**: 확률이 아닌 정확한 개수 지정

### 6.2 데이터 구조 (개수 기반)

#### GimmickGeneratorConfig (기믹 생성 설정 - 개수 기반)
```csharp
[Serializable]
public class GimmickGeneratorConfig
{
    public string gimmickId;           // "surprise", "number" 등
    public bool enabled;               // 활성화 여부

    // === 개수 기반 생성 ===
    public int count;                  // 생성할 기믹 개수

    // Number 기믹용: 각 풍선별 Hit Count 설정
    public List<int> hitCounts;        // 예: [2, 3, 4] → 3개 Number 풍선, 각각 2, 3, 4 hits

    // 향후 확장용
    public int intParam1;              // 예비 파라미터
    public float floatParam1;
    public string stringParam1;
}
```

### 6.3 UI 설계 (개수 기반)

```
┌─────────────────────────────────────┐
│ Gimmick Settings                    │
├─────────────────────────────────────┤
│ ▼ Balloon Gimmicks                  │
│   ┌─────────────────────────────┐   │
│   │ ☑ Surprise                  │   │
│   │   Count: [____5____]        │   │
│   └─────────────────────────────┘   │
│   ┌─────────────────────────────┐   │
│   │ ☑ Number                    │   │
│   │   Count: [____3____]        │   │
│   │   Hit Counts:               │   │
│   │   [+] Add  [-] Remove       │   │
│   │   ├─ #1: [2] hits           │   │
│   │   ├─ #2: [3] hits           │   │
│   │   └─ #3: [4] hits           │   │
│   └─────────────────────────────┘   │
│ ▼ Arrow Gimmicks                    │
│   (향후 확장)                        │
└─────────────────────────────────────┘
```

### 6.4 생성 로직 (개수 기반)

```csharp
// LevelGenerator.cs - 기믹 적용 로직

// 1. 전체 풍선 개수 계산
int totalBalloons = lanes.Sum(lane => lane.Count);

// 2. Surprise 기믹 적용 (랜덤 위치 선택)
if (surpriseConfig.enabled && surpriseConfig.count > 0)
{
    var randomIndices = GetRandomBalloonIndices(totalBalloons, surpriseConfig.count);
    foreach (var idx in randomIndices)
    {
        balloons[idx].AddGimmick(new GimmickInstanceData("surprise"));
    }
}

// 3. Number 기믹 적용 (개별 hitCount 지정)
if (numberConfig.enabled && numberConfig.hitCounts.Count > 0)
{
    var randomIndices = GetRandomBalloonIndices(totalBalloons, numberConfig.hitCounts.Count);
    for (int i = 0; i < randomIndices.Count; i++)
    {
        var gimmick = new GimmickInstanceData("number");
        gimmick.SetParam("requiredHits", numberConfig.hitCounts[i]);
        balloons[randomIndices[i]].AddGimmick(gimmick);
    }
}
```

---

## 7. Validation 시 기믹 고려

### 7.1 현재 문제점
현재 `LevelValidator`는 기믹을 고려하지 않음:
- **Number 풍선**: 1개 풍선을 터뜨리는데 N개 화살표 필요
- **Surprise 풍선**: 색상이 숨겨져 있지만 실제 색상으로 검증 필요

### 7.2 수정 방안

#### Number 기믹 고려
```csharp
// 풍선별 필요 화살표 수 계산
int GetRequiredArrowCount(BalloonData balloon)
{
    var numberGimmick = balloon.GetGimmick("number");
    if (numberGimmick != null)
    {
        return numberGimmick.GetParamInt("requiredHits", 1);
    }
    return 1;
}

// 색상별 필요 화살표 총합
Dictionary<string, int> CalculateRequiredArrowsByColor(LevelData level)
{
    var required = new Dictionary<string, int>();
    foreach (var lane in level.lanes)
    {
        foreach (var balloon in lane.GetBalloonDataList())
        {
            int count = GetRequiredArrowCount(balloon);
            required[balloon.color] = required.GetValueOrDefault(balloon.color, 0) + count;
        }
    }
    return required;
}
```

#### Surprise 기믹 고려
- Surprise 풍선은 **실제 색상**으로 검증 (숨겨진 상태와 무관)
- 이미 BalloonData.color에 실제 색상이 저장되어 있으므로 추가 작업 불필요

---

## 8. 새 기믹 추가 방법 (향후)

새 기믹 추가 시 **기존 코드 수정 없이**:

1. **Behavior 클래스 생성**
```csharp
// RainbowGimmickBehavior.cs (새 파일)
public class RainbowGimmickBehavior : IGimmickBehavior
{
    public string GimmickId => "rainbow";
    // 인터페이스 구현...
}
```

2. **ScriptableObject 에셋 생성**
   - Unity Editor에서 Create > BalloonOut > Gimmick Definition
   - gimmickId, displayName, icon 설정
   - behaviorInstance 할당

3. **Level Editor에서 사용**
   - 풍선 선택 → "+ Add Gimmick" → 새 기믹 선택

---

## 9. 검증 방법

1. **Surprise 기믹 테스트**
   - 회색 "?" 표시 확인
   - Queue 맨 앞 도달 시 색상 공개 + 파티클
   - Undo 후에도 공개 상태 유지 확인

2. **Number 기믹 테스트**
   - 숫자 표시 확인
   - 매 hit마다 숫자 감소 + 애니메이션
   - count=0 되면 터짐 확인
   - Undo 시 count 복원 확인

3. **조합 테스트**
   - Surprise + Number 동시 적용
   - 공개 후 숫자 표시 및 감소 동작

4. **하위 호환성 테스트**
   - 기존 레벨 파일 정상 로드 확인

5. **개수 기반 생성 테스트**
   - Surprise: 5개 지정 → 정확히 5개 Surprise 풍선 생성 확인
   - Number: [2,3,4] 지정 → 3개 Number 풍선, 각각 2,3,4 hits 확인
