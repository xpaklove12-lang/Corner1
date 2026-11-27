# 문서 라우터

"무엇을 바꾸려는가 → 어느 스펙을 읽고 고칠 것인가"를 찾는 길잡이다. 스펙 목록과 옛 번호 대응은 [`README.md`](README.md), 협업 절차는 [`../CONTRIBUTING.md`](../CONTRIBUTING.md), 새 스펙 양식은 [`_TEMPLATE.md`](_TEMPLATE.md)에 있다.

## 수정 규칙

1. **동작을 바꾸면 그 기능을 이미 다루는 영역 스펙을 고친다. 같은 기능에 대해 새 스펙 파일을 만들지 않는다.** 아래 표에서 담당 스펙을 찾아, 그 스펙의 요구사항·수치 표를 먼저 고치고, 코드를 고친 뒤 "5. 코드 위치"를 갱신한다.
2. 한 변경이 여러 스펙에 걸치면 **걸리는 스펙을 모두** 같은 변경에서 고친다(아래 "여러 스펙에 걸치는 변경").
3. 새 스펙은 이 라우터에서 찾아도 맞는 스펙이 없는 **새 기능 영역**일 때만 만든다. 만들 때는 [`_TEMPLATE.md`](_TEMPLATE.md)를 복사해 번호 없는 이름(소문자 영어, 낱말은 `-`로 잇는다. 예: `field-clue.md`)으로 두고, 새 요구사항 ID 접두어를 정해 이 라우터의 표와 [`README.md`](README.md)의 스펙 목록에 등록한다.
4. 요구사항 ID 범위는 각 스펙의 지금 번호다. 스펙에 요구사항을 더하거나 지우면 이 표의 범위도 함께 고친다.

## 주제 → 스펙

"바꾸고 싶은 것"을 떠올린 말로 찾는다. ID 범위는 그 주제를 주로 다루는 요구사항이다.

