using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator.UI;

public partial class HistoryWindow : Window
{
    private readonly HistoryStore _store;

    /// <summary>Raised when the user picks an entry; the host reopens it in the result popup.</summary>
    public event Action<HistoryEntry>? EntryChosen;

    private sealed record Row(HistoryEntry Entry)
    {
        public string Translation => Entry.Translation;
        public string Source => Entry.Source;

        // Each line reads in its own direction, whatever the interface language is.
        public FlowDirection Direction => Loc.DetectFlow(Entry.Translation);
        public TextAlignment Align => Direction == FlowDirection.RightToLeft ? TextAlignment.Right : TextAlignment.Left;
        public FlowDirection SourceDirection => Loc.DetectFlow(Entry.Source);
        public TextAlignment SourceAlign => SourceDirection == FlowDirection.RightToLeft ? TextAlignment.Right : TextAlignment.Left;
        // Mode is stored as a key ("mode.region"); entries written by older versions kept their text, which Loc.T passes through.
        public string Meta => $"{Relative(Entry.When)}  ·  {Entry.Engine}  ·  {Loc.T(Entry.Mode)}";

        private static string Relative(DateTime t)
        {
            var d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return Loc.T("time.now");
            if (d.TotalHours < 1) return Loc.T("time.minutes", (int)d.TotalMinutes);
            if (d.TotalDays < 1) return Loc.T("time.hours", (int)d.TotalHours);
            if (d.TotalDays < 7) return Loc.T("time.days", (int)d.TotalDays);
            return t.ToString("yyyy/MM/dd");
        }
    }

    public HistoryWindow(HistoryStore store)
    {
        InitializeComponent();
        _store = store;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false,
        });

        SourceInitialized += (_, _) =>
        {
            if (!WindowEffects.TryApplyAcrylic(this, Theme.IsDark))
            {
                Shell.Background = Theme.Brush("Surface");
                WindowEffects.SetRoundedCorners(this);
            }
        };

        _store.Changed += Refresh;
        Closed += (_, _) => _store.Changed -= Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var q = SearchBox.Text.Trim();
        IEnumerable<HistoryEntry> items = _store.Entries;
        if (q.Length > 0)
            items = items.Where(e => e.Source.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Translation.Contains(q, StringComparison.OrdinalIgnoreCase));

        var rows = items.Select(e => new Row(e)).ToList();
        List.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = _store.Entries.Count == 0 ? "" : Loc.T("history.count", _store.Entries.Count);
    }

    private void OnSearch(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }

    private void OnSelect(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is Row row)
        {
            List.SelectedItem = null;
            EntryChosen?.Invoke(row.Entry);
        }
    }

    private void OnCopyItem(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Row row })
        {
            try { Clipboard.SetDataObject(row.Translation, true); } catch { }
            e.Handled = true;
        }
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        if (_store.Entries.Count == 0) return;
        if (MessageBox.Show(this, Loc.T("history.confirm"), "ScreenTranslator", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _store.Clear();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
