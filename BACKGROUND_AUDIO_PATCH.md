# Background audio patch for SonicLair.Net (Xbox)

## What changed

1. **New file:** `SonicLair.Xbox/Services/BackgroundAudioService.cs`
   Wires playback state to `SystemMediaTransportControls` (SMTC) — enables
   Play/Pause/Next/Previous, sets `PlaybackStatus`, and pushes track
   title/artist/album/cover art so it shows up in the Xbox guide.

2. **Modified:** `SonicLair.Xbox/App.xaml.cs`
   Two-line addition in the `App()` constructor to construct
   `BackgroundAudioService` once, at startup, and hold a reference for the
   app's lifetime (needed so the event subscription isn't garbage collected).

3. **No manifest change needed.** `Package.appxmanifest` already declares
   `<uap3:Capability Name="backgroundMediaPlayback"/>`.

## To apply

- Drop `BackgroundAudioService.cs` into your `SonicLair.Xbox/Services/` folder.
- Replace your `App.xaml.cs` with the one here (or just add the two marked
  lines to the constructor and the `_backgroundAudio` field yourself).

## Why this should work — and the one real risk

UWP keeps a backgrounded app from being suspended when it detects genuine,
signaled background audio: the `backgroundMediaPlayback` capability plus an
`IsEnabled = true`, `PlaybackStatus = Playing` SMTC session. That's exactly
what this patch adds, and it doesn't touch `SonicLair.Lib`, so the
cross-platform CLI build is untouched.

**The catch:** SonicLair's actual decoding happens through **LibVLCSharp**
(a native VLC engine), not the OS's own `MediaPlayer`. When Xbox backgrounds
an app, it drops its memory budget from ~1024MB to ~128MB. SMTC signaling
should keep the *process* alive and unsuspended, but if LibVLC's native
buffers/codec instance don't fit inside that reduced budget, Xbox can still
kill the audio (or the whole app) shortly after backgrounding — independent
of whether SMTC is wired correctly. This is not something I can verify
without an actual Xbox devkit, and it's the main open question with this
approach.

**If audio still cuts out after this patch:**
- Try lowering LibVLC's network/file caching (`--network-caching=1000`,
  smaller demux buffers) when constructing `LibVLC` in `MusicPlayerService`
  — less native memory held onto may survive the 128MB ceiling.
- If it still doesn't survive, the reliable fix is swapping LibVLC for
  `Windows.Media.Playback.MediaPlayer` on the Xbox target specifically
  (feeding it the same Subsonic stream URI via `MediaSource.CreateFromUri`).
  `MediaPlayer` has native, built-in SMTC/background integration and a much
  lighter footprint, but it's a bigger change — it'd mean forking
  `MusicPlayerService` for the Xbox project instead of sharing it with the
  CLI. Worth trying only if the SMTC-only patch doesn't hold up in testing.

## Build & deploy (needs a Windows PC — can't be done from here)

1. Install **Visual Studio 2022** with the **Universal Windows Platform
   development** workload (Visual Studio Installer → Workloads).
2. Open `SonicLair.Net.sln`, set the startup project to `SonicLair.Xbox`,
   configuration `Release`, platform `x64`.
3. Put your Xbox in **Developer Mode** (Dev Home app on console, or via the
   Xbox Device Portal) and note its IP address.
4. In Visual Studio: Project properties → Debug → Target device: **Remote
   Machine** → enter the Xbox's IP (pair via the PIN it displays if asked).
5. Build → Deploy (or F5 to deploy and launch directly for testing).
6. To test: start a track, press the Xbox button to open the guide, and
   check whether a Multitasking/background-audio tab appears — then launch
   any game and confirm the audio keeps playing.
7. For a store-independent install without Visual Studio each time, you can
   `Create App Packages` (right-click project → Publish) to get a signed
   `.appxbundle`, then sideload it via the Xbox Device Portal's Apps page.

If it doesn't survive backgrounding on real hardware, come back with what
you saw (audio cuts immediately vs. after a few seconds, app crashes vs.
just goes silent) — that'll tell us whether it's a suspension issue (fixable
in code) or a memory-budget issue (needs the MediaPlayer rewrite).