| 바꾸고 싶은 것 | 스펙 | 요구사항 |
|---|---|---|
| 새 게임 시작 독백 문장과 줄 넘김 | [`opening.md`](opening.md) | OPEN-01 |
| 독백 뒤 문 위 `띵-동` 호출 연출 | [`opening.md`](opening.md) | OPEN-02 |
| 문 앞 안내 문구와 문 상호작용 | [`opening.md`](opening.md) | OPEN-03 |
| 첫 손님(은재 어머니) 등장과 걸어 들어오는 연출, 대화 시작 시점 | [`opening.md`](opening.md) | OPEN-04~05 |
| 필드 대사창의 `▼` 넘김 표시 | [`opening.md`](opening.md) | OPEN-06 |
| 컷신 빨리감기 키(Ctrl), 빨리감기 가능 구간 | [`dialogue.md`](dialogue.md) | DLG-32~37 |
| 빨리감기 중 화면의 가로줄과 `▶▶` 표시 | [`dialogue.md`](dialogue.md) | DLG-38~39 |
| 대화 중 Space로 눈 들여다보기, 처음·다시 만날 때 안내 문구 | [`dialogue.md`](dialogue.md) | DLG-01~02 |
| 태엽을 언급하던 문구와 버튼(제거된 시스템) | [`dialogue.md`](dialogue.md) | DLG-03~06, 8절 |
| `다른 이야기를 해볼까요` 같은 처음 이야기로 돌아가는 선택지 | [`dialogue.md`](dialogue.md) | DLG-07~13 |
| 대화 중 하단 조작 안내 문구 | [`dialogue.md`](dialogue.md) | DLG-12 |
| 이미 한 좋은 말을 다시 골랐을 때 `이미 한 이야기다.` | [`dialogue.md`](dialogue.md) | DLG-14 |
| 안 좋은 말을 고른 뒤 목록에서 빠지고 번호가 당겨지는 방식 | [`dialogue.md`](dialogue.md) | DLG-15~19 |
| 신뢰를 올려 층 문을 여는 방식, `(신뢰가 깊어졌다.)` 문구 | [`dialogue.md`](dialogue.md) | DLG-20~23 |
| 단서를 모아 고르는 선택지로 층 문 열기, 인물별 문 선택지 표 | [`dialogue.md`](dialogue.md) | DLG-40~44 |
| 대화 기록 창(L 키) 열기·닫기, 기록되는 줄, 말풍선 모양 | [`dialogue.md`](dialogue.md) | DLG-24~31 |
| 눈 깜빡임 주기, 단서 말풍선을 닫을 때 깜빡이지 않기 | [`blink-maze.md`](blink-maze.md) | BLINK-01~02 |
| 단서를 읽고 닫을 때 다른 층 벽이 바뀌는 것, "다른 층의 길이 바뀌었다." | [`blink-maze.md`](blink-maze.md) | BLINK-03~04 |
| 깜빡일 때 미로가 몇 칸, 어느 방향으로 도는가 | [`blink-maze.md`](blink-maze.md) | BLINK-05~10, BLINK-14 |
| 미로가 돌아간 뒤 이동 키 방향 | [`blink-maze.md`](blink-maze.md) | BLINK-08 |
| 눈꺼풀이 닫히고 열리는 모양, 깜빡일 때 화면 어둡기 | [`blink-maze.md`](blink-maze.md) | BLINK-12~13 |
| 깜빡이는 동안 시선이 멈추는 시간 | [`blink-maze.md`](blink-maze.md) | BLINK-15 |
| 눈을 뜨며 층이 차례로 도는 모습(지연, 곡선, 번갈아 돌기) | [`blink-maze.md`](blink-maze.md) | BLINK-17~24 |
| 깜빡임 후보(F5, F6)와 기본 후보, "눈이 깜빡였다." | [`blink-maze.md`](blink-maze.md) | BLINK-11, BLINK-25, BLINK-38 |
| 층이 도는 동안 벽이 옅어지는 정도 | [`blink-maze.md`](blink-maze.md) | BLINK-26~28 |
| 깜빡임 뒤 벽이 바뀌는 파문과 잔상 연출 | [`blink-maze.md`](blink-maze.md) | BLINK-29~37 |
| 회상이나 파편 글이 끝난 뒤 벽이 다시 만들어지는 연출 | [`blink-maze.md`](blink-maze.md) | BLINK-39~41 |
| 부담감이 차는 속도, 차지 않는 때 | [`burden.md`](burden.md) | BURDEN-01 |
| 좋은 말·안 좋은 말에 따른 부담감 증감 | [`burden.md`](burden.md) | BURDEN-02, BURDEN-09 |
| 부담감이 가득 찼을 때(leave 엔딩 기준) | [`burden.md`](burden.md) | BURDEN-03 |
| 자리를 떠났다 돌아올 때 부담감이 남는가 | [`burden.md`](burden.md) | BURDEN-04 |
| 부담감 막대와 `부담감` 글, 막대가 차오르는 속도 | [`burden.md`](burden.md) | BURDEN-05~08 |
| 벽에 부딪혀 밀려나는 거리·시간·쿨다운 | [`burden.md`](burden.md) | BURDEN-10~15 |
| 부딪힐 때 번쩍임과 초점 흔들림 | [`burden.md`](burden.md) | BURDEN-17~18 |
| 잠긴 문을 계속 밀 때 부담감이 오르는 횟수 | [`burden.md`](burden.md) | BURDEN-19~22 |
| 내담자가 쉬자고 할 때의 눈물, 동공 떨림, 흐려짐 | [`burden.md`](burden.md) | BURDEN-23~26 |
| 쉬자는 대사와 안내 문구, Q로 쉬어 주기 | [`burden.md`](burden.md) | BURDEN-27~28, BURDEN-32 |
| 쉬어 주고 돌아왔을 때의 리액션과 부담감 감소 | [`burden.md`](burden.md) | BURDEN-29~31 |
| 눈 속에서 보이는 "하고 싶은 말이 있는 거 같다" 안내 | [`burden.md`](burden.md) | BURDEN-36 |
| 손님의 감정(평온, 경계, 불안, 슬픔, 안도)과 시작 감정 | [`emotion-maze.md`](emotion-maze.md) | EMO-01~02, EMO-10 |
| 감정마다 달라지는 눈 모양(눈꺼풀, 홍채 색, 눈물, 시선 피함) | [`emotion-maze.md`](emotion-maze.md) | EMO-03, EMO-11~12 |
| 감정에 따라 미로에 나타나고 사라지는 사물 | [`emotion-maze.md`](emotion-maze.md) | EMO-04~05, EMO-09, EMO-13~14 |
| 사물이 길을 막거나, 밀어내거나, 길을 내는 방식 | [`emotion-maze.md`](emotion-maze.md) | EMO-06~08, EMO-18, EMO-20 |
| 사물 그림의 위치와 크기, 어지러움 반경 | [`emotion-maze.md`](emotion-maze.md) | EMO-16~17, EMO-19 |
| 미로 속 파편의 맥동과 빛 번짐 | [`memory-fragments.md`](memory-fragments.md) | MEM-01 |
| 파편을 주울 때 고리 연출과 기억 목록 줄 강조 | [`memory-fragments.md`](memory-fragments.md) | MEM-02~05 |
| 파편에 닿으면 들어가는 회상 장면 그림과 회상 방 | [`memory-fragments.md`](memory-fragments.md) | MEM-06~10 |
| 회상 방 안에서 걷기, 사물 살펴보기, 대본 진행 | [`memory-fragments.md`](memory-fragments.md) | MEM-11~16 |
| 회상이 끝난 뒤 감정 변화, 추가 파편, 본 파편 표시 | [`memory-fragments.md`](memory-fragments.md) | MEM-17~20 |
| 회상 중에 멈추는 것(부담감, 깜빡임, R, Q)과 대화 기록 | [`memory-fragments.md`](memory-fragments.md) | MEM-21~24 |
| 새 선택지가 열렸다는 안내와 `관찰 중지` 버튼 맥동 | [`memory-fragments.md`](memory-fragments.md) | MEM-34~37 |
| 기억 목록 줄을 눌러 파편 요약 보기, 회상 다시 재생 | [`memory-fragments.md`](memory-fragments.md) | MEM-38~44 |
| 기억 잇기(실 잇기) | [`memory-fragments.md`](memory-fragments.md) | ~~MEM-25~33~~ 폐기 |
| 대사 편집기 창, 텍스트 뷰와 노드 뷰 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-01~10, EDITOR-15~19 |
| 인물·회차별 대사 CSV 형식과 게임 연결 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-11~14 |
| 엔딩 조건(truth, half, leave)과 열쇠 파편 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-21~25, EDITOR-35, EDITOR-37 |
| 선택지 신뢰·동공 수치 표시와 편집 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-20, EDITOR-26 |
| 편집기에서 노드·대사·엔딩·결과 추가와 삭제 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-28~32, EDITOR-34, EDITOR-36 |
| 선택지 결과(`response`)를 한 줄씩 넘겨 보여 주기 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-33 |
| 선택지를 고른 뒤 엔딩으로 갈지 가르는 `ending_need` | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-38 |
| 대사 CSV의 번역용 줄 ID `tid`, 편집기에서 `Loc/UI.csv` 원문 편집, 모르는 열 보존 | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-39~41 |
| 지원 언어 목록, 시작 언어, 언어 바꾸기 | [`localization.md`](localization.md) | LOC-01~04 |
| 공용 문구 파일(`Loc/UI.csv`, 옛 `Strings.csv`)과 코드 속 화면 문구 | [`localization.md`](localization.md) | LOC-05~07 |
| 씬 고정 라벨과 인스펙터 문구의 번역 | [`localization.md`](localization.md) | LOC-08~09 |
| 문구 속 손님 이름·대명사(`{client}`), 화자 이름 번역 | [`localization.md`](localization.md) | LOC-10~11 |
| 대화·파편 힌트·회상 대사의 번역 파일과 번역 key | [`localization.md`](localization.md) | LOC-12~15 |
| 번역 파일 갱신, 번역 가져오기, 번역 검사 | [`localization.md`](localization.md) | LOC-16~18 |
| 언어별 폰트, 번역 용어집 | [`localization.md`](localization.md) | LOC-19~20 |
| 필드 인물의 전신 모습과 크기 | [`field.md`](field.md) | FIELD-01, FIELD-09 |
| 필드 인물 걷기·앉기·퇴장 모습, 걷기 주기 | [`field.md`](field.md) | FIELD-02~04, FIELD-10 |
| 어머니의 긴장된 입장 자세 | [`field.md`](field.md) | FIELD-05 |
| 소파 착석 높이와 소파 앞부분 가림 | [`field.md`](field.md) | FIELD-06 |
| 걷기 GIF 제작과 프레임 보간 | [`field.md`](field.md) | FIELD-07~08 |
| 말을 걸 수 있는 NPC 테두리 | [`field.md`](field.md) | FIELD-11~12 |

