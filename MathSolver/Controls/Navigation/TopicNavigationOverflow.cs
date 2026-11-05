using MathSolver.Services;

namespace MathSolver.Controls.Navigation;

internal static class TopicNavigationOverflow
{
    public static void Refresh(VisualElement nativeStrip, ScrollView scroll, bool stacked,
        bool stripVisible, string hintKey)
    {
        string hint = LocalizationService.TranslateKey(hintKey);
        if (nativeStrip.IsVisible)
        {
            SemanticProperties.SetHint(nativeStrip, hint);
            return;
        }
        SemanticProperties.SetHint(scroll,
            TopicNavigationPolicy.HasOverflow(scroll.Width, scroll.ContentSize.Width, stripVisible, stacked)
                ? hint : "");
    }
}
