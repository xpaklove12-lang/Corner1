# docs 라우터

SDD 작업을 시작할 때 이 파일을 가장 먼저 읽는다.
상황에 맞는 파일만 읽고, 나머지는 읽지 않는다.

## 1. 상황별 참조 파일

| 상황 | 읽을 파일 |
|------|-----------|
| 코드 작성 규칙 | `AGENTS.md` |
| 새 기능 스펙 작성 | `docs/spec-template.md` |
| 기존 기능 수정 | `docs/specs/` 안의 해당 스펙 (아래 스펙 목록에서 찾는다) |
| 기능 구현 | 승인된 해당 스펙 하나만 읽는다 |
| 스펙 작성 규칙 확인 | 이 파일의 2번 항목 |

## 2. 스펙 작성 규칙

- 위치: `docs/specs/NNN-feature-name.md` (NNN은 3자리 순번, 기능당 파일 하나)
- 순번: 아래 스펙 목록의 마지막 번호에 1을 더한다. 같은 번호를 두 번 쓰지 않는다.
- 양식: `docs/spec-template.md`의 섹션 구조와 순서를 그대로 따른다.
- 섹션 삭제 금지: 해당 없는 섹션은 지우지 않고 `해당 없음`으로 적는다.
- 양식의 HTML 주석(`<!-- -->`)은 스펙 문서에 복사하지 않는다.
- 변경 이력, 날짜, 작성자, 버전은 적지 않는다.
- 스펙이 바뀌면 해당 내용을 직접 덮어써서 항상 최신 상태 하나만 유지한다.
- 모든 스펙에 동작 플로우차트를 포함한다. (Mermaid `flowchart TD`)

## 3. 작업 절차

1. 스펙 문서를 작성한다.
2. `미결 사항`이 비어 있는지 확인하고, 사용자 승인을 받는다.
3. 승인된 스펙의 `FR-xxx` 단위로 구현한다.
4. FR이 5개를 넘으면 구현 순서를 정해 FR 묶음 단위로 승인받는다.
5. 구현 후 각 `FR-xxx`가 어떤 코드로 충족되는지 요약해서 보고한다.

- 구현 중 스펙에 없는 요구가 발견되면 구현을 멈추고 스펙 수정을 제안한다.
- 스펙이 모호하면 추측하지 않고 질문한다.

## 4. 스펙 목록

스펙을 새로 만들거나 이름을 바꾸면 이 표를 갱신한다.
상태는 `작성중`, `승인`, `구현완료` 중 하나로 적는다. 구현 순서는 번호 순서다.

