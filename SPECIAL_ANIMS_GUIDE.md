# Special Anims Statement Command - Implementation Guide

## Overview
The Special Anims feature from Maid has been implemented as a statement command in the bot's Misc section. This allows you to detect special animation messages during combat and conditionally execute bot commands based on these animations.

## What Was Implemented

### 1. New Statement Command: `CmdSpecialAnims`
- **Location**: Misc section in bot command editor
- **Tag**: "Special"
- **Text**: "Special Anims"
- **Description1**: Animation message
- **Description2**: Skill to use (optional, for reference)

### 2. Configuration Properties
Added to `Configuration` class:
- `AnimationTriggered` (bool): Indicates if an animation was recently detected
- `LastAnimationMessage` (string): Stores the last animation message detected

### 3. Handler Integration
- **Maid Integration**: The existing Maid `AnimsMsgHandler` now sets the Configuration properties
- **Global Handler**: New `HandlerSpecialAnims` can be registered globally for use outside of Maid

## How To Use

### In Bot Scripts

You can now use the "Special Anims" statement in your bot scripts under the Misc section:

**Example Bot Script:**
```
1. Join: ultragramiel-9999
2. Special Anims: shattering
3. Use Skill: 5
4. Attack: *
5. Use Skill: 1,2,3,4
```

**How it works:**
- Line 2 checks if the last animation message contains "shattering"
- If TRUE (animation detected), line 3 executes (Use Skill: 5)
- If FALSE (no matching animation), line 3 is skipped and goes to line 4

### Animation Messages to Detect

Common animation messages you can detect:
- `shattering` - Crystal shattering (Gramiel)
- `sun converges` - Ascended Solstice attacks
- `moon converges` - Ascended Midnight attacks
- `behold our starfire` - Astral Empyrean attacks
- `counter attack` - Boss counter attack preparation
- Any other animation text that appears in chat

### Multiple Conditions

You can check multiple animations by separating them with commas:
```
Special Anims: shattering,sun converges,moon converge
```

### With Maid

The feature works automatically with Maid when:
1. Maid's "Special Anims" checkbox is enabled
2. Animation messages are entered in the "Special Msg" textbox
3. The bot script uses the "Special Anims" statement command

### Without Maid (Standalone Bot)

To use Special Anims detection in standalone bots without Maid:
1. Register the `HandlerSpecialAnims` in your bot initialization
2. Use the "Special Anims" statement in your bot script
3. The handler will automatically detect and store animation messages

## Technical Details

### How Detection Works

1. When an animation packet is received from the game server
2. The `AnimsMsgHandler` or `HandlerSpecialAnims` extracts the message text
3. The message is stored in `Configuration.LastAnimationMessage`
4. `Configuration.AnimationTriggered` is set to `true`
5. When your bot executes a "Special Anims" statement:
   - It checks if the stored message contains your search text
   - If yes, the next command executes
   - If no, the next command is skipped (bot jumps forward one command)
   - After a successful match, `AnimationTriggered` is reset to `false`

### Files Modified/Created

**New Files:**
- `Grimoire/Botting/Commands/Misc/Statements/CmdSpecialAnims.cs`
- `Grimoire/Networking/Handlers/HandlerSpecialAnims.cs`

**Modified Files:**
- `Grimoire/Botting/Configuration.cs` - Added AnimationTriggered and LastAnimationMessage properties
- `Grimoire/UI/Maid/MaidRemake.cs` - Updated AnimsMsgHandler to set Configuration properties
- `Resources/statementcmds.txt` - Added CmdSpecialAnims entry
- `Grimoire.csproj` - Added compile references for new files

## Example Use Cases

### 1. Ultra Boss Gramiel (Crystal Detection)
```
Attack Priority: id.2,crystal
Special Anims: shattering
Use Skill: 5
Attack: *
Use Skill: 1,2,3,4
```

### 2. Ascended Eclipse (Sun/Moon Converges)
```
Attack Priority: Ascended Solstice
Special Anims: sun converges
Use Skill: 5
Attack: *
Use Skill: 1,2,3
```

### 3. Counter Attack Detection
```
Kill: *
Special Anims: counter attack
Rest
Delay: 2000
```

## Notes

- Animation messages are case-insensitive
- Partial matches work (e.g., "sun" will match "sun converges")
- The statement only checks the LAST animation message
- Animation state resets after a successful match
- Works seamlessly with existing Maid functionality

## Future Enhancements

Potential improvements:
- Animation message history/queue
- Timeout for animation triggers
- Counter for repeated animation occurrences
- Multiple animation conditions (AND/OR logic)
