<#
    AG ONE Safe - deterministic tenant emulator.

    Lets the real CIS audit and remediation commands stored in the control catalog run unmodified
    on a developer machine, in CI and in demos. Instead of stubbing each cmdlet by hand, the module
    intercepts command resolution: any Verb-Noun that PowerShell cannot find is served from a JSON
    tenant state document. Get-* reads it, Set-/Update-/Enable-/Disable-/New-* writes to it.

    Because the state file is mutated for real, the full product loop is exercised end to end:
    a scan fails, remediation changes the state, the verification re-scan passes, and a rollback
    puts the original values back.

    Live mode never loads this module - the same payload text is sent to the real tenant.
#>

$script:StatePath = $null
$script:State = $null

function Initialize-AgTenantEmulator {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $StatePath
    )

    $script:StatePath = $StatePath

    if (-not (Test-Path -LiteralPath $StatePath)) {
        throw "Tenant emulator state '$StatePath' was not found."
    }

    $script:State = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json

    $ExecutionContext.InvokeCommand.CommandNotFoundAction = {
        param($CommandName, $EventArgs)

        if ($CommandName -match '^(Get|Resolve|Test|Measure|Search|Find|Set|New|Update|Enable|Disable|Remove|Add|Grant|Revoke|Connect|Disconnect|Install|Import)-[A-Za-z0-9_]+$') {
            $escaped = $CommandName.Replace("'", "''")
            $EventArgs.CommandScriptBlock = [scriptblock]::Create(
                "Invoke-AgEmulatedCommand -Name '$escaped' -Arguments `$args")
            $EventArgs.StopSearch = $true
        }
    }
}

function Save-AgTenantState {
    [CmdletBinding()]
    param()

    if ($null -ne $script:StatePath) {
        $script:State | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $script:StatePath -Encoding utf8
    }
}

function ConvertTo-AgArgumentTable {
    param([object[]] $Arguments)

    $table = @{}
    if ($null -eq $Arguments) { return $table }

    for ($i = 0; $i -lt $Arguments.Count; $i++) {
        $item = $Arguments[$i]
        if ($item -isnot [string] -or -not $item.StartsWith('-')) { continue }

        $name = $item.TrimStart('-')
        $next = if ($i + 1 -lt $Arguments.Count) { $Arguments[$i + 1] } else { $null }

        # A parameter with no value, or one immediately followed by another parameter, is a switch.
        if ($null -eq $next -or ($next -is [string] -and $next.StartsWith('-') -and $next.Length -gt 1 -and -not ($next -match '^-\d'))) {
            $table[$name] = $true
        }
        else {
            $table[$name] = $next
            $i++
        }
    }

    return $table
}

function Get-AgStateKey {
    param([string] $Name)

    $noun = $Name.Substring($Name.IndexOf('-') + 1)
    return "Get-$noun"
}

function Set-AgObjectProperty {
    param(
        [Parameter(Mandatory)] $Target,
        [Parameter(Mandatory)] [hashtable] $Values
    )

    foreach ($key in $Values.Keys) {
        if ($key -in @('Identity', 'ResultSize', 'Confirm', 'WhatIf', 'ErrorAction', 'Force')) { continue }

        if ($Target.PSObject.Properties.Name -contains $key) {
            $Target.$key = $Values[$key]
        }
        else {
            $Target | Add-Member -MemberType NoteProperty -Name $key -Value $Values[$key] -Force
        }
    }
}

function Invoke-AgEmulatedCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Name,
        [object[]] $Arguments
    )

    $verb = $Name.Substring(0, $Name.IndexOf('-'))
    $key = Get-AgStateKey -Name $Name
    $table = ConvertTo-AgArgumentTable -Arguments $Arguments

    switch ($verb) {
        { $_ -in @('Connect', 'Disconnect', 'Install', 'Import') } {
            return
        }

        { $_ -in @('Get', 'Resolve', 'Test', 'Measure', 'Search', 'Find') } {
            if ($script:State.PSObject.Properties.Name -contains $key) {
                return $script:State.$key
            }

            Write-Warning "AG emulator has no state for '$key'."
            return [pscustomobject]@{ AgEmulatorMissingState = $true; Command = $Name }
        }

        default {
            if ($script:State.PSObject.Properties.Name -notcontains $key) {
                $script:State | Add-Member -MemberType NoteProperty -Name $key -Value ([pscustomobject]@{}) -Force
            }

            $target = $script:State.$key

            if ($target -is [System.Collections.IEnumerable] -and $target -isnot [string]) {
                # Collection state: -Identity narrows the write to a single object, otherwise the
                # change applies to every object, which is what the bulk CIS remediations expect.
                $identity = $table['Identity']
                foreach ($item in $target) {
                    if ($null -ne $identity) {
                        $matchesIdentity = @('Identity', 'UserPrincipalName', 'DisplayName', 'Name', 'Id') |
                            Where-Object { $item.PSObject.Properties.Name -contains $_ -and $item.$_ -eq $identity }

                        if (-not $matchesIdentity) { continue }
                    }

                    Set-AgObjectProperty -Target $item -Values $table
                }
            }
            else {
                Set-AgObjectProperty -Target $target -Values $table
            }

            Save-AgTenantState
            return
        }
    }
}

Export-ModuleMember -Function Initialize-AgTenantEmulator, Invoke-AgEmulatedCommand, Save-AgTenantState
