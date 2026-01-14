using StrataVelyx.ViewModels;
using StrataVelyx.Services;

namespace StrataVelyx;

public partial class MainPage : ContentPage
{
	private MainViewModel? _viewModel;
	private ChatCommandService? _chatService;

	public MainPage()
	{
		try
		{
			InitializeComponent();
			
			_viewModel = new MainViewModel();
			_chatService = new ChatCommandService();
			
			BindingContext = _viewModel;
			
			// Bind UI elements to ViewModel - with null checks
			if (ChatEntry != null)
				ChatEntry.SetBinding(Entry.TextProperty, nameof(MainViewModel.ChatInput));
			if (StatusLabel != null)
				StatusLabel.SetBinding(Label.TextProperty, nameof(MainViewModel.StatusMessage));
			if (ChatHistoryView != null)
				ChatHistoryView.SetBinding(ItemsView.ItemsSourceProperty, nameof(MainViewModel.ChatHistory));
			
			// Initialize map when loaded
			Loaded += OnPageLoaded;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"MainPage Constructor Error: {ex}");
		}
	}
	
	private void OnPageLoaded(object? sender, EventArgs e)
	{
		try
		{
			if (MapView?.Map != null && _viewModel != null)
			{
				// Delay initialization slightly to ensure everything is ready
				Dispatcher.Dispatch(() =>
				{
					try
					{
						_viewModel.InitializeMap(MapView.Map);
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Map Init Error: {ex}");
						DisplayAlert("Map Error", $"Could not initialize map: {ex.Message}", "OK");
					}
				});
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"OnPageLoaded Error: {ex}");
		}
	}
	
	private void OnImportClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.ImportWellsCommand?.CanExecute(null) == true)
				_viewModel.ImportWellsCommand.Execute(null);
		}
		catch (Exception ex)
		{
			DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private void OnSendCommand(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.SendCommandCommand?.CanExecute(null) == true)
			{
				_viewModel.SendCommandCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private void OnExportClicked(object sender, EventArgs e)
	{
		try
		{
			if (_viewModel?.ExportWellsCommand?.CanExecute(null) == true)
			{
				_viewModel.ExportWellsCommand.Execute(null);
			}
		}
		catch (Exception ex)
		{
			DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private async void OnHelpClicked(object sender, EventArgs e)
	{
		try
		{
			var helpMessage = _chatService?.GetHelpMessage() ?? "Help not available";
			await DisplayAlert("Available Commands", helpMessage, "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", ex.Message, "OK");
		}
	}
	
	private void OnCloseHistory(object sender, EventArgs e)
	{
		try
		{
			if (ChatHistoryPanel != null)
				ChatHistoryPanel.IsVisible = false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"OnCloseHistory Error: {ex}");
		}
	}
}
