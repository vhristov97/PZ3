using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using WpfApp1.Model;

namespace PZ3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
		int[,] gridMatrix;
		Dictionary<long, Tuple<IPowerEntity, GeometryModel3D>> nodeDictionary;
		Dictionary<GeometryModel3D, LineEntity> lineDictionary;
		Point start;

		ToolTip tooltip;

		List<Tuple<IPowerEntity, GeometryModel3D>> linkedNodes;

		Tuple<double, double> minPoint = new Tuple<double, double>(19.793909, 45.2325);
		Tuple<double, double> maxPoint = new Tuple<double, double>(19.894459, 45.277031);

		const int cubeSize = 5;
		const int zoomMax = 5;

		int zoomCurrent = -zoomMax;

		public MainWindow()
        {
			gridMatrix = new int[200, 300];
			nodeDictionary = new Dictionary<long, Tuple<IPowerEntity, GeometryModel3D>>();
			lineDictionary = new Dictionary<GeometryModel3D, LineEntity>();

			tooltip = new ToolTip();
			linkedNodes = new List<Tuple<IPowerEntity, GeometryModel3D>>();

			InitializeComponent();
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            btnLoad.IsEnabled = false;

			ParseXml();

			checkLines.IsEnabled = true;
        }

		//From UTM to Latitude and longitude in decimal
		private void ToLatLon(double utmX, double utmY, int zoneUTM, out double latitude, out double longitude)
		{
			bool isNorthHemisphere = true;

			var diflat = -0.00066286966871111111111111111111111111;
			var diflon = -0.0003868060578;

			var zone = zoneUTM;
			var c_sa = 6378137.000000;
			var c_sb = 6356752.314245;
			var e2 = Math.Pow((Math.Pow(c_sa, 2) - Math.Pow(c_sb, 2)), 0.5) / c_sb;
			var e2cuadrada = Math.Pow(e2, 2);
			var c = Math.Pow(c_sa, 2) / c_sb;
			var x = utmX - 500000;
			var y = isNorthHemisphere ? utmY : utmY - 10000000;

			var s = ((zone * 6.0) - 183.0);
			var lat = y / (c_sa * 0.9996);
			var v = (c / Math.Pow(1 + (e2cuadrada * Math.Pow(Math.Cos(lat), 2)), 0.5)) * 0.9996;
			var a = x / v;
			var a1 = Math.Sin(2 * lat);
			var a2 = a1 * Math.Pow((Math.Cos(lat)), 2);
			var j2 = lat + (a1 / 2.0);
			var j4 = ((3 * j2) + a2) / 4.0;
			var j6 = ((5 * j4) + Math.Pow(a2 * (Math.Cos(lat)), 2)) / 3.0;
			var alfa = (3.0 / 4.0) * e2cuadrada;
			var beta = (5.0 / 3.0) * Math.Pow(alfa, 2);
			var gama = (35.0 / 27.0) * Math.Pow(alfa, 3);
			var bm = 0.9996 * c * (lat - alfa * j2 + beta * j4 - gama * j6);
			var b = (y - bm) / v;
			var epsi = ((e2cuadrada * Math.Pow(a, 2)) / 2.0) * Math.Pow((Math.Cos(lat)), 2);
			var eps = a * (1 - (epsi / 3.0));
			var nab = (b * (1 - epsi)) + lat;
			var senoheps = (Math.Exp(eps) - Math.Exp(-eps)) / 2.0;
			var delt = Math.Atan(senoheps / (Math.Cos(nab)));
			var tao = Math.Atan(Math.Cos(delt) * Math.Tan(nab));

			longitude = ((delt * (180.0 / Math.PI)) + s) + diflon;
			latitude = ((lat + (1 + e2cuadrada * Math.Pow(Math.Cos(lat), 2) - (3.0 / 2.0) * e2cuadrada * Math.Sin(lat) * Math.Cos(lat) * (tao - lat)) * (tao - lat)) * (180.0 / Math.PI)) + diflat;
		}

		private int RangeConverter(double oldMin, double oldMax, double newMin, double newMax, double oldValue)
		{
			double oldRange = oldMax - oldMin;
			double newRange = newMax - newMin;

			return Convert.ToInt32((((oldValue - oldMin) * newRange) / oldRange) + newMin);
		}
		
		private void ParseXml()
        {
			XmlDocument xmlDoc = new XmlDocument();

			xmlDoc.Load("Geographic.xml");
			XmlNodeList nodeList;

			nodeList = xmlDoc.SelectNodes("/NetworkModel/Substations/SubstationEntity");
			foreach (XmlNode node in nodeList)
			{
				UploadNode(node, new SubstationEntity());
            }

			nodeList = xmlDoc.SelectNodes("/NetworkModel/Nodes/NodeEntity");
			foreach (XmlNode node in nodeList)
			{
				UploadNode(node, new NodeEntity());
			}

			nodeList = xmlDoc.SelectNodes("/NetworkModel/Switches/SwitchEntity");
			foreach (XmlNode node in nodeList)
			{
				UploadNode(node, new SwitchEntity());
			}

			nodeList = xmlDoc.SelectNodes("/NetworkModel/Lines/LineEntity");
			foreach(XmlNode node in nodeList)
            {
				LineEntity l = new LineEntity();

				ParseLine(node, l);

				if(!nodeDictionary.ContainsKey(l.FirstEnd) || !nodeDictionary.ContainsKey(l.SecondEnd))
                {
					continue;
                }

				for(int i = 0; i < l.Vertices.Count(); i++)
                {
					double noviX, noviZ;

					ToLatLon(l.Vertices[i].X, l.Vertices[i].Z, 34, out noviZ, out noviX);

					/*l.Vertices[i] = new Point3D(RangeConverter(minPoint.Item1, maxPoint.Item1, 0, gridMatrix.GetLength(1) - 1, noviX), 1, 
						RangeConverter(minPoint.Item2, maxPoint.Item2, 0, gridMatrix.GetLength(0) - 1, noviZ));*/
					l.Vertices[i] = new Point3D( noviX, 1, noviZ);
				}

				DrawPolyline(l);
            }
		}

        private void UploadNode(XmlNode node, IPowerEntity entity)
        {
			double noviX, noviZ;

			ParseNode(node, entity);

			ToLatLon(entity.X, entity.Z, 34, out noviZ, out noviX);

			if (noviZ < minPoint.Item2 || noviZ > maxPoint.Item2 ||
				noviX < minPoint.Item1 || noviX > maxPoint.Item1)
			{
				return;
			}

			entity.X = RangeConverter(minPoint.Item1, maxPoint.Item1, 0, gridMatrix.GetLength(1) - 1, noviX);
			entity.Z = RangeConverter(minPoint.Item2, maxPoint.Item2, 0, gridMatrix.GetLength(0) - 1, noviZ);
			entity.Y = gridMatrix[(int)entity.Z, (int)entity.X]++;
			entity.Y = entity.Y * (cubeSize + 1) + 1;

			nodeDictionary.Add(entity.Id, new Tuple<IPowerEntity, GeometryModel3D>(entity, DrawNode(entity)));
		}

		private void ParseNode(XmlNode node, IPowerEntity entity)
		{
			entity.Id = long.Parse(node.SelectSingleNode("Id").InnerText);
			entity.Name = node.SelectSingleNode("Name").InnerText;
			entity.X = double.Parse(node.SelectSingleNode("X").InnerText);
			entity.Z = double.Parse(node.SelectSingleNode("Y").InnerText);
			if (entity.GetType() == typeof(SwitchEntity))
				((SwitchEntity)entity).Status = node.SelectSingleNode("Status").InnerText;
		}

		private GeometryModel3D DrawNode(IPowerEntity entity)
        {
			int viewX = RangeConverter(0, gridMatrix.GetLength(1), -587, 587, entity.X);
			int viewZ = 0 - RangeConverter(0, gridMatrix.GetLength(0), -387, 387, entity.Z);
			
			MeshGeometry3D mesh = new MeshGeometry3D();

			Point3D[] positions = new Point3D[] { new Point3D(viewX - cubeSize/2, entity.Y, viewZ + cubeSize/2), new Point3D(viewX + cubeSize/2, entity.Y, viewZ + cubeSize/2),
				new Point3D(viewX + cubeSize/2, entity.Y, viewZ - cubeSize/2), new Point3D(viewX - cubeSize/2, entity.Y, viewZ - cubeSize/2), new Point3D(viewX - cubeSize/2, entity.Y + cubeSize, viewZ + cubeSize/2),
				new Point3D(viewX + cubeSize/2, entity.Y + cubeSize, viewZ + cubeSize/2), new Point3D(viewX + cubeSize/2, entity.Y + cubeSize, viewZ - cubeSize/2), new Point3D(viewX - cubeSize/2, entity.Y + cubeSize, viewZ - cubeSize/2) };

			int[] vertices = new int[] {0, 3, 1, 3, 2, 1,/**/ 4, 5, 7, 5, 6, 7, /**/ 0, 7, 3, 0, 4, 7,
				/**/ 1, 2, 5, 2, 6, 5,/**/ 0, 1, 4, 1, 5, 4, /**/ 3, 7, 2, 7, 6, 2};

			mesh.Positions = new Point3DCollection(positions);
			mesh.TriangleIndices = new Int32Collection(vertices);

			GeometryModel3D model = new GeometryModel3D(mesh, NodeColor(entity));

			models.Children.Add(model);
			
			return model;
        }

		private DiffuseMaterial NodeColor(IPowerEntity entity)
        {
			DiffuseMaterial dm;

			if (entity.GetType() == typeof(SubstationEntity))
				dm = new DiffuseMaterial(Brushes.Red);
			else if (entity.GetType() == typeof(NodeEntity))
				dm = new DiffuseMaterial(Brushes.Green);
			else
				dm = new DiffuseMaterial(Brushes.Blue);

			return dm;
		}

		private void ParseLine(XmlNode node, LineEntity l)
		{
			l.Id = long.Parse(node.SelectSingleNode("Id").InnerText);
			l.Name = node.SelectSingleNode("Name").InnerText;
			if (node.SelectSingleNode("IsUnderground").InnerText.Equals("true"))
			{
				l.IsUnderground = true;
			}
			else
			{
				l.IsUnderground = false;
			}
			l.R = float.Parse(node.SelectSingleNode("R").InnerText);
			l.ConductorMaterial = node.SelectSingleNode("ConductorMaterial").InnerText;
			l.LineType = node.SelectSingleNode("LineType").InnerText;
			l.ThermalConstantHeat = long.Parse(node.SelectSingleNode("ThermalConstantHeat").InnerText);
			l.FirstEnd = long.Parse(node.SelectSingleNode("FirstEnd").InnerText);
			l.SecondEnd = long.Parse(node.SelectSingleNode("SecondEnd").InnerText);

			foreach (XmlNode pointNode in node.ChildNodes[9].ChildNodes) // 9 posto je Vertices 9. node u jednom line objektu
			{
				Point3D p = new Point3D(double.Parse(pointNode.SelectSingleNode("X").InnerText), 1,
					double.Parse(pointNode.SelectSingleNode("Y").InnerText));

				l.Vertices.Add(p);
			}
		}

		private GeometryModel3D DrawLine(Point3D start, Point3D end)
        {
			int startViewX = RangeConverter(minPoint.Item1, maxPoint.Item1, -587, 587, start.X);
			int startViewZ = 0 - RangeConverter(minPoint.Item2, maxPoint.Item2, -387, 387, start.Z);
			Point3D viewStart = new Point3D(startViewX, start.Y, startViewZ);

			int endViewX = RangeConverter(minPoint.Item1, maxPoint.Item1, -587, 587, end.X);
			int endViewZ = 0 - RangeConverter(minPoint.Item2, maxPoint.Item2, -387, 387, end.Z);
			Point3D viewEnd = new Point3D(endViewX, end.Y, endViewZ);

			Vector3D lineDir = viewEnd - viewStart;
			Vector3D sideDir = Vector3D.CrossProduct(lineDir, new Vector3D(0, 1, 0));
			sideDir.Normalize();

			GeometryModel3D line = new GeometryModel3D();

			MeshGeometry3D mesh = new MeshGeometry3D();

			Point3D[] positions = new Point3D[4];

			positions[0] = viewStart - sideDir * ((cubeSize / 2) - 1);
			positions[1] = viewStart + sideDir * ((cubeSize / 2) - 1);
			positions[2] = viewEnd - sideDir * ((cubeSize / 2) - 1);
			positions[3] = viewEnd + sideDir * ((cubeSize / 2) - 1);

			int[] vertices = new int[] { 0, 1, 2, 1, 3, 2};

			mesh.Positions = new Point3DCollection(positions);
			mesh.TriangleIndices = new Int32Collection(vertices);
			line.Geometry = mesh;

			line.Material = new DiffuseMaterial(Brushes.Black);

			models.Children.Add(line);

			return line;
        }

		private void DrawPolyline(LineEntity l)
        {
			foreach (Point3D p in l.Vertices)
			{
				if (l.Vertices.IndexOf(p) == (l.Vertices.Count - 1))
				{
					break;
				}
                else
                {
					lineDictionary.Add(DrawLine(p, l.Vertices[l.Vertices.IndexOf(p) + 1]), l);
                }
			}
		}

        private void checkLines_Checked(object sender, RoutedEventArgs e)
        {
			foreach (KeyValuePair<GeometryModel3D, LineEntity> line in lineDictionary)
			{
				models.Children.Add(line.Key);
			}
		}

        private void checkLines_Unchecked(object sender, RoutedEventArgs e)
        {
			ResetClick();

            foreach (KeyValuePair<GeometryModel3D, LineEntity> line in lineDictionary)
            {
				models.Children.Remove(line.Key);
            }
		}

        private void mainViewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
			ResetClick();

			System.Windows.Point mouseposition = e.GetPosition(mainViewport);
			Point3D testpoint3D = new Point3D(mouseposition.X, mouseposition.Y, 0);

			PointHitTestParameters pointparams =
					 new PointHitTestParameters(mouseposition);

			VisualTreeHelper.HitTest(mainViewport, null, HTResult, pointparams);
		}

		private HitTestResultBehavior HTResult(System.Windows.Media.HitTestResult rawresult)
		{
			RayHitTestResult rayResult = rawresult as RayHitTestResult;

			if (rayResult != null)
			{
				string t = "";

                if (lineDictionary.ContainsKey((GeometryModel3D)rayResult.ModelHit))
                {
					LineEntity l = lineDictionary[(GeometryModel3D)rayResult.ModelHit];
					t = "ID: " + l.Id + "\nName: " + l.Name;

					nodeDictionary[l.FirstEnd].Item2.Material = new DiffuseMaterial(Brushes.Yellow);
					nodeDictionary[l.SecondEnd].Item2.Material = new DiffuseMaterial(Brushes.Yellow);

					linkedNodes.Add(nodeDictionary[l.FirstEnd]);
					linkedNodes.Add(nodeDictionary[l.SecondEnd]);
				}
                else
                {
					foreach (KeyValuePair<long, Tuple<IPowerEntity, GeometryModel3D>> cube in nodeDictionary)
					{
						if (cube.Value.Item2 == rayResult.ModelHit)
						{
							t = "ID: " + cube.Key + "\nName: " + cube.Value.Item1.Name;
							if (cube.Value.Item1.GetType() == typeof(SwitchEntity))
								t += "\nStatus: " + ((SwitchEntity)cube.Value.Item1).Status;
							
							break;
						}
					}
				}

				if(t != "")
                {
					tooltip.Content = t;
					tooltip.IsOpen = true;
					tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
				}
			}

			return HitTestResultBehavior.Stop;
		}

		private void ResetClick()
        {
			tooltip.IsOpen = false;

            foreach (Tuple<IPowerEntity, GeometryModel3D> node in linkedNodes)
            {
				node.Item2.Material = NodeColor(node.Item1);
            }

			linkedNodes.Clear();
		}

        private void mainViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
			if(zoomCurrent < zoomMax && e.Delta > 0)
            {
				zoomCurrent++;
				camera.Position += camera.LookDirection * e.Delta;
			}

			if(zoomCurrent > -zoomMax && e.Delta < 0)
            {
				zoomCurrent--;
				camera.Position += camera.LookDirection * e.Delta;
			}

			tooltip.IsOpen = false;
        }

        private void mainViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
			mainViewport.CaptureMouse();
        }

        private void mainViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
			mainViewport.ReleaseMouseCapture();
        }

        private void mainViewport_MouseMove(object sender, MouseEventArgs e)
        {
			Point end = e.GetPosition(mainViewport);
			Vector dir = end - start;

			if (mainViewport.IsMouseCaptured)
            {
				Vector3D left = Vector3D.CrossProduct(camera.UpDirection, camera.LookDirection);
				
				camera.Position += left * dir.X + camera.UpDirection * dir.Y;

				tooltip.IsOpen = false;
			}

			if(e.MiddleButton == MouseButtonState.Pressed)
            {
				rotateY.Angle += dir.X;
				rotateX.Angle += dir.Y;

				tooltip.IsOpen = false;
            }

			start = e.GetPosition(mainViewport);
		}
    }
}