| 번호 | 파일 | 한 줄 설명 | 상태 |
|------|------|------------|------|
| 001 | `specs/001-blink-single-condition.md` | 눈 깜빡임 조건을 시간 경과 하나로 단일화 | 구현완료 |
| 003 | `specs/003-remove-winding.md` | 태엽 값, 태엽 문구, 태엽 씬 오브젝트를 모두 삭제 | 구현완료 |
| 004 | `specs/004-return-to-start-option.md` | 모든 대화 노드에 처음 이야기로 돌아가는 공통 선택지 추가 | 구현완료 |
| 005 | `specs/005-burden-gauge.md` | 들여다보는 동안 차오르고 가득 차면 상담이 끝나는 부담감 게이지 | 구현완료 |
| 006 | `specs/006-blink-illusion-rotation.md` | 깜빡임 때 맵과 시선을 함께 무작위로 돌려 맵이 변한 듯한 착시를 준다 | 구현완료 |
| 007 | `specs/007-wall-knockback.md` | 벽이나 잠긴 문에 세게 부딪히면 미로가 지지직 흐려지고 시선이 밀려나며 부담감이 오른다 | 구현완료 |
| 008 | `specs/007-dialogue-log.md` | 지나간 필드 대화, 만남 대화, 엔딩 대사를 L 키로 다시 읽는 대화 기록 창 | 구현완료 |
| 009 | `specs/009-blink-visible-turn.md` | 눈을 뜨며 미로가 층별 시간차로 부드럽게 도는 모습을 보여 준다 | 구현완료 |
| 010 | `specs/010-blink-wall-change-effects.md` | 깜빡임 뒤 동공에서 퍼지는 파문을 따라 벽이 실제로 바뀐다 | 구현완료 |
| 011 | `specs/011-client-rest-request.md` | 내담자의 동공이 떨리고 눈물이 차면 쉬어 줄 수 있고, 쉬어 주면 부담감이 줄어든다 | 구현완료 |
| 012 | `specs/012-fragment-visibility-feedback.md` | 줍지 않은 파편을 맥동과 빛으로 눈에 띄게 하고, 주운 순간 고리와 목록 강조로 알린다 | 구현완료 |
| 013 | `specs/009-cutscene-fast-forward.md` | 조작 없는 컷신·대사 구간을 키를 누르는 동안 빨리감기 | 구현완료 |
| 014 | `specs/014-dialogue-editor.md` | 인물·회차별 통합 CSV의 전체 대사 흐름을 텍스트와 노드 뷰에서 편집 | 승인 |
| 015 | `specs/015-gunner-pooled-bullets.md` | Gunner 고정 방향 예고 발사와 탄환 프리팹 풀링 | 구현완료 |
| 016 | `specs/016-debug-enter-gaze.md` | F2 개발자키로 눈동자 미로 조작 화면 즉시 진입 | 구현완료 |
| 017 | `specs/017-spline-crossfire.md` | 기존 Knot 경로 양 끝에서 빈틈을 남기고 다가오는 풀링 탄막 | 구현완료 |
| 018 | `specs/018-line-obstacle-knot.md` | OriginPos 로컬 Y축으로 0.5초 선 그리기 후 0.5초 이동 | 구현완료 |
| 019 | `specs/019-fragment-layer-portals.md` | 층 개방 시 수집 파편 근처에서 해당 층 목적지로 이동하는 포탈 | 구현완료 |
| 020 | `specs/020-moving-obstacle-layer-rotation.md` | 왕복 장애물의 부모 로컬 좌표 이동으로 층 회전 추종 | 구현완료 |
| 021 | `specs/021-fragment-feedback-lifetime.md` | 파괴된 파편을 참조하는 시각 피드백의 반복 MissingReferenceException 방지 | 구현완료 |
| 022 | `specs/022-inner-layer-obstacle-layout.md` | 2·3층 장애물 프리팹 배치와 포탈 목적지 연결 | 구현완료 |
| 023 | `specs/023-clockwise-obstacle-rotation.md` | 장애물 시계방향 90도 회전과 Inspector 대기 시간 반복 | 구현완료 |
| 024 | `specs/024-three-way-turret.md` | 45도·90도·135도 세 방향 동시 주기 발사 포탑 | 구현완료 |
| 025 | `specs/025-rotating-obstacle-parent.md` | 파편 아래 장애물 묶음을 층 아래로 옮겨 미로 회전 추종 복구 | 구현완료 |
| 026 | `specs/026-play-mode-values.md` | 여러 컴포넌트의 Play Mode Inspector 값 보관과 Edit Mode 일괄 적용 | 구현완료 |
| 027 | `specs/027-remove-portals-turret-parent.md` | 현재 씬 포탈 제거와 세 방향 포탑의 미로 회전 추종 | 구현완료 |
| 028 | `specs/028-musical-note-bullets.md` | 공용 포탑 탄환을 팔분음표 외형으로 변경 | 구현완료 |
| 029 | `specs/029-three-way-note-burst.md` | 세 방향 음표 3회 연속 발사와 Inspector 간격·휴식 설정 | 구현완료 |
| 030 | `specs/030-imperfect-piano-loop.md` | 은재 1층에서 불완전한 연주를 멈추고 틈으로 전진하는 건반 기믹 | 작성중 |
| 031 | `specs/031-encounter-layout-rearrange.md` | 상담 화면 눈 확대와 상태·부담감·파편 칸 좌우 재배치 | 구현완료 |
| 032 | `specs/032-aggression-half-turn.md` | 공격성 오브젝트를 주기마다 Z축 반바퀴 돌렸다가 원점으로 복귀 | 구현완료 |
| 033 | `specs/033-aggression-gunner.md` | 공격성 오브젝트가 회전한 방향으로 주기마다 풀링 탄환 발사 | 구현완료 |
| 034 | `specs/034-aggression-sequential-circle-bullets.md` | 공격성 포대 각도 순차 발사, 원형 탄환, 흰자 직전 소멸 | 구현완료 |
| 035 | `specs/035-aggression-sine-sweep.md` | 공격성 포대 사인파 흔들기 3갈래 탄막 패턴 | 구현완료 |
| 036 | `specs/036-gunner-mirror-angles.md` | 포대 발사 각도마다 정반대(180도 뒤) 방향도 함께 발사 | 구현완료 |
| 037 | `specs/037-fragment-line-puzzle.md` | 파편을 열 때 조각 자리를 바꿔 길을 잇고 말을 걸려 단서에 닿아야 획득 | 구현완료 |
| 038 | `specs/038-eunjae-wary-portrait.md` | 대화 상자 은재 초상화가 흘끔거리고 움츠리는 경계 루프 동작 | 구현완료 |
| 039 | `specs/039-field-perspective-camera.md` | 필드를 살짝 내려다보는 원근 카메라로 보여 주는 2.5D 화면 | 구현완료 |
| 040 | `specs/040-spline-line.md` | 스플라인을 따라 LineRenderer 선을 그리고 에디트 모드에서도 따라오게 함 | 구현완료 |
| 041 | `specs/041-eunjae-wary-sprite-animation.md` | 은재 초상화로 만든 경계 동작 스프라이트 시트와 반복 애니메이션 에셋 (에셋 삭제됨) | 구현완료 |
| 042 | `specs/042-encounter-portrait-panel.md` | 만남 화면 상황 칸 글을 숨기고 흰 테두리를 두른 큰 은재 정지 초상화 표시 | 구현완료 |
| 043 | `specs/043-eunjae-idle-portrait-animation.md` | 눈 미로 시작 화면 은재 초상화를 숨쉬기·머리카락·깜빡임 아이들 애니메이션으로 교체 | 구현완료 |
| 044 | `specs/044-eunjae-to-wary-transition.md` | 일반 은재에서 경계하는 은재로 넘어가는 8프레임 모핑 전환 애니메이션 에셋 | 구현완료 |
| 045 | `specs/045-wary-stance-toggle.md` | 임시 버튼으로 경계태세 전환: 은재 경계 애니메이션 재생과 눈 가운데 공격성 포대 켜기 | 구현완료 |
| 046 | `specs/046-fragment-trace-puzzle.md` | 1층 파편을 열 때 4×4 점 사이 V자 조각을 돌려 모두 하나로 이으면 파편 획득, 퍼즐 중 미로 가리기 | 구현완료 |
| 047 | `specs/047-fragment-flood-puzzle.md` | 2층 파편을 열 때 삼각형 판에 색을 부어 이어진 영역을 퍼뜨리고, 제한 수 안에 판 전체를 파편 감정색으로 채우면 획득 | 구현완료 |
| 048 | `specs/048-puzzle-enter-transition.md` | 파편에 닿으면 파편이 커졌다가 눈 영역을 덮는 네모 퍼즐 창으로 펼쳐지고, 풀거나 그만두면 파편 자리로 줄어들며 눈으로 복귀 | 구현완료 |
