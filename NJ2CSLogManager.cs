using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Threading;
using System.Reflection;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Linq; // For XDocument and XElement
using System.Text.RegularExpressions;
using NinjaTrader.Data;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using System.Windows.Threading;
using SchwabApiCS;
using WinForms = System.Windows.Forms;
namespace NinjaTrader.NinjaScript.AddOns
{
	public static class NJ2CSLogManager
	{

		private static NJ2CSLogTab _logTab;
		private static readonly object _lock = new object();
		private static volatile bool _isInitializing = false;
		private static volatile bool _initializationFailed = false;
		private static DateTime _lastFailureTime = DateTime.MinValue;

		/// <summary>
		/// Gets the current log tab or initializes it if not already set.
		/// </summary>
		public static NJ2CSLogTab LogTab
		{
			get
			{
				// First check without locking for performance
				if (_logTab != null) return _logTab;

				lock (_lock)
				{
					// Double-check to ensure no race conditions
					if (_logTab != null) return _logTab;

					if (!_isInitializing && (!_initializationFailed || DateTime.Now - _lastFailureTime > TimeSpan.FromMinutes(1)))
					{
						try
						{
							_isInitializing = true;
							InitializeLogTab();
						}
						catch (Exception ex)
						{
							_initializationFailed = true;
							_lastFailureTime = DateTime.Now;
							LogInitializationError("Initialization failed.", ex);
						}
						finally
						{
							_isInitializing = false;
						}
					}
					return _logTab;
				}
			}
	        set
	        {
	            lock (_lock)
	            {
	                _logTab = value;
	            }
	        }
		}

		/// <summary>
		/// Initializes the log tab if it's not already initialized.
		/// </summary>
		private static void InitializeLogTab()
		{
			// Ensure we're on the UI thread
			if (!Application.Current.Dispatcher.CheckAccess())
			{
				var taskCompletionSource = new TaskCompletionSource<bool>();

				Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					try
					{
						_logTab = new NJ2CSLogTab();
						taskCompletionSource.SetResult(true);
						LogInfo("NJ2CS InitializeLogTab");
					}
					catch (Exception ex)
					{
						taskCompletionSource.SetException(ex);
					}
				}));

				// Wait with a timeout to avoid indefinite blocking
				if (!taskCompletionSource.Task.Wait(TimeSpan.FromSeconds(5)))
				{
					throw new TimeoutException("Failed to initialize NJ2CSLogTab within timeout period.");
				}
			}
			else
			{
				// If already on the UI thread, create directly
				_logTab = new NJ2CSLogTab();
				LogInfo("NJ2CS InitializeLogTab");
			}
		}
	    public static void Initialize()
	    {
			string ts = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
	        lock (_lock)
	        {
	            if (_logTab != null || _isInitializing)
	                return;
	
	            _isInitializing = true;
	        }
	
	        try
	        {
	            // Initialize the log tab on the UI thread
	            if (Application.Current.Dispatcher.CheckAccess())
	            {
	                _logTab = new NJ2CSLogTab();
	                LogMessage($"{ts} Log tab initialized.");
	            }
	            else
	            {
	                Application.Current.Dispatcher.Invoke(() => _logTab = new NJ2CSLogTab());
	                LogMessage($"{ts} Log tab initialized on UI thread.");
	            }
	        }
	        catch (Exception ex)
	        {
 
	            NinjaTrader.Code.Output.Process($"{ts} Failed to initialize log tab: {ex.Message}", PrintTo.OutputTab1);
	        }
	        finally
	        {
	            lock (_lock)
	            {
	                _isInitializing = false;
	            }
	        }
	    }
		/// <summary>
		/// Logs a message to the log tab. Initializes the log tab if necessary.
		/// </summary>
		/// <param name="message">The message to log.</param>
		public static void LogMessage(string message)
		{
			var logTab = LogTab; // Use the getter to ensure initialization

			if (logTab != null)
			{
				if (logTab.Dispatcher.CheckAccess())
				{
					logTab.AddLogMessage(message);
				}
				else
				{
					// Use BeginInvoke without waiting to avoid blocking
					logTab.Dispatcher.BeginInvoke(new Action(() => logTab.AddLogMessage(message)));
				}
			}
			else
			{
				string ts = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
				LogInfo($"{ts} LogManager.LogTab is null.message: {message}.");
			}
		}
		/// <summary>
		/// Logs informational messages to the output tab.
		/// </summary>
		/// <param name="message">The informational message.</param>
		private static void LogInfo(string message)
		{
			string ts = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
			NinjaTrader.Code.Output.Process($"{ts} {message}", PrintTo.OutputTab1);
		}
		/// <summary>
		/// Logs initialization errors with details.
		/// </summary>
		/// <param name="error">The error message.</param>
		/// <param name="ex">The exception, if available.</param>
		private static void LogInitializationError(string error, Exception ex = null)
		{
			string ts = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
			string details = ex != null ? $"\nException: {ex.Message}\nStackTrace: {ex.StackTrace}" : string.Empty;
			NinjaTrader.Code.Output.Process($"{ts} Failed to initialize NJ2CSLogTab: {error}{details}", PrintTo.OutputTab1);
		}
	}
}