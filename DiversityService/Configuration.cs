using System.Collections.Generic;
using System.Linq;
using Model = DiversityPhone.Model;

namespace DiversityServiceConfiguration
{
    public partial class Login
    {
        public static implicit operator Login(Model.UserCredentials creds)
        {
            return new Login()
            {
                user = creds.LoginName,
                password = creds.Password
            };
        }
    }
}

namespace DiversityService.Configuration
{
    using DiversityServiceConfiguration;

    internal class ServiceConfiguration
    {
        private static ServiceConfiguration _instance;
        private static Configuration _cfg;
        private static IEnumerable<Repository> _repos;
        private static ServerLoginCatalog _public_taxa;
        private static ServerLoginCatalog _scientific_terms;

        public static IEnumerable<Repository> Repositories
        {
            get
            {
                return _repos;
            }
        }

        public static ServerLoginCatalog PublicTaxa
        {
            get
            {
                return _public_taxa;
            }
        }

        public static ServerLoginCatalog ScientificTerms
        {
            get
            {
                return _scientific_terms;
            }
        }

        public static Repository RepositoryByName(string name)
        {
            return Repositories.FirstOrDefault(r => r.name == name);
        }

        private ServiceConfiguration()
        {
            _cfg = Configuration.Load(System.Web.Hosting.HostingEnvironment.MapPath(@"~\DiversityServiceConfiguration.xml"));
            _repos = _cfg.Repositories.Repository;
            _public_taxa = _cfg.PublicTaxonConfig;
            _scientific_terms = _cfg.ScientificTermsConfig;
        }

        static ServiceConfiguration()
        {
            _instance = new ServiceConfiguration();
        }
    }
}