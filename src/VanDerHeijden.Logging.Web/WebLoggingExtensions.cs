using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace VanDerHeijden.Logging.Web;

/// <summary>
/// Extension methods for ILogger&lt;T&gt; to log messages with HTTP context information as structured logging scope.
/// </summary>
public static class WebLoggingExtensions
{
	/// <summary>
	/// Logs an information message with HTTP context properties added to the logging scope.
	/// </summary>
	/// <typeparam name="T">The type of the logging category.</typeparam>
	/// <param name="logger">The logger instance.</param>
	/// <param name="context">The current HTTP context. If null, logs without context scope.</param>
	/// <param name="message">The log message (may contain named format items).</param>
	/// <param name="args">Arguments for the message format.</param>
	public static void LogInformationWithContext<T>(
		this ILogger<T> logger,
		HttpContext? context,
		string message,
		params object?[] args)
	{
		LogWithContext(logger, context, LogLevel.Information, message, args);
	}

	/// <summary>
	/// Logs an error message with HTTP context properties added to the logging scope.
	/// </summary>
	/// <typeparam name="T">The type of the logging category.</typeparam>
	/// <param name="logger">The logger instance.</param>
	/// <param name="context">The current HTTP context. If null, logs without context scope.</param>
	/// <param name="message">The log message (may contain named format items).</param>
	/// <param name="args">Arguments for the message format.</param>
	public static void LogErrorWithContext<T>(
		this ILogger<T> logger,
		HttpContext? context,
		string message,
		params object?[] args)
	{
		LogWithContext(logger, context, LogLevel.Error, message, args);
	}

	/// <summary>
	/// Logs a warning message with HTTP context properties added to the logging scope.
	/// </summary>
	/// <typeparam name="T">The type of the logging category.</typeparam>
	/// <param name="logger">The logger instance.</param>
	/// <param name="context">The current HTTP context. If null, logs without context scope.</param>
	/// <param name="message">The log message (may contain named format items).</param>
	/// <param name="args">Arguments for the message format.</param>
	public static void LogWarningWithContext<T>(
		this ILogger<T> logger,
		HttpContext? context,
		string message,
		params object?[] args)
	{
		LogWithContext(logger, context, LogLevel.Warning, message, args);
	}

	/// <summary>
	/// Logs a debug message with HTTP context properties added to the logging scope.
	/// </summary>
	/// <typeparam name="T">The type of the logging category.</typeparam>
	/// <param name="logger">The logger instance.</param>
	/// <param name="context">The current HTTP context. If null, logs without context scope.</param>
	/// <param name="message">The log message (may contain named format items).</param>
	/// <param name="args">Arguments for the message format.</param>
	public static void LogDebugWithContext<T>(
		this ILogger<T> logger,
		HttpContext? context,
		string message,
		params object?[] args)
	{
		LogWithContext(logger, context, LogLevel.Debug, message, args);
	}

	/// <summary>
	/// Internal helper that applies HTTP context as logging scope and logs at the specified level.
	/// </summary>
	/// <typeparam name="T">The type of the logging category.</typeparam>
	/// <param name="logger">The logger instance.</param>
	/// <param name="context">The HTTP context to extract properties from. Can be null.</param>
	/// <param name="level">The log level to use.</param>
	/// <param name="message">The log message template.</param>
	/// <param name="args">Arguments for the message template.</param>
	private static void LogWithContext<T>(
		ILogger<T> logger,
		HttpContext? context,
		LogLevel level,
		string message,
		object?[] args)
	{
		if (context == null)
		{
			logger.Log(level, message, args);
			return;
		}

		string? ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		string? forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
		if (!string.IsNullOrEmpty(forwarded))
		{
			ip = forwarded.Split(',').First().Trim();
		}

		Dictionary<string, object?> scopeProps = new()
		{
			["RequestMethod"] = context.Request.Method,
			["RequestUrl"] = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}",
			["ClientIp"] = ip,
			["TraceId"] = context.TraceIdentifier,
			["Referer"] = context.Request.Headers[HeaderNames.Referer].FirstOrDefault() ?? string.Empty,
			["UserAgent"] = context.Request.Headers[HeaderNames.UserAgent].FirstOrDefault() ?? string.Empty,
			["SessionId"] = context.Session?.Id ?? string.Empty,
			["User"] = context.User?.Identity?.Name ?? "anonymous"
		};

		using (logger.BeginScope(scopeProps))
		{
			logger.Log(level, message, args);
		}
	}
}
