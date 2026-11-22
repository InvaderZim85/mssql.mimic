param(
    [string]$ProjectFile = ".\MsSql.Mimic\MsSql.Mimic.csproj",
    [string]$Configuration = "Release",
    [switch]$Build
)

$ErrorActionPreference = "Stop"
$script:CurrentStep = 0
$script:TotalSteps = if ($Build) { 7 } else { 4 }
$activity = if ($Build) { "Versioning and build" } else { "Versioning" }

function Write-Step {
    param(
        [string]$Message
    )
    
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Success {
    param(
        [string]$Message
    )
    
    Write-Host $Message -ForegroundColor Green
}

function Write-Warning {
    param(
        [string]$Message
    )
    
    Write-Host $Message -ForegroundColor DarkYellow
}

function Write-Error {
    param(
        [string]$Message
    )
    
    Write-Host $Message -ForegroundColor Red
}

function Show-ProgressStep {
    param(
        [string]$Activity,
        [string]$Status
    )
    
    $script:CurrentStep++
    
    $percentComplete = [int](($scrip:CurrentStep / $script:TotalSteps) * 100)
    
    Write-Progress `
        -Activity $Activity `
        -Status $Status `
        -PercentComplete $percentComplete
}

function Set-OrCreateElement {
    param(
        [xml]$XmlDocument,
        [System.Xml.XmlElement]$Parent,
        [string]$Name,
        [string]$Value
    )

    $element = $Parent.SelectSingleNode($Name)

    if ($null -eq $element) {
        $element = $XmlDocument.CreateElement($Name)
        [void]$Parent.AppendChild($element)
    }

    $element.InnerText = $Value
}

try {
    Show-ProgressStep `
        -Activity $activity `
        -Status "Check project file..."
    
    Write-Step "Check project file..."
    
    if (-not (Test-Path $ProjectFile)) {
        throw "Project file was not found: $ProjectFile"
    }
    
    Write-Success "Project file found: $ProjectFile"
    
    Show-ProgressStep `
        -Activity $activity `
        -Status "Generate version number..."
    
    Write-Step "Generate version number..."
    
    $now = Get-Date
    
    $major = [int]($now.ToString("yy"))
    $minor = [int]$now.Month
    $buildNumber = [int]$now.Day
    $revision = [int]($now.Hour * 60 + $now.Minute)
    
    $version = "$major.$minor.$buildNumber.$revision"
    
    Write-Success "Version number generated: $version"
    
    Show-ProgressStep `
        -Activity $activity `
        -Status "Read project file..."
    
    Write-Step "Read project file..."
    
    [xml]$projectXml = Get-Content $ProjectFile
    
    $propertyGroup = $projectXml.Project.PropertyGroup | Select-Object -First 1
    
    if ($null -eq $propertyGroup) {
        throw "No PropertyGroup was found in the project file."
    }
    
    Write-Success "Project file succesfully loaded."
    
    Show-ProgressStep `
        -Activity $activity `
        -Status "Update version information..."
    
    Write-Step "Update version information..."
    
    # Update the version number
    Set-OrCreateElement -XmlDocument $projectXml -Parent $propertyGroup -Name "Version" -Value $version
    Set-OrCreateElement -XmlDocument $projectXml -Parent $propertyGroup -Name "AssemblyVersion" -Value $version
    Set-OrCreateElement -XmlDocument $projectXml -Parent $propertyGroup -Name "FileVersion" -Value $version
    Set-OrCreateElement -XmlDocument $projectXml -Parent $propertyGroup -Name "InformationalVersion" -Value $version
    
    $projectXml.Save((Resolve-Path $ProjectFile))
    
    Write-Success "Version information successfully updated."
    
    if ($Build) {
        Show-ProgressStep `
            -Activity $activity `
            -Status "Start build..."
        
        Write-Step "Start build..."
        
        $projectDirectory = Split-Path $ProjectFile -Parent
        $publishDirectory = Join-Path $projectDirectory "publish\$Configuration"

        dotnet publish $ProjectFile `
            --configuration $Configuration `
            --runtime win-x64 `
            --self-contained false `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            -p:PublishReadyToRun=false `
            -p:WindowsPackageType=None
        
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed."
        }
        
        Write-Success "Build has been successfully completed."
        
        Show-ProgressStep `
            -Activity $activity `
            -Status "Check publish directory..."
        
        Write-Step "Check publish directory..."
        
        if (-not (Test-Path $publishDirectory)) {
            throw "Publish directory was not found: $publishDirectory"
        }
        
        Write-Success "Publish directory found: $publishDirectory"
        
        Show-ProgressStep `
            -Activity $activity `
            -Status "Search for generated EXE..."
        
        Write-Step "Search for generated EXE..."
        
        $exeFiles = Get-ChildItem $publishDirectory -Filter "*.exe" -File
        
        if ($exeFiles.Count -eq 1) {
            Write-Success "Generated EXE: $($exeFiles[0].FullName)"
        }
        elseif ($exeFiles.Count -gt 1) {
            Write-Warning "Multiple EXE files found:"
            
            $exeFiles | ForEach-Object {
                Write-Warning $_.FullName
            }
        }
        else {
            Write-Warning "No EXE found in the publish directory"
        }
    }
    
    Write-Progress `
        -Activity $activity `
        -Completed

    Write-Success "Script completed successfully."
}
catch {
    Write-Progress `
        -Activity $activity `
        -Completed

    Write-Error "Error: $($_.Exception.Message)"
    exit 1
}