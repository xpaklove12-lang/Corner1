# 현지화

## 1. 목적

플레이어가 한국어 외의 언어로 게임 전체를 읽을 수 있게 한다. 제작자는 지금처럼 한국어 원문을 쓰던 곳에서 계속 쓰고, 번역은 언어별 글만 모은 별도 CSV에 한 줄에 `ko en ja`로 나란히 둔다. 번역가는 이 파일을 구글 시트로 받아 문맥을 보며 번역 칸만 채우고, 제작자는 그 결과를 도구로 가져와 검사한다.

### 범위

- 포함:
  - 언어 목록과 현재 언어 결정·저장·전환, 언어별 폰트
  - UI 문구(지금의 `Strings.csv`, 코드 속 화면 문구, 씬 고정 라벨, 인스펙터 문구)의 번역 파일
  - 대화 문구(인물·회차 CSV, `Flags.csv`의 힌트 글, `Flashbacks.csv`)의 줄 ID(`tid`)와 번역 파일
  - 손님 이름·대명사와 화자 이름의 번역 표시
  - 번역 파일 갱신, 번역 가져오기, 번역 검사 도구
  - 번역 참고용 용어집
- 제외:
  - 중국어(간체·번체). 언어 목록에 열을 더하는 구조만 준비하고, 할지와 어느 쪽을 할지는 9절 미결 사항으로 둔다.
  - 플레이어용 언어 설정 화면. 게임에 설정 화면이 없으므로 9절 미결 사항으로 둔다.
  - 디버그 로그, `Header`·`Tooltip` 같은 에디터 전용 글, 대사 편집기 창 자체의 글
  - 그림 안에 그려진 글자(텍스처) 교체
  - 음성
- 구현 순서: 요구사항이 많으므로 다음 묶음 단위로 승인받는다.
  1. UI 기반: LOC-01, LOC-02, LOC-03, LOC-04, LOC-05, LOC-06, EDITOR-40(`dialogue-editor.md`)
  2. UI 문구 정리: LOC-07, LOC-08, LOC-10, LOC-11, LOC-19, EDITOR-41(`dialogue-editor.md`)
  3. 대화: LOC-12, LOC-13, LOC-14, LOC-15, EDITOR-39(`dialogue-editor.md`)
  4. 도구와 검사: LOC-16, LOC-17, LOC-18, LOC-20
  5. 인스펙터 문구: LOC-09 (9절 미결 사항을 정한 뒤)

### 선택하지 않은 방식

- Unity Localization 패키지(`com.unity.localization`)는 쓰지 않는다. 한국어 원문이 인물·회차 CSV와 대사 편집기에 있어서, 패키지의 String Table로 옮기면 편집기·검사기·CSV 자동 리로드를 다시 만들어야 하고 Addressables 빌드가 더해진다.
- 로직 CSV에 `text_en`처럼 언어 열을 더하지 않는다. 번역가에게 분기·수치 열이 보이고 언어가 늘수록 로직 파일이 넓어진다.
- 언어마다 로직 CSV를 통째로 복사하지 않는다(`Eunjae_Day1_en.csv`). 분기·플래그·수치를 고칠 때 모든 언어 파일을 같이 고쳐야 한다.
- 언어마다 번역 파일을 따로 두지 않는다(`Loc/en/…`, `Loc/ja/…`). 한 줄에 원문과 모든 번역을 나란히 보는 편이 비교와 누락 확인이 쉽다. 언어가 다섯 개를 넘어 파일이 지나치게 넓어지면 다시 정한다.
- 한국어 대화 원문을 번역 파일로 옮기지 않는다. 대사 편집기에서 흐름을 읽을 수 없게 된다.
- 문장 속 `은재`·`그녀` 글자를 찾아 바꾸는 방식은 버린다. 다른 언어에서는 바꿀 글자가 없고, 한국어에서도 이름이 바뀌면 조사가 틀어진다.

### 예외 및 경계 조건

- 언어 코드는 ISO 639-1을 쓴다: `ko`, `en`, `ja`. 중국어를 한다면 `zh-Hans`(간체), `zh-Hant`(번체)를 쓴다.
- `ko`는 원문 언어다. `ko`를 고를 때는 번역 파일을 거치지 않고 원문을 그대로 쓴다.
- 번역 칸이 비어 있으면 그 줄만 `ko` 원문으로 보인다. 게임은 멈추지 않는다.
- 원문이 빈 칸(예: 첫 줄이 아닌 행의 `situation`)은 번역 대상이 아니며 번역 파일에 줄을 만들지 않는다.
- 원문의 `\n` 줄바꿈 표기는 번역에서도 같은 뜻이다. `response`처럼 한 줄씩 넘기는 칸은 번역의 줄 수가 원문과 달라도 된다.
- 같은 key를 여러 줄 쓰는 UI 문구(예: `opening`)는 원문과 번역이 같은 줄에 있으므로 줄마다 번역한다. 번역이 빈 줄은 그 줄만 `ko`로 보인다.
- 번역에 `O` 표시가 없어도(`<언어>_ok`가 빈 칸) 번역 글이 있으면 게임은 그 글을 쓴다. `_ok`는 사람이 다시 확인할 줄을 찾는 표시다.

## 2. 요구사항

### 언어와 설정

