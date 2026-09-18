$targetBase = "D:\1-Computer Science\1-Personal Project\1_AI_Agents_Documentation"
$knowledgeBase = "C:\Users\phyna\.gemini\antigravity\knowledge"
$brainBase = "C:\Users\phyna\.gemini\antigravity\brain"

Write-Host "Creating target directory: $targetBase"
if (-not (Test-Path $targetBase)) {
    New-Item -ItemType Directory -Path $targetBase | Out-Null
}

Write-Host "Processing Knowledge Items..."
$kiDirs = Get-ChildItem -Path $knowledgeBase -Directory
foreach ($ki in $kiDirs) {
    if ($ki.Name -eq "knowledge.lock") { continue }
    
    $metaPath = Join-Path $ki.FullName "metadata.json"
    $projectName = $ki.Name
    
    if (Test-Path $metaPath) {
        try {
            $metaContent = Get-Content $metaPath -Raw | ConvertFrom-Json
            if ($metaContent.projects -and $metaContent.projects.Count -gt 0) {
                $projectName = $metaContent.projects[0]
            }
        } catch {
            Write-Host "Error parsing $metaPath"
        }
    }
    
    $projectDir = Join-Path $targetBase $projectName
    if (-not (Test-Path $projectDir)) { New-Item -ItemType Directory -Path $projectDir | Out-Null }
    
    $kiTargetDir = Join-Path $projectDir "Knowledge"
    if (-not (Test-Path $kiTargetDir)) { New-Item -ItemType Directory -Path $kiTargetDir | Out-Null }
    
    # Copy Knowledge artifacts
    $destPath = Join-Path $kiTargetDir $ki.Name
    Copy-Item -Path $ki.FullName -Destination $kiTargetDir -Recurse -Force
}

Write-Host "Processing Conversations (Brain)..."
$brainDirs = Get-ChildItem -Path $brainBase -Directory
foreach ($brain in $brainDirs) {
    if ($brain.Name -eq "tempmediaStorage") { continue }
    
    $projectName = "Uncategorized"
    
    # Try to determine project from overview.txt
    $overviewPath = Join-Path $brain.FullName ".system_generated\logs\overview.txt"
    if (Test-Path $overviewPath) {
        $firstLines = Get-Content $overviewPath -TotalCount 50
        $joinedLines = $firstLines -join "`n"
        
        # Look for a workspace path like D:\1-Computer Science\1-Personal Project\<ProjectName>
        if ($joinedLines -match "(?i)1-Personal Project\\([^\\]+)") {
            $projectName = $Matches[1]
        }
    }
    
    $projectDir = Join-Path $targetBase $projectName
    if (-not (Test-Path $projectDir)) { New-Item -ItemType Directory -Path $projectDir | Out-Null }
    
    $brainTargetDir = Join-Path $projectDir "Conversations"
    if (-not (Test-Path $brainTargetDir)) { New-Item -ItemType Directory -Path $brainTargetDir | Out-Null }
    
    # Copy conversation history, artifacts, scratch files
    Copy-Item -Path $brain.FullName -Destination $brainTargetDir -Recurse -Force
}

Write-Host "Data compilation complete!"
