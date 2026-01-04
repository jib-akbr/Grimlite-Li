using Grimoire.Game;
using Grimoire.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdSpecialAnims : StatementCommand, IBotCommand
    {
        // Per-instance counter - each statement tracks its own message count
        private int _messageCount = 0;

        public CmdSpecialAnims()
        {
            Tag = "Monster";
            Text = "Special Anims";
            Description1 = "Animation message (or messages, comma-separated)";
            Description2 = "Skill index to use (optional)";
        }

        public override void OnBotStarted()
        {
            // Reset message count when bot starts
            _messageCount = 0;
        }

        public override void OnBotStopped()
        {
            // Reset message count when bot stops
            _messageCount = 0;
        }

        public async Task Execute(IBotEngine instance)
        {
            // Value1 = animation message(s) to check for (e.g., "sun converges", "shattering", or "resist")
            //        You can separate multiple keys with commas: "resist,shattering"
            // Value2 = optional skill index to cast immediately when matched (like Maid's message skill)
            // Index = optional occurrence number to taunt on (e.g., 1 = first message, 2 = second message, etc.)
            //         If not specified or <= 0, taunt on every message

            string raw = instance.ResolveVars(Value1);
            string lastMessage = Configuration.LastAnimationMessage?.ToLower();

            // DEBUG: Log what message we're checking
            LogForm.Instance.AppendDebug($"[SpecialAnims] Checking for '{raw}' | LastAnimationMessage: '{lastMessage ?? "NULL"}'");

            // Normalise and support comma-separated search terms like Maid does
            string[] message = string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.ToLower()
                     .Split(',')
                     .Select(t => t.Trim())
                     .Where(t => !string.IsNullOrEmpty(t))
                     .ToArray();

            bool matched = !string.IsNullOrEmpty(lastMessage) && message.Length > 0 &&
                           Array.Exists(message, t => lastMessage.Contains(t));

            // If we have an initial match, consume the message immediately so other statements don't see it
            bool initialMatch = matched;
            if (initialMatch)
            {
                Configuration.LastAnimationMessage = string.Empty;
                Configuration.AnimationTriggered = false;
            }

            // Handle Index-based triggering
            int targetIndex = 0;
            if (!string.IsNullOrWhiteSpace(TauntOrder))
            {
                string resolvedIndex = instance.ResolveVars(TauntOrder);
                if (!string.IsNullOrWhiteSpace(resolvedIndex) && int.TryParse(resolvedIndex, out int parsedIndex))
                {
                    targetIndex = parsedIndex;
                }
            }

            // If Index is specified, only trigger when message count matches the pattern (cyclically)
            if (matched && targetIndex > 0)
            {
                _messageCount++;
                // Cyclic triggering: Index 1 = odd (1,3,5...), Index 2 = even (2,4,6...), etc.
                if (targetIndex == 1)
                    matched = (_messageCount % 2 == 1); // Odd
                else if (targetIndex == 2)
                    matched = (_messageCount % 2 == 0); // Even
                else
                    matched = (_messageCount % targetIndex == 0); // Every Nth
                
                LogForm.Instance.AppendDebug($"[SpecialAnims] Message #{_messageCount}, Index={targetIndex}, matched={matched}");
            }
            else if (matched && targetIndex <= 0)
            {
                // No index specified - increment counter for reference but always match
                _messageCount++;
                LogForm.Instance.AppendDebug($"[SpecialAnims] Message #{_messageCount} received (no index filter)");
            }

            // Two modes:
            // 1) If a skill index is provided (Value2):
            //      - Never skip the next command.
            //      - Only when the message matches, cast that skill (with retry) before continuing.
            // 2) If no skill index is provided:
            //      - Pure conditional: skip the next command while the message has NOT appeared.

            string resolvedSkillIndex = null;
            if (!string.IsNullOrWhiteSpace(Value2))
            {
                resolvedSkillIndex = instance.ResolveVars(Value2);
                if (string.IsNullOrWhiteSpace(resolvedSkillIndex))
                    resolvedSkillIndex = null;
            }

            int delayMs = 0;
            if (!string.IsNullOrWhiteSpace(Delay))
            {
                string resolvedDelay = instance.ResolveVars(Delay);
                if (!string.IsNullOrWhiteSpace(resolvedDelay) && int.TryParse(resolvedDelay, out int parsedDelay))
                {
                    delayMs = Math.Max(0, parsedDelay);
                }
            }

            if (!string.IsNullOrWhiteSpace(resolvedSkillIndex))
            {
                // Skill mode: never skip, only react when the message matches
                if (matched)
                {
                    // Fire off the skill cast in background if there's a delay, otherwise execute immediately
                    if (delayMs > 0)
                    {
                        LogForm.Instance.AppendDebug($"[SpecialAnims] Message matched, will cast skill {resolvedSkillIndex} after {delayMs}ms delay");
                        // Fire and forget - don't await, so bot continues immediately
                        _ = Task.Run(async () => await DelayedCastSkill(resolvedSkillIndex, delayMs));
                    }
                    else
                    {
                        // No delay - cast immediately
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
                    }
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
            }

            // Let the bot continue with the next command (or the one after, if we skipped)
            return;
        }

        public override string ToString()
        {
            string result = $"Special Anims: {Value1}";
            if (!string.IsNullOrEmpty(Value2))
                result += $" | Skill: {Value2}";
            if (!string.IsNullOrEmpty(TauntOrder))
                result += $" | TauntOrder: {TauntOrder}";
            if (!string.IsNullOrEmpty(Delay))
                result += $" | Delay: {Delay}ms";
            return result;
        }

        private async Task DelayedCastSkill(string skillIndex, int delayMs)
        {
            try
            {
                // Wait for the delay
                await Task.Delay(delayMs);

                // Wait for skill to be off cooldown (if necessary), but don't stall forever
                int attempts = 3;
                while (attempts-- > 0)
                {
                    int cd = Player.SkillAvailable(skillIndex);
                    if (cd <= 0)
                        break;

                    await Task.Delay(Math.Min(cd, 1000));
                }

                // First attempt to force-cast
                Player.ForceUseSkill(skillIndex);

                // Short delay, then verify by checking if the skill went on cooldown
                await Task.Delay(150);
                if (Player.SkillAvailable(skillIndex) <= 0)
                {
                    // If it's still instantly available, try one more time
                    LogForm.Instance.AppendDebug($"[SpecialAnims] Skill {skillIndex} appears not to have fired, retrying once.");
                    Player.ForceUseSkill(skillIndex);
                }
            }
            catch (Exception ex)
            {
                LogForm.Instance.AppendDebug($"[SpecialAnims] Error during delayed skill cast {skillIndex}: {ex.Message}");
            }
        }
    }
}
