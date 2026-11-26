<#
.SYNOPSIS
    Uruchamia lokalne środowisko demo w osobnych oknach: Azurite, API, frontend (opcjonalnie Functions).

.EXAMPLE
    ./dev.ps1
    ./dev.ps1 -WithFunctions
#>
param(
    # Uruchamia też Azure Functions (wymaga Azure Functions Core Tools: func).
    [switch]$WithFunctions
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# pwsh (PowerShell 7), a jeśli go nie ma – Windows PowerShell.
$shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell' }

function Assert-Command([string]$Name, [string]$Hint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Nie znaleziono '$Name'. $Hint"
    }
}

function Start-DevWindow([string]$Title, [string]$WorkingDirectory, [string]$Command) {
    $script = "`$Host.UI.RawUI.WindowTitle = '$Title'; Set-Location '$WorkingDirectory'; $Command"
    Start-Process $shell -ArgumentList '-NoExit', '-NoProfile', '-Command', $script | Out-Null
    Write-Host "  uruchomiono: $Title" -ForegroundColor Green
}

function Wait-Port([int]$Port, [string]$Name, [int]$TimeoutSeconds = 60) {
    Write-Host "  czekam na $Name (port $Port)..." -NoNewline
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $client = New-Object System.Net.Sockets.TcpClient
        try {
            $client.Connect('127.0.0.1', $Port)
            Write-Host ' gotowe' -ForegroundColor Green
            return
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
        finally {
            $client.Dispose()
        }
    }
    Write-Host ' brak odpowiedzi – sprawdź okno tej usługi' -ForegroundColor Yellow
}

Assert-Command 'dotnet' 'Zainstaluj .NET SDK 10.'
Assert-Command 'npm' 'Zainstaluj Node.js LTS.'
if ($WithFunctions) {
    Assert-Command 'func' 'Zainstaluj Azure Functions Core Tools v4.'
    $settings = Join-Path $root 'functions/NotesFunctions/local.settings.json'
    if (-not (Test-Path $settings)) {
        throw "Brak $settings. Skopiuj local.settings.json.example i wpisz klucz sprzątania (patrz functions/README.md)."
    }
}

Write-Host '1/4 Azurite (emulator Blob Storage)' -ForegroundColor Cyan
# --skipApiVersionCheck: nowe Azure SDK bywa szybsze niż wydania Azurite.
Start-DevWindow 'Azurite' $root 'npx azurite --location .azurite --skipApiVersionCheck'
Wait-Port 10000 'Azurite'

Write-Host '2/4 Notes API' -ForegroundColor Cyan
Start-DevWindow 'Notes API' (Join-Path $root 'api') 'dotnet run'

if ($WithFunctions) {
    Write-Host '3/4 Azure Functions' -ForegroundColor Cyan
    Start-DevWindow 'Notes Functions' (Join-Path $root 'functions/NotesFunctions') 'func start'
}
else {
    Write-Host '3/4 Azure Functions – pominięte (użyj -WithFunctions)' -ForegroundColor DarkGray
}

Write-Host '4/4 Frontend' -ForegroundColor Cyan
$web = Join-Path $root 'web'
$install = if (-not (Test-Path (Join-Path $web 'node_modules'))) { 'npm install; ' } else { '' }
Start-DevWindow 'Notes Web' $web "${install}npm run dev"

Wait-Port 5000 'API' -TimeoutSeconds 120

Write-Host ''
Write-Host 'Gotowe:' -ForegroundColor Cyan
Write-Host '  Frontend: http://localhost:5173'
Write-Host '  API:      http://localhost:5000/swagger'
Write-Host '  Health:   http://localhost:5000/api/health'
if ($WithFunctions) {
    Write-Host '  Functions: http://localhost:7071'
}
Write-Host 'Aby zatrzymać, zamknij otwarte okna.'
