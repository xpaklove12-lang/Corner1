# AGENTS.md

## 0. Git

- 커밋 메세지 제안만 가능
- git cli 수행 금지
- 커밋 메세지에 Co-Authored-By 등 AI 공동 작성자 줄을 넣지 않는다

## 1. Comment

- Unity `MonoBehaviour`의 Lifecycle 및 Callback 메서드를 제외한 모든 메서드의 상단에 XML `summary` 주석을 작성한다.
- `summary`에는 다음 내용을 포함한다.
  - 메서드가 수행하는 동작
  - 메서드가 사용하는 주요 입력값
  - 메서드가 반환하는 값 또는 변경하는 주요 상태
- 내부 주석은 짧은 평문 문장으로 작성하며, 2~3문장을 초과하지 않는다.
- 내부 주석에는 이모지와 Markdown 문법을 사용하지 않는다.

```csharp
/// <summary>
/// 이동 입력을 받아 현재 속도를 목표 속도로 변경한다.
/// moveInput을 사용하며, 변경된 속도를 _velocity에 저장한다.
/// </summary>
private void ProcessMovement(Vector2 moveInput)
{
    Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

    // 지면 이동에서는 입력 방향을 즉시 속도에 반영하지 않고 가속도로 처리한다.
    _velocity = Vector3.MoveTowards(
        _velocity,
        direction * _moveSpeed,
        _moveAcceleration * Time.fixedDeltaTime);
}
```

---

## 2. Field

### Inspector 설정값

- Inspector에 노출해야 하는 값만 `[SerializeField]`를 사용한다.
- `[SerializeField] public`은 사용하지 않는다.
- `[SerializeField]`가 필요하지 않은 필드는 `private`으로 선언한다.
### UI 생성 규칙
- UI는 uGUI(com.unity.ugui)와 TextMeshPro로 구성한다. UI Toolkit은 사용하지 않는다.
- UI GameObject는 코드(new GameObject, AddComponent, Instantiate 등)로 생성하지 않는다.
- UI GameObject는 Unity 에디터의 GameObject 생성 메뉴(Hierarchy 우클릭 > UI)로 생성하고, 씬 또는 프리팹에 저장한다.
  - 예: GameObject > UI > Canvas, GameObject > UI > Image, GameObject > UI > Text - TextMeshPro
- 스크립트는 생성된 UI 요소를 [SerializeField]로 참조만 한다.


### 외부 공개 상태

- 클래스 외부에서 값을 참조해야할 경우에만 `public` 프로퍼티를 사용한다.

---

## 3. Field 순서

### 역할 별 1차 분류

필드는 역할에 따라 [Header("")] attribute를 사용하여 그룹화한다.

그룹은 다음 순서로 배치한다.

1. `const`
2. `static`
3. `[Header("")]`
4. ...

### 접근 제한자 별 2차 분류

각 [Header("")] 그룹 내부의 필드는 다음 순서로 배치한다.

1. `[SerializeField] private`
2. `private/protected`
3. `public`

---

## 4. Access Modifier

### Field

- 모든 필드는 접근 제한자를 명시한다.

### Method

- 모든 메서드는 접근 제한자를 명시한다.
- 단, Unity Lifecycle, Callback 메서드는 접근 제한자를 생략한다.
- 단, Unity Lifecycle, Callback 메서드에 virtual, override, abstract 등 Method Modifier을 사용하는 경우에는 접근 제한자를 명시한다.

---

## 5. Using

`using`은 다음 순서로 작성한다.

1. `System`
2. `UnityEngine`
3. Unity 패키지
4. 외부 패키지
5. 프로젝트 namespace

각 그룹 사이에는 한 줄을 넣는다.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using Cinemachine;

using Game.Player;
using Game.System;
```

- 사용하지 않는 `using`은 제거한다.
- 같은 그룹 안에서는 알파벳 순서로 정렬한다.

---

## 6. Naming

- private 필드: `_camelCase`
- public 프로퍼티: `PascalCase`
- 메서드: `PascalCase`
- 지역 변수: `camelCase`
- 상수: `SCREAMING_SNAKE_CASE`
- enum 타입: `PascalCase`
- enum 값: `PascalCase`

---

## 7. Scope

- 요청받은 기능과 직접 관련된 코드만 수정한다.
- 요청받지 않은 클래스, 메서드, 변수의 이름을 변경하지 않는다.
- 요청받지 않은 리팩터링을 하지 않는다.
- 기존 동작을 변경하는 코드는 요청된 경우에만 수정한다.
- 테스트 코드 작성 금지

## 8. Code Structure

- Unity Lifecycle 순서와 Inspector 설정을 고려하여 불필요한 `null` check를 추가하지 않는다.

## 9. 애셋 생성 및 Inspector 설정

- 위치, 회전, 크기, 카메라 구도, 연출 시간 등 제작자가 조절할 값은 Inspector에 노출한다.
- 씬과 프리팹에 저장된 설정값을 기준으로 동작한다.
- Awake, OnEnable, Start 등에서 제작자가 설정한 Transform이나 카메라 값을 하드코딩된 값으로 덮어쓰지 않는다.
- OnValidate는 해당 컴포넌트의 입력값 검증에 사용하며, 다른 오브젝트의 Transform이나 카메라 설정을 자동 변경하지 않는다.
- 초기 배치는 애셋 생성 시 한 번 설정하고, 이후 수동 편집한 값을 보존한다.
- 자동 배치나 자동 크기 조절이 필요하면 명시적인 옵션으로 제공하고 기본값은 비활성화한다.
- 자동 배치는 별도 실행 기능으로 제공하여 제작자가 요청할 때만 적용한다.
- 기능상 반드시 필요한 이동과 초기화는 Inspector에 지정된 기준점과 설정값을 사용한다.
- 서로 독립적으로 편집해야 하는 기계, 종이, 카메라의 설정을 분리한다.

## 10. Spec-Driven Development (SDD)

- 기능 구현 전에 `docs/README.md`를 먼저 읽고, 그 안의 규칙과 절차를 따른다.
- 스펙 없이 기능 구현을 시작하지 않는다.

덕시즌 스탠리 패러블


readonly struct ImmutableStruct 
{
	public readonly int ImmutableField;
	public ImmutableStruct( int initValue)
	{
			immutableField = initValue
	}
}

Class SomeClass
{
	public void SomeClass()
	{
		 ImmutableStruct is = new Immutable(123);
	}

}