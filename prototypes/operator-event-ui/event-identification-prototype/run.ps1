$prototypeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$operatorPrototypeRoot = Split-Path -Parent $prototypeRoot
$viteCommand = Join-Path $operatorPrototypeRoot 'node_modules\.bin\vite.cmd'

& $viteCommand $operatorPrototypeRoot --host 127.0.0.1 --port 4181
