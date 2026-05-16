using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SillyDis.Core.Models
{
    /// <summary>
    /// Represents a live entity being tracked on the tactical map.
    /// Updated in-place by each incoming EntityStatePdu from the same entity.
    /// </summary>
    public partial class EntityTrack : ObservableObject
    {
        /// <summary>Site.Application.Entity formatted string — serves as the unique key.</summary>
        [ObservableProperty] private string _entityId = string.Empty;

        /// <summary>SISO-resolved platform description, e.g. "M1A2 Abrams (USA, Land)".</summary>
        [ObservableProperty] private string _entityTypeName = string.Empty;

        /// <summary>SISO-resolved force affiliation.</summary>
        [ObservableProperty] private string _forceIdName = string.Empty;

        /// <summary>Raw force ID byte for symbol colour selection (1=Friendly, 2=Opposing, 3=Neutral).</summary>
        [ObservableProperty] private byte _forceId;

        /// <summary>Geodetic latitude in decimal degrees.</summary>
        [ObservableProperty] private double _latitude;

        /// <summary>Geodetic longitude in decimal degrees.</summary>
        [ObservableProperty] private double _longitude;

        /// <summary>Altitude above WGS-84 ellipsoid in metres.</summary>
        [ObservableProperty] private double _altitudeMetres;

        /// <summary>True heading in degrees (0–360).</summary>
        [ObservableProperty] private double _headingDeg;

        /// <summary>Wall-clock time of the most recent EntityStatePdu for this entity.</summary>
        [ObservableProperty] private DateTime _lastSeen = DateTime.Now;

        /// <summary>True if no ESPDU has been received in the last 5 seconds.</summary>
        [ObservableProperty] private bool _isStale;

        /// <summary>
        /// Updates position and metadata from a freshly decoded EntityStatePdu item.
        /// </summary>
        public void Update(PduItem item, double lat, double lon, double alt)
        {
            EntityTypeName = item.EntityTypeName;
            ForceIdName    = item.ForceIdName;
            ForceId        = item.ForceId;
            Latitude       = lat;
            Longitude      = lon;
            AltitudeMetres = alt;
            LastSeen       = item.Timestamp;
            IsStale        = false;
        }
    }
}