## 코드 파일 → 스펙

각 스펙 "5. 코드 위치"에 나온 파일 기준이다. 경로 앞부분 `Assets/EyeMaze/Scripts/`는 줄였다.

| 코드 | 스펙 |
|---|---|
| `Runtime/EyeMazeDirector.cs` | 기능마다 나뉜다: 깜빡임·벽 재생성은 [`blink-maze.md`](blink-maze.md), 부담감·밀려남·휴식 신호는 [`burden.md`](burden.md), 선택지·문·대화 기록은 [`dialogue.md`](dialogue.md), 감정·사물은 [`emotion-maze.md`](emotion-maze.md), 파편·회상·해금 안내는 [`memory-fragments.md`](memory-fragments.md), CSV 엔딩 판정은 [`dialogue-editor.md`](dialogue-editor.md) |
| `Runtime/EyeMazeUI.cs` | [`blink-maze.md`](blink-maze.md)(BLINK-13, BLINK-16), [`burden.md`](burden.md)(부담감 막대, BURDEN-27, BURDEN-36), [`dialogue.md`](dialogue.md)(공통 선택지, 안내 문구), [`memory-fragments.md`](memory-fragments.md)(기억 목록, 해금 안내) |
| `Runtime/BlinkTurnPreset.cs` | [`blink-maze.md`](blink-maze.md) |
| `Runtime/MazeWall.cs`, `Runtime/MazeWallChangeEffect.cs` | [`blink-maze.md`](blink-maze.md) |
| `Runtime/GazeCursor.cs` | [`blink-maze.md`](blink-maze.md) |
| `Runtime/MazeLayer.cs` | [`blink-maze.md`](blink-maze.md), [`emotion-maze.md`](emotion-maze.md)(사물 묶음의 부모 층) |
| `Runtime/MazeGate.cs` | [`dialogue.md`](dialogue.md)(DLG-21, DLG-40), [`burden.md`](burden.md)(잠긴 문 접촉), [`blink-maze.md`](blink-maze.md)(벽 변화 연출) |
| `Runtime/IrisMaze.cs` | [`blink-maze.md`](blink-maze.md)(벽 생성), [`dialogue.md`](dialogue.md)(층 문 생성), [`emotion-maze.md`](emotion-maze.md)(사물 위치·이동 판정), [`burden.md`](burden.md)(초점 요동), [`memory-fragments.md`](memory-fragments.md)(선 재질) |
| `Runtime/EyeVisual.cs` | [`blink-maze.md`](blink-maze.md)(눈꺼풀, BLINK-12), [`burden.md`](burden.md)(눈물, BURDEN-25), [`emotion-maze.md`](emotion-maze.md)(감정 눈 표현), [`dialogue.md`](dialogue.md)(초점 저장값, DLG-06) |
| `Runtime/Pupil.cs` | [`burden.md`](burden.md)(떨림, BURDEN-24), [`emotion-maze.md`](emotion-maze.md)(감정용 입력) |
| `Runtime/ClientTearCue.cs` | [`burden.md`](burden.md), [`emotion-maze.md`](emotion-maze.md)(EMO-11) |
| `Runtime/ClientEmotion.cs`, `Runtime/EmotionLook.cs`, `Runtime/MazeProp.cs` | [`emotion-maze.md`](emotion-maze.md) |
| `Runtime/ClientCase.cs` | [`emotion-maze.md`](emotion-maze.md)(시작 감정, 사물 묶음), [`dialogue.md`](dialogue.md)(문 플래그, DLG-41), [`dialogue-editor.md`](dialogue-editor.md)(회차 CSV 연결) |
| `Runtime/DialogueNode.cs` | [`dialogue.md`](dialogue.md), [`emotion-maze.md`](emotion-maze.md)(`emotion` 값) |
| `Runtime/DialogueScript.cs` | [`dialogue-editor.md`](dialogue-editor.md), [`burden.md`](burden.md)(`rest` 칸), [`emotion-maze.md`](emotion-maze.md)(`emotion` 칸) |
| `Runtime/TalkScript.cs` | [`dialogue-editor.md`](dialogue-editor.md), [`burden.md`](burden.md)(`rest` 묶음), [`opening.md`](opening.md) |
| `Runtime/EndingScript.cs` | [`dialogue-editor.md`](dialogue-editor.md) |
| `Runtime/Csv.cs` | [`dialogue-editor.md`](dialogue-editor.md), CSV를 읽는 각 스펙 |
| `Runtime/DialogueLog.cs`, `Runtime/DialogueLogUI.cs` | [`dialogue.md`](dialogue.md)(DLG-24~31), [`memory-fragments.md`](memory-fragments.md)(회상 줄 기록, MEM-22) |
| `Runtime/CutsceneFastForward.cs`, `Runtime/FastForwardOverlay.cs` | [`dialogue.md`](dialogue.md)(DLG-32~39) |
| `Runtime/OpeningMonologue.cs` | [`opening.md`](opening.md), [`dialogue.md`](dialogue.md)(빨리감기) |
| `Runtime/FieldCutscene.cs` | [`opening.md`](opening.md), [`dialogue.md`](dialogue.md)(빨리감기) |
| `Runtime/FieldDoor.cs` | [`opening.md`](opening.md) |
| `Runtime/FieldNPC.cs` | [`opening.md`](opening.md), [`dialogue-editor.md`](dialogue-editor.md)(actor별 필드 대사), [`dialogue.md`](dialogue.md)(대화 기록 초상화), [`burden.md`](burden.md)(쉬어 준 뒤 리액션), [`field.md`](field.md)(인물 표시와 테두리, feature/charactrers-model-add 브랜치) |
| `Runtime/GameFlow.cs` | [`opening.md`](opening.md), [`dialogue.md`](dialogue.md), [`burden.md`](burden.md)(Q로 쉬어 주기), [`memory-fragments.md`](memory-fragments.md)(회상 중 입력 막기) |
| `Runtime/GameFlowUI.cs` | [`opening.md`](opening.md)(문 앞 안내, `▼`), [`dialogue.md`](dialogue.md)(빨리감기 안내) |
| `Runtime/Typewriter.cs` | [`dialogue.md`](dialogue.md), [`memory-fragments.md`](memory-fragments.md) |
| `Runtime/Flags.cs` | [`dialogue.md`](dialogue.md)(문 플래그), [`memory-fragments.md`](memory-fragments.md)(파편 플래그) |
| `Runtime/Strings.cs` | [`opening.md`](opening.md)(`Strings.csv` 문구), [`dialogue-editor.md`](dialogue-editor.md)(`Strings.csv` 편집), [`localization.md`](localization.md)(언어별 조회, LOC-05~06) |
| `Runtime/GameSettings.cs`, `Runtime/LocConfig.cs`, `Runtime/Loc.cs`, `Editor/LocTools.cs`, `Runtime/LocalizedLabel.cs` | [`localization.md`](localization.md) |
| `Assets/EyeMaze/Data/Loc/` | [`localization.md`](localization.md) |
| `Runtime/Shapes.cs` | [`blink-maze.md`](blink-maze.md), [`memory-fragments.md`](memory-fragments.md) |
| `Runtime/MemoryFragment.cs`, `Runtime/FragmentFeedback.cs` | [`memory-fragments.md`](memory-fragments.md), [`emotion-maze.md`](emotion-maze.md)(사물 투명도 계산 공유) |
| `Runtime/FlashbackPlayer.cs`, `Runtime/FlashbackRoom.cs`, `Runtime/FlashbackProp.cs`, `Runtime/FlashbackScript.cs` | [`memory-fragments.md`](memory-fragments.md)(MEM-06~24) |
| `Runtime/FieldPlayer.cs`, `Runtime/FieldRoom.cs` | [`memory-fragments.md`](memory-fragments.md)(회상 방 안 이동), `FieldPlayer.cs`는 [`field.md`](field.md)(주인공 외형과 걷기, FIELD-02·09·10)도 함께 |
| `Runtime/SofaForeground.cs`, `Assets/EyeMaze/Art/SofaForeground.shader` | [`field.md`](field.md)(FIELD-06, feature/charactrers-model-add 브랜치) |
| `Assets/EyeMaze/Art/NpcInteractionOutline.shader`, `Assets/EyeMaze/Art/NpcInteractionOutline.mat` | [`field.md`](field.md)(FIELD-11~12, feature/charactrers-model-add 브랜치) |
| `Assets/EyeMaze/Arts/FieldCharacters/` | [`field.md`](field.md)(FIELD-01, FIELD-05, FIELD-07~08, feature/charactrers-model-add 브랜치) |
| `Runtime/FragmentRecapUI.cs` | [`memory-fragments.md`](memory-fragments.md)(MEM-38~44) |
| `Runtime/MemoryLinkScript.cs`, `Runtime/MemoryThread.cs` | [`memory-fragments.md`](memory-fragments.md)(폐기된 기억 잇기, MEM-25~33) |
| `Editor/DialogueEditorWindow.cs`, `Editor/DialogueEditorDocument.cs`, `Editor/DialogueEditorFormat.cs`, `Editor/DialogueEditorGraph.cs`, `Editor/DialogueData.cs` | [`dialogue-editor.md`](dialogue-editor.md), 열 목록은 [`emotion-maze.md`](emotion-maze.md)(EMO-15)도 함께 |
| `Editor/EyeInspectors.cs` | [`blink-maze.md`](blink-maze.md), [`burden.md`](burden.md)(BURDEN-06), [`dialogue.md`](dialogue.md)(DLG-05, DLG-13) |
| `Editor/FieldInspectors.cs` | [`dialogue.md`](dialogue.md) |
| `Assets/EyeMaze/Data/Dialogue/<인물>_DayN.csv` | [`dialogue-editor.md`](dialogue-editor.md)(형식), 칸 의미는 [`dialogue.md`](dialogue.md)·[`burden.md`](burden.md)·[`emotion-maze.md`](emotion-maze.md) |
| `Assets/EyeMaze/Data/Flashbacks.csv` | [`memory-fragments.md`](memory-fragments.md) |

