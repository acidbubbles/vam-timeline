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
    [System.String]::Join("`r`n", ($atomAnimationFiles | % { "    <Compile Include=`"$_`" />" } ) ) `
| Set-Content ".\VamTimeline.csproj" -NoNewline

# meta.json
$allFiles = (ls ./src/*.cs -Recurse) + (ls *.cslist) `
    | % { $_.FullName.Substring((pwd).Path.Length + 1) }
( Get-Content ".\meta.json" -Raw ) -Replace "(?sm)(?<=^  `"contentList`": \[`r?`n).*?(?=`r?`n  \],)", `
    [System.String]::Join("`r`n", ($allFiles | % { "    `"Custom\\Scripts\\AcidBubbles\\Timeline\\$($_.Replace("\", "\\"))`"," } ) ).Trim(",") `
| Set-Content ".\meta.json" -NoNewline

# tests/VamTimeline.Tests.cslist
$testFiles = ( `
   ls ./tests/*.cs -Recurse `
|  % { $_.FullName.Substring((pwd).Path.Length + 7) } `
)
$testFilesAndDependencies = ( `
    $atomAnimationFiles `
    | ? { -not $_.EndsWith("\AtomPlugin.cs") } `
    | % { "..\$_" } `
) + $testFiles
$testFilesAndDependencies > .\tests\VamTimeline.Tests.cslist

( Get-Content ".\VamTimeline.csproj" -Raw ) -Replace "(?sm)(?<=^ +<!-- TestFiles -->`r?`n).*?(?=`r?`n +<!-- /TestFiles -->)", `
    [System.String]::Join("`r`n", ($testFiles | % { "    <Compile Include=`"tests\$_`" />" } ) ) `
| Set-Content ".\VamTimeline.csproj" -NoNewline