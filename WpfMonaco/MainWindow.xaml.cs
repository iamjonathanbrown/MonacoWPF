using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfMonaco
{
    public partial class MainWindow : Window
    {
        const string FileLanguage = "javascript";

        const string DecorationsCollectionName = "decorations";
        const string StylesCollectionName = "styles";
        const string StylesBreakpointClassName = "glyph-breakpoint";
        const string StylesPerfClassName = "glyph-perf";

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
        public static RoutedCommand GetEditorConfigCommand { get; } = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();

            this.Closed += OnClose;
            this.editor.Ready += OnEditorReady;

            CommandBindings.Add(new CommandBinding(NewFileCommand, async (sender, e) => this.tabControl.SelectedItem = await this.editor.CreateFile("Untitled", "// Hello, world", FileLanguage)));
            CommandBindings.Add(new CommandBinding(CloseFileCommand, (sender, e) => _ = this.editor.DeleteFile(e.Parameter as MonacoEditor.File)));
            CommandBindings.Add(new CommandBinding(SetReadOnlyCommand, (sender, e) => _ = this.editor.Configuration.ReadOnly.Set(true)));
            CommandBindings.Add(new CommandBinding(SetEditableCommand, (sender, e) => _ = this.editor.Configuration.ReadOnly.Set(false)));
            CommandBindings.Add(new CommandBinding(ShowLineNumbersCommand, (sender, e) => _ = this.editor.Configuration.LineNumbers.Set(true)));
            CommandBindings.Add(new CommandBinding(HideLineNumbersCommand, (sender, e) => _ = this.editor.Configuration.LineNumbers.Set(false)));
            CommandBindings.Add(new CommandBinding(DarkThemeCommand, (sender, e) => _ = this.editor.Theme.SetDark()));
            CommandBindings.Add(new CommandBinding(LightThemeCommand, (sender, e) => _ = this.editor.Theme.SetLight()));
            CommandBindings.Add(new CommandBinding(AppendTextCommand, (sender, e) => _ = this.editor.Text.Append(this.CurrentFile.Uri, "\n//Test")));
            CommandBindings.Add(new CommandBinding(PrependTextCommand, (sender, e) => _ = this.editor.Text.Prepend(this.CurrentFile.Uri, "//Test\n")));
            CommandBindings.Add(new CommandBinding(GetEditorConfigCommand, async (sender, e) => MessageBox.Show(await this.editor.Configuration.Get())));
        }

        async void OnEditorReady(object sender, EventArgs e)
        {
            this.editor.Ready -= OnEditorReady;

            // Create the decorations collection.
            // The decorations in the collection will be cleared any time the editor's model (file) changes
            await this.editor.Decorations.CreateCollection(DecorationsCollectionName);

            // Create the custom styles
            await this.editor.Styles.CreateCollection(StylesCollectionName);
            await this.editor.Styles.CreateRule(StylesCollectionName, StylesBreakpointClassName, "background-color", "red");
            await this.editor.Styles.CreateRule(StylesCollectionName, StylesPerfClassName, "background-color", "yellow");

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
            await UpdateDecorations(file);
        }

        Task ClearFile()
        {
            return this.editor.ClearFile();
        }

        async Task UpdateDecorations(MonacoEditor.File file)
        {
            // Clear existing decorations
            await this.editor.Decorations.ClearCollection(DecorationsCollectionName);

            // Add new decorations
            await this.editor.Decorations.CreateDecoration(DecorationsCollectionName, new MonacoEditor.Decoration()
            {
                Range = new MonacoEditor.Range { StartLineNumber = 1, EndLineNumber = 1 },
                Options = new MonacoEditor.DecorationOptions() { GlyphMarginClassName = StylesBreakpointClassName }
            });

            await this.editor.Decorations.CreateDecoration(DecorationsCollectionName, new MonacoEditor.Decoration()
            {
                Range = new MonacoEditor.Range { StartLineNumber = 3, EndLineNumber = 3 },
                Options = new MonacoEditor.DecorationOptions() { GlyphMarginClassName = StylesPerfClassName }
            });
        }

        void OnSelectedFileChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = (this.tabControl.SelectedItem is MonacoEditor.File file)
                ? SelectFile(file) : ClearFile();
        }
    }
}
