# TapStrum Launcher (No Console Window)
# Uses dotnet run to handle MSIX deployment automatically

Set-Location $PSScriptRoot
Start-Process dotnet -ArgumentList "run --project TapStrum.Fluent.csproj" -WindowStyle Hidden
