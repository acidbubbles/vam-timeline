cd (Split-Path ($MyInvocation.MyCommand.Path))

( `
    ls src/*.cs -Recurse `
    | ? { -not $_.FullName.Contains("Controller\") -and $_.Name -ne "ControllerPlugin.cs" } `
    | % { $_.FullName.Substring((pwd).Path.Length + 1) } `
) > .\VamTimeline.AtomAnimation.cslist