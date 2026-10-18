# Advanced Waveform workflow

Install Waveform Free separately from Tracktion's official site. Free needs a free Tracktion account to unlock; paid Pro is optional. Do not replace an existing working studio project just to follow this guide.

In **Waveform 14 → Settings → Audio Devices → Output Device**, select the output matching the actual headphone connection. Razer Game may be labeled **Speakers (Razer Audio Controller - Game)**; Yeti's jack may be labeled **Speakers (Yeti Stereo Microphone)**. Product names containing “Speakers” do not establish where a physical headset is plugged in. Check that before monitoring. Leave the studio master low initially.

The simple panel and Waveform are independent audio paths. Turn the panel's Hear Myself OFF before enabling Waveform input monitoring. For processed monitoring, enable the selected mic input's Monitoring Mode, unmute only the intended vocal track and watch input/output meters. Yeti hardware direct monitoring is a separate dry signal.

Use separate VOICE, SONG and DRY COMPARE tracks. Keep the dry comparison muted during recording/monitoring. A DAW normally records the dry input before track effects; render/export the vocal track for a processed comparison. Detailed 8-band EQ, compression/limiting and reverb/delay are available in Waveform Free; enable the effects you need. Optional GTune and GSnap require separate downloads/licenses. The GSnap GUI shows input/corrected pitch and offers fixed-scale or MIDI workflows.

## Optional calls

Only use a virtual audio cable already installed and authorized for use. A new audio driver requires explicit installation approval. No driver is included here.

One selectable workflow: a **voice-only** Waveform session outputs processed VOICE to **CABLE Input**; the call app explicitly chooses **CABLE Output** as its microphone. The call app's return/output stays on physical headphones. Do not add SONG, desktop loopback, call return or the panel's mirrored Main bus to that outgoing session. Leave the Windows default microphone unchanged. No call is needed to perform the initial local routing test.

This setup's earlier cable test proved local synthetic transfer through an existing VB-CABLE. It did not verify Waveform-to-cable-to-call operation or simultaneous processed headphone monitoring. A single-output DAW may require an additional explicitly configured monitor path. This repository does not claim that final flow is already working.

Restore by selecting the original microphone/output in the individual call app and closing/bypassing the voice-only cable session. Keep a backup of existing DAW/OBS settings before editing them.
