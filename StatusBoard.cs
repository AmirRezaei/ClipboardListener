// file: StatusBoard.cs
// StatusBoard.cs (non-destructive; never overwrites earlier logs)
using System;
using System.Collections.Generic;

namespace ClipboardListener;

internal sealed class StatusBoard
{
    private readonly object _consoleLock;
    private int _top = -1;                   // first row of the board body (below a heading)
    private readonly List<string> _titles = new();
    private readonly List<string> _lines  = new();
    private readonly List<int> _rows      = new(); // absolute console row for each slot

    public StatusBoard(object consoleLock) => _consoleLock = consoleLock;

    public int Add(string title)
    {
        lock (_consoleLock)
        {
            // Ensure a heading once
            if (_top == -1)
            {
                Console.WriteLine();
                Console.WriteLine("=== Active downloads ===");
                _top = Console.CursorTop; // next row becomes first slot
            }

            int slotIndex  = _lines.Count;
            int targetRow  = _top + slotIndex;

            // Make sure the console has at least targetRow available (append blank lines if needed)
            while (Console.CursorTop <= targetRow)
                Console.WriteLine();

            _titles.Add(title);
            _lines.Add("starting…");
            _rows.Add(targetRow);

            // Draw initial line
            RedrawLine_NoLock(slotIndex);
            return slotIndex;
        }
    }

    public void Update(int index, string text)
    {
        lock (_consoleLock)
        {
            if ((uint)index >= (uint)_lines.Count) return;
            _lines[index] = text;
            RedrawLine_NoLock(index);
        }
    }

    public void Complete(int index, string text)
    {
        Update(index, text);
    }

    /// <summary>
    /// Non-destructive: we keep the finished row for history. No compaction, no clearing.
    /// </summary>
    public void Remove(int index)
    {
        // intentionally noop — keeps one-line history, avoids overwriting prior logs
    }

    // -------- internals --------

    private void RedrawLine_NoLock(int i)
    {
        var (cx, cy) = (Console.CursorLeft, Console.CursorTop);
        TrySetCursorPosition(0, _rows[i]);
        WritePadded($"{_titles[i]} — {_lines[i]}");
        TrySetCursorPosition(cx, cy);
    }

    private static void WritePadded(string text)
    {
        int width = Math.Max(1, Console.BufferWidth - 1);
        if (text.Length > width)
            text = text.Substring(0, Math.Max(0, width - 1)) + "…";
        Console.Write(text.PadRight(width));
    }

    private static void TrySetCursorPosition(int left, int top)
    {
        try
        {
            int w = Math.Max(1, Console.BufferWidth) - 1;
            int h = Math.Max(1, Console.BufferHeight) - 1;
            Console.SetCursorPosition(Math.Clamp(left, 0, w), Math.Clamp(top, 0, h));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Window resized mid-draw; ignore—next update will fix it.
        }
    }
}