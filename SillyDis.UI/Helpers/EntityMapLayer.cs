using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using SillyDis.Core.Models;

namespace SillyDis.UI.Helpers
{
    /// <summary>
    /// A Mapsui MemoryLayer that stays in sync with a live ObservableCollection
    /// of EntityTrack objects. Rebuilds features whenever the collection changes
    /// and applies force-specific symbology.
    /// </summary>
    public class EntityMapLayer : MemoryLayer
    {
        private readonly System.Collections.ObjectModel.ObservableCollection<EntityTrack> _tracks;

        // Force ID → symbol color
        private static readonly Color FriendlyColor  = new(0,   180, 255); // blue
        private static readonly Color OpposingColor  = new(255,  68,  68); // red
        private static readonly Color NeutralColor   = new(100, 220, 100); // green
        private static readonly Color UnknownColor   = new(180, 180, 180); // grey

        private static readonly Color StaleOverlay   = new(60, 60, 60, 120); // dim overlay

        public EntityMapLayer(
            System.Collections.ObjectModel.ObservableCollection<EntityTrack> tracks)
            : base("Entities")
        {
            _tracks = tracks;
            _tracks.CollectionChanged += OnTracksChanged;

            // Watch individual track property changes via a simple polling approach
            // (full INotifyPropertyChanged wiring would need a composite collection helper)
            Rebuild();
        }

        private void OnTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Rebuild();
        }

        /// <summary>
        /// Rebuilds all PointFeature instances from the current EntityTrack collection.
        /// Called on the UI thread via the Dispatcher in SimulationSession.
        /// </summary>
        public void Rebuild()
        {
            var features = new List<IFeature>(_tracks.Count);

            foreach (var track in _tracks)
            {
                if (track.Latitude == 0 && track.Longitude == 0) continue;

                // Convert geodetic → spherical mercator for Mapsui
                var (mx, my) = SphericalMercator.FromLonLat(track.Longitude, track.Latitude);
                var feature  = new PointFeature(mx, my);

                feature["EntityId"]      = track.EntityId;
                feature["EntityType"]    = track.EntityTypeName;
                feature["ForceId"]       = track.ForceIdName;
                feature["Altitude"]      = $"{track.AltitudeMetres:F0} m";
                feature["LastSeen"]      = track.LastSeen.ToString("HH:mm:ss");

                var color  = ForceColor(track.ForceId);
                var radius = track.IsStale ? 6 : 9;
                var opacity = track.IsStale ? 0.35f : 1.0f;

                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType   = SymbolType.Ellipse,
                    Fill         = new Brush(color),
                    Outline      = new Pen(Color.White, track.IsStale ? 0.5f : 1.5f),
                    SymbolScale  = radius / 10.0,
                    Opacity      = opacity
                });

                features.Add(feature);
            }

            Features = features;
        }

        private static Color ForceColor(byte forceId) => forceId switch
        {
            1 => FriendlyColor,
            2 => OpposingColor,
            3 => NeutralColor,
            _ => UnknownColor
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _tracks.CollectionChanged -= OnTracksChanged;
            base.Dispose(disposing);
        }
    }
}
