# VFXDecoration
VFX Decorations prototype
Using Unity 2019.2.0f1

Load the "GrassWind" scene

Now works across multiple Terrain tiles

Issues:
   you may get some flashing while editing across multiple Terrain tiles ...
   this is because it has to amortize the respawn, as there is a bug in VFX that it
   can only handle one spawn event per frame

   Spawning pattern is non-deterministic.
   If you look away then look back, you will get different results.

   Note: many properties are baked into the decoration on spawn.
   If you are modifying these (color, size, etc), and want to see live update,
   enable "continuous respawn in editor"
   This will look weird if you move the camera (see non-deterministic spawn pattern above)
   but will give you live update of all of the spawn baked properties.
   
   Sometimes when you modify the VFX system, it resets the state, which causes all of the particles to disappear.
   You can either hit "reset and respawn" on the VFX controller, or simply Ctrl+S (save) to get them back.
   
   Undo has a similar issue.. it tries to hook undo/redo and trigger a reset/respawn (in case terrain was modified).
   But for some reason this is not very reliable.
   
