using Grimoire.Game;
using Grimoire.UI;
using System;
using System.Collections.Generic;
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
            // Value3 = optional attack priority (monmapid, monster name, etc.) to target before casting the skill
            // TauntOrder (Index) = optional occurrence number to taunt on (e.g., 1 = first message, 2 = second message, etc.)
            //         If not specified or <= 0, taunt on every message
            // Label (Account Total) = optional account rotation count

            string raw = instance.ResolveVars(Value1);
            string lastMessage = Configuration.LastAnimationMessage?.ToLower();

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

            // Always consume the message if it exists (whether it matched or not)
            // This prevents the same message from being checked repeatedly
            if (!string.IsNullOrEmpty(lastMessage))
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

            // Handle Account Total (Label)
            int accountTotal = 0;
            if (!string.IsNullOrWhiteSpace(Label))
            {
                string resolvedLabel = instance.ResolveVars(Label);
                if (!string.IsNullOrWhiteSpace(resolvedLabel) && int.TryParse(resolvedLabel, out int parsedLabel))
                {
                    accountTotal = parsedLabel;
                }
            }

            // If Index is specified, only trigger when it's this account's turn in the rotation
            if (matched && targetIndex > 0)
            {
                _messageCount++;
                
                if (accountTotal > 0)
                {
                    // Rotation mode: Player N triggers when (_messageCount - 1) % accountTotal + 1 == targetIndex
                    // E.g., with accountTotal=4: P1 on 1,5,9... P2 on 2,6,10... P3 on 3,7,11... P4 on 4,8,12...
                    matched = ((_messageCount - 1) % accountTotal + 1 == targetIndex);
                    if (matched)
                        LogForm.Instance.AppendDebug($"[SpecialAnims] Message #{_messageCount}, TauntOrder={targetIndex}, AccountTotal={accountTotal}, matched=True");
                }
                else
                {
                    // No account total specified: simple cyclic triggering
                    // Index 1 triggers on all messages, Index 2 every 2nd, Index 3 every 3rd, etc.
                    matched = (_messageCount % targetIndex == 0);
                    if (matched)
                        LogForm.Instance.AppendDebug($"[SpecialAnims] Message #{_messageCount}, TauntOrder={targetIndex}, matched=True");
                }
            }
            else if (matched && targetIndex <= 0)
            {
                // No index specified - increment counter for reference but always match
                _messageCount++;
                LogForm.Instance.AppendDebug($"[SpecialAnims] Message #{_messageCount} received: '{lastMessage}'");
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

            string resolvedAttackPriority = null;
            if (!string.IsNullOrWhiteSpace(Value3))
            {
                resolvedAttackPriority = instance.ResolveVars(Value3);
                if (string.IsNullOrWhiteSpace(resolvedAttackPriority))
                    resolvedAttackPriority = null;
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
                    // First, target the attack priority if specified
                    if (!string.IsNullOrWhiteSpace(resolvedAttackPriority))
                    {
                        try
                        {
                            // Handle comma-separated list of attack priorities (like CmdKill does)
                            List<string> priorities = new List<string>();
                            if (resolvedAttackPriority.Contains(","))
                            {
                                foreach (string p in resolvedAttackPriority.Split(','))
                                {
                                    priorities.Add(p.Trim());
                                }
                            }
                            else
                            {
                                priorities.Add(resolvedAttackPriority.Trim());
                            }

                            // Attack the first available priority target
                            foreach (string p in priorities)
                            {
                                if (World.IsMonsterAvailable(p))
                                {
                                    LogForm.Instance.AppendDebug($"[SpecialAnims] Attacking priority: {p}");
                                    Player.AttackMonster(p);
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogForm.Instance.AppendDebug($"[SpecialAnims] Error while setting attack priority {resolvedAttackPriority}: {ex.Message}");
                        }
                    }

                    // Now handle the skill cast with optional delay
                    if (delayMs > 0)
                    {
                        LogForm.Instance.AppendDebug($"[SpecialAnims] Message matched, waiting {delayMs}ms before pausing and casting skill {resolvedSkillIndex}");
                        // Fire off in background: wait for delay, THEN pause and cast
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(delayMs);
                            instance.paused = true;
                            await CastSkillImmediate(resolvedSkillIndex);
                            instance.paused = false;
                        });
                    }
                    else
                    {
                        // No delay - pause and cast immediately
                        instance.paused = true;
                        try
                        {
                            await CastSkillImmediate(resolvedSkillIndex);
                            instance.paused = false;
                        }
                        catch (Exception ex)
                        {
                            LogForm.Instance.AppendDebug($"[SpecialAnims] Error while forcing skill {resolvedSkillIndex}: {ex.Message}");
                            instance.paused = false;
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
            if (!string.IsNullOrEmpty(Value3))
                result += $" | Attack Priority: {Value3}";
            if (!string.IsNullOrEmpty(TauntOrder))
                result += $" | TauntOrder: {TauntOrder}";
            if (!string.IsNullOrEmpty(Delay))
                result += $" | Delay: {Delay}ms";
            return result;
        }

        private async Task CastSkillImmediate(string skillIndex)
        {
            try
            {
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
                LogForm.Instance.AppendDebug($"[SpecialAnims] Error during skill cast {skillIndex}: {ex.Message}");
            }
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
                else
                {
                    // Skill didn't fire - pause the bot for user attention
                    LogForm.Instance.AppendDebug($"[SpecialAnims] Delayed skill {skillIndex} failed to fire, pausing bot.");
                }
            }
            catch (Exception ex)
            {
                LogForm.Instance.AppendDebug($"[SpecialAnims] Error during delayed skill cast {skillIndex}: {ex.Message}");
            }
        }
    }
}
