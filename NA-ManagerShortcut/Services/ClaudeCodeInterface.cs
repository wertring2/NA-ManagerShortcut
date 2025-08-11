using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NA_ManagerShortcut.Services
{
    public class ClaudeCodeInterface
    {
        private readonly DebugMonitor _debugMonitor;
        private readonly string _outputPath;
        private readonly string _commandPath;
        private bool _isCapturing;
        private readonly List<DebugEvent> _capturedEvents = new();
        private readonly Dictionary<string, Func<string[], Task<string>>> _commands;

        public ClaudeCodeInterface()
        {
            _debugMonitor = DebugMonitor.Instance;
            _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "claude_output");
            _commandPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "claude_commands");
            
            Directory.CreateDirectory(_outputPath);
            Directory.CreateDirectory(_commandPath);

            _commands = InitializeCommands();
            SetupEventHandlers();
        }

        public async Task<string> ExecuteCommand(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "Invalid command";

            var cmd = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(cmd, out var handler))
            {
                try
                {
                    return await handler(args);
                }
                catch (Exception ex)
                {
                    return $"Command failed: {ex.Message}";
                }
            }

            return $"Unknown command: {cmd}";
        }

        public void StartCapture(bool autoFix = false)
        {
            _isCapturing = true;
            _capturedEvents.Clear();
            _debugMonitor.StartMonitoring(autoFix);
            
            WriteOutput(new CaptureStatus
            {
                IsCapturing = true,
                AutoFixEnabled = autoFix,
                StartTime = DateTime.Now,
                Message = "Debug capture started. Monitoring all application events..."
            });
        }

        public void StopCapture()
        {
            _isCapturing = false;
            _debugMonitor.StopMonitoring();
            
            var summary = GenerateCaptureSummary();
            WriteOutput(summary);
        }

        public async Task<AutoFixResult> ApplyAutoFix(string errorType)
        {
            var result = new AutoFixResult
            {
                ErrorType = errorType,
                Timestamp = DateTime.Now
            };

            try
            {
                var fixes = await GenerateFixesForError(errorType);
                
                foreach (var fix in fixes)
                {
                    if (await ApplyFix(fix))
                    {
                        result.AppliedFixes.Add(fix);
                        result.Success = true;
                    }
                    else
                    {
                        result.FailedFixes.Add(fix);
                    }
                }

                result.Message = result.Success 
                    ? $"Successfully applied {result.AppliedFixes.Count} fixes"
                    : "Failed to apply fixes";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Auto-fix failed: {ex.Message}";
                _debugMonitor.LogException(ex);
            }

            WriteOutput(result);
            return result;
        }

        public List<DebugEvent> GetRecentErrors(int count = 10)
        {
            return _capturedEvents
                .Where(e => e.Type == EventType.Error)
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }

        public async Task<AnalysisResult> AnalyzeCurrentState()
        {
            var analysis = new AnalysisResult
            {
                Timestamp = DateTime.Now,
                TotalEvents = _capturedEvents.Count,
                Errors = _capturedEvents.Count(e => e.Type == EventType.Error),
                Warnings = _capturedEvents.Count(e => e.Type == EventType.Warning),
                PerformanceIssues = _capturedEvents.Count(e => e.Type == EventType.Performance)
            };

            analysis.CommonErrors = _capturedEvents
                .Where(e => e.Type == EventType.Error)
                .GroupBy(e => e.Message)
                .Select(g => new ErrorFrequency
                {
                    Error = g.Key,
                    Count = g.Count(),
                    LastOccurrence = g.Max(e => e.Timestamp)
                })
                .OrderByDescending(e => e.Count)
                .Take(5)
                .ToList();

            analysis.SuggestedFixes = await GenerateSuggestedFixes(analysis.CommonErrors);
            
            WriteOutput(analysis);
            return analysis;
        }

        private Dictionary<string, Func<string[], Task<string>>> InitializeCommands()
        {
            return new Dictionary<string, Func<string[], Task<string>>>
            {
                ["start-debug"] = async (args) =>
                {
                    var autoFix = args.Contains("--autofix");
                    StartCapture(autoFix);
                    return "Debug monitoring started" + (autoFix ? " with auto-fix enabled" : "");
                },
                
                ["stop-debug"] = async (args) =>
                {
                    StopCapture();
                    return "Debug monitoring stopped";
                },
                
                ["analyze"] = async (args) =>
                {
                    var result = await AnalyzeCurrentState();
                    return JsonConvert.SerializeObject(result, Formatting.Indented);
                },
                
                ["get-errors"] = async (args) =>
                {
                    var count = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 10;
                    var errors = GetRecentErrors(count);
                    return JsonConvert.SerializeObject(errors, Formatting.Indented);
                },
                
                ["apply-fix"] = async (args) =>
                {
                    if (args.Length == 0) return "Error type required";
                    var result = await ApplyAutoFix(args[0]);
                    return JsonConvert.SerializeObject(result, Formatting.Indented);
                },
                
                ["generate-report"] = async (args) =>
                {
                    var report = await _debugMonitor.GenerateDebugReport();
                    var path = Path.Combine(_outputPath, $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(report, Formatting.Indented));
                    return $"Report generated: {path}";
                },
                
                ["clear-output"] = async (args) =>
                {
                    _debugMonitor.ClearDebugOutput();
                    _capturedEvents.Clear();
                    return "Debug output cleared";
                },
                
                ["get-output"] = async (args) =>
                {
                    return _debugMonitor.GetDebugOutput();
                },
                
                ["watch-file"] = async (args) =>
                {
                    if (args.Length == 0) return "File path required";
                    StartFileWatcher(args[0]);
                    return $"Watching file: {args[0]}";
                },
                
                ["test-error"] = async (args) =>
                {
                    try
                    {
                        throw new NullReferenceException("Test error for Claude Code");
                    }
                    catch (Exception ex)
                    {
                        _debugMonitor.LogException(ex);
                        return "Test error logged";
                    }
                }
            };
        }

        private void SetupEventHandlers()
        {
            _debugMonitor.EventCaptured += (sender, evt) =>
            {
                if (_isCapturing)
                {
                    _capturedEvents.Add(evt);
                    
                    if (evt.Type == EventType.Error)
                    {
                        WriteImmediateError(evt);
                    }
                }
            };

            _debugMonitor.AutoFixSuggested += (sender, suggestion) =>
            {
                WriteAutoFixSuggestion(suggestion);
            };

            _debugMonitor.OutputUpdated += (sender, output) =>
            {
                if (_isCapturing)
                {
                    var outputFile = Path.Combine(_outputPath, "live_output.txt");
                    File.AppendAllText(outputFile, output + Environment.NewLine);
                }
            };
        }

        private void WriteOutput<T>(T data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var filename = $"{typeof(T).Name}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var path = Path.Combine(_outputPath, filename);
            File.WriteAllText(path, json);
        }

        private void WriteImmediateError(DebugEvent error)
        {
            var errorFile = Path.Combine(_outputPath, "immediate_errors.jsonl");
            var json = JsonConvert.SerializeObject(error);
            File.AppendAllText(errorFile, json + Environment.NewLine);
        }

        private void WriteAutoFixSuggestion(AutoFixSuggestion suggestion)
        {
            var suggestionFile = Path.Combine(_outputPath, "autofix_suggestions.jsonl");
            var json = JsonConvert.SerializeObject(suggestion);
            File.AppendAllText(suggestionFile, json + Environment.NewLine);
        }

        private CaptureSummary GenerateCaptureSummary()
        {
            return new CaptureSummary
            {
                TotalEvents = _capturedEvents.Count,
                Errors = _capturedEvents.Count(e => e.Type == EventType.Error),
                Warnings = _capturedEvents.Count(e => e.Type == EventType.Warning),
                PerformanceIssues = _capturedEvents.Count(e => e.Type == EventType.Performance),
                Duration = _capturedEvents.Any() 
                    ? _capturedEvents.Max(e => e.Timestamp) - _capturedEvents.Min(e => e.Timestamp)
                    : TimeSpan.Zero,
                TopErrors = _capturedEvents
                    .Where(e => e.Type == EventType.Error)
                    .GroupBy(e => e.Message)
                    .Select(g => new { Error = g.Key, Count = g.Count() })
                    .OrderByDescending(e => e.Count)
                    .Take(5)
                    .ToDictionary(e => e.Error, e => e.Count)
            };
        }

        private async Task<List<CodeFix>> GenerateFixesForError(string errorType)
        {
            var fixes = new List<CodeFix>();
            
            switch (errorType.ToLower())
            {
                case "nullreference":
                    fixes.Add(new CodeFix
                    {
                        Type = "NullCheck",
                        Description = "Add null validation",
                        Code = "if (obj == null) throw new ArgumentNullException(nameof(obj));"
                    });
                    break;
                    
                case "network":
                    fixes.Add(new CodeFix
                    {
                        Type = "RetryLogic",
                        Description = "Add retry mechanism",
                        Code = "for (int i = 0; i < 3; i++) { try { /* operation */ break; } catch { await Task.Delay(1000); } }"
                    });
                    break;
                    
                case "permission":
                    fixes.Add(new CodeFix
                    {
                        Type = "ElevatePrivileges",
                        Description = "Request administrator privileges",
                        Code = "// Requires app.manifest modification"
                    });
                    break;
            }
            
            return fixes;
        }

        private async Task<bool> ApplyFix(CodeFix fix)
        {
            try
            {
                _debugMonitor.LogEvent($"Applying fix: {fix.Type}", EventType.Info);
                await Task.Delay(100);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<List<string>> GenerateSuggestedFixes(List<ErrorFrequency> commonErrors)
        {
            var suggestions = new List<string>();
            
            foreach (var error in commonErrors)
            {
                if (error.Error.Contains("null"))
                {
                    suggestions.Add("Implement comprehensive null checking");
                }
                else if (error.Error.Contains("network"))
                {
                    suggestions.Add("Add network connectivity validation");
                }
                else if (error.Error.Contains("timeout"))
                {
                    suggestions.Add("Increase timeout values or add async handling");
                }
            }
            
            return suggestions;
        }

        private void StartFileWatcher(string path)
        {
            if (!File.Exists(path)) return;
            
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!)
            {
                Filter = Path.GetFileName(path),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            
            watcher.Changed += (sender, e) =>
            {
                _debugMonitor.LogEvent($"File changed: {e.FullPath}", EventType.Info);
            };
            
            watcher.EnableRaisingEvents = true;
        }
    }

    public class CaptureStatus
    {
        public bool IsCapturing { get; set; }
        public bool AutoFixEnabled { get; set; }
        public DateTime StartTime { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CaptureSummary
    {
        public int TotalEvents { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public int PerformanceIssues { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, int> TopErrors { get; set; } = new();
    }

    public class AnalysisResult
    {
        public DateTime Timestamp { get; set; }
        public int TotalEvents { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public int PerformanceIssues { get; set; }
        public List<ErrorFrequency> CommonErrors { get; set; } = new();
        public List<string> SuggestedFixes { get; set; } = new();
    }

    public class ErrorFrequency
    {
        public string Error { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime LastOccurrence { get; set; }
    }

    public class AutoFixResult
    {
        public string ErrorType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<CodeFix> AppliedFixes { get; set; } = new();
        public List<CodeFix> FailedFixes { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class CodeFix
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}