# 이 PC에서 저장소를 쓸 준비를 한다. 리포를 받은 뒤, 또는 Unity 버전을 바꾼 뒤 한 번 실행한다. 여러 번 실행해도 된다.
# 1. 커밋 훅(.githooks)을 켠다.
# 2. 씬·프리팹 병합에 쓸 UnityYAMLMerge를 이 프로젝트의 Unity 버전으로 등록한다(.gitattributes의 merge=unityyamlmerge).
# 사용: powershell -ExecutionPolicy Bypass -File Tools/setup-git.ps1

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = Split-Path -Parent $PSScriptRoot

git -C $root config core.hooksPath .githooks
Write-Output '커밋 훅: .githooks'

# ProjectSettings/ProjectVersion.txt의 Unity 버전으로 Hub 설치 경로에서 UnityYAMLMerge를 찾는다.
$versionLine = Get-Content (Join-Path $root 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
$version = ($versionLine -split ':', 2)[1].Trim()
$candidates = @(
    "C:\Program Files\Unity\Hub\Editor\$version\Editor\Data\Tools\UnityYAMLMerge.exe",
    "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/Tools/UnityYAMLMerge",
    "$HOME/Unity/Hub/Editor/$version/Editor/Data/Tools/UnityYAMLMerge"
)
$tool = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $tool) {
    Write-Output "UnityYAMLMerge를 찾지 못했다(Unity $version). 씬·프리팹 병합은 Git 기본 병합을 쓴다. Unity Hub로 $version 을 설치한 뒤 다시 실행한다."
    exit 0
}
$toolPath = $tool.Replace('\', '/')
git -C $root config merge.unityyamlmerge.name 'Unity SmartMerge (UnityYAMLMerge)'
git -C $root config merge.unityyamlmerge.driver "'$toolPath' merge -h -p --fallback none %O %B %A %A"
git -C $root config merge.unityyamlmerge.recursive binary
Write-Output "씬·프리팹 병합: $toolPath"
