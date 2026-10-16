using System.Windows;
using System.Windows.Controls;
using WetWare.App.ViewModels;

namespace WetWare.App.Views;

public partial class HomeView : UserControl
{
    private HomeViewModel? _vm;

    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _vm = DataContext as HomeViewModel;
            if (_vm is null) return;
            HeroGraph.Network = _vm.Network;
            HeroGraph.SelectedNeuron = -1;
            _vm.FrameUpdated += OnFrame;
        };
        Unloaded += (_, _) =>
        {
            if (_vm is not null) _vm.FrameUpdated -= OnFrame;
        };
    }

    private void OnFrame(object? sender, EventArgs e) => HeroGraph.Render();
}
