using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Web.WebView2.Wpf;

namespace WpfMonaco
{
    public class MonacoEditor : Control
    {
        const string EditorGlobalName = "monaco.editor";
        const string EditorLocalName = "editor"; // Must match the JS variable name in the HTML
        const string EditorDomain = "test.editor";
        const string EditorBaseUrl = "http://" + EditorDomain;
        const string EditorBaseDir = "Monaco";
        const string IndexFileName = "index.html";

        WebView2 webView;

        // Internal commands
        ModelCommandManager Models { get; set; }

        // External commands
        public TextCommandManager Text { get; private set; }
        public FontCommandManager Font { get; private set; }
        public LineNumbersCommandManager LineNumbers { get; private set; }
        public ConfigurationCommandManager Configuration { get; private set; }
        public ThemeCommandManager Theme { get; private set; }

        public ObservableCollection<File> Files { get; } = new ObservableCollection<File>();

        static MonacoEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MonacoEditor), new FrameworkPropertyMetadata(typeof(MonacoEditor)));
        }

        public async override void OnApplyTemplate()
        {
            this.webView = GetTemplateChild("PART_WebView") as WebView2;

            // Create the command executors
            this.Models = new ModelCommandManager(this.webView);
            this.Text = new TextCommandManager(this.webView);
            this.Font = new FontCommandManager(this.webView);
            this.LineNumbers = new LineNumbersCommandManager(this.webView);
            this.Configuration = new ConfigurationCommandManager(this.webView);
            this.Theme = new ThemeCommandManager(this.webView);

            // Ensure the WebView is ready before we start using it
            await this.webView.EnsureCoreWebView2Async();

            // Register to retrieve console output
            this.webView.CoreWebView2.OpenDevToolsWindow();

            /* Not getting all log messages, just the first one for some reason. But having the console open helps
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
                    editorDomain, editorBaseDir, CoreWebView2HostResourceAccessKind.Allow);

            // Set the initial locations
            this.webView.Source = new Uri(new Uri(editorBaseUrl), indexFileName);
            */

            this.webView.Source = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EditorBaseDir, IndexFileName));
        }

        public async Task<File> CreateFile(string name, string content, string language = "")
        {
            var uri = await this.Models.Create(content, language);
            var file = new File(name, content, language, uri);
            this.Files.Add(file);

            return file;
        }

        public async Task CloseFile(File file)
        {
            await this.Models.Dispose(file.Uri);
            this.Files.Remove(file);
        }

        public async Task SelectFile(File file)
        {
            await this.Models.SetActive(file?.Uri ?? null);
        }

        public async Task ClearFile()
        {
            await this.Models.SetNull();
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
                public Task<string> Get() => ExecuteScript<string>($"{EditorLocalName}.getConfiguration().fontSize");
                public Task Set(int value) => ExecuteScript($"{EditorLocalName}.updateOptions({{ fontSize: {value} }})");

                public FontSizeCommandManager(WebView2 webView) : base(webView)
                { }
            }

            public class FontFamilyCommandManager : CommandManager
            {
                public Task<int> Get() => ExecuteScript<int>($"{EditorLocalName}.getConfiguration().fontFamily");
                public Task Set(string value) => ExecuteScript($"{EditorLocalName}.updateOptions({{ fontFamily: {value.Serialize()} }})");

                public FontFamilyCommandManager(WebView2 webView) : base(webView)
                { }
            }
        }

        public class LineNumbersCommandManager : CommandManager
        {
            public async Task<bool> Get() => ToBool(ToState(await ExecuteScript<string>($"{EditorLocalName}.getConfiguration().lineNumbers")));
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

        public class ConfigurationCommandManager : CommandManager
        {
            public Task<string> Get() => ExecuteScript<string>($"{EditorLocalName}.getConfiguration()");
            public Task Set(string value) => ExecuteScript($"{EditorLocalName}.updateOptions({value.Serialize()})");
            public ConfigurationCommandManager(WebView2 webView ) : base(webView)
            { }
        }

        public class File
        {
            public string Name { get; }
            public string Content { get; }
            public string Language { get; }
            public string Uri { get; private set; }

            public File(string name, string content, string language, string uri)
            {
                this.Name = name;
                this.Content = content;
                this.Language = language;
                this.Uri = uri;
            }
        }

        public class Position
        {
            public int LineNumber { get; set; }
            public int Column { get; set; }
        }

        public class Range
        {
            const int Default = 1;

            public int StartLineNumber { get; set; } = Default;
            public int StartColumn { get; set; } = Default;
            public int EndLineNumber { get; set; } = Default;
            public int EndColumn { get; set; } = Default;

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
    }
}
