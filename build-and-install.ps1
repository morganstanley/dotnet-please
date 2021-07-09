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

dotnet build -c:Release DotNetPlease.sln
dotnet pack --no-build -c:Release DotNetPlease.sln
dotnet tool update $toolScope $toolPath --add-source DotNetPlease/bin/nupkg MorganStanley.DotNetPlease