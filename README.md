# Gameboy Emulator

## Introduction
This is a prototype gameboy emulator in a very early stage. Progress is slow/nonexistent. 
Built for education first and performance second. 
As such accuracy or emulating every part of the console is of no concern currently.

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
- implementation of the graphics: (http://www.codeslinger.co.uk/pages/projects/gameboy/graphics.html)
- there are many more resources. will append as I find and use them
- BGB is accurate so it can to be used for debugging purposes

## Progress
This is an overview on what's been implemented and what's still left:

- [x] properly mapped memory
- [x] basic rom loading setup
- [x] flags and registers
- [x] cpu loop, instruction fetching, decoding and executing (last two are done at once)
- [x] basic support for interrupts
- [x] basic rendering pipeline (implemented, not working yet)
- [x] testing pipeline. a modified cpu can be injected with opcodes to test flags and registers
- [x] fully implement all opcodes
- [x] render tilemaps and sprite sheets for debugging purposes
- [x] use Dear ImGUI for options and other UI 
- [ ] memory bank switching
- [ ] get through blarggs test roms
- [ ] rendering results visible on screen (currently there's nothing visible on screen yet...)
- [ ] make sure timings and interrupts work properly...
- [ ] play tetris...
- [ ] sound (out of scope for the near future)

## Other things to consider
- replace fixed setting of cycles with dynamically setting it (accessing memory takes 1 cycle per byte, pushing/popping from SP takes 1 cycle per byte, fetching takes 1 byte per fetch)