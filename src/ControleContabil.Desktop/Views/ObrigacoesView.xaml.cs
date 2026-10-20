using System.Windows.Controls;
using ControleContabil.Desktop.ViewModels;

namespace ControleContabil.Desktop.Views;

public partial class ObrigacoesView : UserControl
{
    public ObrigacoesView() => InitializeComponent();

    private void GridObrigacoes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ObrigacoesViewModel viewModel && sender is DataGrid grid)
            viewModel.Selecionados = grid.SelectedItems;
    }
}
