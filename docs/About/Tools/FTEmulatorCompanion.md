# VRCFT-FtEmuCompanion

The README of [Rionquosue/VRCFT-FtEmuCompanion](https://github.com/Rionquosue/VRCFT-FtEmuCompanion), FTEmulator's VRCFaceTracking companion. Paths below are in that repository.

Companion to RiryTools FTEmulator: a VRCFaceTracking module, a Recorder app and a command-line app that record face tracking and visemes, and turn recorded videos into face tracking and visemes. The Unity side (FTEmulator) is the `com.riry.ftemulator` package in the [RiryTools VPM listing](https://rionquosue.github.io/rirytools_vpm/).

- **Module** (`RiryTools FTRecorder`): feeds VRCFaceTracking from a recording, a video file, a webcam or the Babble app, and records everything it sends.
- **Recorder** (`Recorder.pyw`, Python): a window that relays VRCFaceTracking's output to VRChat while it is open, and records it (what FTEmulator in Unity receives) together with the visemes and your microphone on demand. It can start with SteamVR.
- **Companion app** (`python -m ftrecorder`, Python): processes videos and replays recordings, from the command line.

## Downloads

Every push to `main` builds the Recorder and the companion command line into one program, attached to the [latest](https://github.com/Rionquosue/VRCFT-FtEmuCompanion/releases/tag/latest) pre-release (replaced each time):

- `RiryTools-FTRecorder.exe` (Windows) and `RiryTools-FTRecorder-x86_64.AppImage` (Linux; `chmod +x` it first). Opened with no arguments, they show the Recorder; with arguments, they are the companion command line (`RiryTools-FTRecorder.exe process --video clip.mp4 --out clip.mkv`).
- They include Python, numpy, onnxruntime and the face models, so nothing else is installed for them except ffmpeg (below). They are not code-signed, so Windows SmartScreen may ask before the first start.
- `VRCFT_Module.zip`: the module, built as below. Install it in VRCFaceTracking's Module Registry as a local .zip module.

`.github/workflows/build.yml` and `packaging/build.py` make the downloads; the module is compiled against VRCFaceTracking.Core and .SDK built from VRCFaceTracking's source at a pinned commit.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build the module, and [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) to use it.
- To run the Recorder and the companion app from source instead of the downloads: [Python](https://www.python.org/downloads/) 3.10 or newer, with tkinter (included in python.org's Windows installer; on Linux, your distribution's `python3-tk`), for the Recorder and the companion app. `process` also needs `pip install -r requirements.txt` (numpy, onnxruntime).
- [ffmpeg](https://ffmpeg.org/) on `PATH` (or its path set) for recording, processing, `.mkv` recordings, video files and webcams. Every command and module source that needs it checks first and says how to install it when it is missing.
- On Windows, VRChat installed through Steam for OVRLipSync (VRChat's viseme engine), or the path to an `OVRLipSync.dll`. OVRLipSync is a Windows DLL: on Linux, visemes come only from a running VRChat (and `process --visemes classifier`).
- To take visemes from a running VRChat when processing videos: a virtual audio cable (such as VB-Cable), with VRChat's microphone set to the cable's output.
- Optional: a Babble face model (`.onnx`) you already have, for the mouth.

## Build

Module zip (Windows):

```bash
dotnet publish FTRecorder.Module -c Release -r win-x64 -p:VrcftDir="C:\path\to\folder with VRCFaceTracking.Core.dll and VRCFaceTracking.SDK.dll"
```

The zip is written to `FTRecorder.Module/bin/RiryTools.FTRecorder.Module-win-x64.zip`. Install it in VRCFaceTracking's Module Registry as a local .zip module.

The Recorder and the companion app are Python: use the downloads, or run them from this folder (below). To build the downloads yourself: `pip install pyinstaller numpy onnxruntime`, then `python packaging/build.py` (Windows), or `python packaging/build.py --appimagetool PATH` with [appimagetool](https://github.com/AppImage/appimagetool) (Linux).

## Module

Settings are in `%LocalAppData%\RiryTools\FTRecorder\module-config.json`, written with defaults on the first start. Changes apply while VRCFaceTracking runs.

| Setting | Default | Use |
|---|---|---|
| `source` | `webcam` | `playback`, `video`, `webcam` or `babbleOsc`. |
| `record` | `false` | While `true`, everything the module sends is recorded into `recordingsFolder/module_<date>_<time>.mkv` (the recording attached, with 20 ms of silence as its track). Until recording stops it is kept in `NAME.mkv.recording.ftrec.jsonl`, which is deleted once the `.mkv` is written and kept (named in the log) if not. Needs ffmpeg: checked when recording starts; when it is missing, the log says so and nothing is recorded. |
| `recordingsFolder` | `Documents\RiryTools FTRecorder` | Where recordings go. |
| `playbackFile`, `loop` | `""`, `true` | The recording `playback` plays (`.ftrec.jsonl`, or `.mkv` through ffmpeg); `loop` also restarts a video file. |
| `videoFile` | `""` | The video `video` plays at its own speed. |
| `webcamFormat`, `webcamDevice` | `dshow`, `""` | ffmpeg's input format and the camera's name. List cameras with `ffmpeg -list_devices true -f dshow -i dummy`. |
| `ffmpegPath` | `ffmpeg` | ffmpeg's path. |
| `frameSize`, `fps` | `640`, `30` | Frames are fitted into a square of this size, at this rate. |
| `babbleModel`, `babbleLabels`, `babbleCropScale` | `""`, `""`, `1` | A Babble model for the mouth (video and webcam), its output names (default `Models/babble-labels.json`), and how much of the face its crop takes. |
| `babbleOscPort` | `8888` | Where the Babble app sends (`babbleOsc`). The Babble module listens there too, so use one or the other. |
| `threads` | `2` | ONNX Runtime threads per model. |

Video and webcam run MediaPipe's face models (bundled) for the whole face; a Babble model, when set, replaces MediaPipe's values for the shapes it names.

## Recorder

Open it by double-clicking `Recorder.pyw` (Windows), or with `python Recorder.pyw` or `python -m ftrecorder recorder` from this folder. Set VRCFaceTracking's OSC output port to `9010`. Whenever the Recorder is open it relays everything VRCFaceTracking sends there on to VRChat on `9000`, unchanged; the line next to the button counts the packets. It records only between **Start recording** and **Stop and save**. A recording puts VRCFaceTracking's output, visemes and (with **Record audio**) your microphone into one Matroska file. While it runs, the parts are kept next to the output (`NAME.mkv.recording.ftrec.jsonl`, and `NAME.mkv.microphone.s16le` with audio), so a crash keeps them; on stop they are combined into the `.mkv` and deleted. If writing the `.mkv` fails, they are kept and the log says where. Closing the window while recording stops and saves first. When ffmpeg is not found, **Start recording** shows an "FFmpeg not found" popup and does nothing else.

| Field | Default | Use |
|---|---|---|
| Output | `Documents\RiryTools FTRecorder` | The folder recordings go to (created when needed). Each is named `FTRecording-HHmm-ddMMyy.mkv` from when it starts, 24-hour time and day, month, year without separators: `FTRecording-1405-201126.mkv`. Another started in the same minute gets ` (2)`, then ` (3)`, and so on. |
| VRCFaceTracking OSC output port | `9010` | Set VRCFaceTracking's OSC output port to this. Changing a port restarts the relay; both are locked while recording. |
| Pass on to port | `9000` | Where the packets go on to: VRChat's OSC input port. |
| Microphone | the first one ffmpeg lists | Windows: the DirectShow microphones (`ffmpeg -list_devices true -f dshow -i dummy`). Linux: PipeWire and PulseAudio sources (through ffmpeg's `pulse` input, which PipeWire serves; speaker monitors left out) and ALSA's `default` and `plughw:` cards (`ffmpeg -sources pulse`, `ffmpeg -sources alsa`). No others. With none found, "! Visemes will not be recorded without a mic" shows beside the box; recording still works, with face tracking only. |
| Record audio | on | Keeps the microphone's audio in the recording. Off: on Windows with VRChat not running, the microphone still makes the OVRLipSync visemes, but its audio is not kept; otherwise the microphone is not opened. |
| ffmpeg | `ffmpeg` | ffmpeg's path. |
| OVRLipSync.dll | VRChat's copy | The OVRLipSync library, used while VRChat is not running (Windows). On Linux the field is off, with "! Visemes are only available when VRChat is running." beside it. |
| Start with SteamVR | off | SteamVR opens the Recorder when it starts (it relays from then on; recording still waits for Start recording). |

Visemes depend on whether VRChat is running when the recording starts:

- **VRChat running:** turn OSC on in VRChat. It finds the Recorder through OSCQuery and sends it `Viseme` and `Voice`, which are recorded.
- **VRChat not running, Windows:** the microphone goes through OVRLipSync, from the field, else from your VRChat install (found through Steam, in `VRChat_Data\Plugins`). When it cannot be found or loaded, the log shows the error and a file picker asks for `OVRLipSync.dll`; cancelling it records without visemes (said in the log). Without a microphone there are no visemes.
- **VRChat not running, Linux:** no visemes (said in the log).

The log shows what is happening and every error; while recording, the line next to the button counts the packets recorded, VRChat viseme values and seconds of microphone.

**Start with SteamVR** (Windows) registers the Recorder with SteamVR as an overlay application (`%LocalAppData%\RiryTools\FTRecorder\RiryTools.FTRecorder.Recorder.vrmanifest`, which starts `Recorder.pyw` with the Python that ticked the box, so tick it again after moving this folder or changing Python) and turns on its auto-launch; unticking turns auto-launch off and unregisters it. It uses Valve's `openvr_api.dll`, included in `ThirdParty/OpenVR`. Ticking or unticking starts SteamVR's server if SteamVR is not running. The box shows SteamVR's own setting while SteamVR runs; otherwise it shows whether the manifest is there.

## Companion app

### process

```bash
python -m ftrecorder process --video clip.mp4 --out clip.mkv
```

Runs a recorded video through the face models into one Matroska file, like the Recorder's: the video itself (copied unchanged), its sound as audio (Opus, 96 kb/s), the visemes as a subtitle track, and the whole recording (face data and visemes) attached. Until the `.mkv` is written the data is kept in `clip.mkv.recording.ftrec.jsonl`; it is deleted once the `.mkv` is written, kept if not. Its visemes depend on whether VRChat is running:

- **VRChat running (Windows):** the video's sound is played into VRChat through a virtual audio cable (`--cable NAME`, default `CABLE Input`), and the `Viseme` and `Voice` VRChat sends back over OSC are recorded. Turn OSC on in VRChat and set its microphone to the cable's output. This runs at the video's own speed, after a 5 second wait for VRChat to find the app.
- **VRChat not running (Windows):** the sound goes through OVRLipSync, taken from `--ovrlipsync PATH`, else from your VRChat install (found through Steam, in `VRChat_Data\Plugins`). When it cannot be found or loaded, the app shows the error and asks for the path; an empty answer stops.
- **Linux:** no visemes unless `--visemes classifier` is given (it says so).

| Option | Default | Use |
|---|---|---|
| `--ffmpeg PATH` | `ffmpeg` | ffmpeg's path. |
| `--babble-model PATH`, `--babble-labels PATH`, `--babble-crop SCALE` | none, bundled labels, `1` | A Babble model for the mouth. |
| `--visemes ovrlipsync\|classifier` | Windows: VRChat if running, else `ovrlipsync`. Linux: none | Uses that engine whether VRChat runs or not. `classifier` is FTRecorder's own rule-based classifier (less exact). |
| `--ovrlipsync PATH` | VRChat's copy | The OVRLipSync library. |
| `--cable NAME` | `CABLE Input` | The playback device VRChat hears, matched by name. |
| `--fps N`, `--size N`, `--threads N` | `30`, `640`, half the cores | Frame rate, frame size and model threads. |

### replay

```bash
python -m ftrecorder replay --in session.mkv --port 9000 --loop
```

Sends recorded OSC packets, VRChat parameters and visemes to `127.0.0.1:PORT` at their recorded times. Takes `.mkv` (read through ffmpeg, `--ffmpeg PATH`) and `.ftrec.jsonl` recordings. Expression frames are played by the module's `playback` source instead. VRChat ignores `Viseme` and `Voice` sent to it (it makes its own); FTEmulator in Unity takes them.

## Recorder or module?

Both write one `.mkv` with the recording attached, but they record different things, at different points:

| | Recorder (companion app) | Module (`record` setting) |
|---|---|---|
| What it records | VRCFaceTracking's OSC output: the packets VRChat (or FTEmulator) receives, after VRCFaceTracking's calibration and settings, as the avatar's own parameters. | The Unified Expressions and eyes the module hands VRCFaceTracking, before VRCFaceTracking changes them, for no avatar in particular. |
| Which tracker | Any: whatever module VRCFaceTracking is running (Quest Pro, Babble, this module...). | Only this module's own source (playback, video, webcam or babbleOsc). |
| Visemes | Yes: VRChat's `Viseme` and `Voice` while VRChat runs; otherwise, on Windows, OVRLipSync on the microphone. | No. |
| Audio | The microphone, with **Record audio**. | No. |
| `.mkv` holds | Audio (when kept), the "Visemes" subtitle track and the recording. | The recording, with 20 ms of silence as its track. |
| Recording entries | `osc` (and `param` or `viseme`). | `expressions`. |
| Played back with | `replay`, to VRChat or FTEmulator (sends the packets and visemes as recorded, so it suits the avatar it was recorded with). | The module's `playback` source, through VRCFaceTracking again (so any avatar, with VRCFaceTracking's current settings). |
| Setup | VRCFaceTracking's OSC output port set to `9010`, and the Recorder open (it relays to VRChat on `9000`). | None beyond the module: `record` in `module-config.json`. |
| Started and stopped by | **Start recording** / **Stop and save**. | Setting `record` to `true` / `false` (or VRCFaceTracking closing). |
| File name | `FTRecording-HHmm-ddMMyy.mkv` in **Output**. | `module_<date>_<time>.mkv` in `recordingsFolder`. |
| Runs on | Windows and Linux. | Wherever VRCFaceTracking runs it (the release zip is for Windows). |
| ffmpeg | Needed: a popup when missing, and nothing is recorded. | Needed: logged when missing, and nothing is recorded. |

`process` records neither: it turns a video file into a recording with `expressions` (like the module, from MediaPipe and Babble) and visemes, in an `.mkv` with the video and its sound, played back with the module's `playback` source (face) and `replay` (visemes).

## Recordings

The Recorder and `process` write `.mkv` (Matroska), one file with:

- **Audio:** for the Recorder with **Record audio**, your microphone (Opus at 48 kb/s, starting where the microphone started in the recording); for `process`, the video's sound (Opus at 96 kb/s). A recording with no picture, sound or visemes gets 20 ms of silence, because a Matroska file needs a track.
- **Video:** for `process`, the video's picture, copied unchanged.
- **Subtitles ("Visemes"):** one caption per change, holding the viseme's index and name, Voice, and the 15 weights when OVRLipSync or the classifier made them. Any player that shows subtitles (VLC, mpv) shows them with the audio.
- **Attachment `recording.ftrec.jsonl`:** the whole recording, in the format below. `ffmpeg -dump_attachment:t:0 recording.ftrec.jsonl -i session.mkv -t 0 -f null -` copies it out.

The module's `record` setting writes `.mkv` too, holding only the attached recording (and 20 ms of silence, the track a Matroska file needs).

`.ftrec.jsonl`: one JSON object per line. The first line is the header (`format`, `version`, `source`, `created`, and the `unified` and `eye` value names). Each following line is one entry with its time `t` in seconds:

- `{"t":..,"unified":[..],"eye":[..]}`: Unified Expressions and eyes (module recordings and processed videos).
- `{"t":..,"osc":"<base64>"}`: one OSC packet VRCFaceTracking sent, unchanged.
- `{"t":..,"param":"Viseme","i":n}` / `{"t":..,"param":"Voice","f":v}`: what VRChat sent (the Recorder, and `process` with VRChat running).
- `{"t":..,"viseme":n,"voice":v,"weights":[15]}`: a viseme made by OVRLipSync or the classifier, in VRChat's viseme order (sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou).

## Licences

MIT for this repository's code. The bundled MediaPipe models are Apache-2.0, ONNX Runtime is MIT, and Valve's OpenVR (`openvr_api.dll`) is BSD 3-Clause; see [NOTICE.md](https://github.com/Rionquosue/VRCFT-FtEmuCompanion/blob/main/NOTICE.md). Babble's model, OVRLipSync and ffmpeg are not included: they are loaded from your own copies, under their own licences. Python, numpy and onnxruntime are installed by you.
