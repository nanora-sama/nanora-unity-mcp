param(
    [string]$Tool = 'mcp_status',
    [string]$ArgumentsJson = '{}',
    [string]$CodeFile,
    [switch]$Legacy,
    [int]$TimeoutSec = 190
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$config = Get-Content -LiteralPath (Join-Path $projectRoot '.mcp.json') -Raw | ConvertFrom-Json
$serverUri = [Uri]$config.mcpServers.'nanora-unity-mcp'.url
$baseUri = $serverUri.GetLeftPart([UriPartial]::Authority)
$health = Invoke-RestMethod -Uri "$baseUri/health" -TimeoutSec 5
if ($health.server -ne 'nanora-unity-mcp' -or
    [IO.Path]::GetFullPath($health.projectPath).TrimEnd('\','/') -ne $projectRoot.TrimEnd('\','/')) {
    throw "MCP endpoint belongs to a different project: $($health.projectPath)"
}
$toolArguments = ConvertFrom-Json -InputObject $ArgumentsJson -AsHashtable
if ($CodeFile) {
    $Tool = 'execute_csharp'
    $toolArguments = @{ code = Get-Content -LiteralPath $CodeFile -Raw }
}
$requestBody = @{ jsonrpc = '2.0'; id = [Guid]::NewGuid().ToString(); method = 'tools/call';
    params = @{ name = $Tool; arguments = $toolArguments } } | ConvertTo-Json -Depth 40 -Compress
$endpoint = if ($Legacy) { 'message' } else { 'mcp' }
$reply = Invoke-RestMethod -Uri "$baseUri/$endpoint" -Method Post -ContentType 'application/json; charset=utf-8' `
    -Headers @{ Accept = 'application/json, text/event-stream'; 'MCP-Protocol-Version' = '2025-06-18' } `
    -Body ([Text.Encoding]::UTF8.GetBytes($requestBody)) -TimeoutSec $TimeoutSec
if ($reply.error) { throw ($reply.error | ConvertTo-Json -Compress) }
$reply.result | ConvertTo-Json -Depth 40
if ($reply.result.isError) { exit 1 }
