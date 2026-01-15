namespace StrataVelyx.TestPages;

public partial class SimplePage : ContentPage
{
	private int _clickCount = 0;

	public SimplePage()
	{
		InitializeComponent();
	}

	private void OnButtonClicked(object sender, EventArgs e)
	{
		_clickCount++;
		ResultLabel.Text = $"Clicked {_clickCount} time{(_clickCount == 1 ? "" : "s")}!";
	}
}
