# VRCFT-FtEmuCompanion

The README of [Rionquosue/VRCFT-FtEmuCompanion](https://github.com/Rionquosue/VRCFT-FtEmuCompanion), FTEmulator's VRCFaceTracking companion. Paths below are in that repository.

Companion to RiryTools FTEmulator: a VRCFaceTracking module, a Recorder app and a command-line app that record face tracking and visemes, and turn recorded videos into face tracking and visemes. The Unity side (FTEmulator) is the `com.riry.ftemulator` package in the [RiryTools VPM listing](https://rionquosue.github.io/rirytools_vpm/).

- **Module** (`RiryTools FTRecorder`): feeds VRCFaceTracking from a recording, a video file, a webcam or the Babble app, and records everything it sends.
- **Recorder** (`RiryTools.FTRecorder.Recorder`): a window that records VRCFaceTracking's output (what FTEmulator in Unity receives) together with the visemes and your microphone.
- **Companion app** (`RiryTools.FTRecorder`): processes videos and replays recordings, from the command line.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build.
- [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) for the module.
- [ffmpeg](https://ffmpeg.org/) on `PATH` (or its path set) for recording, processing, `.mkv` recordings, video files and webcams. Every command and module source that needs it checks first and says how to install it when it is missing.
- VRChat installed through Steam for OVRLipSync (VRChat's viseme engine), or the path to an `OVRLipSync.dll`.
- To take visemes from a running VRChat when processing videos: a virtual audio cable (such as VB-Cable), with VRChat's microphone set to the cable's output.
- Optional: a Babble face model (`.onnx`) you already have, for the mouth.

## Build

Module zip (Windows):

```bash
dotnet publish FTRecorder.Module -c Release -r win-x64 -p:VrcftDir="C:\path\to\folder with VRCFaceTracking.Core.dll and VRCFaceTracking.SDK.dll"
```

The zip is written to `FTRecorder.Module/bin/RiryTools.FTRecorder.Module-win-x64.zip`. Install it in VRCFaceTracking's Module Registry as a local .zip module.

Recorder:

```bash
dotnet publish FTRecorder.Recorder -c Release -r win-x64
```

Companion app:

```bash
dotnet publish FTRecorder.Companion -c Release -r win-x64
```

## Module

Settings are in `%LocalAppData%\RiryTools\FTRecorder\module-config.json`, written with defaults on the first start. Changes apply while VRCFaceTracking runs.

| Setting | Default | Use |
|---|---|---|
| `source` | `webcam` | `playback`, `video`, `webcam` or `babbleOsc`. |
| `record` | `false` | While `true`, everything the module sends is written to a recording. |
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

Start `RiryTools.FTRecorder.Recorder`, set the fields, press **Start recording**, and **Stop and save** when done. It records VRCFaceTracking's output, visemes and your microphone into one Matroska file. While it runs, the parts are kept next to the output (`NAME.mkv.recording.ftrec.jsonl` and `NAME.mkv.microphone.s16le`), so a crash keeps them; on stop they are combined into the `.mkv` and deleted. If writing the `.mkv` fails, they are kept and the log says where. Closing the window while recording stops and saves first.

| Field | Default | Use |
|---|---|---|
| Recording (.mkv) | `Documents\RiryTools FTRecorder\recording-<time>.mkv` | Where the recording goes. A new name is filled in after each save. |
| VRCFaceTracking OSC output port | `9010` | Set VRCFaceTracking's OSC output port to this. Every packet is passed on and recorded. |
| Pass on to port | `9000` | Where the packets go on to: VRChat's OSC input port. |
| Microphone | the first one ffmpeg lists | The microphones `ffmpeg -list_devices true -f dshow -i dummy` lists. |
| ffmpeg | `ffmpeg` | ffmpeg's path. |
| OVRLipSync.dll | VRChat's copy | The OVRLipSync library, used while VRChat is not running. |

Visemes depend on whether VRChat is running when the recording starts:

- **VRChat running:** turn OSC on in VRChat. It finds the Recorder through OSCQuery and sends it `Viseme` and `Voice`, which are recorded.
- **VRChat not running:** the microphone goes through OVRLipSync, from the field, else from your VRChat install (found through Steam, in `VRChat_Data\Plugins`). When it cannot be found or loaded, the log shows the error and a file picker asks for `OVRLipSync.dll`; cancelling it stops the start.

The log shows what is happening and every error; the line next to the button counts packets, VRChat viseme values and seconds of microphone.

## Companion app

### process

```bash
RiryTools.FTRecorder process --video clip.mp4 --out clip.mkv
```

Runs a recorded video through the face models into one Matroska file, like the Recorder's: the video itself (copied unchanged), its sound as audio (Opus, 96 kb/s), the visemes as a subtitle track, and the whole recording (face data and visemes) attached. Until the `.mkv` is written the data is kept in `clip.mkv.recording.ftrec.jsonl`; it is deleted once the `.mkv` is written, kept if not. Its visemes depend on whether VRChat is running:

- **VRChat running:** the video's sound is played into VRChat through a virtual audio cable (`--cable NAME`, default `CABLE Input`), and the `Viseme` and `Voice` VRChat sends back over OSC are recorded. Turn OSC on in VRChat and set its microphone to the cable's output. This runs at the video's own speed, after a 5 second wait for VRChat to find the app.
- **VRChat not running:** the sound goes through OVRLipSync, taken from `--ovrlipsync PATH`, else from your VRChat install (found through Steam, in `VRChat_Data\Plugins`). When it cannot be found or loaded, the app shows the error and asks for the path; an empty answer stops.

| Option | Default | Use |
|---|---|---|
| `--ffmpeg PATH` | `ffmpeg` | ffmpeg's path. |
| `--babble-model PATH`, `--babble-labels PATH`, `--babble-crop SCALE` | none, bundled labels, `1` | A Babble model for the mouth. |
| `--visemes ovrlipsync\|classifier` | VRChat if running, else `ovrlipsync` | Uses that engine whether VRChat runs or not. `classifier` is FTRecorder's own rule-based classifier (less exact). |
| `--ovrlipsync PATH` | VRChat's copy | The OVRLipSync library. |
| `--cable NAME` | `CABLE Input` | The playback device VRChat hears, matched by name. |
| `--fps N`, `--size N`, `--threads N` | `30`, `640`, half the cores | Frame rate, frame size and model threads. |

### replay

```bash
RiryTools.FTRecorder replay --in session.mkv --port 9000 --loop
```

Sends recorded OSC packets, VRChat parameters and visemes to `127.0.0.1:PORT` at their recorded times. Takes `.mkv` (read through ffmpeg, `--ffmpeg PATH`) and `.ftrec.jsonl` recordings. Expression frames are played by the module's `playback` source instead. VRChat ignores `Viseme` and `Voice` sent to it (it makes its own); FTEmulator in Unity takes them.

## Recordings

The Recorder and `process` write `.mkv` (Matroska), one file with:

- **Audio:** for the Recorder, your microphone (Opus at 48 kb/s, starting where the microphone started in the recording); for `process`, the video's sound (Opus at 96 kb/s). A recording with no picture, sound or visemes gets 20 ms of silence, because a Matroska file needs a track.
- **Video:** for `process`, the video's picture, copied unchanged.
- **Subtitles ("Visemes"):** one caption per change, holding the viseme's index and name, Voice, and the 15 weights when OVRLipSync made them. Any player that shows subtitles (VLC, mpv) shows them with the audio.
- **Attachment `recording.ftrec.jsonl`:** the whole recording, in the format below. `ffmpeg -dump_attachment:t:0 recording.ftrec.jsonl -i session.mkv -t 0 -f null -` copies it out.

The module's `record` setting writes `.ftrec.jsonl` directly.

`.ftrec.jsonl`: one JSON object per line. The first line is the header (`format`, `version`, `source`, `created`, and the `unified` and `eye` value names). Each following line is one entry with its time `t` in seconds:

- `{"t":..,"unified":[..],"eye":[..]}`: Unified Expressions and eyes (module recordings and processed videos).
- `{"t":..,"osc":"<base64>"}`: one OSC packet VRCFaceTracking sent, unchanged.
- `{"t":..,"param":"Viseme","i":n}` / `{"t":..,"param":"Voice","f":v}`: what VRChat sent (the Recorder, and `process` with VRChat running).
- `{"t":..,"viseme":n,"voice":v,"weights":[15]}`: a viseme made by OVRLipSync or the classifier, in VRChat's viseme order (sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou).

## Licences

MIT for this repository's code. The bundled MediaPipe models are Apache-2.0, ONNX Runtime is MIT, and Avalonia (the Recorder's window) is MIT; see [NOTICE.md](https://github.com/Rionquosue/VRCFT-FtEmuCompanion/blob/main/NOTICE.md). Babble's model, OVRLipSync and ffmpeg are not included: they are loaded from your own copies, under their own licences.
