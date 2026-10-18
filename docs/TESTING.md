# Test scope

Automated checks use generated tones only. No user recordings, local endpoint IDs, account information or private paths are committed.

| Flow | Result |
| --- | --- |
| Three original DSP presets; finite samples | Passed |
| Sustained A440 input pitch estimate | 439.998 Hz |
| GSnap 3.1.0 synthetic processing | Passed with separately licensed local DLL |
| Synthetic dual DRY / FX recording | Passed: both 96480 frames, 48 kHz stereo, 24-bit; contents differ |
| Local WAV song transport, pause/seek/rewind | Passed |
| Generated MP3 → float WAV conversion | Passed using separately installed FFmpeg |
| Startup headphones/main silence | Passed; Main endpoint unopened during safe stream test |
| Invalid device failure and stop | Passed; no fallback output |
| Main bus accepts song samples only | Code-reviewed and offline gain/mix contract tested |
| GUI tone presets, dry toggle, effects values and headphone guard | Tested with output OFF |
| Main/private headphone audible split; other-app loopback mirroring | Untested on physical outputs |
| UI key/scale/natural/hard correction panel | Displayed; DSP parameter mapping checked against installed GSnap parameters; musical listening assessment pending |
| Headphone listening quality / physical round-trip latency | Requires a hardware listen check |
| Actual Suno song file | Untested; the integration loads a downloaded local audio file, not a Suno API |
| Real calls / long recordings / every Windows device | Untested |

The safe stream test opened the known headphone endpoint with monitoring OFF throughout. A requested audible dual-output test was not performed because the physical destination remained unconfirmed. Use headphones and local signals to complete physical validation before a performance.

This is a small original local host with limited format support, not a certified or broadly validated DAW/audio driver. On the tested busy desktop, 10 ms input/output engine periods were more reliable than the input driver's minimum 2 ms period. Device periods and queue estimates are not end-to-end latency measurements.
