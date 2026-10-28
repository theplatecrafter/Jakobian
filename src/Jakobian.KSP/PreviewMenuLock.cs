using System;
using System.Threading;

namespace Jakobian.KSP
{
    /// <summary>Only modal MainMenu input ownership, not a game/flight admission barrier.</summary>
    internal sealed class PreviewMenuLock : IDisposable
    {
        private readonly string key = "Jakobian.TorusPreview." + Guid.NewGuid().ToString("N");
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private bool disposed;
        internal PreviewMenuLock() { InputLockManager.SetControlLock(ControlTypes.MAIN_MENU, key); }
        public void Dispose()
        {
            if (disposed) return;
            if (Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("Menu lock belongs to its Unity owner thread.");
            InputLockManager.RemoveControlLock(key); disposed = true;
        }
    }
}
