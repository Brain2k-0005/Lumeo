# force-foreground.ps1 — bring the automated Edge window to real OS foreground focus.
#
# NVDA reads whatever holds OS foreground focus. A browser launched programmatically by
# run.mjs (deep under a background agent / terminal) does NOT get foreground on Windows —
# so without this, an unattended NVDA sweep reads the wrong window (see README "Known
# blocker: OS foreground focus"). This reproduces what a human mouse-click does, using the
# documented ALT-key foreground-lock release + AttachThreadInput + SetForegroundWindow
# sequence, so the sweep can run fully unattended (e.g. scheduled overnight).
#
# Usage: powershell -ExecutionPolicy Bypass -File force-foreground.ps1 [-Match "Lumeo"]
# Exit 0 if the matched window is foreground afterwards, 1 otherwise.
param([string]$Match = "Lumeo")

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class FG {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("user32.dll")] public static extern void keybd_event(byte v, byte s, uint f, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int m);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string title);
  public static string TitleOf(IntPtr h){ var sb=new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
  // NVDA's Speech Viewer is a visible window that aggressively holds foreground; NVDA then
  // reads IT, not the page. Guidepup captures speech via NVDA's remote channel (not this
  // window), so minimizing it is safe and stops it stealing focus mid-sweep.
  public static void MinimizeSpeechViewer(){
    IntPtr sv = FindWindow(null, "NVDA Speech Viewer");
    if (sv != IntPtr.Zero) ShowWindow(sv, 6); // SW_MINIMIZE
  }
  public static void Force(IntPtr h){
    ShowWindow(h, 9); BringWindowToTop(h);
    keybd_event(0x12,0,0,UIntPtr.Zero); keybd_event(0x12,0,2,UIntPtr.Zero);
    uint pid; uint tCur=GetCurrentThreadId();
    uint tFg=GetWindowThreadProcessId(GetForegroundWindow(), out pid);
    AttachThreadInput(tCur, tFg, true);
    SetForegroundWindow(h); BringWindowToTop(h);
    AttachThreadInput(tCur, tFg, false);
    // Window is foreground, but OS KEYBOARD focus is still on the browser chrome. run.mjs
    // follows up (via click-at.ps1) with a click on the page HEADING's real screen coords to
    // move OS focus into the content — a neutral, per-page target that beats a fixed guess.
  }
}
"@

# Prefer an Edge main window whose title matches -Match; else any Edge main window.
$hwnd = [IntPtr]::Zero
$edge = Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
foreach ($proc in $edge) {
  $t = [FG]::TitleOf($proc.MainWindowHandle)
  if ($t -like "*$Match*") { $hwnd = $proc.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero -and $edge) { $hwnd = ($edge | Select-Object -First 1).MainWindowHandle }
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "[force-fg] no Edge window found"; exit 1 }

[FG]::MinimizeSpeechViewer()
[FG]::Force($hwnd)
Start-Sleep -Milliseconds 500
$fg = [FG]::GetForegroundWindow()
if ($fg -eq $hwnd) { Write-Host "[force-fg] OK -> '$([FG]::TitleOf($hwnd))'"; exit 0 }
else { Write-Host "[force-fg] MISS: foreground is '$([FG]::TitleOf($fg))'"; exit 1 }
