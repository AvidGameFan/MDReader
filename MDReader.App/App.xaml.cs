using System.Configuration;
using System.Data;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MDReader.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	private const string MutexName = "MDReader_SingleInstance_Mutex";
	private const string PipeName = "MDReader_FileOpen_Pipe";
	private Mutex? _mutex;
	private bool _isFirstInstance;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		_mutex = new Mutex(true, MutexName, out _isFirstInstance);

		if (_isFirstInstance)
		{
			string? initialFilePath = null;
			if (e.Args.Length > 0)
			{
				initialFilePath = e.Args[0];
			}

			var mainWindow = new MainWindow(initialFilePath);
			mainWindow.Show();

			Task.Run(() => ListenForFileOpenRequests(mainWindow));
		}
		else
		{
			if (e.Args.Length > 0)
			{
				SendFilePathToExistingInstance(e.Args[0]);
			}
			Shutdown();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_mutex?.ReleaseMutex();
		_mutex?.Dispose();
		base.OnExit(e);
	}

	private async void ListenForFileOpenRequests(MainWindow mainWindow)
	{
		while (true)
		{
			try
			{
				using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
				await server.WaitForConnectionAsync();

				using var reader = new StreamReader(server);
				var filePath = await reader.ReadLineAsync();

				if (!string.IsNullOrWhiteSpace(filePath))
				{
					Dispatcher.Invoke(() =>
					{
						mainWindow.Activate();
						mainWindow.WindowState = WindowState.Normal;
						_ = mainWindow.LoadFileFromExternal(filePath);
					});
				}
			}
			catch
			{
				break;
			}
		}
	}

	private void SendFilePathToExistingInstance(string filePath)
	{
		try
		{
			using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
			client.Connect(1000);

			using var writer = new StreamWriter(client) { AutoFlush = true };
			writer.WriteLine(filePath);
			writer.Flush();
			client.WaitForPipeDrain();
		}
		catch
		{
			// Failed to send to existing instance
		}
	}
}

