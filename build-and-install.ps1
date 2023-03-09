$toolPath = $args[0];
if ($toolPath -eq $null) {
    $toolPath = $env:DotNetToolsPath
}
if ($toolPath -eq $null) {
    $toolPath = ''
}

if ($toolPath -ne '') {
    $toolScope = '--tool-path'
}
else {
    $toolScope = '--global'
}

dotnet build --configuration Release DotNetPlease.sln
Remove-Item -Recurse -Path ./packages
dotnet pack --no-build --configuration Release DotNetPlease.sln
$nupkg = Get-ChildItem -Filter packages/*.nupkg | Select-Object -First 1
$packageName = "MorganStanley.DotNetPlease"
$version = $nupkg.Name.Substring($packageName.Length + 1, $nupkg.Name.Length - $packageName.Length - 1 - ".nupkg".Length)
dotnet tool update $toolScope $toolPath --add-source ./packages MorganStanley.DotNetPlease --version $version
