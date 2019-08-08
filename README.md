# VFXDecoration
VFX Decorations prototype
Using Unity 2019.2.0f1

Load the "GrassWind" scene

Now works across multiple Terrain tiles

Issues:
   you may get some flashing while editing across multiple Terrain tiles ...
   this is because it has to amortize the respawn, as there is a bug in VFX that it
   can only handle one spawn event per frame
