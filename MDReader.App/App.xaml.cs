using System.Configuration;
using System.Data;
using System.Windows;

namespace MDReader.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		string? initialFilePath = null;
		if (e.Args.Length > 0)
		{
			initialFilePath = e.Args[0];
		}

		var mainWindow = new MainWindow(initialFilePath);
		mainWindow.Show();
	}
}

