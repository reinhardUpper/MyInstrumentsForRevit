using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ContextFilter.UI.Views;

/// <summary>
/// Small classic snake game hosted by Revit as a WPF utility window.
/// </summary>
public partial class SnakeGameWindow : Window, INotifyPropertyChanged
{
    private const int GridSize = 18;
    private const int CellSize = 20;
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private readonly LinkedList<Point> _snake = new();
    private Direction _direction = Direction.Right;
    private Direction _pendingDirection = Direction.Right;
    private Point _food;
    private int _score;
    private bool _isGameOver;

    /// <summary>Creates and starts the game window.</summary>
    public SnakeGameWindow()
    {
        InitializeComponent();
        DataContext = this;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(115)
        };
        _timer.Tick += OnTick;
        Restart();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Current score display text.</summary>
    public string ScoreText => $"Score: {_score}";

    /// <summary>Current game status text.</summary>
    public string StatusText { get; private set; } = "Use arrow keys or WASD. Eat the green square.";

    private void OnCanvasLoaded(object sender, RoutedEventArgs e)
    {
        GameCanvas.Focus();
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        Restart();
        GameCanvas.Focus();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_isGameOver && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            Restart();
            return;
        }

        var next = e.Key switch
        {
            Key.Up or Key.W => Direction.Up,
            Key.Down or Key.S => Direction.Down,
            Key.Left or Key.A => Direction.Left,
            Key.Right or Key.D => Direction.Right,
            _ => _pendingDirection
        };

        if (!IsOpposite(_direction, next))
        {
            _pendingDirection = next;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isGameOver)
        {
            return;
        }

        _direction = _pendingDirection;
        var head = _snake.First?.Value ?? new Point(0, 0);
        var nextHead = Move(head, _direction);

        if (nextHead.X < 0
            || nextHead.Y < 0
            || nextHead.X >= GridSize
            || nextHead.Y >= GridSize
            || _snake.Contains(nextHead))
        {
            EndGame();
            return;
        }

        _snake.AddFirst(nextHead);
        if (nextHead == _food)
        {
            _score++;
            OnPropertyChanged(nameof(ScoreText));
            PlaceFood();
        }
        else
        {
            _snake.RemoveLast();
        }

        Render();
    }

    private void Restart()
    {
        _timer.Stop();
        _snake.Clear();
        _snake.AddFirst(new Point(7, 9));
        _snake.AddFirst(new Point(8, 9));
        _snake.AddFirst(new Point(9, 9));
        _direction = Direction.Right;
        _pendingDirection = Direction.Right;
        _score = 0;
        _isGameOver = false;
        StatusText = "Use arrow keys or WASD. Eat the green square.";
        PlaceFood();
        Render();
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(StatusText));
        _timer.Start();
    }

    private void EndGame()
    {
        _isGameOver = true;
        _timer.Stop();
        StatusText = "Game over. Press Enter, Space, or Restart.";
        OnPropertyChanged(nameof(StatusText));
    }

    private void PlaceFood()
    {
        var freeCells = new List<Point>();
        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                var point = new Point(x, y);
                if (!_snake.Contains(point))
                {
                    freeCells.Add(point);
                }
            }
        }

        _food = freeCells.Count == 0
            ? new Point(0, 0)
            : freeCells[_random.Next(freeCells.Count)];
    }

    private void Render()
    {
        GameCanvas.Children.Clear();
        DrawGrid();
        DrawCell(_food, Brushes.GreenYellow, 4);

        var isHead = true;
        foreach (var segment in _snake)
        {
            DrawCell(segment, isHead ? Brushes.White : Brushes.MediumTurquoise, 4);
            isHead = false;
        }
    }

    private void DrawGrid()
    {
        var pen = new SolidColorBrush(Color.FromRgb(40, 43, 48));
        for (var i = 1; i < GridSize; i++)
        {
            var offset = i * CellSize;
            GameCanvas.Children.Add(new Line
            {
                X1 = offset,
                X2 = offset,
                Y1 = 0,
                Y2 = GridSize * CellSize,
                Stroke = pen,
                StrokeThickness = 1
            });
            GameCanvas.Children.Add(new Line
            {
                X1 = 0,
                X2 = GridSize * CellSize,
                Y1 = offset,
                Y2 = offset,
                Stroke = pen,
                StrokeThickness = 1
            });
        }
    }

    private void DrawCell(Point point, Brush brush, double radius)
    {
        var rectangle = new Rectangle
        {
            Width = CellSize - 2,
            Height = CellSize - 2,
            RadiusX = radius,
            RadiusY = radius,
            Fill = brush
        };
        Canvas.SetLeft(rectangle, point.X * CellSize + 1);
        Canvas.SetTop(rectangle, point.Y * CellSize + 1);
        GameCanvas.Children.Add(rectangle);
    }

    private static Point Move(Point point, Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Point(point.X, point.Y - 1),
            Direction.Down => new Point(point.X, point.Y + 1),
            Direction.Left => new Point(point.X - 1, point.Y),
            Direction.Right => new Point(point.X + 1, point.Y),
            _ => point
        };
    }

    private static bool IsOpposite(Direction current, Direction next)
    {
        return current == Direction.Up && next == Direction.Down
            || current == Direction.Down && next == Direction.Up
            || current == Direction.Left && next == Direction.Right
            || current == Direction.Right && next == Direction.Left;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}