- **LOC-01** `[승인]` 언어 설정 에셋(`LocConfig`)에 언어 목록을 둔다. 한 항목은 언어 코드, 언어 이름(그 언어로 쓴 이름, 예: `English`, `日本語`), 그 언어의 폰트다. 처음 목록은 `ko`, `en`, `ja`이며 `ko`가 첫 항목이자 원문이다. 언어를 더할 때는 이 목록에 항목을 넣고 번역 파일에 `<코드>`, `<코드>_ok` 열을 더하는 것으로 끝나야 한다.
- **LOC-02** `[승인]` 게임을 시작하면 다음 순서로 현재 언어를 정한다.
  1. 저장된 언어 코드(PlayerPrefs `_languagePrefKey`)가 언어 목록에 있으면 그 언어
  2. 없으면 운영체제 언어(`Application.systemLanguage`)를 4절 "운영체제 언어 대응" 표로 바꾼 코드가 언어 목록에 있으면 그 언어
  3. 둘 다 아니면 `LocConfig`의 대체 언어(`_fallbackLanguage`)
  - 2·3으로 정한 언어는 저장하지 않는다. 플레이어가 언어를 직접 바꿀 때(LOC-04, 이후 설정 화면)만 그 코드를 저장한다. 그래서 저장값이 없는 동안에는 운영체제 언어를 바꾸면 다음 실행부터 따라간다.
  - `_detectSystemLanguage`를 끄면 2를 건너뛴다(개발 중 항상 한국어로 시작하고 싶을 때).
- **LOC-03** `[승인]` 현재 언어를 바꾸면 화면에 떠 있는 고정 라벨(LOC-08)은 곧바로 새 언어로 바뀐다. 대사·상황·선택지·안내 문구는 다음에 새로 나오는 글부터 새 언어로 나온다. 이미 대화 기록 창(`DLG-24`~`DLG-31`, `dialogue.md`)에 쌓인 줄은 바꾸지 않는다.
- **LOC-04** `[승인]` 메뉴 `EyeMaze/번역/언어 바꾸기`는 언어 바꾸기 창을 연다. 창은 현재 언어, 저장된 언어, 운영체제 언어와 그 대응 코드를 보여 주고, 언어 목록의 언어마다 버튼을 둔다. 버튼을 누르면 그 언어를 저장하고 현재 언어로 삼는다. Play Mode가 아니면 에디터의 현재 언어도 바뀌어 편집 중 미리보기(대사 편집기 등)에 쓰이고, Play를 시작하면 정적 값이 초기화된 뒤 저장값으로 다시 정해 그 언어로 시작한다. `저장된 언어 지우기` 버튼은 저장값을 지우고 LOC-02 규칙으로 언어를 다시 정한다. 개발 확인용이며 플레이어용 설정 화면은 범위 밖이다.

### UI 문구

- **LOC-05** `[승인]` 공용 문구 파일 `Assets/EyeMaze/Data/Strings.csv`를 `.meta`와 함께 `Assets/EyeMaze/Data/Loc/UI.csv`로 옮겨 GUID를 유지하고, 머리글을 `key,context,maxlen,ko,en,ja,note,en_ok,ja_ok,ko_synced`로 바꾼다. 기존 `text`는 `ko`로, `memo`는 `note`로 옮긴다. `ko` 칸이 UI 문구의 원문이며 제작자가 이 파일에서 직접 고친다. `Assets/EyeMaze/Data/README.md`의 `Strings.csv` 절을 새 파일 형식으로 고친다.
- **LOC-06** `[승인]` `Strings.Get`과 `Strings.Lines`는 현재 언어 칸의 글을 돌려준다. 그 칸이 비어 있으면 `ko` 칸을 쓴다. 기본 글을 넘긴 `Strings.Get(key, 기본 글)`은 key가 표에 없으면 그 기본 글을, 기본 글 없이 부른 `Strings.Get(key)`는 key 이름을 그대로 보이고 같은 key마다 한 번 경고한다. `{name}` 같은 칸 채우기는 고른 글에 한다.
- **LOC-07** `[승인]` 화면에 나오는 글 가운데 코드에만 적힌 것을 `Loc/UI.csv`의 key로 옮긴다.
  - `Strings.Get("key", "한글 기본 글")`처럼 코드에 적힌 기본 글은 지운다. 같은 key가 이미 `Loc/UI.csv`에 있으므로 `Strings.Get("key")`로 부른다.
  - 코드에 직접 적힌 화면 문구(부담 증감 안내, 문의 행동 이름 `맞이하기`)는 새 key(`msg.burden_down`, `msg.burden_up`, `verb.greet`)로 옮긴다.
  - 인스펙터 기본값을 쓰던 디렉터 문구(`_burdenEndLine`, `_returnMessage`, `_firstGuide`, `_againGuide`, `_restReturnMessage`, `_returnText`)는 다른 공용 문구처럼 key(`msg.burden_end`, `msg.return`, `msg.first_guide`, `msg.again_guide`, `msg.rest_return`, `ui.return_choice`)를 먼저 읽고, key가 없을 때만 인스펙터 값을 쓴다.
  - 코드가 글을 이어 붙여 만드는 문장은 칸을 가진 문장 하나의 key로 바꿔 어순을 언어별 글이 정하게 한다: 선택지 번호와 글은 `ui.choice`(`{n}. {text}`), 필드 상호작용 안내는 `hint.interact`(`Space  {verb} — {target}`).
  - 디버그 로그, 검사 오류 문구, `Header`, `Tooltip`, 오브젝트 이름은 옮기지 않는다. 다른 key가 있을 때만 쓰이는 인스펙터 기본값(`MazeGate._lockedMessage`, `MazeLayer._displayName`, `FieldCutscene`의 혼잣말·안내, `CutsceneFastForward._hintText`, `OpeningMonologue`의 기본 독백)은 코드에 남는다.
- **LOC-08** `[승인]` 코드가 글을 넣지 않는 고정 라벨에는 `LocalizedLabel` 컴포넌트를 붙인다. `LocalizedLabel`은 `[SerializeField]`로 같은 오브젝트의 `Text`와 `Loc/UI.csv` key를 참조하고, 켜질 때와 언어가 바뀔 때 그 key의 글을 넣는다. 씬에 적힌 글은 편집용 미리보기로만 남는다. 붙이는 라벨은 다음 여섯 개다. 나머지 `Text`는 코드가 매번 글을 넣으므로 붙이지 않는다.

  | 라벨 | 위치 | key |
  |---|---|---|
  | `부담감` | 씬 `부담감 글` | `label.burden` |
  | `재생` | 씬 기억 다시 보기 창 버튼 | `label.recap_play` |
  | `닫기` | 씬 기억 다시 보기 창 버튼 | `label.recap_close` |
  | `띵-동` | 씬 `호출 글씨 (띵-동)` | `label.bell` |
  | `대화 기록` | 프리팹 `DialogueLogCanvas` 제목 | `label.log_title` |
  | `L / Esc  닫기 · W S / ↑ ↓  스크롤` | 프리팹 `DialogueLogCanvas` 안내 | `label.log_hint` |
