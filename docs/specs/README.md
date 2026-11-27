# 스펙 문서 안내

이 폴더는 기능 영역별 스펙을 담는다. 작업 절차와 작성 규칙은 [`../CONTRIBUTING.md`](../CONTRIBUTING.md), 바꾸려는 것에서 담당 스펙과 요구사항 ID를 찾는 길잡이는 [`ROUTER.md`](ROUTER.md), 새 스펙 양식은 [`_TEMPLATE.md`](_TEMPLATE.md)에 있다. 스펙 파일 이름에는 번호를 붙이지 않는다.

## 1. 스펙 목록

영역 파일을 새로 만들거나 이름을 바꿀 때만 이 표를 고친다. 요구사항별 상태는 각 스펙 2절에 있다.

| 접두어 | 파일 | 다루는 것 |
|--------|------|-----------|
| `BLINK` | [`blink-maze.md`](blink-maze.md) | 눈 깜빡임 조건, 깜빡임 착시 회전, 눈을 뜨며 보이는 미로 회전, 파문을 따라 바뀌는 벽 |
| `BURDEN` | [`burden.md`](burden.md) | 부담감 게이지, 벽·잠긴 문 부딪힘과 밀려남, 내담자의 휴식 신호와 쉬어 주기 |
| `DLG` | [`dialogue.md`](dialogue.md) | 만남 시작과 들여다보기, 처음 이야기로 돌아가는 선택지, 대화 기록 창, 컷신 빨리감기, 선택지로 여는 층 문 |
| `EMO` | [`emotion-maze.md`](emotion-maze.md) | 내담자 감정 상태와 눈 표현 |
| `MEM` | [`memory-fragments.md`](memory-fragments.md) | 파편 가시성과 획득 알림, 파편 회상 재연과 추가 파편, 단서 해금 알림과 다시 보기 |
| `EDITOR` | [`dialogue-editor.md`](dialogue-editor.md) | 인물·회차별 통합 CSV 대사를 텍스트와 노드 뷰에서 편집하는 도구 |
| `OPEN` | [`opening.md`](opening.md) | 시작 독백과 첫 손님 입장 |
| `FIELD` | [`field.md`](field.md) | 필드 인물의 전신 모습, 걷기·앉기·퇴장 표시, 주인공 외형, 말을 걸 수 있는 NPC 테두리 |
| `CUT` | [`intro-cuttoon.md`](intro-cuttoon.md) | 운석 충돌부터 수면 캡슐까지 만화 칸이 차례로 열리는 인트로 컷툰, 그레이 박스 씬 |

## 2. 옛 번호 대응

번호 스펙(`NNN-*.md`)을 영역 스펙으로 합쳤다. 옛 커밋 제목의 `feat(015)`나 옛 문서의 "스펙 010 FR-017"은 아래 표로 새 ID를 찾는다.

### 옛 파일 단위

옛 번호 007과 009는 파일이 두 개씩이다. 옛 목록의 "008"은 `007-dialogue-log`, "013"은 `009-cutscene-fast-forward`를 가리킨다.

