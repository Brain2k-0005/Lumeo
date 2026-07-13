# click-at.ps1 — real OS left-click on a CSS-coordinate point inside the Edge viewport.
#
# Converts CSS viewport coords -> screen pixels WITHOUT touching window.screenX/screenY,
# whose meaning is ambiguous across Chromium versions (sometimes the OS-window origin,
# sometimes the viewport origin — Codex P2, PR #368): anchor from the OS window rect
# instead. Edge has no bottom chrome, so the viewport's bottom edge == the window rect's
# bottom edge, and the viewport's left edge == the window rect's left edge:
#   y = rect.Bottom - (innerHeight - cssY) * dpr
#   x = rect.Left   + cssX * dpr
# run.mjs passes the page heading's CSS center + innerHeight + devicePixelRatio. The click
# moves OS keyboard focus into the web content (el.focus() only sets DOM focus, leaving OS
# focus on the browser chrome so NVDA reads the toolbar).
param(
  [string]$Match = "Lumeo",
  [double]$CssX,
  [double]$CssY,
  [double]$InnerH,
  [double]$Dpr = 1
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Click {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int m);
  public static string TitleOf(IntPtr h){ var sb=new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
  public static bool At(IntPtr h, double cssX, double cssY, double innerH, double dpr){
    RECT r; if (!GetWindowRect(h, out r)) return false;
    int x = r.L + (int)Math.Round(cssX * dpr);
    int y = r.B - (int)Math.Round((innerH - cssY) * dpr);
    if (x <= r.L || x >= r.R || y <= r.T || y >= r.B) return false; // sanity: stay inside the window
    SetCursorPos(x, y);
    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    return true;
  }
}
"@

$hwnd = [IntPtr]::Zero
$edge = Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
foreach ($proc in $edge) {
  if ([Click]::TitleOf($proc.MainWindowHandle) -like "*$Match*") { $hwnd = $proc.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero -and $edge) { $hwnd = ($edge | Select-Object -First 1).MainWindowHandle }
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "[click-at] no Edge window found"; exit 1 }

if ([Click]::At($hwnd, $CssX, $CssY, $InnerH, $Dpr)) { Write-Host "[click-at] OK"; exit 0 }
else { Write-Host "[click-at] out-of-bounds or rect failure"; exit 1 }