- **LOC-09** `[작성중]` 씬 인스펙터에 적힌 화면 문구를 번역할 수 있게 한다. 방식은 9절 미결 사항에서 정한다. 정하기 전까지 인스펙터 문구는 언어와 관계없이 원문으로 나온다. 실제로 화면에 쓰이는 인스펙터 문구는 다음과 같다(씬 기준 개수).

  | 컴포넌트 | 필드 | 개수 | 비고 |
  |---|---|---|---|
  | `FlashbackProp` | `_displayName`, `_actionText`, `_lookText`, `_lookTextAfter` | 51, 51, 48, 10 | 회상 방 사물. 번역 대상의 대부분 |
  | `FieldNPC` | `_lineAfterTalk`, `_lineAfterResolved` | 3, 3 | CSV에 맞는 대화가 없을 때의 재대화 |
  | `ClientCase` | `_entryLine` | 1 | 앉은 뒤 첫마디 |
  | `MemoryFragment` | `_title`, `_text` | 11, 11 | `Flags.csv`에 title·text가 없을 때만 쓰임 |

  다음은 화면에 쓰이지 않으므로 번역 대상이 아니다: `DialogueNode`(옛 방식 대화 오브젝트), `ClientCase`의 `_endings`·`_introMessage`·`_idleSituation`·`_abyssMessage`와 `EyeMazeDirector`의 같은 이름 필드(회차 CSV의 엔딩·`@intro`·`@idle`·`@abyss`가 있으면 쓰이지 않음), `FieldRoom._roomName`, `CoreFragment._title`, `FieldNPC._lineBefore`(코드에서 읽지 않음). 인물·사물 이름 `_displayName`(`Person`, `FieldNPC`, `FieldDoor`, `MazeLayer`)은 LOC-11의 `name.` key와 `layer.` key로 번역한다.
- **LOC-10** `[승인]` `EyeMazeDirector.Personalize`의 `은재`·`그녀` 글자 바꾸기를 없앤다. 대신 문구에 `{client}`(손님 이름)와 `{client_pron}`(손님 3인칭)을 쓰고, 게임이 지금 손님 값으로 채운다. 손님 이름은 `name.<손님 PersonName>`(LOC-11과 같은 key), 3인칭은 `pron.<손님 PersonName>` key의 현재 언어 글이며, key가 없으면 `ClientCase`의 `_personName`·`_pronoun`을 쓴다.
  - 조사: 칸 이름 뒤에 `|받침 있을 때/받침 없을 때`를 적으면(`{client|이/가}`, `{name|은/는}`) 채운 값의 끝 글자 받침에 맞는 조사를 붙인다. 뒤쪽이 `로`이면 ㄹ 받침에도 뒤쪽을 쓴다(`{client|으로/로}`). 끝 글자가 숫자면 한국어 읽기로 판단하고, 그 밖의 글자는 받침이 없는 것으로 본다. 모든 `Strings.Get` 칸 채우기에 같은 규칙을 쓴다.
  - `Loc/UI.csv`에서 손님을 가리키던 `은재`·`그녀`를 칸으로 바꾼다(`gate.first`, `gate.pupil`, `wind.first`, `wind.slip`, `wind.empty`, `ui.abyss`). `{name}가`·`{name}는`도 조사 표기로 바꾼다(`ui.rest_hint_gaze`, `msg.already`). 은재 미로 전용 문구(`prop.eunjae.*`)와 인물·회차 CSV의 글은 그 손님의 것이므로 바꾸지 않는다.
- **LOC-11** `[승인]` 대사창·대화 기록·회상·필드 상호작용 안내·눈 속 상황 칸에 보이는 이름은 `name.<원래 이름>` key의 현재 언어 글로 보인다(`Strings.Name`). key가 없으면 원래 이름을 그대로 보인다. 원래 이름은 대화 CSV `speaker` 칸이나 `Flashbacks.csv` `speaker` 칸 값, 없으면 그 인물·대상의 `DisplayName`이다. 빈 이름, `-`(서술), `???`는 바꾸지 않는다. 이름은 화면에 그릴 때만 바꾸고, 대화 기록에 남기는 값·초상화 찾기(`FieldNPC.PortraitOf`)·actor 판정은 원래 이름을 그대로 쓴다. `Loc/UI.csv`에 지금 인물 이름(`name.은재`, `name.은재 어머니`, `name.지수`, `name.문`, 회상 화자 다섯)과 3인칭(`pron.은재`, `pron.지수`) 줄을 둔다.

### 대화 문구

