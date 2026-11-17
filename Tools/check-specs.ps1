# 스펙 규칙 검사: docs/specs가 영역 스펙 방식(docs/CONTRIBUTING.md 2절)을 지키는지 확인한다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools/check-specs.ps1 [-CommitMessageFile <파일>]
# 검사: 번호 스펙 파일(NNN-*.md) 금지, 영역 스펙의 README·ROUTER 등록, 2절 요구사항 ID의 접두어 일치와 중복.
# -CommitMessageFile을 주면 커밋 제목의 괄호가 요구사항 ID 형식인지도 본다(.githooks/commit-msg).
# 종료 코드: 0 통과, 1 규칙 위반. 커밋 훅(.githooks)과 GitHub 자동 검사(.github/workflows/spec-check.yml)가 같이 쓴다.
param([string]$CommitMessageFile = '')

$ErrorActionPreference = 'Stop'
# Git 창(UTF-8)에서 안내 글이 깨지지 않게 출력 인코딩을 맞춘다.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot
$specDir = Join-Path $root 'docs/specs'
$metaFiles = @('README.md', 'ROUTER.md', '_TEMPLATE.md')
$errors = New-Object System.Collections.Generic.List[string]

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
$routerPath = Join-Path $specDir 'ROUTER.md'
$readme = if (Test-Path $readmePath) { Read-Utf8 $readmePath } else { $errors.Add('docs/specs/README.md가 없다.'); '' }
$router = if (Test-Path $routerPath) { Read-Utf8 $routerPath } else { $errors.Add('docs/specs/ROUTER.md가 없다.'); '' }

# README 1절 목록 표에서 접두어와 파일을 읽는다.
$registered = @{}
$listSection = [regex]::Match($readme, '(?ms)^## 1\..*?(?=^## )').Value
foreach ($m in [regex]::Matches($listSection, '(?m)^\|\s*`([A-Z]+)`\s*\|\s*\[?`([a-z0-9-]+\.md)`')) {
    $registered[$m.Groups[2].Value] = $m.Groups[1].Value
}

foreach ($file in Get-ChildItem $specDir -Filter '*.md' | Sort-Object Name) {
    $name = $file.Name
    if ($metaFiles -contains $name) { continue }
    if ($name -match '^\d') {
        $errors.Add("번호 스펙 파일은 만들지 않는다: docs/specs/$name. ROUTER.md에서 담당 영역 스펙을 찾아 요구사항을 이어 적는다.")
        continue
    }
    if (-not $registered.ContainsKey($name)) {
        $errors.Add("docs/specs/$name 이(가) docs/specs/README.md 1절 목록에 없다. 새 영역이면 접두어와 함께 등록한다.")
        continue
    }
    if ($router -notmatch [regex]::Escape($name)) {
        $errors.Add("docs/specs/$name 이(가) docs/specs/ROUTER.md에 없다.")
    }

    # 2절 요구사항 줄의 ID만 모아 접두어와 중복을 본다. 폐기 줄(~~ID~~)도 번호를 차지한다.
    $prefix = $registered[$name]
    $section = [regex]::Match((Read-Utf8 $file.FullName), '(?ms)^## 2\..*?(?=^## )').Value
    $seen = @{}
    foreach ($m in [regex]::Matches($section, '(?m)^\s*-\s*(?:~~)?\*{0,2}(?:~~)?([A-Z]+)-(\d{2,3})\b')) {
        $id = "$($m.Groups[1].Value)-$($m.Groups[2].Value)"
        if ($m.Groups[1].Value -ne $prefix) {
            $errors.Add("docs/specs/$name 의 $id 는 이 파일의 접두어 $prefix 와 다르다.")
        }
        if ($seen.ContainsKey($id)) {
            $errors.Add("docs/specs/$name 에서 요구사항 $id 가 두 번 정의됐다. 새 요구사항은 마지막 번호 다음을 쓴다.")
        }
        $seen[$id] = $true
    }
}

foreach ($file in $registered.Keys) {
    if (-not (Test-Path (Join-Path $specDir $file))) {
        $errors.Add("docs/specs/README.md 목록의 $file 이(가) 실제로 없다.")
    }
}

if ($errors.Count -gt 0) {
    Write-Output "[스펙 규칙 위반 $($errors.Count)건] 규칙은 docs/CONTRIBUTING.md 1·2절을 본다."
    $errors | ForEach-Object { Write-Output " - $_" }
    exit 1
}
Write-Output '스펙 규칙 검사 통과'
exit 0
