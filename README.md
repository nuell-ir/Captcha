Configuring the captcha class:
```c#
public Startup(IConfiguration config, IWebHostEnvironment env)
{
   Configuration = configuration;
      
   Captcha.ConnectionString = config.GetConnectionString("MySQLServerConnectionString");
   Captcha.RootPath = env.WebRootPath;
   Captcha.FontPath = "Roboto.ttf"; //reltive path

   //optional properties:
   Captcha.Digits = 3; //default: 5
   Captcha.ForeColor = SixLabors.ImageSharp.Color.Blue; //default: FromRgb(60, 60, 60)
   Captcha.BackColor = SixLabors.ImageSharp.Color.Black; //default: White
   Captcha.Width = 150; //default: 200
   Captcha.Height = 50; //default: 60
}
```
A new table named CaptchaCodes will be created in the SQL Server database. 
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
