using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dateimanager.Core;
using Dateimanager.ViewModels;
using Dateimanager.Views;

namespace Dateimanager.Tests;

public class AppearanceSettingsTests
{
    [Theory]
    [InlineData(13, RowSpacing.Compact, 21)]
    [InlineData(13, RowSpacing.Normal, 27)]
    [InlineData(13, RowSpacing.Wide, 33)]
    [InlineData(18, RowSpacing.Compact, 28)]
    public void RowHeight_DependsOnFontSizeAndSpacing(double size, RowSpacing spacing, double expected) =>
        Assert.Equal(expected, new AppearanceSettings { ListFont = new FontSetting("", size), RowSpacing = spacing }.RowHeight);

    [Fact]
    public void Normalized_ClampsSizesAndTrimsNames()
    {
        var settings = new AppearanceSettings
        {
            ListFont = new FontSetting("  DejaVu Sans  ", 3),
            ButtonFont = new FontSetting("", double.NaN),
            RowSpacing = (RowSpacing)42,
        }.Normalized();

        Assert.Equal(new FontSetting("DejaVu Sans", FontSetting.MinSize), settings.ListFont);
        Assert.Equal(new FontSetting("", FontSetting.DefaultSize), settings.ButtonFont);
        Assert.Equal(RowSpacing.Compact, settings.RowSpacing);
    }
}

public sealed class AppearanceStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SaveAndLoad_RoundTripsAppearance()
    {
        var store = new SettingsStore(_temp.Combine("settings.json"));
        var appearance = new AppearanceSettings
        {
            ListFont = new FontSetting("Liberation Mono", 11),
            ButtonFont = new FontSetting("Topaz", 14),
            RowSpacing = RowSpacing.Wide,
        };

        store.Save(new AppSettings { Appearance = appearance });
        var loaded = store.Load();

        Assert.Equal(appearance, loaded.Appearance);
        var json = File.ReadAllText(store.FilePath);
        Assert.Contains("\"rowSpacing\": \"Wide\"", json);
        Assert.DoesNotContain("rowHeight", json);
    }

    [Fact]
    public void Load_OldSettingsWithoutAppearanceUseCompactDefaults()
    {
        var path = _temp.File("settings.json", """{ "leftPath": "/a" }""");

        var appearance = new SettingsStore(path).Load().Appearance;

        Assert.Equal(new AppearanceSettings(), appearance);
        Assert.Equal(RowSpacing.Compact, appearance.RowSpacing);
    }
}

public class AppearanceDialogTests
{
    private static readonly string[] Families = ["Liberation Sans", "DejaVu Sans", "Liberation Mono", "Noto Serif"];

    [AvaloniaFact]
    public void FontChoice_ListsDefaultFirstThenSortedFamilies()
    {
        var choice = new FontChoiceViewModel([.. Families, "dejavu sans", ""], new FontSetting());

        Assert.Equal([FontChoiceViewModel.DefaultFamilyName, "DejaVu Sans", "Liberation Mono", "Liberation Sans", "Noto Serif"],
            choice.FilteredFamilies.Select(f => f.Name));
        Assert.Equal(FontChoiceViewModel.DefaultFamilyName, choice.SelectedFamily?.Name);
        Assert.Equal("DejaVu Sans", choice.FilteredFamilies[1].Family.Name);
        Assert.Equal(new FontSetting("", 13), choice.ToSetting());
    }

    [AvaloniaFact]
    public void FontChoice_FilterKeepsChosenFamilyEvenIfHidden()
    {
        var choice = new FontChoiceViewModel(Families, new FontSetting("Noto Serif", 12));

        choice.Filter = "liberation";
        Assert.Equal(["Liberation Mono", "Liberation Sans"], choice.FilteredFamilies.Select(f => f.Name));
        choice.SelectedFamily = null; // die Liste verliert die Auswahl, weil die Schrift ausgeblendet ist

        Assert.Equal("Noto Serif", choice.ToSetting().Family);

        choice.SelectedFamily = choice.FilteredFamilies[0];
        choice.Size = 15;
        Assert.Equal(new FontSetting("Liberation Mono", 15), choice.ToSetting());
        Assert.Equal("Liberation Mono", choice.PreviewFamily.Name);
    }