## 요구사항 ID 접두어 → 스펙

| 접두어 | 스펙 | 지금 범위 |
|---|---|---|
| `BLINK` | [`blink-maze.md`](blink-maze.md) | BLINK-01~41 |
| `BURDEN` | [`burden.md`](burden.md) | BURDEN-01~36 |
| `DLG` | [`dialogue.md`](dialogue.md) | DLG-01~44 |
| `EMO` | [`emotion-maze.md`](emotion-maze.md) | EMO-01~20 |
| `MEM` | [`memory-fragments.md`](memory-fragments.md) | MEM-01~44 (MEM-25~33 폐기) |
| `EDITOR` | [`dialogue-editor.md`](dialogue-editor.md) | EDITOR-01~41 |
| `OPEN` | [`opening.md`](opening.md) | OPEN-01~06 |
| `FIELD` | [`field.md`](field.md) | FIELD-01~12 |
| `LOC` | [`localization.md`](localization.md) | LOC-01~20 |

옛 번호(`015`, `스펙 010 FR-017` 등)로 적힌 커밋이나 코드 주석은 [`README.md`](README.md) 2절 대응표로 새 ID를 찾는다.

## 여러 스펙에 걸치는 변경

| 변경 예 | 고칠 스펙 |
|---|---|
| 대사 CSV에 새 열 추가 | `dialogue-editor`(머리글 목록, 편집기 열 목록) + 그 열을 쓰는 동작 영역(예: `emotion` 열은 `emotion-maze`, `rest` 열은 `burden`) |
| 새 감정 추가 | `emotion-maze`(`ClientEmotion`, 눈 표현 값) + `dialogue-editor`(`emotion` 열 값) + 회상 뒤 감정을 쓰면 `memory-fragments`(MEM-18) |
| 새 감정 사물 종류 추가 | `emotion-maze` + 밀려남을 쓰면 `burden`(BURDEN-12~18) + 깜빡임 회전과 겹치면 `blink-maze` |
| 깜빡임 중이나 회상 중 멈추는 것 바꾸기 | `blink-maze`(BLINK-15, BLINK-21, BLINK-40) + `burden`(BURDEN-01, BURDEN-20) + `memory-fragments`(MEM-21, MEM-24) |
| 층 문을 여는 조건 바꾸기 | `dialogue`(DLG-20~23, DLG-40~44) + `memory-fragments`(파편 플래그, MEM-34 해금 안내) + 잠긴 문 접촉이 바뀌면 `burden`(BURDEN-19~22) |
| 새 회상 추가 | `memory-fragments`(`Flashbacks.csv`, 회상 방) + 감정이 바뀌면 `emotion-maze` + 문 선택지를 열면 `dialogue`(DLG-42, DLG-44) + 편집기 파편 노드가 필요하면 `dialogue-editor`(EDITOR-27, EDITOR-34) |
| 눈 위에 겹쳐 그리는 표현 바꾸기(눈물, 동공 떨림, 눈꺼풀) | `burden`(휴식 신호) + `emotion-maze`(EMO-03, EMO-11) + `blink-maze`(BLINK-12) |
| 눈 속 안내 문구 줄 추가 | 문구를 내는 영역 + `burden`(BURDEN-36)과 `memory-fragments`(MEM-35)의 줄 순서 |
| 시간을 멈추는 새 창 추가 | 그 창의 영역 + `dialogue`(DLG-29)와 `memory-fragments`(MEM-41)의 입력 막기 규칙 |
| 컷신 구간 추가 | `opening` 또는 해당 영역 + `dialogue`(빨리감기 허용 구간, DLG-32) |
| 엔딩 조건 바꾸기 | `dialogue-editor`(EDITOR-21~25, EDITOR-38) + `burden`(BURDEN-03 leave 기준) |
| 화면에 새 문구 추가 | 문구를 내는 영역 + `localization`(`Loc/UI.csv` key, LOC-05~07) |
| 대사 CSV나 `Flashbacks.csv`에 새 글 칸 추가 | `dialogue-editor`(머리글) + `localization`(번역 대상 칸, LOC-13) + 그 칸을 쓰는 동작 영역 |
