# click-at.ps1 — real OS left-click at absolute screen pixel coords.
# run.mjs computes the page HEADING's screen coords (a neutral, per-page target) and passes
# them here; the physical click moves OS keyboard focus into the web content (el.focus() only
# sets DOM focus, leaving OS focus on the browser chrome so NVDA reads the toolbar). A fixed
# content-column coordinate was per-layout fragile (heading Y differs per page); the heading
# is neutral (no side effects) and always present.
param([int]$X, [int]$Y)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Click {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  public static void At(int x, int y){
    SetCursorPos(x, y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
  }
}
"@
[Click]::At($X, $Y)
