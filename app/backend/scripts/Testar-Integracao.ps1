param([switch]$Visual, [switch]$JornadaF3, [switch]$JornadaF4, [string]$FiltroIntegracao)
$ErrorActionPreference = 'Stop'
$id = [Guid]::NewGuid().ToString('N')
$projeto = "locadora-f1-testes-$id"
$container = "$projeto-sqlserver-1"
$volume = "${projeto}_locadora-sqlserver"
$compose = @('--project-name', $projeto, '--file', (Join-Path $PSScriptRoot '../../../compose.yml'),
    '--file', (Join-Path $PSScriptRoot '../tests/compose.verificacao.yml'))
$senha = 'Aa1!' + [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$anteriores = @{}
$variaveis = @{
    MSSQL_SA_PASSWORD = $senha
    Autenticacao__Jwt__Chaves__v1 = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Autenticacao__Jwt__Chaves__anterior = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Logging__LogLevel__Microsoft = 'Critical'
}
$api = $null
$frontend = $null
function Aguardar-Sql {
    for ($tentativa = 0; $tentativa -lt 60; $tentativa++) {
        $estado = docker inspect --format '{{.State.Health.Status}}' $container 2>$null
        if ($estado -eq 'healthy') { return }
        Start-Sleep -Seconds 2
    }
    throw 'SQL de testes não ficou saudável.'
}
try {
    foreach ($nome in $variaveis.Keys) {
        $anteriores[$nome] = [Environment]::GetEnvironmentVariable($nome)
        [Environment]::SetEnvironmentVariable($nome, $variaveis[$nome])
    }
    docker compose @compose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Compose de testes inválido.' }
    docker compose @compose up --detach --pull never | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível iniciar SQL exclusivo de testes.' }
    Aguardar-Sql
    docker exec $container sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "CREATE DATABASE LocadoraTestePersistencia" > /dev/null 2>&1'
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível preparar a prova de persistência.' }
    docker restart $container | Out-Null
    Aguardar-Sql
    docker exec $container sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "IF DB_ID(N''LocadoraTestePersistencia'') IS NULL THROW 50000, ''Volume não persistiu'', 1" > /dev/null 2>&1'
    if ($LASTEXITCODE -ne 0) { throw 'A prova de persistência após restart falhou.' }
    $porta = (docker port $container 1433/tcp).Split(':')[-1]
    $nomeConexao = 'ConnectionStrings__Locadora'
    $anteriores[$nomeConexao] = [Environment]::GetEnvironmentVariable($nomeConexao)
    [Environment]::SetEnvironmentVariable($nomeConexao, "Server=127.0.0.1,$porta;Database=LocadoraTeste_Base;User Id=sa;Password=$senha;TrustServerCertificate=True;Connect Timeout=5")
    Write-Host 'SQL Server exclusivo pronto; segredos somente no processo. Desenvolvimento não foi alterado.'
    Push-Location (Join-Path $PSScriptRoot '..')
    try {
        $alvo = if ($FiltroIntegracao) { 'tests/Locadora.IntegrationTests' } else { 'Locadora.slnx' }
        $filtro = if ($FiltroIntegracao) { @('--filter', $FiltroIntegracao) } else { @() }
        dotnet test $alvo --configuration Release --no-restore --logger 'console;verbosity=minimal' @filtro
        if ($LASTEXITCODE -ne 0) { throw 'Testes backend falharam.' }
        if ($Visual -or $JornadaF3 -or $JornadaF4) {
            if ($JornadaF3 -or $JornadaF4) {
                foreach ($portaVisual in @(4201, 5001)) {
                    $testePorta = [Net.Sockets.TcpListener]::new([Net.IPAddress]::IPv6Any, $portaVisual)
                    try {
                        $testePorta.Server.DualMode = $true
                        $testePorta.Server.ExclusiveAddressUse = $true
                        $testePorta.Start()
                    } catch {
                        throw "A porta visual $portaVisual já está indisponível. A jornada não usará um servidor preexistente."
                    } finally { $testePorta.Stop() }
                }
            }
            $env:ASPNETCORE_ENVIRONMENT = 'Development'
            $env:Cors__Origens__0 = 'http://localhost:4201'
            $env:Cors__Origens__1 = 'http://127.0.0.1:4201'
            $temporario = Join-Path $env:LOCALAPPDATA 'Temp/opencode'
            $prefixo = if ($JornadaF4) { 'f4' } elseif ($JornadaF3) { 'f3' } else { 'f1' }
            $api = Start-Process dotnet -ArgumentList @('run', '--project', 'src/Locadora.Api', '--configuration', 'Release', '--no-build', '--no-launch-profile', '--urls', 'http://localhost:5001') -PassThru -RedirectStandardOutput (Join-Path $temporario "$prefixo-api.out.log") -RedirectStandardError (Join-Path $temporario "$prefixo-api.err.log")
            $comandoFrontend = 'npm start -- --port 4201 --configuration verificacao-f1'
            if ($JornadaF3 -or $JornadaF4) { $comandoFrontend += ' --prebundle=false --hmr=false --live-reload=false --watch=false' }
            if ($JornadaF4) { $comandoFrontend += ' --host 127.0.0.1 --allowed-hosts localhost' }
            $frontend = Start-Process pwsh -WorkingDirectory (Join-Path $PSScriptRoot '../../frontend') -ArgumentList @('-NoProfile', '-Command', $comandoFrontend) -PassThru -RedirectStandardOutput (Join-Path $temporario "$prefixo-frontend.out.log") -RedirectStandardError (Join-Path $temporario "$prefixo-frontend.err.log")
            @{ frontendPid = $frontend.Id; apiPid = $api.Id; container = $container; volume = $volume } |
                ConvertTo-Json | Set-Content (Join-Path $temporario "$prefixo-visual-processos.json")
            Write-Host "Ambiente visual exclusivo: http://localhost:4201 e API http://localhost:5001. Estado sem segredos em Temp/opencode/$prefixo-visual-processos.json."
            if ($JornadaF3 -or $JornadaF4) {
                $origemProntidao = if ($JornadaF4) { 'http://127.0.0.1:4201/' } else { 'http://localhost:4201/' }
                $urls = @('http://localhost:5001/health', $origemProntidao)
                foreach ($url in $urls) {
                    $pronto = $false
                    for ($i = 0; $i -lt 50; $i++) {
                        if ($api.HasExited -or $frontend.HasExited) { throw 'Processo da jornada encerrou antes de ficar pronto.' }
                        try {
                            $pronto = (Invoke-WebRequest $url -TimeoutSec 2).StatusCode -eq 200
                            if ($pronto) { break }
                        } catch { Start-Sleep -Seconds 1 }
                    }
                    if (!$pronto) { throw "Servidor da jornada não respondeu: $url." }
                }
                if ($JornadaF3) {
                    node (Join-Path $PSScriptRoot '../../e2e/fase3.playwright.cjs')
                    if ($LASTEXITCODE -ne 0) { throw 'A jornada F3 no navegador falhou.' }
                }
                if ($JornadaF4) {
                    node (Join-Path $PSScriptRoot '../../e2e/fase4.playwright.cjs')
                    if ($LASTEXITCODE -ne 0) { throw 'A jornada F4 no navegador falhou.' }
                }
            } else { $frontend.WaitForExit() }
        }
    } finally { Pop-Location }
} finally {
    if (($JornadaF3 -or $JornadaF4) -and $frontend -and !$frontend.HasExited) { taskkill /PID $frontend.Id /T /F | Out-Null }
    if ($api -and !$api.HasExited) { taskkill /PID $api.Id /T /F | Out-Null }
    # Exclusivamente os recursos sintéticos criados por esta execução.
    docker compose @compose down --volumes 2>$null | Out-Null
    foreach ($nome in $anteriores.Keys) { [Environment]::SetEnvironmentVariable($nome, $anteriores[$nome]) }
}
