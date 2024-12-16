using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace WpfMonaco
{
    public class MonacoEditor : Control
    {
        const int DefaultLineNumber = 1;
        const int DefaultColumnNumber = 1;

        const string EditorGlobalName = "monaco.editor";
        const string EditorLocalName = "editor"; // Must match the JS variable name in the HTML
        const string EditorDomain = "test.editor";
        const string EditorBaseUrl = "http://" + EditorDomain;
        const string EditorBaseDir = "Monaco";
        const string IndexFileName = "index.html";

        WebView2 webView;
        File currentFile;

        // Internal commands
        ModelCommandManager Models { get; set; }
        StateCommandMnager State { get; set; }

        // External commands
        public TextCommandManager Text { get; private set; }
        public FontCommandManager Font { get; private set; }
        public LocationCommandManager Location { get; private set; }
        public ConfigurationCommandManager Config { get; private set; }
        public StyleCommandManager Styles { get; private set; }
        public DecorationCommandManager Decorations { get; private set; }
        public ThemeCommandManager Theme { get; private set; }
        public ScriptCommandManager Script { get; private set; }

        public ObservableCollection<File> Files { get; } = new ObservableCollection<File>();

        public EventHandler Ready;

        static MonacoEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MonacoEditor), new FrameworkPropertyMetadata(typeof(MonacoEditor)));
        }

        public async override void OnApplyTemplate()
        {
            this.webView = GetTemplateChild("PART_WebView") as WebView2;

            // Create the command executors
            this.Models = new ModelCommandManager(this.webView);
            this.State = new StateCommandMnager(this.webView);
            this.Text = new TextCommandManager(this.webView);
            this.Font = new FontCommandManager(this.webView);
            this.Location = new LocationCommandManager(this.webView);
            this.Config = new ConfigurationCommandManager(this.webView);
            this.Styles = new StyleCommandManager(this.webView);
            this.Decorations = new DecorationCommandManager(this.webView);
            this.Theme = new ThemeCommandManager(this.webView);
            this.Script = new ScriptCommandManager(this.webView);

            // Ensure the WebView is ready before we start using it
            await this.webView.EnsureCoreWebView2Async();

            // Open the dev tools for debugging
            this.webView.CoreWebView2.OpenDevToolsWindow();

            /* Not getting all log messages, just the first one for some reason. But having the console open helps
            // Register to retrieve console output
            var res = await this.webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable", "{}");
            var logEventReceiver = webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Log.entryAdded");

            logEventReceiver.DevToolsProtocolEventReceived += (s, e) =>
            {
                Debug.WriteLine($"JS Console: {e.ParameterObjectAsJson}");
            };
            */

            /*
            // Map the folder to a domain so that things like web workers can work
            this.webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    EditorDomain, EditorBaseDir, CoreWebView2HostResourceAccessKind.Allow);

            // Set the initial location
            this.webView.Source = new Uri(new Uri(EditorBaseUrl), IndexFileName);
            */

            this.webView.CoreWebView2.DOMContentLoaded += OnDomLoaded;
            this.webView.Source = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EditorBaseDir, IndexFileName));
        }

        public async Task Close()
        {
            this.webView?.CoreWebView2.ClearVirtualHostNameToFolderMapping(EditorDomain);

            foreach (var file in this.Files.ToList())
            {
                await DeleteFile(file);
            }

            await this.Styles.DeleteAllCollections();
        }

        void OnDomLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            this.webView.CoreWebView2.DOMContentLoaded -= OnDomLoaded;
            this.Ready?.Invoke(this, EventArgs.Empty);
        }

        public async Task<File> CreateFile(string name, string content, string language = "")
        {
            var uri = await this.Models.Create(content, language);
            var file = new File(name, uri);
            this.Files.Add(file);

            return file;
        }

        public async Task DeleteFile(File file)
        {
            await this.Models.Dispose(file.Uri);
            this.Files.Remove(file);
        }

        public async Task SelectFile(File file)
        {
            // Save the current file state
            if (this.currentFile != null)
            {
                this.currentFile.SetState(await this.State.Get());
            }

            // Select the new file and set its state
            if (file?.Uri != null)
            {
                await this.Models.SetActive(file.Uri);
                await this.State.Set(file.State);
                this.currentFile = file;
            }
            else
            {
                await ClearFile();
            }
        }

        public async Task ClearFile()
        {
            await this.Models.SetNull();
            this.currentFile = null;
        }

        public abstract class CommandManager
        {
            WebView2 webView;

            protected string GetModel(string uri) => $"{EditorGlobalName}.getModel({uri.Serialize()})";

            protected CommandManager(WebView2 webView)
            {
                this.webView = webView;
            }

            protected async Task<T> ExecuteScript<T>(string script)
            {
                await CheckAccess();
                var result = await this.webView.ExecuteScriptAsync(script);

                Debug.WriteLine($"-----\nScript: {script}\nOutput: {result}\n-----");
                return result.Deserialize<T>();
            }

            protected async Task ExecuteScript(string script)
            {
                await CheckAccess();
                var result = await this.webView.ExecuteScriptAsync(script);
                Debug.WriteLine($"-----\nScript: {script}\nOutput: {result}\n-----");
            }

            Task CheckAccess()
            {
                return this.webView.EnsureCoreWebView2Async();
            }
        }

        public class FontCommandManager : CommandManager
        {
            public FontSizeCommandManager Size { get; }
            public FontFamilyCommandManager Family { get; }

            public FontCommandManager(WebView2 webView) : base(webView)
            {
                this.Size = new FontSizeCommandManager(webView);
                this.Family = new FontFamilyCommandManager(webView);
            }

            public class FontSizeCommandManager : CommandManager
            {
                public Task Set(int value) => ExecuteScript($"{EditorLocalName}.updateOptions({{ fontSize: {value} }})");

                public FontSizeCommandManager(WebView2 webView) : base(webView)
                { }
            }

            public class FontFamilyCommandManager : CommandManager
            {
                public Task Set(string value) => ExecuteScript($"{EditorLocalName}.updateOptions({{ fontFamily: {value.Serialize()} }})");

                public FontFamilyCommandManager(WebView2 webView) : base(webView)
                { }
            }
        }

        public class LocationCommandManager : CommandManager
        {
            public Task GoToLine(int line) => ExecuteScript($"{EditorLocalName}.revealLineInNearTop({line})");
            public Task GoToPosition(Position position) => ExecuteScript($"{EditorLocalName}.revealPositionNearTop({position.Serialize()})");
            public Task GoToRange(Range range) => ExecuteScript($"{EditorLocalName}.revealRangeNearTop({range.Serialize()})");

            public LocationCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class ThemeCommandManager : CommandManager
        {
            public static string DarkTheme = "vs-dark";
            public static string LightTheme = "vs";

            public Task<string> Get() => ExecuteScript<string>($"{EditorLocalName}._themeService.getColorTheme().id");
            public Task SetDark() => ExecuteScript($"{EditorGlobalName}.setTheme({DarkTheme.Serialize()})");
            public Task SetLight() => ExecuteScript($"{EditorGlobalName}.setTheme({LightTheme.Serialize()})");

            public ThemeCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class TextCommandManager : CommandManager
        {
            public Task<string> Get(string uri) => ExecuteScript<string>($"{GetModel(uri)}.getValue()");
            public Task Set(string uri, string value) => ExecuteScript($"{GetModel(uri)}.setValue({value.Serialize()})");

            public Task<Position> GetEofPosition(string uri) => ExecuteScript<Position>($"{GetModel(uri)}.getPositionAt({GetModel(uri)}.getValueLength())");

            public async Task Append(string uri, string value) => await Insert(uri, value, new Range(await GetEofPosition(uri)));
            public Task Prepend(string uri, string value) => Insert(uri, value, new Range());

            public Task Insert(string uri, string value, Range range) => ExecuteScript($"{GetModel(uri)}.pushEditOperations(" +
                        $"[], " +
                        $"[{{text: {value.Serialize()}, range: {range.Serialize()}}}], " +
                        $"() => null);");


            public TextCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class ModelCommandManager : CommandManager
        {
            public Task<string[]> GetAll() => ExecuteScript<string[]>($"{EditorGlobalName}.getModels().map(m => m.id)");
            public Task<string> Create(string text, string language) => ExecuteScript<string>($"{EditorGlobalName}.createModel({text.Serialize()}, {language.Serialize()}).uri.toString()");
            public Task Dispose(string uri) => ExecuteScript($"{GetModel(uri)}.dispose()");
            public Task SetActive(string uri) => ExecuteScript($"{EditorLocalName}.setModel({GetModel(uri)})");
            public Task SetNull() => ExecuteScript($"{EditorLocalName}.setModel(null)");

            public ModelCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class StateCommandMnager : CommandManager
        {
            public Task<object> Get() => ExecuteScript<object>($"{EditorLocalName}.saveViewState()");
            public Task Set(object state) => ExecuteScript($"{EditorLocalName}.restoreViewState({state.Serialize()})");

            public StateCommandMnager(WebView2 webView) : base(webView)
            { }
        }

        public class ConfigurationCommandManager : CommandManager
        {
            public ReadOnlyCommandManager ReadOnly { get; private set; }
            public LineNumbersCommandManager LineNumbers { get; private set; }
            public GlyphsCommandManager Glyphs { get; private set; }

            public Task<Configuration> Get() => ExecuteScript<Configuration>($"{EditorLocalName}.getRawOptions()");
            public Task Set(Configuration config) => ExecuteScript($"{EditorLocalName}.updateOptions({config.Serialize()})");

            public ConfigurationCommandManager(WebView2 webView) : base(webView)
            {
                this.ReadOnly = new ReadOnlyCommandManager(webView);
                this.LineNumbers = new LineNumbersCommandManager(webView);
                this.Glyphs = new GlyphsCommandManager(webView);
            }

            public class ReadOnlyCommandManager : CommandManager
            {
                // Would be nice to change the editor style to indicate it is readonly
                // https://stackoverflow.com/questions/78282605/how-to-specify-the-background-color-for-when-monaco-is-read-only
                public Task Set(bool value) => ExecuteScript($"{EditorLocalName}.updateOptions({{readOnly: {value.Serialize()} }})");

                public ReadOnlyCommandManager(WebView2 webView) : base(webView)
                { }
            }

            public class LineNumbersCommandManager : CommandManager
            {
                public Task Set(bool value) => ExecuteScript($"{EditorLocalName}.updateOptions({{ lineNumbers: {ToState(value).ToString().ToLower().Serialize()} }})");

                public LineNumbersCommandManager(WebView2 webView) : base(webView)
                { }

                public enum OnOffState
                {
                    On,
                    Off,
                }

                OnOffState ToState(string value)
                {
                    if (Enum.TryParse(value, true, out OnOffState result) &&
                        Enum.IsDefined(typeof(OnOffState), result))
                    {
                        return result;
                    }

                    Debug.Assert(false, $"Invalid {nameof(OnOffState)}: {value}");
                    return OnOffState.Off;
                }

                OnOffState ToState(bool value) => value switch
                {
                    true => OnOffState.On,
                    false => OnOffState.Off,
                };

                bool ToBool(OnOffState state) => state switch
                {
                    OnOffState.On => true,
                    OnOffState.Off => false,
                    _ => false,
                };
            }

            public class GlyphsCommandManager : CommandManager
            {
                public Task ShowMargin(bool value) => ExecuteScript($"{EditorLocalName}.updateOptions({{ glyphMargin: {value.Serialize()} }})");

                public GlyphsCommandManager(WebView2 webView) : base(webView)
                { }
            }
        }

        public class StyleCommandManager : CommandManager
        {
            public Task CreateCollection(string collectionName) => ExecuteScript($"const {collectionName} = new CSSStyleSheet(); document.adoptedStyleSheets.push({collectionName});");
            public Task ClearCollection(string collectionName) => ExecuteScript($"{collectionName}.replace('')");
            public Task DeleteCollection(string collectionName) => ExecuteScript($"document.adoptedStyleSheets = document.adoptedStyleSheets.filter(s => s !== {collectionName})");

            public Task CreateRule(string collectionName, string className, string property, string value) => ExecuteScript($"{collectionName}.insertRule(\".{className} {{ {property}: {value}; }}\")");
            public Task DeleteRule(string collectionName, int index) => ExecuteScript($"{collectionName}.deleteRule({index})");

            public Task DeleteAllCollections() => ExecuteScript("document.adoptedStyleSheets = []");

            public StyleCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class DecorationCommandManager : CommandManager
        {
            public Task CreateCollection(string collectionName) => ExecuteScript($"const {collectionName} = {EditorLocalName}.createDecorationsCollection()");
            public Task ClearCollection(string collectionName) => ExecuteScript($"{collectionName}.clear()");

            public Task CreateDecoration(string collectionName, Decoration decoration) => ExecuteScript($"{collectionName}.append([{decoration.Serialize()}])");

            public DecorationCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class ScriptCommandManager : CommandManager
        {
            public Task Execute(string script) => ExecuteScript(script);

            public ScriptCommandManager(WebView2 webView) : base(webView)
            { }
        }

        public class File
        {
            public string Name { get; }
            public string Uri { get; private set; }
            public object State { get; private set; }

            public File(string name, string uri)
            {
                this.Name = name;
                this.Uri = uri;
            }

            public void SetState(object state)
            {
                this.State = state;
            }
        }

        public class Position
        {
            public int LineNumber { get; set; } = DefaultLineNumber;
            public int Column { get; set; } = DefaultColumnNumber;
        }

        public class Range
        {
            public int StartLineNumber { get; set; } = DefaultLineNumber;
            public int StartColumn { get; set; } = DefaultColumnNumber;
            public int EndLineNumber { get; set; } = DefaultLineNumber;
            public int EndColumn { get; set; } = DefaultColumnNumber;

            public Range()
            { }

            public Range(Position position)
            {
                this.StartLineNumber = position.LineNumber;
                this.StartColumn = position.Column;
                this.EndLineNumber = position.LineNumber;
                this.EndColumn = position.Column;
            }
        }

        public class Decoration
        {
            public Range Range { get; set; }
            public DecorationOptions Options { get; set; }
        }

        public class DecorationOptions
        {
            public bool IsWholeLine { get; set; }
            public DecoratorStickiness Stickiness { get; set; } = DecoratorStickiness.NeverGrowsWhenTypingAtEdges;

            public string ClassName { get; set; }
            public string InlineClassName { get; set; }

            public string LineNumberClassName { get; set; }
            public MarkdownString LineNumberHoverMessage { get; set; }

            public string GlyphMarginClassName { get; set; }
            public MarkdownString GlyphMarginHoverMessage { get; set; }
        }

        public enum DecoratorStickiness
        {
            AlwaysGrowsWhenTypingAtEdges,
            NeverGrowsWhenTypingAtEdges,
            GrowsOnlyWhenTypingBefore,
            GrowsOnlyWhenTypingAfter,
        }

        public class MarkdownString
        {
            public bool SupportsHtml { get; set; }
            public bool IsTrusted { get; set; } // Appears to be required for rendering HTML
            public string Value { get; set; }
        }

        public class Configuration
        {
            public string FontFamily { get; set; }
            public int FontSize { get; set; }
            public bool ReadOnly { get; set; }
            public bool GlyphMargin { get; set; }
        }
    }
}
