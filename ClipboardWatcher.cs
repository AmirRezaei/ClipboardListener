// file: ./ClipboardWatcher.cs
using System.Windows.Forms;

namespace ClipboardListener;

/// <summary>
/// Simple polling clipboard watcher that runs on an STA thread to safely access Windows Forms Clipboard.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly int _intervalMs;
    private readonly Thread _thread;
    private readonly AutoResetEvent _stop = new(false);
    private string _lastText = "";

    public event EventHandler<string>? TextCopied;

    public ClipboardWatcher(int intervalMs = 400)
    {
        _intervalMs = Math.Max(100, intervalMs);
        _thread = new Thread(Loop) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA); // Clipboard requires STA
    }

    public void Start(CancellationToken token)
    {
        token.Register(() => _stop.Set());
        _thread.Start();
    }

    private void Loop()
    {
        while (true)
        {
            if (_stop.WaitOne(_intervalMs))
                break;

            try
            {
                if (Clipboard.ContainsText())
                {
                    var txt = Clipboard.GetText() ?? "";
                    if (txt.Length > 0 && !txt.Equals(_lastText, StringComparison.Ordinal))
                    {
                        _lastText = txt;
                        TextCopied?.Invoke(this, txt);
                    }
                }
            }
            catch
            {
                // Clipboard might be in use by another process; ignore and retry.
            }
        }
    }

    public void Dispose()
    {
        _stop.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
        _stop.Dispose();
    }
}