namespace StrataVelyx;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);
		
		// Set minimum window size for Mac
		window.MinimumWidth = 1024;
		window.MinimumHeight = 768;
		
		// Set initial window size
		window.Width = 1400;
		window.Height = 900;
		
		// Set window title
		window.Title = "StrataVelyx - Reservoir GIS";
		
		return window;
	}
}
