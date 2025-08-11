using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NA_ManagerShortcut.Services;

namespace NA_ManagerShortcut.Views
{
    public partial class DebugWindow : Window
    {
        private readonly ClaudeCodeInterface _claudeInterface;
        private readonly DebugMonitor _debugMonitor;
        private readonly ObservableCollection<DebugEvent> _errors;
        private readonly ObservableCollection<AutoFixSuggestion> _suggestions;
        private readonly ObservableCollection<PerformanceMetric> _performanceMetrics;
        private readonly DispatcherTimer _updateTimer;
        private DateTime _captureStartTime;
        private int _eventCount;
        private int _errorCount;
        private int _warningCount;

        public DebugWindow()
        {
            InitializeComponent();
            
            _claudeInterface = new ClaudeCodeInterface();
            _debugMonitor = DebugMonitor.Instance;
            _errors = new ObservableCollection<DebugEvent>();
            _suggestions = new ObservableCollection<AutoFixSuggestion>();
            _performanceMetrics = new ObservableCollection<PerformanceMetric>();
            
            ErrorsGrid.ItemsSource = _errors;
            SuggestionsListBox.ItemsSource = _suggestions;
            PerformanceGrid.ItemsSource = _performanceMetrics;
            
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
            
            SetupEventHandlers();
            InitializeCommandHelp();
        }

        private void SetupEventHandlers()
        {
            _debugMonitor.EventCaptured += OnEventCaptured;
            _debugMonitor.AutoFixSuggested += OnAutoFixSuggested;
            _debugMonitor.OutputUpdated += OnOutputUpdated;
        }

        private void OnEventCaptured(object? sender, DebugEvent e)
        {
            Dispatcher.Invoke(() =>
            {
                _eventCount++;
                
                switch (e.Type)
                {
                    case EventType.Error:
                        _errorCount++;
                        _errors.Add(e);
                        if (_errors.Count > 100) _errors.RemoveAt(0);
                        break;
                    case EventType.Warning:
                        _warningCount++;
                        break;
                    case EventType.Performance:
                        UpdatePerformanceMetric(e);
                        break;
                }
                
                UpdateStatusBar();
                LastEventText.Text = $"Last: {e.Message}";
            });
        }

        private void OnAutoFixSuggested(object? sender, AutoFixSuggestion e)
        {
            Dispatcher.Invoke(() =>
            {
                e.SeverityColor = e.Severity switch
                {
                    FixSeverity.Critical => Brushes.Red,
                    FixSeverity.High => Brushes.Orange,
                    FixSeverity.Medium => Brushes.Yellow,
                    _ => Brushes.LightGreen
                };
                
                _suggestions.Add(e);
                if (_suggestions.Count > 20) _suggestions.RemoveAt(0);
            });
        }

        private void OnOutputUpdated(object? sender, string output)
        {
            Dispatcher.Invoke(() =>
            {
                OutputTextBox.AppendText(output + Environment.NewLine);
                
                if (AutoScrollCheckBox.IsChecked == true)
                {
                    OutputScrollViewer.ScrollToEnd();
                }
            });
        }

        private void StartCapture_Click(object sender, RoutedEventArgs e)
        {
            var autoFix = AutoFixCheckBox.IsChecked == true;
            _claudeInterface.StartCapture(autoFix);
            _captureStartTime = DateTime.Now;
            
            StartCaptureBtn.IsEnabled = false;
            StopCaptureBtn.IsEnabled = true;
            StatusText.Text = "Capturing";
            StatusText.Foreground = Brushes.LimeGreen;
            
            ResetCounters();
        }

        private void StopCapture_Click(object sender, RoutedEventArgs e)
        {
            _claudeInterface.StopCapture();
            
            StartCaptureBtn.IsEnabled = true;
            StopCaptureBtn.IsEnabled = false;
            StatusText.Text = "Idle";
            StatusText.Foreground = Brushes.Gray;
        }