- **LOC-12** `[작성중]` 인물·회차 CSV와 `Flashbacks.csv`에 `tid` 열을 둔다. `tid`는 줄 하나의 고정 ID이고, 한 번 정하면 줄을 옮기거나 글을 고쳐도 바꾸지 않는다. 인물·회차 CSV의 `tid`는 `<파일 이름 소문자>_<네 자리 번호>`(예: `jisu_day1_0005`), `Flashbacks.csv`의 `tid`는 `fb_<네 자리 번호>`다. 빈 `tid`는 대사 편집기 저장(EDITOR-39)이나 번역 파일 갱신(LOC-16) 때 채우며, 번호는 그 원본 파일과 그 번역 파일에 있는 가장 큰 번호 다음부터 쓴다. 같은 파일 안에서 `tid`가 겹치면 뒤쪽 줄에 새 번호를 주고 경고한다.
- **LOC-13** `[작성중]` 번역 key는 `<tid>.<칸 이름>`이다(예: `jisu_day1_0005.response`). 번역 대상 칸은 인물·회차 CSV의 `situation`, `text`, `response`, `back`, `rest`, `title`, `after`, `Flashbacks.csv`의 `text`다. `Flags.csv`는 `id`를 그대로 써서 `flag.<id>.title`, `flag.<id>.text`를 key로 한다. `speaker` 칸은 번역 대상이 아니다(LOC-11).
- **LOC-14** `[작성중]` 대화 번역 파일은 `Assets/EyeMaze/Data/Loc/Dialogue/<원본 파일 이름>.csv`다(예: `Loc/Dialogue/Jisu_Day1.csv`, `Loc/Dialogue/Flags.csv`, `Loc/Dialogue/Flashbacks.csv`). 머리글은 `key,speaker,context,maxlen,ko,en,ja,note,en_ok,ja_ok`다. 줄은 원본의 줄 순서(대화는 대본 순서)를 따른다. `speaker`, `context`, `ko`는 번역 파일 갱신이 원본에서 채우는 칸이고, 사람은 언어 칸·`note`·`maxlen`·`_ok`만 고친다.
- **LOC-15** `[작성중]` 현재 언어가 `ko`가 아니면, 대화 문구를 읽는 곳(`DialogueScript`, `TalkScript`, `EndingScript`, `Flags`, `FlashbackScript`)은 원문 칸 대신 같은 key의 현재 언어 글을 쓴다. 그 글이 없거나 비어 있으면 원문을 쓴다. `tid`가 비어 있는 줄은 원문을 쓴다. `tid`가 전역에서 겹치지 않으므로 `LocConfig`에 등록한 대화 번역 파일을 모두 한 표로 합쳐 찾는다.

### 도구와 검사

- **LOC-16** `[작성중]` 메뉴 `EyeMaze/번역/번역 파일 갱신`을 누르면 다음을 한다.
  - 인물·회차 CSV, `Flags.csv`, `Flashbacks.csv`의 빈 `tid`를 LOC-12 규칙으로 채운다.
  - 원본마다 대화 번역 파일을 만들거나 고친다. 원본의 번역 대상 칸마다 한 줄을 두고, `speaker`, `context`, `ko`를 원본 값으로 채운다. `context`는 `<section>/<actor>/<id>`와, `response`면 그 선택지 글, Talk·Ending·회상 대사면 바로 앞 대사를 적는다.
  - 원본에 새로 생긴 key는 줄을 더하고, 원본에서 사라진 key의 줄은 지운다. 지운 줄 수를 보고한다.
  - `ko`가 바뀐 줄은 모든 `<언어>_ok`를 비운다. `Loc/UI.csv`는 `ko`와 `ko_synced`가 다른 줄의 `_ok`를 비우고 `ko_synced`에 지금 `ko`를 적는다.
  - 언어 칸, `note`, `maxlen`은 건드리지 않는다. 내용이 같으면 파일을 다시 쓰지 않는다.
- **LOC-17** `[작성중]` 메뉴 `EyeMaze/번역/가져오기`로 구글 시트에서 받은 CSV(`Loc/UI.csv`나 대화 번역 파일과 같은 머리글)를 고르면, `key`가 같은 줄의 언어 칸과 `note`만 해당 번역 파일에 반영한다. `ko`, `speaker`, `context`, `ko_synced`는 읽지 않는다. 가져온 언어 칸이 비어 있지 않고 받은 파일의 `ko`가 지금 `ko`와 같으면 그 언어의 `_ok`에 `O`를 적는다. `ko`가 다르면 글은 반영하되 `_ok`는 비워 두고 경고한다. 번역 파일에 없는 key는 반영하지 않고 목록으로 보고한다. 반영 전에 LOC-18의 오류 검사를 하고, 오류가 있는 줄은 반영하지 않는다.
- **LOC-18** `[작성중]` 메뉴 `EyeMaze/기획 데이터 검사`가 번역도 검사한다.
  - 오류: 번역 파일 안의 같은 key 중복, 원문과 번역의 `{…}` 칸 이름 집합이 다름, 언어 칸 이름이 `LocConfig` 목록에 없음.
  - 경고: 언어별 빈 번역 개수, 글은 있는데 `_ok`가 빈 줄(다시 확인할 줄) 개수, `maxlen`보다 긴 번역, 원본에 없는 key(번역 파일 갱신이 필요), 그 언어 폰트에 없는 글자(LOC-19). 폰트 글자 검사는 동적 폰트의 `Font.HasCharacter`에 기대지 않고, 에디터에서 폰트 파일(TTF)의 cmap 표를 직접 읽어 그 글자가 있는지 본다.
- **LOC-19** `[승인]` 현재 언어의 `LocConfig` 폰트가 지정돼 있으면, 화면의 uGUI `Text`는 그 폰트로 글을 그린다. 폰트가 비어 있으면 씬에 지정된 폰트(지금 `Galmuri11`)를 쓴다. 폰트를 바꿔도 글자 크기·정렬·위치는 씬 값을 유지한다. `GameSettings`가 `Start`와 언어가 바뀔 때 씬의 모든 `Text`(꺼진 것 포함)에 입힌다. `Galmuri11`은 한글 11,172자, 히라가나·가타카나 전부, 한자 6,477자(JIS 1·2수준 6,355자 이상)를 가지므로 일본어도 기본 폰트로 그릴 수 있다. 중국어를 하면 한자가 모자랄 수 있어 언어 폰트를 따로 지정한다.
- **LOC-20** `[작성중]` 번역 참고용 용어집 `Assets/EyeMaze/Data/Loc/Glossary.csv`를 둔다. 머리글은 `ko,en,ja,note`이고 인물 이름, 고유 용어(파편, 심연, 동공 등)의 정해진 번역을 적는다. 게임과 검사기는 이 파일을 읽지 않는다.

## 3. 상태와 전이

