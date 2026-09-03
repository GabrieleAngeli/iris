using Iris.App.ViewModels;

namespace Iris.App.Views;

public partial class ExtractorGuidePage : ContentPage
{
    public ExtractorGuidePage(ExtractorGuideViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
