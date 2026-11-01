// Builds the trainer's country maps (Resources/Maps/*.map) from Natural Earth data (public domain)
// and the game's editor data. The editor places every club on a 440 x 320 picture of its country;
// the clubs' stadium towns (StadXxxx.sav) are looked up among Natural Earth's populated places,
// and an affine fit of their map positions (LandXxxx.sav) gives the projection from longitude and
// latitude to the editor's pixels. That fit is then refined so the country covers the brown
// country shape of the editor's own picture (JPEG resources of Editor.exe) as well as possible -
// the clubs were placed on that picture. See README.md.
//
// dotnet run -- <game dir> <Natural Earth geojson dir> <output dir> [<preview dir>]

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static class Program
{
    internal const int Width = 440, Height = 320;
    private const double Margin = 24;          // pixels kept around the picture
    private const double Tolerance = 0.2;      // Douglas-Peucker, pixels
    private const int Scale = 16;              // fixed point: 1/16 pixel

    private sealed record Country(string Iso, string File, string Name, int EditorJpeg, Func<JsonElement, bool> Member, Func<JsonElement, string> Region);

    private static readonly Country[] Countries =
    {
        new("DEU", "Deut", "Deutschland", 319, p => Str(p, "adm0_a3") == "DEU", p => Str(p, "name")),
        new("GBR", "Engl", "England", 320, p => Str(p, "adm0_a3") == "GBR" && Str(p, "geonunit") == "England", p => Str(p, "region")),
        new("FRA", "Fran", "Frankreich", 321, p => Str(p, "adm0_a3") == "FRA" && Lon(p) > -6 && Lat(p) > 40, p => Str(p, "region")),
        new("ITA", "Ital", "Italien", 323, p => Str(p, "adm0_a3") == "ITA", p => Str(p, "region")),
        new("ESP", "Span", "Spanien", 318, p => Str(p, "adm0_a3") == "ESP" && Lat(p) > 35 && Lon(p) > -10, p => Str(p, "region")),
    };

    private static string Str(JsonElement p, string name)
    {
        foreach (JsonProperty prop in p.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
        return null;
    }

    private static double Num(JsonElement p, string name) => double.TryParse(Str(p, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
    private static double Lon(JsonElement p) => Num(p, "longitude");
    private static double Lat(JsonElement p) => Num(p, "latitude");

    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("dotnet run -- <game dir> <Natural Earth geojson dir> <output dir> [<preview dir>]");
            return 1;
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string game = args[0], ne = args[1], output = args[2];
        string preview = args.Length > 3 ? args[3] : null;
        byte[] editor = File.ReadAllBytes(Path.Combine(game, "Editor.exe"));
        Directory.CreateDirectory(output);
        if (preview != null)
            Directory.CreateDirectory(preview);

        Console.WriteLine("Lese Natural Earth ...");
        JsonDocument places = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ne, "ne_10m_populated_places.geojson")));
        JsonDocument admin0 = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ne, "ne_10m_admin_0_countries.geojson")));
        JsonDocument admin1 = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ne, "ne_10m_admin_1_states_provinces.geojson")));

        foreach (Country country in Countries)
        {
            List<(string Town, double X, double Y)> clubs = ReadClubs(game, country.File);
            Affine fit = Calibrate(country, clubs, places, out List<(string Town, double X, double Y, double Lon, double Lat)> used);
            using Bitmap picture = (Bitmap)Image.FromStream(new MemoryStream(PeResource(editor, "JPEGS", country.EditorJpeg)));
            fit = Refine(country, fit, picture, MemberRings(country, admin1));
            Console.WriteLine($"{country.Name}: Restfehler der Vereine nach dem Ausrichten {Math.Sqrt(used.Average(m => Error(fit, m) * Error(fit, m))):F1} px");
            Map map = BuildMap(country, fit, admin0, admin1);
            string file = Path.Combine(output, country.Iso + ".map");
            map.Write(file);
            Console.WriteLine($"{country.Name}: {file} ({new FileInfo(file).Length:N0} Byte)");
            if (preview != null)
                Preview(map, used, fit, picture, Path.Combine(preview, country.Iso + ".png"));
        }
        return 0;
    }

    #region Clubs
    // Map position and stadium town of every club of the country, in the files' order.
    private static List<(string Town, double X, double Y)> ReadClubs(string game, string stem)
    {
        Encoding latin1 = Encoding.GetEncoding(28591);
        string dir = Path.Combine(game, "DATA.DEF");
        string[] land = File.ReadAllLines(FindFile(dir, $"Land{stem}.sav"), latin1);
        string[] stad = File.ReadAllLines(FindFile(dir, $"Stad{stem}.sav"), latin1);

        List<(string, double, double)> clubs = new();
        int count = int.Parse(land[2]), i = 6;
        for (int c = 0; c < count; c++)
        {
            i += 4 + 5 + 4 + 6;               // name, short name, chant, fan name; manager; trainer; jerseys
            double x = double.Parse(land[i + 10]), y = double.Parse(land[i + 11]);
            int players = int.Parse(land[i + 12]);
            i += 13;
            for (int p = 0; p < players; p++)
            {
                if (land[i].Length == 0 || land[i][0] < ' ')
                    i++;
                i += 15;
            }
            clubs.Add((stad[c * 31 + 1].Trim(), x, y));
        }
        return clubs;
    }

    private static string FindFile(string dir, string name) =>
        Directory.GetFiles(dir).First(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        s = s.ToLowerInvariant().Replace("ß", "ss");
        StringBuilder b = new();
        foreach (char ch in s.Normalize(NormalizationForm.FormD))
            if (char.IsLetter(ch) && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                b.Append(ch);
        return b.ToString();
    }
    #endregion

    #region Calibration
    internal sealed class Affine
    {
        public double A, B, C, D, E, F, CosLat;
        public (double X, double Y) Apply(double lon, double lat)
        {
            double u = lon * this.CosLat, v = -lat;
            return (this.A * u + this.B * v + this.C, this.D * u + this.E * v + this.F);
        }
    }

    private static Affine Calibrate(Country country, List<(string Town, double X, double Y)> clubs, JsonDocument places,
        out List<(string Town, double X, double Y, double Lon, double Lat)> used)
    {
        Dictionary<string, (double Lon, double Lat, double Pop)> index = new();
        foreach (JsonElement f in places.RootElement.GetProperty("features").EnumerateArray())
        {
            JsonElement p = f.GetProperty("properties");
            if (Str(p, "ADM0_A3") != country.Iso)
                continue;
            double lon = Num(p, "LONGITUDE"), lat = Num(p, "LATITUDE"), pop = Num(p, "POP_MAX");
            IEnumerable<string> names = new[] { "NAME", "NAMEASCII", "NAME_DE", "NAME_EN", "NAME_FR", "NAME_IT", "NAME_ES", "LS_NAME" }.Select(n => Str(p, n))
                .Concat((Str(p, "NAMEALT") ?? "").Split('|'));
            foreach (string name in names.Select(Normalize).Where(n => n.Length > 0).Distinct())
                if (!index.TryGetValue(name, out var known) || known.Pop < pop)
                    index[name] = (lon, lat, pop);
        }

        List<(string Town, double X, double Y, double Lon, double Lat)> matches = new();
        foreach (var club in clubs)
            if (index.TryGetValue(Normalize(club.Town), out var place))
                matches.Add((club.Town, club.X, club.Y, place.Lon, place.Lat));

        double cosLat = Math.Cos(matches.Average(m => m.Lat) * Math.PI / 180);
        Affine fit = null;
        List<(string Town, double X, double Y, double Lon, double Lat)> current = matches.Distinct().ToList();
        for (int round = 0; round < 6; round++)
        {
            fit = Solve(current, cosLat);
            List<double> errors = current.Select(m => Error(fit, m)).ToList();
            double median = errors.OrderBy(e => e).ElementAt(errors.Count / 2);
            double limit = Math.Max(4, 3 * median);
            List<(string, double, double, double, double)> kept = current.Where(m => Error(fit, m) <= limit).ToList();
            if (kept.Count == current.Count)
                break;
            current = kept;
        }
        double rms = Math.Sqrt(current.Average(m => Error(fit, m) * Error(fit, m)));
        Console.WriteLine($"{country.Name}: {clubs.Count} Vereine, {matches.Count} Orte gefunden, {current.Count} für die Abbildung genutzt, Restfehler {rms:F1} px");
        used = current;
        return fit;
    }

    private static double Error(Affine fit, (string Town, double X, double Y, double Lon, double Lat) m)
    {
        var p = fit.Apply(m.Lon, m.Lat);
        return Math.Sqrt((p.X - m.X) * (p.X - m.X) + (p.Y - m.Y) * (p.Y - m.Y));
    }

    // Least squares for x = a u + b v + c and y = d u + e v + f.
    private static Affine Solve(List<(string Town, double X, double Y, double Lon, double Lat)> points, double cosLat)
    {
        double[,] n = new double[3, 3];
        double[] rx = new double[3], ry = new double[3];
        foreach (var m in points)
        {
            double[] row = { m.Lon * cosLat, -m.Lat, 1 };
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                    n[i, j] += row[i] * row[j];
                rx[i] += row[i] * m.X;
                ry[i] += row[i] * m.Y;
            }
        }
        double[] x = Gauss((double[,])n.Clone(), rx), y = Gauss((double[,])n.Clone(), ry);
        return new Affine { A = x[0], B = x[1], C = x[2], D = y[0], E = y[1], F = y[2], CosLat = cosLat };
    }

    private static double[] Gauss(double[,] a, double[] b)
    {
        int n = b.Length;
        b = (double[])b.Clone();
        for (int k = 0; k < n; k++)
        {
            int pivot = Enumerable.Range(k, n - k).OrderByDescending(r => Math.Abs(a[r, k])).First();
            for (int j = 0; j < n; j++)
                (a[k, j], a[pivot, j]) = (a[pivot, j], a[k, j]);
            (b[k], b[pivot]) = (b[pivot], b[k]);
            for (int r = k + 1; r < n; r++)
            {
                double factor = a[r, k] / a[k, k];
                for (int j = k; j < n; j++)
                    a[r, j] -= factor * a[k, j];
                b[r] -= factor * b[k];
            }
        }
        double[] x = new double[n];
        for (int r = n - 1; r >= 0; r--)
        {
            double s = b[r];
            for (int j = r + 1; j < n; j++)
                s -= a[r, j] * x[j];
            x[r] = s / a[r, r];
        }
        return x;
    }
    #endregion

    #region Geometry
    internal enum Layer : byte { NeighbourLand = 1, NeighbourBorder = 2, Land = 3, Outline = 4, Region = 5, Subregion = 6 }

    internal sealed class Map
    {
        public readonly Dictionary<Layer, List<List<PointF>>> Layers = new();

        public void Add(Layer layer, List<PointF> shape)
        {
            if (!this.Layers.TryGetValue(layer, out var list))
                this.Layers[layer] = list = new List<List<PointF>>();
            list.Add(shape);
        }

        // "A2GM", version 1, width, height, layer count; per layer: kind, shape count, per shape:
        // point count and points as Int16 in 1/16 pixel.
        public void Write(string file)
        {
            using BinaryWriter w = new(File.Create(file));
            w.Write(Encoding.ASCII.GetBytes("A2GM"));
            w.Write((byte)1);
            w.Write((short)Width);
            w.Write((short)Height);
            w.Write((byte)this.Layers.Count);
            foreach (var layer in this.Layers.OrderBy(l => l.Key))
            {
                w.Write((byte)layer.Key);
                w.Write(layer.Value.Count);
                foreach (List<PointF> shape in layer.Value)
                {
                    w.Write((ushort)shape.Count);
                    foreach (PointF p in shape)
                    {
                        w.Write((short)Math.Round(p.X * Scale));
                        w.Write((short)Math.Round(p.Y * Scale));
                    }
                }
            }
        }
    }

    private static readonly RectangleF Frame = new((float)-Margin, (float)-Margin, (float)(Width + 2 * Margin), (float)(Height + 2 * Margin));

    // Rings of a Polygon/MultiPolygon geometry as longitude/latitude pairs.
    private static IEnumerable<List<(double Lon, double Lat)>> Rings(JsonElement geometry)
    {
        string type = geometry.GetProperty("type").GetString();
        JsonElement coordinates = geometry.GetProperty("coordinates");
        IEnumerable<JsonElement> polygons = type == "Polygon" ? new[] { coordinates } : coordinates.EnumerateArray();
        foreach (JsonElement polygon in polygons)
            foreach (JsonElement ring in polygon.EnumerateArray())
                yield return ring.EnumerateArray().Select(c => (c[0].GetDouble(), c[1].GetDouble())).ToList();
    }

    private static Map BuildMap(Country country, Affine fit, JsonDocument admin0, JsonDocument admin1)
    {
        Map map = new();
        PointF Project((double Lon, double Lat) c) { var p = fit.Apply(c.Lon, c.Lat); return new PointF((float)p.X, (float)p.Y); }

        // Every other country (and for England: Scotland, Northern Ireland) as background land.
        foreach (JsonElement f in admin0.RootElement.GetProperty("features").EnumerateArray())
        {
            JsonElement p = f.GetProperty("properties");
            if (f.GetProperty("geometry").ValueKind == JsonValueKind.Null || Str(p, "ADM0_A3") == country.Iso)
                continue;
            AddBackground(map, Rings(f.GetProperty("geometry")).Select(r => r.Select(Project).ToList()));
        }

        // The country's own units: filled land, and every edge classified by who is on both sides.
        List<(string Region, List<PointF> Ring)> units = new();
        foreach (JsonElement f in admin1.RootElement.GetProperty("features").EnumerateArray())
        {
            JsonElement p = f.GetProperty("properties");
            if (f.GetProperty("geometry").ValueKind == JsonValueKind.Null || Str(p, "adm0_a3") != country.Iso)
                continue;
            bool member = country.Member(p);
            foreach (List<(double Lon, double Lat)> ring in Rings(f.GetProperty("geometry")))
            {
                List<PointF> projected = ring.Select(Project).ToList();
                if (member)
                    units.Add((country.Region(p) ?? "", projected));
                else
                    AddBackground(map, new[] { projected });
            }
        }

        foreach (var unit in units)
        {
            List<PointF> clipped = ClipPolygon(unit.Ring, Frame);
            if (clipped.Count >= 3)
                map.Add(Layer.Land, Simplify(clipped, Tolerance, closed: true));
        }

        // Edge -> regions of the rings using it (vertices of shared borders are identical in Natural Earth).
        static (long, long, long, long) Key(PointF a, PointF b)
        {
            long ax = (long)Math.Round(a.X * 1000), ay = (long)Math.Round(a.Y * 1000), bx = (long)Math.Round(b.X * 1000), by = (long)Math.Round(b.Y * 1000);
            return (ax, ay) .CompareTo((bx, by)) <= 0 ? (ax, ay, bx, by) : (bx, by, ax, ay);
        }
        Dictionary<(long, long, long, long), List<string>> edges = new();
        foreach (var unit in units)
            for (int i = 0; i + 1 < unit.Ring.Count; i++)
            {
                var key = Key(unit.Ring[i], unit.Ring[i + 1]);
                if (!edges.TryGetValue(key, out var regions))
                    edges[key] = regions = new List<string>();
                regions.Add(unit.Region);
            }

        Layer Classify((long, long, long, long) key)
        {
            List<string> regions = edges[key];
            if (regions.Count < 2)
                return Layer.Outline;
            return regions.Distinct().Count() > 1 ? Layer.Region : Layer.Subregion;
        }

        HashSet<(long, long, long, long)> done = new();
        foreach (var unit in units)
        {
            List<PointF> run = null;
            Layer runLayer = Layer.Outline;
            void Flush()
            {
                if (run != null && run.Count >= 2)
                    foreach (List<PointF> part in ClipLine(run, Frame))
                        map.Add(runLayer, Simplify(part, Tolerance, closed: false));
                run = null;
            }
            for (int i = 0; i + 1 < unit.Ring.Count; i++)
            {
                var key = Key(unit.Ring[i], unit.Ring[i + 1]);
                if (!done.Add(key))
                {
                    Flush();
                    continue;
                }
                Layer layer = Classify(key);
                if (run == null || layer != runLayer)
                {
                    Flush();
                    run = new List<PointF> { unit.Ring[i] };
                    runLayer = layer;
                }
                run.Add(unit.Ring[i + 1]);
            }
            Flush();
        }
        return map;
    }

    private static void AddBackground(Map map, IEnumerable<List<PointF>> rings)
    {
        foreach (List<PointF> ring in rings)
        {
            RectangleF bounds = Bounds(ring);
            if (!bounds.IntersectsWith(Frame))
                continue;
            List<PointF> clipped = ClipPolygon(ring, Frame);
            if (clipped.Count >= 3)
                map.Add(Layer.NeighbourLand, Simplify(clipped, Tolerance, closed: true));
            foreach (List<PointF> part in ClipLine(ring, Frame))
                map.Add(Layer.NeighbourBorder, Simplify(part, Tolerance, closed: false));
        }
    }

    private static RectangleF Bounds(List<PointF> points)
    {
        float l = points.Min(p => p.X), t = points.Min(p => p.Y), r = points.Max(p => p.X), b = points.Max(p => p.Y);
        return new RectangleF(l, t, r - l, b - t);
    }

    // Sutherland-Hodgman against the four edges of the frame.
    private static List<PointF> ClipPolygon(List<PointF> ring, RectangleF frame)
    {
        List<PointF> output = ring;
        foreach (var (inside, cut) in new (Func<PointF, bool>, Func<PointF, PointF, PointF>)[]
        {
            (p => p.X >= frame.Left, (a, b) => Cut(a, b, (frame.Left - a.X) / (b.X - a.X))),
            (p => p.X <= frame.Right, (a, b) => Cut(a, b, (frame.Right - a.X) / (b.X - a.X))),
            (p => p.Y >= frame.Top, (a, b) => Cut(a, b, (frame.Top - a.Y) / (b.Y - a.Y))),
            (p => p.Y <= frame.Bottom, (a, b) => Cut(a, b, (frame.Bottom - a.Y) / (b.Y - a.Y))),
        })
        {
            List<PointF> input = output;
            output = new List<PointF>();
            for (int i = 0; i < input.Count; i++)
            {
                PointF current = input[i], previous = input[(i + input.Count - 1) % input.Count];
                if (inside(current))
                {
                    if (!inside(previous))
                        output.Add(cut(previous, current));
                    output.Add(current);
                }
                else if (inside(previous))
                    output.Add(cut(previous, current));
            }
            if (output.Count == 0)
                break;
        }
        return output;
    }

    private static PointF Cut(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    // Pieces of a polyline with at least one end of each segment inside the frame.
    private static IEnumerable<List<PointF>> ClipLine(List<PointF> line, RectangleF frame)
    {
        List<PointF> part = null;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            bool keep = frame.Contains(line[i]) || frame.Contains(line[i + 1]);
            if (keep)
            {
                part ??= new List<PointF> { line[i] };
                part.Add(line[i + 1]);
            }
            else if (part != null)
            {
                yield return part;
                part = null;
            }
        }
        if (part != null)
            yield return part;
    }

    private static List<PointF> Simplify(List<PointF> points, double tolerance, bool closed)
    {
        if (points.Count <= 3)
            return points;
        bool[] keep = new bool[points.Count];
        keep[0] = keep[points.Count - 1] = true;
        Stack<(int, int)> stack = new();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            double max = 0;
            int index = -1;
            for (int i = first + 1; i < last; i++)
            {
                double d = Distance(points[i], points[first], points[last]);
                if (d > max)
                {
                    max = d;
                    index = i;
                }
            }
            if (index >= 0 && max > tolerance)
            {
                keep[index] = true;
                stack.Push((first, index));
                stack.Push((index, last));
            }
        }
        List<PointF> result = points.Where((p, i) => keep[i]).ToList();
        return closed && result.Count < 3 ? points : result;
    }

    private static double Distance(PointF p, PointF a, PointF b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, length = dx * dx + dy * dy;
        if (length == 0)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length));
        double x = a.X + t * dx - p.X, y = a.Y + t * dy - p.Y;
        return Math.Sqrt(x * x + y * y);
    }
    #endregion

    #region Refinement
    // The country's units as longitude/latitude rings, coarsened for the mask comparison.
    private static List<List<(double Lon, double Lat)>> MemberRings(Country country, JsonDocument admin1)
    {
        List<List<(double, double)>> rings = new();
        foreach (JsonElement f in admin1.RootElement.GetProperty("features").EnumerateArray())
        {
            JsonElement p = f.GetProperty("properties");
            if (f.GetProperty("geometry").ValueKind == JsonValueKind.Null || Str(p, "adm0_a3") != country.Iso || !country.Member(p))
                continue;
            foreach (List<(double Lon, double Lat)> ring in Rings(f.GetProperty("geometry")))
            {
                List<(double, double)> coarse = new();
                foreach (var c in ring)
                    if (coarse.Count == 0 || Math.Abs(coarse[^1].Item1 - c.Lon) + Math.Abs(coarse[^1].Item2 - c.Lat) > 0.02)
                        coarse.Add(c);
                if (coarse.Count >= 3)
                    rings.Add(coarse);
            }
        }
        return rings;
    }

    // The brown country shape of the editor's picture (the golden background is much greener).
    private static bool[] Silhouette(Bitmap picture)
    {
        bool[] mask = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Color c = picture.GetPixel(x * picture.Width / Width, y * picture.Height / Height);
                mask[y * Width + x] = c.R > 80 && c.G < 0.62 * c.R && c.B < 0.5 * c.R;
            }
        return mask;
    }

    private static bool[] Render(Affine fit, List<List<(double Lon, double Lat)>> rings, Bitmap canvas)
    {
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.Black);
            using GraphicsPath path = new(FillMode.Winding);
            foreach (var ring in rings)
                path.AddPolygon(ring.Select(c => { var p = fit.Apply(c.Lon, c.Lat); return new PointF((float)p.X, (float)p.Y); }).ToArray());
            g.FillPath(Brushes.White, path);
        }
        var data = canvas.LockBits(new Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        byte[] bytes = new byte[data.Stride * Height];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        canvas.UnlockBits(data);
        bool[] mask = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                mask[y * Width + x] = bytes[y * data.Stride + x * 4] > 127;
        return mask;
    }

    private static double Overlap(bool[] a, bool[] b)
    {
        int both = 0, either = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] && b[i]) both++;
            if (a[i] || b[i]) either++;
        }
        return either == 0 ? 0 : (double)both / either;
    }

    // Nelder-Mead over the six affine parameters, maximising the overlap with the editor's shape.
    private static Affine Refine(Country country, Affine start, Bitmap picture, List<List<(double Lon, double Lat)>> rings)
    {
        bool[] target = Silhouette(picture);
        using Bitmap canvas = new(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Affine Make(double[] v) => new() { A = v[0], B = v[1], C = v[2], D = v[3], E = v[4], F = v[5], CosLat = start.CosLat };
        double Cost(double[] v) => -Overlap(Render(Make(v), rings, canvas), target);

        double[] x0 = { start.A, start.B, start.C, start.D, start.E, start.F };
        double[] step = { start.A * 0.03, start.A * 0.03, 4, start.E * 0.03, start.E * 0.03, 4 };
        List<double[]> simplex = new() { x0 };
        for (int i = 0; i < 6; i++)
        {
            double[] v = (double[])x0.Clone();
            v[i] += step[i];
            simplex.Add(v);
        }
        List<double> costs = simplex.Select(Cost).ToList();
        double before = -costs[0];
        for (int iteration = 0; iteration < 1500; iteration++)
        {
            int[] order = Enumerable.Range(0, 7).OrderBy(i => costs[i]).ToArray();
            simplex = order.Select(i => simplex[i]).ToList();
            costs = order.Select(i => costs[i]).ToList();
            double[] centroid = new double[6];
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    centroid[j] += simplex[i][j] / 6;
            double[] Toward(double t) => centroid.Select((c, j) => c + t * (simplex[6][j] - c)).ToArray();
            double[] reflected = Toward(-1);
            double reflectedCost = Cost(reflected);
            if (reflectedCost < costs[0])
            {
                double[] expanded = Toward(-2);
                double expandedCost = Cost(expanded);
                (simplex[6], costs[6]) = expandedCost < reflectedCost ? (expanded, expandedCost) : (reflected, reflectedCost);
            }
            else if (reflectedCost < costs[5])
                (simplex[6], costs[6]) = (reflected, reflectedCost);
            else
            {
                double[] contracted = Toward(0.5);
                double contractedCost = Cost(contracted);
                if (contractedCost < costs[6])
                    (simplex[6], costs[6]) = (contracted, contractedCost);
                else
                    for (int i = 1; i < 7; i++)
                    {
                        simplex[i] = simplex[i].Select((v, j) => simplex[0][j] + 0.5 * (v - simplex[0][j])).ToArray();
                        costs[i] = Cost(simplex[i]);
                    }
            }
            if (costs.Max() - costs.Min() < 1e-6)
                break;
        }
        int best = Enumerable.Range(0, 7).OrderBy(i => costs[i]).First();
        Console.WriteLine($"{country.Name}: Deckung mit der Editorkarte {before:P1} -> {-costs[best]:P1}");
        return Make(simplex[best]);
    }

    // A resource of the given named type and numeric ID from a PE file (first language).
    private static byte[] PeResource(byte[] pe, string type, int id)
    {
        int nt = BitConverter.ToInt32(pe, 0x3C);
        int sections = BitConverter.ToUInt16(pe, nt + 6), optional = BitConverter.ToUInt16(pe, nt + 20);
        int dataDirs = nt + 24 + (BitConverter.ToUInt16(pe, nt + 24) == 0x20B ? 112 : 96);
        uint resourceRva = BitConverter.ToUInt32(pe, dataDirs + 2 * 8);
        int Offset(uint rva)
        {
            for (int s = 0; s < sections; s++)
            {
                int h = nt + 24 + optional + 40 * s;
                uint va = BitConverter.ToUInt32(pe, h + 12), size = BitConverter.ToUInt32(pe, h + 16), raw = BitConverter.ToUInt32(pe, h + 20);
                if (rva >= va && rva < va + Math.Max(size, BitConverter.ToUInt32(pe, h + 8)))
                    return (int)(raw + rva - va);
            }
            throw new InvalidDataException($"RVA 0x{rva:X} liegt in keinem Abschnitt.");
        }
        int root = Offset(resourceRva);
        IEnumerable<(uint Name, uint Target)> Entries(int dir)
        {
            int count = BitConverter.ToUInt16(pe, dir + 12) + BitConverter.ToUInt16(pe, dir + 14);
            for (int i = 0; i < count; i++)
                yield return (BitConverter.ToUInt32(pe, dir + 16 + 8 * i), BitConverter.ToUInt32(pe, dir + 20 + 8 * i));
        }
        string NameOf(uint name)
        {
            int at = root + (int)(name & 0x7FFFFFFF);
            return Encoding.Unicode.GetString(pe, at + 2, BitConverter.ToUInt16(pe, at) * 2);
        }
        var typeEntry = Entries(root).First(e => (e.Name & 0x80000000) != 0 && NameOf(e.Name) == type);
        var idEntry = Entries(root + (int)(typeEntry.Target & 0x7FFFFFFF)).First(e => e.Name == id);
        var languageEntry = Entries(root + (int)(idEntry.Target & 0x7FFFFFFF)).First();
        int dataEntry = root + (int)languageEntry.Target;
        int offset = Offset(BitConverter.ToUInt32(pe, dataEntry));
        return pe.Skip(offset).Take(BitConverter.ToInt32(pe, dataEntry + 4)).ToArray();
    }
    #endregion

    #region Preview
    // The map over the editor's own picture, with each used town (red: club position, blue: projected).
    private static void Preview(Map map, List<(string Town, double X, double Y, double Lon, double Lat)> used, Affine fit, Bitmap picture, string file)
    {
        const int zoom = 2;
        using Bitmap bitmap = new(Width * zoom, Height * zoom);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage(picture, 0, 0, Width * zoom, Height * zoom);
        g.ScaleTransform(zoom, zoom);
        foreach (var layer in map.Layers)
        {
            Color color = layer.Key switch
            {
                Layer.NeighbourBorder => Color.FromArgb(160, 90, 90, 90),
                Layer.Outline => Color.Black,
                Layer.Region => Color.FromArgb(200, 0, 0, 160),
                Layer.Subregion => Color.FromArgb(160, 0, 120, 0),
                _ => Color.Empty
            };
            if (color.IsEmpty)
                continue;
            using Pen pen = new(color, layer.Key == Layer.Outline ? 1f : 0.5f);
            foreach (List<PointF> line in layer.Value)
                g.DrawLines(pen, line.ToArray());
        }
        foreach (var m in used)
        {
            var p = fit.Apply(m.Lon, m.Lat);
            g.DrawLine(Pens.Magenta, (float)m.X, (float)m.Y, (float)p.X, (float)p.Y);
            g.FillEllipse(Brushes.Red, (float)m.X - 1.5f, (float)m.Y - 1.5f, 3, 3);
            g.FillEllipse(Brushes.Blue, (float)p.X - 1f, (float)p.Y - 1f, 2, 2);
        }
        bitmap.Save(file);
    }
    #endregion
}
