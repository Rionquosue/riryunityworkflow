# Data Models

All data models use JSON with `JsonUtility` (no Newtonsoft). Asset references are kept as GUIDs; scene objects use `SceneObjectId` strings.

## FTEmulator

### Avatar Profile (`*.json`)

Location: `com.riry.ftemulator/Editor/FTEmulator/AvatarProfiles/`

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `schema` | int | Yes | Version of the profile format (currently 1). |
| `id` | string | Yes | Unique identifier (e.g. `"vrchat-native"`, `"adjerry-legacy"`, `"pawlygon-legacy"`, `"pawlygon-native"`, `"vrcft-unified-v2"`). |
| `kind` | string | Yes | `"template"` or `"parameters"`. |
| `name` | string | Yes | Display name. |
| `backend` | string | No | `"native"` or `"legacy"`. |
| `version` | string | No | SDK or template version. |
| `evidence` | string[] | No | Source URLs or notes. |
| `notes` | string[] | No | Implementation notes. |
| `signatures` | object | No | Parameter prefixes and package names for legacy templates. |
| `parameterSet` | string | No | The parameter set this template uses (e.g. `"vrcft-unified-v2"`). |
| `excludePrefixes` | string[] | No | Parameter prefixes to exclude from matching. |
| `status` | Status[] | No | Tracking status flags (eye/face/lip active). |
| `parameters` | Parameter[] | No | Parameter definitions for `"parameters"` kind. |

#### JSON Example

`signatures` object (from `pawlygon-legacy.json`):

```json
{
  "signatures": {
    "parameterPrefixes": [
      "OSCm/Binary/FT/v2/",
      "OSCm/Local/Smooth/FT/v2/",
      "OSCm/Remote/Smooth/FT/v2/"
    ],
    "packages": [
      {
        "name": "net.pawlygon.vrc-facetracking",
        "maxVersion": "1.999.999"
      }
    ]
  }
}
```

| Field | Type | Notes |
| --- | --- | --- |
| `parameterPrefixes` | string[] | Parameter name prefixes that identify this template's output. |
| `componentTypes` | string[] | Component type names that identify this template. |
| `packages` | Package[] | Package name/version constraints. |

#### Package

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Package name (e.g. `"net.pawlygon.vrc-facetracking"`). |
| `minVersion` | string | Lowest matching package version. |
| `maxVersion` | string | Highest matching package version. |

#### Status

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Bool parameter the emulator owns (e.g. `"EyeTrackingActive"`). |
| `region` | string | `"eye"` or `"face"`: true while that region is tracked. |

#### Parameter

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Parameter name (e.g. `"v2/EyeX"`, `"v2/JawOpen"`). |
| `value` | string | Formula or direct source value. |
| `min` | float | Minimum value constraint. |
| `max` | float | Maximum value constraint. |
| `boolBelow` | float | If present, the parameter is a bool: true while value < boolBelow. |

#### Example Avatar Profile

Template (`pawlygon-legacy.json`):

```json
{
  "schema": 1,
  "id": "pawlygon-legacy",
  "kind": "template",
  "name": "Pawlygon 1.x",
  "backend": "legacy",
  "version": "1.x",
  "parameterSet": "vrcft-unified-v2",
  "evidence": [
    "S9 PawlygonStudio/VRC-Facetracking, Deprecated/Animators (1.x controllers), parameter prefixes read 2026-11-16"
  ],
  "notes": [
    "Raw inputs are FT/v2/...; OSCm/... parameters are the template's own smoothing and binary decoding."
  ],
  "signatures": {
    "parameterPrefixes": [
      "OSCm/Binary/FT/v2/",
      "OSCm/Local/Smooth/FT/v2/",
      "OSCm/Remote/Smooth/FT/v2/"
    ],
    "packages": [
      {
        "name": "net.pawlygon.vrc-facetracking",
        "maxVersion": "1.999.999"
      }
    ]
  }
}
```

Parameter set (`vrcft-unified-v2.json`, shortened):

```json
{
  "schema": 1,
  "id": "vrcft-unified-v2",
  "kind": "parameters",
  "name": "VRCFT Unified Expressions v2",
  "version": "5.x",
  "excludePrefixes": ["OSCm/", "BinaryOut/"],
  "status": [
    { "name": "EyeTrackingActive", "region": "eye" },
    { "name": "ExpressionTrackingActive", "region": "face" },
    { "name": "LipTrackingActive", "region": "face" }
  ],
  "parameters": [
    {
      "name": "v2/EyeX",
      "value": "((EyeLookInLeft - EyeLookOutLeft) + (EyeLookOutRight - EyeLookInRight)) / 2",
      "min": -1
    },
    {
      "name": "v2/EyeSquintRight",
      "value": "EyeSquintRight",
      "boolBelow": 0
    }
  ]
}
```

