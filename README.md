# Virt-A-Mate Timeline

An animation timeline with keyframe and controllable curves

## Installing

### src/VamTimeline.cslist and src/*.cs

This is the main script for animating atoms. You can then add `VamTimeline.cslist` on atoms you want to animate.

### tools/VamTimelineController.cs

This allows creating a floating payback controller, and control multiple atoms together. Create a `Simple Sign` atom and add the script to it. Optional if you only want to animate one atom.

### tools/VamTimelineBackup.cs

This allows for reloading the main script without losing your data. Mosty useful for development. Add to atoms with VamTimeline.cslist. Unnecessary with normal use.

## How to use

## Basic usage

1. Add controllers
2. Add keyframes by scrubbing and moving

## License

[MIT](LICENSE.md)
