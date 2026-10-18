# Exercise the production routing controller and actual UI callbacks with fake
# controls/engines. No GUI session, audio client or physical device is opened.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\app\RouteGuard.ps1')
Add-Type -AssemblyName System.Drawing
$source=Join-Path $PSScriptRoot '..\app\Listen.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
if($errors){throw ($errors.Message -join '; ')}
$functions=@('Get-DisplayedRoute','Sync-DisplayedRoute','Update-RouteUi','StartAudio','Poll-RouteApplication')
foreach($node in $ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)){
    if($node.Name -in $functions){. ([scriptblock]::Create($node.Extent.Text))}
}
$callbacks=@{}
foreach($node in $ast.FindAll({param($n)$n -is [Management.Automation.Language.InvokeMemberExpressionAst]},$true)){
    if($node.Member.Value -eq 'Add_Click' -and $node.Expression -is [Management.Automation.Language.VariableExpressionAst]){
        $name=$node.Expression.VariablePath.UserPath
        if($name -in @('hear','mainMusic','apply')){$callbacks[$name]=$node.Arguments[0].ScriptBlock.GetScriptBlock()}
    }
    if($node.Member.Value -eq 'Add_Shown' -and $node.Expression -is [Management.Automation.Language.VariableExpressionAst] -and $node.Expression.VariablePath.UserPath -eq 'form'){
        $callbacks.startup=$node.Arguments[0].ScriptBlock.GetScriptBlock()
    }
}
if($callbacks.Count -ne 4){throw 'Expected production output/Apply/startup callbacks were not found'}
$script:checks=0
function Assert($condition,[string]$message){if(-not $condition){throw $message};$script:checks++}
function FakeControl { [pscustomobject]@{Enabled=$true;Checked=$false;Text='';ForeColor=[Drawing.Color]::Silver} }
function FakeEngine {
    $e=[pscustomobject]@{Running=$false;Error='';Hear=$false;MainMusic=$false;IsRecording=$false;Disposed=$false}
    $e|Add-Member ScriptMethod Dispose {$this.Hear=$false;$this.MainMusic=$false;$this.Running=$false;$this.Disposed=$true}
    return $e
}
function New-SelectedRouteEngine($Selection){
    $script:factoryCalls++
    if($script:factoryThrows){throw 'Simulated constructor failure'}
    FakeEngine
}
function ApplyControls {} # DSP is covered separately; this test never loads it.
function Show-PanelWindow {} # Startup window presentation is stubbed; no native window API runs.
$tempRoot=Join-Path ([IO.Path]::GetTempPath()) ('vocal-route-tests-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($tempRoot)
$configPath=Join-Path $tempRoot 'devices.local.json'
function ResetFixture {
    $script:routeGuard=New-RouteGuard;$script:engine=$null;$script:applyTicket=$null;$script:applyWatch=$null
    $script:factoryCalls=0;$script:factoryThrows=$false;$script:message=''
    $script:micChoice=[pscustomobject]@{SelectedIndex=0;SelectedItem=[pscustomobject]@{Id='mic-A'}}
    $script:output=[pscustomobject]@{SelectedIndex=0;SelectedItem=[pscustomobject]@{Id='headphones-A'}}
    $script:mainChoice=[pscustomobject]@{SelectedItem=[pscustomobject]@{Id='main-A'}}
    foreach($name in @('mirror','headphones','hear','mainMusic','record','apply','routeBadge','songName','form','title')){
        Set-Variable -Name $name -Scope Script -Value (FakeControl)
    }
    $script:title.ForeColor=[Drawing.Color]::FromArgb(52,222,194)
    $script:timer=[pscustomobject]@{Started=$false}
    $script:timer|Add-Member ScriptMethod Start {$this.Started=$true}
    Sync-DisplayedRoute;Update-RouteUi
}
function ApplyAndFinish {
    & $callbacks.apply
    Assert ($script:routeGuard.Phase -eq 'Applying') 'Apply must wait for actual engine readiness'
    $script:engine.Running=$true;Poll-RouteApplication
    Assert (Test-RouteReady $script:routeGuard $script:engine) 'Successful initialization must authorize exactly the selected route'
    Assert (-not $script:engine.Hear -and -not $script:engine.MainMusic -and -not $headphones.Checked) 'Apply must leave both outputs and headphone confirmation OFF'
}
function EnableBoth {
    $headphones.Checked=$true;& $callbacks.hear;& $callbacks.mainMusic
    Assert ($script:engine.Hear -and $script:engine.MainMusic) 'Applied/confirmed route must support independent output toggles'
}
function AssertBlocked($context){
    $headphones.Checked=$true # Attempt to bypass the disabled checkbox programmatically.
    & $callbacks.hear;& $callbacks.mainMusic
    Assert (-not $script:engine.Hear -and -not $script:engine.MainMusic) "$context must refuse both re-enable requests"
    Assert (-not $hear.Enabled -and -not $mainMusic.Enabled -and -not $headphones.Enabled) "$context must disable the relevant UI controls"
    Assert (-not $headphones.Checked) "$context must clear stale headphone confirmation"
}
try {
    ResetFixture
    Assert (-not $hear.Enabled -and -not $mainMusic.Enabled) 'Startup must be locked before any Apply'
    & $callbacks.hear;& $callbacks.mainMusic
    Assert ($null -eq $script:engine) 'Startup re-enable attempts must not create an audio engine'
    & $callbacks.startup
    Assert ($script:routeGuard.Phase -eq 'Applying') 'Starting engine is not yet an applied route'
    Assert $timer.Started 'Production startup callback must start its polling timer'
    AssertBlocked 'Initialization pending'
    $script:engine.Running=$true;Poll-RouteApplication
    & $callbacks.hear
    Assert (-not $script:engine.Hear) 'Successful Apply still requires fresh headphone confirmation'
    EnableBoth

    foreach($selector in @('Microphone','Headphones','Main','Mirror')){
        ResetFixture;ApplyAndFinish;EnableBoth
        $before=$script:factoryCalls
        switch($selector){
            'Microphone' {$micChoice.SelectedItem.Id='mic-B'}
            'Headphones' {$output.SelectedItem.Id='headphones-B'}
            'Main' {$mainChoice.SelectedItem.Id='main-B'}
            'Mirror' {$mirror.Checked=$true}
        }
        Sync-DisplayedRoute;Update-RouteUi
        Assert ($script:routeGuard.Phase -eq 'Unapplied') "$selector change must invalidate the route"
        Assert ($apply.Text -eq 'APPLY ROUTING' -and $routeBadge.Text.StartsWith('NOT APPLIED')) "$selector change must make Apply visible"
        AssertBlocked "$selector change"
        Assert ($script:factoryCalls -eq $before) 'Selector change must not silently reopen endpoints'
        ApplyAndFinish;EnableBoth
    }

    ResetFixture;ApplyAndFinish;EnableBoth
    $output.SelectedItem.Id='headphones-B' # Skip the selection event entirely.
    & $callbacks.hear;& $callbacks.mainMusic
    AssertBlocked 'Displayed route mismatch with missed change event'

    ResetFixture;ApplyAndFinish;EnableBoth
    $old=$script:engine;$before=$script:factoryCalls;$old.IsRecording=$true
    $output.SelectedItem.Id='headphones-B';Sync-DisplayedRoute;Update-RouteUi
    Assert ($record.Enabled -and -not $apply.Enabled) 'Recording must remain stoppable while Apply is disabled'
    & $callbacks.apply
    Assert ([object]::ReferenceEquals($old,$script:engine) -and $old.IsRecording -and -not $old.Disposed) 'Forced Apply must preserve an active recording'
    Assert ($script:factoryCalls -eq $before) 'Forced Apply during recording must not reopen streams'
    AssertBlocked 'Selection change during recording'

    ResetFixture;ApplyAndFinish;EnableBoth
    $old=$script:engine;$headphones.Checked=$true
    & $callbacks.apply
    $staleTicket=$script:applyTicket
    Assert ($old.Disposed -and -not $old.Hear -and -not $old.MainMusic) 'Repeated Apply must release the old muted engine'
    AssertBlocked 'Repeated Apply pending'
    & $callbacks.apply # Force a second callback while Apply is visually disabled.
    $script:engine.Running=$true
    Assert (-not(Update-RouteApplication $script:routeGuard $staleTicket $script:engine)) 'An older completion must not authorize a newer Apply'
    Assert (-not(Test-RouteReady $script:routeGuard $script:engine)) 'Latest Apply still needs its own completion'
    Poll-RouteApplication
    Assert (Test-RouteReady $script:routeGuard $script:engine) 'Latest matching Apply should succeed'
    Assert (-not $headphones.Checked -and -not $script:engine.Hear -and -not $script:engine.MainMusic) 'Repeated Apply must not restore output enables'

    ResetFixture;ApplyAndFinish;EnableBoth
    $mainChoice.SelectedItem.Id=$output.SelectedItem.Id
    & $callbacks.apply
    Assert ($script:routeGuard.Phase -eq 'Failed') 'Invalid identical outputs must fail Apply'
    AssertBlocked 'Validation failure'

    ResetFixture;ApplyAndFinish;EnableBoth
    $script:factoryThrows=$true;& $callbacks.apply
    Assert ($script:routeGuard.Phase -eq 'Failed' -and $null -eq $script:engine) 'Constructor failure must not retain an authorized old engine'
    & $callbacks.hear;& $callbacks.mainMusic
    Assert (-not $hear.Enabled -and -not $mainMusic.Enabled) 'Constructor failure must keep output buttons disabled'
    $script:factoryThrows=$false;ApplyAndFinish

    ResetFixture;ApplyAndFinish;EnableBoth
    $script:engine|Add-Member ScriptMethod Dispose {throw 'Simulated stream-close failure'} -Force
    & $callbacks.apply
    Assert ($script:routeGuard.Phase -eq 'Failed') 'Failure to close the old stream must fail Apply'
    AssertBlocked 'Old stream close failure'

    ResetFixture;& $callbacks.apply
    $script:engine.Error='Simulated unsupported format';Poll-RouteApplication
    Assert ($script:routeGuard.Phase -eq 'Failed') 'Asynchronous initialization error must fail Apply'
    $script:engine.Error='';$script:engine.Running=$true
    AssertBlocked 'Failed engine later reports running'
    ApplyAndFinish
    $script:engine.Running=$false;Poll-RouteApplication
    AssertBlocked 'Applied engine stops'

    ResetFixture;& $callbacks.apply
    $script:applyWatch=[pscustomobject]@{Elapsed=[TimeSpan]::FromSeconds(6)};Poll-RouteApplication
    Assert ($script:routeGuard.Phase -eq 'Failed') 'Initialization timeout must fail Apply'
    AssertBlocked 'Apply timeout'

    ResetFixture;$mainChoice.SelectedItem.Id='';$mirror.Checked=$true;& $callbacks.apply
    Assert ($script:routeGuard.Phase -eq 'Failed') 'Mirror with no Main output must fail validation'
    Assert ($script:factoryCalls -eq 0) 'Invalid selection must be rejected before opening any engine'
    ResetFixture;$output.SelectedItem=$null;& $callbacks.apply
    Assert ($script:routeGuard.Phase -eq 'Failed' -and $script:factoryCalls -eq 0) 'Startup with missing headphones must stay locked without opening an engine'
    ResetFixture;$output.SelectedItem=$null;$output.SelectedIndex=-1;& $callbacks.startup
    Assert ($script:factoryCalls -eq 0 -and -not $hear.Enabled -and -not $mainMusic.Enabled) 'Production startup with incomplete choices must not initialize or authorize any route'
    ResetFixture;$mainChoice.SelectedItem.Id='';ApplyAndFinish;& $callbacks.mainMusic
    Assert (-not $script:engine.MainMusic -and -not $mainMusic.Enabled) 'Headphones-only route must not enable Main Music'

    [pscustomobject]@{Passed=$true;Assertions=$script:checks;ActualUiCallbacksTested=$true;FakeEnginesOnly=$true;AudioDevicesOpened=$false;RunningUserSessionTouched=$false;SoundPlayed=$false}|ConvertTo-Json
} finally {
    # Only the one synthetic preference file produced by this isolated fixture.
    if(Test-Path -LiteralPath $configPath){Remove-Item -LiteralPath $configPath}
    [IO.Directory]::Delete($tempRoot)
}
