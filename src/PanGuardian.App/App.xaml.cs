using System.Windows;
using System.Windows.Threading;

namespace PanGuardian.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowFriendlyError(e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFriendlyError(ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ShowFriendlyError(e.Exception);
    }

    private static void ShowFriendlyError(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"程序遇到一个未预期的问题，但已尽量避免直接崩溃。\n\n{ex.GetBaseException().Message}",
                "豆豆蛙磁盘管家",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // 如果 UI 尚未初始化或正在退出，避免异常处理器自身再次抛错。
        }
    }
}
