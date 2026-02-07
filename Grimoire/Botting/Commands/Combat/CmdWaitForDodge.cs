using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.Game.Data;

namespace Grimoire.Botting.Commands.Combat
{
    public class CmdWaitForDodge : IBotCommand
    {
        public int TimeoutMs { get; set; } = 30000; // Default 30 seconds
        
        public bool ContinueOnTimeout { get; set; } = false;

        // Instance-level TaskCompletionSource to properly handle async waiting
        private TaskCompletionSource<bool> _dodgeCompletionSource;

        public async Task Execute(IBotEngine instance)
        {
            // Create completion source FIRST to avoid race condition where NotifyDodge 
            // is called before _dodgeCompletionSource exists
            _dodgeCompletionSource = new TaskCompletionSource<bool>();
            
            // Register this command to receive the next dodge
            DodgeDetector.RegisterWaiter(this);
            
            // Now log with correct queue position (after registering)
            UI.LogForm.Instance.AppendDebug($"[WaitForDodge] Waiting for player dodge (timeout: {TimeoutMs}ms, Queue position: {DodgeDetector.GetQueuePosition(this)})...");
            
            try
            {
                // Wait for either dodge or timeout
                var dodgeTask = _dodgeCompletionSource.Task;
                var timeoutTask = Task.Delay(TimeoutMs);
                
                var completedTask = await Task.WhenAny(dodgeTask, timeoutTask);
                
                if (completedTask == dodgeTask && dodgeTask.Result)
                {
                    UI.LogForm.Instance.AppendDebug($"[WaitForDodge] ✓ Dodge detected! Continuing...");
                }
                else
                {
                    if (ContinueOnTimeout)
                    {
                        UI.LogForm.Instance.AppendDebug($"[WaitForDodge] ⏱ Timeout reached. Continuing anyway...");
                    }
                    else
                    {
                        UI.LogForm.Instance.AppendDebug($"[WaitForDodge] ⏱ Timeout reached. Stopping bot...");
                        instance.Stop();
                    }
                }
            }
            finally
            {
                // Unregister this command from the queue
                DodgeDetector.UnregisterWaiter(this);
            }
        }

        internal void NotifyDodge()
        {
            UI.LogForm.Instance.AppendDebug($"[WaitForDodge] 🛡️ Dodge notified!");
            _dodgeCompletionSource?.TrySetResult(true);
        }

        public override string ToString()
        {
            return $"Wait for Dodge ({TimeoutMs}ms, {(ContinueOnTimeout ? "Continue" : "Stop")} on timeout)";
        }
    }

    // Queue-based dodge detection to ensure each dodge only satisfies the first waiting command
    public static class DodgeDetector
    {
        private static Queue<CmdWaitForDodge> _waitingCommands = new Queue<CmdWaitForDodge>();
        private static Queue<ISkillWaiter> _skillWaiters = new Queue<ISkillWaiter>();
        private static object _lock = new object();
        
        // Dodge buffering: stores the most recent dodge with a timestamp
        private static DateTime? _lastDodgeTime = null;
        private static readonly TimeSpan DODGE_BUFFER_WINDOW = TimeSpan.FromMilliseconds(500);

        public static void RegisterWaiter(CmdWaitForDodge command)
        {
            lock (_lock)
            {
                _waitingCommands.Enqueue(command);
                UI.LogForm.Instance.AppendDebug($"[DodgeDetector] Registered CmdWaitForDodge. Queue size: {_waitingCommands.Count}");
            }
        }

        public static void UnregisterWaiter(CmdWaitForDodge command)
        {
            lock (_lock)
            {
                var temp = new Queue<CmdWaitForDodge>();
                while (_waitingCommands.Count > 0)
                {
                    var cmd = _waitingCommands.Dequeue();
                    if (cmd != command)
                        temp.Enqueue(cmd);
                }
                _waitingCommands = temp;
            }
        }

        public static int GetQueuePosition(CmdWaitForDodge command)
        {
            lock (_lock)
            {
                int position = 0;
                foreach (var cmd in _waitingCommands)
                {
                    position++;
                    if (cmd == command)
                        return position;
                }
                return -1;
            }
        }

