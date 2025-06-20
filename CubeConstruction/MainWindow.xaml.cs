using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CubeConstruction
{
    public partial class MainWindow : Window
    {
        private Point _lastMousePosition;
        private Random _random = new Random();
        private int _cubeCount;
        private List<Point> _drawnSquares = new List<Point>();
        private List<Point3D> _cubePositions = new List<Point3D>();
        private RotateTransform3D _rotationX;
        private RotateTransform3D _rotationY;
        private Transform3DGroup _cubeGroupTransform;
        private readonly double _rotationSpeed = 0.5;
        private ModelVisual3D _boundingCubeVisual;
        private DispatcherTimer statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(2);
            statusTimer.Tick += (s, e) =>
            {
                StatusText.Text = "";
                statusTimer.Stop();
            };
            if (drawingCanvas == null || viewComboBox == null)
            {
                MessageBox.Show("ОШИБКА");
                return;
            }

            // Initialize rotation transforms
            _cubeGroupTransform = new Transform3DGroup();
            _rotationX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0));
            _rotationY = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0));
            _cubeGroupTransform.Children.Add(_rotationX);
            _cubeGroupTransform.Children.Add(_rotationY);
            cubeGroup.Transform = _cubeGroupTransform;

            // Setup lighting
            SetupLighting();

            // Generate cubes and bounding cube
            GenerateCubes();
            AddDirectionArrows();
            _boundingCubeVisual = CreateBoundingCube();
            viewport.Children.Add(_boundingCubeVisual);
            AdjustCameraToFit();

            // Attach canvas event handlers
            drawingCanvas.MouseLeftButtonDown += DrawingCanvas_MouseLeftButtonDown;
            drawingCanvas.MouseRightButtonDown += DrawingCanvas_MouseRightButtonDown;
            viewComboBox.SelectionChanged += ViewComboBox_SelectionChanged;

            // Draw grid and labels
            DrawGridAndLabels();
        }

        private void SetupLighting()
        {
            Model3DGroup lightGroup = new Model3DGroup();
            lightGroup.Children.Add(new AmbientLight(Color.FromRgb(255, 255, 255)));

            ModelVisual3D lighting = new ModelVisual3D
            {
                Content = lightGroup
            };

            viewport.Children.Add(lighting);
        }

        private void GenerateCubes()
        {
            _cubeCount = _random.Next(3, 8);
            cubeGroup.Children.Clear();
            _cubePositions.Clear();

            List<Point3D> occupiedPositions = new List<Point3D>();
            List<Point3D> possiblePositions = new List<Point3D>();

            Point3D firstPosition = new Point3D(0, 0, 0);
            occupiedPositions.Add(firstPosition);
            _cubePositions.Add(firstPosition);
            cubeGroup.Children.Add(CreateCube(firstPosition.X, firstPosition.Y, firstPosition.Z));

            AddPossiblePositions(firstPosition, possiblePositions, occupiedPositions);

            for (int i = 1; i < _cubeCount; i++)
            {
                if (possiblePositions.Count == 0) break;

                int index = _random.Next(possiblePositions.Count);
                Point3D newPosition = possiblePositions[index];
                possiblePositions.RemoveAt(index);

                cubeGroup.Children.Add(CreateCube(newPosition.X, newPosition.Y, newPosition.Z));
                occupiedPositions.Add(newPosition);
                _cubePositions.Add(newPosition);

                AddPossiblePositions(newPosition, possiblePositions, occupiedPositions);
            }
        }

        private void AddPossiblePositions(Point3D position, List<Point3D> possiblePositions, List<Point3D> occupiedPositions)
        {
            double cubeSize = 1.0;
            Point3D[] directions = new Point3D[]
            {
                new Point3D(position.X + cubeSize, position.Y, position.Z),
                new Point3D(position.X - cubeSize, position.Y, position.Z),
                new Point3D(position.X, position.Y + cubeSize, position.Z),
                new Point3D(position.X, position.Y - cubeSize, position.Z),
                new Point3D(position.X, position.Y, position.Z + cubeSize),
                new Point3D(position.X, position.Y, position.Z - cubeSize)
            };

            foreach (var newPos in directions)
            {
                if (!occupiedPositions.Contains(newPos) && !possiblePositions.Contains(newPos))
                {
                    possiblePositions.Add(newPos);
                }
            }
        }

        private Model3DGroup CreateCube(double x, double y, double z)
        {
            // Create the main cube mesh for faces
            MeshGeometry3D cubeMesh = new MeshGeometry3D();
            Point3DCollection points = new Point3DCollection
            {
                new Point3D(x, y, z),
                new Point3D(x + 1, y, z),
                new Point3D(x + 1, y + 1, z),
                new Point3D(x, y + 1, z),
                new Point3D(x, y, z + 1),
                new Point3D(x + 1, y, z + 1),
                new Point3D(x + 1, y + 1, z + 1),
                new Point3D(x, y + 1, z + 1)
            };
            Int32Collection indices = new Int32Collection
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 3, 7, 0, 7, 4,
                1, 5, 6, 1, 6, 2,
                3, 2, 6, 3, 6, 7,
                0, 4, 5, 0, 5, 1
            };
            cubeMesh.Positions = points;
            cubeMesh.TriangleIndices = indices;

            // Create the main cube with random color faces
            Color cubeColor = Color.FromRgb((byte)_random.Next(256), (byte)_random.Next(256), (byte)_random.Next(256));
            GeometryModel3D cubeModel = new GeometryModel3D
            {
                Geometry = cubeMesh,
                Material = new DiffuseMaterial(new SolidColorBrush(cubeColor)),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(cubeColor))
            };

            // Create the wireframe for black edges
            GeometryModel3D wireframeModel = CreateCubeWireframe(x, y, z);

            // Create a Model3DGroup to hold both the cube and wireframe
            Model3DGroup cubeGroup = new Model3DGroup();
            cubeGroup.Children.Add(wireframeModel); // Black edges first (behind)
            cubeGroup.Children.Add(cubeModel);      // Main cube faces on top

            return cubeGroup;
        }
        private GeometryModel3D CreateCubeWireframe(double x, double y, double z)
        {
            MeshGeometry3D wireframeMesh = new MeshGeometry3D();
            double thickness = 0.015; // Thickness of the wireframe edges

            // Define the 8 vertices of the cube to match CreateCube's geometry
            Point3D[] vertices = new Point3D[]
            {
        new Point3D(x, y, z),           // Vertex 0
        new Point3D(x + 1, y, z),       // Vertex 1
        new Point3D(x + 1, y + 1, z),   // Vertex 2
        new Point3D(x, y + 1, z),       // Vertex 3
        new Point3D(x, y, z + 1),       // Vertex 4
        new Point3D(x + 1, y, z + 1),   // Vertex 5
        new Point3D(x + 1, y + 1, z + 1), // Vertex 6
        new Point3D(x, y + 1, z + 1)    // Vertex 7
            };

            // Define the 12 edges of the cube by connecting vertices
            int[][] edges = new int[][]
            {
        new[] { 0, 1 }, // Bottom front
        new[] { 1, 2 }, // Bottom right
        new[] { 2, 3 }, // Bottom back
        new[] { 3, 0 }, // Bottom left
        new[] { 4, 5 }, // Top front
        new[] { 5, 6 }, // Top right
        new[] { 6, 7 }, // Top back
        new[] { 7, 4 }, // Top left
        new[] { 0, 4 }, // Front left vertical
        new[] { 1, 5 }, // Front right vertical
        new[] { 2, 6 }, // Back right vertical
        new[] { 3, 7 }  // Back left vertical
            };

            Point3DCollection positions = new Point3DCollection();
            Int32Collection indices = new Int32Collection();

            // For each edge, create a thin rectangular prism (approximating a line)
            for (int i = 0; i < edges.Length; i++)
            {
                Point3D start = vertices[edges[i][0]];
                Point3D end = vertices[edges[i][1]];
                Vector3D dir = end - start;
                double length = dir.Length;
                dir.Normalize();

                // Define a small cross-section perpendicular to the edge direction
                Vector3D up = Math.Abs(dir.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
                Vector3D right = Vector3D.CrossProduct(dir, up);
                right.Normalize();
                up = Vector3D.CrossProduct(right, dir);
                up.Normalize();

                // Create 4 vertices for the cross-section at start and end
                Point3D[] crossSectionStart = new Point3D[4];
                Point3D[] crossSectionEnd = new Point3D[4];
                for (int j = 0; j < 4; j++)
                {
                    double angle = j * Math.PI / 2;
                    Vector3D offset = (Math.Cos(angle) * right + Math.Sin(angle) * up) * thickness;
                    crossSectionStart[j] = start + offset;
                    crossSectionEnd[j] = end + offset;
                }

                // Add vertices to positions
                int baseIndex = positions.Count;
                foreach (var p in crossSectionStart) positions.Add(p);
                foreach (var p in crossSectionEnd) positions.Add(p);

                // Define triangles for the prism (4 sides, each with 2 triangles)
                int[] sideIndices = new int[]
                {
            0, 1, 5, 0, 5, 4, // Side 1
            1, 2, 6, 1, 6, 5, // Side 2
            2, 3, 7, 2, 7, 6, // Side 3
            3, 0, 4, 3, 4, 7  // Side 4
                };
                foreach (int idx in sideIndices)
                {
                    indices.Add(baseIndex + idx);
                }
            }

            wireframeMesh.Positions = positions;
            wireframeMesh.TriangleIndices = indices;

            // Create black material for the wireframe
            GeometryModel3D wireframeModel = new GeometryModel3D
            {
                Geometry = wireframeMesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 0))),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 0)))
            };

            return wireframeModel;
        }

        private void AddDirectionArrows()
        {
            double offset = _cubeCount + 1;
            GeometryModel3D xArrow = CreateArrow(new Point3D(-5, 0, 0), new Vector3D(-1, 0, 0), Colors.Red);
            cubeGroup.Children.Add(xArrow);
            GeometryModel3D yArrow = CreateArrow(new Point3D(0, 5, 0), new Vector3D(0, 1, 0), Colors.Green);
            cubeGroup.Children.Add(yArrow);
            GeometryModel3D zArrow = CreateArrow(new Point3D(0, 0, offset), new Vector3D(0, 0, 1), Colors.Blue);
            cubeGroup.Children.Add(zArrow);
        }

        private GeometryModel3D CreateArrow(Point3D start, Vector3D direction, Color color)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            double length = 1.0;
            double arrowHeadSize = 0.3;
            direction.Normalize();
            Vector3D perpendicular1;
            if (direction != new Vector3D(0, 1, 0))
                perpendicular1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            else
                perpendicular1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
            perpendicular1.Normalize();
            Vector3D perpendicular2 = Vector3D.CrossProduct(direction, perpendicular1);
            perpendicular2.Normalize();
            Point3D end = start + direction * length;
            Point3DCollection shaftPoints = new Point3DCollection
            {
                start,
                start + perpendicular1 * 0.05,
                start + perpendicular2 * 0.05,
                start - perpendicular1 * 0.05,
                start - perpendicular2 * 0.05,
                end + perpendicular1 * 0.05,
                end + perpendicular2 * 0.05,
                end - perpendicular1 * 0.05,
                end - perpendicular2 * 0.05
            };
            Point3D tip = end + direction * arrowHeadSize;
            Point3DCollection headPoints = new Point3DCollection
            {
                end + perpendicular1 * arrowHeadSize,
                end + perpendicular2 * arrowHeadSize,
                end - perpendicular1 * arrowHeadSize,
                end - perpendicular2 * arrowHeadSize,
                tip
            };
            mesh.Positions = new Point3DCollection();
            foreach (var point in shaftPoints) mesh.Positions.Add(point);
            foreach (var point in headPoints) mesh.Positions.Add(point);
            Int32Collection indices = new Int32Collection
            {
                0, 1, 5, 0, 5, 1,
                0, 2, 6, 0, 6, 2,
                0, 3, 7, 0, 7, 3,
                0, 4, 8, 0, 8, 4,
                9, 10, 13, 10, 11, 13, 11, 12, 13, 12, 9, 13
            };
            mesh.TriangleIndices = indices;
            GeometryModel3D arrow = new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(color)),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(color))
            };
            return arrow;
        }

        private void AdjustCameraToFit()
        {
            double maxDimension = Math.Max(3, _cubeCount);
            double distance = maxDimension * 2.5;
            camera.Position = new Point3D(distance, distance, distance);
            camera.LookDirection = new Vector3D(-distance, -distance, -distance);
        }

        private ModelVisual3D CreateBoundingCube()
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            double size = 50.0;

            Point3DCollection positions = new Point3DCollection
            {
                new Point3D(-size, -size, -size),
                new Point3D(size, -size, -size),
                new Point3D(size, size, -size),
                new Point3D(-size, size, -size),
                new Point3D(-size, -size, size),
                new Point3D(size, -size, size),
                new Point3D(size, size, size),
                new Point3D(-size, size, size)
            };

            Int32Collection indices = new Int32Collection
            {
                0, 1, 2, 0, 2, 3,
                5, 4, 7, 5, 7, 6,
                4, 0, 3, 4, 3, 7,
                1, 5, 6, 1, 6, 2,
                3, 2, 6, 3, 6, 7,
                4, 5, 1, 4, 1, 0
            };

            mesh.Positions = positions;
            mesh.TriangleIndices = indices;

            DiffuseMaterial material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));

            GeometryModel3D cubeModel = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            ModelVisual3D modelVisual = new ModelVisual3D
            {
                Content = cubeModel
            };

            viewport.MouseLeftButtonDown += (s, e) =>
            {
                if (IsMouseOverBoundingCube(e.GetPosition(viewport)))
                {
                    _lastMousePosition = e.GetPosition(viewport);
                    e.Handled = true;
                }
            };

            viewport.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && IsMouseOverBoundingCube(e.GetPosition(viewport)))
                {
                    Point currentPosition = e.GetPosition(viewport);
                    double deltaX = currentPosition.X - _lastMousePosition.X;
                    double deltaY = currentPosition.Y - _lastMousePosition.Y;

                    // Get camera's up direction for horizontal rotation
                    Vector3D upAxis = camera.UpDirection;
                    upAxis.Normalize();

                    // Get vertical axis (perpendicular to LookDirection and UpDirection)
                    Vector3D lookDirection = camera.LookDirection;
                    lookDirection.Normalize();
                    Vector3D horizontalAxis = Vector3D.CrossProduct(lookDirection, upAxis);
                    horizontalAxis.Normalize();

                    // Update rotations with camera-relative axes, clamping to [-90, 90] degrees
                    if (_rotationX.Rotation is AxisAngleRotation3D rotX)
                    {
                        rotX.Axis = upAxis; // Horizontal rotation
                        double newXAngle = rotX.Angle + deltaX * _rotationSpeed;
                        rotX.Angle = Math.Max(-60, Math.Min(120, newXAngle)); // Clamp to [-90, 90]
                    }
                    if (_rotationY.Rotation is AxisAngleRotation3D rotY)
                    {
                        rotY.Axis = horizontalAxis; // Vertical rotation
                        double newYAngle = rotY.Angle - deltaY * _rotationSpeed;
                        rotY.Angle = Math.Max(-60, Math.Min(120, newYAngle)); // Clamp to [-90, 90]
                    }

                    _lastMousePosition = currentPosition;
                    e.Handled = true;
                }
            };

            return modelVisual;
        }

        private bool IsMouseOverBoundingCube(Point mousePosition)
        {
            RayMeshGeometry3DHitTestResult hitResult = VisualTreeHelper.HitTest(viewport, mousePosition) as RayMeshGeometry3DHitTestResult;
            if (hitResult != null && hitResult.ModelHit == _boundingCubeVisual.Content)
            {
                return true;
            }
            return false;
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (drawingCanvas == null) return;

            Point position = e.GetPosition(drawingCanvas);
            int gridX = (int)(position.X / 20);
            int gridY = (int)(position.Y / 20);

            if (!_drawnSquares.Contains(new Point(gridX, gridY)))
            {
                Rectangle rect = new Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = Brushes.Yellow,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, gridX * 20);
                Canvas.SetTop(rect, gridY * 20);
                drawingCanvas.Children.Add(rect);
                _drawnSquares.Add(new Point(gridX, gridY));
                Console.WriteLine($"Added square at ({gridX}, {gridY})");
                e.Handled = true;
            }
        }

        private void DrawingCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (drawingCanvas == null) return;

            Point position = e.GetPosition(drawingCanvas);
            int gridX = (int)(position.X / 20);
            int gridY = (int)(position.Y / 20);
            Point targetPoint = new Point(gridX, gridY);

            for (int i = drawingCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (drawingCanvas.Children[i] is Rectangle rect)
                {
                    double left = Canvas.GetLeft(rect);
                    double top = Canvas.GetTop(rect);
                    int rectX = (int)(left / 20);
                    int rectY = (int)(top / 20);
                    if (rectX == gridX && rectY == gridY)
                    {
                        drawingCanvas.Children.Remove(rect);
                        _drawnSquares.Remove(targetPoint);
                        Console.WriteLine($"Removed square at ({gridX}, {gridY})");
                        break;
                    }
                }
            }
            e.Handled = true;
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas == null) return;

            drawingCanvas.Children.Clear();
            _drawnSquares.Clear();
            DrawGridAndLabels();
        }

        private void DrawGridAndLabels()
        {
            if (drawingCanvas == null)
            {
                MessageBox.Show("Error: drawingCanvas is null in DrawGridAndLabels.");
                return;
            }

            drawingCanvas.Children.Clear();

            Path gridLines = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };
            GeometryGroup geometryGroup = new GeometryGroup();
            for (int i = 0; i <= 200; i += 20)
            {
                geometryGroup.Children.Add(new LineGeometry(new Point(i, 0), new Point(i, 200)));
                geometryGroup.Children.Add(new LineGeometry(new Point(0, i), new Point(200, i)));
            }
            gridLines.Data = geometryGroup;
            drawingCanvas.Children.Add(gridLines);

            string selectedView = (viewComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Top View";
            string xAxisLabel = "";
            string yAxisLabel = "";

            if (selectedView == "Top View")
            {
                xAxisLabel = "X";
                yAxisLabel = "Z";
            }
            else if (selectedView == "Left View")
            {
                xAxisLabel = "Z";
                yAxisLabel = "Y";
            }
            else if (selectedView == "Front View")
            {
                xAxisLabel = "X";
                yAxisLabel = "Y";
            }

            for (int i = 0; i < 10; i++)
            {
                TextBlock label = new TextBlock
                {
                    Text = i.ToString(),
                    FontSize = 10,
                    Foreground = Brushes.Red,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, i * 20 + 5);
                Canvas.SetTop(label, 0);
                //drawingCanvas.Children.Add(label);
            }
            TextBlock xAxis = new TextBlock
            {
                Text = xAxisLabel,
                FontSize = 12,
                Foreground = Brushes.Red,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(xAxis, 200);
            Canvas.SetTop(xAxis, 0);
           // drawingCanvas.Children.Add(xAxis);

            for (int i = 0; i < 10; i++)
            {
                int yValue = i;
                if (selectedView != "Top View") yValue = 9 - i;
                TextBlock label = new TextBlock
                {
                    Text = yValue.ToString(),
                    FontSize = 10,
                    Foreground = Brushes.Blue,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, i * 20 + 5);
              //  drawingCanvas.Children.Add(label);
            }
            TextBlock yAxis = new TextBlock
            {
                Text = yAxisLabel,
                FontSize = 12,
                Foreground = Brushes.Blue,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(yAxis, 0);
            Canvas.SetTop(yAxis, 200);
           // drawingCanvas.Children.Add(yAxis);
        }

        private void ViewComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearGrid_Click(null, null);
        }

        private void VerifyDrawing_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas == null || viewComboBox == null) return;
          //  ComboBoxItem selectedView = viewComboBox.SelectedItem();
            string selectedView = (viewComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            bool isCorrect = false;

            HashSet<Point> expectedPattern = new HashSet<Point>();
            double minX = 0, minZ = 0, minY = 0;

            if (selectedView == "Вид сверху")
            {
                var groupedByXZ = _cubePositions.GroupBy(p => new { p.X, p.Z })
                    .Select(g => g.OrderByDescending(p => p.Y).First());
                minX = groupedByXZ.Min(p => p.X);
                minZ = groupedByXZ.Min(p => p.Z);
                foreach (var pos in groupedByXZ)
                {
                    int relativeX = (int)(pos.X - minX);
                    int relativeZ = (int)(pos.Z - minZ); // Z maps to Y on the grid for Top View
                    expectedPattern.Add(new Point(relativeX, relativeZ));
                }
            }
            else if (selectedView == "Вид слева")
            {
                var groupedByYZ = _cubePositions.GroupBy(p => new { p.Y, p.Z })
                    .Select(g => g.OrderByDescending(p => p.X).First());
                minY = groupedByYZ.Min(p => p.Y);
                minZ = groupedByYZ.Min(p => p.Z);
                foreach (var pos in groupedByYZ)
                {
                    int relativeX = (int)(pos.Z - minZ);
                    int relativeY = (int)(maxY(groupedByYZ) - (pos.Y - minY));
                    expectedPattern.Add(new Point( relativeX, relativeY));
                }
            }
            else if (selectedView == "Вид спереди" )
            {
                var groupedByXY = _cubePositions.GroupBy(p => new { p.X, p.Y })
                    .Select(g => g.OrderByDescending(p => p.Z).First());
                minX = groupedByXY.Min(p => p.X);
                minY = groupedByXY.Min(p => p.Y);
                foreach (var pos in groupedByXY)
                {
                    int relativeX = (int)(pos.X - minX);
                    int relativeY = (int)(maxY(groupedByXY) - (pos.Y - minY));
                    expectedPattern.Add(new Point(relativeX, relativeY));
                }
            }

            if (_drawnSquares.Count == 0)
            {
                ShowStatus("Нарисуйте что нибудь в области!!");
              
                return;
            }

            double drawnMinX = _drawnSquares.Min(p => p.X);
            double drawnMinY = _drawnSquares.Min(p => p.Y);
            HashSet<Point> drawnPattern = new HashSet<Point>();
            foreach (var pos in _drawnSquares)
            {
                int relativeX = (int)(pos.X - drawnMinX);
                int relativeY = (int)(pos.Y - drawnMinY);
                drawnPattern.Add(new Point(relativeX, relativeY));
            }

            isCorrect = expectedPattern.Count == drawnPattern.Count &&
                       expectedPattern.All(p => drawnPattern.Contains(p)) &&
                       drawnPattern.All(p => expectedPattern.Contains(p));

            //string expectedDebug = "Expected Pattern: " + string.Join(", ", expectedPattern.Select(p => $"({p.X},{p.Y})"));
            // string drawnDebug = "Drawn Pattern: " + string.Join(", ", drawnPattern.Select(p => $"({p.X},{p.Y})"));
            //  string debugMessage = $"{expectedDebug}\n{drawnDebug}";
            // MessageBox.Show(debugMessage);
            ShowStatus(isCorrect ? "Верно" : "Неверно");
        }

        private double maxY(IEnumerable<Point3D> points)
        {
            double minY = points.Min(p => p.Y);
            return points.Max(p => p.Y - minY);
        }

        private void ShowStatus(string message)
        {
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
            statusTimer.Stop(); // сбрасываем, если таймер уже бежит
            statusTimer.Start(); // запускаем заново
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas == null) return;

            drawingCanvas.Children.Clear();
            _drawnSquares.Clear();
            DrawGridAndLabels();

            GenerateCubes();
            AddDirectionArrows();
            _boundingCubeVisual = CreateBoundingCube();
            viewport.Children.Add(_boundingCubeVisual);
            AdjustCameraToFit();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}