

namespace RemSolution.Web.AcceptanceTests.StepDefinitions;

[Binding]
public sealed class CarStepDefinitions
{
    private readonly CarPage _carPage;

    public CarStepDefinitions(CarPage carPage)
    {
        _carPage = carPage;
    }

    [BeforeFeature("Cars")]
    public static async Task BeforeCarsScenario(IObjectContainer container)
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        var carPage = new CarPage(browser, page);

        container.RegisterInstanceAs(playwright);
        container.RegisterInstanceAs(browser);
        container.RegisterInstanceAs(carPage);
    }

    [Given(@"I am on the Cars page")]
    public async Task GivenIAmOnTheCarsPage()
    {
        await _carPage.GotoAsync();
    }

    [When(@"I create a car with matricule ""(.*)"" and color ""(.*)""")]
    public async Task WhenICreateACarWithMatriculeAndColor(string matricule, string color)
    {
        await _carPage.ClickCreateCarAsync();
        await _carPage.SetMatriculeAsync(matricule);
        await _carPage.SetColorAsync(color);
        await _carPage.SaveCarAsync();
    }

    [Then(@"I should see the car ""(.*)"" in the list")]
    public async Task ThenIShouldSeeTheCarInTheList(string matricule)
    {
        var exists = await _carPage.CarExistsInListAsync(matricule);
        exists.Should().BeTrue();
    }

    [AfterFeature("Cars")]
    public static async Task AfterCarsScenario(IObjectContainer container)
    {
        var browser = container.Resolve<IBrowser>();
        var playwright = container.Resolve<IPlaywright>();

        await browser.CloseAsync();
        playwright.Dispose();
    }
}
