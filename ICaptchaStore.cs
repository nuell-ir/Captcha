namespace nuel;

public interface ICaptchaStore
{
	/// <summary>
	/// Stores the generated captcha code associated with the challenge ID.
	/// </summary>
	Task StoreAsync(long id, int captcha, TimeSpan expiration, CancellationToken cancellationToken = default);

	/// <summary>
	/// Atomically validates and consumes the captcha code.
	/// </summary>
	Task<bool> ValidateAsync(long id, int userInput, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes expired captcha records older than the specified duration.
	/// </summary>
	Task<int> CleanUpAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default);
}

