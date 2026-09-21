using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using MathSolver.Numerics;
using MathSolver.Services;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using Environment = System.Environment;
using OperationCanceledException = System.OperationCanceledException;

namespace NeonNttAndroidValidation;

[Activity(Label = "NTT NEON Validation", MainLauncher = true, Exported = true,
    Name = "com.mathsolver.neonvalidation.MainActivity")]
public sealed class MainActivity : Android.App.Activity
{
    private readonly CancellationTokenSource _stop = new();
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        var status = new TextView(this) { Text = "Running Scalar / NEON validation. Results in logcat: NttNeon" };
        SetContentView(status);
        int exponent = Intent?.GetIntExtra("exponent", 100_000) ?? 100_000;
        int workers = Intent?.GetIntExtra("workers", 2) ?? 2;
        int rounds = Intent?.GetIntExtra("rounds", 3) ?? 3;
        ulong basis = ulong.Parse(Intent?.GetStringExtra("basis") ?? "999999999999999999");
        bool validate = Intent?.GetBooleanExtra("validate", true) ?? true;
        _ = Task.Run(() =>
        {
            using var report = new StreamWriter(Path.Combine(FilesDir!.AbsolutePath, "report.txt"), false) { AutoFlush = true };
            void Log(string message) { report.WriteLine(message); Android.Util.Log.Info("NttNeon", message); }
            try
            {
                var debug = (DebuggableAttribute?)Attribute.GetCustomAttribute(
                    typeof(MainActivity).Assembly, typeof(DebuggableAttribute));
                Log($"DEVICE={Build.Model} runtime={Environment.Version} cpu={Environment.ProcessorCount} AdvSimd={AdvSimd.IsSupported} Arm64={AdvSimd.Arm64.IsSupported} Vector128={Vector128.IsHardwareAccelerated} Optimized={debug?.IsJITOptimizerDisabled != true}");
                if (!CalculationAccelerationManager.IsPowerNttAccelerationAvailable)
                {
                    CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.ArmNeon);
                    CalculationAccelerationManager.SetUseSimd(true);
                    var fallback = ParallelBigUnsigned.Pow(12345, 4000, workers, null, _stop.Token);
                    if (fallback.Diagnostics.UsedNeonNttButterflies ||
                        fallback.Magnitude.ToDecimalString() != BigInteger.Pow(12345, 4000).ToString())
                        throw new Exception("Scalar fallback dispatch/result mismatch");
                    Log("DONE PASS: Scalar fallback enforced; NEON timing skipped (runtime/build not eligible).");
                    RunOnUiThread(() => status.Text = "PASS: Scalar fallback. NEON unavailable for this runtime/build.");
                    return;
                }
                CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.ArmNeon);
                if (validate)
                {
                    Log($"PASS kernel checks={ParallelBigUnsigned.ValidateNeonKernels()}");
                    foreach (ulong b in new ulong[] { 3, 12345, 999999999999999999 })
                    foreach (int w in new[] { 1, 2 })
                    {
                        CalculationAccelerationManager.SetUseSimd(true);
                        var actual = ParallelBigUnsigned.Pow(b, 4000, w, null, _stop.Token);
                        if (actual.Magnitude.ToDecimalString() != BigInteger.Pow(b, 4000).ToString())
                            throw new Exception("Small BigInteger oracle mismatch");
                    }
                    CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.Auto);
                    if (!CalculationAccelerationManager.UsePowerNttNeon) throw new Exception("Auto dispatch");
                    using var cancel = new CancellationTokenSource();
                    cancel.Cancel();
                    try { ParallelBigUnsigned.Pow(3, 10000, 2, null, cancel.Token); throw new Exception("Cancellation ignored"); }
                    catch (OperationCanceledException) { }
                    Log("PASS BigInteger oracle, Auto and cancellation");
                }
                // Warm both paths without including JIT in the measured interval.
                foreach (bool neon in new[] { false, true })
                {
                    CalculationAccelerationManager.SetUseSimd(neon);
                    ParallelBigUnsigned.Pow(basis, 10000, workers, null, _stop.Token);
                }
                string? expectedHash = null;
                for (int round = 0; round < rounds; round++)
                foreach (bool neon in round % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    GC.Collect(); GC.WaitForPendingFinalizers();
                    CalculationAccelerationManager.SetUseSimd(neon);
                    if (CalculationAccelerationManager.UsePowerNttNeon != neon) throw new Exception("Preference dispatch");
                    var timer = Stopwatch.StartNew();
                    var result = ParallelBigUnsigned.Pow(basis, exponent, workers, null, _stop.Token);
                    double seconds = timer.Elapsed.TotalSeconds;
                    Log($"TIME round={round} neon={neon} base={basis} exponent={exponent} workers={workers} seconds={seconds:F6} forward={result.Diagnostics.ForwardTransform.TotalSeconds:F6} inverse={result.Diagnostics.InverseTransform.TotalSeconds:F6} ntt={result.Diagnostics.NttMultiplicationCount} backend={result.Diagnostics.UsedNeonNttButterflies}");
                    string digits = result.Magnitude.ToDecimalString();
                    string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digits)));
                    expectedHash ??= hash;
                    if (hash != expectedHash || result.Diagnostics.UsedNeonNttButterflies != neon || result.Diagnostics.UsedAvx2NttButterflies || result.Diagnostics.UsedSseNttButterflies)
                        throw new Exception("Hash/backend mismatch");
                    foreach (uint prime in new uint[] { 1000000007, 1000000009, 4294967291 })
                    {
                        ulong remainder = 0;
                        foreach (char digit in digits) remainder = (remainder * 10 + (uint)(digit - '0')) % prime;
                        if (remainder != (ulong)BigInteger.ModPow(basis, exponent, prime))
                            throw new Exception("Independent modular oracle mismatch");
                    }
                    Log($"PASS hash={hash} digits={digits.Length}");
                }
                Log("DONE PASS");
                RunOnUiThread(() => status.Text = "PASS. Benchmark complete; see report.txt / NttNeon logcat.");
            }
            catch (Exception ex)
            {
                Log("FAIL " + ex);
                RunOnUiThread(() => status.Text = ex.ToString());
            }
        });
    }
    protected override void OnDestroy() { _stop.Cancel(); base.OnDestroy(); }
}
