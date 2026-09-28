function Get-AssemblyFileVersion([string]$Version) {
    $core = ($Version -split '[+-]')[0]
    $parts = @($core.Split('.') | Where-Object { $_ -match '^\d+$' })
    while ($parts.Count -lt 4) {
        $parts += '0'
    }

    if ($parts.Count -gt 4) {
        $parts = $parts[0..3]
    }

    if ($Version -match '(?i)ci\.(\d+)') {
        $revision = [int]$Matches[1]
        if ($revision -gt 65535) {
            $revision = $revision % 65536
        }

        $parts[3] = "$revision"
    }

    return ($parts -join '.')
}
