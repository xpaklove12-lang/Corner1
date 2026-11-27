# 스펙 규칙 검사: docs/specs가 docs/CONTRIBUTING.md의 규칙을 지키는지 확인한다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools/check-specs.ps1 [-WriteStatus] [-CommitMessageFile <파일>]
# -WriteStatus를 주면 각 스펙의 "진행 중" 줄을 모아 docs/specs/STATUS.md를 다시 쓴다.
# -CommitMessageFile을 주면 커밋 제목의 괄호가 요구사항 ID 형식인지도 본다(.githooks/commit-msg).
# 종료 코드: 0 통과, 1 규칙 위반. 커밋 훅(.githooks)과 GitHub 자동 검사(.github/workflows/spec-check.yml)가 같이 쓴다.
param([string]$CommitMessageFile = '', [switch]$WriteStatus)

$ErrorActionPreference = 'Stop'
# Git 창(UTF-8)에서 안내 글이 깨지지 않게 출력 인코딩을 맞춘다.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$specDir = Join-Path $root 'docs/specs'
$statusPath = Join-Path $specDir 'STATUS.md'
$metaFiles = @('README.md', '_TEMPLATE.md', 'STATUS.md', 'LEGACY-IDS.md')
$errors = New-Object System.Collections.Generic.List[string]
$board = New-Object System.Collections.Generic.List[object]

