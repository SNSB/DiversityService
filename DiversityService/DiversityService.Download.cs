using DiversityORM;
using DiversityPhone.Model;
using DiversityService.Model;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;

namespace DiversityService
{
    public partial class DiversityService : IDiversityService
    {
        public IEnumerable<EventSeries> EventSeriesByQuery(string query, UserCredentials login)
        {
            // substring match against series code
            query = string.Format("%{0}%", query);

            using (var db = login.GetConnection())
            {
                return db.Query<EventSeries>("WHERE [SeriesCode] LIKE @0", query);
            }
        }

        public EventSeries EventSeriesByID(int collectionSeriesID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                return db.Single<EventSeries>(collectionSeriesID);
            }
        }

        private static IEnumerable<Localization> EnumeratePoints(SqlGeography geo)
        {
            var pointCount = geo.STNumPoints().Value;
            for (int i = 1; i <= pointCount; ++i)
            {
                var pt = geo.STPointN(i);
                yield return new Localization()
                {
                    Altitude = (pt.HasZ) ? pt.Z.Value : null as double?,
                    Longitude = pt.Long.Value,
                    Latitude = pt.Lat.Value
                };
            }
        }

        public IEnumerable<Localization> LocalizationsForSeries(int collectionSeriesID, UserCredentials login)
        {
            try
            {
                using (var db = login.GetConnection())
                {
                    var sql =
                    new PetaPoco.Sql()
                            .Select("[Geography]")
                            .From("[CollectionEventSeries]")
                            .Where("[SeriesID] = @0", collectionSeriesID);

                    var geo = db
                        .ExecuteScalar<SqlGeography>(
                        sql
                        );

                    return EnumeratePoints(geo).ToList();
                }
            }
            catch (Exception)
            {
            }
            return Enumerable.Empty<Localization>();
        }

        public IEnumerable<Event> EventsByLocality(string locality, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var events = db.Query<Event>("FROM [dbo].[DiversityMobile_EventsForProject] (@0, @1) as [CollectionEvent]", login.ProjectID, locality).Take(50).ToList();

                foreach (var ev in events)
                {
                    AddLocalization(ev, db);
                }

                return events;
            }
        }

        public IEnumerable<EventProperty> PropertiesForEvent(int collectionEventID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                return db.Query<EventProperty>("WHERE CollectionEventID=@0", collectionEventID).ToList();
            }
        }

        public IEnumerable<Specimen> SpecimenForEvent(int collectionEventID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                return db.Query<Specimen>("WHERE CollectionEventID=@0", collectionEventID).ToList();
            }
        }

        public IEnumerable<IdentificationUnit> UnitsForSpecimen(int collectionSpecimenID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var ius = db.Query<IdentificationUnit>("WHERE CollectionSpecimenID=@0", collectionSpecimenID).ToList();

                foreach (var iu in ius)
                {
                    AddUnitExternalInformation(iu, db);
                }
                return ius;
            }
        }

        public IEnumerable<IdentificationUnit> SubUnitsForIU(int collectionUnitID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var ius = db.Query<IdentificationUnit>("WHERE RelatedUnitID=@0", collectionUnitID).ToList();

                foreach (var iu in ius)
                {
                    AddUnitExternalInformation(iu, db);
                }

                return ius;
            }
        }

        private void AddUnitExternalInformation(IdentificationUnit iu, Diversity db)
        {
            var id = db.SingleOrDefault<Identification>("WHERE [CollectionSpecimenID]=@0 AND [IdentificationUnitID]=@1", iu.CollectionSpecimenID, iu.CollectionUnitID);
            if (id != null)
            {
                iu.IdentificationUri = id.NameURI;
                iu.LastIdentificationCache = id.TaxonomicName;
                iu.Qualification = id.IdentificationQualifier;
                iu.AnalysisDate = (id.IdentificationYear.HasValue && id.IdentificationMonth.HasValue && id.IdentificationDay.HasValue)
                    ? new DateTime(id.IdentificationYear.Value, id.IdentificationMonth.Value, id.IdentificationDay.Value, 0, 0, 0)
                    : DateTime.Now;
            }

            AddLocalization(iu, db);
        }

        private void AddLocalization(IdentificationUnit iu, Diversity db)
        {
            // The decimal -> double dance ensures the coordinates round trip correctly
            iu.Altitude = DecimalToDouble(db.SingleOrDefault<decimal?>("SELECT CAST([Geography].Z AS DECIMAL(25, 20)) FROM [IdentificationUnitGeoAnalysis] WHERE [CollectionSpecimenID]=@0 AND [IdentificationUnitID]=@1", iu.CollectionSpecimenID, iu.CollectionUnitID));
            var lat = db.SingleOrDefault<decimal?>("SELECT CAST([Geography].Lat AS DECIMAL(25, 20)) FROM [IdentificationUnitGeoAnalysis] WHERE [CollectionSpecimenID]=@0 AND [IdentificationUnitID]=@1", iu.CollectionSpecimenID, iu.CollectionUnitID);
            iu.Latitude = DecimalToDouble(lat);
            var lon = db.SingleOrDefault<decimal?>("SELECT CAST([Geography].Long AS DECIMAL(25, 20)) FROM [IdentificationUnitGeoAnalysis] WHERE [CollectionSpecimenID]=@0 AND [IdentificationUnitID]=@1", iu.CollectionSpecimenID, iu.CollectionUnitID);
            iu.Longitude = DecimalToDouble(lon);
        }

        private void AddLocalization(Event ev, Diversity db)
        {
            // The decimal -> double dance ensures the coordinates round trip correctly
            ev.Altitude = DecimalToDouble(db.SingleOrDefault<decimal?>("SELECT CAST([AverageAltitudeCache] AS DECIMAL(25, 20)) FROM [CollectionEventLocalisation] WHERE [CollectionEventID]=@0 AND [LocalisationSystemID]=@1", ev.CollectionEventID, ClientServiceConversions.ALTITUDE_LOC_SYS_ID));
            var lat = db.SingleOrDefault<decimal?>("SELECT CAST([AverageLatitudeCache] AS DECIMAL(25, 20)) FROM [CollectionEventLocalisation] WHERE [CollectionEventID]=@0 AND [LocalisationSystemID]=@1", ev.CollectionEventID, ClientServiceConversions.WGS84_LOC_SYS_ID);
            ev.Latitude = DecimalToDouble(lat);
            var lon = db.SingleOrDefault<decimal?>("SELECT CAST([AverageLongitudeCache] AS DECIMAL(25, 20)) FROM [CollectionEventLocalisation] WHERE [CollectionEventID]=@0 AND [LocalisationSystemID]=@1", ev.CollectionEventID, ClientServiceConversions.WGS84_LOC_SYS_ID);
            ev.Longitude = DecimalToDouble(lon);
        }

        public IEnumerable<IdentificationUnitAnalysis> AnalysesForIU(int collectionUnitID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var analyses = db.Query<IdentificationUnitAnalysis>("WHERE IdentificationUnitID=@0", collectionUnitID).ToList();

                return analyses;
            }
        }

        private double? DecimalToDouble(decimal? val)
        {
            const decimal PreciseOne = 1.000000000000000000000000000000000000000000000000m;

            if (val.HasValue)
            {
                return Convert.ToDouble(val.Value / PreciseOne);
            }
            return null;
        }
    }
}