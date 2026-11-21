# VRCFT-FtEmuCompanion

The README of [Rionquosue/VRCFT-FtEmuCompanion](https://github.com/Rionquosue/VRCFT-FtEmuCompanion), FTEmulator's VRCFaceTracking companion. Paths below are in that repository.

Companion to RiryTools FTEmulator: a VRCFaceTracking module, a Recorder app and a command-line app that record face tracking and visemes, and turn recorded videos into face tracking and visemes. The Unity side (FTEmulator) is the `com.riry.ftemulator` package in the [RiryTools VPM listing](https://rionquosue.github.io/rirytools_vpm/).

- **Module** (`RiryTools FTRecorder`): feeds VRCFaceTracking from a recording, a video file, a webcam or the Babble app, and records everything it sends.
- **Recorder** (`Recorder.pyw`, Python): a window that records every avatar parameter VRChat sends over OSC (found through OSCQuery), whatever sends the face tracking to VRChat. It can start with SteamVR.
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
- For `process` on Windows, VRChat installed through Steam for OVRLipSync (VRChat's viseme engine), or the path to an `OVRLipSync.dll`. OVRLipSync is a Windows DLL: on Linux, `process` takes visemes only from a running VRChat or `--visemes classifier`.
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

Open it by double-clicking `Recorder.pyw` (Windows), or with `python Recorder.pyw` or `python -m ftrecorder recorder` from this folder. Turn OSC on in VRChat. Nothing sits between your tracking and VRChat: VRCFaceTracking, BoltOn or any other app sends to VRChat as usual (VRCFaceTracking on its default port, `9000`).

**Start recording** checks for ffmpeg, then for VRChat: when ffmpeg is not found it shows an "FFmpeg not found" popup, and when VRChat is not running a "VRChat is not running!" popup, and does nothing else. Otherwise the Recorder announces itself over OSCQuery; VRChat finds it and sends it every avatar parameter as it changes (face tracking, eyes, `Viseme`, `Voice` and the avatar's own), and each is recorded with its time. These are the values VRChat holds for the current avatar, from whichever apps set them. It records until **Stop and save**, keeping the values next to the output (`NAME.mkv.recording.ftrec.jsonl`) so a crash keeps them; on stop they go into one Matroska file and the working file is deleted. If writing the `.mkv` fails, it is kept and the log says where. Closing the window while recording stops and saves first.

| Field | Default | Use |
|---|---|---|
| Output | `Documents\RiryTools FTRecorder` | The folder recordings go to (created when needed). Each is named `FTRecording-HHmm-ddMMyy.mkv` from when it starts, 24-hour time and day, month, year without separators: `FTRecording-1405-201126.mkv`. Another started in the same minute gets ` (2)`, then ` (3)`, and so on. |
| ffmpeg | `ffmpeg` | ffmpeg's path. |
| Start with SteamVR | off | SteamVR opens the Recorder when it starts (recording still waits for Start recording). |

The log shows what is happening and every error; while recording, the line next to the button counts the VRChat values recorded.

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

## What the module and the application record

| | Records |
|---|---|
| **Module** (`record` setting) | Face tracking: Unified Expressions and eyes <br> Only from the module's own source (playback, video, webcam, Babble app) <br> As VRCFaceTracking receives it, before calibration, for any avatar <br> One `.mkv` with the recording attached |
| **Application** (Recorder) | Every avatar parameter VRChat sends over OSC: face tracking, eyes, `Viseme`, `Voice` and the avatar's own <br> From any app that sends to VRChat (VRCFaceTracking, BoltOn, others) <br> As VRChat holds them, for the current avatar <br> Needs VRChat running <br> One `.mkv` with the recording attached |

## Recordings

The Recorder and `process` write `.mkv` (Matroska), one file with:

- **Audio:** for `process`, the video's sound (Opus at 96 kb/s); the Recorder records none. A recording with no picture, sound or visemes gets 20 ms of silence, because a Matroska file needs a track.
- **Video:** for `process`, the video's picture, copied unchanged.
- **Subtitles ("Visemes"):** one caption per change, holding the viseme's index and name, Voice, and the 15 weights when OVRLipSync or the classifier made them. Any player that shows subtitles (VLC, mpv) shows them with the audio.
- **Attachment `recording.ftrec.jsonl`:** the whole recording, in the format below. `ffmpeg -dump_attachment:t:0 recording.ftrec.jsonl -i session.mkv -t 0 -f null -` copies it out.

The module's `record` setting writes `.mkv` too, holding only the attached recording (and 20 ms of silence, the track a Matroska file needs).

`.ftrec.jsonl`: one JSON object per line. The first line is the header (`format`, `version`, `source`, `created`, and the `unified` and `eye` value names). Each following line is one entry with its time `t` in seconds:

- `{"t":..,"unified":[..],"eye":[..]}`: Unified Expressions and eyes (module recordings and processed videos).
- `{"t":..,"osc":"<base64>"}`: one OSC packet, unchanged (Recorder recordings made before it recorded through OSCQuery).
- `{"t":..,"param":NAME,"i":n}` / `"f":v` / `"b":true|false`: one avatar parameter VRChat sent, named without `/avatar/parameters/` (every parameter for the Recorder; `Viseme` and `Voice` for `process` with VRChat running).
- `{"t":..,"viseme":n,"voice":v,"weights":[15]}`: a viseme made by OVRLipSync or the classifier, in VRChat's viseme order (sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou).

## Licences

MIT for this repository's code. The bundled MediaPipe models are Apache-2.0, ONNX Runtime is MIT, and Valve's OpenVR (`openvr_api.dll`) is BSD 3-Clause; see [NOTICE.md](https://github.com/Rionquosue/VRCFT-FtEmuCompanion/blob/main/NOTICE.md). Babble's model, OVRLipSync and ffmpeg are not included: they are loaded from your own copies, under their own licences. Python, numpy and onnxruntime are installed by you.
