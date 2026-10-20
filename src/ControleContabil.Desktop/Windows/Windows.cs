using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using ControleContabil.Core;
using ControleContabil.Desktop.ViewModels;

namespace ControleContabil.Desktop.Windows;

public sealed class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = "Controle Contábil";
        Width = 1320;
        Height = 820;
        MinWidth = 980;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(31, 45, 61)) };
        var user = new StackPanel { Margin = new Thickness(18, 24, 18, 18) };
        user.Children.Add(new TextBlock
        {
            Text = "CONTROLE CONTÁBIL",
            Foreground = Brushes.White,
            FontSize = 18,
            FontWeight = FontWeights.Bold
        });
        user.Children.Add(new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 14,
            Margin = new Thickness(0, 28, 0, 2)
        }.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.UsuarioNome))));
        user.Children.Add(new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(190, 205, 220)),
            FontSize = 12
        }.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.PerfilTexto))));
        DockPanel.SetDock(user, Dock.Top);
        side.Children.Add(user);

        var menu = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            DisplayMemberPath = nameof(ItemMenu.Titulo),
            Padding = new Thickness(8, 4, 8, 4)
        };
        menu.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainViewModel.Menu)));
        menu.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(MainViewModel.MenuSelecionado))
        {
            Mode = BindingMode.TwoWay
        });
        menu.ItemContainerStyle = new Style(typeof(ListBoxItem))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(12, 11, 8, 11)),
                new Setter(Control.MarginProperty, new Thickness(4, 2, 4, 2)),
                new Setter(Control.FontSizeProperty, 14d),
                new Setter(Control.ForegroundProperty, Brushes.White)
            }
        };
        side.Children.Add(menu);
        Grid.SetColumn(side, 0);
        root.Children.Add(side);

        var content = new Grid { Margin = new Thickness(28, 24, 28, 20) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new TextBlock
        {
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        };
        heading.SetBinding(TextBlock.TextProperty, new Binding($"{nameof(MainViewModel.PaginaAtual)}.{nameof(PaginaBase.Titulo)}"));
        content.Children.Add(heading);

        var page = new ContentControl();
        page.SetBinding(ContentControl.ContentProperty, new Binding(nameof(MainViewModel.PaginaAtual)));
        Grid.SetRow(page, 1);
        content.Children.Add(page);

        var status = new TextBlock
        {
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 12, 0, 0)
        };
        status.SetBinding(TextBlock.TextProperty, new Binding($"{nameof(MainViewModel.PaginaAtual)}.{nameof(PaginaBase.Mensagem)}"));
        Grid.SetRow(status, 2);
        content.Children.Add(status);

        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;
        Loaded += (_, _) => _viewModel.Iniciar();
        Closed += (_, _) => _viewModel.PaginaAtual?.Dispose();
    }

}

internal static class FrameworkElementBindingExtensions
{
    public static T Bind<T>(this T element, DependencyProperty property, Binding binding) where T : FrameworkElement
    {
        element.SetBinding(property, binding);
        return element;
    }
}

public sealed class LoginWindow : Window
{
    public Usuario? UsuarioEscolhido { get; private set; }

    public LoginWindow(IReadOnlyList<Usuario> usuarios)
    {
        Title = "Identificação do usuário";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var content = DialogUi.Content("Selecione seu usuário", out var form);
        var combo = DialogUi.Combo(usuarios, u => u.Nome);
        form.Children.Add(DialogUi.Field("Usuário", combo));
        DialogUi.Buttons(content, () =>
        {
            UsuarioEscolhido = combo.SelectedValue as Usuario;
            DialogResult = UsuarioEscolhido is not null;
        }, this);
        Content = content;
    }
}

public sealed class EmpresaEditWindow : Window
{
    public Empresa? Resultado { get; private set; }

