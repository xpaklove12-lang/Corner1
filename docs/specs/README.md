# 스펙 목록

작업 절차와 쓰는 법은 [`../CONTRIBUTING.md`](../CONTRIBUTING.md), 양식은 [`_TEMPLATE.md`](_TEMPLATE.md)에 있다. 영역 스펙에는 그 영역의 역할과 완료된 기능만 있다. 진행 중인 작업과 승인은 [`../tasks/`](../tasks/README.md)에 있다. 화면에 나오는 대사 원문 목록은 [`../story/DialogueInventory.md`](../story/DialogueInventory.md)에 있다.

## 영역

새 영역 파일을 만들거나 이름을 바꿀 때만 이 표를 고친다.

| 접두어 | 파일 | 다루는 것 |
|--------|------|-----------|
| `OPEN` | [`opening.md`](opening.md) | 시작 독백, 초인종, 문 앞 안내, 첫 손님 입장 |
| `FIELD` | [`field.md`](field.md) | 필드 인물 전신, 걷기·앉기·퇴장, NPC 테두리 |
| `DLG` | [`dialogue.md`](dialogue.md) | 들여다보기, 선택지 처리, 처음으로 돌아가기, 신뢰와 층 문, 대화 기록, 컷신 빨리감기 |
| `BLINK` | [`blink-maze.md`](blink-maze.md) | 깜빡임 주기, 깜빡임 회전, 파문 벽 변화, 파편 뒤 벽 재생성 |
| `BURDEN` | [`burden.md`](burden.md) | 부담감 게이지, 벽·잠긴 문 부딪힘, 휴식 신호와 쉬어 주기 |
| `EMO` | [`emotion-maze.md`](emotion-maze.md) | 손님 감정과 눈 표현 |
| `MEM` | [`memory-fragments.md`](memory-fragments.md) | 파편 표시와 획득, 회상 방 재연, 해금 안내, 기억 다시 보기 |
| `EDITOR` | [`dialogue-editor.md`](dialogue-editor.md) | 인물·회차 대사 CSV, 대사 편집기, 엔딩 조건 |
| `LOC` | [`localization.md`](localization.md) | 언어 선택과 폰트, 화면 문구와 대사 번역, 번역 도구 |
| `CUT` | [`intro-cuttoon.md`](intro-cuttoon.md) | 운석 충돌부터 수면 캡슐까지 만화 칸이 차례로 열리는 인트로 컷툰, 그레이 박스 씬 |
| `LAYOUT` | [`maze-layout.md`](maze-layout.md) | 미로 모양 만들기, 기억을 모을수록 새겨지는 상처 무늬, 부딪히거나 의심이 풀리면 드러나는 상처 |
| `MTEST` | [`maze-test.md`](maze-test.md) | 필드와 대화 없이 눈 미로만 바로 플레이하는 테스트 씬, 테스트 조건, 탈출과 다시 하기 |
| `BOOM` | [`maze-boom.md`](maze-boom.md) | 미로 칸이 무늬대로 경고 뒤 터지고, 휩쓸리면 층 입구로 돌아가는 폭발 미로 |

## 여러 스펙에 걸치는 변경

| 변경 | 함께 고칠 스펙 |
|---|---|
| 화면에 새 문구 | 그 문구의 영역 + `localization`(공용 문구 key) |
| 대사 CSV나 `Flashbacks.csv`에 새 열 | `dialogue-editor` + 그 열을 쓰는 영역 + 글 칸이면 `localization`(LOC-08) |
| 새 감정 | `emotion-maze` + `dialogue-editor`(`emotion` 열) |
| 깜빡임·회상 중 멈추는 것 | `blink-maze` + `burden`(BURDEN-01) + `memory-fragments`(MEM-03) |
| 층 문 여는 조건 | `dialogue`(DLG-06, DLG-11, DLG-12) + `memory-fragments`(MEM-08) + `burden`(BURDEN-07) |
| 새 회상 | `memory-fragments` + 감정이 바뀌면 `emotion-maze` + 문을 열면 `dialogue` |
| 눈 위 표현(눈물, 떨림, 눈꺼풀) | `burden` + `emotion-maze`(EMO-02) + `blink-maze`(BLINK-05) |
| 시간을 멈추는 새 창 | 그 영역 + `dialogue`(DLG-08) + `memory-fragments`(MEM-09) |
| 새 컷신 구간 | 그 영역 + `dialogue`(DLG-09 빨리감기) |
| 엔딩 조건 | `dialogue-editor`(EDITOR-10) + `burden`(BURDEN-03) |
