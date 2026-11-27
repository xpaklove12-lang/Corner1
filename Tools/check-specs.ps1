# 스펙 규칙 검사: docs/specs가 docs/CONTRIBUTING.md의 규칙을 지키는지 확인한다.
# 사람이 직접 돌리지 않는다. 커밋 훅(.githooks)과 GitHub 자동 검사(.github/workflows/spec-check.yml)가 부른다.
# -CommitMessageFile을 주면 커밋 제목의 괄호가 요구사항 ID 형식인지도 본다(.githooks/commit-msg).
# 종료 코드: 0 통과(경고는 통과), 1 규칙 위반.
param([string]$CommitMessageFile = '')

$ErrorActionPreference = 'Stop'
# Git 창(UTF-8)에서 안내 글이 깨지지 않게 출력 인코딩을 맞춘다.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$specDir = Join-Path $root 'docs/specs'
$metaFiles = @('README.md', '_TEMPLATE.md')
$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$clueNames = New-Object System.Collections.Generic.List[object]
$doneIds = @{}

function Read-Utf8([string]$Path) {
    return [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8)
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

# README "영역" 표에서 접두어와 파일을 읽는다. 같은 접두어를 두 영역이 쓰면 오류다.
$registered = [ordered]@{}
$prefixOwner = @{}
foreach ($m in [regex]::Matches($readme, '(?m)^\|\s*`([A-Z]+)`\s*\|\s*\[?`([a-z0-9-]+\.md)`')) {
    $prefix = $m.Groups[1].Value
    $file = $m.Groups[2].Value
    if ($prefixOwner.ContainsKey($prefix)) {
        $errors.Add("접두어 $prefix 를 $($prefixOwner[$prefix]) 와 $file 가 함께 쓴다. 새 영역은 겹치지 않는 접두어를 쓴다.")
    }
    $prefixOwner[$prefix] = $file
    $registered[$file] = $prefix
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
    if ($text -notmatch '(?ms)^## 흐름[ \t]*\n.*?^```mermaid') {
        $errors.Add("docs/specs/$name 에 '## 흐름' 절과 Mermaid 차트가 없다.")
    }

    # 요구사항은 "### 기능 이름 (ID)" 제목이다. 접두어, 중복, 다음 번호를 본다.
    $reqPattern = '(?m)^### (.+?) \(([A-Z]+)-(\d{2,3})\)(.*)$'
    $seen = @{}
    foreach ($m in [regex]::Matches($text, $reqPattern)) {
        $id = "$($m.Groups[2].Value)-$($m.Groups[3].Value)"
        if ($m.Groups[2].Value -ne $prefix) {
            $errors.Add("docs/specs/$name 의 $id 는 이 파일의 접두어 $prefix 와 다르다.")
        }
        if ($seen.ContainsKey($id)) {
            $errors.Add("docs/specs/$name 에서 $id 가 두 번 나온다. 브랜치를 합치다 번호가 겹쳤다면 늦게 합친 쪽이 다음 번호로 바꾼다.")
        }
        if ([int]$m.Groups[3].Value -ge $nextNumber) {
            $errors.Add("docs/specs/$name 의 $id 가 다음 번호보다 작지 않다. 다음 번호를 올린다.")
        }
        $seen[$id] = $true
    }
    if ($text -match '(?m)^\s*-\s*\*\*[A-Z]+-\d{2,3}\*\*') {
        $errors.Add("docs/specs/$name 에 옛 형식 요구사항 줄(- **ID**)이 있다. ""### 기능 이름 (ID)"" 제목으로 쓴다.")
    }

    if ($progress.Success) {
        foreach ($m in [regex]::Matches($progress.Groups[1].Value, $reqPattern)) {
            if ($m.Groups[4].Value -notmatch '^\s*`\[(제안|개발중)\]`\s*$') {
                $errors.Add("docs/specs/$name 진행 중의 $($m.Groups[2].Value)-$($m.Groups[3].Value) 제목 끝에 상태 태그(``[제안]``, ``[개발중]``)가 없다.")
            }
        }
    }
    if ($done.Success) {
        foreach ($m in [regex]::Matches($done.Groups[1].Value, $reqPattern)) {
            $doneIds["$($m.Groups[2].Value)-$($m.Groups[3].Value)"] = $true
            if ($m.Groups[4].Value.Trim()) {
                $errors.Add("docs/specs/$name 완료의 $($m.Groups[2].Value)-$($m.Groups[3].Value) 제목에 상태 태그가 남아 있다.")
            }
        }
    }

    # 코드 단서 표: 요구사항마다 한 줄. 이름은 아래에서 코드와 대조한다.
    $clue = [regex]::Match($text, '(?ms)^### 코드 단서[ \t]*\n(.*?)(?=^#{2,3} |\z)')
    if (-not $clue.Success) {
        $warnings.Add("docs/specs/$name 에 '### 코드 단서' 표가 없다.")
    } else {
        $rows = @{}
        foreach ($r in [regex]::Matches($clue.Groups[1].Value, '(?m)^\|\s*([A-Z]+-\d{2,3})\s*\|(.*)\|\s*$')) {
            $rows[$r.Groups[1].Value] = $true
            foreach ($n in [regex]::Matches($r.Groups[2].Value, '`([^`]+)`')) { $clueNames.Add([pscustomobject]@{ File = $name; Id = $r.Groups[1].Value; Name = $n.Groups[1].Value }) }
        }
        foreach ($id in $seen.Keys) {
            if (-not $rows.ContainsKey($id)) { $warnings.Add("docs/specs/$name 코드 단서 표에 $id 줄이 없다.") }
        }
    }
}

$assetsDir = Join-Path $root 'Assets'
if (Test-Path $assetsDir) {
    $code = (Get-ChildItem $assetsDir -Recurse -Include '*.cs', '*.shader' | ForEach-Object { Read-Utf8 $_.FullName }) -join "`n"

    # 코드에는 요구사항 ID와 스펙 번호를 쓰지 않는다. 스펙을 고칠 때 코드까지 고치지 않게 하기 위해서다.
    $prefixPattern = ($prefixOwner.Keys | Sort-Object) -join '|'
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($f in Get-ChildItem $assetsDir -Recurse -Include '*.cs', '*.shader') {
        $m = [regex]::Match((Read-Utf8 $f.FullName), "(?<![A-Z])(?:$prefixPattern)-\d{2,3}(?!\d)|스펙 ?\d{3}")
        if ($m.Success) { $found.Add("$($f.Name)($($m.Value))") }
    }
    if ($found.Count -gt 0) {
        $errors.Add("코드에 요구사항 ID나 스펙 번호를 쓰지 않는다. 스펙과의 연결은 커밋 제목의 ID로 한다: $($found -join ', ')")
    }

    # 완료 요구사항의 코드 단서 이름이 코드에 있는지 본다. 진행 중인 요구사항은 아직 코드가 없을 수 있어 보지 않는다.
    # 경로(/ 포함)는 Assets/EyeMaze/Data 아래 파일로 본다.
    foreach ($c in ($clueNames | Where-Object { $doneIds.ContainsKey($_.Id) })) {
        $ok = $true
        if ($c.Name -match '/') {
            $ok = Test-Path (Join-Path $root ('Assets/EyeMaze/Data/' + $c.Name))
        } else {
            foreach ($part in $c.Name.Split('.')) {
                if ($code -notmatch ('\b' + [regex]::Escape($part) + '\b')) { $ok = $false }
            }
        }
        if (-not $ok) { $warnings.Add("docs/specs/$($c.File) 코드 단서의 $($c.Id) 이름 '$($c.Name)'이(가) 코드에 없다. 이름이 바뀌었으면 표를 고친다.") }
    }
}

$warnings | ForEach-Object { Write-Output "[경고] $_" }
if ($errors.Count -gt 0) {
    Write-Output "[스펙 규칙 위반 $($errors.Count)건] 규칙은 docs/CONTRIBUTING.md를 본다."
    $errors | ForEach-Object { Write-Output " - $_" }
    exit 1
}
Write-Output '스펙 규칙 검사 통과'
exit 0