    [AvaloniaFact]
    public void Dialog_BuildsResultAndResets()
    {
        var dialog = new AppearanceDialogViewModel(new AppearanceSettings
        {
            ListFont = new FontSetting("DejaVu Sans", 12),
            ButtonFont = new FontSetting("Noto Serif", 14),
            RowSpacing = RowSpacing.Normal,
        }, Families);
        Assert.True(dialog.IsNormal);

        dialog.IsWide = true;
        dialog.ListFont.Size = 20;
        Assert.Equal(26 + 16, dialog.PreviewRowHeight);
        AppearanceSettings? result = null;
        dialog.CloseRequested += (_, r) => result = r;

        dialog.SaveCommand.Execute(null);
        Assert.Equal(new AppearanceSettings
        {
            ListFont = new FontSetting("DejaVu Sans", 20),
            ButtonFont = new FontSetting("Noto Serif", 14),
            RowSpacing = RowSpacing.Wide,
        }, result);

        dialog.ResetToDefaultsCommand.Execute(null);
        Assert.Equal(new AppearanceSettings(), dialog.BuildResult());
        Assert.True(dialog.IsCompact);
    }

    [AvaloniaFact]
    public void Window_ShowsFontsAndClosesWithResult()
    {
        var window = new AppearanceWindow
        {
            DataContext = new AppearanceDialogViewModel(new AppearanceSettings(), Families),
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var list = window.GetVisualDescendants().OfType<ListBox>().First();
        Assert.Equal(5, list.ItemCount);
        Assert.Equal(FontChoiceViewModel.DefaultFamilyName, ((FontOption)list.SelectedItem!).Name);
        var names = list.GetVisualDescendants().OfType<ListBoxItem>().Select(i => i.FindDescendantOfType<TextBlock>()!).ToList();
        Assert.Equal("DejaVu Sans", names.Single(t => t.Text == "DejaVu Sans").FontFamily.Name);
        window.Close();
    }
}

public sealed class AppearanceIntegrationTests : IDisposable
{
    private readonly TestEnvironment _env = new();

    public void Dispose() => _env.Dispose();

    [AvaloniaFact]
    public async Task Lister_UsesCompactRowsByDefault()
    {
        _env.Temp.File("a.txt");
        var window = new MainWindow { DataContext = _env.ViewModel };
        window.Show();
        await _env.ViewModel.InitializeAsync(_env.Temp.Path, _env.Temp.Path);
        Dispatcher.UIThread.RunJobs();

        var grid = window.FindControl<PaneView>("LeftPane")!.Grid;
        var row = grid.GetVisualDescendants().OfType<DataGridRow>().First();
        var cell = row.GetVisualDescendants().OfType<DataGridCell>().First();

        Assert.Equal(21, row.Bounds.Height);
        Assert.Equal(21, cell.Bounds.Height);
        Assert.Equal(13, grid.FontSize);
        window.Close();
    }

    [AvaloniaFact]
    public async Task EditAppearance_AppliesToListersAndButtonsAndSaves()
    {
        var window = new MainWindow { DataContext = _env.ViewModel };
        window.Show();
        await _env.ViewModel.InitializeAsync(_env.Temp.Path, _env.Temp.Path);
        _env.Dialogs.AppearanceAnswers.Enqueue(new AppearanceSettings
        {
            ListFont = new FontSetting("DejaVu Sans", 18),
            ButtonFont = new FontSetting("", 11),
            RowSpacing = RowSpacing.Normal,
        });

        await _env.ViewModel.EditAppearanceCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        foreach (var pane in new[] { "LeftPane", "RightPane" })
        {
            var grid = window.FindControl<PaneView>(pane)!.Grid;
            Assert.Equal(18, grid.FontSize);
            Assert.Equal("DejaVu Sans", grid.FontFamily.Name);
            Assert.Equal(24 + 10, grid.RowHeight);
        }

        var bank = window.FindControl<ItemsControl>("ButtonBankItems")!;
        var button = bank.GetRealizedContainers().Select(c => c.FindDescendantOfType<Button>()).First()!;
        Assert.Equal(11, button.FontSize);
        Assert.Equal(FontFamily.Default, button.FontFamily);

        Assert.Equal(RowSpacing.Normal, _env.Store.Load().Appearance.RowSpacing);
        window.Close();
    }

    [AvaloniaFact]
    public async Task EditAppearance_CancelChangesNothing()
    {
        _env.Dialogs.AppearanceAnswers.Enqueue(null);

        await _env.ViewModel.EditAppearanceCommand.ExecuteAsync(null);

        Assert.Equal(21, _env.ViewModel.Appearance.RowHeight);
        Assert.False(File.Exists(_env.Store.FilePath));
    }
}