    public EmpresaEditWindow(Empresa empresa, IReadOnlyList<Usuario> usuarios)
    {
        Title = empresa.Id == 0 ? "Nova empresa" : "Editar empresa";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = DialogUi.Content(Title, out var form);
        var cnpj = DialogUi.Text(empresa.Cnpj);
        var razao = DialogUi.Text(empresa.RazaoSocial);
        var fantasia = DialogUi.Text(empresa.NomeFantasia);
        var regime = DialogUi.EnumCombo<RegimeTributario>(
            RegimeTributario.SimplesNacional, RegimeTributario.LucroPresumido, RegimeTributario.LucroReal);
        regime.SelectedItem = empresa.Regime;
        var analista = DialogUi.Combo(new Usuario?[] { null }.Concat(usuarios), u => u?.Nome ?? "Sem responsável");
        analista.SelectedValue = usuarios.FirstOrDefault(u => u.Id == empresa.AnalistaId);
        var backup = DialogUi.Combo(new Usuario?[] { null }.Concat(usuarios), u => u?.Nome ?? "Sem backup");
        backup.SelectedValue = usuarios.FirstOrDefault(u => u.Id == empresa.AnalistaBackupId);
        var ativa = new CheckBox { Content = "Empresa ativa", IsChecked = empresa.Ativa, Margin = new Thickness(0, 6, 0, 6) };
        form.Children.Add(DialogUi.Field("CNPJ", cnpj));
        form.Children.Add(DialogUi.Field("Razão social", razao));
        form.Children.Add(DialogUi.Field("Nome fantasia", fantasia));
        form.Children.Add(DialogUi.Field("Regime tributário", regime));
        form.Children.Add(DialogUi.Field("Analista responsável", analista));
        form.Children.Add(DialogUi.Field("Analista de backup", backup));
        form.Children.Add(ativa);
        DialogUi.Buttons(content, () =>
        {
            empresa.Cnpj = cnpj.Text;
            empresa.RazaoSocial = razao.Text;
            empresa.NomeFantasia = fantasia.Text;
            empresa.Regime = (RegimeTributario)regime.SelectedValue;
            empresa.AnalistaId = (analista.SelectedValue as Usuario)?.Id;
            empresa.AnalistaBackupId = (backup.SelectedValue as Usuario)?.Id;
            empresa.Ativa = ativa.IsChecked == true;
            Resultado = empresa;
            DialogResult = true;
        }, this);
        Content = content;
    }
}

public sealed class UsuarioEditWindow : Window
{
    public Usuario? Resultado { get; private set; }