        private async void Analyze_Click(object sender, RoutedEventArgs e)
        {
            var result = await _claudeInterface.AnalyzeCurrentState();
            
            var analysisWindow = new Window
            {
                Title = "Analysis Results",
                Width = 600,
                Height = 400,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            
            var textBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas"),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented)
            };
            
            analysisWindow.Content = textBox;
            analysisWindow.ShowDialog();
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var result = await _claudeInterface.ExecuteCommand("generate-report");
            MessageBox.Show(result, "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Clear();
            CommandOutputTextBox.Clear();
            _errors.Clear();
            _suggestions.Clear();
            _performanceMetrics.Clear();
            ResetCounters();
            _claudeInterface.ExecuteCommand("clear-output");
        }

        private async void ApplyFix_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DebugEvent error)
            {
                var result = await _claudeInterface.ApplyAutoFix(error.Message);
                MessageBox.Show($"Fix Result: {result.Message}", "Auto-Fix", 
                    MessageBoxButton.OK, 
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }

        private async void ApplyAllFixes_Click(object sender, RoutedEventArgs e)
        {
            foreach (var suggestion in _suggestions)
            {
                await _claudeInterface.ApplyAutoFix(suggestion.ErrorType);
            }
            MessageBox.Show("All fixes applied", "Auto-Fix", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ExecuteCommand_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCommand();
        }

        private async void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ExecuteCommand();
            }
        }

        private async System.Threading.Tasks.Task ExecuteCommand()
        {
            var command = CommandInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;
            
            CommandOutputTextBox.AppendText($"> {command}{Environment.NewLine}");
            
            try
            {
                var result = await _claudeInterface.ExecuteCommand(command);
                CommandOutputTextBox.AppendText($"{result}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                CommandOutputTextBox.AppendText($"Error: {ex.Message}{Environment.NewLine}{Environment.NewLine}");
            }
            
            CommandInputTextBox.Clear();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (StopCaptureBtn.IsEnabled)
            {
                var elapsed = DateTime.Now - _captureStartTime;
                TimeText.Text = elapsed.ToString(@"hh\:mm\:ss");
            }
            
            UpdatePerformanceDisplay();
        }

        private void UpdateStatusBar()
        {
            EventCountText.Text = _eventCount.ToString();
            ErrorCountText.Text = _errorCount.ToString();
            WarningCountText.Text = _warningCount.ToString();
        }

        private void UpdatePerformanceMetric(DebugEvent e)
        {
            if (e.Context.TryGetValue("Metric", out var metric) &&
                e.Context.TryGetValue("Value", out var value) &&
                e.Context.TryGetValue("Unit", out var unit))
            {
                var perfMetric = new PerformanceMetric
                {
                    Metric = metric.ToString()!,
                    Value = value.ToString()!,
                    Unit = unit.ToString()!,
                    Threshold = e.Context.ContainsKey("Threshold") ? e.Context["Threshold"].ToString()! : "N/A",
                    Status = DetermineStatus(Convert.ToDouble(value), 
                        e.Context.ContainsKey("Threshold") ? Convert.ToDouble(e.Context["Threshold"]) : 0)
                };
                
                var existing = _performanceMetrics.FirstOrDefault(m => m.Metric == perfMetric.Metric);
                if (existing != null)
                {
                    _performanceMetrics.Remove(existing);
                }
                _performanceMetrics.Add(perfMetric);
            }
        }

        private string DetermineStatus(double value, double threshold)
        {
            if (threshold == 0) return "OK";
            return value > threshold ? "Warning" : "OK";
        }

        private void UpdatePerformanceDisplay()
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64 / (1024 * 1024);
                MemoryText.Text = $"{workingSet} MB";
                
                if (_eventCount > 0 && StopCaptureBtn.IsEnabled)
                {
                    var elapsed = (DateTime.Now - _captureStartTime).TotalSeconds;
                    if (elapsed > 0)
                    {
                        EventRateText.Text = (_eventCount / elapsed).ToString("F1");
                    }
                }
            }
            catch { }
        }

        private void ResetCounters()
        {
            _eventCount = 0;
            _errorCount = 0;
            _warningCount = 0;
            UpdateStatusBar();
        }

        private void InitializeCommandHelp()
        {
            CommandOutputTextBox.Text = @"Claude Code Debug Interface - Available Commands:
=============================================
start-debug [--autofix]   : Start debug monitoring
stop-debug               : Stop debug monitoring
analyze                  : Analyze current state
get-errors [count]       : Get recent errors
apply-fix <error-type>   : Apply auto-fix for error type
generate-report          : Generate debug report
clear-output             : Clear debug output
get-output               : Get debug output
watch-file <path>        : Watch file for changes
test-error               : Generate test error

Type a command and press Enter to execute.
=============================================

";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _updateTimer.Stop();
            _debugMonitor.EventCaptured -= OnEventCaptured;
            _debugMonitor.AutoFixSuggested -= OnAutoFixSuggested;
            _debugMonitor.OutputUpdated -= OnOutputUpdated;
        }
    }

    public class PerformanceMetric
    {
        public string Metric { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Threshold { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

}