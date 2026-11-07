using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>One explicit disclosure request, one pass. Idle views have no ticker.</summary>
internal abstract class GuideMotionView : Grid
{
    private const string AnimationName = "GuideIllustration";
    private Window? _observedWindow;
    private int _generation;
    private bool _pending;
    private int _permissionVersion;
    private bool _windowActive = true;
    public bool IsPlaying { get; private set; }
    public bool HasPlayed { get; private set; }
    public bool MotionAllowed => MotionPreferences.AllowIllustrationMotion();
    protected abstract uint Duration { get; }
    protected abstract void Apply(double progress);
    protected abstract void ResetFrame();

    protected GuideMotionView()
    {
        HorizontalOptions = LayoutOptions.Center; InputTransparent = true; IsClippedToBounds = true;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        Loaded += OnLoaded;
        SizeChanged += (_, _) => TryStartPending();
        Unloaded += (_, _) => Detach();
        HandlerChanging += (_, e) => { if (e.OldHandler is not null) Detach(); };
    }

    // Loading/measure may follow the disclosure click. A later settings change never creates a request.
    public void RequestPlay()
    {
        Stop();
        _permissionVersion = IllustrationMotionSettings.DisableVersion;
        _pending = MotionAllowed && (_observedWindow is null || _windowActive);
        TryStartPending();
        Dispatcher.Dispatch(TryStartPending);
    }

    public bool Play()
    {
        Stop();
        _permissionVersion = IllustrationMotionSettings.DisableVersion;
        if (!CanPlay()) return false;
        Start(); return true;
    }

    private bool CanPlay() => IsLoaded && Handler is not null && Window is not null
        && Width > 0 && Height > 0 && MotionAllowed && _windowActive
        && _permissionVersion == IllustrationMotionSettings.DisableVersion && IsEffectivelyVisible();

    private bool IsEffectivelyVisible()
    {
        for (Element? element = this; element is not null; element = element.Parent)
            if (element is VisualElement { IsVisible: false }) return false;
        return true;
    }

    private void TryStartPending()
    {
        if (!_pending) return;
        if (!MotionAllowed || _permissionVersion != IllustrationMotionSettings.DisableVersion) { _pending = false; return; }
        if (!CanPlay()) return;
        _pending = false; Start();
    }

    private void Start()
    {
        int generation = ++_generation;
        IsPlaying = true; HasPlayed = true; Apply(0);
        new Animation(p =>
        {
            if (generation != _generation) return;
            if (!CanPlay()) { Stop(); return; }
            Apply(p);
        }).Commit(this, AnimationName, rate: 16, length: Duration, easing: Easing.Linear,
            finished: (_, _) =>
            {
                if (generation != _generation) return;
                IsPlaying = false; ResetFrame();
            });
    }

    public void Stop()
    {
        _pending = false; ++_generation;
        this.AbortAnimation(AnimationName);
        IsPlaying = false; ResetFrame();
    }

    protected static Image Artwork(string asset)
    {
        var image = new Image { Source = asset, Aspect = Aspect.AspectFit, InputTransparent = true };
        AutomationProperties.SetIsInAccessibleTree(image, false);
        return image;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Unobserve();
        _windowActive = true;
        _observedWindow = Window;
        if (_observedWindow is { } window)
        {
            window.Deactivated += OnBackground; window.Stopped += OnBackground; window.Destroying += OnBackground;
            window.Activated += OnActivated;
            Battery.Default.EnergySaverStatusChanged += OnPreferenceChanged;
            IllustrationMotionSettings.Changed += OnPreferenceChanged;
        }
        TryStartPending();
    }
    private void OnBackground(object? sender, EventArgs e) { _windowActive = false; Stop(); }
    private void OnActivated(object? sender, EventArgs e) => _windowActive = true;
    private void OnPreferenceChanged(object? sender, EventArgs e)
    {
        // Capture the veto now: a later enable must not revive this playback.
        if (MotionAllowed && _permissionVersion == IllustrationMotionSettings.DisableVersion) return;
        int generation = _generation;
        if (!Dispatcher.IsDispatchRequired) Stop();
        else Dispatcher.Dispatch(() => { if (generation == _generation) Stop(); });
    }
    private void Detach() { Stop(); Unobserve(); }
    private void Unobserve()
    {
        if (_observedWindow is not { } window) return;
        window.Deactivated -= OnBackground; window.Stopped -= OnBackground; window.Destroying -= OnBackground;
        window.Activated -= OnActivated;
        Battery.Default.EnergySaverStatusChanged -= OnPreferenceChanged;
        IllustrationMotionSettings.Changed -= OnPreferenceChanged;
        _observedWindow = null;
    }
}
