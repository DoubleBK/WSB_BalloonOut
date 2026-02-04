# LevelEditor 프레임워크 이식 가이드

## 개요

LevelEditorWindow.cs를 다른 Unity 프레임워크로 이식할 때 필요한 의존성과 주의사항을 정리한 문서입니다.

---

## 의존성 분석

### 1. Unity API 의존성

| API | 용도 | 비고 |
|-----|------|------|
| `UnityEditor` | EditorWindow, AssetDatabase, EditorGUI 등 | Editor 전용 |
| `UnityEngine` | ScriptableObject, Vector2, Color 등 | Runtime 공용 |

### 2. 내부 프로젝트 의존성

```
LevelEditorWindow.cs
│
├── BalloonOut.Core
│   └── GameEnums.cs
│       ├── GameColor (enum)
│       ├── ArrowDirection (enum)
│       └── ColorHelper (static class)
│
├── BalloonOut.Data
│   ├── LevelData.cs (레벨 구조 정의)
│   ├── StageData.cs (ScriptableObject)
│   ├── LevelGenerator.cs (레벨 생성 알고리즘)
│   ├── LevelValidator.cs (레벨 검증)
│   ├── GimmickGeneratorConfig.cs (기믹 설정)
│   ├── ArrowData.cs (화살표 데이터)
│   ├── BalloonData.cs (풍선 데이터)
│   ├── GimmickInstanceData.cs (기믹 인스턴스)
│   └── Vector2IntSerializable.cs (직렬화 헬퍼)
│
└── BalloonOut.Game.Gimmick
    └── IGimmickBehavior.cs (인터페이스만 참조)
```

### 3. Resources 의존성

| 경로 | 파일 | 용도 |
|------|------|------|
| `Resources/Tables/` | LevelConfigTable.json | 레벨 설정 테이블 |
| `Resources/ScriptableObjects/Stages/` | stage_XXXXXX.asset | 스테이지 저장 경로 |

---

## 필수 이식 파일 목록

### Editor 스크립트
```
Assets/Scripts/Editor/
└── LevelEditorWindow.cs
```

### Core 스크립트
```
Assets/Scripts/Core/
└── GameEnums.cs
```

### Data 스크립트
```
Assets/Scripts/Data/
├── LevelData.cs
├── StageData.cs
├── LevelGenerator.cs
├── LevelValidator.cs
├── GimmickGeneratorConfig.cs
├── ArrowData.cs
├── BalloonData.cs
├── GimmickInstanceData.cs
└── Vector2IntSerializable.cs
```

### Gimmick 인터페이스
```
Assets/Scripts/Game/Gimmick/
└── IGimmickBehavior.cs
```

### Resources
```
Assets/Resources/
├── Tables/
│   └── LevelConfigTable.json
└── ScriptableObjects/
    └── Stages/
        └── (빈 폴더 - 스테이지 저장용)
```

---

## 이식 시 주의사항

### 1. Namespace 충돌

새 프레임워크에 동일한 namespace가 존재하면 충돌 발생:
- `BalloonOut.Core`
- `BalloonOut.Data`
- `BalloonOut.Editor`
- `BalloonOut.Game.Gimmick`

**해결책**: namespace 변경 또는 프레임워크와 분리된 Assembly Definition 사용

### 2. ScriptableObject GUID 문제

StageData.cs의 ScriptableObject는 Unity GUID 기반으로 참조됨.
- 기존 .asset 파일을 그대로 복사하면 Script Missing 발생
- .meta 파일의 GUID가 달라지면 참조 끊어짐

**해결책**:
1. StageData.cs와 함께 .meta 파일도 복사
2. 또는 기존 .asset 파일을 새로 생성된 StageData로 재연결

### 3. 하드코딩된 경로

LevelEditorWindow.cs에 하드코딩된 경로:
```csharp
"Assets/Resources/ScriptableObjects/Stages/"  // 스테이지 저장
"Resources/Tables/LevelConfigTable"           // 설정 테이블
```

**해결책**: 새 프레임워크에 동일 폴더 구조 생성 또는 경로 수정

### 4. 게임플레이 코드와 분리됨

LevelEditor는 다음 코드에 의존하지 **않음**:
- GameManager.cs
- ArrowController.cs
- GridSystem.cs
- QueueUI.cs
- HomingArrow.cs

따라서 게임플레이 로직 없이도 레벨 편집 기능만 독립적으로 사용 가능.

---

## 이식 절차

### Step 1: 폴더 구조 생성
```
NewProject/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/
│   │   ├── Data/
│   │   ├── Editor/
│   │   └── Game/Gimmick/
│   └── Resources/
│       ├── Tables/
│       └── ScriptableObjects/Stages/
```

### Step 2: 파일 복사 (with .meta)
1. 위 "필수 이식 파일 목록" 섹션의 모든 .cs 파일 복사
2. 각 파일의 .meta 파일도 함께 복사 (GUID 유지)

### Step 3: Resources 복사
1. `LevelConfigTable.json` 복사
2. 기존 스테이지 .asset 파일 필요시 함께 복사

### Step 4: 검증
1. Unity Editor 재시작
2. `Tools > Balloon Out > Level Editor` 메뉴 확인
3. 새 레벨 생성 및 저장 테스트

---

## 결론

| 항목 | 결과 |
|------|------|
| LevelEditor 단독 이식 | **불가** |
| Data Layer와 함께 이식 | **가능** |
| 필요 파일 수 | 약 12개 (.cs) + Resources |
| 게임플레이 코드 필요 | **불필요** |

LevelEditor는 데이터 레이어(LevelData, LevelGenerator 등)에만 의존하므로, 해당 파일들을 함께 이식하면 다른 프레임워크에서도 독립적으로 사용할 수 있습니다.

---

*문서 작성일: 2026-02-04*
