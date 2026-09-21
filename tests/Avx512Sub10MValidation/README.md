# Windows AVX-512 <=10M uncached single-stage experiment

Production dispatch was retained: the candidate improved the affected stages,
but did not demonstrate a whole-power improvement. No production experiment
switch or CPU preference was added.

This harness links current arithmetic and acceleration policy, replacing only
MAUI preferences and language state. Run one benchmark process at a time.

```powershell
./tests/Avx512Sub10MValidation/Prepare.ps1
dotnet build tests/Avx512Sub10MValidation -c Release -p:AuditSource="$PWD/artifacts/avx512-sub10m/Baseline.cs" -o artifacts/avx512-sub10m/baseline
dotnet build tests/Avx512Sub10MValidation -c Release -p:AuditSource="$PWD/artifacts/avx512-sub10m/Candidate.cs" -o artifacts/avx512-sub10m/candidate
$env:DOTNET_TieredCompilation = '0'
dotnet artifacts/avx512-sub10m/candidate/Avx512Sub10MValidation.dll
dotnet artifacts/avx512-sub10m/baseline/Avx512Sub10MValidation.dll bench 10000000 24
dotnet artifacts/avx512-sub10m/candidate/Avx512Sub10MValidation.dll bench 10000000 24
Remove-Item Env:DOTNET_TieredCompilation
```

Alternate baseline/candidate order across repetitions. Kernel checks use an
independent modular reference for both production primes, 1/3/24 workers,
short widths, arbitrary tails, untouched guards and cancellation. Small full
powers are checked against BigInteger. Benchmarks check three independent
modular residues and hash all result limbs outside the timer. Full powers warm
up at exponent 100,000. The kernel benchmark uses six alternating rounds.

See `MathSolver/AVX512_SUB10M_UNCACHED_EXPERIMENT.md` for results and limits.
