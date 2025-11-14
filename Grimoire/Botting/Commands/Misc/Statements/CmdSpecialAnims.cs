using Grimoire.Game;
using Grimoire.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdSpecialAnims : StatementCommand, IBotCommand
    {
        public CmdSpecialAnims()
        {
            Tag = "Monster";
            Text = "Special Anims";
            Description1 = "Animation message (or messages, comma-separated)";
            Description2 = "Skill index to use (optional)";
        }

        public async Task Execute(IBotEngine instance)
        {
            // Value1 = animation message(s) to check for (e.g., "sun converges", "shattering", or "resist")
            //        You can separate multiple keys with commas: "resist,shattering"
            // Value2 = optional skill index to cast immediately when matched (like Maid's message skill)

            string raw = (instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1);
            string lastMessage = Configuration.LastAnimationMessage?.ToLower();

            // Normalise and support comma-separated search terms like Maid does
            string[] targets = string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.ToLower()
                     .Split(',')
                     .Select(t => t.Trim())
                     .Where(t => !string.IsNullOrEmpty(t))
                     .ToArray();

            bool matched = !string.IsNullOrEmpty(lastMessage) && targets.Length > 0 &&
                           Array.Exists(targets, t => lastMessage.Contains(t));

            // Debug so we can see what the statement sees
            LogForm.Instance.AppendDebug($"[SpecialAnims] last='{lastMessage}' targets=[{string.Join(",", targets)}] matched={matched}");

            // Two modes:
            // 1) If a skill index is provided (Value2):
            //      - Never skip the next command.
            //      - Only when the message matches, cast that skill (with retry) before continuing.
            // 2) If no skill index is provided:
            //      - Pure conditional: skip the next command while the message has NOT appeared.

            string resolvedSkillIndex = null;
            if (!string.IsNullOrWhiteSpace(Value2))
            {
                resolvedSkillIndex = instance.IsVar(Value2)
                    ? Configuration.Tempvariable[instance.GetVar(Value2)]
                    : Value2;
                if (string.IsNullOrWhiteSpace(resolvedSkillIndex))
                    resolvedSkillIndex = null;
            }

            if (!string.IsNullOrWhiteSpace(resolvedSkillIndex))
            {
                // Skill mode: never skip, only react when the message matches
                if (matched)
                {
                    try
                    {
                        // Wait for skill to be off cooldown (if necessary), but don't stall forever
                        int attempts = 3;
                        while (attempts-- > 0)
                        {
                            int cd = Player.SkillAvailable(resolvedSkillIndex);
                            if (cd <= 0)
                                break;

                            await Task.Delay(Math.Min(cd, 1000));
                        }

                        // First attempt to force-cast
                        Player.ForceUseSkill(resolvedSkillIndex);

                        // Short delay, then verify by checking if the skill went on cooldown
                        await Task.Delay(150);
                        if (Player.SkillAvailable(resolvedSkillIndex) <= 0)
                        {
                            // If it's still instantly available, try one more time
                            LogForm.Instance.AppendDebug($"[SpecialAnims] Skill {resolvedSkillIndex} appears not to have fired, retrying once.");
                            Player.ForceUseSkill(resolvedSkillIndex);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogForm.Instance.AppendDebug($"[SpecialAnims] Error while forcing skill {resolvedSkillIndex}: {ex.Message}");
                    }

                    // Consume the last message so we don't keep matching the same text forever
                    Configuration.LastAnimationMessage = string.Empty;
                    Configuration.AnimationTriggered = false;
                }
            }
            else
            {
                // No skill index configured: classic statement behavior
                if (!matched)
                {
                    // Message hasn't appeared yet -> skip the next command
                    instance.Index++;
                }
                else
                {
                    // Message matched -> let the next command run, and consume the trigger
                    Configuration.LastAnimationMessage = string.Empty;
                    Configuration.AnimationTriggered = false;
                }
            }

            // Let the bot continue with the next command (or the one after, if we skipped)
            return;
        }

        public override string ToString()
        {
            return "Special Anims: " + Value1 + (string.IsNullOrEmpty(Value2) ? "" : " | Skill: " + Value2);
        }
    }
}
