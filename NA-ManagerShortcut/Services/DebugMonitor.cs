using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace NA_ManagerShortcut.Services
{
    public class DebugMonitor : IDisposable
    {
        private static DebugMonitor? _instance;
        private readonly ConcurrentQueue<DebugEvent> _eventQueue = new();
        private readonly ConcurrentDictionary<string, ErrorPattern> _errorPatterns = new();
        private readonly ConcurrentDictionary<string, AutoFixSuggestion> _autoFixes = new();
        private readonly StringBuilder _debugOutput = new();
        private readonly object _outputLock = new();
        private readonly Timer _flushTimer;
        private readonly string _logPath;
        private bool _isMonitoring;
        private bool _autoFixEnabled;
        private readonly DispatcherTimer _performanceTimer;
        private Process? _currentProcess;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _memoryCounter;

        public static DebugMonitor Instance => _instance ??= new DebugMonitor();

        public event EventHandler<DebugEvent>? EventCaptured;
        public event EventHandler<AutoFixSuggestion>? AutoFixSuggested;
        public event EventHandler<string>? OutputUpdated;

        private DebugMonitor()
        {
            _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_logs");
            Directory.CreateDirectory(_logPath);
            
            _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            _performanceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _performanceTimer.Tick += MonitorPerformance;

            InitializeErrorPatterns();
            InitializeAutoFixes();
            SetupGlobalErrorHandlers();
        }

        public void StartMonitoring(bool enableAutoFix = false)
        {
            _isMonitoring = true;
            _autoFixEnabled = enableAutoFix;
            _performanceTimer.Start();
            
            LogEvent("Debug Monitoring Started", EventType.Info, new Dictionary<string, object>
            {
                ["AutoFix"] = enableAutoFix,
                ["Timestamp"] = DateTime.Now,
                ["Process"] = Process.GetCurrentProcess().ProcessName
            });
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _performanceTimer.Stop();
            FlushLogs(null);
            
            LogEvent("Debug Monitoring Stopped", EventType.Info);
        }

        public void LogEvent(string message, EventType type = EventType.Info, 
            Dictionary<string, object>? context = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!_isMonitoring) return;

            var debugEvent = new DebugEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                Type = type,
                Message = message,
                Context = context ?? new Dictionary<string, object>(),
                StackTrace = type == EventType.Error ? Environment.StackTrace : null,
                Source = $"{Path.GetFileName(sourceFilePath)}:{memberName}:{sourceLineNumber}"
            };

            _eventQueue.Enqueue(debugEvent);
            EventCaptured?.Invoke(this, debugEvent);

            if (type == EventType.Error && _autoFixEnabled)
            {
                Task.Run(() => SuggestAutoFix(debugEvent));
            }

            UpdateOutput(FormatEvent(debugEvent));
        }

        public void LogException(Exception ex, Dictionary<string, object>? context = null)
        {
            var errorContext = context ?? new Dictionary<string, object>();
            errorContext["ExceptionType"] = ex.GetType().Name;
            errorContext["InnerException"] = ex.InnerException?.Message;
            errorContext["TargetSite"] = ex.TargetSite?.Name;
            
            LogEvent(ex.Message, EventType.Error, errorContext);

            if (_autoFixEnabled)
            {
                var suggestion = AnalyzeException(ex);
                if (suggestion != null)
                {
                    AutoFixSuggested?.Invoke(this, suggestion);
                }
            }
        }

        public void LogNetworkOperation(string operation, string adapter, bool success, TimeSpan duration)
        {
            LogEvent($"Network Operation: {operation}", 
                success ? EventType.Info : EventType.Warning, 
                new Dictionary<string, object>
                {
                    ["Operation"] = operation,
                    ["Adapter"] = adapter,
                    ["Success"] = success,
                    ["Duration"] = duration.TotalMilliseconds,
                    ["ThreadId"] = Thread.CurrentThread.ManagedThreadId
                });
        }

        public void LogPerformanceMetric(string metric, double value, string unit = "ms")
        {
            LogEvent($"Performance: {metric}", EventType.Performance, new Dictionary<string, object>
            {
                ["Metric"] = metric,
                ["Value"] = value,
                ["Unit"] = unit,
                ["Threshold"] = GetPerformanceThreshold(metric)
            });
        }

        public async Task<DebugReport> GenerateDebugReport()
        {
            var events = _eventQueue.ToList();
            var report = new DebugReport
            {
                GeneratedAt = DateTime.Now,
                TotalEvents = events.Count,
                ErrorCount = events.Count(e => e.Type == EventType.Error),
                WarningCount = events.Count(e => e.Type == EventType.Warning),
                PerformanceIssues = events.Where(e => e.Type == EventType.Performance).ToList(),
                RecentErrors = events.Where(e => e.Type == EventType.Error)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(10)
                    .ToList(),
                AutoFixSuggestions = _autoFixes.Values.ToList(),
                SystemInfo = GatherSystemInfo()
            };

            var reportPath = Path.Combine(_logPath, $"debug_report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            await File.WriteAllTextAsync(reportPath, JsonConvert.SerializeObject(report, Formatting.Indented));

            return report;
        }

        public string GetDebugOutput()
        {
            lock (_outputLock)
            {
                return _debugOutput.ToString();
            }
        }

        public void ClearDebugOutput()
        {
            lock (_outputLock)
            {
                _debugOutput.Clear();
            }
            OutputUpdated?.Invoke(this, string.Empty);
        }

        private void InitializeErrorPatterns()
        {
            _errorPatterns["NullReference"] = new ErrorPattern
            {
                Pattern = "NullReferenceException",
                Description = "Attempting to use a null object reference",
                AutoFixable = true
            };

            _errorPatterns["NetworkUnreachable"] = new ErrorPattern
            {
                Pattern = "network is unreachable",
                Description = "Network adapter or connection issue",
                AutoFixable = true
            };

            _errorPatterns["AccessDenied"] = new ErrorPattern
            {
                Pattern = "Access is denied",
                Description = "Insufficient permissions for operation",
                AutoFixable = true
            };

            _errorPatterns["WMITimeout"] = new ErrorPattern
            {
                Pattern = "WMI operation timed out",
                Description = "Windows Management Instrumentation timeout",
                AutoFixable = true
            };
        }

        private void InitializeAutoFixes()
        {
            _autoFixes["NullReference"] = new AutoFixSuggestion
            {
                ErrorType = "NullReferenceException",
                Suggestion = "Add null check before accessing object",
                CodeFix = "if (object != null) { /* access object */ }",
                Severity = FixSeverity.High
            };

            _autoFixes["NetworkUnreachable"] = new AutoFixSuggestion
            {
                ErrorType = "NetworkUnreachable",
                Suggestion = "Check network adapter status and retry",
                CodeFix = "await NetworkAdapterService.RestartAdapter(adapterName);",
                Severity = FixSeverity.Medium
            };

            _autoFixes["AccessDenied"] = new AutoFixSuggestion
            {
                ErrorType = "AccessDenied",
                Suggestion = "Ensure application is running with Administrator privileges",
                CodeFix = "// Add to app.manifest: <requestedExecutionLevel level=\"requireAdministrator\" />",
                Severity = FixSeverity.High
            };
        }

        private void SetupGlobalErrorHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogException(ex ?? new Exception("Unknown error"), new Dictionary<string, object>
                {
                    ["IsTerminating"] = args.IsTerminating
                });
            };

            if (Application.Current != null)
            {
                Application.Current.DispatcherUnhandledException += (sender, args) =>
                {
                    LogException(args.Exception);
                    args.Handled = _autoFixEnabled;
                };
            }

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                foreach (var ex in args.Exception.InnerExceptions)
                {
                    LogException(ex);
                }
                args.SetObserved();
            };
        }

        private void MonitorPerformance(object? sender, EventArgs e)
        {
            try
            {
                _currentProcess ??= Process.GetCurrentProcess();
                
                var workingSet = _currentProcess.WorkingSet64 / (1024 * 1024);
                var gcMemory = GC.GetTotalMemory(false) / (1024 * 1024);
                
                if (workingSet > 200)
                {
                    LogEvent("High memory usage detected", EventType.Performance, new Dictionary<string, object>
                    {
                        ["WorkingSet"] = workingSet,
                        ["GCMemory"] = gcMemory
                    });
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private void UpdateOutput(string message)
        {
            lock (_outputLock)
            {
                _debugOutput.AppendLine(message);
                if (_debugOutput.Length > 100000)
                {
                    _debugOutput.Remove(0, 50000);
                }
            }
            OutputUpdated?.Invoke(this, message);
        }

        private string FormatEvent(DebugEvent evt)
        {
            var contextStr = evt.Context.Any() 
                ? $" | Context: {JsonConvert.SerializeObject(evt.Context)}" 
                : "";
                
            return $"[{evt.Timestamp:HH:mm:ss.fff}] [{evt.Type}] {evt.Source} | {evt.Message}{contextStr}";
        }

        private void FlushLogs(object? state)
        {
            if (!_eventQueue.Any()) return;

            var events = new List<DebugEvent>();
            while (_eventQueue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }

            if (events.Any())
            {
                var logFile = Path.Combine(_logPath, $"debug_{DateTime.Now:yyyyMMdd}.log");
                var logContent = string.Join(Environment.NewLine, events.Select(FormatEvent));
                
                try
                {
                    File.AppendAllText(logFile, logContent + Environment.NewLine);
                }
                catch { }
            }
        }

        private AutoFixSuggestion? AnalyzeException(Exception ex)
        {
            foreach (var pattern in _errorPatterns.Values)
            {
                if (ex.Message.Contains(pattern.Pattern) || ex.GetType().Name.Contains(pattern.Pattern))
                {
                    if (_autoFixes.TryGetValue(ex.GetType().Name, out var fix))
                    {
                        return fix;
                    }
                }
            }
            return null;
        }

        private async Task SuggestAutoFix(DebugEvent debugEvent)
        {
            await Task.Delay(100);
            
            foreach (var pattern in _errorPatterns.Values.Where(p => p.AutoFixable))
            {
                if (debugEvent.Message.Contains(pattern.Pattern))
                {
                    var suggestion = new AutoFixSuggestion
                    {
                        ErrorType = pattern.Pattern,
                        Suggestion = pattern.Description,
                        CodeFix = $"// Auto-fix for: {pattern.Pattern}",
                        Severity = FixSeverity.Medium
                    };
                    
                    AutoFixSuggested?.Invoke(this, suggestion);
                    break;
                }
            }
        }

        private double GetPerformanceThreshold(string metric)
        {
            return metric switch
            {
                "NetworkOperation" => 1000,
                "UIRender" => 16,
                "DatabaseQuery" => 100,
                _ => 500
            };
        }

        private Dictionary<string, object> GatherSystemInfo()
        {
            return new Dictionary<string, object>
            {
                ["OS"] = Environment.OSVersion.ToString(),
                ["CLR"] = Environment.Version.ToString(),
                ["ProcessorCount"] = Environment.ProcessorCount,
                ["Is64Bit"] = Environment.Is64BitProcess,
                ["WorkingSet"] = Process.GetCurrentProcess().WorkingSet64,
                ["TotalMemory"] = GC.GetTotalMemory(false)
            };
        }

        public void Dispose()
        {
            StopMonitoring();
            _flushTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _currentProcess?.Dispose();
        }
    }

    public class DebugEvent
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public EventType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
        public string? StackTrace { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public enum EventType
    {
        Info,
        Warning,
        Error,
        Performance,
        Network,
        UI
    }

    public class ErrorPattern
    {
        public string Pattern { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool AutoFixable { get; set; }
    }

    public class AutoFixSuggestion
    {
        public string ErrorType { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public string CodeFix { get; set; } = string.Empty;
        public FixSeverity Severity { get; set; }
        public System.Windows.Media.Brush? SeverityColor { get; set; }
    }

    public enum FixSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class DebugReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalEvents { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<DebugEvent> PerformanceIssues { get; set; } = new();
        public List<DebugEvent> RecentErrors { get; set; } = new();
        public List<AutoFixSuggestion> AutoFixSuggestions { get; set; } = new();
        public Dictionary<string, object> SystemInfo { get; set; } = new();
    }
}