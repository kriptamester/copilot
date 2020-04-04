﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Static;
using SharpDX;
using static CoPilot.WinApiMouse;
using Point = CoPilot.WinApiMouse.Point;
using System.Windows.Input;
using ImGuiNET;

namespace CoPilot
{
    public class CoPilot : BaseSettingsPlugin<CoPilotSettings>
    {
        Stats stats = new Stats();
        private readonly int mouseAutoSnapRange = 250;
        private DateTime lastPhaserun = new DateTime();
        private DateTime lastMoltenShell = new DateTime();
        private DateTime lastWarCry = new DateTime();
        private DateTime lastTimeAny = new DateTime();
        private DateTime lastDelveFlare = new DateTime();
        private DateTime lastVortex = new DateTime();
        private DateTime lastBloodRage = new DateTime();
        private DateTime lastStats = new DateTime();
        private DateTime lastdoedreEffigy = new DateTime();
        private DateTime lastFlask = new DateTime();
        private DateTime lastStackSkill = new DateTime();
        private readonly int delay = 70;
        private IEnumerable<Entity> enemys;
        private IEnumerable<Entity> corpses;
        Vector3 playerPosition;
        private Entity flask4;
        private Entity flask5;
        private DateTime autoAttackRunning = new DateTime();
        private DateTime autoAttackUpdate = new DateTime();
        private int golemsAlive;

        private Coroutine CoroutineWorker;
        private const string coroutineKeyPress = "KeyPress";


        private void KeyPress(Keys key, bool anyDelay = true)
        {
            if (anyDelay)
                lastTimeAny = DateTime.Now;
            if (CoroutineWorker != null && !CoroutineWorker.IsDone)
                return;
            CoroutineWorker = new Coroutine(KeyPressRoutine(key), this, coroutineKeyPress);
            Core.ParallelRunner.Run(CoroutineWorker);
        }

        private static IEnumerator KeyPressRoutine(Keys key)
        {
            Keyboard.KeyDown(key);
            yield return new WaitTime(20);
            Keyboard.KeyUp(key);
        }
        public override bool Initialise()
        {
            GameController.LeftPanel.WantUse(() => Settings.Enable);
            return true;
        }
        public int GetMonsterWithin(float maxDistance)
        {
            int count = 0;
            float maxDistanceSquare = maxDistance * maxDistance;
            foreach (var monster in enemys)
            {
                if (monster.Path.Contains("Summoned")) { continue; }
                var monsterPosition = monster.Pos;

                var xDiff = playerPosition.X - monsterPosition.X;
                var yDiff = playerPosition.Y - monsterPosition.Y;
                var monsterDistanceSquare = (xDiff * xDiff + yDiff * yDiff);

                if (monsterDistanceSquare <= maxDistanceSquare)
                {
                    count++;
                }

            }
            return count;
        }

        public int CountCorpsesAroundMouse(float maxDistance)
        {
            int count = 0;
            float maxDistanceSquare = maxDistance * maxDistance;
            foreach (var corpse in corpses)
            {
                var monsterPosition = corpse.Pos;
                var screenPosition = GameController.IngameState.Camera.WorldToScreen(monsterPosition);
                var cursorPosition = MouseTools.GetCursorPosition();

                var xDiff = screenPosition.X - cursorPosition.X;
                var yDiff = screenPosition.Y - cursorPosition.Y;
                var monsterDistanceSquare = (xDiff * xDiff + yDiff * yDiff);

                if (monsterDistanceSquare <= maxDistanceSquare)
                {
                    count++;
                }
            }
            //LogMessage("Total Corpses: " + corpses.Count().ToString() + " Valid Corpses Counted: " + count.ToString());
            return count;
        }
        public int CountEnemysAroundMouse(float maxDistance)
        {
            int count = 0;
            float maxDistanceSquare = maxDistance * maxDistance;
            foreach (var enemy in enemys)
            {
                var monsterPosition = enemy.Pos;
                var screenPosition = GameController.IngameState.Camera.WorldToScreen(monsterPosition);
                var cursorPosition = MouseTools.GetCursorPosition();

                var xDiff = screenPosition.X - cursorPosition.X;
                var yDiff = screenPosition.Y - cursorPosition.Y;
                var monsterDistanceSquare = (xDiff * xDiff + yDiff * yDiff);

                if (monsterDistanceSquare <= maxDistanceSquare)
                {
                    count++;
                }
            }
            //LogMessage("Total Enemys: " + enemy.Count().ToString() + " Valid Enemys Counted: " + count.ToString());
            return count;
        }

