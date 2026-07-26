# AtlyssAttackSounds
### Version 1.1.1

### **Description**

This mod adds custom plap attack sounds triggered by weighted probabilities. Players can customize the sounds by replacing the audio files in the mod folder. The rare event? A fart.

### **Changelog - v1.1.1**

- **Audio System Fixes:** Updated audio files and resolved playback failure issues.
- **Animation Timing Sync:** Sounds now play at the end of the attack animation instead of triggering before it starts.
- **Continuous Input Support:** Holding down the attack button now correctly triggers multiple sounds across consecutive attacks (You're welcome, katar users!).

### **Changelog - v1.1.0**

- **In-Game Settings Menu:** Press **F7** to open a dynamic settings overlay.
  - **Volume Controls:** Adjust individual volume levels for `Fast`, `Medium`, and `Slow` audio categories.
  - **Proc Chance Sliders:** Customize relative weight chances for each sound trigger.
  - **Visual & Physics Tweaks:** Adjust ass bone jiggle intensity and particle size on the fly.
- **Improved Particle Physics:** Particle effects now attach directly to the player character, continuously following movement until they fade out instead of remaining static in the world.

### **Features**

- **Dynamic Audio Injection:** Intercepts the *PlayerCombat.Init_Attack* method of the local player *Player._mainPlayer* and plays sounds.
- **Weighted Rarity System:** Stochastic audio selection divided by categories based on configurable probabilities:
  - **Fast:** Default `84%` trigger chance (fast combat sounds).
  - **Medium:** Default `12%` trigger chance (intermediate sounds).
  - **Slow:** Default `4%` trigger chance (rare events).
- **Ass Bone Physics:** Jiggles your character's butt on attack, with configurable intensity.
- **Dynamic Particle Emission:** Emits particles that seamlessly move with the player.
- **Audio Concurrency Prevention:** Cooldown time management based on the exact duration of the played file to prevent messy overlapping (audio clipping). 

### **Tip**

- Press **F7** in-game to configure audio volumes, proc chances, and visual effect scales.
- When you want to replace the audio, prefer using .wav and .ogg files for trouble-free playback.
- You can edit the attack sound delay in the `.cfg` file to fit custom weapons by modifying values that follow this pattern: `Bow_Grounded = 0.366`.

### **About This Project**

This mod started out of pure curiosity. I wanted to understand how [*TransientGuy's AtlyssFartMod*](https://thunderstore.io/c/atlyss/p/TransientGuy/AtlyssFartMod/) worked under the hood. After digging through the code and assets, I felt inspired to create my own version. Using the original mod's files as a foundation, I developed this project. A tribute to both the learning journey and the creativity that modding encourages. I hope this project brings everyone as much fun as it brings me.

### **Credits**
 - Thank you **TransientGuy** for the original mod!
 - [*TransientGuy's GitHub*](https://github.com/transientguy/AtlyssFartMod) | [*TransientGuy Mod*](https://thunderstore.io/c/atlyss/p/TransientGuy/AtlyssFartMod/)
 - A big thank you to everyone who teaches for free online using videos, text, and audio! You all form the foundation of knowledge on the internet.

### **Contact**
If you have any problems, questions or any idea about this mod, feel free to contact me at:
- **Discord:** @scrithor