# Windows CI contract for bilingual UI resources.
# Checks both dictionary parity and references used by actual WPF controls.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$app = Join-Path $root 'src/SolarOfThings.App'
$en = Join-Path $app 'Resources/Strings.en.xaml'
$es = Join-Path $app 'Resources/Strings.es.xaml'
$xamlNs = 'http://schemas.microsoft.com/winfx/2006/xaml'

function Read-LocalizedStrings([string] $path) {
    [xml] $xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $entries = @{}
    foreach ($node in $xml.DocumentElement.ChildNodes) {
        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
        $key = $node.GetAttribute('Key', $xamlNs)
        if ([string]::IsNullOrWhiteSpace($key)) { continue }
        if ($entries.ContainsKey($key)) { throw "Duplicate resource key: $key in $path" }
        $entries[$key] = $node.InnerText
    }
    return $entries
}

$english = Read-LocalizedStrings $en
$spanish = Read-LocalizedStrings $es
if ($english.Count -eq 0) { throw 'No English strings were parsed.' }
$errors = [System.Collections.Generic.List[string]]::new()
foreach ($key in $english.Keys) {
    if (-not $spanish.ContainsKey($key)) {
        $errors.Add("Spanish missing: $key")
        continue
    }
    # Compare positional placeholders, including repeated and format-suffixed
    # forms, without requiring the natural-language text to be the same.
    $pattern = '(?<!\{)\{(\d+)(?:\s*:[^}]+)?\}'
    $argsEn = @([regex]::Matches($english[$key], $pattern) |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object) -join ','
    $argsEs = @([regex]::Matches($spanish[$key], $pattern) |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object) -join ','
    if ($argsEn -cne $argsEs) {
        $errors.Add("Format arguments differ: $key")
    }
}
foreach ($key in $spanish.Keys) {
    if (-not $english.ContainsKey($key)) {
        $errors.Add("English missing: $key")
    }
}

$references = 0
Get-ChildItem -Path $app -Recurse -Filter '*.xaml' |
    Where-Object { $_.FullName -notmatch '[\\/]Resources[\\/]Strings\.(?:en|es)\.xaml$' } |
    ForEach-Object {
        $path = $_.FullName
        $source = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        foreach ($match in [regex]::Matches($source,
            '\{DynamicResource\s+([^\s},]+)[^}]*\}')) {
            $references++
            $key = $match.Groups[1].Value
            if (-not $english.ContainsKey($key)) {
                $errors.Add("Missing WPF DynamicResource: $key in $($_.Name)")
            }
        }
    }

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Host "LOCALIZATION PASS en/es=$($english.Count) WPF_dynamic_references=$references"
