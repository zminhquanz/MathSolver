using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Views;

/// <summary>Human review is explicit. Pending drafts are never usable practice questions.</summary>
public sealed class AiQuestionReviewPage : ContentPage
{
    private readonly QuestionBankStore _store;
    private readonly Action<string,bool>? _onResolved;
    private readonly CollectionView _items=new(){SelectionMode=SelectionMode.None,ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical){ItemSpacing=12}};
    private readonly Label _status=new(){LineBreakMode=LineBreakMode.WordWrap};
    private readonly LiveWallpaperView _wallpaper=new();
    private bool _busy;
    private static string T(string key)=>LocalizationService.TranslateKey("AiBank.Review."+key);
    private sealed record Card(PendingQuestionReview Pending,string Candidate,string Reference,string Calculation,string Heading);

    public AiQuestionReviewPage(QuestionBankStore store,Action<string,bool>? onResolved=null)
    {
        _store=store;_onResolved=onResolved;Title=T("Title");Shell.SetNavBarIsVisible(this,true);
        this.SetDynamicResource(BackgroundColorProperty,"PageBackgroundColor");
        _status.SetDynamicResource(Label.TextColorProperty,"WallpaperTextPrimaryColor");
        var hint=new Label{Text=T("Hint"),LineBreakMode=LineBreakMode.WordWrap};
        hint.SetDynamicResource(Label.TextColorProperty,"WallpaperTextPrimaryColor");
        var layout=new Grid{RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Star)],RowSpacing=12,
            Padding=new Thickness(16),MaximumWidthRequest=1100,HorizontalOptions=LayoutOptions.Fill};
        layout.Add(hint,0,0);layout.Add(_status,0,1);layout.Add(_items,0,2);
        _items.ItemTemplate=new DataTemplate(() =>
        {
            Label Text(string? binding=null,bool bold=false)
            {
                var label=new Label{LineBreakMode=LineBreakMode.WordWrap,FontSize=15,FontAttributes=bold?FontAttributes.Bold:FontAttributes.None};
                label.SetDynamicResource(Label.TextColorProperty,"WallpaperTextPrimaryColor");
                if(binding is not null)label.SetBinding(Label.TextProperty,binding);
                return label;
            }
            Label Caption(string key){var label=Text(bold:true);label.Text=T(key);return label;}
            var approve=new Button{Text="✅ "+T("Approve")};approve.SetDynamicResource(StyleProperty,"SolverPrimaryActionStyle");
            approve.SetBinding(Button.CommandParameterProperty,".");approve.Command=new Command<Card>(async card=>await ActAsync(card,true));
            var reject=new Button{Text="🗑 "+T("Reject")};reject.SetDynamicResource(StyleProperty,"SolverOutlinedActionStyle");
            reject.SetBinding(Button.CommandParameterProperty,".");reject.Command=new Command<Card>(async card=>await ActAsync(card,false));
            var content=new VerticalStackLayout{Spacing=10,Children={Text("Heading",true),Caption("Candidate"),Text("Candidate"),
                Caption("Reference"),Text("Reference"),Caption("Calculation"),Text("Calculation"),approve,reject}};
            var border=new Border{Content=content};border.SetDynamicResource(StyleProperty,"SolverCardStyle");return border;
        });
        var root=new Grid();root.Add(_wallpaper);root.Add(layout);Content=root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();_wallpaper.Resume();await RefreshAsync();
    }
    protected override void OnDisappearing(){_wallpaper.Pause();base.OnDisappearing();}
    private async Task RefreshAsync()
    {
        try
        {
            var rows=await _store.GetPendingReviewsAsync();
            _items.ItemsSource=rows.Select(p=>new Card(p,p.Draft.ToWordProblem(p.Contract).ProblemText,
                OpenQuestionProse.Example(p.Contract).ToWordProblem(p.Contract).ProblemText,p.Contract.Solution,
                LocalizationService.TranslateKey(p.Contract.Family==BankQuestionFamily.FindX?"FindXBank.Title":p.Contract.Family==BankQuestionFamily.Measurement?"Quiz.ProblemMeasurement":"AiBank.BasicArithmetic"))).ToArray();
            _status.Text=rows.Count==0?T("Empty"):string.Format(System.Globalization.CultureInfo.CurrentCulture,T("Count"),rows.Count);
        }
        catch(Exception e){System.Diagnostics.Debug.WriteLine(e);_status.Text=T("Error");}
    }
    private async Task ActAsync(Card card,bool approve)
    {
        if(_busy || card is null)return;_busy=true;_items.IsEnabled=false;
        try
        {
            bool saved=false;
            if(approve)
            {
                if(!await DisplayAlertAsync(T("Title"),T("Confirm"),T("Approve"),LocalizationService.TranslateKey("AiBank.Cancel")))return;
                saved=await _store.ApproveReviewAsync(card.Pending.Id);
            }
            else await _store.RejectReviewAsync(card.Pending.Id);
            saved |= await _store.ContainsProseAsync(new(card.Pending.Contract,card.Pending.Draft,card.Pending.RawJson,card.Pending.ModelName,card.Pending.CreatedUtc));
            _onResolved?.Invoke(card.Pending.Id,saved);
            await RefreshAsync();
        }
        catch(Exception e){System.Diagnostics.Debug.WriteLine(e);await DisplayAlertAsync(T("Title"),T("Error"),LocalizationService.TranslateKey("AiBank.Ok"));}
        finally{_busy=false;_items.IsEnabled=true;}
    }
}
