using Microsoft.Extensions.Logging;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public static class ILoggerNugetLoggerExtensions {
		public static NuGet.Common.ILogger GetNuGetLogger<T>(this ILogger<T> logger) {
			return new LoggerAdapter<T>(logger);
		}
	}
	public class LoggerAdapter<T> : NuGet.Common.ILogger {
		private ILogger<T> logger;

		public LoggerAdapter(ILogger<T> logger) {
			this.logger = logger;
		}

		public void Log(NuGet.Common.LogLevel level, string data) {
			logger.Log(TranslateLevel(level), data);
		}

		private Microsoft.Extensions.Logging.LogLevel TranslateLevel(NuGet.Common.LogLevel level) => level switch {
			NuGet.Common.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Trace,
			NuGet.Common.LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Debug,
			NuGet.Common.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
			NuGet.Common.LogLevel.Minimal => Microsoft.Extensions.Logging.LogLevel.Information,
			NuGet.Common.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
			NuGet.Common.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
			_ => Microsoft.Extensions.Logging.LogLevel.Critical
		};

		public void Log(ILogMessage message) {
			Log(message.Level, message.FormatWithCode());
		}

		public Task LogAsync(NuGet.Common.LogLevel level, string data) {
			Log(level, data);
			return Task.CompletedTask;
		}

		public Task LogAsync(ILogMessage message) {
			Log(message);
			return Task.CompletedTask;
		}

		public void LogDebug(string data) {
			Log(NuGet.Common.LogLevel.Debug, data);
		}

		public void LogError(string data) {
			Log(NuGet.Common.LogLevel.Error, data);
		}

		public void LogInformation(string data) {
			Log(NuGet.Common.LogLevel.Information, data);
		}

		public void LogInformationSummary(string data) {
			Log(NuGet.Common.LogLevel.Information, data);
		}

		public void LogMinimal(string data) {
			Log(NuGet.Common.LogLevel.Minimal, data);
		}

		public void LogVerbose(string data) {
			Log(NuGet.Common.LogLevel.Verbose, data);
		}

		public void LogWarning(string data) {
			Log(NuGet.Common.LogLevel.Warning, data);
		}
	}
}
