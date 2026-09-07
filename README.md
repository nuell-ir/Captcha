# nuel_Captcha

A fast, lightweight, and thread-safe .NET captcha library with cached typography, SixLabors ImageSharp rendering, and asynchronous storage.

## Features
- **Non-blocking & Asynchronous**: All database and storage operations support async/await and `CancellationToken`.
- **Cached Typography**: Font families and glyph collections are loaded and cached once at startup, eliminating per-request disk reads.
- **Dependency Injection**: Seamless integration via standard ASP.NET Core DI extensions.
- **Storage Decoupling**: Pluggable storage via `ICaptchaStore` (with built-in `SqlServerCaptchaStore`).
- **Low-Resource Friendly**: Housekeeping uses opportunistic, throttled cleanup without requiring background worker processes that may get killed on shared hosting or low-resource hosts.

---

## Getting Started

### 1. Register Services (`Program.cs`)
```csharp
builder.Services.AddCaptcha(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.FontPath = Path.Combine(builder.Environment.WebRootPath, "fonts/Roboto.ttf");
    
    // Optional settings:
    options.Digits = 5;                              // default: 5 (supported: 1-9)
    options.Width = 200;                             // default: 200
    options.Height = 60;                             // default: 60
    options.FontSize = 30f;                          // default: 30
    options.ForeColor = SixLabors.ImageSharp.Color.FromRgb(60, 60, 60);
    options.BackColor = SixLabors.ImageSharp.Color.White;
    options.Expiration = TimeSpan.FromMinutes(5);    // default: 5 minutes
    options.CleanUpInterval = TimeSpan.FromMinutes(10); // default: 10 minutes
});
```

---

### 2. Generate Captcha in Controller or Minimal API

#### Controller Example:
```csharp
public class HomeController : Controller
{
    private readonly ICaptchaService _captchaService;

    public HomeController(ICaptchaService captchaService)
    {
        _captchaService = captchaService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var captcha = await _captchaService.GenerateAsync(ct);
        ViewBag.Src = captcha.Src;
        ViewBag.Code = captcha.Id;
        return View();
    }
}
```

#### Razor View (`Index.cshtml`):
```html
<form method="post" action="/submit">
    <input type="hidden" name="CaptchaCode" value="@ViewBag.Code" />
    <img src="@ViewBag.Src" alt="Captcha" />
    <input type="text" name="CaptchaUserInput" autocomplete="off" required />
    <button type="submit">Verify</button>
</form>
```

#### Validating Captcha:
```csharp
[HttpPost("/submit")]
public async Task<IActionResult> Submit(
    [FromForm] string captchaCode, 
    [FromForm] string captchaUserInput, 
    CancellationToken ct)
{
    bool isValid = await _captchaService.ValidateAsync(captchaCode, captchaUserInput, ct);
    if (!isValid)
    {
        ModelState.AddModelError("CaptchaUserInput", "Invalid or expired captcha.");
        return View();
    }

    // Process submission...
    return RedirectToAction("Success");
}
```

---

## Database Setup & Housekeeping

- **Automatic Setup**: `dbo.CaptchaCodes` and index `IX_CaptchaCodes_CreationDate` are automatically created on first use if not already present.
- **Atomic Validation**: Captchas are validated and consumed atomically using UTC (`DateTime.UtcNow`). Captchas older than `Expiration` cannot be validated.
- **Opportunistic Housekeeping**: Expired records are cleaned up asynchronously in the background when the store is accessed, throttled by `CleanUpInterval` to prevent load on shared hosting. You can also trigger cleanup manually:
  ```csharp
  await captchaService.CleanUpAsync(ct);
  ```