### Hardware Profile (`*.json`)

Location: `com.riry.ftemulator/Editor/FTEmulator/HardwareProfiles/`

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `schema` | int | Yes | Version of the profile format (currently 1). |
| `id` | string | Yes | Unique identifier (e.g. `"quest-pro-base-fb1"`, `"unrestricted"`, `"vive-pro-eye"`). |
| `name` | string | Yes | Display name. |
| `detail` | string | No | Short description. |
| `version` | string | Yes | Profile version. |
| `order` | int | Yes | Sort order for UI. |
| `eyeTracking` | bool | Yes | Whether the hardware provides eye tracking. |
| `faceTracking` | bool | Yes | Whether the hardware provides face tracking. |
| `evidence` | string[] | No | Source URLs or notes. |
| `notes` | string[] | No | Implementation notes. |
| `unrestricted` | bool | No | If true, no hardware emulation is applied. |
| `controls` | Control[] | No | Input controls (mapping from hardware to internal keys). |
| `outputs` | Output[] | No | Final tracking keys (mapping to VRChat SDK keys). |

#### Control

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Internal control name. |
| `group` | string | Category (e.g. `"Brow"`, `"Mouth"`, `"Lips"`). |
| `from` | string | Source: a hardware key, a formula, or a reference. |
| `min` | float | Minimum value constraint. |
| `max` | float | Maximum value constraint (default 1). |
| `neutral` | float | Neutral/zero point value. |

#### Output

| Field | Type | Notes |
| --- | --- | --- |
| `key` | string | VRChat SDK tracking key (e.g. `"EyeLookOutLeft"`). |
| `value` | string | Source: a control name, a formula, or a reference. |
| `derived` | bool | If true, the output is computed from other outputs. |
| `note` | string | Implementation note. |

#### Example Hardware Profile

`quest-pro-base-fb1.json`, shortened (the `min`/`neutral` control is from `vive-pro-eye-plus-facial.json`):

```json
{
  "schema": 1,
  "id": "quest-pro-base-fb1",
  "name": "Base Quest Pro",
  "detail": "FB1 63-channel baseline",
  "version": "1",
  "order": 10,
  "eyeTracking": true,
  "faceTracking": true,
  "evidence": [
    "H1 Meta XR_FB_face_tracking expression set (OculusXRMovementTypes, v63): 63 expressions"
  ],
  "notes": [
    "No tongue, pupil, jaw backward, jaw clench, throat or neck channels."
  ],
  "controls": [
    {
      "name": "BrowLowererL",
      "group": "Brow",
      "from": "BrowLowererLeft"
    },
    {
      "name": "LeftEyeOpenness",
      "group": "Eyelid",
      "from": "1 - EyeClosedLeft",
      "neutral": 1
    }
  ],
  "outputs": [
    {
      "key": "BrowPinchLeft",
      "value": "BrowLowererL",
      "derived": true,
      "note": "Movement has no brow pinch; follows BrowLowerer."
    }
  ]
}
```

### Mapping Schema (`*.json`)

Location: `com.riry.ftemulator/Editor/FTEmulator/MappingSchemas/`

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `schema` | int | Yes | Version of the schema format (currently 1). |
| `id` | string | Yes | Unique identifier (e.g. `"vrchat-face-v1"`). |
| `name` | string | Yes | Display name. |
| `address` | string | Yes | OSC or API path (e.g. `"/tracking/face/v1"`). |
| `blobLength` | int | No | Expected payload size in bytes. |
| `evidence` | string[] | No | Source URLs or notes. |
| `keys` | Key[] | Yes | List of tracking keys. |

#### Key

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | VRChat SDK tracking key name. |
| `region` | string | Anatomical region (e.g. `"eye"`, `"face"`). |

#### Example Mapping Schema

`unified-expressions.json`, shortened:

```json
{
  "schema": 1,
  "id": "vrchat-face-v1",
  "name": "VRChat native face tracking keys",
  "address": "/tracking/face/v1",
  "blobLength": 102,
  "evidence": [
    "https://creators.vrchat.com/avatars/face-tracking/osc/ (tracking key order, read 2026-11-16)"
  ],
  "keys": [
    { "name": "EyeLookOutRight", "region": "eye" },
    { "name": "EyeLookInRight", "region": "eye" }
  ]
}
```

## RiryTools

### Blendshape Sync Job

