using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace A2G_Trainer_XP.View
{
    /// <summary>
    /// A country of the five leagues as drawn on the "Landkarte" tab, in the pixel grid of the data
    /// editor's map pictures (440 x 320), in which the clubs' map positions are stored. Built from
    /// Natural Earth data by scripts/MapData (format described there) and embedded as
    /// Resources/Maps/*.map.
    /// </summary>
    internal sealed class CountryMap
    {
        /// <summary>What a shape of the map is.</summary>
        internal enum Layer : byte
        {
            /// <summary>Land of another country (closed ring).</summary>
            NeighbourLand = 1,
            /// <summary>Coast or border of another country (line).</summary>
            NeighbourBorder = 2,
            /// <summary>Land of the country (closed ring).</summary>
            Land = 3,
            /// <summary>The country's coast and border (line).</summary>
            Outline = 4,
            /// <summary>Border between first-level regions: Bundesländer, régions, regioni, comunidades, English regions (line).</summary>
            Region = 5,
            /// <summary>Border between second-level units: départements, province, provincias, counties (line).</summary>
            Subregion = 6
        }

        /// <summary>Width of the editor's map in pixels.</summary>
        public int Width { get; }
        /// <summary>Height of the editor's map in pixels.</summary>
        public int Height { get; }
        private readonly Dictionary<Layer, List<PointF[]>> layers = new Dictionary<Layer, List<PointF[]>>();

        /// <summary>The shapes of a layer in map pixels; false if the map has none.</summary>
        public bool TryGetLayer(Layer layer, out List<PointF[]> shapes) => this.layers.TryGetValue(layer, out shapes);

        private static readonly Dictionary<PlayerEnums.Country, string> Files = new Dictionary<PlayerEnums.Country, string>
        {
            { PlayerEnums.Country.Deutschland, "DEU" },
            { PlayerEnums.Country.England, "GBR" },
            { PlayerEnums.Country.Frankreich, "FRA" },
            { PlayerEnums.Country.Italien, "ITA" },
            { PlayerEnums.Country.Spanien, "ESP" },
        };
        private static readonly Dictionary<PlayerEnums.Country, CountryMap> Loaded = new Dictionary<PlayerEnums.Country, CountryMap>();

        private CountryMap(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>The map of the country, or null if there is none (only the five league countries have one).</summary>
        public static CountryMap For(PlayerEnums.Country country)
        {
            lock (Loaded)
            {
                if (Loaded.TryGetValue(country, out CountryMap map))
                    return map;
                map = null;
                if (Files.TryGetValue(country, out string file))
                    using (Stream stream = typeof(CountryMap).Assembly.GetManifestResourceStream($"A2G_Trainer_XP.Resources.Maps.{file}.map"))
                        map = stream != null ? Read(stream) : null;
                Loaded[country] = map;
                return map;
            }
        }

        /// <summary>Reads a map in the format of scripts/MapData; throws <see cref="InvalidDataException"/> for anything else.</summary>
        internal static CountryMap Read(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "A2GM" || reader.ReadByte() != 1)
                    throw new InvalidDataException("Keine Kartendatei (A2GM, Version 1).");
                CountryMap map = new CountryMap(reader.ReadInt16(), reader.ReadInt16());
                int layerCount = reader.ReadByte();
                for (int l = 0; l < layerCount; l++)
                {
                    Layer layer = (Layer)reader.ReadByte();
                    int shapeCount = reader.ReadInt32();
                    List<PointF[]> shapes = new List<PointF[]>(shapeCount);
                    for (int s = 0; s < shapeCount; s++)
                    {
                        PointF[] points = new PointF[reader.ReadUInt16()];
                        for (int p = 0; p < points.Length; p++)
                            points[p] = new PointF(reader.ReadInt16() / 16f, reader.ReadInt16() / 16f);
                        shapes.Add(points);
                    }
                    map.layers[layer] = shapes;
                }
                return map;
            }
        }
    }
}
