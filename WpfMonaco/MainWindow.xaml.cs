using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfMonaco
{
    public partial class MainWindow : Window
    {
        const string FileLanguage = "javascript";

        const string DecorationsCollectionName = "decorations";
        const string StylesCollectionName = "styles";
        const string StylesBreakpointClassName = "glyph-breakpoint";
        const string StylesPerfClassName = "glyph-perf";

        SolidColorBrush breakpointBrush = new SolidColorBrush(Colors.Red);
        SolidColorBrush perfBrush = new SolidColorBrush(Colors.Yellow);

        MonacoEditor.File CurrentFile => this.tabControl.SelectedItem as MonacoEditor.File;

        public static RoutedCommand NewFileCommand { get; } = new RoutedCommand();
        public static RoutedCommand CloseFileCommand { get; } = new RoutedCommand();
        public static RoutedCommand SetReadOnlyCommand { get; } = new RoutedCommand();
        public static RoutedCommand SetEditableCommand { get; } = new RoutedCommand();
        public static RoutedCommand ShowLineNumbersCommand { get; } = new RoutedCommand();
        public static RoutedCommand HideLineNumbersCommand { get; } = new RoutedCommand();
        public static RoutedCommand DarkThemeCommand { get; } = new RoutedCommand();
        public static RoutedCommand LightThemeCommand { get; } = new RoutedCommand();
        public static RoutedCommand AppendTextCommand { get; } = new RoutedCommand();
        public static RoutedCommand PrependTextCommand { get; } = new RoutedCommand();
        public static RoutedCommand AddDecorationsCommand { get; } = new RoutedCommand();
        public static RoutedCommand ToggleStylesCommand { get; } = new RoutedCommand();
        public static RoutedCommand GetEditorConfigCommand { get; } = new RoutedCommand();
        public static RoutedCommand RunScriptCommand { get; } = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();

            this.Closed += OnClose;
            this.editor.Ready += OnEditorReady;

            // For debugging
            this.textBox.Text = "editor.updateOptions({ glyphMargin: true });";

            CommandBindings.Add(new CommandBinding(NewFileCommand, async (sender, e) => this.tabControl.SelectedItem = await this.editor.CreateFile("Untitled", "// Hello, world", FileLanguage)));
            CommandBindings.Add(new CommandBinding(CloseFileCommand, (sender, e) => _ = this.editor.DeleteFile(e.Parameter as MonacoEditor.File)));
            CommandBindings.Add(new CommandBinding(SetReadOnlyCommand, (sender, e) => _ = this.editor.Config.ReadOnly.Set(true)));
            CommandBindings.Add(new CommandBinding(SetEditableCommand, (sender, e) => _ = this.editor.Config.ReadOnly.Set(false)));
            CommandBindings.Add(new CommandBinding(ShowLineNumbersCommand, (sender, e) => _ = this.editor.Config.LineNumbers.Set(true)));
            CommandBindings.Add(new CommandBinding(HideLineNumbersCommand, (sender, e) => _ = this.editor.Config.LineNumbers.Set(false)));
            CommandBindings.Add(new CommandBinding(DarkThemeCommand, (sender, e) => _ = this.editor.Theme.SetDark()));
            CommandBindings.Add(new CommandBinding(LightThemeCommand, (sender, e) => _ = this.editor.Theme.SetLight()));
            CommandBindings.Add(new CommandBinding(AppendTextCommand, (sender, e) => _ = this.editor.Text.Append(this.CurrentFile.Uri, "\n//Test")));
            CommandBindings.Add(new CommandBinding(PrependTextCommand, (sender, e) => _ = this.editor.Text.Prepend(this.CurrentFile.Uri, "//Test\n")));
            CommandBindings.Add(new CommandBinding(AddDecorationsCommand, (sender, e) => _ = UpdateDecorations(this.CurrentFile)));
            CommandBindings.Add(new CommandBinding(ToggleStylesCommand, (sender, e) => _ = ToggleStyles()));
            CommandBindings.Add(new CommandBinding(GetEditorConfigCommand, async (sender, e) => MessageBox.Show((await this.editor.Config.Get()).Serialize())));
            CommandBindings.Add(new CommandBinding(RunScriptCommand, (sender, e) => this.editor.Script.Execute(this.textBox.Text)));
        }

        async void OnEditorReady(object sender, EventArgs e)
        {
            this.editor.Ready -= OnEditorReady;

            // Create the decorations collection.
            // The decorations in the collection will be cleared any time the editor's model (file) changes
            await this.editor.Decorations.CreateCollection(DecorationsCollectionName);

            // Create the custom styles
            await this.editor.Styles.CreateCollection(StylesCollectionName);
            await UpdateStyles();

            // Add the files
            await this.editor.CreateFile("main.js", "function main() {\n\talert('main!');\n}\n", FileLanguage);
            await this.editor.CreateFile("inc.js", "function inc() {\n\talert('inc!');\n}\n", FileLanguage);
            await this.editor.CreateFile("helper.js", "function helper() {\n\talert('helper!');\n}\n", FileLanguage);

            // Select the first file
            this.tabControl.SelectedIndex = 0;
            await SelectFile(editor.Files[0]);

            // Do some config
            await this.editor.Font.Size.Set(16);
            await this.editor.Font.Family.Set("Segoe UI");

            this.tabControl.SelectionChanged += OnSelectedFileChanged;
        }

        async void OnClose(object sender, System.EventArgs e)
        {
            await this.editor.Close();
        }

        async Task SelectFile(MonacoEditor.File file)
        {
            await this.editor.SelectFile(file);
        }

        Task ClearFile()
        {
            return this.editor.ClearFile();
        }

        Task ToggleStyles()
        {
            // Pretend the theme binding changed and the styles need to be updated
            this.breakpointBrush.Color = (this.breakpointBrush.Color == Colors.Red) ? Colors.Blue : Colors.Red;
            this.perfBrush.Color = (this.perfBrush.Color == Colors.Yellow) ? Colors.Green : Colors.Yellow;
            return UpdateStyles();
        }

        async Task UpdateStyles()
        {
            // Clear any existing styles
            await this.editor.Styles.ClearCollection(StylesCollectionName);

            // Add styles with their current brush values
            await this.editor.Styles.CreateRule(StylesCollectionName, StylesBreakpointClassName, "background-color", this.breakpointBrush.Color.ToHex());
            await this.editor.Styles.CreateRule(StylesCollectionName, StylesPerfClassName, "background-color", this.perfBrush.Color.ToHex());
        }

        async Task UpdateDecorations(MonacoEditor.File file)
        {
            // Show the glyph margin if not already visible
            await this.editor.Config.Glyphs.ShowMargin(true);

            // Clear any existing decorations
            await this.editor.Decorations.ClearCollection(DecorationsCollectionName);

            // Super lame, only very limited html is supported, and not even color names but only hex values
            var hoverHtml = $"<span style=\"color:#ff0000;\">hello!<span>";

            // Add new decorations
            await this.editor.Decorations.CreateDecoration(DecorationsCollectionName, new MonacoEditor.Decoration()
            {
                Range = new MonacoEditor.Range { StartLineNumber = 1, EndLineNumber = 1 },
                Options = new MonacoEditor.DecorationOptions()
                {
                    GlyphMarginClassName = StylesBreakpointClassName,
                    GlyphMarginHoverMessage = new MonacoEditor.MarkdownString { SupportsHtml = true, IsTrusted = true, Value = hoverHtml },
                    LineNumberClassName = StylesPerfClassName,
                    LineNumberHoverMessage = new MonacoEditor.MarkdownString { SupportsHtml = true, IsTrusted = true, Value = hoverHtml },
                }
            });

            await this.editor.Decorations.CreateDecoration(DecorationsCollectionName, new MonacoEditor.Decoration()
            {
                Range = new MonacoEditor.Range { StartLineNumber = 2, StartColumn = 2, EndLineNumber = 2, EndColumn = 7 },
                Options = new MonacoEditor.DecorationOptions() { ClassName = StylesBreakpointClassName }
            });

            await this.editor.Decorations.CreateDecoration(DecorationsCollectionName, new MonacoEditor.Decoration()
            {
                Range = new MonacoEditor.Range { StartLineNumber = 3, StartColumn = 1, EndLineNumber = 3, EndColumn = 5 },
                Options = new MonacoEditor.DecorationOptions() { IsWholeLine = true, ClassName = StylesPerfClassName }
            });
        }

        void OnSelectedFileChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = (this.tabControl.SelectedItem is MonacoEditor.File file)
                ? SelectFile(file) : ClearFile();
        }
    }
}
