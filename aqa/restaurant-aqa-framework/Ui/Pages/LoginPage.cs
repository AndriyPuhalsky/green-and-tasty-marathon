using Microsoft.Playwright;

namespace RestaurantAqa.Ui.Pages;

public sealed class LoginPage(IPage page)
{
    public ILocator Form => page.Locator("form").Filter(new() { HasText = "Sign In to Your Account" });
    public ILocator Email => Form.GetByRole(AriaRole.Textbox, new() { Name = "Email" });
    public ILocator Password => Form.GetByRole(AriaRole.Textbox, new() { Name = "Password" });
    public ILocator SignInButton => Form.GetByRole(AriaRole.Button, new() { Name = "Sign In", Exact = true });

    public async Task Open() => await page.GotoAsync("/login");

    public async Task SignIn(string email, string password)
    {
        await Email.FillAsync(email);
        await Password.FillAsync(password);
        await SignInButton.ClickAsync();
        // профіль (/me) застосунок кладе в localStorage вже після входу, від нього залежать сторінки офіціанта
        await page.WaitForFunctionAsync("() => localStorage.getItem('user') !== null");
    }
}
