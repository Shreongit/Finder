
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Finder.Core.Models;
using Finder.Core.Services;
using Finder.Platform.Windows.Services;

namespace Finder.App.Views;

public partial class SearchWindow : Window
{
    private readonly SearchService _searchService;
    private readonly WindowsLauncher _launcher = new();

    private CancellationTokenSource? _searchCancellation;
    private long _queryVersion;
    private bool _isClosing;

    public SearchWindow()
    {
        InitializeComponent();

        _searchService = ((App)System.Windows.Application.Current).SearchService;
        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = Math.Max(0, SystemParameters.PrimaryScreenHeight * 0.16);

        Loaded += SearchWindow_Loaded;
        Closed += SearchWindow_Closed;
        PreviewKeyDown += SearchWindow_PreviewKeyDown;
        IsVisibleChanged += SearchWindow_IsVisibleChanged;
        Deactivated += SearchWindow_Deactivated;
    }

    private void SearchWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && IsLoaded)
        {
            _isClosing = false;
            CancelActiveSearch();
            SearchInput.Clear();
            ResultsList.ItemsSource = null;
            ResultsList.SelectedIndex = -1;
            ResultsList.Visibility = Visibility.Collapsed;
            Height = 150;

            EmptyStateText.Text =
                "Start typing to find apps, files, settings and more";
            EmptyStateText.Visibility = Visibility.Visible;

            UpdatePlaceholder();

            Dispatcher.BeginInvoke(() =>
            {
                Activate();
                SearchInput.Focus();
                Keyboard.Focus(SearchInput);
            }, DispatcherPriority.Input);
        }
        else if (!IsVisible)
        {
            CancelActiveSearch();
            SearchInput.Clear();
            ResultsList.ItemsSource = null;
            ResultsList.SelectedIndex = -1;
            ResultsList.Visibility = Visibility.Collapsed;
            EmptyStateText.Text = "Start typing to find apps, files, settings and more";
            EmptyStateText.Visibility = Visibility.Visible;
            Height = 150;
        }
    }

    private void SearchWindow_Deactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (IsVisible && !IsActive) Hide();
        }, DispatcherPriority.Background);
    }

    private void SearchWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SearchInput.Focus();
        Keyboard.Focus(SearchInput);
        UpdatePlaceholder();
    }

    private async void SearchInput_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        UpdatePlaceholder();

        CancelActiveSearch();
        var version = _queryVersion;

        var query = SearchInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            ResultsList.ItemsSource = null;
            ResultsList.Visibility = Visibility.Collapsed;
            EmptyStateText.Text =
                "Start typing to find apps, files, settings and more";
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;

        EmptyStateText.Text = "Searching...";
        EmptyStateText.Visibility = Visibility.Visible;
        ResultsList.Visibility = Visibility.Collapsed;

        try
        {
            var results = await _searchService.SearchAsync(
                query,
                cancellation.Token);

            if (cancellation.IsCancellationRequested || _isClosing || !IsVisible || version != _queryVersion)
            {
                return;
            }

            ResultsList.ItemsSource = results;
            Height = Math.Clamp(116 + Math.Min(results.Count, 8) * 50, 150, 520);
            ResultsList.SelectedIndex = results.Count > 0 ? 0 : -1;
            ResultsList.Visibility = results.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            EmptyStateText.Text = results.Count > 0
                ? string.Empty
                : "No matching results found";

            EmptyStateText.Visibility = results.Count > 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            // A newer query replaced this one.
        }
        catch (Exception)
        {
            if (!_isClosing && !cancellation.IsCancellationRequested)
            {
                EmptyStateText.Text =
                    "Something went wrong while searching";
                EmptyStateText.Visibility = Visibility.Visible;
                ResultsList.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
        }
    }

    private void SearchWindow_PreviewKeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchInput.Clear();
            SearchInput.Focus();
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = ResultsList.IsKeyboardFocusWithin
                ? Math.Min(ResultsList.Items.Count - 1, ResultsList.SelectedIndex + 1)
                : Math.Max(0, ResultsList.SelectedIndex);
            ResultsList.Focus();
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up &&
            ResultsList.Items.Count > 0 &&
            ResultsList.IsKeyboardFocusWithin)
        {
            if (ResultsList.SelectedIndex <= 0)
            {
                SearchInput.Focus();
                e.Handled = true;
                return;
            }

            ResultsList.SelectedIndex--;
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter &&
            ResultsList.SelectedItem is SearchResult result)
        {
            OpenResult(result);
            e.Handled = true;
        }
    }

    private void SearchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Keyboard navigation is handled centrally by PreviewKeyDown.
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is not null)
        {
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        }
    }

    private void UpdatePlaceholder()
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(SearchInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OpenResult(SearchResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Target))
        {
            return;
        }

        var opened = result.Kind switch
        {
            "App" => _launcher.LaunchApplicationShortcut(result.Target),
            "File" => _launcher.OpenFile(result.Target),
            "Folder" => _launcher.OpenFolder(result.Target),
            "Web" => _launcher.OpenUrl(result.Target),
            "Settings" => _launcher.OpenWindowsSettings(result.Target),
            _ => false
        };

        if (opened)
        {
            Hide();
        }
    }
    
    
    private void ResultsList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchResult result)
        {
            OpenResult(result);
            e.Handled = true;
        }
    }


    private void SearchWindow_Closed(object? sender, EventArgs e)
    {
        _isClosing = true;
        CancelActiveSearch();
    }

    private void CancelActiveSearch()
    {
        _queryVersion++;
        var active = _searchCancellation;
        _searchCancellation = null;
        active?.Cancel();
    }
}
