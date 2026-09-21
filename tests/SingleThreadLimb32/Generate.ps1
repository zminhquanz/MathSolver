$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $root 'MathSolver/Numerics/Powers/SingleThreadBigIntegerPower.cs'))
$source=$source.Replace('BaselinePower','InstrumentedPower').Replace('BigInteger.Pow(', 'SquareBridge.Pow(')
$source=[regex]::Replace($source,'resultBigInteger \*=\s*resultBigInteger;', 'resultBigInteger = SquareBridge.Square(resultBigInteger);')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'InstrumentedPower.generated.cs'),$source)
