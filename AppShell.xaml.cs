namespace StrataVelyx;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
	}
	
	// Menu handlers - forward to MainPage
	private async void OnImportWells(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("ImportWells");
		}
	}
	
	private async void OnImportPolygons(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("ImportPolygons");
		}
	}
	
	private async void OnExport(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("Export");
		}
	}
	
	private async void OnNewProject(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("NewProject");
		}
	}
	
	private async void OnSaveProject(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("SaveProject");
		}
	}
	
	private async void OnOpenProject(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("OpenProject");
		}
	}
	
	private async void OnChangeBasemap(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("ChangeBasemap");
		}
	}
	
	private async void OnDrawPolygon(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("DrawPolygon");
		}
	}
	
	private async void OnZoomToFit(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("ZoomToFit");
		}
	}
	
	private async void OnSatelliteBasemap(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("SatelliteBasemap");
		}
	}
	
	private async void OnOSMBasemap(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("OSMBasemap");
		}
	}
	
	private async void OnTopoBasemap(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("TopoBasemap");
		}
	}
	
	private async void OnDarkBasemap(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("DarkBasemap");
		}
	}
	
	private async void OnLightBasemap(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("LightBasemap");
		}
	}
	
	private async void OnTestSpatial(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("TestSpatial");
		}
	}
	
	private async void OnStatistics(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("Statistics");
		}
	}
	
	private async void OnAbout(object? sender, EventArgs e)
	{
		if (CurrentPage is MainPage mainPage)
		{
			await mainPage.HandleMenuAction("About");
		}
	}
}
