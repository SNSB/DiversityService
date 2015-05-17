﻿using DiversityORM;
using DiversityPhone.Model;
using DiversityService.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiversityService
{
    public partial class DiversityService
    {
        private const int PAGE_SIZE = 1000;

        private static IEnumerable<AnalysisTaxonomicGroup> analysisTaxonomicGroupsForProject(int projectID, Diversity db)
        {
            return db.Query<AnalysisTaxonomicGroup>("FROM [DiversityMobile_AnalysisTaxonomicGroupsForProject](@0) AS [AnalysisTaxonomicGroup]", projectID);
        }

        private static IEnumerable<TaxonList> taxonListsForUser(string loginName, Diversity db)
        {
            return db.Query<TaxonList>("FROM [DiversityMobile_TaxonListsForUser](@0) AS [TaxonList]", loginName);
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