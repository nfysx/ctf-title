using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MoeCTF.RE.Tutorial.Host.Services;

namespace MoeCTF.RE.Tutorial.Host;

public class App : Application
{
	private const string BootstrapOnlyArgument = "--bootstrap-only";

	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		if (e.Args.Length != 0 && string.Equals(e.Args[0], "--apply-update", StringComparison.Ordinal))
		{
			base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			base.OnStartup(e);
			HostUpdateInstallResult hostUpdateInstallResult = ((e.Args.Length == 2 && !string.IsNullOrWhiteSpace(e.Args[1])) ? Task.Run(() => HostUpdateInstaller.ApplyUpdateAsync(e.Args[1])).GetAwaiter().GetResult() : HostUpdateInstallResult.Failed("更新申请无效：需要提供一个申请文件路径。"));
			string text = ((e.Args.Length == 2) ? e.Args[1] : null);
			if (!hostUpdateInstallResult.Success && (string.IsNullOrWhiteSpace(text) || HostUpdateInstaller.ShouldShowApplyFailureDialog(text)))
			{
				MessageBox.Show(hostUpdateInstallResult.Message, "MoeCTF 逆向工程指北更新失败", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			Shutdown((!hostUpdateInstallResult.Success) ? 1 : 0);
			return;
		}
		if (e.Args.Length == 1 && string.Equals(e.Args[0], "--bootstrap-only", StringComparison.Ordinal))
		{
			base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			base.OnStartup(e);
			try
			{
				PathService pathService = new PathService();
				bool flag = File.Exists(Path.Combine(pathService.RepoRoot, "courses", "index.json")) && File.Exists(Path.Combine(pathService.RepoRoot, "stages", "index.json")) && File.Exists(Path.Combine(pathService.RepoRoot, "assets", "i18n", "zh-CN.json"));
				Shutdown((!flag) ? 2 : 0);
				return;
			}
			catch
			{
				Shutdown(1);
				return;
			}
		}
		base.OnStartup(e);
		HostUpdateInstaller.CleanupCompletedUpdates(HostUpdateInstaller.GetDefaultDataRoot());
		MainWindow mainWindow = (MainWindow)(base.MainWindow = new MainWindow());
		mainWindow.Show();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MoeCTF.RE.Tutorial;V0.1.0.0;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.2.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