| 옛 번호 | 옛 파일 | 새 파일 | 새 ID 범위 |
|---|---|---|---|
| 001 | `001-blink-single-condition.md` | [`blink-maze.md`](blink-maze.md) | `BLINK-01`~`BLINK-04` |
| 003 | `003-remove-winding.md` | [`dialogue.md`](dialogue.md) | `DLG-01`~`DLG-06` (FR-001, FR-007은 `dialogue.md` 8절로 옮김) |
| 004 | `004-return-to-start-option.md` | [`dialogue.md`](dialogue.md) | `DLG-07`~`DLG-23` |
| 005 | `005-burden-gauge.md` | [`burden.md`](burden.md) | `BURDEN-01`~`BURDEN-09` |
| 006 | `006-blink-illusion-rotation.md` | [`blink-maze.md`](blink-maze.md) | `BLINK-05`~`BLINK-16` |
| 007 | `007-wall-knockback.md` | [`burden.md`](burden.md) | `BURDEN-10`~`BURDEN-22` |
| 007 | `007-dialogue-log.md` | [`dialogue.md`](dialogue.md) | `DLG-24`~`DLG-31` |
| 008 (별칭) | `007-dialogue-log.md` | [`dialogue.md`](dialogue.md) | `DLG-24`~`DLG-31` |
| 009 | `009-blink-visible-turn.md` | [`blink-maze.md`](blink-maze.md) | `BLINK-17`~`BLINK-28` (일부는 `BLINK-01`, `BLINK-05`, `BLINK-15`에 합침) |
| 009 | `009-cutscene-fast-forward.md` | [`dialogue.md`](dialogue.md) | `DLG-32`~`DLG-39` |
| 010 | `010-blink-wall-change-effects.md` | [`blink-maze.md`](blink-maze.md) | `BLINK-29`~`BLINK-41` (일부는 `BLINK-01`, `BLINK-11`, `BLINK-15`, `BLINK-21`, `BLINK-25`에 합침) |
| 011 | `011-client-rest-request.md` | [`burden.md`](burden.md) | `BURDEN-23`~`BURDEN-36` |
| 012 | `012-fragment-visibility-feedback.md` | [`memory-fragments.md`](memory-fragments.md) | `MEM-01`~`MEM-05` |
| 013 (별칭) | `009-cutscene-fast-forward.md` | [`dialogue.md`](dialogue.md) | `DLG-32`~`DLG-39` |
| 014 | `014-dialogue-editor.md` | [`dialogue-editor.md`](dialogue-editor.md) | `EDITOR-01`~`EDITOR-38` |
| 015 | `015-emotion-maze-objects.md` | [`emotion-maze.md`](emotion-maze.md) | `EMO-01`~`EMO-20` |
| 016 | `016-fragment-flashback.md` | [`memory-fragments.md`](memory-fragments.md) | `MEM-06`~`MEM-24` |
| 017 | `017-memory-linking.md` | [`memory-fragments.md`](memory-fragments.md) | `MEM-25`~`MEM-33` |
| 018 | `018-choice-opens-gate.md` | [`dialogue.md`](dialogue.md) | `DLG-40`~`DLG-44` (FR-005는 `DLG-22`에 합침) |
| 019 | `019-clue-unlock-feedback-and-recap.md` | [`memory-fragments.md`](memory-fragments.md) | `MEM-34`~`MEM-44` (FR-014는 `MEM-04`에 합침) |
| 없음 | `docs/spec-opening-monologue-door-arrival.md` | [`opening.md`](opening.md) | `OPEN-01`~`OPEN-06` |
| 009 | `009-field-character-animation.md` (feature/charactrers-model-add 브랜치) | [`field.md`](field.md) | `FIELD-01`~`FIELD-06` (FR-002는 `FIELD-02`에서 010 FR-003과 합침) |
| 010 | `010-walk-cycle-and-player.md` (feature/charactrers-model-add 브랜치) | [`field.md`](field.md) | `FIELD-07`~`FIELD-10` (FR-003은 `FIELD-02`에 합침) |
| 011 | `011-npc-interaction-outline.md` (feature/charactrers-model-add 브랜치) | [`field.md`](field.md) | `FIELD-11`~`FIELD-12` |

번호 파일은 모두 `docs/specs/`에 있었고, 시작 독백 문서만 `docs/` 바로 아래에 있었다.

feature/charactrers-model-add 브랜치의 009·010·011은 이 브랜치의 `009-blink-visible-turn`·`009-cutscene-fast-forward`, `010-blink-wall-change-effects`, `011-client-rest-request`와 번호가 겹치므로 파일 이름으로 구별한다.

### 요구사항 단위

한 새 ID에 옛 FR 여러 개를 합친 곳은 "합침"으로, 요구사항으로 옮기지 않은 FR과 옛 스펙에서 건너뛴 번호도 그대로 적는다.

