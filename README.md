# SLUDGE LIFE 2 VR - Beta v1

alrighty, here's SLUDGE LIFE 2 in VR. i wanted it to feel like you're actually Ghost: green hands, black biker gloves, black nails, the ant tattoo, and a bunch of stuff you can mess with physically.

this is an unofficial PC VR mod made using **OpenAI Codex**. the original game belongs to its creators.

you can climb with the spray can in your right hand now. grip the ladder and pull like normal, then keep your left grip on the ladder while you let go with the right and paint with the right trigger. release both grips to drop.

this is Beta v1. go play, try the different interactions, and tell me what feels wrong.

## get it running

you need your own installed Windows Steam copy of **SLUDGE LIFE 2**, a PC VR headset, motion controllers, and SteamVR. my setup is Quest 3 + Virtual Desktop + SteamVR. other setups haven't been checked. this isn't a standalone Quest install.

1. close the game and extract this whole ZIP somewhere you can keep it. don't run it from inside the ZIP.
2. run **INSTALL.cmd**. it looks for the game in your Steam libraries. if it can't find it, paste the folder containing **SludgeLife2.exe**. BepInEx and the required runtime are included.
3. connect the headset to your PC and start SteamVR. make sure SteamVR sees the headset and both controllers.
4. keep Steam open, then run **LAUNCH-VR.cmd** from this extracted folder. use this launcher for the VR arguments.

if it picked the wrong drive or you've got a custom install, open a terminal in this folder and use:

```bat
INSTALL.cmd -GamePath "D:\SteamLibrary\steamapps\common\SLUDGE LIFE 2"
LAUNCH-VR.cmd -GamePath "D:\SteamLibrary\steamapps\common\SLUDGE LIFE 2"
```

this package is for **SLUDGE LIFE 2**. use the separate Sludge Life 1 package for the first game.

the installer keeps backups of files it replaces. it doesn't touch your save. if another mod loader is present, it stops so you can check compatibility. a clean game/mod setup is the easiest way to start.

## how the controls are lined up

| control | what it does |
|---|---|
| left stick | move, based on where you're looking |
| right stick | smooth turn; moves the cursor when the laptop/menu is open |
| right trigger | talk/use stuff; click on the laptop; hold to drag a window |
| right A | jump; hold it in the air to glide after you've got the glider |
| hold left grip while moving, away from physical holds | sprint/run (Shift); right A can still jump |
| hold left trigger (left grip released) | crouch; press right A at the same time to crouch-jump where the game allows |
| left stick click | fart on the ground, flip in the air when the game lets you |
| right stick click | smoke when the game allows it |
| left Y | pause |
| either grip near food, soda or a disk | grab it; let go to drop it |
| grip a ladder/climb area and pull | physical climbing, including with the spray can out; release both grips to let go |
| right grip near a basketball | grab it; move your hand and release to shoot |
| right grip behind your right shoulder | pull out the camera once you have it; return it there and release to stow |
| right B | alternate camera toggle |
| right trigger with camera out | take a picture; you can also poke its top shutter |
| left grip supporting the camera | steady it; left stick up/down zooms |
| left grip + X / left grip + trigger | telewarp / Vandal Eyes |

on the laptop, **left grip slows the cursor**, left stick scrolls, and right grip is right-click. there isn't a hand laser. **F10** on the keyboard recenters the view and puts an open menu in front of you.

## actually use your hands

- hold right trigger and physically sweep the can over the tag. after about 7.5 seconds of spraying, release the trigger and give the can a couple quick shakes, then keep painting. the ball rattles with the rumble and the nozzle hisses while spraying. your tag keeps its progress. the can shows up when the game's spray interaction is active.
- grab a soda or slug and keep gripping while bringing it to your mouth. tilt the soda a little. grabbing it alone doesn't eat it. the slugs should still squirm, and they're meant to stay their big cartoony game size.
- poke buttons and bells with your empty index finger. on the rocket, poke the actual red cap. push doors with an empty hand, or give the kid on the swing a push.
- pet the cats and dogs. try scratching under a cat's chin for the purring rumble.
- tap the club MPC pads with your fingers or palm and play drums along with the song. lift between hits. tap gently for a softer sound, or hit harder for full volume. the top row is low D2, G2, A2 and B2 acid bass from left to right, with distortion. they ring for about 2.8 seconds. different notes can ring together, and tapping the same note restarts it. the other twelve pads are drums, including kick, snare and hi-hats.
- with the glider out, empty hands should grab the metal handles automatically. keep holding right A; steer with the left stick.
- on mushrooms, the left stick flies where you're looking, including up/down. left Y uses the game's return action.

the game's unlocks and story rules still apply. items and abilities become available as you progress.

## feedback and screenshots

if something is not working, include your headset, connection method, where you were, what you did, and what you expected to happen. screenshots and steps to reproduce the issue help.

hold **both grips for a second**, or press **F8**, to capture the game view. the default location is `BepInEx/SludgeLife2VR-Captures` inside the game folder. include the matching `.txt` report and `BepInEx/LogOutput.log` when reporting a problem. **F9** writes a scene report, and **F11** toggles VR.

if the game launches flat or stays on SteamVR's "next up" screen, close the game, check that SteamVR sees the headset, and try **LAUNCH-VR.cmd** again.

## settings and uninstalling

after the first run, settings are in `BepInEx/config/local.sludgelife2.vr.cfg`. close the game before editing. smooth turning defaults to 240 degrees/second. lower it if you prefer, or set `SmoothTurning = false` for snap turning.

to remove the mod, close the game and run **UNINSTALL.cmd**. it restores matching original files and keeps your saves, captures, logs, configs and backups. files changed by another mod or by you are kept.

## credits

SLUDGE LIFE 2 was created by Terri Vellmann and doseone and published by Devolver Digital. this is an unofficial fan project.

mod source is included in `Source/`. dependency licenses and source archives are in `THIRD-PARTY/`, and asset details are in `ASSET-NOTES.txt`.