| 지금 상태 | 계기 | 다음 상태 | 관련 요구사항 |
|---|---|---|---|
| 언어 미정 | 게임 시작, 저장값이 목록에 있음 | 저장된 언어 | LOC-02 |
| 언어 미정 | 게임 시작, 저장값 없음, 운영체제 언어가 목록에 있음 | 운영체제 언어 (저장 안 함) | LOC-02 |
| 언어 미정 | 게임 시작, 저장값 없음, 운영체제 언어가 목록에 없음 | 대체 언어 (저장 안 함) | LOC-02 |
| 현재 언어 A | 언어 바꾸기(LOC-04 메뉴) | 현재 언어 B, 저장 | LOC-02, LOC-03, LOC-04 |
| 번역 줄 확인됨(`_ok=O`) | 원문 `ko`가 바뀌고 번역 파일 갱신 | 확인 필요(`_ok` 빈 칸) | LOC-16 |
| 번역 줄 확인 필요 | 같은 원문으로 번역 가져오기 | 확인됨 | LOC-17 |

```mermaid
flowchart TD
    A[게임 시작] --> B{저장된 언어가 목록에 있나 - LOC-02}
    B -- 예 --> C[저장된 언어]
    B -- 아니오 --> N{운영체제 언어가 목록에 있나 - LOC-02}
    N -- 예 --> O[운영체제 언어, 저장 안 함]
    N -- 아니오 --> D[대체 언어, 저장 안 함]
    C --> E[글을 보여 줄 때]
    O --> E
    D --> E
    E --> F{현재 언어가 ko인가}
    F -- 예 --> G[원문 - LOC-06, LOC-15]
    F -- 아니오 --> H{번역 칸에 글이 있나}
    H -- 예 --> I[번역 글 - LOC-06, LOC-15]
    H -- 아니오 --> G
    I --> J[칸 채우기 client, name 등 - LOC-10]
    G --> J
    J --> K[현재 언어 폰트로 표시 - LOC-19]
    K --> L{언어 바꾸기 - LOC-04}
    L -- 예 --> M[저장, 고정 라벨 즉시 갱신 - LOC-03, LOC-08]
    M --> E

    subgraph 제작 흐름
        P[작가가 원문 CSV 수정] --> Q[번역 파일 갱신 - LOC-12, LOC-16]
        Q --> R[번역 파일을 구글 시트로 공유]
        R --> S[번역가가 언어 칸 채움]
        S --> T[번역 가져오기 - LOC-17]
        T --> U[기획 데이터 검사 - LOC-18]
    end
```

## 4. 수치 표

기본값은 코드의 필드 초기값이다. 씬이나 프리팹에 저장된 값은 이와 다를 수 있으며, Play에서는 저장된 값이 쓰인다. 모두 새로 만들 값이다.

| 이름 | 기본값 | 필드(컴포넌트) | 뜻 |
|---|---|---|---|
| 언어 목록 | `ko`, `en`, `ja` | `_languages` (`LocConfig`) | 언어 코드, 언어 이름, 폰트(Font, 비면 씬 폰트) |
| 운영체제 언어 감지 | 켬 | `_detectSystemLanguage` (`LocConfig`) | 저장값이 없을 때 운영체제 언어를 볼지 (bool) |
| 대체 언어 | `en` | `_fallbackLanguage` (`LocConfig`) | 저장값도 없고 운영체제 언어도 목록에 없을 때 쓰는 언어 코드. 감지를 끄면 저장값이 없을 때 바로 이 언어 |
| 언어 저장 이름 | `EyeMaze.Language` | `_languagePrefKey` (`LocConfig`) | 현재 언어 코드를 저장하는 PlayerPrefs 이름 |
| 언어 설정 | `Data/Loc/LocConfig.asset` | `_locConfig` (`GameSettings`) | 게임이 쓰는 언어 설정 에셋 |
| UI 번역 파일 | `Data/Loc/UI.csv` | `_stringsCsv` (`EyeMazeDirector`) | UI 문구 원문과 번역 (TextAsset). 옛 `Strings.csv` 참조를 GUID 그대로 쓴다 |
| 대화 번역 파일 | `Data/Loc/Dialogue/*.csv` | `_dialogueTables` (`LocConfig`) | 대화 번역 파일 목록 (TextAsset 배열). LOC-15를 구현할 때 더한다 |
| 라벨 key | 빈 값 | `_key` (`LocalizedLabel`) | `Loc/UI.csv`의 key |
| 라벨 글 상자 | 같은 오브젝트에서 연결 | `_label` (`LocalizedLabel`) | 글을 넣을 uGUI `Text` |
| tid 번호 자릿수 | 4 | 상수 | `jisu_day1_0005`의 번호 자릿수 |

### 운영체제 언어 대응

| `SystemLanguage` | 언어 코드 |
|---|---|
| `Korean` | `ko` |
| `English` | `en` |
| `Japanese` | `ja` |
| `ChineseSimplified`, `Chinese` | `zh-Hans` (목록에 없으면 대체 언어) |
| `ChineseTraditional` | `zh-Hant` (목록에 없으면 대체 언어) |
| 그 외 | 대체 언어 |

### 파일 형식

`Loc/UI.csv`

```
key,context,maxlen,ko,en,ja,note,en_ok,ja_ok,ko_synced
gate.first,신뢰 1 문에 막혔을 때,60,단단히 닫힌 문이다. {client}가 조금 더 마음을 열어야 한다.,The door is shut tight. {client} needs to open up a little more.,,{client}=손님 이름,O,,단단히 닫힌 문이다. {client}가 조금 더 마음을 열어야 한다.
name.EunjaeMom,대사창 이름,8,은재 어머니,Eunjae's Mother,,,O,,은재 어머니
```

`Loc/Dialogue/Jisu_Day1.csv`

