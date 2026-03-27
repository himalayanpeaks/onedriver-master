using CommunityToolkit.Mvvm.ComponentModel;

namespace OneDriver.Master.Ui.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _greeting = "Welcome to Avalonia!";
    }
}
