$ErrorActionPreference = 'Stop'

$configuracoes = @{
    ASPNETCORE_ENVIRONMENT = 'Production'
    DOTNET_ENVIRONMENT = 'Production'
    ConnectionStrings__Locadora = $env:SQLSERVER_RUNTIME_CONNECTIONSTRING
    Autenticacao__Jwt__Chaves__v1 = $env:JWT_SIGNING_KEY
    Fotos__Provedor = 'R2'
    Fotos__R2__Endpoint = $env:R2_ENDPOINT
    Fotos__R2__Bucket = $env:R2_BUCKET
    Fotos__R2__AccessKeyId = $env:R2_ACCESS_KEY_ID
    Fotos__R2__SecretAccessKey = $env:R2_SECRET_ACCESS_KEY
    Cors__Origens__0 = $env:LOCACAR_FRONTEND_ORIGIN
}

foreach ($nome in $configuracoes.Keys) {
    if ([string]::IsNullOrWhiteSpace($configuracoes[$nome])) {
        throw "Falta o valor para a configuração $nome."
    }
}

$origem = $null
if (-not [Uri]::TryCreate($env:LOCACAR_FRONTEND_ORIGIN, [UriKind]::Absolute, [ref]$origem) -or
    $origem.Scheme -ne 'https' -or $origem.UserInfo -or
    $env:LOCACAR_FRONTEND_ORIGIN -cne $origem.GetLeftPart([UriPartial]::Authority)) {
    throw 'LOCACAR_FRONTEND_ORIGIN deve ser uma origem HTTPS, sem caminho ou barra final.'
}
if ($env:AZURE_SUBSCRIPTION_ID -notmatch '^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$') {
    throw 'AZURE_SUBSCRIPTION_ID inválido.'
}

try {
    $token = az account get-access-token --query accessToken --output tsv --only-show-errors 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
        throw 'Não foi possível obter o token ARM.'
    }
    $autenticacao = @{
        Authentication = 'Bearer'
        Token = ConvertTo-SecureString $token -AsPlainText -Force
    }
    $recurso = "https://management.azure.com/subscriptions/$env:AZURE_SUBSCRIPTION_ID/resourceGroups/locacar/providers/Microsoft.Web/sites/locacar-api/config/appsettings"
    $versao = 'api-version=2024-11-01'
    $atuais = Invoke-RestMethod @autenticacao -Uri "$recurso/list?$versao" -Method Post -AsHashtable
    if ($atuais.properties -isnot [System.Collections.IDictionary]) {
        throw 'Resposta inesperada ao listar App Settings.'
    }
    foreach ($nome in $configuracoes.Keys) {
        $atuais.properties[$nome] = $configuracoes[$nome]
    }
    $corpo = @{ properties = $atuais.properties } | ConvertTo-Json -Depth 4 -Compress
    $null = Invoke-RestMethod @autenticacao -Uri "${recurso}?$versao" -Method Put `
        -ContentType 'application/json; charset=utf-8' -Body $corpo
    Write-Host 'App Settings atualizadas.'
} catch {
    # A resposta do ARM pode conter configurações sensíveis.
    throw 'Falha ao atualizar as App Settings de locacar-api.'
}