        // Register skill waiter (from Skill.useSkill)
        public static void RegisterSkillWaiter(ISkillWaiter waiter)
        {
            lock (_lock)
            {
                // Check if there was a recent dodge within the buffer window
                if (_lastDodgeTime.HasValue && 
                    DateTime.Now - _lastDodgeTime.Value < DODGE_BUFFER_WINDOW)
                {
                    UI.LogForm.Instance.AppendDebug($"[DodgeDetector] 🛡️ Recent dodge found in buffer ({(DateTime.Now - _lastDodgeTime.Value).TotalMilliseconds:F0}ms ago) - using it for skill waiter!");
                    _lastDodgeTime = null; // Consume the buffered dodge
                    
                    // Immediately notify this waiter (fire and forget on background thread)
                    Task.Run(() => waiter.NotifyDodge());
                    return;
                }
                
                _skillWaiters.Enqueue(waiter);
                UI.LogForm.Instance.AppendDebug($"[DodgeDetector] Registered skill waiter. Queue size: {_skillWaiters.Count}");
            }
        }

        public static void UnregisterSkillWaiter(ISkillWaiter waiter)
        {
            lock (_lock)
            {
                var temp = new Queue<ISkillWaiter>();
                while (_skillWaiters.Count > 0)
                {
                    var w = _skillWaiters.Dequeue();
                    if (w != waiter)
                        temp.Enqueue(w);
                }
                _skillWaiters = temp;
            }
        }

        // Call this from your packet handler when a dodge is detected
        public static void NotifyDodge(string targetInfo)
        {
            // Verify it's the player who dodged (tInf should contain player ID)
            if (targetInfo != null && targetInfo.StartsWith("p:"))
            {
                // Optional: Verify it's YOUR player (uncomment for multiplayer safety)
                // try
                // {
                //     string playerUserID = Grimoire.Tools.Flash.Call<string>("UserID");
                //     string expectedPlayerInfo = $"p:{playerUserID}";
                //     if (targetInfo != expectedPlayerInfo)
                //     {
                //         UI.LogForm.Instance.AppendDebug($"[DodgeDetector] 🛡️ Dodge detected but not for YOUR character. TargetInfo: {targetInfo}");
                //         return;
                //     }
                // }
                // catch { }
                
                //UI.LogForm.Instance.AppendDebug($"[DodgeDetector] 🛡️ Player dodge detected! TargetInfo: {targetInfo}");
                
                lock (_lock)
                {
                    _lastDodgeTime = DateTime.Now; // Store dodge time
                    
                    // Notify the first CmdWaitForDodge in queue
                    if (_waitingCommands.Count > 0)
                    {
                        var command = _waitingCommands.Dequeue();
                        _lastDodgeTime = null; // Dodge consumed - clear buffer
                        UI.LogForm.Instance.AppendDebug($"[DodgeDetector] Notifying CmdWaitForDodge. Remaining in queue: {_waitingCommands.Count}");
                        command.NotifyDodge();
                    }
                    // Also notify the first skill waiter
                    else if (_skillWaiters.Count > 0)
                    {
                        var waiter = _skillWaiters.Dequeue();
                        _lastDodgeTime = null; // Dodge consumed - clear buffer
                        UI.LogForm.Instance.AppendDebug($"[DodgeDetector] Notifying skill waiter. Remaining in queue: {_skillWaiters.Count}");
                        waiter.NotifyDodge();
                    }
                    else
                    {
                        //UI.LogForm.Instance.AppendDebug($"[DodgeDetector] No waiters queued - dodge buffered for {DODGE_BUFFER_WINDOW.TotalMilliseconds}ms");
                    }
                }
            }
            else
            {
                UI.LogForm.Instance.AppendDebug($"[DodgeDetector] ❌ Non-player dodge detected (ignored): {targetInfo}");
            }
        }

        // Reset all waiters and clear queues (call when stopping auto attack)
        public static void Reset()
        {
            lock (_lock)
            {
                UI.LogForm.Instance.AppendDebug($"[DodgeDetector] 🔄 Resetting - clearing {_waitingCommands.Count} commands and {_skillWaiters.Count} skill waiters");
                _waitingCommands.Clear();
                _skillWaiters.Clear();
                _lastDodgeTime = null; // Clear any buffered dodge
            }
        }
    }
}
