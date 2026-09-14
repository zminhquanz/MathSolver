namespace MathSolver.Services;

/// <summary>
/// Chế độ trình bày số dùng chung cho các tab trong màn hình Giải toán.
/// Mặc định kết quả dài hơn 18 chữ số được rút gọn; người dùng có thể
/// chuyển sang dạng đầy đủ trong menu Cài đặt.
/// </summary>
public static class ResultNumberDisplayMode
{
    private const string ShowFullNumbersPreferenceKey =
        "result_number_display_show_full";

    private static bool _isInitialized;
    private static bool _showFullNumbers;

    public static bool ShowFullNumbers
    {
        get
        {
            EnsureInitialized();
            return _showFullNumbers;
        }
    }

    public static event EventHandler? DisplayModeChanged;

    public static bool SetShowFullNumbers(
        bool showFullNumbers)
    {
        EnsureInitialized();

        if (_showFullNumbers ==
            showFullNumbers)
        {
            return false;
        }

        _showFullNumbers =
            showFullNumbers;

        Preferences.Default.Set(
            ShowFullNumbersPreferenceKey,
            showFullNumbers);

        DisplayModeChanged?.Invoke(
            null,
            EventArgs.Empty);

        return true;
    }

    public static void ResetToDefault()
    {
        Preferences.Default.Remove(
            ShowFullNumbersPreferenceKey);

        bool changed =
            _isInitialized &&
            _showFullNumbers;

        _showFullNumbers =
            false;

        _isInitialized =
            true;

        if (changed)
        {
            DisplayModeChanged?.Invoke(
                null,
                EventArgs.Empty);
        }
    }

    private static void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _showFullNumbers =
            Preferences.Default.Get(
                ShowFullNumbersPreferenceKey,
                false);

        _isInitialized =
            true;
    }
}