Location: Serialized in `BlendshapeSyncJobs.cs` (Unity scene).

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Master` | string | Yes | `SceneObjectId` of the master skinned mesh renderer. |
| `Name` | string | Yes | User-provided name (empty for default). |
| `Children` | SavedChild[] | Yes | List of child renderers. |
| `Auto` | bool | Yes | Run automatically. |
| `CaseSensitive` | bool | Yes | Case-sensitive matching. |

#### SavedChild

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Renderer` | string | Yes | `SceneObjectId` of the child skinned mesh renderer. |
| `Links` | ManualLink[] | Yes | Source→Destination mappings. |

#### ManualLink

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Source` | string | Yes | Source blendshape key. |
| `Destination` | string | Yes | Destination blendshape key. |

### IconCap Job File (`.iconcap.json`)

Location: Hidden file in each IconCap export folder.

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Jobs` | IconCapJob[] | Yes | List of named job groups. |

#### IconCapJob

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Name` | string | Yes | Job group name. |
| `Entries` | IconCapEntry[] | Yes | List of captures. |

#### IconCapEntry

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `FileName` | string | Yes | Base filename of the capture. |
| `SavedPath` | string | No | Full path after export (empty until exported). |
| `Settings` | IconCapSettings | Yes | Capture settings. |
| `Objects` | IconCapObjects | Yes | What was captured. |

#### IconCapSettings

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Size` | int | Yes | Icon size in pixels (default 512). |
| `Camera` | IconCapCamera | Yes | Camera view and settings. |
| `Background` | IconCapBackgroundSettings | Yes | Background mode and color. |
| `Options` | IconCapOptions | Yes | Lighting and exclusion flags. |
| `Effects` | IconCapEffect[] | Yes | Effect stack. |

#### IconCapCamera

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Position` | Vector3 | Yes | Camera position at capture time. |
| `Rotation` | Quaternion | Yes | Camera rotation at capture time. |
| `Orthographic` | bool | Yes | Whether the camera is orthographic. |
| `OrthographicSize` | float | Yes | Orthographic size. |
| `SceneFov` | float | Yes | Scene View FOV at capture time. |
| `NearClip` | float | Yes | Near clip plane. |
| `FovMode` | enum | Yes | `SceneView`, `Custom`, `Lens35/50/85`. |
| `Fov` | float | Yes | Custom FOV (used when `FovMode` is `Custom`). |
| `FarClip` | float | Yes | Far clip plane. |
| `Roll` | float | Yes | Camera roll. |

#### IconCapBackgroundSettings

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Mode` | enum | Yes | `Transparent`, `Opaque`, `TransparentWithBackground`. |
| `Colour` | Color | Yes | Background color (used when `Mode` is `Opaque` or `TransparentWithBackground`). |
| `Skybox` | bool | No | If true, use the scene skybox instead of `Colour` (only for `Opaque`). |

#### IconCapOptions

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `AlphaCutout` | bool | Yes | Enable alpha cutout. |
| `Lighting` | enum | Yes | `Scene`, `Hdri`, or `Flat`. |
| `Hdri` | string | No | GUID of the HDRI cubemap. |
| `ExcludeGlobal` | bool | Yes | Exclude global lights. |
| `ExcludeScene` | bool | Yes | Exclude scene lights. |
| `ExcludeWorld` | bool | Yes | Exclude world lights. |
| `ExcludeReflections` | bool | Yes | Exclude reflections. |
| `ExcludeBaked` | bool | Yes | Exclude baked lights. |
| `ExcludeShadows` | bool | Yes | Exclude shadows. |

#### IconCapObjects

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `OnlySelected` | bool | Yes | If true, only selected renderers are captured. |
| `Renderers` | string[] | Yes | List of `SceneObjectId` strings (used when `OnlySelected` is false). |
| `Isolated` | string | No | Single `SceneObjectId` for isolated capture. |

