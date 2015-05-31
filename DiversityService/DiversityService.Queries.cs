

namespace DiversityService
{
    using DiversityORM;
    using DiversityPhone.Model;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Splat;
    using System.Diagnostics.Contracts;
    using global::DiversityService.Model;

    public partial class DiversityService
    {
        private const int PAGE_SIZE = 1000;

        private const string REPOSITORY_PUBLICTAXA = "PublicTaxa";

        private const string DIVERSITYMODULE_TAXONNAMES = "DiversityTaxonNames";

        // DB Function Names
        private const string FUNCTION_DM_TAXONLISTS4U = "DiversityMobile_TaxonListsForUser";
        private const string FUNCTION_DIVERSITYMODULE = "DiversityWorkbenchModule";

        // Cache Tags
        private const string CACHE_MODULES = "MODULES";
        private const string CACHE_TAXON_LISTS = "TAXONLISTS";

        private readonly TimeSpan CACHE_TTL = TimeSpan.FromMinutes(5);

        private DateTimeOffset getCacheExpiration()
        {
            return DateTimeOffset.Now.Add(CACHE_TTL);
        }

        /// <summary>
        /// Gets a map containing all DiversityWorkbenchModule Databases 
        /// indexed by type (i.e. DiversityCollection, DiversityTaxonNames etc.)
        /// </summary>
        /// <remarks>
        /// Internally caches the result of the query to avoid iterating through 
        /// all Databases each time the method is called.
        /// there is a cache entry for each value of the tuple (serverId, userId)
        /// </remarks>
        /// <returns></returns>
        private IDictionary<string, IEnumerable<string>> getDiversityModules(Diversity db, string serverId, string userId)
        {
            Contract.Requires(db != null);
            Contract.Requires(!string.IsNullOrWhiteSpace(serverId));
            
            // Check the cache
            var cacheKey = string.Format("{0}_{1}_{2}", serverId, userId, CACHE_MODULES);
            var cached = Cache.Get(cacheKey) as IDictionary<string, IEnumerable<string>>;

            if (cached != null)
            {
                return cached;
            }

            // Get Databases
            IEnumerable<string> databases = Enumerable.Empty<string>();
            try
            {
                databases = db.Query<string>("SELECT name FROM sys.databases").ToList();
            }
            catch(Exception ex)
            {
                this.Log().ErrorException(string.Format("Selecting Databases from Server: {0}", serverId), ex);
                throw; // Stop further processing
            }

            var modules = new Dictionary<string, IEnumerable<string>>();
                       
            foreach (var dbName in databases)
            {
                try
                {
                    // Check if we have access to the DB

                    var hasAccess = hasAccessToDB(db, dbName);

                    if (!hasAccess)
                    {
                        this.Log().Debug("({0}) {1} cannot access {2} on server {3}", nameof(getDiversityModules), userId, dbName, serverId);
                        continue;
                    }


                    // Check if the DiversityWorkbenchModule Function exists
                    var isModule = functionExists(db, dbName, FUNCTION_DIVERSITYMODULE);
                    
                    if (!isModule)
                    {
                        this.Log().Debug("({0}) database {1} on {2} is not a module, skipping", nameof(getDiversityModules), dbName, serverId);
                        continue;
                    }

                    var moduleType = db.ExecuteScalar<string>("SELECT [" + dbName + "].[dbo].["+ FUNCTION_DIVERSITYMODULE + "]()");

                    if (!string.IsNullOrWhiteSpace(moduleType))
                    {
                        IEnumerable<string> moduleList;

                        // Get existing List or create a new one for this type
                        if(!modules.TryGetValue(moduleType, out moduleList))
                        {
                            moduleList = modules[moduleType] = new List<string>();
                        }

                        (moduleList as List<string>).Add(dbName);
                    }
                }
                catch (Exception ex)
                {
                    this.Log().ErrorException("Finding Modules", ex);
                }
            }

            // Cache the result
            Cache.Add(cacheKey, modules, getCacheExpiration());

            return modules;
        }

        private bool hasAccessToDB(Diversity db, string dbName)
        {
            return db.ExecuteScalar<int?>("SELECT HAS_DBACCESS(@0)", dbName) == 1;
        }

