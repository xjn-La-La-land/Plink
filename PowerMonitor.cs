using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Plink
{
    // Listens to SystemEvents.PowerModeChanged and emits distinct events when
    // the AC line transitions between connected and disconnected. The current
    // status is read from SystemInformation.PowerStatus.PowerLineStatus.
    internal sealed class PowerMonitor : IDisposable
    {
        private PowerLineStatus _lastStatus = PowerLineStatus.Unknown;
        private bool _started;

        public event EventHandler PowerConnected;
        public event EventHandler PowerDisconnected;

        public void Start()
        {
            if (_started)
                return;
            _lastStatus = SystemInformation.PowerStatus.PowerLineStatus;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _started = true;
            DebugLog.Write("power monitor started; line=" + _lastStatus);
        }

        public void Stop()
        {
            if (!_started)
                return;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _started = false;
            DebugLog.Write("power monitor stopped");
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            // StatusChange fires on AC plug/unplug; Resume covers the case
            // where the line state changed during sleep and StatusChange
            // didn't reach us across the suspend boundary.
            if (e.Mode != PowerModes.StatusChange && e.Mode != PowerModes.Resume)
                return;

            PowerLineStatus current = SystemInformation.PowerStatus.PowerLineStatus;
            if (current == _lastStatus)
                return;
            PowerLineStatus previous = _lastStatus;
            _lastStatus = current;

            // Only fire on actual Online <-> Offline transitions. Movements
            // involving Unknown (transient read or no battery driver) stay
            // silent so desktops without batteries don't emit on first probe.
            if (previous == PowerLineStatus.Offline && current == PowerLineStatus.Online)
            {
                DebugLog.Write("power line connected");
                EventHandler h = PowerConnected;
                if (h != null) h(this, EventArgs.Empty);
            }
            else if (previous == PowerLineStatus.Online && current == PowerLineStatus.Offline)
            {
                DebugLog.Write("power line disconnected");
                EventHandler h = PowerDisconnected;
                if (h != null) h(this, EventArgs.Empty);
            }
        }
    }
}
