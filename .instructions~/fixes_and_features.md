# This file contains bugfixes and feature requests.

These are organized by tool, with a sub section for features and bugs. Once these bugs are fixed or removed, please remove the associated line.

Please reference "filestructure.md" for where each tool is located (Tool names table and Script locations).

### Main menu

#### Bugs

#### Features

### Animator Tools

#### Bugs

#### Features
    
### AutoSync

#### Bugs

#### Features
    
### Avatar Migration Assistant

#### Bugs

#### Features
    
### Blendshape Sync

#### Bugs

#### Features
    
### Clean Animation Keys

#### Bugs

#### Features
    
### Constraint Tool

#### Bugs

#### Features
    
### GUID Tools

#### Bugs



#### Features

1. Give the user an "Ignore" button on the right that will move these to a silent "ignored" category, ignored GUID errors should exclude from the save check. An ignored error should not trigger the "WARNING" popup.

#### Fixed

1. Does not find a broken shader reference on a "Mesh Renderer" object at element 0 position.
2. Does not find a broken mesh reference on a "Mesh renderer" object (child of VRCFury prefab).
3. When repairing a GUID inside a prefab it blocks while saying "Close prefab mode without saving first, then apply the redirects.", this should say "Repair GUID in prefab mode?" with a yes/no, instead of blocking all functions.

### IconCap

#### Bugs

#### Features
    

### Import Presets

#### Bugs

#### Features

### Menu Tool

#### Bugs

#### Features

1. Add a "Remove menu item" button that will use vrcfury or modular avatar and pipe that item to null, or delete depending on host owner.

#### Fixed
1. Menu entry state does not properly set itself after gesturemanager begins emulation on the avatar.
2. Menu manager does not include all VRCFury added objects (using latest beta release)
### Debug Tools

#### Refresh Scripts
    
##### Bugs
        
##### Features
        
#### Find Null references
    
##### Bugs
        
##### Features
            
#### Menu Mockup
    
##### Bugs
        
##### Features
        

# Possible new tools:

| Tool name | Tool purpose | Tool reasoning |
| :---: | :---: | :---: |
| VPM Manager | Manage VPM dependencies for packages. | To automatically fix possible broken GUID issues or feature issues before they become a problem that requires human intervention. An avatar might need GoGoLoco or Avatar Limb Scaling, since the VPM Manifest might not automatically contain these for the project we can have a tool that generates a very basic script that will inject them and call the VPM for package resolution, and present the JSON list as copyable objects so the user can add those packages to their VPM tool (such as VCC or ALCOM) | 