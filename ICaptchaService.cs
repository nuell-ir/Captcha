namespace nuel;

public interface ICaptchaService
{
    /// <summary>
    /// Generates a new captcha challenge, renders the image, stores the solution, and returns the challenge result.
    /// </summary>
    Task<CaptchaResult> GenerateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically validates the user's captcha answer against the challenge ID.
    /// </summary>
    Task<bool> ValidateAsync(long id, int userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically validates the user's captcha answer against the challenge ID.
    /// </summary>
    Task<bool> ValidateAsync(long id, string? userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically validates the user's captcha answer against the challenge ID string.
    /// </summary>
    Task<bool> ValidateAsync(string? id, string? userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually deletes expired captcha records.
    /// </summary>
    Task<int> CleanUpAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default);
}

