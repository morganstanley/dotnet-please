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
dotnet pack --no-build --configuration Release --output ./packages  DotNetPlease.sln
dotnet tool update $toolScope $toolPath --add-source ./packages MorganStanley.DotNetPlease