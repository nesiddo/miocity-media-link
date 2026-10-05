; MioCity Media Link — per-user installer (NSIS 3, no administrator rights)
;
;   makensis -DVERSION=1.8.0 -DSOURCE=<folder with MioCityMediaLink.exe> installer/MioCityMediaLink.nsi
;
; Installs to %LOCALAPPDATA%\Programs\MioCity Media Link, adds a Start menu shortcut and an "Apps & features" entry
; for the current user, and optionally starts the app with Windows (HKCU Run, the same value the app's own
; "Windows起動時" button uses). Settings / pairing keys in %LOCALAPPDATA%\MioCity\LocalMediaBridge are kept on
; uninstall so a reinstall does not need pairing again.

Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"

!define APPNAME "MioCity Media Link"
!define EXE "MioCityMediaLink.exe"
!define APPKEY "Software\MioCity\MediaLink"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MioCityMediaLink"
!define RUNKEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define APPROVEDKEY "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
!define RUNVALUE "MioCityMediaLink"
!ifndef VERSION
  !define VERSION "1.8.0"
!endif
!ifndef SOURCE
  !define SOURCE "../publish"
!endif

Name "${APPNAME}"
OutFile "MioCity-Media-Link-${VERSION}-Setup.exe"
RequestExecutionLevel user
InstallDir "$LOCALAPPDATA\Programs\MioCity Media Link"
InstallDirRegKey HKCU "${APPKEY}" "InstallDir"
SetCompressor /SOLID lzma
ManifestDPIAware true
BrandingText "${APPNAME} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1041 "ProductName" "${APPNAME}"
VIAddVersionKey /LANG=1041 "FileDescription" "${APPNAME} セットアップ"
VIAddVersionKey /LANG=1041 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1041 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1041 "CompanyName" "nesiddo"
VIAddVersionKey /LANG=1041 "LegalCopyright" "© 2026 nesiddo (MIT License)"

!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_NODESC
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "MioCity Media Link を起動する"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "../LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Japanese"

Section "MioCity Media Link（必須）" SecMain
  SectionIn RO
  SetShellVarContext current

  ; a running copy (tray) keeps the exe locked: close it so it can be replaced
  nsExec::Exec 'taskkill /IM "${EXE}" /F'
  Pop $0
  Sleep 800

  SetOutPath "$INSTDIR"
  File "${SOURCE}/${EXE}"
  File "/oname=LICENSE.txt" "../LICENSE"
  File "/oname=THIRD-PARTY-NOTICES.txt" "../THIRD-PARTY-NOTICES.md"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\${EXE}"

  WriteRegStr HKCU "${APPKEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "nesiddo"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${EXE}"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "URLInfoAbout" "https://github.com/nesiddo/miocity-media-link"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" "$0"

  ; the autostart section below decides; an unticked box on reinstall turns it off
  DeleteRegValue HKCU "${RUNKEY}" "${RUNVALUE}"
SectionEnd

Section "Windows の起動時に自動で起動する" SecAutostart
  ; same value as the app's own button (BridgeForm.RunCommand); --startup = start hidden in the notification area
  WriteRegStr HKCU "${RUNKEY}" "${RUNVALUE}" '"$INSTDIR\${EXE}" --startup'
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  nsExec::Exec 'taskkill /IM "${EXE}" /F'
  Pop $0
  Sleep 800

  Delete "$INSTDIR\${EXE}"
  Delete "$INSTDIR\LICENSE.txt"
  Delete "$INSTDIR\THIRD-PARTY-NOTICES.txt"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APPNAME}.lnk"

  DeleteRegValue HKCU "${RUNKEY}" "${RUNVALUE}"
  DeleteRegValue HKCU "${APPROVEDKEY}" "${RUNVALUE}"
  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "${APPKEY}"
SectionEnd
