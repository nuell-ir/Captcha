namespace nuel;

public interface ICaptchaRenderer
{
	/// <summary>
	/// Renders the captcha text into a base64 Data URI string ("data:image/png;base64,...").
	/// </summary>
	string Render(string text);

	/// <summary>
	/// Renders the captcha text into a PNG byte array.
	/// </summary>
	byte[] RenderBytes(string text);
}