```
key,speaker,context,maxlen,ko,en,ja,note,en_ok,ja_ok
jisu_day1_0005.situation,-,dialogue/Jisu/smile,,지수가 웃으며 말한다. …,Jisu smiles as she speaks. …,,,O,
jisu_day1_0005.text,상담사,dialogue/Jisu/smile,40,피곤하면 푹 쉬셔야죠. 오늘은 일찍 들어가세요.,,,,,
jisu_day1_0005.response,Jisu,dialogue/Jisu/smile · 선택지: 피곤하면 푹 쉬셔야죠. …,,"""네, 그럴게요!"" 대답이 너무 빨랐다.",,,,,
```

- `_ok` 칸은 `O` 또는 빈 칸이다.
- 파일은 지금 CSV와 같이 UTF-8로 저장한다(`Assets/EyeMaze/Data/README.md`).

## 5. 코드 위치

> LOC-01~LOC-06은 구현했다. 나머지는 고칠 예정인 곳이며, 구현 후 메서드 이름까지 갱신한다.

| 요구사항 | 파일 | 메서드·속성 |
|---|---|---|
| LOC-01 | `Assets/EyeMaze/Scripts/Runtime/LocConfig.cs`, `Assets/EyeMaze/Data/Loc/LocConfig.asset` | `LocConfig.Languages`, `LocConfig.Find`, `LocConfig.Has`, `LocConfig.OnValidate` |
| LOC-02 | `Assets/EyeMaze/Scripts/Runtime/Loc.cs`, `Assets/EyeMaze/Scripts/Runtime/GameSettings.cs`, `Assets/Scenes/EyeMaze.unity`(`게임 설정 (GameSettings)` 오브젝트) | `Loc.Init`, `Loc.StartLanguage`, `Loc.FromSystemLanguage`, `Loc.SetLanguage`, `GameSettings.Awake`, `GameSettings.Language`, `GameSettings.SetLanguage` |
| LOC-03 | `Assets/EyeMaze/Scripts/Runtime/Loc.cs`, `Assets/EyeMaze/Scripts/Runtime/Strings.cs` | `Loc.LanguageChanged`, `Strings.EnsureResolved`(언어가 바뀐 뒤 첫 조회 때 그 언어로 다시 고름). 고정 라벨 즉시 갱신은 LOC-08에서 |
| LOC-04 | `Assets/EyeMaze/Scripts/Editor/LocTools.cs` | `LocTools.Open`, `LocTools.OnGUI`, `Loc.ClearSavedLanguage` |
| LOC-05 | `Assets/EyeMaze/Data/Loc/UI.csv`, `Assets/EyeMaze/Data/README.md`, `Assets/EyeMaze/Scripts/Runtime/Strings.cs` | `Strings.DefaultPath`, `Strings.SOURCE_COLUMN` |
| LOC-06 | `Assets/EyeMaze/Scripts/Runtime/Strings.cs` | `Strings.Load`, `Strings.EnsureResolved`, `Strings.Pick`, `Strings.Get`, `Strings.Lines` |
| LOC-16, LOC-17 | 새 메뉴를 `Assets/EyeMaze/Scripts/Editor/LocTools.cs`에 더함 | 확인하지 못함 (구현 전) |
| LOC-07 | `Assets/EyeMaze/Scripts/Runtime/Strings.cs`, `EyeMazeDirector.cs`, `EyeMazeUI.cs`, `FieldNPC.cs`, `FieldClue.cs`, `FieldDoor.cs`, `GameFlowUI.cs`, `Assets/EyeMaze/Data/Loc/UI.csv` | `Strings.Get(key, vars)`, `EyeMazeDirector.EndByBurden`·`ReturnText`과 부담 증감 안내, `EyeMazeUI.Update`(선택지·돌아가기 라벨 `ui.choice`), `GameFlowUI.Update`(`hint.interact`), `FieldDoor.Verb` |
| LOC-08 | `Assets/EyeMaze/Scripts/Runtime/LocalizedLabel.cs`, `Assets/Scenes/EyeMaze.unity`, `Assets/EyeMaze/Prefabs/DialogueLogCanvas.prefab` | `LocalizedLabel.OnEnable`, `LocalizedLabel.Refresh` |
| LOC-10 | `Assets/EyeMaze/Scripts/Runtime/EyeMazeDirector.cs`, `Assets/EyeMaze/Scripts/Runtime/Strings.cs`, `Assets/EyeMaze/Data/Loc/UI.csv` | `EyeMazeDirector.Personalize`, `CLIENT_VAR`, `CLIENT_PRONOUN_VAR`, `Strings.Fill`, `Strings.Particle`, `Strings.PRONOUN_PREFIX` |
| LOC-11 | `Assets/EyeMaze/Scripts/Runtime/Strings.cs`, `GameFlow.cs`, `GameFlowUI.cs`, `DialogueLogUI.cs`, `FlashbackPlayer.cs`, `EyeMazeDirector.cs`, `EyeMazeUI.cs` | `Strings.Name`, `Strings.NAME_PREFIX`, `GameFlow.ShowConvLine`(대사창 이름), `DialogueLogUI`(기록 이름), `FlashbackPlayer`(회상 화자) |
| LOC-12, LOC-13, LOC-15 | `Assets/EyeMaze/Scripts/Runtime/DialogueScript.cs`, `TalkScript.cs`, `EndingScript.cs`, `Flags.cs`, `FlashbackScript.cs`, `Csv.cs` | 원문 칸을 읽는 곳(`r["text"]` 등) |
| LOC-14, LOC-20 | 새 `Assets/EyeMaze/Data/Loc/Dialogue/*.csv`, 새 `Assets/EyeMaze/Data/Loc/Glossary.csv` | 해당 없음 |
| LOC-18 | `Assets/EyeMaze/Scripts/Editor/DialogueData.cs` | `EyeMaze/기획 데이터 검사` 메뉴 |
| LOC-19 | `Assets/EyeMaze/Scripts/Runtime/GameSettings.cs` | `GameSettings.Start`, `GameSettings.ApplyFonts` |

### 구조

