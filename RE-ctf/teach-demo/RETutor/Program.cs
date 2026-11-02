using System;
using System.Windows;

namespace RETutor;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "RETutor 未处理异常");
            e.Handled = true;
        };
        app.Run(new MainWindow());
    }
}