| 옛 스펙 | 옛 FR | 새 ID |
|---|---|---|
| `001-blink-single-condition` | FR-001 | `BLINK-01` |
| `001-blink-single-condition` | FR-002 | `BLINK-02` |
| `001-blink-single-condition` | FR-003 | `BLINK-03` |
| `001-blink-single-condition` | FR-004 | `BLINK-04` |
| `003-remove-winding` | FR-001 | 옮기지 않음: `dialogue.md` 8절 "태엽 시스템은 제거됨" |
| `003-remove-winding` | FR-002 | `DLG-01` |
| `003-remove-winding` | FR-003 | `DLG-02` |
| `003-remove-winding` | FR-004 | `DLG-03` |
| `003-remove-winding` | FR-005 | `DLG-04` |
| `003-remove-winding` | FR-006 | `DLG-05` |
| `003-remove-winding` | FR-007 | 옮기지 않음: `dialogue.md` 8절 "태엽 시스템은 제거됨" |
| `003-remove-winding` | FR-008 | `DLG-06` |
| `004-return-to-start-option` | FR-001 | `DLG-07` |
| `004-return-to-start-option` | FR-002 | `DLG-08` |
| `004-return-to-start-option` | FR-003 | `DLG-09` |
| `004-return-to-start-option` | FR-004 | `DLG-10` |
| `004-return-to-start-option` | FR-005 | `DLG-11` |
| `004-return-to-start-option` | FR-006 | `DLG-12` |
| `004-return-to-start-option` | FR-007 | `DLG-13` |
| `004-return-to-start-option` | FR-008 | `DLG-14` |
| `004-return-to-start-option` | FR-009 | `DLG-15` |
| `004-return-to-start-option` | FR-010 | `DLG-16` |
| `004-return-to-start-option` | FR-011 | `DLG-17` |
| `004-return-to-start-option` | FR-012 | `DLG-18` |
| `004-return-to-start-option` | FR-013 | `DLG-19` |
| `004-return-to-start-option` | FR-014 | `DLG-20` |
| `004-return-to-start-option` | FR-015 | `DLG-21` |
| `004-return-to-start-option` | FR-016 | `DLG-22` (018 FR-005와 합침) |
| `004-return-to-start-option` | FR-017 | `DLG-23` |
| `005-burden-gauge` | FR-001 | `BURDEN-01` |
| `005-burden-gauge` | FR-002 | `BURDEN-02` |
| `005-burden-gauge` | FR-003 | `BURDEN-03` |
| `005-burden-gauge` | FR-004 | `BURDEN-04` |
| `005-burden-gauge` | FR-005 | `BURDEN-05` |
| `005-burden-gauge` | FR-006 | `BURDEN-06` |
| `005-burden-gauge` | FR-007 | `BURDEN-07` |
| `005-burden-gauge` | FR-008 | `BURDEN-08` |
| `005-burden-gauge` | FR-009 | `BURDEN-09` |
| `006-blink-illusion-rotation` | FR-001 | `BLINK-05` |
| `006-blink-illusion-rotation` | FR-002 | `BLINK-06` |
| `006-blink-illusion-rotation` | FR-003 | `BLINK-07` |
| `006-blink-illusion-rotation` | FR-004 | `BLINK-08` |
| `006-blink-illusion-rotation` | FR-005 | `BLINK-09` |
| `006-blink-illusion-rotation` | FR-006 | `BLINK-10` |
| `006-blink-illusion-rotation` | FR-007 | `BLINK-11` |
| `006-blink-illusion-rotation` | FR-008 | `BLINK-12` |
| `006-blink-illusion-rotation` | FR-009 | `BLINK-13` |
| `006-blink-illusion-rotation` | FR-010 | `BLINK-14` |
| `006-blink-illusion-rotation` | FR-011 | `BLINK-15` |
| `006-blink-illusion-rotation` | FR-012 | `BLINK-16` |
| `007-dialogue-log` | FR-001 | `DLG-24` |
| `007-dialogue-log` | FR-002 | `DLG-25` |
| `007-dialogue-log` | FR-003 | `DLG-26` |
| `007-dialogue-log` | FR-004 | `DLG-27` |
| `007-dialogue-log` | FR-005 | `DLG-28` |
| `007-dialogue-log` | FR-006 | `DLG-29` |
| `007-dialogue-log` | FR-007 | `DLG-30` |
| `007-dialogue-log` | FR-008 | `DLG-31` |
| `007-wall-knockback` | FR-000 | `BURDEN-10` |
| `007-wall-knockback` | FR-001 | `BURDEN-11` |
| `007-wall-knockback` | FR-002 | `BURDEN-12` |
| `007-wall-knockback` | FR-003 | `BURDEN-13` |
| `007-wall-knockback` | FR-004 | `BURDEN-14` |
| `007-wall-knockback` | FR-005 | `BURDEN-15` |
| `007-wall-knockback` | FR-006 | `BURDEN-16` |
| `007-wall-knockback` | FR-007 | `BURDEN-17` |
| `007-wall-knockback` | FR-008 | `BURDEN-18` |
| `007-wall-knockback` | FR-009 | `BURDEN-19` |
| `007-wall-knockback` | FR-010 | `BURDEN-20` |
| `007-wall-knockback` | FR-011 | `BURDEN-21` |
| `007-wall-knockback` | FR-012 | `BURDEN-22` |
| `009-blink-visible-turn` | FR-001 | `BLINK-05` (006 FR-001과 합침) |
| `009-blink-visible-turn` | FR-002 | `BLINK-17` |
| `009-blink-visible-turn` | FR-003 | `BLINK-18` |
| `009-blink-visible-turn` | FR-004 | `BLINK-19` |
| `009-blink-visible-turn` | FR-005 | `BLINK-20` |
| `009-blink-visible-turn` | FR-006 | `BLINK-21` |
| `009-blink-visible-turn` | FR-007 | `BLINK-22` |
| `009-blink-visible-turn` | FR-008 | `BLINK-15` (006 FR-011과 합침) |
| `009-blink-visible-turn` | FR-009 | `BLINK-23` |
| `009-blink-visible-turn` | FR-010 | `BLINK-01` (001 FR-001과 합침) |
| `009-blink-visible-turn` | FR-011 | `BLINK-24` |
| `009-blink-visible-turn` | FR-012 | 옛 스펙에 없는 번호 |
| `009-blink-visible-turn` | FR-013 | `BLINK-25` |
| `009-blink-visible-turn` | FR-014 | `BLINK-26` |
| `009-blink-visible-turn` | FR-015 | `BLINK-27` |
| `009-blink-visible-turn` | FR-016 | `BLINK-28` |
| `009-cutscene-fast-forward` | FR-001 | `DLG-32` |
| `009-cutscene-fast-forward` | FR-002 | `DLG-33` |
| `009-cutscene-fast-forward` | FR-003 | `DLG-34` |
| `009-cutscene-fast-forward` | FR-004 | `DLG-35` |
| `009-cutscene-fast-forward` | FR-005 | `DLG-36` |
| `009-cutscene-fast-forward` | FR-006 | `DLG-37` |
| `009-cutscene-fast-forward` | FR-007 | `DLG-38` |
| `009-cutscene-fast-forward` | FR-008 | `DLG-39` |
| `010-blink-wall-change-effects` | FR-001 | `BLINK-29` |
| `010-blink-wall-change-effects` | FR-002 | `BLINK-30` |
| `010-blink-wall-change-effects` | FR-003 | `BLINK-31` |
| `010-blink-wall-change-effects` | FR-004 | `BLINK-32` |
| `010-blink-wall-change-effects` | FR-005 | `BLINK-33` |
| `010-blink-wall-change-effects` | FR-006 | `BLINK-15` (006 FR-011과 합침) |
| `010-blink-wall-change-effects` | FR-007 | `BLINK-21` (009-blink-visible-turn FR-006과 합침) |
| `010-blink-wall-change-effects` | FR-008 | `BLINK-01` (001 FR-001과 합침) |
| `010-blink-wall-change-effects` | FR-009 | `BLINK-34` |
| `010-blink-wall-change-effects` | FR-010 | `BLINK-35` |
| `010-blink-wall-change-effects` | FR-011 | `BLINK-11` (006 FR-007과 합침) |
| `010-blink-wall-change-effects` | FR-012 | `BLINK-36` |
| `010-blink-wall-change-effects` | FR-013 | `BLINK-37` |
| `010-blink-wall-change-effects` | FR-014 | 옛 스펙에 없는 번호 |
| `010-blink-wall-change-effects` | FR-015 | `BLINK-38` |
| `010-blink-wall-change-effects` | FR-016 | `BLINK-25` (009-blink-visible-turn FR-013과 합침) |
| `010-blink-wall-change-effects` | FR-017 | `BLINK-39` |
| `010-blink-wall-change-effects` | FR-018 | `BLINK-40` |
| `010-blink-wall-change-effects` | FR-019 | `BLINK-41` |
| `011-client-rest-request` | FR-001 | `BURDEN-23` |
| `011-client-rest-request` | FR-002 | `BURDEN-24` |
| `011-client-rest-request` | FR-003 | `BURDEN-25` |
| `011-client-rest-request` | FR-004 | `BURDEN-26` |
| `011-client-rest-request` | FR-005 | `BURDEN-27` |
| `011-client-rest-request` | FR-006 | `BURDEN-28` |
| `011-client-rest-request` | FR-007 | `BURDEN-29` |
| `011-client-rest-request` | FR-008 | `BURDEN-30` |
| `011-client-rest-request` | FR-009 | `BURDEN-31` |
| `011-client-rest-request` | FR-010 | `BURDEN-32` |
| `011-client-rest-request` | FR-011 | `BURDEN-33` |
| `011-client-rest-request` | FR-012 | `BURDEN-34` |
| `011-client-rest-request` | FR-013 | `BURDEN-35` |
| `011-client-rest-request` | FR-014 | `BURDEN-36` |
| `012-fragment-visibility-feedback` | FR-001 | `MEM-01` |
| `012-fragment-visibility-feedback` | FR-002 | `MEM-02` |
| `012-fragment-visibility-feedback` | FR-003 | `MEM-03` |
| `012-fragment-visibility-feedback` | FR-004 | `MEM-04` |
| `012-fragment-visibility-feedback` | FR-005 | `MEM-05` |
| `014-dialogue-editor` | FR-001 | `EDITOR-01` |
| `014-dialogue-editor` | FR-002 | `EDITOR-02` |
| `014-dialogue-editor` | FR-003 | `EDITOR-03` |
| `014-dialogue-editor` | FR-004 | `EDITOR-04` |
| `014-dialogue-editor` | FR-005 | `EDITOR-05` |
| `014-dialogue-editor` | FR-006 | `EDITOR-06` |
| `014-dialogue-editor` | FR-007 | `EDITOR-07` |
| `014-dialogue-editor` | FR-008 | `EDITOR-08` |
| `014-dialogue-editor` | FR-009 | `EDITOR-09` |
| `014-dialogue-editor` | FR-010 | `EDITOR-10` |
| `014-dialogue-editor` | FR-011 | `EDITOR-11` |
| `014-dialogue-editor` | FR-012 | `EDITOR-12` |
| `014-dialogue-editor` | FR-013 | `EDITOR-13` |
| `014-dialogue-editor` | FR-014 | `EDITOR-14` |
| `014-dialogue-editor` | FR-015 | `EDITOR-15` |
| `014-dialogue-editor` | FR-016 | `EDITOR-16` |
| `014-dialogue-editor` | FR-017 | `EDITOR-17` |
| `014-dialogue-editor` | FR-018 | `EDITOR-18` |
| `014-dialogue-editor` | FR-019 | `EDITOR-19` |
| `014-dialogue-editor` | FR-020 | `EDITOR-20` |
| `014-dialogue-editor` | FR-021 | `EDITOR-21` |
| `014-dialogue-editor` | FR-022 | `EDITOR-22` |
| `014-dialogue-editor` | FR-023 | `EDITOR-23` |
| `014-dialogue-editor` | FR-024 | `EDITOR-24` |
| `014-dialogue-editor` | FR-025 | `EDITOR-25` |
| `014-dialogue-editor` | FR-026 | `EDITOR-26` |
| `014-dialogue-editor` | FR-027 | `EDITOR-27` |
| `014-dialogue-editor` | FR-028 | `EDITOR-28` |
| `014-dialogue-editor` | FR-029 | `EDITOR-29` |
| `014-dialogue-editor` | FR-030 | `EDITOR-30` |
| `014-dialogue-editor` | FR-031 | `EDITOR-31` |
| `014-dialogue-editor` | FR-032 | `EDITOR-32` |
| `014-dialogue-editor` | FR-033 | `EDITOR-33` |
| `014-dialogue-editor` | FR-034 | `EDITOR-34` |
| `014-dialogue-editor` | FR-035 | `EDITOR-35` |
| `014-dialogue-editor` | FR-036 | `EDITOR-36` |
| `014-dialogue-editor` | FR-037 | `EDITOR-37` |
| `014-dialogue-editor` | FR-038 | `EDITOR-38` |
| `015-emotion-maze-objects` | FR-001 | `EMO-01` |
| `015-emotion-maze-objects` | FR-002 | `EMO-02` |
| `015-emotion-maze-objects` | FR-003 | `EMO-03` |
| `015-emotion-maze-objects` | FR-004 | `EMO-04` |
| `015-emotion-maze-objects` | FR-005 | `EMO-05` |
| `015-emotion-maze-objects` | FR-006 | `EMO-06` |
| `015-emotion-maze-objects` | FR-007 | `EMO-07` |
| `015-emotion-maze-objects` | FR-008 | `EMO-08` |
| `015-emotion-maze-objects` | FR-009 | `EMO-09` |
| `015-emotion-maze-objects` | FR-010 | `EMO-10` |
| `015-emotion-maze-objects` | FR-011 | `EMO-11` |
| `015-emotion-maze-objects` | FR-012 | `EMO-12` |
| `015-emotion-maze-objects` | FR-013 | `EMO-13` |
| `015-emotion-maze-objects` | FR-014 | `EMO-14` |
| `015-emotion-maze-objects` | FR-015 | `EMO-15` |
| `015-emotion-maze-objects` | FR-016 | `EMO-16` |
| `015-emotion-maze-objects` | FR-017 | `EMO-17` |
| `015-emotion-maze-objects` | FR-018 | `EMO-18` |
| `015-emotion-maze-objects` | FR-019 | `EMO-19` |
| `015-emotion-maze-objects` | FR-020 | `EMO-20` |
| `016-fragment-flashback` | FR-001 | `MEM-06` |
| `016-fragment-flashback` | FR-002 | `MEM-07` |
| `016-fragment-flashback` | FR-003 | `MEM-08` |
| `016-fragment-flashback` | FR-004 | `MEM-09` |
| `016-fragment-flashback` | FR-005 | `MEM-10` |
| `016-fragment-flashback` | FR-006 | `MEM-11` |
| `016-fragment-flashback` | FR-007 | `MEM-12` |
| `016-fragment-flashback` | FR-008 | `MEM-13` |
| `016-fragment-flashback` | FR-009 | `MEM-14` |
| `016-fragment-flashback` | FR-010 | `MEM-16` |
| `016-fragment-flashback` | FR-011 | `MEM-17` |
| `016-fragment-flashback` | FR-012 | `MEM-18` |
| `016-fragment-flashback` | FR-013 | `MEM-19` |
| `016-fragment-flashback` | FR-014 | `MEM-20` |
| `016-fragment-flashback` | FR-015 | `MEM-21` |
| `016-fragment-flashback` | FR-016 | `MEM-22` |
| `016-fragment-flashback` | FR-017 | `MEM-23` |
| `016-fragment-flashback` | FR-018 | `MEM-24` |
| `016-fragment-flashback` | FR-019 | `MEM-15` |
| `017-memory-linking` | FR-001 | `MEM-25` |
| `017-memory-linking` | FR-002 | `MEM-26` |
| `017-memory-linking` | FR-003 | `MEM-27` |
| `017-memory-linking` | FR-004 | `MEM-28` |
| `017-memory-linking` | FR-005 | `MEM-29` |
| `017-memory-linking` | FR-006 | `MEM-30` |
| `017-memory-linking` | FR-007 | `MEM-31` |
| `017-memory-linking` | FR-008 | `MEM-33` |
| `017-memory-linking` | FR-009 | `MEM-32` |
| `018-choice-opens-gate` | FR-001 | `DLG-40` |
| `018-choice-opens-gate` | FR-002 | `DLG-41` |
| `018-choice-opens-gate` | FR-003 | `DLG-42` |
| `018-choice-opens-gate` | FR-004 | `DLG-43` |
| `018-choice-opens-gate` | FR-005 | `DLG-22` (004 FR-016과 합침) |
| `018-choice-opens-gate` | FR-006 | `DLG-44` |
| `019-clue-unlock-feedback-and-recap` | FR-001 | `MEM-34` |
| `019-clue-unlock-feedback-and-recap` | FR-002 | `MEM-35` |
| `019-clue-unlock-feedback-and-recap` | FR-003 | `MEM-36` |
| `019-clue-unlock-feedback-and-recap` | FR-004 | `MEM-37` |
| `019-clue-unlock-feedback-and-recap` | FR-005 | 옛 스펙에 없는 번호 |
| `019-clue-unlock-feedback-and-recap` | FR-006 | 옛 스펙에 없는 번호 |
| `019-clue-unlock-feedback-and-recap` | FR-007 | `MEM-38` |
| `019-clue-unlock-feedback-and-recap` | FR-008 | `MEM-39` |
| `019-clue-unlock-feedback-and-recap` | FR-009 | `MEM-40` |
| `019-clue-unlock-feedback-and-recap` | FR-010 | `MEM-41` |
| `019-clue-unlock-feedback-and-recap` | FR-011 | `MEM-42` |
| `019-clue-unlock-feedback-and-recap` | FR-012 | `MEM-43` |
| `019-clue-unlock-feedback-and-recap` | FR-013 | `MEM-44` |
| `019-clue-unlock-feedback-and-recap` | FR-014 | `MEM-04` (012 FR-004와 합침) |
| `spec-opening-monologue-door-arrival` | FR-001 | `OPEN-01` |
| `spec-opening-monologue-door-arrival` | FR-002 | `OPEN-02` |
| `spec-opening-monologue-door-arrival` | FR-003 | `OPEN-03` |
| `spec-opening-monologue-door-arrival` | FR-004 | `OPEN-04` |
| `spec-opening-monologue-door-arrival` | FR-005 | `OPEN-05` |
| `spec-opening-monologue-door-arrival` | FR-006 | `OPEN-06` |
| `009-field-character-animation` (feature/charactrers-model-add 브랜치) | FR-001 | `FIELD-01` |
| `009-field-character-animation` (feature/charactrers-model-add 브랜치) | FR-002 | `FIELD-02` (010-walk-cycle-and-player FR-003과 합침) |
| `009-field-character-animation` (feature/charactrers-model-add 브랜치) | FR-003 | `FIELD-03` |
| `009-field-character-animation` (feature/charactrers-model-add 브랜치) | FR-004 | `FIELD-04` |
| `009-field-character-animation` (feature/charactrers-model-add 브랜치) | FR-005 | `FIELD-05` |
| `009-field-character-animation` (feature/charactrers-model-add 브랜치) | FR-006 | `FIELD-06` |
| `010-walk-cycle-and-player` (feature/charactrers-model-add 브랜치) | FR-001 | `FIELD-07` |
| `010-walk-cycle-and-player` (feature/charactrers-model-add 브랜치) | FR-002 | `FIELD-08` |
| `010-walk-cycle-and-player` (feature/charactrers-model-add 브랜치) | FR-003 | `FIELD-02` (009-field-character-animation FR-002와 합침) |
| `010-walk-cycle-and-player` (feature/charactrers-model-add 브랜치) | FR-004 | `FIELD-09` |
| `010-walk-cycle-and-player` (feature/charactrers-model-add 브랜치) | FR-005 | `FIELD-10` |
| `011-npc-interaction-outline` (feature/charactrers-model-add 브랜치) | FR-001 | `FIELD-11` |
| `011-npc-interaction-outline` (feature/charactrers-model-add 브랜치) | FR-002 | `FIELD-12` |
