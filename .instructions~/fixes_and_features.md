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

1. When editing the menu of a prefab provide the option to apply your changes as VRCFury overides or as direct menu path changes with a second "Destructive Move" submenu on the right click that is only active when right clicking a menu item added by ModularAvatar or VRCFury. Automatically determine what the "Root" of that menu would be, only present available options from that menu root (starting from the Full Controller menu path as "Root")

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
        

