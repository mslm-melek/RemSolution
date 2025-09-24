namespace RemSolution.Web.AcceptanceTests.Pages;

public class CarPage : BasePage
{
    private readonly IBrowser _browser;
    private IPage _page;

    public CarPage(IBrowser browser, IPage page)
    {
        _browser = browser;
        _page = page;
    }

    public override string PagePath => $"{BaseUrl}/cars";  // adjust route if needed
    public override IBrowser Browser => _browser;
    public override IPage Page
    {
        get => _page;
        set => _page = value;
    }

    // Actions on Cars page
    public async Task ClickCreateCarAsync()
    {
        await Page.ClickAsync("#create-car-btn"); // update selector
    }

    public async Task SetMatriculeAsync(string matricule)
    {
        await Page.FillAsync("#matricule-input", matricule);
    }

    public async Task SetColorAsync(string color)
    {
        await Page.FillAsync("#color-input", color);
    }

    public async Task SaveCarAsync()
    {
        await Page.ClickAsync("#save-car-btn");
    }

    public async Task<bool> CarExistsInListAsync(string matricule)
    {
        return await Page.Locator($"text={matricule}").IsVisibleAsync();
    }
}
