using DiversityPhone.Model;
using DiversityService.Model;
using PetaPoco;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiversityService
{
    public partial class DiversityService
    {
        public int InsertEventSeries(EventSeries series, IEnumerable<Localization> localizations, UserCredentials login)
        {
            using (var db = login.GetConnection())
            using (var t = new Transaction(db))
            {
                db.Insert(series);

                var geoString = SerializeLocalizations(localizations);
                if (!string.IsNullOrWhiteSpace(geoString))
                    db.Execute("UPDATE [dbo].[CollectionEventSeries] SET geography=GEOGRAPHY::STGeomFromText(@0, 4326) WHERE SeriesID=@1", geoString, series.CollectionEventSeriesID);

                t.Complete();
                return series.CollectionEventSeriesID;
            }
        }

        public int InsertEvent(Event ev, IEnumerable<EventProperty> properties, UserCredentials login)
        {
            using (var db = login.GetConnection())
            using (var t = new Transaction(db))
            {
                db.Insert(ev);

                var geoString = SerializeLocalization(ev.Latitude, ev.Longitude, ev.Altitude);
                foreach (var loc in ev.GetLocalisations(login))
                {
                    db.Insert(loc);

                    if (!string.IsNullOrWhiteSpace(geoString))
                    {
                        db.Execute("UPDATE [dbo].[CollectionEventLocalisation] SET geography=GEOGRAPHY::STGeomFromText(@0, 4326) WHERE CollectionEventID=@1 AND LocalisationSystemID=@2", geoString, loc.CollectionEventID, loc.LocalisationSystemID);
                    }
                }

                if (properties != null)
                    foreach (var p in properties)
                    {
                        p.CollectionEventID = ev.CollectionEventID;
                        db.Insert(p);
                    }

                t.Complete();

                return ev.CollectionEventID;
            }
        }

        public int InsertSpecimen(Specimen s, UserCredentials login)
        {
            using (var db = login.GetConnection())
            using (var t = new Transaction(db))
            {
                db.Insert(s);

                db.Insert(s.GetProject(login.ProjectID));
                db.Insert(s.GetAgent(login));

                t.Complete();
                return s.CollectionSpecimenID;
            }
        }

        public int InsertIdentificationUnit(IdentificationUnit iu, IEnumerable<IdentificationUnitAnalysis> analyses, UserCredentials login)
        {
            using (var db = login.GetConnection())
            using (var t = db.GetTransaction())
            {
                db.Insert(iu);
                db.Insert(iu.GetIdentification(login));

                db.Insert(iu.GetGeoAnalysis(login));

                var geoString = SerializeLocalization(iu.Latitude, iu.Longitude, iu.Altitude);
                if (!string.IsNullOrWhiteSpace(geoString))
                {
                    db.Execute("UPDATE [dbo].[IdentificationUnitGeoAnalysis] SET Geography=GEOGRAPHY::STGeomFromText(@0, 4326) WHERE CollectionSpecimenID=@1 AND IdentificationUnitID=@2", geoString, iu.CollectionSpecimenID, iu.CollectionUnitID);
                }

                if (analyses != null)
                    foreach (var a in analyses)
                    {
                        a.CollectionUnitID = iu.CollectionUnitID;
                        a.CollectionSpecimenID = iu.CollectionSpecimenID;
                        db.Insert(a);
                    }

                t.Complete();

                return iu.CollectionUnitID;
            }
        }

        public void InsertMMO(MultimediaObject mmo, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                try
                {
                    switch (mmo.OwnerType)
                    {
                        case MultimediaOwner.EventSeries:
                            CollectionEventSeriesImage cesi = mmo.ToSeriesImage();
                            db.Insert(cesi);
                            break;

                        case MultimediaOwner.Event:
                            CollectionEventImage cei = mmo.ToEventImage();
                            db.Insert(cei);
                            break;

                        case MultimediaOwner.Specimen:
                        case MultimediaOwner.IdentificationUnit:
                            CollectionSpecimenImage csi = mmo.ToSpecimenImage(db);
                            db.Insert(csi);
                            break;

                        default:
                            throw new ArgumentException("unknown type");
                    }
                }
                catch (SqlException)
                {
                    // Presumably Multiple Insert, meaning the object already exists
                    // -> Success
                }
            }
        }

        private static string SerializeLocalizations(IEnumerable<Localization> locs)
        {
            // The points in the LineString must be unique
            var uniqueLocs = (locs != null)
                ? locs.Distinct().ToList()
                : new List<Localization>();

            if (uniqueLocs.Count > 1)
            {
                var cult = new CultureInfo("en-US");
                return string.Format("LINESTRING({0})",
                        string.Join(", ", uniqueLocs.Select(gp => string.Format(cult, "{0:R} {1:R}", gp.Longitude, gp.Latitude)))
                    );
            }
            else return string.Empty;
        }

        private static string SerializeLocalization(double? latitude, double? longitude, double? altitude)
        {
            if (!latitude.HasValue || !longitude.HasValue ||
                double.IsNaN(latitude.Value) || double.IsNaN(longitude.Value))
                return string.Empty;

            var cult = new CultureInfo("en-US");
            string longitudeStr = longitude.Value.ToString("R", cult);

            string latStr = latitude.Value.ToString("R", cult);
            latStr = latStr.Replace(',', '.');

            StringBuilder builder = new StringBuilder("POINT(");
            builder.Append(longitudeStr);
            builder.Append(" ");
            builder.Append(latStr);
            if (altitude.HasValue && double.IsNaN((double)altitude) == false)
            {
                string altStr = altitude.Value.ToString("R", cult);
                altStr = altStr.Replace(',', '.');
                builder.Append(" ");
                builder.Append(altStr);
            }
            builder.Append(")");
            string s = builder.ToString();
            return builder.ToString();
        }
    }
}