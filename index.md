---
layout: default
---

## Download

##### ACAudio:
**[Installer for Decal plugin: ACAudio 2.0](https://github.com/bahstrike/ACAudio/releases/download/2.0.0.0/ACAudio-2.0.0.0.exe) _(most users only need this)_**

_[Portable version; download whole folder: you must add `ACAudio.dll` to Decal](https://github.com/bahstrike/ACAudio/tree/2.0.0.0/DEPLOY_ACAUDIO)_

##### For players wanting to host voice chat server via in-game bot:
[Installer for Decal plugin: ACAVCServer 2.0](https://github.com/bahstrike/ACAudio/releases/download/2.0.0.0/ACAVCServer-2.0.0.0.exe)

_[Portable version; download whole folder: you must add `ACAVCServer.dll` to Decal](https://github.com/bahstrike/ACAudio/tree/2.0.0.0/DEPLOY_ACAVCSERVER)_


##### For Asheron's Call server administrators wanting to host dedicated voice chat server:
[GitHub repository for ACAVCServer (.NET Core)](https://github.com/bahstrike/ACAVCServer)

_For source code or older versions, please [visit the GitHub repository](https://github.com/bahstrike/ACAudio)._


## What it does

[Music plays when you're](#how-the-audio-engine-works) portaling or entering specific dungeons. You may hear music when near towns or encountering specific boss monsters. 

[Candles, campfires, lanterns,](#how-the-sound-pack-works) blacksmith anvils, portals, lifestones, casinos, town criers... These are just a few examples of objects that have new sounds.
There are roughly 100,000 sound sources in the provided sound pack.

[The built-in voice chat is better than Discord!](#how-the-voice-chat-works) It is proximity based and someone's speech comes from their in-game 3D character with a floating icon over their head. There are also Allegiance and Fellowship channels which work like traditional 2D voice chat; perfect for questing!

## How the audio engine works

ACAudio does not alter the in-game audio in any way. The plugin's FMOD sound engine runs alongside Asheron Call's DirectSound sound engine.
Your operating system mixes both audio streams for your final speaker output.

ACAudio is able to play sounds that truly feel as though the game is playing them. This is because FMOD supports 3D positional audio and Decal can provide 3D coordinates for in-game objects.
Speech from an NPC or sizzling from a fireplace seem as though they are really coming from the object; fading out as you move farther away and panning as you turn around.

_The real magic is mapping customizable .ACA files to in-game elements. I hope the plugin does the game justice and you enjoy using and/or modifying it!_

## How the sound pack works

The sound pack is 100% customizable through the configuration script [/data/master.aca](https://github.com/bahstrike/ACAudio/blob/main/DEPLOY_ACAUDIO/data/master.aca).
All of the .ACA configuration and sound files are stored within the [/data/](https://github.com/bahstrike/ACAudio/tree/main/DEPLOY_ACAUDIO/data) folder.
_If you make your own sound pack to distribute, you should keep your [credits.txt](https://github.com/bahstrike/ACAudio/blob/main/DEPLOY_ACAUDIO/data/credits.txt) up-to-date!_

Sound Attributes are defined by not only their filename, but also properties such as their volume, audible distance or probability to play. The same configuration directives can define a looping song just as easily as they can define a rare chance for NPC speech.

Sound Sources can be anything from the player being inside a certain dungeon, a certain point on the terrain, `Data ID` of static decor _(such as a fireplace.. ALL fireplaces)_, items on the ground, NPCs with a certain species, enemies with a certain name... The possibilities are endless!

## How the voice chat works

The optional voice chat is push-to-talk only so you don't have to worry about an accidental "hot mic". Bind whatever push-to-talk key you want via Virindi Hotkeys.

The network protocol is not peer-to-peer so you don't have to worry about your IP address being shared with other players. Instead, ACAudio must connect to a voice server, of which there are two options:
- The owner of the Asheron's Call server you play on can also run [ACAVCServer](https://github.com/bahstrike/ACAVCServer) which ACAudio will automatically connect to
- Any player can run a voice server bot (usually in marketplace) that you connect to via `/tell TheBotName, join`

## readme.txt

_NOTE: This excerpt may be out of date. Please check your install directory for `readme.txt` for your version. Alternatively, click [readme.txt](https://github.com/bahstrike/ACAudio/blob/main/DEPLOY_ACAUDIO/readme.txt) for the beta build information._

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
μ-law Voice Compression		https://www.codeproject.com/Articles/482735/TCP-Audio-Streamer-and-Player-Voice-Chat-over-IP


ADVICE and DISCUSSION
--------------------------------------------------------------------------------------------
Advan
Harli Quin
Hells
Immortal Bob
OptimShi
paradox
trevis
Yonneh


MUSIC and SOUND EFFECTS
--------------------------------------------------------------------------------------------
Please see ./data/credits.txt for whichever soundpack you are using.

NOTE: Plugin developer(s) are not responsible for copyright violations in soundpacks made by end-users!
      The official sound pack has properly attributed music and sounds.


Smith Libraries  (SmithCore.dll, SmithAudio.dll)
--------------------------------------------------------------------------------------------
Smith Libraries are a math and utilities library written by Strike (Bad Ass Hackers) for a proprietary game engine called Smith (https://smith.bah.wtf).

Smith Libraries MAY be used (as-is) in non-commercial projects:
 - The following attribution must be easily visible to the end-user within your application:  "Uses components from Smith Engine (Bad Ass Hackers)"
 - The entirety of this "Smith Libraries" subsection must be present within your readme file or legal disclaimer.

You MAY NOT rename, modify, decompile, reverse engineer or sell Smith Libraries. You must contact Bad Ass Hackers to discuss.
You MAY NOT use Smith Libraries in a commercial project. You must contact Bad Ass Hackers to discuss.
You MAY NOT distribute Smith Libraries in any fashion, unless the entirety of this "Smith Libraries" subsection is present within your readme file or legal disclaimer.



ACAUDIO, OR ANY ASSOCIABLE COMPONENT, IS NOT MADE, DISTRIBUTED, OR
SUPPORTED BY WB GAMES BOSTON. ELEMENTS TM and COPYRIGHT WB GAMES BOSTON.
```