| 구분 | 이름 | 역할 |
|------|------|------|
| 컴포넌트 | GameSettings | 게임 전역 설정 매니저. 씬에 하나 두고 `[DefaultExecutionOrder(-1000)]`로 다른 스크립트보다 먼저 `Loc.Init`을 부른다. 현재 언어 코드는 `GameSettings.Language`로 읽는다. 언어 폰트를 화면 글에 입힌다(LOC-19) |
| ScriptableObject | LocConfig | 언어 목록, 운영체제 언어 감지, 대체 언어, 저장 이름, (LOC-15에서) 대화 번역 파일 참조 |
| 정적 클래스 | Loc | 현재 언어 보관·저장, 번역 표 읽기, key로 글 찾기, 언어 변경 알림 |
| 컴포넌트 | LocalizedLabel | 고정 라벨의 `Text`에 key의 현재 언어 글을 넣음 |
| 에디터 창 | LocTools | 언어 바꾸기 창. 번역 파일 갱신, 번역 가져오기 메뉴도 이 파일에 더한다 |

- 오브젝트 구성: 씬 루트의 `게임 설정 (GameSettings)` 오브젝트가 `[SerializeField] _locConfig`로 `LocConfig`를 참조하고, `Awake`에서 `Loc.Init`에 넘긴다. 현재 언어 값은 정적 `Loc`에 두므로 다른 스크립트는 오브젝트를 찾지 않고 `GameSettings.Language`(`Loc.Current`)로 읽는다. 값이 정적이라 씬이 바뀌어도 유지되므로 `DontDestroyOnLoad`를 쓰지 않는다. 씬이 늘면 씬마다 `GameSettings`를 하나씩 두며, 같은 설정이면 `Loc.Init`은 언어를 다시 정하지 않는다.
- 실행 순서 의존성: `GameSettings`는 `[DefaultExecutionOrder(-1000)]`이라 다른 스크립트의 `Awake`보다 먼저 시작 언어를 정한다. 에디터에서는 `Loc.Ensure`가 `Strings.Ensure`처럼 기본 경로(`Loc.DefaultConfigPath`)의 설정을 직접 읽는다.
- 언어 팩 준비: `Strings`는 파일을 읽은 뒤, 처음 조회할 때 모든 key를 현재 언어로 골라 `key → 글` 표를 한 번 만든다. 이후 `Strings.Get`은 이 표에서 한 번 찾기만 한다. 파일을 다시 읽거나 현재 언어가 바뀌면 다음 조회 때 다시 만든다. 매 프레임 부르는 화면(`EyeMazeUI.Update`)도 줄마다 언어 칸을 다시 찾지 않는다. LOC-15의 대화 번역 표도 같은 방식으로 한 곳(`Loc`)에서 만든다.
- 기존 코드와의 접점: `Strings`는 `Loc`의 UI 표를 쓰도록 바뀐다. 대화를 읽는 스크립트는 원문 칸을 읽은 뒤 `Loc`에 `<tid>.<칸>`을 물어 번역이 있으면 바꾼다.

## 6. 다른 영역과의 접점

- **부르는 것**: `Strings`(공용 문구), `Csv`(CSV 읽기), `DialogueData`(기획 데이터 검사), `FieldNPC.PortraitOf`(초상화)
- **불리는 것**: 화면에 글을 내는 모든 영역이 `Strings`와 대화 스크립트를 거쳐 현재 언어 글을 받는다.
- **다른 스펙의 데이터**: 인물·회차 CSV 형식과 `tid` 열 보존은 `EDITOR-39`, 공용 문구 파일 편집은 `EDITOR-40`(`dialogue-editor.md`)을 따른다. 대화 기록 창은 `DLG-24`~`DLG-31`(`dialogue.md`), 회상 대본은 `MEM-11`~`MEM-16`(`memory-fragments.md`), 시작 독백 문구는 `OPEN-01`(`opening.md`)이다.

## 7. 검증 방법

