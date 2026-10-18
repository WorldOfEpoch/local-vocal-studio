# Device-free routing state used by the UI and its regression tests.
function New-RouteSelection {
    param([string]$MicId, [string]$HeadphoneId, [string]$MainId, [bool]$Mirror)
    [pscustomobject]@{ MicId=$MicId; HeadphoneId=$HeadphoneId; MainId=$MainId; Mirror=$Mirror }
}

function Copy-RouteSelection($Selection) {
    New-RouteSelection $Selection.MicId $Selection.HeadphoneId $Selection.MainId $Selection.Mirror
}

function Test-SameRoute($Left, $Right) {
    if ($null -eq $Left -or $null -eq $Right) { return $false }
    return [string]::Equals($Left.MicId,$Right.MicId,[StringComparison]::Ordinal) -and
        [string]::Equals($Left.HeadphoneId,$Right.HeadphoneId,[StringComparison]::Ordinal) -and
        [string]::Equals($Left.MainId,$Right.MainId,[StringComparison]::Ordinal) -and
        $Left.Mirror -eq $Right.Mirror
}

function New-RouteGuard {
    [pscustomobject]@{ Desired=$null; Applied=$null; Pending=$null; Phase='Unapplied'; Sequence=0; Error='' }
}

function Stop-RouteOutputs($Engine) {
    if ($null -ne $Engine) { $Engine.Hear=$false; $Engine.MainMusic=$false }
}

function Set-DisplayedRoute($State, $Selection, $Engine) {
    Stop-RouteOutputs $Engine
    $State.Desired=Copy-RouteSelection $Selection
    $State.Sequence++
    $State.Pending=$null
    $State.Phase='Unapplied'
    $State.Error=''
}

function Fail-RouteApplication($State, $Engine, [string]$Message) {
    Stop-RouteOutputs $Engine
    $State.Sequence++
    $State.Applied=$null
    $State.Pending=$null
    $State.Phase='Failed'
    $State.Error=$Message
}

function Start-RouteApplication($State, $Engine) {
    Stop-RouteOutputs $Engine
    $State.Sequence++
    $State.Applied=$null
    $State.Pending=$null
    $State.Error=''
    $State.Phase='Applying'
    $selection=$State.Desired
    $errorMessage=if (-not $selection.MicId -or -not $selection.HeadphoneId) {
        'Select a microphone and headphone output first.'
    } elseif ($selection.MainId -and $selection.MainId -eq $selection.HeadphoneId) {
        'Main and Headphones must be different endpoints.'
    } elseif ($selection.Mirror -and -not $selection.MainId) {
        'Select a Main Output to mirror.'
    } else { '' }
    if ($errorMessage) {
        Fail-RouteApplication $State $Engine $errorMessage
        return $null
    }
    $ticket=[pscustomobject]@{ Sequence=$State.Sequence; Selection=(Copy-RouteSelection $selection) }
    $State.Pending=$ticket
    return $ticket
}

function Update-RouteApplication($State, $Ticket, $Engine) {
    # A late success from an earlier Apply can never authorize a newer selection.
    if ($State.Phase -ne 'Applying' -or $null -eq $Ticket -or
        $null -eq $State.Pending -or $Ticket.Sequence -ne $State.Sequence -or
        -not (Test-SameRoute $Ticket.Selection $State.Desired)) { return $false }
    if ($null -eq $Engine) { return $false }
    if ($Engine.Error) {
        Fail-RouteApplication $State $Engine $Engine.Error
        return $false
    }
    if (-not $Engine.Running) { return $false }
    Stop-RouteOutputs $Engine
    $State.Applied=Copy-RouteSelection $Ticket.Selection
    $State.Pending=$null
    $State.Phase='Applied'
    $State.Error=''
    return $true
}

function Test-RouteReady($State, $Engine) {
    return $State.Phase -eq 'Applied' -and $null -ne $Engine -and
        $Engine.Running -and -not $Engine.Error -and
        (Test-SameRoute $State.Desired $State.Applied)
}

function Request-RouteHear($State, $Engine, [bool]$HeadphonesConfirmed) {
    if (-not (Test-RouteReady $State $Engine)) { Stop-RouteOutputs $Engine; return $false }
    if (-not $HeadphonesConfirmed) { $Engine.Hear=$false; return $false }
    $Engine.Hear=-not $Engine.Hear
    return $true
}

function Request-RouteMainMusic($State, $Engine) {
    if (-not (Test-RouteReady $State $Engine)) { Stop-RouteOutputs $Engine; return $false }
    if (-not $State.Applied.MainId) { $Engine.MainMusic=$false; return $false }
    $Engine.MainMusic=-not $Engine.MainMusic
    return $true
}
