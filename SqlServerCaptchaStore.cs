using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace nuel;

public class SqlServerCaptchaStore : ICaptchaStore
{
	private readonly string _connectionString;
	private readonly TimeSpan _expiration;
	private readonly TimeSpan _cleanUpInterval;
	private readonly bool _autoCreateTable;

	private static long _lastCleanUpTicks = 0;
	private static volatile bool _isDatabaseInitialized = false;
	private static readonly SemaphoreSlim _initLock = new(1, 1);

	public SqlServerCaptchaStore(IOptions<CaptchaOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		var opt = options.Value;

		if (string.IsNullOrWhiteSpace(opt.ConnectionString))
			throw new InvalidOperationException("CaptchaOptions.ConnectionString must be configured.");

		_connectionString = opt.ConnectionString;
		_expiration = opt.Expiration;
		_cleanUpInterval = opt.CleanUpInterval;
		_autoCreateTable = opt.AutoCreateDatabaseTable;
	}

	public SqlServerCaptchaStore(
		string connectionString,
		TimeSpan? expiration = null,
		TimeSpan? cleanUpInterval = null,
		bool autoCreateTable = true)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_connectionString = connectionString;
		_expiration = expiration ?? TimeSpan.FromMinutes(5);
		_cleanUpInterval = cleanUpInterval ?? TimeSpan.FromMinutes(10);
		_autoCreateTable = autoCreateTable;
	}

	public async Task EnsureDatabaseInitializedAsync(CancellationToken cancellationToken = default)
	{
		if (_isDatabaseInitialized)
			return;

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_isDatabaseInitialized)
				return;

			const string ddl = @"
				IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CaptchaCodes' AND schema_id = SCHEMA_ID('dbo'))
				BEGIN
					CREATE TABLE dbo.CaptchaCodes (
						Id bigint NOT NULL,
						Captcha int NOT NULL,
						CreationDate datetime2 NOT NULL,
						CONSTRAINT PK_CaptchaCodes PRIMARY KEY CLUSTERED (Id)
					);
				END;

				IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CaptchaCodes_CreationDate' AND object_id = OBJECT_ID('dbo.CaptchaCodes'))
				BEGIN
					CREATE NONCLUSTERED INDEX IX_CaptchaCodes_CreationDate ON dbo.CaptchaCodes (CreationDate);
				END;";

			await using var cnnct = new SqlConnection(_connectionString);
			await using var cmnd = new SqlCommand(ddl, cnnct);
			await cnnct.OpenAsync(cancellationToken).ConfigureAwait(false);
			await cmnd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

			_isDatabaseInitialized = true;
		}
		catch
		{
			// Suppress schema initialization collisions (e.g. concurrent creation or pre-existing table)
		}
		finally
		{
			_initLock.Release();
		}
	}

	public async Task StoreAsync(long id, int captcha, TimeSpan expiration, CancellationToken cancellationToken = default)
	{
		if (_autoCreateTable && !_isDatabaseInitialized)
		{
			await EnsureDatabaseInitializedAsync(cancellationToken).ConfigureAwait(false);
		}

		TryOpportunisticCleanUp();

		await using var cnnct = new SqlConnection(_connectionString);
		await using var cmnd = new SqlCommand(
			@"INSERT INTO dbo.CaptchaCodes (Id, Captcha, CreationDate) 
			  VALUES (@id, @captcha, @date);", cnnct);

		cmnd.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
		cmnd.Parameters.Add(new SqlParameter("@captcha", SqlDbType.Int) { Value = captcha });
		cmnd.Parameters.Add(new SqlParameter("@date", SqlDbType.DateTime2) { Value = DateTime.UtcNow });

		await cnnct.OpenAsync(cancellationToken).ConfigureAwait(false);
		await cmnd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<bool> ValidateAsync(long id, int userInput, CancellationToken cancellationToken = default)
	{
		await using var cnnct = new SqlConnection(_connectionString);
		await using var cmnd = new SqlCommand(
			@"DELETE FROM dbo.CaptchaCodes 
			  WHERE Id = @id AND Captcha = @captcha AND CreationDate >= @date;", cnnct);

		cmnd.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
		cmnd.Parameters.Add(new SqlParameter("@captcha", SqlDbType.Int) { Value = userInput });
		cmnd.Parameters.Add(new SqlParameter("@date", SqlDbType.DateTime2) { Value = DateTime.UtcNow.Subtract(_expiration) });

		await cnnct.OpenAsync(cancellationToken).ConfigureAwait(false);
		int affected = await cmnd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		return affected == 1;
	}

	public async Task<int> CleanUpAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
	{
		DateTime cutoffDate = DateTime.UtcNow.Subtract(olderThan ?? _expiration);

		await using var cnnct = new SqlConnection(_connectionString);
		await using var cmnd = new SqlCommand(
			"DELETE FROM dbo.CaptchaCodes WHERE CreationDate < @date;", cnnct);
		cmnd.Parameters.Add(new SqlParameter("@date", SqlDbType.DateTime2) { Value = cutoffDate });

		await cnnct.OpenAsync(cancellationToken).ConfigureAwait(false);
		return await cmnd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private void TryOpportunisticCleanUp()
	{
		long now = Environment.TickCount64;
		long last = Volatile.Read(ref _lastCleanUpTicks);

		if (now - last < _cleanUpInterval.TotalMilliseconds)
			return;

		if (Interlocked.CompareExchange(ref _lastCleanUpTicks, now, last) == last)
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await CleanUpAsync(_expiration, CancellationToken.None).ConfigureAwait(false);
				}
				catch
				{
					// Suppress opportunistic exceptions to protect host request throughput
				}
			});
		}
	}
}