- **LOC-01, LOC-02**: `LocConfig`에 `ko`, `en`, `ja`를 두고 대체 언어를 `en`으로 한다. 저장값을 지운 뒤 한국어 Windows에서 Play하면 한국어로, Windows 표시 언어를 일본어로 바꾸면 일본어로, 프랑스어로 바꾸면 영어로 시작한다. 이때 PlayerPrefs에 언어가 저장되지 않는다. LOC-04 메뉴로 `English`를 고르고 Play를 다시 시작하면 운영체제 언어와 관계없이 영어로 시작한다. PlayerPrefs 값을 목록에 없는 코드로 바꾸면 운영체제 언어 규칙으로 정한다. `_detectSystemLanguage`를 끄고 저장값을 지우면 대체 언어로 시작한다.
- **LOC-03, LOC-04**: 상담 중 언어를 바꾸면 고정 라벨은 곧바로, 다음 선택지·상황 문구는 새 언어로 나오고 대화 기록 창의 이전 줄은 그대로다.
- **LOC-05, LOC-06**: `Loc/UI.csv`의 `gate.first`에 `en` 글을 넣고 영어로 잠긴 문에 부딪히면 영어 글이 나온다. `en` 칸을 비우면 한국어가, `ko` 줄까지 지우면 코드 기본 글이 나온다.
- **LOC-07**: 영어로 오프닝부터 은재·지수 상담 끝까지 진행할 때, `Loc/UI.csv`의 모든 key에 `en`이 있다면 화면에 한글이 남지 않는다(인스펙터 문구 LOC-09 제외).
- **LOC-07**(선택지 번호): `ui.choice`의 `en`에 `{n}) {text}`를 넣고 영어로 상담하면 선택지와 돌아가기 줄이 `1) …` 모양으로 나온다. 필드에서 문 앞에 서면 `hint.interact` 글로 안내가 나온다.
- **LOC-08**: 표의 여섯 라벨이 Play를 시작하면 `Loc/UI.csv` 글로 나오고, 언어 바꾸기 창에서 언어를 바꿀 때마다 곧바로 바뀐다. `label.burden` 줄을 지우면 라벨에 `label.burden`이 보이고 Console에 경고가 한 번 나온다.
- **LOC-10**: 지수 상담에서 신뢰 1 문에 막히면 "단단히 닫힌 문이다. 지수가 조금 더 마음을 열어야 한다."가, 은재 상담에서는 "은재가 …"가 나온다. `name.지수`의 `ko`를 받침 있는 이름(예: `지연`)으로 바꾸면 "지연이 …"로 조사가 바뀐다. 심연 상황 칸은 `pron.<이름>` 값으로 "그녀의 마음 속 심연"이 된다.
- **LOC-11**: `name.은재 어머니`의 `en`에 `Eunjae's Mother`를 넣고 영어로 바꾸면, 어머니 대사창 이름과 대화 기록 이름, 문 앞 안내가 영어로 나오고 초상화는 그대로 나온다. 첫 줄의 `???`는 그대로다.
- **LOC-12, LOC-16**: 지수 Day1 CSV의 `tid`를 비우고 번역 파일 갱신을 누르면 `jisu_day1_0001`부터 채워지고, `Loc/Dialogue/Jisu_Day1.csv`가 대본 순서로 생긴다. 원문 한 줄을 고치고 다시 누르면 그 줄의 `_ok`만 비워지고 번역 글은 남는다. 원문 줄을 지우고 누르면 그 줄이 번역 파일에서 지워지고 지운 수가 보고된다.
- **LOC-13, LOC-14, LOC-15**: 번역 파일의 `jisu_day1_0005.response`에 `en` 글을 넣고 영어로 그 선택지를 고르면 영어 반응이 나온다. 한 줄씩 넘기는 `\n`도 영어 글 기준으로 나뉜다. `Flags.csv`의 파편 힌트, 회상 대사도 같은 방법으로 확인한다.
- **LOC-17**: 번역 파일을 구글 시트에 올려 `en` 칸을 채우고 CSV로 받아 가져오면 해당 줄의 `en`과 `en_ok=O`가 반영된다. 받은 뒤 원문을 고쳐 두고 가져오면 글은 들어가고 `en_ok`는 빈 칸이며 경고가 나온다. `ko` 칸을 시트에서 고쳐 가져와도 원문은 바뀌지 않는다.
- **LOC-18**: 번역 글에서 `{client}`를 지우면 오류가 나고 가져오기에서 그 줄이 빠진다. `maxlen`을 넘는 번역, 빈 번역, `_ok`가 빈 줄이 경고 수로 나온다.
- **LOC-19**: `ja`에 다른 폰트를 지정하고 일본어로 바꾸면 화면 글이 그 폰트로 그려지고 글 상자 위치·크기는 그대로다. 다시 `ko`로 바꾸면 `Galmuri11`로 돌아온다. `ja` 폰트를 비워 두면 일본어 번역도 `Galmuri11`로 깨지지 않고 그려진다.
- **LOC-20**: 해당 없음 (게임 동작 없음). 파일이 있고 머리글이 맞는지만 본다.

## 8. 알려진 문제

- 대화 기록 창의 이름 칸은 그릴 때 현재 언어로 바꾸므로(LOC-11), 언어를 바꾼 뒤 이전 줄을 다시 열면 이름만 새 언어이고 글은 그 줄을 남길 때의 언어다(LOC-03).
- LOC-19 폰트는 `GameSettings.Start` 때 씬에 있는 `Text`와 언어를 바꿀 때 있는 `Text`에만 입힌다. 지금 씬은 모든 `Text`를 처음부터 갖고 있어 문제가 없지만, 나중에 실행 중 프리팹으로 글 상자를 만들게 되면 그 자리에서 폰트를 입혀야 한다.
- `EyeMazeUI.Update`는 매 프레임 `{n}`·`{name}` 같은 칸을 채우므로 글자열을 새로 만든다. 언어 팩 조회(5절 "언어 팩 준비")는 한 번으로 끝나지만 칸 채우기는 줄이지 않았다.
- 기본 글 없이 부르는 `Strings.Get(key)`는 `Loc/UI.csv`에서 그 줄을 지우면 key 이름이 화면에 보인다(LOC-06). 줄을 지울 때는 코드에서 쓰는지 먼저 찾는다.

## 9. 미결 사항

- 인스펙터 문구(LOC-09)를 어떻게 번역할지 정한다. 제안은 다음 둘이다.
  - 가: 인스펙터 문구를 새 `Loc/Scene.csv`의 key로 옮기고, 인스펙터 칸에는 key를 저장한다. 칸 옆에 `ko` 원문을 보여 주고 그 자리에서 고치면 `Loc/Scene.csv`의 `ko`에 저장하는 인스펙터 표시를 만든다. 한 번 옮기는 도구를 별도 메뉴로 둔다. (추천: 원문과 번역이 UI 문구와 같은 방식으로 한 파일에 모인다)
  - 나: 인스펙터 문구는 원문으로 그대로 두고, 컴포넌트마다 고정 ID 칸을 더해 대화처럼 `tid` 방식으로 번역 파일을 만든다. 오브젝트를 복제하면 ID가 겹쳐 검사로 잡아야 한다.
- 처음 실제로 번역해 내보낼 언어를 정한다. 구조는 `en`, `ja`를 함께 준비하고, LOC-18 경고는 목록의 모든 언어에 대해 낸다.
- 중국어를 할지, 간체(`zh-Hans`)와 번체(`zh-Hant`) 중 무엇을 할지 정한다.
- Steam 등 플랫폼에 낼지 정한다. Steam에 내면 운영체제 언어보다 Steam 클라이언트의 게임 언어 설정(Steamworks `GetCurrentGameLanguage`)을 먼저 보도록 LOC-02 순서에 단계를 더한다.
- 플레이어가 언어를 바꿀 설정 화면을 언제 만들지, 첫 실행 때 언어 선택 창을 띄울지 정한다. 자동 감지(LOC-02)가 틀릴 수 있으므로 출시 전에는 필요하다. 만들면 그 화면의 영역 스펙에 요구사항을 둔다.