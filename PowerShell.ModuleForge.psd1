@{
    RootModule           = 'PowerShell.ModuleForge.dll'
    ModuleVersion        = '0.1.0'
    GUID                 = '8100f8b6-3740-4f6e-9ab5-5e4e340d0049'
    Author               = 'Kim Bum Jun'
    Copyright            = '(c) 2026 Kim Bum Jun'
    Description          = 'Scaffolds, builds and creates the manifest for a .NET binary PowerShell module (C# cmdlet project) with a single New-ModuleForge command.'
    PowerShellVersion    = '7.6'
    CompatiblePSEditions = @('Core')

    CmdletsToExport      = @('New-ModuleForge')
    FunctionsToExport    = @()
    AliasesToExport      = @()
    VariablesToExport    = @()

    PrivateData          = @{
        PSData = @{
            Tags         = @('binary-module', 'dotnet', 'csharp', 'cmdlet', 'scaffold', 'template')
            LicenseUri   = 'https://github.com/ViVaKR/PowerShell.ModuleForge/blob/main/LICENSE'
            ProjectUri   = 'https://github.com/ViVaKR/PowerShell.ModuleForge'
            Prerelease   = 'alpha001'
            ReleaseNotes = 'Initial alpha: New-ModuleForge creates a binary module skeleton, builds it with the dotnet SDK and generates a validated manifest.'
        }
    }
}
