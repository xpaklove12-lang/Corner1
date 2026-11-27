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
$nextNumbers = @{}

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
# 파일은 docs/specs 기준 경로다. 영역이 늘면 하위 폴더(예: gameplay/blink-maze.md)에 둘 수 있다.
foreach ($m in [regex]::Matches($readme, '(?m)^\|\s*`([A-Z]+)`\s*\|\s*\[?`([a-z0-9/-]+\.md)`')) {
    $prefix = $m.Groups[1].Value
    $file = $m.Groups[2].Value
    if ($prefixOwner.ContainsKey($prefix)) {
        $errors.Add("접두어 $prefix 를 $($prefixOwner[$prefix]) 와 $file 가 함께 쓴다. 새 영역은 겹치지 않는 접두어를 쓴다.")
    }
    $prefixOwner[$prefix] = $file
    $registered[$file] = $prefix
}

foreach ($file in Get-ChildItem $specDir -Filter '*.md' -Recurse | Sort-Object FullName) {
    if ($metaFiles -contains $file.Name) { continue }
    $name = $file.FullName.Substring([IO.Path]::GetFullPath($specDir).Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
    if ($file.Name -match '^\d') {
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

    $nextNumbers[$prefix] = $nextNumber
    $features = [regex]::Match($text, '(?ms)^## 기능[ \t]*\n(.*?)(?=^## |\z)')
    if (-not $features.Success) { $errors.Add("docs/specs/$name 에 '## 기능' 절이 없다.") }
    if ($text -notmatch '(?ms)^## 흐름[ \t]*\n.*?^```mermaid') {
        $errors.Add("docs/specs/$name 에 '## 흐름' 절과 Mermaid 차트가 없다.")
    }
    foreach ($old in @('진행 중', '진행 현황', '완료')) {
        if ($text -match "(?m)^## $old[ \t]*$") { $errors.Add("docs/specs/$name 에 '## $old' 절이 있다. 진행 상태는 docs/tasks의 작업 문서에서 관리하고, 스펙에는 '## 기능'만 둔다.") }
    }

    # 기능은 "### 기능 이름 (ID)" 제목이다. 접두어, 중복, 다음 번호를 본다. 스펙에는 상태 태그를 쓰지 않는다.
    $reqPattern = '(?m)^### (.+?) \(([A-Z]+)-(\d{2,3})\)(.*)$'
    $seen = @{}
    if ($features.Success) {
        foreach ($m in [regex]::Matches($features.Groups[1].Value, $reqPattern)) {
            $id = "$($m.Groups[2].Value)-$($m.Groups[3].Value)"
            if ($m.Groups[2].Value -ne $prefix) { $errors.Add("docs/specs/$name 의 $id 는 이 파일의 접두어 $prefix 와 다르다.") }
            if ($seen.ContainsKey($id)) { $errors.Add("docs/specs/$name 에서 $id 가 두 번 나온다. 브랜치를 합치다 번호가 겹쳤다면 늦게 합친 쪽이 다음 번호로 바꾼다.") }
            if ([int]$m.Groups[3].Value -ge $nextNumber) { $errors.Add("docs/specs/$name 의 $id 가 다음 번호보다 작지 않다. 다음 번호를 올린다.") }
            if ($m.Groups[4].Value -match '\[(제안|개발중|완료)\]') { $errors.Add("docs/specs/$name 의 $id 제목에 상태 태그가 있다. 상태는 작업 문서에만 쓴다.") }
            $seen[$id] = $true
            $doneIds[$id] = $true
        }
    }
    if ($text -match '(?m)^\s*-\s*\*\*[A-Z]+-\d{2,3}\*\*') {
        $errors.Add("docs/specs/$name 에 옛 형식 요구사항 줄(- **ID**)이 있다. ""### 기능 이름 (ID)"" 제목으로 쓴다.")
    }

    # 코드 단서 표: 요구사항마다 한 줄. 이름은 아래에서 코드와 대조한다.
    $clue = [regex]::Match($text, '(?ms)^### 코드 단서[ \t]*\n(.*?)(?=^#{2,3} |\z)')
    if (-not $clue.Success) {
        if ($seen.Count -eq 0) { continue }
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

# 작업 문서(docs/tasks): 진행 상태와 승인은 여기서만 관리한다.
# 파일 이름은 "<첫 ID>-<영어 이름>.md", 첫 줄은 "# [상태] 이름", 대상 줄에 ID를 쓴다.
$taskDir = Join-Path $root 'docs/tasks'
$openOwner = @{}
if (Test-Path $taskDir) {
    $taskNames = @{}
    foreach ($f in Get-ChildItem $taskDir -Filter '*.md' -Recurse | Sort-Object FullName) {
        if (@('README.md', '_TEMPLATE.md') -contains $f.Name) { continue }
        $inDone = $f.Directory.Name -eq 'done'
        $tname = if ($inDone) { "docs/tasks/done/$($f.Name)" } else { "docs/tasks/$($f.Name)" }
        if ($taskNames.ContainsKey($f.Name)) { $errors.Add("$tname 와 $($taskNames[$f.Name]) 의 파일 이름이 같다. 이름 뒤쪽을 이번 변경을 나타내는 말로 바꾼다.") }
        $taskNames[$f.Name] = $tname
        $t = (Read-Utf8 $f.FullName) -replace "`r`n", "`n"
        $fileId = [regex]::Match($f.Name, '^([A-Z]+-\d{2,3})-[a-z0-9-]+\.md$')
        if (-not $fileId.Success) { $errors.Add("$tname 이름은 '<첫 ID>-<소문자 영어 이름>.md'여야 한다(예: LOC-08-dialogue-translation.md)."); continue }
        $head = [regex]::Match($t, '^﻿?# \[(제안|개발중|완료)\] .+')
        if (-not $head.Success) { $errors.Add("$tname 첫 줄은 '# [제안] 이름', '# [개발중] 이름', '# [완료] 이름' 중 하나여야 한다."); continue }
        $state = $head.Groups[1].Value
        if ($inDone -and $state -ne '완료') { $errors.Add("$tname 은 done 폴더에 있는데 상태가 [$state]다. 끝난 작업만 done에 둔다.") }
        if (-not $inDone -and $state -eq '완료') { $errors.Add("$tname 은 [완료]인데 done 폴더에 없다. docs/tasks/done/으로 옮긴다.") }
        $target = [regex]::Match($t, '(?m)^대상: ([^·\n]+)')
        if (-not $target.Success) { $errors.Add("$tname 에 '대상: ID, ID' 줄이 없다."); continue }
        $ids = @([regex]::Matches($target.Groups[1].Value, '[A-Z]+-\d{2,3}') | ForEach-Object { $_.Value })
        if ($ids -notcontains $fileId.Groups[1].Value) { $errors.Add("$tname 파일 이름의 ID $($fileId.Groups[1].Value)가 대상 줄에 없다.") }
        if ($t -notmatch '(?m)^## 무엇을 만드나') { $errors.Add("$tname 에 '## 무엇을 만드나' 절이 없다.") }
        foreach ($id in $ids) {
            $p = $id.Split('-')[0]
            if (-not $nextNumbers.ContainsKey($p)) { $errors.Add("$tname 의 $id 접두어 $p 가 스펙 목록에 없다."); continue }
            if ([int]$id.Split('-')[1] -ge $nextNumbers[$p]) { $errors.Add("$tname 의 $id 가 그 영역 스펙의 다음 번호보다 작지 않다. 영역 스펙의 다음 번호를 올린다.") }
            if ($state -eq '완료') {
                if (-not $doneIds.ContainsKey($id)) { $errors.Add("$tname 은 완료인데 $id 가 영역 스펙의 '기능'에 없다. 기능을 스펙에 옮긴다.") }
            } else {
                if ($openOwner.ContainsKey($id)) { $errors.Add("$id 를 진행 중인 작업 둘($($openOwner[$id]), $tname)이 함께 맡았다. 하나로 합치거나 번호를 나눈다.") }
                $openOwner[$id] = $tname
            }
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

    # 스펙 기능의 코드 단서 이름이 코드에 있는지 본다.
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