        // Taken from ->
        // https://www.reddit.com/r/pathofexiledev/comments/787yq7/c_logout_app_same_method_as_lutbot/
        public static partial class CommandHandler
        {
            public static void KillTCPConnectionForProcess(int ProcessId)
            {
                MibTcprowOwnerPid[] table;
                var afInet = 2;
                var buffSize = 0;
                var ret = GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, afInet, TcpTableClass.TcpTableOwnerPidAll);
                var buffTable = Marshal.AllocHGlobal(buffSize);
                try
                {
                    ret = GetExtendedTcpTable(buffTable, ref buffSize, true, afInet, TcpTableClass.TcpTableOwnerPidAll);
                    if (ret != 0)
                        return;
                    var tab = (MibTcptableOwnerPid)Marshal.PtrToStructure(buffTable, typeof(MibTcptableOwnerPid));
                    var rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.dwNumEntries));
                    table = new MibTcprowOwnerPid[tab.dwNumEntries];
                    for (var i = 0; i < tab.dwNumEntries; i++)
                    {
                        var tcpRow = (MibTcprowOwnerPid)Marshal.PtrToStructure(rowPtr, typeof(MibTcprowOwnerPid));
                        table[i] = tcpRow;
                        rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(tcpRow));

                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffTable);
                }

                //Kill Path Connection
                var PathConnection = table.FirstOrDefault(t => t.owningPid == ProcessId);
                PathConnection.state = 12;
                var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(PathConnection));
                Marshal.StructureToPtr(PathConnection, ptr, false);
                SetTcpEntry(ptr);


            }

            [DllImport("iphlpapi.dll", SetLastError = true)]
            private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, TcpTableClass tblClass, uint reserved = 0);

            [DllImport("iphlpapi.dll")]
            private static extern int SetTcpEntry(IntPtr pTcprow);

            [StructLayout(LayoutKind.Sequential)]
            public struct MibTcprowOwnerPid
            {
                public uint state;
                public uint localAddr;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] localPort;
                public uint remoteAddr;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] remotePort;
                public uint owningPid;

            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MibTcptableOwnerPid
            {
                public uint dwNumEntries;
                private readonly MibTcprowOwnerPid table;
            }

            private enum TcpTableClass
            {
                TcpTableBasicListener,
                TcpTableBasicConnections,
                TcpTableBasicAll,
                TcpTableOwnerPidListener,
                TcpTableOwnerPidConnections,
                TcpTableOwnerPidAll,
                TcpTableOwnerModuleListener,
                TcpTableOwnerModuleConnections,
                TcpTableOwnerModuleAll
            }
        }

        public bool Ready()
        {
            if ((DateTime.Now - lastTimeAny).TotalMilliseconds > delay)
                return true;
            else
                return false;
        }

        public void Quit()
        {
            try
            {
                CommandHandler.KillTCPConnectionForProcess(GameController.Window.Process.Id);
            }
            catch (Exception e)
            {
                LogError($"{e}");
            }
            
        }
        public override void DrawSettings()
        {
            //base.DrawSettings();

            System.Numerics.Vector4 green = new System.Numerics.Vector4(0.102f, 0.388f, 0.106f, 1.000f);
            System.Numerics.Vector4 red = new System.Numerics.Vector4(0.388f, 0.102f, 0.102f, 1.000f);

            ImGuiTreeNodeFlags collapsingHeaderFlags = ImGuiTreeNodeFlags.CollapsingHeader;
            ImGui.Text("Plugin by Totalschaden. https://github.com/totalschaden/copilot");

            // Auto Attack
            if (Settings.autoAttackEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Auto Attack with Cyclone / Nova / ...", collapsingHeaderFlags))
            {
                Settings.autoAttackEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.autoAttackEnabled);
                Settings.autoAttackLeftMouseCheck.Value = ImGuiExtension.Checkbox("Pause on Left Mouse Pressed", Settings.autoAttackLeftMouseCheck);
                Settings.autoAttackKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.autoAttackKey.Value, Settings.autoAttackKey);
                Settings.autoAttackPickItKey.Value = ImGuiExtension.HotkeySelector("PickIt Key: " + Settings.autoAttackPickItKey.Value, Settings.autoAttackPickItKey);
                Settings.autoAttackRange.Value = ImGuiExtension.IntSlider("Range", Settings.autoAttackRange);
            }

            // Auto Golem
            if (Settings.autoGolemEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Auto Golem", collapsingHeaderFlags))
            {
                Settings.autoGolemEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.autoGolemEnabled);
                Settings.autoGolemKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.autoGolemKey.Value, Settings.autoGolemKey);
                Settings.autoGolemMax.Value = ImGuiExtension.IntSlider("Max. Golems", Settings.autoGolemMax);
            }


            // Auto Quit
            if (Settings.autoQuitEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Auto Quit", collapsingHeaderFlags))
            {
                Settings.autoQuitEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.autoQuitEnabled);
                Settings.hpPctQuit.Value = ImGuiExtension.FloatSlider("HP%", Settings.hpPctQuit);
                Settings.esPctQuit.Value = ImGuiExtension.FloatSlider("ES%", Settings.esPctQuit);
                Settings.forcedAutoQuit.Value = ImGuiExtension.HotkeySelector("Force Quit Key: " + Settings.forcedAutoQuit.Value, Settings.forcedAutoQuit);
            }


            // Blood Rage
            if (Settings.bloodRageEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Blood Rage", collapsingHeaderFlags))
            {
                Settings.bloodRageEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.bloodRageEnabled);
                Settings.bloodRageKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.bloodRageKey.Value, Settings.bloodRageKey);
                Settings.bloodRageDelay.Value = ImGuiExtension.IntSlider("Delay", Settings.bloodRageDelay);
                Settings.bloodRageRange.Value = ImGuiExtension.IntSlider("Range", Settings.bloodRageRange);
            }


            if (Settings.delveFlareEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Delve Flare", collapsingHeaderFlags))
            {
                Settings.delveFlareEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.delveFlareEnabled);
                Settings.delveFlareKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.delveFlareKey.Value, Settings.delveFlareKey);
                Settings.delveFlareDebuffStacks.Value = ImGuiExtension.IntSlider("min. Debuff Stacks", Settings.delveFlareDebuffStacks);
                Settings.delveFlareHpBelow.Value = ImGuiExtension.FloatSlider("HP%", Settings.delveFlareHpBelow);
                Settings.delveFlareEsBelow.Value = ImGuiExtension.FloatSlider("ES%", Settings.delveFlareEsBelow);
            }


            // Doedre Effigy
            if (Settings.doedreEffigyEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Doedre Effigy", collapsingHeaderFlags))
            {
                Settings.doedreEffigyEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.doedreEffigyEnabled);
                Settings.doedreEffigyKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.doedreEffigyKey.Value, Settings.doedreEffigyKey);
                Settings.doedreEffigyDelay.Value = ImGuiExtension.IntSlider("min. Debuff Stacks", Settings.doedreEffigyDelay);
            }


            if (Settings.divineIreEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Divine Ire / Blade Flurry / Scourge Arrow", collapsingHeaderFlags))
            {
                Settings.divineIreEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.divineIreEnabled);
                Settings.divineIreKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.divineIreKey.Value, Settings.divineIreKey);
                Settings.divineIreStacks.Value = ImGuiExtension.IntSlider("Stacks", Settings.divineIreStacks);
                Settings.divineIreWaitForInfused.Value = ImGuiExtension.Checkbox("Wait for Infused Channeling Support", Settings.divineIreWaitForInfused);


            }


            // Warcrys
            if (Settings.enduringCryEnabled || Settings.rallyingCryEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Enduring Cry / Rallying Cry", collapsingHeaderFlags))
            {
                Settings.warCryDelay.Value = ImGuiExtension.IntSlider("Cooldown", Settings.warCryDelay);
                Settings.enduringCryEnabled.Value = ImGuiExtension.Checkbox("Enduring Cry Enabled", Settings.enduringCryEnabled);
                Settings.enduringCryKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.enduringCryKey.Value, Settings.enduringCryKey);
                Settings.enduringCryRange.Value = ImGuiExtension.IntSlider("Range", Settings.enduringCryRange);
                Settings.rallyingCryEnabled.Value = ImGuiExtension.Checkbox("Rallying Cry Enabled", Settings.rallyingCryEnabled);
                Settings.rallyingCryKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.rallyingCryKey.Value, Settings.rallyingCryKey);
                Settings.rallyingCryRange.Value = ImGuiExtension.IntSlider("Range", Settings.rallyingCryRange);

            }


            // Molten Shell / Steelskin / Bone Armour / Arcane Cloak
            if (Settings.moltenShellEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Molten Shell / Steelskin / Bone Armour / Arcane Cloak", collapsingHeaderFlags))
            {
                Settings.moltenShellEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.moltenShellEnabled);
                Settings.moltenShellKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.moltenShellKey.Value, Settings.moltenShellKey);
                Settings.moltenShellDelay.Value = ImGuiExtension.IntSlider("Cooldown", Settings.moltenShellDelay);
                Settings.moltenShellRange.Value = ImGuiExtension.IntSlider("Range", Settings.moltenShellRange);
            }


            // Offerings
            if (Settings.offeringsEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Offerings (This will get you stuck in Animation for your Casttime !)", collapsingHeaderFlags))
            {
                Settings.offeringsEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.offeringsEnabled);
                Settings.offeringsKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.offeringsKey.Value, Settings.offeringsKey);
            }


            // Phaserun
            if (Settings.phaserunEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Phaserun", collapsingHeaderFlags))
            {
                Settings.phaserunEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.phaserunEnabled);
                Settings.phaserunKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.phaserunKey.Value, Settings.phaserunKey);
                Settings.phaserunDelay.Value = ImGuiExtension.IntSlider("Cooldown", Settings.phaserunDelay);
            }

            // Speed Flasks
            if (Settings.useSpeed4 || Settings.useSpeed5)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Speed Flask", collapsingHeaderFlags))
            {
                Settings.useSpeed4.Value = ImGuiExtension.Checkbox("Flask 4 Enabled", Settings.useSpeed4);
                Settings.useSpeed5.Value = ImGuiExtension.Checkbox("Flask 5 Enabled", Settings.useSpeed5);
                Settings.useSpeedMoving.Value = ImGuiExtension.Checkbox("Use when Moving", Settings.useSpeedMoving);
                Settings.useSpeedAttack.Value = ImGuiExtension.Checkbox("Use when Attacking", Settings.useSpeedAttack);
            }


            // Vortex
            if (Settings.vortexEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Vortex", collapsingHeaderFlags))
            {
                Settings.vortexEnabled.Value = ImGuiExtension.Checkbox("Enabled", Settings.vortexEnabled);
                Settings.vortexKey.Value = ImGuiExtension.HotkeySelector("Key: " + Settings.vortexKey.Value, Settings.vortexKey);
                Settings.vortexDelay.Value = ImGuiExtension.IntSlider("Cooldown", Settings.vortexDelay);
                Settings.vortexRange.Value = ImGuiExtension.IntSlider("Range", Settings.vortexRange);
            }
     


            if (Settings.minesEnabled)
                ImGui.PushStyleColor(ImGuiCol.Header, green);
            else
                ImGui.PushStyleColor(ImGuiCol.Header, red);
            if (ImGui.TreeNodeEx("Mines (Not Implemented)", collapsingHeaderFlags))
            {
                //Settings.enduringCryEnabled = 
            }
        }
        public override void Render()
        {
            if (Settings.Enable)
            {
                // Update Stats
                /*
                if ((DateTime.Now - lastStats).TotalMilliseconds >= 1000)
                {
                    stats.Update();
                    lastStats = DateTime.Now;
                }*/
                if ((WinApi.GetAsyncKeyState(Settings.forcedAutoQuit) & 0x8000) != 0)
                {
                    LogMessage("Panic Quit...");
                    Quit();
                }                

                if (!GameController.Area.CurrentArea.IsHideout && !GameController.Area.CurrentArea.IsTown && !GameController.IngameState.IngameUi.ChatBox.IsVisible /*&& !IngameUi.StashElement.IsVisible && !IngameUi.OpenRightPanel.IsVisible*/ )
                {

                    var localPlayer = GameController.Game.IngameState.Data.LocalPlayer;
                    var player = localPlayer.GetComponent<Life>();
                    var buffs = player.Buffs;
                    var isAttacking = localPlayer.GetComponent<Actor>().isAttacking;
                    var isCasting = localPlayer.GetComponent<Actor>().Action.HasFlag(ActionFlags.UsingAbility);
                    var isMoving = localPlayer.GetComponent<Actor>().isMoving;

                    playerPosition = GameController.Player.Pos;
                    enemys = GameController.Entities.Where(x => x.IsValid && x.IsHostile && !x.IsHidden && !x.IsDead && x.IsAlive && x.GetComponent<Monster>() != null && x.GetComponent<Life>().CurHP > 0 && !x.Buffs.Exists(b => b.Name == "hidden_monster_disable_minions"));
                    corpses = GameController.Entities.Where(x => x.IsHostile && x.GetComponent<Monster>() != null && x.IsDead && x.IsTargetable);


                    // Chest isTargetable && !isOpen && isHostile
                    if (Settings.autoGolemEnabled)
                        golemsAlive = GameController.Entities.Where(x => !x.IsHostile && (x.Path.Contains("ChaosElemental") || x.Path.Contains("FireElemental") || x.Path.Contains("IceElemental") || x.Path.Contains("LightningGolem") || x.Path.Contains("RockGolem") || x.Path.Contains("BoneGolem") || x.Path.Contains("DropBearUniqueSummoned"))).Count();


                    // Feature request
                    //  LeHeupOfSoupheute um 19:49 Uhr
                    //  Would it be possible to add convocate on nearby enemies? Pretty please

                    // Maybe someone will add proper Skill API in the future ?
                    // Simple Loop through Skills available, could move regions into this
                    //var skills = RemoteMemoryObject.pTheGame.IngameState.Data.LocalPlayer.GetComponent<Actor>().ActorSkills;
                    /*
                    foreach (var skill in skills)
                    {
                        // Add this when there is proper cooldown checks
                        // If skill.name == "Baboom of Death" && skill.isReady
                    }
                    */

                    #region Auto Quit
                    if (Settings.autoQuitEnabled)
                    {
                        try
                        {
                            if (localPlayer.IsValid)
                            {
                                if ((Math.Round(player.HPPercentage, 3) * 100 < Settings.hpPctQuit.Value || player.MaxES > 0 && (Math.Round(player.ESPercentage, 3) * 100 < Settings.esPctQuit.Value)))
                                {
                                    Quit();
                                }
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Offerings

                    if (Settings.offeringsEnabled)
                    {
                        try
                        {
                            if (Ready() && !buffs.Exists(x => x.Name == "active_offering" && x.Timer > 0.3) && CountCorpsesAroundMouse(mouseAutoSnapRange) > 0)
                            {
                                KeyPress(Settings.offeringsKey);
                            }
                        }
                        catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Rallying Cry
                    if (Settings.rallyingCryEnabled)
                    {
                        try
                        {
                            if (Ready() && (DateTime.Now - lastWarCry).TotalMilliseconds > Settings.warCryDelay.Value && (GetMonsterWithin(Settings.rallyingCryRange) >= 1))
                            {
                                if (Settings.enduringCryEnabled && (!buffs.Exists(x => x.Name == "inspiring_cry") || buffs.Exists(x => x.Name == "inspiring_cry" && x.Timer < 3.13)))
                                {
                                    KeyPress(Settings.rallyingCryKey.Value);
                                    lastWarCry = DateTime.Now;
                                }
                                else if (!Settings.enduringCryEnabled)
                                {
                                    KeyPress(Settings.rallyingCryKey.Value);
                                    lastWarCry = DateTime.Now;
                                }
                            }                            
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Enduring Cry
                    if (Settings.enduringCryEnabled)
                    {
                        try
                        {
                            if (Ready() && (DateTime.Now - lastWarCry).TotalMilliseconds > Settings.warCryDelay.Value && (GetMonsterWithin(Settings.enduringCryRange) >= 1 || player.HPPercentage < 0.90))
                            {
                                KeyPress(Settings.enduringCryKey.Value);
                                lastWarCry = DateTime.Now;
                            }
                            
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Phase Run
                    if (Settings.phaserunEnabled)
                    {
                        try
                        {
                            if (!isAttacking && isMoving && Ready() && (DateTime.Now - lastPhaserun).TotalMilliseconds > Settings.phaserunDelay.Value)
                            {
                                if (!buffs.Exists(b => b.Name == "new_phase_run" || buffs.Exists(x => x.Name == "new_phase_run" && x.Timer < 0.013)))
                                {
                                    KeyPress(Settings.phaserunKey.Value);
                                    lastPhaserun = DateTime.Now;
                                }
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Molten Shell / Steelskin / Bone Armour / Arcane Cloak
                    if (Settings.moltenShellEnabled)
                    {
                        try
                        {
                            // Cooldown reset starts on Buff expire
                            if (buffs.Exists(x => x.Name == "fire_shield") || buffs.Exists(x => x.Name == "quick_guard") || buffs.Exists(x => x.Name == "bone_armour") || buffs.Exists(x => x.Name == "arcane_cloak") )
                            {
                                lastMoltenShell = DateTime.MaxValue;
                            }
                            else
                            {
                                if (lastMoltenShell == DateTime.MaxValue)
                                {
                                    lastMoltenShell = DateTime.Now;
                                }
                            }
                            if (Ready() && (DateTime.Now - lastMoltenShell).TotalMilliseconds > Settings.moltenShellDelay.Value)
                            {
                                if ((GetMonsterWithin(Settings.moltenShellRange) >= 1))
                                {
                                    KeyPress(Settings.moltenShellKey.Value);
                                    lastMoltenShell = DateTime.MaxValue;
                                }
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Divine Ire
                    if (Settings.divineIreEnabled)
                    {
                        try
                        {
                            if ((DateTime.Now - lastStackSkill).TotalMilliseconds > 250 && 
                            buffs.Exists(b => (b.Name == "divine_tempest_stage" && b.Charges >= Settings.divineIreStacks.Value) || 
                            (b.Name == "charged_attack" && b.Charges >= Settings.divineIreStacks) || 
                            (b.Name == "virulent_arrow_counter" && b.Charges >= Settings.divineIreStacks)))
                            {

                                if (Settings.divineIreWaitForInfused) {
                                    // Get delay here at some point ?
                                    if (!buffs.Exists(x => x.Name == "storm_barrier_support_damage" && x.Timer > 1.0)) {
                                        return;
                                    }
                                }
                                if (Settings.divineIreKey == Keys.LButton)
                                    MouseTools.MouseLeftClickEvent();
                                else if (Settings.divineIreKey == Keys.RButton)
                                    MouseTools.MouseRightClickEvent();
                                else
                                {
                                    
                                    Keyboard.KeyUp(Settings.divineIreKey);
                                    lastStackSkill = DateTime.Now;
                                    if (Settings.debugMode)
                                        LogError("Release Key Pressed: " + Settings.divineIreKey.ToString());
                                }
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }

                    }
                    #endregion

                    #region Delve Flare
                    if (Settings.delveFlareEnabled)
                    {
                        try
                        {
                            if ((DateTime.Now - lastDelveFlare).TotalMilliseconds > 1000 && (player.ESPercentage < (Settings.delveFlareEsBelow / 100) || player.HPPercentage < (Settings.delveFlareHpBelow / 100)) && buffs.Exists(x => x.Name == "delve_degen_buff" && x.Charges >= Settings.delveFlareDebuffStacks))
                            {
                                KeyPress(Settings.delveFlareKey.Value);
                                lastDelveFlare = DateTime.Now;
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Vortex
                    if (Settings.vortexEnabled)
                    {
                        try
                        {
                            if (Ready() && (DateTime.Now - lastVortex).TotalMilliseconds > Settings.vortexDelay.Value && (GetMonsterWithin(Settings.vortexRange) >= 1))
                            {
                                KeyPress(Settings.vortexKey.Value);
                                lastVortex = DateTime.Now;
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Blood Rage
                    if (Settings.bloodRageEnabled)
                    {
                        try
                        {
                            if (Ready() && (DateTime.Now - lastBloodRage).TotalMilliseconds > Settings.bloodRageDelay.Value && !buffs.Exists(b => b.Name == "blood_rage" && b.Timer > 1.0) &&(GetMonsterWithin(Settings.bloodRageRange) >= 1))
                            {
                                KeyPress(Settings.bloodRageKey.Value);
                                lastBloodRage = DateTime.Now;
                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Doedre Effigy
                    if (Settings.doedreEffigyEnabled)
                    {
                        try
                        {
                            if ((DateTime.Now - lastdoedreEffigy).TotalMilliseconds > Settings.doedreEffigyDelay.Value && CountEnemysAroundMouse(350) > 0)
                            {
                                KeyPress(Settings.doedreEffigyKey.Value);
                                lastdoedreEffigy = DateTime.Now;
                            }
                        }
                        catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Speed Flask
                    if ((Settings.useSpeed4 || Settings.useSpeed5))
                    {
                        try
                        {
                            if ((DateTime.Now - lastFlask).TotalMilliseconds > 250 && ((Settings.useSpeedAttack && isAttacking) || (Settings.useSpeedMoving && isMoving)) && !buffs.Exists(x => x.Name == "flask_utility_sprint"))
                            {
                                flask4 = GameController.Game.IngameState.ServerData.PlayerInventories.FirstOrDefault(x => x.Inventory.InventType == InventoryTypeE.Flask)?.Inventory?.InventorySlotItems?.FirstOrDefault(x => x.InventoryPosition.X == 3)?.Item;
                                flask5 = GameController.Game.IngameState.ServerData.PlayerInventories.FirstOrDefault(x => x.Inventory.InventType == InventoryTypeE.Flask)?.Inventory?.InventorySlotItems?.FirstOrDefault(x => x.InventoryPosition.X == 4)?.Item;

                                if (Settings.useSpeed4 && Settings.useSpeed5 && flask4 != null && flask4.Address != 0x00 && flask5 != null && flask5.Address != 0x00)
                                {
                                    var charges4 = flask4.GetComponent<Charges>();
                                    var charges5 = flask5.GetComponent<Charges>();
                                    if (charges4.NumCharges >= charges4.ChargesPerUse && charges5.NumCharges >= charges5.ChargesPerUse)
                                    {
                                        if (charges4.NumCharges > charges5.NumCharges)
                                        {
                                            KeyPress(Keys.D4, false);
                                            lastFlask = DateTime.Now;
                                        }
                                        else if (charges5.NumCharges > charges4.NumCharges)
                                        {
                                            KeyPress(Keys.D5, false);
                                            lastFlask = DateTime.Now;
                                        }
                                        else
                                        {
                                            KeyPress(Keys.D4, false);
                                            lastFlask = DateTime.Now;
                                        }
                                    }

                                }
                                else if (Settings.useSpeed4 && flask4 != null && flask4.Address != 0x00)
                                {
                                    var charges4 = flask4.GetComponent<Charges>();
                                    if (charges4.NumCharges >= charges4.ChargesPerUse )
                                    {
                                        KeyPress(Keys.D4, false);
                                        lastFlask = DateTime.Now;
                                    }
                                }
                                else if (Settings.useSpeed5 && flask5 != null && flask5.Address != 0x00)
                                {
                                    var charges5 = flask5.GetComponent<Charges>();
                                    if (charges5.NumCharges >= charges5.ChargesPerUse )
                                    {
                                        KeyPress(Keys.D5, false);
                                        lastFlask = DateTime.Now;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Detonate Mines ( to be done )
                    if (Settings.minesEnabled)
                    {
                        try
                        {
                            if (Ready())
                            {
                                var remoteMines = localPlayer.GetComponent<Actor>().DeployedObjects.Where(x => x.Entity != null && x.Entity.Path == "Metadata/MiscellaneousObjects/RemoteMine").ToList();

                                // Removed Logic
                                // What should a proper Detonator do and when ?
                                // Detonate Mines when they have the chance to hit a target (Range), include min. mines ?
                                // Internal delay 500-1000ms ?
                                // Removing/Filter enemys that are not "deployed" yet / invunerale from enemys list ? 

                            }
                        } catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region AutoAttack Cyclone / Nova / etc.
                    if (Settings.autoAttackEnabled && (DateTime.Now - autoAttackUpdate).TotalMilliseconds > 50)
                    {
                        try
                        {
                            autoAttackUpdate = DateTime.Now;
                            if ((Settings.autoAttackLeftMouseCheck.Value && !MouseTools.IsMouseLeftPressed() || !Settings.autoAttackLeftMouseCheck.Value) &&
                                GetMonsterWithin(Settings.autoAttackRange) >= 1)
                            {
                                if (Keyboard.IsKeyDown((int)Settings.autoAttackPickItKey.Value) ||!isCasting && !isAttacking && autoAttackRunning > DateTime.MinValue && (DateTime.Now - autoAttackRunning).TotalMilliseconds > 100 && Keyboard.IsKeyDown((int)Settings.autoAttackKey.Value))
                                {
                                    Keyboard.KeyUp(Settings.autoAttackKey.Value);
                                    if (Settings.debugMode.Value)
                                        LogMessage("Detected Key Priority Problem due to User Input, fixing.");
                                }
                                if (!Keyboard.IsKeyDown((int)Settings.autoAttackKey.Value) && !Keyboard.IsKeyDown((int)Settings.autoAttackPickItKey.Value) )
                                {
                                    Keyboard.KeyDown(Settings.autoAttackKey.Value);
                                    autoAttackRunning = DateTime.Now;
                                }                                
                            } 
                            else if (Keyboard.IsKeyDown((int)Settings.autoAttackKey.Value))
                            {
                                Keyboard.KeyUp(Settings.autoAttackKey.Value);
                                autoAttackRunning = DateTime.MinValue;
                            }
                        }
                        catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion

                    #region Auto Golem
                    if (Settings.autoGolemEnabled)
                    {
                        try
                        {
                            if (Ready() && !isCasting && !isAttacking && golemsAlive < Settings.autoGolemMax.Value && GetMonsterWithin(600) == 0)
                            {
                                KeyPress(Settings.autoGolemKey.Value);
                            }
                        }
                        catch (Exception e)
                        {
                            LogError(e.ToString());
                        }
                    }
                    #endregion


                }
            }
        }
    }
    internal static class MouseTools
    {
        public static bool IsMouseLeftPressed()
        {
            if (Control.MouseButtons == MouseButtons.Left)
                return true;
            else
                return false;
        }
        public static void MouseLeftClickEvent()
        {
            MouseEvent(MouseEventFlags.LeftUp);
            Thread.Sleep(10);
            MouseEvent(MouseEventFlags.LeftDown);
        }

        public static void MouseRightClickEvent()
        {
            MouseEvent(MouseEventFlags.RightUp);
            Thread.Sleep(10);
            MouseEvent(MouseEventFlags.RightDown);
        }

        public static Point GetCursorPosition()
        {
            Point currentMousePoint;
            return GetCursorPos(out currentMousePoint) ? new Point(currentMousePoint.X, currentMousePoint.Y) : new Point(0, 0);
        }

        private static void MouseEvent(MouseEventFlags value)
        {
            var position = GetCursorPosition();

            mouse_event
                ((int)value,
                    position.X,
                    position.Y,
                    0,
                    0)
                ;
        }
    }
    public class Keyboard
    {
        [DllImport("user32.dll")]
        private static extern uint keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private const int ACTION_DELAY = 15;


        [DllImport("user32.dll")]
        public static extern bool BlockInput(bool fBlockIt);

        public static void KeyDown(Keys key)
        {
            keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY | 0, 0);
        }

        public static void KeyUp(Keys key)
        {
            keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0); //0x7F
        }

        [DllImport("USER32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public static bool IsKeyDown(int nVirtKey)
        {
            return GetKeyState(nVirtKey) < 0;
        }
    }
    public static class WinApiMouse
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point lpMousePoint);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        #region Structs/Enums

        [Flags]
        public enum MouseEventFlags
        {
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040,
            Move = 0x00000001,
            Absolute = 0x00008000,
            RightDown = 0x00000008,
            RightUp = 0x00000010
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }

        }

        #endregion
    }
}





