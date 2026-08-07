$prototypeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $prototypeRoot
if (-not (Test-Path 'node_modules\.bin\vite.cmd')) { npm install }
npm run dev
