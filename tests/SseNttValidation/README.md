# SSE NTT validation

The project links production NTT, SSE and acceleration-policy code. `Bridge.cs` exposes private arithmetic only within this test assembly. No preference stub selects a fake backend.

```powershell
dotnet build tests/SseNttValidation -c Release
dotnet tests/SseNttValidation/bin/Release/net10.0/SseNttValidation.dll validate

# SSE4.1 with AVX disabled (set only for this PowerShell process).
$env:DOTNET_EnableAVX='0'
dotnet tests/SseNttValidation/bin/Release/net10.0/SseNttValidation.dll bench 1000000 2 999999999999999999

# .NET 10 groups SSE3, SSSE3, SSE4.1, SSE4.2 and POPCNT under EnableSSE42.
$env:DOTNET_EnableSSE42='0'
dotnet tests/SseNttValidation/bin/Release/net10.0/SseNttValidation.dll validate 9999999 4 2
dotnet tests/SseNttValidation/bin/Release/net10.0/SseNttValidation.dll bench 1000000 2 999999999999999999
Remove-Item Env:DOTNET_EnableAVX, Env:DOTNET_EnableSSE42
```

See the [runtime configuration source](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/inc/clrconfigvalues.h) for the grouped mask. `EnableSSE3` and `EnableSSE41` do not independently disable these intrinsics in this runtime. The harness prints actual `IsSupported` flags and asserts the SSE2 mask took effect.

Arguments: `validate|bench exponent workers base [rounds]`. Validation additionally compares complete output to BigInteger up to 400,000 digits and tests Auto dispatch/cancellation/scope. All sizes compare full Scalar/SSE output and residues against BigInteger.ModPow using three independent primes; this avoids a costly BigInteger decimal conversion at the 10M boundary. Bench defaults to three alternating Scalar/SSE rounds; decimal conversion, modular checks and SHA-256 are outside the timed region. Results are in `Results/`.

Reproduce the UI-sized regression separately in both configurations (run sequentially to avoid CPU contention):

```powershell
dotnet run --project tests/SseNttValidation -c Debug -- bench 10000000 24 999999999999999999 2
dotnet run --project tests/SseNttValidation -c Release -- bench 10000000 24 999999999999999999 2
```

The revised modular tests exercise the actual butterfly loop, including its Shoup product and canonical sum/difference outputs. Arithmetic is kept in that loop to avoid helper calls under the Debug MinOpts JIT. Block tests also verify fused radix-4 and stage-wide traversal against an independent scalar DIF implementation and inverse round trips.
