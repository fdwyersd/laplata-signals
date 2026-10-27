# La Plata west curve signal watcher v0.31.
# Classic Windows PowerShell 2.0+ / Windows Forms. No Python or OpenCV required.
$ErrorActionPreference = 'Stop'
[string]$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$expectedVersion = '0.31.0.0'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Add-Type -AssemblyName System.Core
    Add-Type -AssemblyName System.Xml
    # The included DLL targets CLR 2.0 / .NET 3.5. No client-side compilation.
    $componentPath = Join-Path $base 'WestSignalWatch.dll'
    if (-not [IO.File]::Exists($componentPath)) { throw ('Missing component: ' + $componentPath + '. Extract the entire ZIP first.') }
    $component = [Reflection.Assembly]::LoadFrom($componentPath)
    if ($component.GetName().Version.ToString() -ne $expectedVersion) { throw 'The launcher and DLL are from different versions. Extract the whole v0.31 ZIP into a new folder.' }
    [System.Windows.Forms.Application]::EnableVisualStyles()
    # Let PowerShell bind the string parameter directly, without a reflection
    # object[] that retains PSObject wrappers in Windows PowerShell 2.0.
    [LapWest.WatchForm]::Run([string]$base)
} catch {
    $details = $_ | Out-String
    $details += "`r`n" + $_.Exception.ToString()
    $details += "`r`nRuntime: " + [Environment]::Version.ToString()
    $details += "`r`nComponent: " + $componentPath
    try { [IO.File]::WriteAllText((Join-Path $base 'startup-error.txt'), $details) } catch { }
    Write-Host $details -ForegroundColor Red
    try { [System.Windows.Forms.MessageBox]::Show($details, 'West signal watcher could not start') | Out-Null } catch { }
    exit 1
}