        private bool functionExists(Diversity db, string dbName, string functionName)
        {
            return db.ExecuteScalar<int>(
                        string.Format(@"
                        SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END 
                        FROM   [{0}].[sys].[objects]
                        WHERE  object_id = OBJECT_ID(N'[{0}].[dbo].[{1}]')
                        AND type IN(N'FN', N'IF', N'TF', N'FS', N'FT')",dbName, functionName)) != 0;
        }

        private IEnumerable<TaxonList> enumerateTaxonListsFromServer(Diversity db, string repository, string loginName)
        {
            var result = new List<TaxonList>();

            var modulesByType = getDiversityModules(db, repository, loginName);

            IEnumerable<string> dtnInstances;

            if (modulesByType.TryGetValue(DIVERSITYMODULE_TAXONNAMES, out dtnInstances))
            {
                foreach (var dtn in dtnInstances)
                {
                    var hasDMFunction = functionExists(db, dtn, FUNCTION_DM_TAXONLISTS4U);

                    if (!hasDMFunction)
                    {
                        this.Log().Warn("({0}) Database {1} on server {2} reports to be a {3} module but does not have {4}",
                            nameof(enumerateTaxonListsFromServer),
                            dtn,
                            repository,
                            DIVERSITYMODULE_TAXONNAMES,
                            FUNCTION_DM_TAXONLISTS4U);
                        continue;
                    }

                    var lists = taxonListsForUser(dtn, loginName, db)
                        .Select(l => 
                        {
                            l.Catalog = dtn;
                            return l;
                        }).Where(l =>
                        {
                            // Validate results from The DB
                            // And filter garbage
                            if (string.IsNullOrWhiteSpace(l.DisplayText) ||
                                string.IsNullOrWhiteSpace(l.TaxonomicGroup) ||
                                l.Id == 0)
                            {
                                this.Log().Warn("({0}) Invalid taxon list '{1}' ({3}) in {4} on server {2} encountered", nameof(enumerateTaxonListsFromServer), l.DisplayText, repository, l.Id, dtn);
                                return false;
                            }
                            return true;
                        });

                    result.AddRange(lists);
                }
            }

            return result;
        }

        private static IEnumerable<AnalysisTaxonomicGroup> analysisTaxonomicGroupsForProject(int projectID, Diversity db)
        {
            return db.Query<AnalysisTaxonomicGroup>("FROM [DiversityMobile_AnalysisTaxonomicGroupsForProject](@0) AS [AnalysisTaxonomicGroup]", projectID);
        }

        private static IEnumerable<TaxonList> taxonListsForUser(string catalog, string loginName, Diversity db)
        {
            return db.Query<TaxonList>(
                string.Format("SELECT * FROM [{0}].[dbo].[{1}](@0) AS [TaxonList]", catalog, FUNCTION_DM_TAXONLISTS4U),
                loginName);
        }

        private static IEnumerable<TaxonName> taxaFromListAndPage(TaxonList list, int page, Diversity db)
        {
            return db.Page<TaxonName>(page, PAGE_SIZE, "FROM [DiversityMobile_TaxonList](@0) AS [TaxonName]", list.Id).Items;
        }

        private static IEnumerable<Analysis> analysesForProject(int projectID, Diversity db)
        {
            return db.Query<Analysis>("FROM [DiversityMobile_AnalysisProjectList](@0) AS [Analysis]", projectID);
        }

        private static IEnumerable<AnalysisResult> analysisResultsForProject(int projectID, Diversity db)
        {
            return db.Query<AnalysisResult>("FROM [DiversityMobile_AnalysisResultForProject](@0) AS [AnalysisResult]", projectID);
        }

        private static IEnumerable<PropertyList> propertyListsForUser(UserCredentials login, Diversity db)
        {
            return db.Query<PropertyList>("FROM [DiversityMobile_TermsListsForUser](@0) AS [PropertyList]", login.LoginName);
        }

        private static IEnumerable<PropertyValue> propertyValuesFromList(int propertyId, int page, Diversity db)
        {
            return db.Page<PropertyValue>(page, PAGE_SIZE, "FROM [DiversityMobile_TermsList](@0) AS [PropertyValue]", propertyId).Items;
        }

        private static IEnumerable<Qualification> getQualifications(Diversity db)
        {
            return db.Query<Qualification>("FROM [DiversityMobile_IdentificationQualifiers]() AS [Qualification]");
        }

        private static Event getEvent(Diversity db, int DiversityCollectionID)
        {
            Event ev = db.SingleOrDefault<Event>("FROM CollectionEvent WHERE CollectionEventID=@0", DiversityCollectionID);
            IEnumerable<CollectionEventLocalisation> cel_List = getLocalisationForEvent(db, DiversityCollectionID);
            foreach (CollectionEventLocalisation cel in cel_List)
            {
                if (cel.LocalisationSystemID == 4)
                    try
                    {
                        ev.Altitude = double.Parse(cel.Location1);
                    }
                    catch (Exception) { ev.Altitude = null; }
                if (cel.LocalisationSystemID == 8)
                {
                    try
                    {
                        ev.Longitude = double.Parse(cel.Location1);
                        ev.Latitude = double.Parse(cel.Location2);
                    }
                    catch (Exception) { ev.Longitude = null; ev.Latitude = null; }
                }
            }
            return ev;
        }

        private static IEnumerable<CollectionEventLocalisation> getLocalisationForEvent(Diversity db, int DiversityCollectionID)
        {
            return db.Query<CollectionEventLocalisation>("Select LocalisationSystemID, Location1, Location2 FROM CollectionEventLocalisation WHERE CollectionEventID=@0", DiversityCollectionID);
        }

        private static IEnumerable<EventProperty> getCollectionPropertyForEvent(Diversity db, int DiversityCollectionID)
        {
            return db.Query<EventProperty>("Select CollectionEventID, PropertyID, DisplayText,PropertyURI FROM CollectionEventProperty WHERE CollectionEventID=@0", DiversityCollectionID);
        }

        private static IEnumerable<Specimen> getSpecimenForEvent(Diversity db, int DiversityCollectionID)
        {
            return db.Query<Specimen>("Select CollectionSpecimenID,CollectionEventID, DepositorsAccessionNumber FROM CollectionSpecimen WHERE CollectionEventID=@0", DiversityCollectionID);
        }

        private static IdentificationUnitGeoAnalysis getGeoAnalysisForIU(Diversity db, int DiversityCollectionID)
        {
            //Attention: No Geodata
            return db.SingleOrDefault<IdentificationUnitGeoAnalysis>("Select IdentificationUnitID,CollectionSpecimenID,AnalysisDate From IdentificationUnitGeoAnalysis WHERE IdentificationUnitID=@0", DiversityCollectionID);
        }
    }
}