# Virt-A-Mate Timeline

An animation timeline with keyframe and controllable curves

## Installing

Download the content of `src/*` into `(VaM Install Folder)/Custom/Scripts/Acidbubbles/vam-timeline`

## Basic Setup

It is expected that you have some basic knowledge of [how Virt-A-Mate works](https://www.reddit.com/r/VirtAMate/wiki/index) before getting started. Basic knowledge of keyframe based animation is also useful. In a nutshell, you specify some positions at certain times, and all positions in between will be interpolated using curves (linear, smooth, etc.).

### Animating an atom

1. Add the `VamTimeline.AtomPlugin.cslist` plugin on atoms you want to animate, and open the plugin settings (`Open Custom UI` in the Atom's `Plugin` section).
2. On the right side, select a controller you want to animate in the `Animate Controller` drop down, and select `Add / Remove Controller` to attach it. This will turn on the "position" and "rotation" controls for that controller.
3. To add a frame, move the `Time` slider to where you want to create a keyframe, and move the atom. This will create a new keyframe.
4. Check the `Display` text field; you can see all the keyframes you have created there, and visualize which one you have selected.
5. Move from a frame to another using the `Next Frame` and `Previous Frame` buttons.
6. Play your animation using the `Play` button, and stop it using the `Stop` button (was this instruction really useful?)

### Adding an external playback controller

This allows creating a floating payback controller, and control multiple atoms together. Create a `Simple Sign` atom and add the script to it. This is optional, you only need this if you want to animate more than one atom, or if you want the floating playback controls.

Add the `VamTimeline.ControllerPlugin.cs` plugin on a `Simple Sign` atom.

In the plugin settings, select the animations you want to control and select `Link`.

You can now control the animations in the floating panel; you can also select which atom and animation to play.

### Development

When reloading a Virt-A-Mate script after it was modified externally, you will lose your data. For complex animations, it can be a frustrating workflow. Add the `VamTimeline.BackupPlugin.cs` script on all atoms that have the `VamTimeline.AtomPlugin.cslist`.

This allows for reloading the main script without losing your data. Mosty useful for development. Add to atoms with VamTimeline.cslist. Unnecessary with normal use.

## How to use

## Basic usage

1. Add controllers
2. Add keyframes by scrubbing and moving

## License

[MIT](LICENSE.md)
