$toolPath = $args[0];
if ($toolPath -eq $null) {
    $toolPath = $env:DotNetToolsPath
}
if ($toolPath -eq $null) {
    $toolPath = ''
}

$ErrorActionPreference = "Stop"

dotnet build --configuration Release DotNetPlease.sln
Remove-Item -Recurse -Path ./packages -ErrorAction SilentlyContinue
dotnet pack --no-build --configuration Release DotNetPlease.sln
$nupkg = Get-ChildItem -Filter packages/*.nupkg | Select-Object -First 1
$packageName = "MorganStanley.DotNetPlease"
$version = $nupkg.Name -replace "$packageName\.(.*?)\.nupkg", '$1'

if ($toolPath) {
    dotnet tool update $packageName --tool-path $toolPath ./packages --version $version
}
else {
    dotnet tool update $packageName --global --add-source ./packages --version $version
}
