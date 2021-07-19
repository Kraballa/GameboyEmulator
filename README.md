# Gameboy Emulator

## Introduction
This is a prototype gameboy emulator in a very early stage. Progress is slow/nonexistent. 
Built for education first and performance second. 
As such accuracy or emulationg every part of the console is of no concern currently.

Graphics are done via the FNA library.

## Setup Repo
- pull this repository via git or download as zip
- follow the steps to [include FNA](https://github.com/FNA-XNA/FNA/wiki/1:-Download-and-Update-FNA) into the project file and copy the .dll files into the output
- you can download the native libs [here](http://fna.flibitijibibo.com/archive/), the link in the wiki is broken

## Helpful Links
- Game Boy CPU Instruction Table including some short but helpful explanations: (https://meganesulli.com/generate-gb-opcodes/)
- Game Boy CPU Instruction Table again followed by some basic informations on registers and flags: (https://www.pastraiser.com/cpu/gameboy/gameboy_opcodes.html)
- mirror to blarggs test roms: (https://gbdev.gg8.se/files/roms/blargg-gb-tests/)
- excellent documentation of the hardware and how it interacts: (https://problemkaputt.de/pandocs.htm)
- there are many more resources. will append as I find and use them

## Code Conventions
- any hex ciphers that use letters (i.e. 0xFF, 0xA8) must use the capitalized letter
- code files relating to emulating the gameboy should be placed under /emu
- files that contain data that is not code or this readme should be placed in /data
- lines that are not necessarily easily understood may be explained on the same line. keep it as short as possible