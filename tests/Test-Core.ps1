param([string]$PitchPlugin)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot '..\app\AudioCore.cs')
function Assert($condition,$message){if(-not $condition){throw $message}}
$raw=[float[]]::new(48000*2*2)
for($i=0;$i -lt 96000;$i++){$v=[float](.05*[Math]::Sin(2*[Math]::PI*440*$i/48000));$raw[$i*2]=$v;$raw[$i*2+1]=$v}
$presets=@();foreach($p in 0..2){$r=[AlexListen.Engine]::RenderOffline($raw,$p,60,0,0,$null);$sum=0.;$peak=0.;foreach($v in $r.Samples){Assert (-not [float]::IsNaN($v) -and -not [float]::IsInfinity($v)) 'Nonfinite sample';$sum+=$v*$v;$peak=[Math]::Max($peak,[Math]::Abs($v))};Assert ([Math]::Abs($r.PitchHz-440)-lt 1) 'A440 input pitch estimate';Assert ($peak -gt .001 -and $peak -le .5011) 'Expected nonzero limited audio';$presets+=@{Preset=$p;Rms=[Math]::Sqrt($sum/$raw.Length);Peak=$peak;PitchHz=$r.PitchHz}}
Assert ([Math]::Abs($presets[0].Rms-$presets[1].Rms)-gt .001) 'Warm preset changes the sound'
Assert ([AlexListen.Routing]::ListenGain($false,0)-eq 0) 'Hear OFF must produce zero monitor gain'
Assert ([Math]::Abs([AlexListen.Routing]::ListenGain($true,-24)-.0630957)-lt .00001) 'Hear ON uses selected low gain'
Assert ([AlexListen.Routing]::MainSongOnly(0,1)-eq 0) 'Main is silent without song samples'
Assert ([Math]::Abs([AlexListen.Routing]::PrivateVoiceAndMusic(.2,.1)-.3)-lt .00001) 'Private bus mixes voice and music'
Assert ([AlexListen.Routing]::PrivateVoiceAndMusic(1,1)-le .5011) 'Private bus peak limit'
$optional='not requested';if($PitchPlugin){Assert (Test-Path -LiteralPath $PitchPlugin) 'Pitch plugin missing';$r=[AlexListen.Engine]::RenderOffline($raw,0,60,0,0,$PitchPlugin);$peak=($r.Samples|Measure-Object -Maximum).Maximum;Assert ($peak-gt .001) 'Pitch plugin nonzero processing';$optional='passed'}
@{Passed=$true;Presets=$presets;OptionalPitchPlugin=$optional;AudioDevicesOpened=$false;MicrophoneRecorded=$false;SoundPlayed=$false}|ConvertTo-Json -Depth 5
