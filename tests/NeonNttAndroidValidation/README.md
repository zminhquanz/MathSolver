# Android ARM64 NTT validation

This separate APK links the production NTT kernels and SIMD dispatch manager.
Only MAUI preferences/language storage are stubbed. It does not replace Math Solver.
Run on a real ARM64 device; x86/emulator timing is not an ARM performance result.

```powershell
dotnet build tests/NeonNttAndroidValidation -c Release -t:SignAndroidPackage
adb -s DEVICE_SERIAL install -r tests/NeonNttAndroidValidation/bin/Release/net10.0-android/android-arm64/com.mathsolver.neonvalidation-Signed.apk
adb -s DEVICE_SERIAL shell am force-stop com.mathsolver.neonvalidation
adb -s DEVICE_SERIAL shell am start -n com.mathsolver.neonvalidation/.MainActivity --ei exponent 1000000 --ei workers 2 --ei rounds 3 --ez validate true
adb -s DEVICE_SERIAL logcat -d -s NttNeon:I '*:S'
```

Use the installed Android SDK's `platform-tools/adb.exe` if adb is not on PATH.
Wait for `DONE PASS` before starting another run. Each round runs both Scalar and
NEON and reverses the order on alternating rounds. `basis` is an optional string
extra (default `999999999999999999`). Only compute time is measured; result
formatting, SHA-256 and three independent modular oracles run afterwards.

The harness uses Mono JIT, no interpreter/AOT, with `Optimize=true` by default.
Current production policy requires Release as well: Debug always validates the
Scalar fallback, even with `Optimize=true`. Existing optimized Debug timing logs
predate this policy and are not measurements of the current Release build.
Build with `-p:Optimize=false` to check the app's normal Debug compilation too.
That configuration now validates the enforced Scalar fallback and skips NEON
timing, because the candidate regressed on the connected device. Historical
`vector128-debug-*` logs document the rejected timings before the gate was added.
The header reports the runtime capability and optimization flags; never label
an `AdvSimd=False, Vector128=True` run as a direct AdvSimd benchmark.

Validation includes modular boundary/random cases for both NTT primes,
forward DIF against a scalar oracle, inverse DIT round trips, multiple tile sizes,
BigInteger oracle checks, Auto selection, cancellation and backend diagnostics.
Direct AdvSimd checks run only when that API is supported. The tested Mono runtime
uses the Vector128 kernel with four scalar high-product calculations per vector.

Start with 100K, then 1M. A 10M run with the default base creates 180M digits and
uses substantial RAM. Use the same worker count in both modes, keep the app in
the foreground, and record thermal/battery conditions. No CPU affinity or clock
controls are changed by this runner. Raw device logs are in `Results/`.
