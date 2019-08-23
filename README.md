# VFXDecoration
VFX Decorations prototype
Using Unity 2019.2.0f1

![image](https://drive.google.com/uc?export=view&id=1i9Rvt9c2vGkUy_TxGGHgvFzHZAVxQv8_)

Load the "GrassWind" scene

Now works across multiple Terrain tiles

Issues:
   you may get some flashing while editing across multiple Terrain tiles ...
   this is because it has to amortize the respawn, as there is a bug in VFX that it
   can only handle one spawn event per frame

   Random values are non-deterministic.
   If you look away then look back, you will get different results.
   If you use the PatternGridSpawn node, you will get deterministic positions.
   But we need a way to override VFX random seed to fix random non-determinism.

   Note: many properties are baked into the decoration on spawn.
   If you are modifying these (color, size, etc), and want to see live update,
   enable "continuous respawn in editor"
   This will look weird if you move the camera (see non-deterministic spawn pattern above)
   but will give you live update of all of the spawn baked properties.
   
   Sometimes when you modify the VFX system, it resets the state, which causes all of the particles to disappear.
   You can either hit "reset and respawn" on the VFX controller, or simply Ctrl+S (save) to get them back.
   
   Undo has a similar issue.. it tries to hook undo/redo and trigger a reset/respawn (in case terrain was modified).
   But for some reason this is not very reliable.
   
![image](https://drive.google.com/uc?export=view&id=1FSAfniuHxoegLQh9J6_858ub_WGxPa-q)
