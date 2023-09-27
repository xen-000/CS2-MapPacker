# CS2-MapPacker

A modified tool originally written by [kristiker](https://github.com/kristiker) as part of their [early bootleg tools](https://github.com/kristiker/CS2-Early-Bootleg-Tools). It is meant to facilitate distributing maps during the limited beta until Valve adds workshop support. The code is not the cleanest but it does the job.

## What it does
The tool uses the resource manifest within a compiled map to locate all referenced assets and pack them in the .vpk, or optionally they can just be isolated in a folder should the user want to add more files manually like sounds. It also finds any referenced maps like 3D skyboxes and recursively adds assets.

## What it cannot do
Compiled maps have no references to sound event files or sounds, in such cases you will have to manually add the sound files yourself. Maybe the tool can be made to parse `soundevents_addon.vsndevts` to get referenced sounds but I felt it wasn't worth the effort.

## How to use
Just drag your compiled .vpk from where it is (`game/csgo_addons/addon_name/maps`) onto MapPacker.exe.

From there you have the option to pack the vpk immediately or isolate assets and map internal files in a separate folder located at: `game/csgo_addons/mappacker/map_name`

If the latter is chosen, you can later pack a vpk by simply dragging the map folder onto MapPacker.exe.