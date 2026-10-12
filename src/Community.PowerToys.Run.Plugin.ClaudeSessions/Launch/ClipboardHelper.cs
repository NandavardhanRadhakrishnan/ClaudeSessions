using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

internal static class ClipboardHelper
{
    /// <summary>Copies text, hopping to an STA thread if needed. Returns false if the clipboard was busy.</summary>
    public static bool TrySetText(string text)
    {
        var ok = false;

        void Set()
        {
            // Another process may briefly hold the clipboard open.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, copy: true);
                    ok = true;
                    return;
                }
                catch (Exception e) when (e is COMException or ExternalException)
                {
                    if (attempt == 10)
                    {
                        Log.Exception("Clipboard write failed", e, typeof(ClipboardHelper));
                        return;
                    }

                    Thread.Sleep(50);
                }
            }
        }

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            Set();
        }
        else
        {
            var thread = new Thread(Set);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        return ok;
    }
}
