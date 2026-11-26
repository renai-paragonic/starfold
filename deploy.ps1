# Starfold deploy: test, build, push, and roll out to the droplet.
# Run it through deploy.cmd (this machine blocks running .ps1 files directly).
#
#   deploy.cmd          asks before the push and before the droplet
#   deploy.cmd -Yes     no questions
#
# Steps: refuse uncommitted changes; run the xUnit tests; docker build --pull,
# tagged <date>-<commit>; push to Docker Hub; copy deploy-remote.sh to the
# droplet and run it there. It switches /root/starfold to the new tag,
# health-checks the site, and puts the previous tag back if the check fails.
#
# Needs: Docker Desktop running and signed in with a read-and-write token,
# `ssh starfold-droplet` working (see ProjectPlans/ezra.md, step 8), and the
# git-ignored wwwroot/images/star-bg.gif in place.
# Keep this file ASCII: Windows PowerShell 5.1 misreads UTF-8 without a BOM.

param([switch]$Yes)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$Repo = 'renaiparagonic/renai-repo'
$Droplet = 'starfold-droplet'

function Write-Step([string]$Message) { Write-Host "`n== $Message" -ForegroundColor Cyan }

function Stop-Deploy([string]$Message) {
    Write-Host "`nSTOPPED: $Message" -ForegroundColor Red
    exit 1
}

# Runs a program and stops the deploy if it fails.
function Invoke-Checked([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { Stop-Deploy "$Program $($Arguments -join ' ') exited with $LASTEXITCODE." }
}

function Confirm-Step([string]$Question) {
    if ($Yes) { return }
    $answer = Read-Host "$Question [y/N]"
    if ($answer -notmatch '^(y|yes)$') { Stop-Deploy 'Not confirmed. Nothing after this point was done.' }
}

Write-Step 'Checking the working tree'
$dirty = git status --porcelain
if ($dirty) {
    $dirty | ForEach-Object { Write-Host "  $_" }
    Stop-Deploy 'Commit (or stash) these first, so the image tag matches a commit.'
}
if (-not (Test-Path 'wwwroot/images/star-bg.gif')) {
    Stop-Deploy 'wwwroot/images/star-bg.gif is missing (it is git-ignored; see the plan).'
}
$commit = git rev-parse --short HEAD
$tag = '{0}-{1}' -f (Get-Date -Format 'yyyy-MM-dd'), $commit
$image = "${Repo}:$tag"
Write-Host "  commit $commit -> image $image"

Write-Step 'Running the tests'
# Release, because a running `dotnet run` locks bin/Debug.
Invoke-Checked 'dotnet' @('test', 'tests/Starfold.Tests', '-c', 'Release', '--nologo')

Write-Step "Building $image"
Invoke-Checked 'docker' @('build', '--pull', '-t', $image, '.')

Confirm-Step "Push $image to Docker Hub?"
Write-Step 'Pushing'
Invoke-Checked 'docker' @('push', $image)
$digest = docker image inspect $image --format '{{index .RepoDigests 0}}'
Write-Host "  pushed $digest"

Confirm-Step "Deploy $tag to the droplet (renaiparagonic.live)?"
Write-Step 'Deploying on the droplet'
# One copy and one run: ufw limits port 22 to about 6 new connections in 30s.
Invoke-Checked 'scp' @('-q', 'deploy-remote.sh', "${Droplet}:/root/starfold/deploy-remote.sh")
& ssh $Droplet "bash /root/starfold/deploy-remote.sh $tag"
if ($LASTEXITCODE -ne 0) { Stop-Deploy "The droplet did not take $tag (see above). The previous version should still be running." }

Write-Host "`nDeployed $tag ($digest)." -ForegroundColor Green
Write-Host 'Check https://renaiparagonic.live and /ezra in a browser.'