    public UsuarioEditWindow(Usuario usuario)
    {
        Title = usuario.Id == 0 ? "Novo usuário" : "Editar usuário";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = DialogUi.Content(Title, out var form);
        var nome = DialogUi.Text(usuario.Nome);
        var email = DialogUi.Text(usuario.Email);
        var login = DialogUi.Text(usuario.LoginWindows);
        var perfil = DialogUi.EnumCombo<PerfilUsuario>(Enum.GetValues<PerfilUsuario>());
        perfil.SelectedValue = usuario.Perfil;
        var ativo = new CheckBox { Content = "Usuário ativo", IsChecked = usuario.Ativo, Margin = new Thickness(0, 6, 0, 6) };
        form.Children.Add(DialogUi.Field("Nome", nome));
        form.Children.Add(DialogUi.Field("E-mail", email));
        form.Children.Add(DialogUi.Field("Login do Windows", login));
        form.Children.Add(DialogUi.Field("Perfil", perfil));
        form.Children.Add(ativo);
        DialogUi.Buttons(content, () =>
        {
            if (string.IsNullOrWhiteSpace(nome.Text))
            {
                MessageBox.Show(this, "Informe o nome do usuário.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            usuario.Nome = nome.Text;
            usuario.Email = email.Text;
            usuario.LoginWindows = login.Text;
            usuario.Perfil = (PerfilUsuario)perfil.SelectedValue;
            usuario.Ativo = ativo.IsChecked == true;
            Resultado = usuario;
            DialogResult = true;
        }, this);
        Content = content;
    }
}

public sealed class ModeloObrigacaoEditWindow : Window
{
    public ObrigacaoModelo? Resultado { get; private set; }

    public ModeloObrigacaoEditWindow(ObrigacaoModelo modelo)
    {
        Title = modelo.Id == 0 ? "Novo modelo de obrigação" : "Editar modelo de obrigação";
        Width = 560;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = DialogUi.Content(Title, out var form);
        var nome = DialogUi.Text(modelo.Nome);
        var descricao = DialogUi.Text(modelo.Descricao);
        var tipo = DialogUi.EnumCombo<TipoObrigacao>(Enum.GetValues<TipoObrigacao>());
        tipo.SelectedValue = modelo.Tipo;
        var periodicidade = DialogUi.EnumCombo<Periodicidade>(Enum.GetValues<Periodicidade>());
        periodicidade.SelectedValue = modelo.Periodicidade;
        var regra = DialogUi.EnumCombo<RegraVencimento>(Enum.GetValues<RegraVencimento>());
        regra.SelectedValue = modelo.Regra;
        var dia = DialogUi.Text(modelo.Dia.ToString());
        var meses = DialogUi.Text(modelo.MesesAposCompetencia.ToString());
        var mesVencimento = DialogUi.Text(modelo.MesVencimento.ToString());
        var regime = DialogUi.EnumCombo<RegimeTributario>(
            RegimeTributario.Todos, RegimeTributario.SimplesNacional,
            RegimeTributario.LucroPresumido, RegimeTributario.LucroReal,
            RegimeTributario.SimplesNacional | RegimeTributario.LucroPresumido,
            RegimeTributario.SimplesNacional | RegimeTributario.LucroReal,
            RegimeTributario.LucroPresumido | RegimeTributario.LucroReal);
        regime.SelectedValue = modelo.RegimesAplicaveis;
        var antecipa = new CheckBox { Content = "Antecipar quando cair em dia não útil", IsChecked = modelo.AntecipaDiaNaoUtil };
        var ativo = new CheckBox { Content = "Modelo ativo", IsChecked = modelo.Ativo };
        form.Children.Add(DialogUi.Field("Nome", nome));
        form.Children.Add(DialogUi.Field("Descrição", descricao));
        form.Children.Add(DialogUi.Field("Tipo", tipo));
        form.Children.Add(DialogUi.Field("Periodicidade", periodicidade));
        form.Children.Add(DialogUi.Field("Regra do vencimento", regra));
        form.Children.Add(DialogUi.Field("Dia fixo / dia útil", dia));
        form.Children.Add(DialogUi.Field("Meses após competência", meses));
        form.Children.Add(DialogUi.Field("Mês de vencimento anual", mesVencimento));
        form.Children.Add(DialogUi.Field("Regimes aplicáveis", regime));
        form.Children.Add(antecipa);
        form.Children.Add(ativo);
        DialogUi.Buttons(content, () =>
        {
            if (!int.TryParse(dia.Text, out var diaValor) ||
                !int.TryParse(meses.Text, out var mesesValor) ||
                !int.TryParse(mesVencimento.Text, out var mesValor))
            {
                MessageBox.Show(this, "Informe valores numéricos válidos para dia e meses.", "Validação",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            modelo.Nome = nome.Text;
            modelo.Descricao = descricao.Text;
            modelo.Tipo = (TipoObrigacao)tipo.SelectedValue;
            modelo.Periodicidade = (Periodicidade)periodicidade.SelectedValue;
            modelo.Regra = (RegraVencimento)regra.SelectedValue;
            modelo.Dia = diaValor;
            modelo.MesesAposCompetencia = mesesValor;
            modelo.MesVencimento = mesValor;
            modelo.RegimesAplicaveis = (RegimeTributario)regime.SelectedValue;
            modelo.AntecipaDiaNaoUtil = antecipa.IsChecked == true;
            modelo.Ativo = ativo.IsChecked == true;
            Resultado = modelo;
            DialogResult = true;
        }, this);
        Content = content;
    }
}

public sealed class ModeloChecklistEditWindow : Window
{
    public ChecklistModelo? Resultado { get; private set; }
    public List<string> Itens { get; private set; } = new();

    public ModeloChecklistEditWindow(ChecklistModelo modelo, IReadOnlyList<Empresa> empresas)
    {
        Title = modelo.Id == 0 ? "Novo modelo de checklist" : "Editar modelo de checklist";
        Width = 560;
        Height = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = DialogUi.Content(Title, out var form);
        var nome = DialogUi.Text(modelo.Nome);
        var regimes = DialogUi.EnumCombo<RegimeTributario>(
            RegimeTributario.Todos, RegimeTributario.SimplesNacional,
            RegimeTributario.LucroPresumido, RegimeTributario.LucroReal);
        regimes.SelectedValue = modelo.RegimesAplicaveis;
        var empresa = DialogUi.Combo(new Empresa?[] { null }.Concat(empresas),
            e => e?.RazaoSocial ?? "Aplicar aos regimes");
        empresa.SelectedValue = empresas.FirstOrDefault(e => e.Id == modelo.EmpresaId);
        var ativo = new CheckBox { Content = "Modelo ativo", IsChecked = modelo.Ativo };
        var itens = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 180,
            Text = string.Join(Environment.NewLine, modelo.Itens.OrderBy(i => i.Ordem).Select(i => i.Descricao))
        };
        form.Children.Add(DialogUi.Field("Nome", nome));
        form.Children.Add(DialogUi.Field("Regimes aplicáveis", regimes));
        form.Children.Add(DialogUi.Field("Empresa específica (opcional)", empresa));
        form.Children.Add(ativo);
        form.Children.Add(DialogUi.Field("Itens (um por linha)", itens));
        DialogUi.Buttons(content, () =>
        {
            modelo.Nome = nome.Text;
            modelo.RegimesAplicaveis = (RegimeTributario)regimes.SelectedValue;
            modelo.EmpresaId = (empresa.SelectedValue as Empresa)?.Id;
            modelo.Ativo = ativo.IsChecked == true;
            Itens = itens.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            Resultado = modelo;
            DialogResult = true;
        }, this);
        Content = content;
    }
}

public sealed class ObrigacaoEditWindow : Window
{
    public ObrigacaoEdicao? Resultado { get; private set; }

    public ObrigacaoEditWindow(ObrigacaoEdicao dados, IReadOnlyList<Empresa> empresas, IReadOnlyList<Usuario> usuarios)
    {
        Title = dados.Id == 0 ? "Nova obrigação" : "Editar obrigação";
        Width = 540;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = DialogUi.Content(Title, out var form);
        var empresa = DialogUi.Combo(empresas, e => e.RazaoSocial);
        empresa.SelectedValue = empresas.FirstOrDefault(e => e.Id == dados.EmpresaId) ?? empresas.FirstOrDefault();
        var nome = DialogUi.Text(dados.Nome);
        var tipo = DialogUi.EnumCombo<TipoObrigacao>(Enum.GetValues<TipoObrigacao>());
        tipo.SelectedValue = dados.Tipo;
        var competencia = DialogUi.DatePicker(dados.Competencia);
        var vencimento = DialogUi.DatePicker(dados.Vencimento);
        var responsavel = DialogUi.Combo(new Usuario?[] { null }.Concat(usuarios), u => u?.Nome ?? "Sem responsável");
        responsavel.SelectedValue = usuarios.FirstOrDefault(u => u.Id == dados.ResponsavelId);
        var observacoes = DialogUi.Text(dados.Observacoes, multiline: true);
        form.Children.Add(DialogUi.Field("Empresa", empresa));
        form.Children.Add(DialogUi.Field("Obrigação", nome));
        form.Children.Add(DialogUi.Field("Tipo", tipo));
        form.Children.Add(DialogUi.Field("Competência", competencia));
        form.Children.Add(DialogUi.Field("Vencimento", vencimento));
        form.Children.Add(DialogUi.Field("Responsável", responsavel));
        form.Children.Add(DialogUi.Field("Observações", observacoes));
        DialogUi.Buttons(content, () =>
        {
            if (empresa.SelectedValue is not Empresa e || competencia.SelectedDate is not DateTime c ||
                vencimento.SelectedDate is not DateTime v || string.IsNullOrWhiteSpace(nome.Text))
            {
                MessageBox.Show(this, "Preencha empresa, obrigação, competência e vencimento.", "Validação",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            dados.EmpresaId = e.Id;
            dados.Nome = nome.Text;
            dados.Tipo = (TipoObrigacao)tipo.SelectedValue;
            dados.Competencia = DateOnly.FromDateTime(c);
            dados.Vencimento = DateOnly.FromDateTime(v);
            dados.ResponsavelId = (responsavel.SelectedValue as Usuario)?.Id;
            dados.Observacoes = observacoes.Text;
            Resultado = dados;
            DialogResult = true;
        }, this);
        Content = content;
    }
}

public sealed class CompetenciaWindow : Window
{
    public DateOnly? Resultado { get; private set; }

    public CompetenciaWindow(string titulo, string mensagem, DateOnly sugestao)
    {
        Title = titulo;
        Width = 500;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var content = DialogUi.Content(titulo, out var form);
        form.Children.Add(new TextBlock
        {
            Text = mensagem,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
        var competencia = DialogUi.DatePicker(sugestao);
        form.Children.Add(DialogUi.Field("Mês de competência", competencia));
        DialogUi.Buttons(content, () =>
        {
            if (competencia.SelectedDate is not DateTime data)
            {
                MessageBox.Show(this, "Selecione uma competência.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Resultado = new DateOnly(data.Year, data.Month, 1);
            DialogResult = true;
        }, this);
        Content = content;
    }
}

internal static class DialogUi
{
    public static DockPanel Content(string titulo, out StackPanel form)
    {
        var root = new DockPanel { Margin = new Thickness(20), LastChildFill = false };
        var header = new TextBlock
        {
            Text = titulo,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        form = new StackPanel();
        root.Children.Add(new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        return root;
    }

    public static void Buttons(Panel root, Action salvar, Window janela)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttons.Children.Add(new Button
        {
            Content = "Cancelar",
            IsCancel = true,
            MinWidth = 90,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 6, 10, 6)
        });
        var ok = new Button
        {
            Content = "Salvar",
            IsDefault = true,
            MinWidth = 90,
            Padding = new Thickness(10, 6, 10, 6)
        };
        ok.Click += (_, _) => salvar();
        buttons.Children.Add(ok);
        DockPanel.SetDock(buttons, Dock.Bottom);
        if (root is DockPanel dock) dock.Children.Add(buttons);
    }

    public static StackPanel Field(string label, UIElement control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(control);
        return panel;
    }

    public static TextBox Text(string? value, bool multiline = false) => new()
    {
        Text = value ?? "",
        MinWidth = 220,
        Padding = new Thickness(6),
        AcceptsReturn = multiline,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        MinHeight = multiline ? 70 : 28,
        VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
    };

    public static ComboBox Combo<T>(IEnumerable<T> values, Func<T, string> texto)
    {
        var combo = new ComboBox { MinWidth = 220, Padding = new Thickness(5) };
        foreach (var value in values)
            combo.Items.Add(new ComboBoxItem<T>(value, texto(value)));
        combo.DisplayMemberPath = nameof(ComboBoxItem<T>.Texto);
        combo.SelectedValuePath = nameof(ComboBoxItem<T>.Valor);
        return combo;
    }

    public static ComboBox EnumCombo<T>(params T[] values) where T : struct, Enum
    {
        var combo = new ComboBox { MinWidth = 220, Padding = new Thickness(5) };
        foreach (var value in values)
            combo.Items.Add(new ComboBoxItem<T>(value, Rotulos.Para(value)));
        combo.DisplayMemberPath = nameof(ComboBoxItem<T>.Texto);
        combo.SelectedValuePath = nameof(ComboBoxItem<T>.Valor);
        return combo;
    }

    public static DatePicker DatePicker(DateOnly value) => new()
    {
        SelectedDate = value.ToDateTime(TimeOnly.MinValue),
        MinWidth = 220
    };

    private sealed record ComboBoxItem<T>(T Valor, string Texto);
}
