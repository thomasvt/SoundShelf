using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SoundShelf.WaveForm
{
    public class SoundViewer : Control
    {
        static SoundViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SoundViewer), new FrameworkPropertyMetadata(typeof(SoundViewer)));
        }

        private readonly WaveFormGenerator _waveFormGenerator = new();
        private Canvas? _canvas;
        private Line? _waveformCursorLine;
        private Point? _selectionStartPos;
        private Rectangle? _selectionRect;
        private WaveForm? _waveForm;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _canvas = GetTemplateChild("PART_WaveformCanvas") as Canvas;

            if (_canvas != null)
            {
                _canvas.SizeChanged += CanvasOnSizeChanged;
                _canvas.MouseDown += CanvasOnMouseDown;
                _canvas.MouseMove += CanvasOnMouseMove;
                _canvas.MouseUp += CanvasOnMouseUp;
            }
        }

        private void CanvasOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SoundFilePath != null)
                DrawWaveform(SoundFilePath);

            if (_waveformCursorLine != null)
                _waveformCursorLine.Y2 = _canvas.ActualHeight;
        }

        private void CanvasOnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SoundFilePath == null) return;

            _canvas!.CaptureMouse();

            _selectionStartPos = e.GetPosition(_canvas);

            // Create a rectangle if not already added
            _selectionRect ??= new Rectangle
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Colors.DeepSkyBlue with { A = 50 })
            };

            if (!_canvas.Children.Contains(_selectionRect))
                _canvas.Children.Add(_selectionRect);

            // Reset position and size
            Canvas.SetLeft(_selectionRect, _selectionStartPos.Value.X);
            Canvas.SetTop(_selectionRect, 0);
            _selectionRect.Width = 0;
            _selectionRect.Height = _canvas.ActualHeight;
        }

        private void CanvasOnMouseMove(object sender, MouseEventArgs e)
        {
            if (SoundFilePath == null || _selectionStartPos == null || _selectionRect == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var current = e.GetPosition(_canvas);

            var x = Math.Min(current.X, _selectionStartPos.Value.X);
            var width = Math.Abs(current.X - _selectionStartPos.Value.X);

            Canvas.SetLeft(_selectionRect, x);
            _selectionRect.Width = width;
        }

        private void CanvasOnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SoundFilePath == null) return;

            _canvas!.ReleaseMouseCapture();

            if (_selectionStartPos != null && _selectionRect != null)
            {
                var x = Canvas.GetLeft(_selectionRect);
                var width = _selectionRect.Width;


                var canvasWidth = _canvas.ActualWidth;
                var totalSec = _waveForm!.Duration.TotalSeconds;
                var startSec = x / canvasWidth * totalSec;

                TimeCursor = TimeSpan.FromSeconds(startSec);

                if (width > 1)
                {
                    if (totalSec > 0)
                    {
                        var endSec = (x + width) / canvasWidth * totalSec;

                        
                        SliceStart = TimeSpan.FromSeconds(startSec);
                        SliceEnd = TimeSpan.FromSeconds(endSec);
                        HasSliceSelection = true;
                    }
                }
                else
                {
                    ClearSelection();
                }
            }

            _selectionStartPos = null;
        }

        private void ClearSelection()
        {
            SliceStart = TimeSpan.FromSeconds(0);
            SliceEnd = TimeSpan.FromSeconds(0);
            HasSliceSelection = false;
        }

        private void DrawWaveform(string? filePath)
        {
            _canvas!.Children.Clear();

            if (filePath == null) return;

            AddCursorLine();
            AddBaseLine();

            var canvasWidth = _canvas.ActualWidth;
            var canvasHeight = _canvas.ActualHeight;
            var centerY = canvasHeight / 2.0;

            _waveForm = _waveFormGenerator.ExtractWaveform2(filePath, (int)canvasWidth);

            var xStep = canvasWidth / _waveForm.Samples.Length;

            for (var i = 0; i < _waveForm.Samples.Length; i++)
            {
                var amplitude = _waveForm.Samples[i]; // 0.0 to 1.0
                var lineHeight = amplitude * canvasHeight;

                var line = new Line
                {
                    X1 = i * xStep,
                    Y1 = centerY - lineHeight / 2,
                    X2 = i * xStep,
                    Y2 = centerY + lineHeight / 2,
                    Stroke = Brushes.AntiqueWhite,
                    StrokeThickness = 1
                };

                _canvas.Children.Add(line);
            }
        }

        private void AddCursorLine()
        {
            _waveformCursorLine = new Line
            {
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Y1 = 0,
                Y2 = _canvas!.ActualHeight,
            };

            _canvas.Children.Add(_waveformCursorLine);
        }

        private void AddBaseLine()
        {
            var line = new Line
            {
                Stroke = Brushes.DimGray,
                StrokeThickness = 1,
                X2 = _canvas!.ActualWidth,
                Y1 = _canvas!.ActualHeight * 0.5f,
                Y2 = _canvas!.ActualHeight * 0.5f,
            };

            _canvas.Children.Add(line);
        }

        private void UpdateCursor(TimeSpan time)
        {
            if (_waveformCursorLine == null || _waveForm.Duration.TotalSeconds <= 0)
                return;

            var x = time.TotalSeconds / _waveForm.Duration.TotalSeconds * _canvas!.ActualWidth;
            Canvas.SetLeft(_waveformCursorLine, x);
        }

        public readonly static DependencyProperty SoundFilePathProperty = DependencyProperty.Register(
            nameof(SoundFilePath), typeof(string), typeof(SoundViewer), new FrameworkPropertyMetadata(
                (o, args) =>
                {
                    var waveFormViewer = (o as SoundViewer)!;
                    waveFormViewer.DrawWaveform((string?)args.NewValue);
                    waveFormViewer.ClearSelection();
                }));

        public string? SoundFilePath
        {
            get => (string)GetValue(SoundFilePathProperty);
            set => SetValue(SoundFilePathProperty, value);
        }

        public readonly static DependencyProperty SliceStartProperty = DependencyProperty.Register(
            nameof(SliceStart), typeof(TimeSpan), typeof(SoundViewer), new PropertyMetadata(default(TimeSpan)));

        public TimeSpan SliceStart
        {
            get => (TimeSpan)GetValue(SliceStartProperty);
            set => SetValue(SliceStartProperty, value);
        }

        public readonly static DependencyProperty SliceEndProperty = DependencyProperty.Register(
            nameof(SliceEnd), typeof(TimeSpan), typeof(SoundViewer), new PropertyMetadata(default(TimeSpan)));

        public TimeSpan SliceEnd
        {
            get => (TimeSpan)GetValue(SliceEndProperty);
            set => SetValue(SliceEndProperty, value);
        }

        public readonly static DependencyProperty HasSliceSelectionProperty = DependencyProperty.Register(
            nameof(HasSliceSelection), typeof(bool), typeof(SoundViewer), new PropertyMetadata(default(bool)));

        public bool HasSliceSelection
        {
            get => (bool)GetValue(HasSliceSelectionProperty);
            set => SetValue(HasSliceSelectionProperty, value);
        }

        public readonly static DependencyProperty TimeCursorProperty = DependencyProperty.Register(
            nameof(TimeCursor), typeof(TimeSpan), typeof(SoundViewer), new FrameworkPropertyMetadata(
                default(TimeSpan),
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (o, args) =>
                {
                    (o as SoundViewer)?.UpdateCursor((TimeSpan)args.NewValue);
                }));

        public TimeSpan TimeCursor
        {
            get => (TimeSpan)GetValue(TimeCursorProperty);
            set => SetValue(TimeCursorProperty, value);
        }
    }
}
