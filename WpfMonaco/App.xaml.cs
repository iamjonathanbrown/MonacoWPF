using System.Text;
using System.Windows;

namespace WpfMonaco
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerUnobservedTaskException;
        }

        void TaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
        }

        void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleUnhandledException(e.ExceptionObject as Exception);
        }

        public void HandleUnhandledException(Exception ex)
        {
            if (ExceptionIsCancellation(ex))
            {
                return;
            }

            var sb = new StringBuilder();
            Exception innermost = ex;

            sb.AppendLine($"Unhandled exception:");

            while (ex != null)
            {
                sb.AppendLine($"{ex.GetType().Name}: {ex.Message}");
                sb.AppendLine(ex.StackTrace);

                innermost = ex;
                ex = ex.InnerException;
                if (ex != null)
                {
                    sb.AppendLine("Inner Exception:");
                }
            }

            MessageBox.Show($"{innermost.GetType().Name}: {innermost.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        static bool ExceptionIsCancellation(Exception ex)
        {
            var exceptionsToProcess = new Queue<Exception>();

            exceptionsToProcess.Enqueue(ex);
            while (exceptionsToProcess.Count > 0)
            {
                Exception curException = exceptionsToProcess.Dequeue();

                if (curException is OperationCanceledException)
                {
                    return true;
                }

                if (curException is AggregateException aggException)
                {
                    foreach (Exception inner in aggException.InnerExceptions)
                    {
                        exceptionsToProcess.Enqueue(inner);
                    }
                }
            }

            return false;
        }
    }
}
