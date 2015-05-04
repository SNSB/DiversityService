using DiversityORM;
using DiversityPhone.Model;
using DiversityService.Configuration;
using DiversityService.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiversityService
{
    internal static class CredentialsExtensions
    {
        public static Diversity GetConnection(this UserCredentials This, string repository = null)
        {
            DiversityServiceConfiguration.Login l = This;
            var repo = Configuration.ServiceConfiguration.RepositoryByName(This.Repository);
            return new Diversity(l, repo.Server, repository ?? repo.Catalog);
        }
    }

    public partial class DiversityService : IDiversityService
    {
        static DiversityService()
        {
            SqlServerTypes.Utilities.LoadNativeAssemblies(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin"));
        }

        #region Get

        public IEnumerable<Term> GetStandardVocabulary(UserCredentials login)
        {
            IEnumerable<Term> linqTerms;
            using (var db = login.GetConnection())
            {
                linqTerms =
                Enumerable.Concat(
                    db.Query<Term>("FROM [dbo].[DiversityMobile_TaxonomicGroups]() as Term")
                    .Select(t => { t.Source = DiversityPhone.Model.TermList.TaxonomicGroups; return t; }),
                    db.Query<Term>("FROM [dbo].[DiversityMobile_UnitRelationTypes]() as Term")
                    .Select(t => { t.Source = DiversityPhone.Model.TermList.RelationshipTypes; return t; })
                    ).ToList();
            }
            return linqTerms;
        }

        public IEnumerable<Project> GetProjectsForUser(UserCredentials login)
        {
            if (string.IsNullOrWhiteSpace(login.Repository))
                return Enumerable.Empty<Project>();
            using (var db = login.GetConnection())
            {
                try
                {
                    return db.Query<Project>("FROM [dbo].[DiversityMobile_ProjectList] () AS [Project]")
                        .Select(p =>
                            {
                                p.DisplayText = p.DisplayText ?? "No Description";
                                return p;
                            })
                        .ToList(); //TODO Use credential DB
                }
                catch
                {
                    return Enumerable.Empty<Project>();
                }
            }
        }

        public IEnumerable<AnalysisTaxonomicGroup> GetAnalysisTaxonomicGroupsForProject(int projectID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var atgs = new Queue<AnalysisTaxonomicGroup>(analysisTaxonomicGroupsForProject(projectID, db));
                var flattened = new HashSet<AnalysisTaxonomicGroup>();
                var analyses = analysesForProject(projectID, db).ToLookup(an => an.AnalysisParentID);

                while (atgs.Any())
                {
                    var atg = atgs.Dequeue();
                    if (flattened.Add(atg)) //added just now -> queue children
                    {
                        if (analyses.Contains(atg.AnalysisID))
                            foreach (var child in analyses[atg.AnalysisID])
                                atgs.Enqueue(
                                    new AnalysisTaxonomicGroup()
                                    {
                                        AnalysisID = child.AnalysisID,
                                        TaxonomicGroup = atg.TaxonomicGroup
                                    });
                    }
                }
                return flattened;
            }
        }

        public IEnumerable<Model.Analysis> GetAnalysesForProject(int projectID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var res = analysesForProject(projectID, db).ToList();
                return res;
            }
        }

        public IEnumerable<Model.AnalysisResult> GetAnalysisResultsForProject(int projectID, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                return analysisResultsForProject(projectID, db).ToList();
            }
        }

        public UserProfile GetUserInfo(UserCredentials login)
        {
            try
            {
                using (var db = login.GetConnection())
                {
                    return db.Query<UserProfile>("FROM [DiversityMobile_UserInfo]() AS [UserProfile]").Single();
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly UserCredentials TNT_Login = new UserCredentials() { LoginName = "TNT", Password = "mu7idSwg", Repository = "DiversityMobile" };

        public IEnumerable<Model.TaxonList> GetTaxonListsForUser(UserCredentials login)
        {
            var result = new List<TaxonList>();
            using (var db = login.GetConnection())
            {
                result.AddRange(
                    taxonListsForUser(login.LoginName, db)
                    .Select(l => { l.IsPublicList = false; return l; })
                    );
            }
            var publicTaxa = ServiceConfiguration.PublicTaxa;
            using (var db = new DiversityORM.Diversity(publicTaxa.Login, publicTaxa.Server, publicTaxa.Catalog))
            {
                result.AddRange(
                    taxonListsForUser(publicTaxa.Login.user, db)
                    .Select(l => { l.IsPublicList = true; return l; })
                    );
            }
            return result;
        }

        public IEnumerable<TaxonName> DownloadTaxonList(TaxonList list, int page, UserCredentials login)
        {
            using (var db = ConnectToDBForList(list, login))
            {
                return taxaFromListAndPage(list, page, db);
            }
        }

        private Diversity ConnectToDBForList(TaxonList list, UserCredentials login)
        {
            if (list.IsPublicList)
            {
                var taxa = ServiceConfiguration.PublicTaxa;
                return new Diversity(taxa.Login, taxa.Server, taxa.Catalog);
            }
            else
            {
                return login.GetConnection();
            }
        }

        public IEnumerable<Model.Property> GetPropertiesForUser(UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                return propertyListsForUser(login, db);
            }
        }

        public IEnumerable<Model.PropertyValue> DownloadPropertyNames(Property p, int page, UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                var propsForUser = propertyListsForUser(login, db).ToDictionary(pl => pl.PropertyID);
                PropertyList list;
                if (propsForUser.TryGetValue(p.PropertyID, out list))
                {
                    return loadTablePaged<Model.PropertyValue>(list.Table, page, db);
                }
                else
                {
                    return Enumerable.Empty<Model.PropertyValue>();
                }
            }
        }

        public IEnumerable<Qualification> GetQualifications(UserCredentials login)
        {
            using (var db = login.GetConnection())
            {
                return getQualifications(db)
                    .Select(q =>
                        {
                            if (string.IsNullOrWhiteSpace(q.DisplayText))
                            {
                                q.DisplayText = "no qualifier";
                            }
                            return q;
                        })
                    .ToList();
            }
        }

        #endregion Get

        #region utility

        public IEnumerable<Repository> GetRepositories(UserCredentials login)
        {
            return from repo in Configuration.ServiceConfiguration.Repositories
                   select new Repository()
                   {
                       Database = repo.Catalog,
                       DisplayText = repo.name
                   };
        }

        public bool ValidateLogin(UserCredentials login)
        {
            using (var ctx = login.GetConnection())
            {
                try
                {
                    ctx.OpenSharedConnection(); // validate Credentials
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }

        private DiversityServiceConfiguration.Repository GetRepositoryByName(string name)
        {
            return (from repo in Configuration.ServiceConfiguration.Repositories
                    where repo.name == name
                    select repo).FirstOrDefault();
        }

        #endregion utility
    }
}