function Read-Utf8([string]$Path) {
    return [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
}

# 요구사항 줄의 첫 문장만 남겨 진행판 한 칸에 들어가게 줄인다.
function Get-Summary([string]$Text) {
    $t = $Text.Trim()
    $m = [regex]::Match($t, '^(.*?다\.)')
    if ($m.Success) { $t = $m.Groups[1].Value }
    if ($t.Length -gt 90) {
        $t = $t.Substring(0, 90)
        # 잘린 자리가 코드 표기 안이면 닫아 준다.
        if ((($t.ToCharArray() | Where-Object { $_ -eq '`' }).Count % 2) -eq 1) { $t += '`' }
        $t += '…'
    }
    return $t.Replace('|', '\|')
}

# 커밋 제목 검사: 괄호에는 요구사항 ID(예: BLINK-03,MEM-12)만 쓴다. Merge·Revert 제목은 건너뛴다.
if ($CommitMessageFile) {
    $title = @((Read-Utf8 $CommitMessageFile) -split "`r?`n" | Where-Object { $_ -and $_ -notmatch '^#' })[0]
    if ($title -and $title -notmatch '^(Merge|Revert) ') {
        if ($title -match '^[a-z]+\(([^)]*)\)!?:' -and $Matches[1] -notmatch '^[A-Z]+-\d{2,3}(, ?[A-Z]+-\d{2,3})*$') {
            $errors.Add("커밋 제목 괄호에는 요구사항 ID만 쓴다(예: feat(BLINK-03,MEM-12): 요약). 지금: '$title'")
        }
    }
}

$readmePath = Join-Path $specDir 'README.md'
$readme = if (Test-Path $readmePath) { Read-Utf8 $readmePath } else { $errors.Add('docs/specs/README.md가 없다.'); '' }

# README "영역" 표에서 접두어와 파일을 읽는다. 표 순서가 진행판 순서다.
$registered = [ordered]@{}
foreach ($m in [regex]::Matches($readme, '(?m)^\|\s*`([A-Z]+)`\s*\|\s*\[?`([a-z0-9-]+\.md)`')) {
    $registered[$m.Groups[2].Value] = $m.Groups[1].Value
}

foreach ($file in Get-ChildItem $specDir -Filter '*.md' | Sort-Object Name) {
    $name = $file.Name
    if ($metaFiles -contains $name) { continue }
    if ($name -match '^\d') {
        $errors.Add("번호 스펙 파일은 만들지 않는다: docs/specs/$name. README에서 담당 영역 스펙을 찾아 요구사항을 더한다.")
        continue
    }
    if (-not $registered.Contains($name)) {
        $errors.Add("docs/specs/$name 이(가) docs/specs/README.md 영역 표에 없다. 새 영역이면 접두어와 함께 등록한다.")
    }
}

foreach ($name in $registered.Keys) {
    $path = Join-Path $specDir $name
    if (-not (Test-Path $path)) {
        $errors.Add("docs/specs/README.md 표의 $name 이(가) 실제로 없다.")
        continue
    }
    $prefix = $registered[$name]
    $text = (Read-Utf8 $path) -replace "`r`n", "`n"

    $next = [regex]::Match($text, '다음 번호 `([A-Z]+)-(\d{2,3})`')
    if (-not $next.Success) {
        $errors.Add("docs/specs/$name 맨 위에 '다음 번호 ``$prefix-NN``'이 없다.")
        continue
    }
    if ($next.Groups[1].Value -ne $prefix) {
        $errors.Add("docs/specs/$name 의 다음 번호 접두어가 $prefix 와 다르다.")
    }
    $nextNumber = [int]$next.Groups[2].Value

    $progress = [regex]::Match($text, '(?ms)^## 진행 중[ \t]*\n(.*?)(?=^## |\z)')
    $done = [regex]::Match($text, '(?ms)^## 완료[ \t]*\n(.*?)(?=^## |\z)')
    if (-not $progress.Success) { $errors.Add("docs/specs/$name 에 '## 진행 중' 절이 없다.") }
    if (-not $done.Success) { $errors.Add("docs/specs/$name 에 '## 완료' 절이 없다.") }

    # 파일 전체의 요구사항 줄로 접두어, 중복, 다음 번호를 본다.
    $seen = @{}
    foreach ($m in [regex]::Matches($text, '(?m)^\s*-\s*\*\*([A-Z]+)-(\d{2,3})\*\*')) {
        $id = "$($m.Groups[1].Value)-$($m.Groups[2].Value)"
        if ($m.Groups[1].Value -ne $prefix) {
            $errors.Add("docs/specs/$name 의 $id 는 이 파일의 접두어 $prefix 와 다르다.")
        }
        if ($seen.ContainsKey($id)) {
            $errors.Add("docs/specs/$name 에서 $id 가 두 번 나온다. 새 요구사항은 다음 번호를 쓴다.")
        }
        if ([int]$m.Groups[2].Value -ge $nextNumber) {
            $errors.Add("docs/specs/$name 의 $id 가 다음 번호보다 작지 않다. 다음 번호를 올린다.")
        }
        $seen[$id] = $true
    }

    if ($progress.Success) {
        foreach ($m in [regex]::Matches($progress.Groups[1].Value, '(?m)^-\s*\*\*([A-Z]+-\d{2,3})\*\*(.*)$')) {
            $id = $m.Groups[1].Value
            $tag = [regex]::Match($m.Groups[2].Value, '^\s*`\[(제안|개발중)\]`\s*(.*)$')
            if (-not $tag.Success) {
                $errors.Add("docs/specs/$name 진행 중의 $id 에 상태 태그(``[제안]``, ``[개발중]``)가 없다.")
                continue
            }
            $board.Add([pscustomobject]@{ Id = $id; Status = $tag.Groups[1].Value; File = $name; Text = (Get-Summary $tag.Groups[2].Value) })
        }
    }
    if ($done.Success) {
        foreach ($m in [regex]::Matches($done.Groups[1].Value, '(?m)^-\s*\*\*([A-Z]+-\d{2,3})\*\*\s*`\[')) {
            $errors.Add("docs/specs/$name 완료의 $($m.Groups[1].Value) 에 상태 태그가 남아 있다. 완료 줄에는 태그를 쓰지 않는다.")
        }
    }
}

# 진행판 STATUS.md를 만든다.
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# 진행 상황')
$lines.Add('')
$lines.Add('각 스펙 "진행 중"에서 자동으로 만든다. 손으로 고치지 않고 `Tools/check-specs.ps1 -WriteStatus`로 다시 만든다.')
foreach ($status in @('개발중', '제안')) {
    $lines.Add('')
    $lines.Add("## $status")
    $lines.Add('')
    $rows = @($board | Where-Object { $_.Status -eq $status })
    if ($rows.Count -eq 0) {
        $lines.Add('없음')
        continue
    }
    $lines.Add('| ID | 스펙 | 내용 |')
    $lines.Add('|---|---|---|')
    foreach ($r in $rows) { $lines.Add("| $($r.Id) | [``$($r.File)``]($($r.File)) | $($r.Text) |") }
}
$statusText = ($lines -join "`n") + "`n"

if ($WriteStatus) {
    [IO.File]::WriteAllText($statusPath, $statusText, (New-Object Text.UTF8Encoding($false)))
    Write-Output 'docs/specs/STATUS.md를 다시 만들었다.'
} elseif (-not (Test-Path $statusPath) -or ((Read-Utf8 $statusPath) -replace "`r`n", "`n") -ne $statusText) {
    $errors.Add('docs/specs/STATUS.md가 스펙과 다르다. Tools/check-specs.ps1 -WriteStatus로 다시 만든다.')
}

if ($errors.Count -gt 0) {
    Write-Output "[스펙 규칙 위반 $($errors.Count)건] 규칙은 docs/CONTRIBUTING.md를 본다."
    $errors | ForEach-Object { Write-Output " - $_" }
    exit 1
}
Write-Output '스펙 규칙 검사 통과'
exit 0
