function Get-AssemblyFileVersion([string]$Version) {
    $core = ($Version -split '[+-]')[0]
    $parts = @($core.Split('.') | Where-Object { $_ -match '^\d+$' })
    while ($parts.Count -lt 4) {
        $parts += '0'
    }

    if ($parts.Count -gt 4) {
        $parts = $parts[0..3]
    }

    return ($parts -join '.')
}
