# This file contains bugfixes and feature requests.

These are organized by tool, with a sub section for features and bugs. Once these bugs are fixed or removed, please remove the associated line.

Please reference "filestructure.md" for where each tool is located (Tool names table and Script locations).

### Main menu

#### Bugs

#### Features

### Animator Tools

#### Bugs

#### Features

1. Move "Clean Animation Keys" and "Extract Animations" into here.
    
### AutoSync

#### Bugs

#### Features
    
### Avatar Migration Assistant

#### Bugs

#### Features
    
### Blendshape Sync

#### Bugs

#### Features

1. Add in the ability to name jobs, default to current job naming scheme.
    
### Clean Animation Keys

#### Bugs

1. Overlaps with other drawn UI elements, justify hard left to avoid interference with common animator tools.

#### Features

1. Move to "Animator Tools" in the right click menu.

### Constraint Tool

#### Bugs

#### Features
    
### GUID Tools

#### Bugs

#### Features

1. "start" empty needs a definition field. All bones should be constrained to the "start" except for the first bone that moves along the chain which should be at the first bone of the "rest" chain.

#### Features


#### Fixed


### IconCap

#### Bugs

#### Features
    

### Import Presets

#### Bugs

#### Features

1. Add a field to allow the user to select which filters the import preset tool should listen to.

### Menu Tool

#### Bugs

#### Features

1. Add a "Remove menu item" button that will use vrcfury or modular avatar and pipe that item to null, or delete depending on host owner.

#### Fixed


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