#ifndef BuildNumber
  #define BuildNumber "local"
#endif
#ifndef BuildFolderNumber
  #define BuildFolderNumber BuildNumber
#endif

[Setup]
AppId=SolarEnergyMonitor-Build-{#BuildNumber}
AppName=Solar Energy Monitor (QA Build {#BuildNumber})
AppVersion=0.10.0
AppPublisher=Solar Energy Monitor
DefaultDirName=E:\Solar_Energy_Monitor\Applications\SolarEnergyMonitor-Build-{#BuildFolderNumber}-win-x64
DefaultGroupName=Solar Energy Monitor
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=no
UsePreviousTasks=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\artifacts\installer
OutputBaseFilename=SolarEnergyMonitor-Build-{#BuildNumber}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
Uninstallable=yes
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\..\artifacts\SolarEnergyMonitor-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "data-path.txt,Data\*,Backups\*,Logs\*"

[Icons]
Name: "{userprograms}\Solar Energy Monitor\Build {#BuildFolderNumber}"; Filename: "{app}\SolarEnergyMonitor.exe"; WorkingDir: "{app}"

[Code]
var
  DataPage: TInputDirWizardPage;

procedure InitializeWizard;
begin
  DataPage := CreateInputDirPage(wpSelectDir, 'Datos / Data',
    'Carpeta de datos independiente / Independent data directory',
    'Para QA selecciona una copia aislada existente de Data, no la base productiva. / For QA select an existing isolated Data copy, not the production database.',
    False, '');
  DataPage.Add('Data:');
  DataPage.Values[0] := 'E:\Solar_Energy_Monitor\Data';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  AppPath, DataPath: String;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if DirExists(WizardDirValue) then
    begin
      MsgBox('Selecciona una carpeta nueva para esta build. / Choose a new folder for this build.', mbError, MB_OK);
      Result := False;
    end;
  end;
  if CurPageID = DataPage.ID then
  begin
    AppPath := AddBackslash(ExpandFileName(WizardDirValue));
    DataPath := AddBackslash(ExpandFileName(DataPage.Values[0]));
    if (Trim(DataPage.Values[0]) = '') or
       (Pos(Uppercase(AppPath), Uppercase(DataPath)) = 1) or
       (Pos(Uppercase(DataPath), Uppercase(AppPath)) = 1) then
    begin
      MsgBox('La carpeta Data debe ser independiente de la carpeta de la aplicación. / Data must be independent of the application folder.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if not FileExists(DataPath + 'energy.db') then
    begin
      MsgBox('Selecciona una carpeta Data existente con energy.db. / Select an existing Data folder containing energy.db.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if CompareText(DataPath, 'E:\Solar_Energy_Monitor\Data\') = 0 then
      Result := MsgBox('Esta parece ser la carpeta productiva. Se recomienda una copia aislada para QA. ¿Confirmas esta ruta? / This appears to be the production folder. An isolated QA copy is recommended. Confirm this path?', mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Lines: TArrayOfString;
begin
  if CurStep = ssPostInstall then
  begin
    SetArrayLength(Lines, 1);
    Lines[0] := ExpandFileName(DataPage.Values[0]);
    if not SaveStringsToUTF8File(ExpandConstant('{app}\data-path.txt'), Lines, False) then
      RaiseException('No se pudo guardar data-path.txt / Could not save data-path.txt');
  end;
end;
