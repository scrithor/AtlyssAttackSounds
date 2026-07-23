# AtlyssAttackSounds
### Version 1.0.0

### **Description**

This mod adds custom plap attack sounds triggered by weighted probabilities. Players can customize the sounds by replacing the audio files in the mod folder. The rare event? A fart.

### **Features**

- **Dynamic Audio Injection:** Intercepts the *PlayerCombat.Init_Attack* method of the local player *Player._mainPlayer* and plays sounds.
- **Weighted Rarity System:** Stochastic audio selection divided by categories based on configurable probabilities:
  - **Fast:** `84%` trigger chance (fast combat sounds).
  - **Medium:** `14%` trigger chance (intermediate sounds).
  - **Slow:** `2%` trigger chance (rare events).
- **Ass Bone Physics:** It jiggles your character's butt.
- **Particle Emission:** Particles? Yeeeeeeee
- **Audio Concurrency Prevention:** Cooldown time management based on the exact duration of the played file to prevent messy overlapping (audio clipping). 

### **Tip**

- When you want to replace the audio, prefer using .wav and .ogg files for trouble-free playback.

### **About This Project**

This mod started out of pure curiosity. I wanted to understand how [*TransientGuy's AtlyssFartMod*](https://thunderstore.io/c/atlyss/p/TransientGuy/AtlyssFartMod/) worked under the hood. After digging through the code and assets, I felt inspired to create my own version. Using the original mod's files as a foundation, I developed this project. A tribute to both the learning journey and the creativity that modding encourages. I hope this project brings everyone as much fun as it brings me.

### **Credits**
 - Thank you **TransientGuy** for the original mod! 
 - [*TransientGuy's GitHub*](https://github.com/transientguy/AtlyssFartMod) | [*TransientGuy Mod*](https://thunderstore.io/c/atlyss/p/TransientGuy/AtlyssFartMod/)
 - A big thank you to everyone who teaches for free online using videos, text, and audio! You all form the foundation of knowledge on the internet.

### **Contact**
If you have any problems or questions about this mod, feel free to contact me at:
- **Discord:** @scrithor