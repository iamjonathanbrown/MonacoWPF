using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfMonaco
{
    public partial class MainWindow : Window
    {
        const string FileLanguage = "javascript";

        MonacoEditor.File CurrentFile => this.tabControl.SelectedItem as MonacoEditor.File;

        public static RoutedCommand NewFileCommand { get; } = new RoutedCommand();
        public static RoutedCommand CloseFileCommand { get; } = new RoutedCommand();
        public static RoutedCommand ShowLineNumbersCommand { get; } = new RoutedCommand();
        public static RoutedCommand HideLineNumbersCommand { get; } = new RoutedCommand();
        public static RoutedCommand DarkThemeCommand { get; } = new RoutedCommand();
        public static RoutedCommand LightThemeCommand { get; } = new RoutedCommand();
        public static RoutedCommand AppendTextCommand { get; } = new RoutedCommand();
        public static RoutedCommand PrependTextCommand { get; } = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();

            this.editor.Loaded += OnEditorLoaded;

            CommandBindings.Add(new CommandBinding(NewFileCommand, async (sender, e) => SelectFile(await this.editor.CreateFile("Untitled", "// Hello, world", FileLanguage))));
            CommandBindings.Add(new CommandBinding(CloseFileCommand, (sender, e) => _ = this.editor.CloseFile(e.Parameter as MonacoEditor.File)));
            CommandBindings.Add(new CommandBinding(ShowLineNumbersCommand, (sender, e) => _ = this.editor.LineNumbers.Set(true)));
            CommandBindings.Add(new CommandBinding(HideLineNumbersCommand, (sender, e) => _ = this.editor.LineNumbers.Set(false)));
            CommandBindings.Add(new CommandBinding(DarkThemeCommand, (sender, e) => _ = this.editor.Theme.SetDark()));
            CommandBindings.Add(new CommandBinding(LightThemeCommand, (sender, e) => _ = this.editor.Theme.SetLight()));
            CommandBindings.Add(new CommandBinding(AppendTextCommand, (sender, e) => _ = this.editor.Text.Append(this.CurrentFile.Uri, "\n//Test")));
            CommandBindings.Add(new CommandBinding(PrependTextCommand, (sender, e) => _ = this.editor.Text.Prepend(this.CurrentFile.Uri, "//Test\n")));
        }

        async void OnEditorLoaded(object sender, RoutedEventArgs e)
        {
            this.editor.Loaded -= OnEditorLoaded;

            // For some reason there is no Initialized event on the editor
            // so give a little delay to let it finish setting up, otherwise
            // creating the file models will fail (won't get a valid ID).
            // https://github.com/microsoft/monaco-editor/issues/115
            await Task.Delay(500);

            await this.editor.CreateFile("main.js", "function main() {\n\talert('main!');\n}\n", FileLanguage);
            await this.editor.CreateFile("inc.js", "function inc() {\n\talert('inc!');\n}\n", FileLanguage);
            await this.editor.CreateFile("helper.js", "function helper() {\n\talert('helper!');\n}\n", FileLanguage);

            this.tabControl.SelectedIndex = 0;
            await this.editor.SelectFile(editor.Files[0]);

            await this.editor.Font.Size.Set(16);
            await this.editor.Font.Family.Set("Segoe UI");

            this.tabControl.SelectionChanged += OnSelectedFileChanged;
        }

        void SelectFile(MonacoEditor.File file)
        {
            this.tabControl.SelectedItem = file;
        }

        async void OnSelectedFileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.tabControl.SelectedItem is MonacoEditor.File file)
            {
                await this.editor.SelectFile(this.CurrentFile);
            }
            else
            {
                await this.editor.ClearFile();
            }
        }
    }
}
