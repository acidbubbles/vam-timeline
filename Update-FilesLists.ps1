cd (Split-Path ($MyInvocation.MyCommand.Path))

# Get Atom animation files
$atomAnimationFiles = ( `
    ls ./src/*.cs -Recurse `
    | ? { -not $_.FullName.Contains("Controller\") -and $_.Name -ne "ControllerPlugin.cs" } `
    | % { $_.FullName.Substring((pwd).Path.Length + 1) } `
)

# VamTimeline.AtomAnimation.cslist
$atomAnimationFiles > .\VamTimeline.AtomAnimation.cslist

# VamTimeline.csproj
( Get-Content ".\VamTimeline.csproj" -Raw ) -Replace "(?sm)(?<=^ +<!-- AtomAnimationFiles -->`r?`n).*?(?=`r?`n +<!-- /AtomAnimationFiles -->)", `
    [System.String]::Join("`r`n", ($atomAnimationFiles | % { "    <Compile Include=`"$_`" />" } ) ) | `
    Set-Content ".\VamTimeline.csproj"

# meta.json
$allFiles = (ls ./src/*.cs -Recurse) + (ls *.cslist) `
    | % { $_.FullName.Substring((pwd).Path.Length + 1) }
( Get-Content ".\meta.json" -Raw ) -Replace "(?sm)(?<=^  `"contentList`": \[`r?`n).*?(?=`r?`n  \],)", `
    [System.String]::Join("`r`n", ($allFiles | % { "    `"Custom\\Scripts\\AcidBubbles\\Timeline\\$($_.Replace("\", "\\"))`"," } ) ).Trim(",") `
| Set-Content ".\meta.json"

# tests/VamTimeline.Tests.cslist
$testFiles = ( `
    $atomAnimationFiles `
    | ? { -not $_.EndsWith("\AtomPlugin.cs") } `
    | % { "..\$_" } `
) + ( `
    ls ./tests/*.cs -Recurse `
    |  % { $_.FullName.Substring((pwd).Path.Length + 7) } `
)
$testFiles > .\tests\VamTimeline.Tests.cslist