#### IconCapEffect

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Type` | enum | Yes | Effect type (e.g. `Overlay`, `Mask`, `Adjustments`). |
| `Enabled` | bool | Yes | Whether the effect is active. |
| `Textures` | string[] | No | GUIDs of texture assets (cycled across an icon list). |
| `Tint` | Color | Yes | Color tint for textures. |
| `Position` | Vector2 | Yes | Position (pixels of the final image, x right, y down). |
| `Scale` | float | Yes | Scale (1 fills the icon). |
| `Corner` | enum | Yes | `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`. |
| `Material` | string | No | GUID of a custom material. |
| `Zoom` | float | Yes | Zoom factor. |
| `Centre` | Vector2 | Yes | Center point (0.5, 0.5). |
| `Angle` | float | Yes | Rotation angle. |
| `FlipX` | bool | Yes | Flip horizontally. |
| `FlipY` | bool | Yes | Flip vertically. |
| `Padding` | float | Yes | Padding around the effect. |
| `Brightness` | float | Yes | Brightness adjustment. |
| `Contrast` | float | Yes | Contrast multiplier. |
| `Saturation` | float | Yes | Saturation multiplier. |
| `Colour` | Color | Yes | Color for outline, drop shadow, or overlay. |
| `Thickness` | float | Yes | Outline thickness. |
| `Softness` | float | Yes | Outline softness. |
| `Offset` | Vector2 | Yes | Outline/drop shadow offset. |
| `Blur` | float | Yes | Blur amount. |
| `Hue` | float | Yes | Hue shift (degrees). |
| `Levels` | int | Yes | Number of levels for filters. |
| `Amount` | float | Yes | Filter strength (also darkening for `GrayOut`). |

#### Example IconCap Job File

Written by `JsonUtility`, so enums are stored as numbers and every field is present (values shown are the defaults; GUIDs and IDs are placeholders):

```json
{
  "Jobs": [
    {
      "Name": "Outfit icons",
      "Entries": [
        {
          "FileName": "Hoodie",
          "SavedPath": "",
          "Settings": {
            "Size": 512,
            "Camera": {
              "Position": { "x": 0.0, "y": 1.4, "z": 0.8 },
              "Rotation": { "x": 0.0, "y": 1.0, "z": 0.0, "w": 0.0 },
              "Orthographic": false,
              "OrthographicSize": 1.0,
              "SceneFov": 60.0,
              "NearClip": 0.03,
              "FovMode": 0,
              "Fov": 30.0,
              "FarClip": 1000.0,
              "Roll": 0.0
            },
            "Background": {
              "Mode": 0,
              "Colour": { "r": 0.2, "g": 0.2, "b": 0.2, "a": 1.0 },
              "Skybox": false
            },
            "Options": {
              "AlphaCutout": true,
              "Lighting": 0,
              "Hdri": "",
              "ExcludeGlobal": false,
              "ExcludeScene": false,
              "ExcludeWorld": false,
              "ExcludeReflections": false,
              "ExcludeBaked": false,
              "ExcludeShadows": false
            },
            "Effects": [
              {
                "Type": 0,
                "Enabled": true,
                "Textures": ["0123456789abcdef0123456789abcdef"],
                "Tint": { "r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0 },
                "Position": { "x": 0.0, "y": 0.0 },
                "Scale": 1.0,
                "Corner": 1,
                "Material": "",
                "Zoom": 1.0,
                "Centre": { "x": 0.5, "y": 0.5 },
                "Angle": 0.0,
                "FlipX": false,
                "FlipY": false,
                "Padding": 0.1,
                "Brightness": 0.0,
                "Contrast": 1.0,
                "Saturation": 1.0,
                "Colour": { "r": 0.0, "g": 0.0, "b": 0.0, "a": 1.0 },
                "Thickness": 4.0,
                "Softness": 0.2,
                "Offset": { "x": 4.0, "y": 4.0 },
                "Blur": 4.0,
                "Hue": 35.0,
                "Levels": 6,
                "Amount": 1.0
              }
            ]
          },
          "Objects": {
            "OnlySelected": false,
            "Renderers": ["<SceneObjectId>"],
            "Isolated": ""
          }
        }
      ]
    }
  ]
}
```

### AutoSync Job

Location: Serialized in `AutoSync.cs` (Unity scene).

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Title` | string | Yes | Timestamped save title. |
| `Paths` | string[] | Yes | List of asset paths saved in this pass. |

### Avatar Migrator Job

Location: Serialized in `AvatarMigratorYaml.cs` (Unity scene).

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Master` | string | Yes | `SceneObjectId` of the master avatar. |
| `Children` | SavedChild[] | Yes | List of child renderers. |
| `Auto` | bool | Yes | Run automatically. |
| `CaseSensitive` | bool | Yes | Case-sensitive matching. |

#### SavedChild

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `Renderer` | string | Yes | `SceneObjectId` of the child skinned mesh renderer. |
| `Links` | ManualLink[] | Yes | Source→Destination mappings. |

## Notes

- All GUIDs are Unity asset GUIDs, resolved via `AssetDatabase.GUIDToAssetPath`.
- `SceneObjectId` strings are Unity's internal scene object identifiers, resolved via `SceneObjectId.Find<T>()`.
- Job files are written once per session and read on scene reload; they are never modified in place.
- Effect textures and materials are referenced by GUID to survive scene reloads and job file migrations.
