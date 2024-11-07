# Webfishing Cove
Cove is a dedicated server for WebFishing written in C#!

> [!IMPORTANT]  
> Cove is currently in less than a Beta state, things are hard coded and buggy.
> It is not recommended to use as of right now!

Cove is a implementation of the WebFishing network protocall in a CLI meaning it dosent require Godot or anything other than Steamworks!

# How it works
Cove uses none of WebFishing's code, insted it used C# to emulate the same network calls that the offical game would make to host a lobby!

Things like event spawning all had to be written from scratch to allow for the portability of C#! (also because i dont know GDScript)

Because of this emulation to run the server you must run it from a steam account that owns the game and that has steam open in the background


# Todo
- [ ] Spawn actors required for the Metal Detector
- [ ] Move entire server into a Program class (idk why .net gave me a classless program)
- [ ] Improve error handling
- [ ] Improve GoDot serialization both ways
- [X] Add proper support for actor handling (its okay atm)
- [ ] Add spawns for metal detector items
- [ ] Some sort of plugin or modding support (maybe)

# How to run:

> [!NOTE]  
> To run a server you must have Steam open on the computer you wish to run the server on
> and Steam must be logged into a account that has WebFishing in it's library 

1. Download
	- You can download the most recent version of the server here: [Nightly Releases](https://github.com/DrMeepso/WebFishingCove/tags)
	- A new build is made everytime code is changed so it may update quite alot!

2. Decompile WebFishing
	- Once you have the source files drag the `main_map.tscn` file into the `/worlds` folder of the server!
	- The `main_map.tscn` file can be found here in the WebFishing project `/Scenes/Map`

3. Change settings
	- You can modify the settings in the server.cgf file with all the info you want!
	- Too add a admin put there Steam64ID in the admins.cfg file with a ` = true` after it!
	- I.E. `76561198288728683 = true`

4. Run!
	- Run the server EXE and enjoy! 
	- Please be respectful and dont name the servers anything stupid!
