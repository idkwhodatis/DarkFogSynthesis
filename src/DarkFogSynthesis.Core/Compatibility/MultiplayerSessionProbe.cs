using System;
using System.Reflection;

namespace DarkFogSynthesis.Core.Compatibility
{
    public enum MultiplayerSessionStatus { NotInstalled, SinglePlayer, Active, Unavailable }

    /// <summary>
    /// Optional, read-only Nebula detection. Cache the public getter, NEVER its value or an
    /// unsuccessful lookup. There is no Nebula assembly reference or process-wide failure latch.
    /// </summary>
    public sealed class MultiplayerSessionProbe
    {
        public const string NebulaPluginGuid = "dsp.nebula-multiplayer";
        public const string NebulaAssemblyName = "NebulaWorld";
        public const string NebulaTypeName = "NebulaWorld.Multiplayer";
        private readonly Func<bool> isInstalled;
        private readonly Func<Type?> findMultiplayerType;
        private Func<bool>? readActive;

        public MultiplayerSessionProbe(Func<bool> isInstalled, Func<Type?> findMultiplayerType)
        {
            this.isInstalled = isInstalled ?? throw new ArgumentNullException(nameof(isInstalled));
            this.findMultiplayerType = findMultiplayerType ?? throw new ArgumentNullException(nameof(findMultiplayerType));
        }

        public MultiplayerSessionStatus Observe()
        {
            try
            {
                // No reflection, dependency loading or API access in a profile without Nebula.
                // The optional API plugin on its own is not an active multiplayer implementation.
                if (!isInstalled()) return MultiplayerSessionStatus.NotInstalled;
                if (readActive == null)
                {
                    var property = findMultiplayerType()?.GetProperty("IsActive", BindingFlags.Public | BindingFlags.Static);
                    var getter = property?.GetGetMethod(false);
                    if (property == null || property.PropertyType != typeof(bool) || getter == null ||
                        !getter.IsStatic || getter.GetParameters().Length != 0)
                        return MultiplayerSessionStatus.Unavailable;
                    readActive = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                }
                return readActive() ? MultiplayerSessionStatus.Active : MultiplayerSessionStatus.SinglePlayer;
            }
            catch (Exception)
            {
                // An unreadable installed peer is unknown, not proven single-player. Do not poison
                // startup: an unsuccessful lookup/getter is retried and a new SP session can recover.
                return MultiplayerSessionStatus.Unavailable;
            }
        }

        public bool AllowsSinglePlayer => BlockingReason == null;

        public string? BlockingReason
        {
            get
            {
                switch (Observe())
                {
                    case MultiplayerSessionStatus.Active:
                        return "DarkFogSynthesis does not support an active Nebula multiplayer session: its custom lab recipe selection is not network-synchronized. " +
                            "Leave multiplayer and load a fresh single-player session. Nebula may remain installed. Do not save the affected session. / " +
                            "本 Mod 的自定义研究站配方尚不支持联机同步。请退出联机并重新载入单人存档；无需卸载 Nebula，勿保存受影响的联机会话。";
                    case MultiplayerSessionStatus.Unavailable:
                        return "Nebula is installed, but its public active-session state could not be read. DarkFogSynthesis cannot qualify this session; " +
                            "use a supported Nebula build or a separate single-player profile. This is not a content-registration failure. / " +
                            "无法读取 Nebula 的公开会话状态，暂不能验证当前会话。请使用受支持版本或独立单人配置；内容注册未被永久禁用。";
                    default:
                        return null;
                }
            }
        }

        public void EnsureSinglePlayer()
        {
            string? reason = BlockingReason;
            if (reason != null) throw new InvalidOperationException(reason);
        }
    }
}
