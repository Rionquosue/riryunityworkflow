# This file contains bugfixes and feature requests.

These are organized by tool, with a sub section for features and bugs. Once these bugs are fixed or removed, please remove the associated line.

Please reference "filestructure.md" for where each tool is located (Tool names table and Script locations).

### Main menu

#### Bugs

#### Features

### Animator Tools

#### Bugs

#### Features

### Attach To

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

### Constraint Tools

#### Bugs

#### Features

### Controller Cleaner

#### Bugs

#### Features

### Find and Replace

#### Bugs

#### Features

### GUID Tools

#### Bugs

#### Features

### IconCap

#### Bugs

#### Features

### Import Presets

#### Bugs

#### Features

### Material Cleaner

#### Bugs

#### Features

### Menu Tools

#### Bugs

#### Features

### Stop Auto Game

#### Bugs

#### Features

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

#### Script Load Guard

##### Bugs

##### Features

# Possible new tools:

| Tool name | Tool purpose | Tool reasoning |
| :---: | :---: | :---: |
| VPM Manager | Manage VPM dependencies for packages. | To automatically fix possible broken GUID issues or feature issues before they become a problem that requires human intervention. An avatar might need GoGoLoco or Avatar Limb Scaling, since the VPM Manifest might not automatically contain these for the project we can have a tool that generates a very basic script that will inject them and call the VPM for package resolution, and present the JSON list as copyable objects so the user can add those packages to their VPM tool (such as VCC or ALCOM) | 