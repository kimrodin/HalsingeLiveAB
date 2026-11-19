$ErrorActionPreference = 'Stop'
$project = 'halsingelive-keycloak-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$composeArguments = @('compose', '-p', $project, '-f', (Join-Path $PSScriptRoot 'compose.yaml'))

function Invoke-TestDocker {
    param([string[]] $Arguments)
    $output = & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker failed: $($Arguments -join ' ')" }
    return $output
}

try {
    Invoke-TestDocker -Arguments ($composeArguments + @('up', '-d', '--wait', 'keycloak', 'proxy', 'other'))
    # Warm the actual proxy's DNS cache before moving the same Keycloak container.
    $probe = "import urllib.request; r=urllib.request.urlopen('http://proxy/realms/halsingelive/.well-known/openid-configuration', timeout=10); assert r.status == 200"
    Invoke-TestDocker -Arguments ($composeArguments + @('run', '--rm', '--no-deps', 'client', 'python', '-c', $probe))
    $keycloak = Invoke-TestDocker -Arguments ($composeArguments + @('ps', '-q', 'keycloak'))
    $proxy = Invoke-TestDocker -Arguments ($composeArguments + @('ps', '-q', 'proxy'))
    $started = Invoke-TestDocker -Arguments @('inspect', '--format', '{{.State.StartedAt}}', $proxy)
    $oldAddress = Invoke-TestDocker -Arguments @('inspect', '--format', '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}', $keycloak)
    $newAddress = '172.30.154.10' # Outside the fixture's dynamic address pool.
    if ($oldAddress -eq $newAddress) { throw 'Fixture must begin at a different address.' }
    Invoke-TestDocker -Arguments @('network', 'disconnect', "${project}_default", $keycloak)
    Invoke-TestDocker -Arguments @('network', 'connect', '--ip', $newAddress, '--alias', 'keycloak', "${project}_default", $keycloak)
    # Includes discovery retries, real browser login, refresh and all security checks.
    Invoke-TestDocker -Arguments ($composeArguments + @('run', '--rm', '--no-deps', 'client'))
    $currentStarted = Invoke-TestDocker -Arguments @('inspect', '--format', '{{.State.StartedAt}}', $proxy)
    if ($currentStarted -ne $started) { throw 'Proxy must recover without a restart.' }
    Write-Host 'PASS: Keycloak IP changed; proxy recovered without restart; security checks passed.'
}
finally {
    Invoke-TestDocker -Arguments ($composeArguments + @('down'))
}
