$Config = "Debug"
$Root = (Resolve-Path "$PSScriptRoot/..").Path

dotnet build $Root/FastMPH.slnx -c $Config