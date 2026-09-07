using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace nuel;

public class CaptchaService : ICaptchaService
{
    private readonly ICaptchaRenderer _renderer;
    private readonly ICaptchaStore _store;
    private readonly CaptchaOptions _options;

    public CaptchaService(
        ICaptchaRenderer renderer,
        ICaptchaStore store,
        IOptions<CaptchaOptions> options)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<CaptchaResult> GenerateAsync(CancellationToken cancellationToken = default)
    {
        int maxExclusive = (int)Math.Pow(10, _options.Digits);
        int randNumber = RandomNumberGenerator.GetInt32(maxExclusive);
        string text = randNumber.ToString("D" + _options.Digits);

        string src = _renderer.Render(text);
        long id = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 4);

        await _store.StoreAsync(id, randNumber, _options.Expiration, cancellationToken).ConfigureAwait(false);

        return new CaptchaResult(id, src);
    }

    public Task<bool> ValidateAsync(long id, int userInput, CancellationToken cancellationToken = default)
    {
        return _store.ValidateAsync(id, userInput, cancellationToken);
    }

    public Task<bool> ValidateAsync(long id, string? userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput) || !int.TryParse(userInput.Trim(), out int input))
            return Task.FromResult(false);

        return ValidateAsync(id, input, cancellationToken);
    }

    public Task<bool> ValidateAsync(string? id, string? userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !long.TryParse(id.Trim(), out long codeId))
            return Task.FromResult(false);

        return ValidateAsync(codeId, userInput, cancellationToken);
    }

    public Task<int> CleanUpAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        return _store.CleanUpAsync(olderThan, cancellationToken);
    }
}

