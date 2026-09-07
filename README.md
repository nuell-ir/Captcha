Configuring the captcha class:
```c#
public Startup(IConfiguration config, IWebHostEnvironment env)
{
   Configuration = config;
      
   Captcha.ConnectionString = config.GetConnectionString("MySQLServerConnectionString");
   Captcha.RootPath = env.WebRootPath;
   Captcha.FontPath = "Roboto.ttf"; //relative path

   //optional properties:
   Captcha.Digits = 3; //default: 5
   Captcha.ForeColor = SixLabors.ImageSharp.Color.Blue; //default: FromRgb(60, 60, 60)
   Captcha.BackColor = SixLabors.ImageSharp.Color.Black; //default: White
   Captcha.Width = 150; //default: 200
   Captcha.Height = 50; //default: 60
   Captcha.Expiration = TimeSpan.FromMinutes(5); //default: 5 minutes
   Captcha.CleanUpInterval = TimeSpan.FromMinutes(10); //default: 10 minutes
}
```
### Database Setup
The table `dbo.CaptchaCodes` and the expiration index `IX_CaptchaCodes_CreationDate` are automatically created on first use. Alternatively, the migration script in `Migrations/001_CreateCaptchaCodesTable.sql` can be executed ahead of time.

### Expiration & Housekeeping
- **Validation**: Enforced directly and atomically using UTC (`DateTime.UtcNow`). Captchas older than `Captcha.Expiration` cannot be validated.
- **Housekeeping**: Expired records are cleaned up automatically in the background using an index on `CreationDate`, throttled by `Captcha.CleanUpInterval` to avoid overhead on shared hosting. You can also trigger cleanup manually by calling `Captcha.CleanUp()`.

Creating the captcha and sending the image and the code to the razor view:
```c#
public IActionResult Index()
{
   var captcha = new Captcha();
   ViewBag.Src = captcha.Src;
   ViewBag.Code = captcha.Code;
   return View();
}
```
Displaying the captcha in the razor view form:
```html
<input type="hidden" name="CaptchaCode" value="@ViewBag.Code">
<img src="@ViewBag.Src">
<input type="text" name="CaptchaUserInput">
```
Checking the validity of the user input after the form was posted back to the server:
```c#
if (Captcha.IsValid(request.CaptchaUserInput, request.CaptchaCode)) { . . . }
```
