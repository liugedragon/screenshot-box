# Installer startup checks

[简体中文](README.zh-CN.md)

These Windows PowerShell 5.1 probes verify startup registration with isolated fixtures. They compile a small Windows executable and clone the production installer with a random application ID, Run value name and Start menu group. They do not install or launch the full ScreenshotBox application.

Run on Windows 11 x64. The Setup probe requires Inno Setup 6; pass `-CompilerPath` if it is not available at `.tools\inno\ISCC.exe`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\ScreenshotBox.Installer.Probe\Verify-Startup.ps1 -CompilerPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
powershell -NoProfile -ExecutionPolicy Bypass -File tests\ScreenshotBox.Installer.Probe\Verify-ScriptStartup.ps1
```

Each probe creates a new directory under `artifacts/`, saves logs and `result.json`, and removes its own registration. An optional `-OutputDirectory` must refer to a new directory. Use a short path to retain room for the command-length boundary case.

The Setup probe covers the default selection, update selection retention, opt-out, path changes, the 260-character startup command limit, quoting and uninstall. The script probe additionally verifies `-NoAutoStart`, preservation of another installation's Run value and files outside the installed manifest.

The probes compare all pre-existing HKCU Run and StartupApproved values before and after testing. Avoid changing startup settings or running another installer during a probe. They never restore snapshots over user values. The only StartupApproved value they create uses their own random name and opaque sentinel bytes; this checks preservation without depending on an undocumented Windows encoding.

The probes do not test a real sign-in, manually disable an entry in Task Manager, or verify tray interaction. Those require separate application and Windows checks. On timeout, a probe terminates only the exact process it started.

Microsoft documents the [Run command limit](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys) and [startup controls in Windows Settings and Task Manager](https://support.microsoft.com/en-us/windows/experience/startup-boot/configure-startup-applications-in-windows).
