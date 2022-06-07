# ACAudio

ACAudio is a Decal plugin for Asheron's Call that adds in-game music and 3D sound effects.

## What it does

Music plays when you're portaling or entering specific dungeons. You may hear music when near towns or encountering specific boss monsters. 

Candles, campfires, lanterns, blacksmith anvils, portals, lifestones, casinos, town criers... These are just a few examples of objects that have new sounds.
There are roughly 100,000 sound sources in the provided sound pack.

## How the sound pack works

The sound pack is 100% customizable through the configuration script [/data/master.aca](https://github.com/bahstrike/ACAudio/blob/main/DEPLOY/data/master.aca).
All of the .ACA configuration and sound files are stored within the [/data/](https://github.com/bahstrike/ACAudio/tree/main/DEPLOY/data) folder.
_If you make your own sound pack, you should keep your [credits.txt](https://github.com/bahstrike/ACAudio/blob/main/DEPLOY/data/credits.txt) up-to-date!_

Sound Attributes are defined by not only their filename, but also properties such as their volume, audible distance or probability to play. The same configuration directives can define a looping song just as easily as they can define a rare chance for NPC speech.

Sound Sources can be anything from the player being inside a certain dungeon, a certain point on the terrain, `Data ID` of static decor _(such as a fireplace.. ALL fireplaces)_, items on the ground, NPCs with a certain species, enemies with a certain name... The possibilities are endless!

## How the audio engine works

ACAudio does not alter the in-game audio in any way. The plugin's FMOD sound engine runs alongside Asheron Call's DirectSound sound engine.
Your operating system mixes both audio streams for your final speaker output.

ACAudio is able to play sounds that truly feel as though the game is playing them. This is because FMOD supports 3D positional audio and Decal can provide 3D coordinates for in-game objects.
Speech from an NPC or sizzling from a fireplace seem as though they are really coming from the object; fading out as you move farther away and panning as you turn around.

## readme.txt

_NOTE: This excerpt may be out of date. Please check your install directory for `readme.txt` for your version. Alternatively, click [readme.txt](https://github.com/bahstrike/ACAudio/blob/main/DEPLOY/readme.txt) for the beta build information._

```
Welcome to ACAudio!

Feel free to customize your own soundpack!
Everything is in the "data" folder.
The plugin will load "data/master.aca" to get started.


DEVELOPMENT
--------------------------------------------------------------------------------------------
Code				Strike
Additional Code			trevis
Static Positions Data		OptimShi
Audio Engine			FMOD (Firelight Technologies Pty Ltd.)
Dynamic Music Concept		Maethor


ADVICE and DISCUSSION
--------------------------------------------------------------------------------------------
Advan
Harli Quin
Hells
Immortal Bob
Maethor
OptimShi
paradox
trevis
Yonneh


MUSIC and SOUND EFFECTS
--------------------------------------------------------------------------------------------
Please see ./data/credits.txt for whichever soundpack you are using.

NOTE: Plugin developer(s) are not responsible for copyright violations in soundpacks made by end-users!
      The official sound pack has properly attributed music and sounds.


SmithLib.dll
--------------------------------------------------------------------------------------------
SmithLib.dll is a math and utilities library written by Strike (Bad Ass Hackers) for a proprietary game engine called Smith (https://smith.bah.wtf).

SmithLib.dll MAY be used (as-is) in non-commercial projects:
 - The following attribution must be easily visible to the end-user within your application:  "Uses components from Smith Engine (Bad Ass Hackers)"
 - The entirety of this "SmithLib.dll" subsection must be present within your readme file or legal disclaimer.

You MAY NOT modify, decompile, reverse engineer or sell SmithLib.dll. You must contact Bad Ass Hackers to discuss.
You MAY NOT use SmithLib.dll in a commercial project. You must contact Bad Ass Hackers to discuss.
You MAY NOT distribute SmithLib.dll in any fashion, unless the entirety of this "SmithLib.dll" subsection is included.
```
