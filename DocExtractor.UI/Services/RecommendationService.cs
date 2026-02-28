using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Models;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.Recommendation;

namespace DocExtractor.UI.Services
{
    internal class RecommendationService
    {
        public KnowledgeLearningSummary AutoLearnGroupKnowledge(string dbPath, IReadOnlyList<ExtractedRecord> completeResults)
        {
            var withGroup = completeResults
                .Where(r => r.Fields.ContainsKey("GroupName")
                            && !string.IsNullOrWhiteSpace(r.Fields["GroupName"]))
                .ToList();

            var summary = new KnowledgeLearningSummary();
            if (withGroup.Count == 0)
                return summary;

            using var repo = new GroupKnowledgeRepository(dbPath);
            var byFile = withGroup
                .GroupBy(r => r.SourceFile ?? string.Empty)
                .ToList();

            foreach (var fileGroup in byFile)
            {
                string sourceFile = fileGroup.Key;
                bool wasLearned = repo.IsSourceFileLearned(sourceFile);

                var groupDict = fileGroup
                    .GroupBy(r => r.Fields["GroupName"])
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<ExtractedRecord>)g.ToList());

                int inserted = repo.ReplaceSourceFileItems(groupDict, sourceFile);

                summary.FileDetails.Add(new KnowledgeFileDetail
                {
                    SourceFile = sourceFile,
                    GroupCount = groupDict.Count,
                    InsertedCount = inserted,
                    WasReplaced = wasLearned
                });
                summary.TotalGroups += groupDict.Count;
                summary.TotalInserted += inserted;
                if (wasLearned) summary.ReplacedFiles++;
            }

            return summary;
        }

        public List<string> BuildRecommendGroups(string dbPath, IReadOnlyList<ExtractedRecord> lastResults)
        {
            var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var repo = new GroupKnowledgeRepository(dbPath))
            {
                foreach (var g in repo.GetDistinctGroupNames())
                    items.Add(g);
            }

            foreach (var r in lastResults)
            {
                string gn = r.GetField("GroupName");
                if (!string.IsNullOrWhiteSpace(gn))
                    items.Add(GroupKnowledgeRepository.NormalizeGroupName(gn));
            }

            return items.OrderBy(x => x).ToList();
        }

        public RecommendResponse Recommend(string dbPath, string groupName)
        {
            using var repo = new GroupKnowledgeRepository(dbPath);
            var allGroupNames = repo.GetDistinctGroupNames();

            var response = new RecommendResponse
            {
                KnowledgeCount = repo.GetKnowledgeCount()
            };

            if (allGroupNames.Count == 0)
            {
                response.Items = new List<RecommendedItem>();
                return response;
            }

            var recommender = new GroupItemRecommender();
            response.Items = recommender.Recommend(
                groupName,
                allGroupNames,
                gn =>
                {
                    using var innerRepo = new GroupKnowledgeRepository(dbPath);
                    var items = innerRepo.GetItemsForGroup(gn);
                    return items.ConvertAll(gi => new KnowledgeItem
                    {
                        ItemName = gi.ItemName,
                        RequiredValue = gi.RequiredValue,
                        SourceFile = gi.SourceFile
                    });
                });

            return response;
        }
    }

    internal class KnowledgeLearningSummary
    {
        public int TotalGroups { get; set; }
        public int TotalInserted { get; set; }
        public int ReplacedFiles { get; set; }
        public List<KnowledgeFileDetail> FileDetails { get; set; } = new List<KnowledgeFileDetail>();
    }

    internal class KnowledgeFileDetail
    {
        public string SourceFile { get; set; } = string.Empty;
        public int GroupCount { get; set; }
        public int InsertedCount { get; set; }
        public bool WasReplaced { get; set; }
    }

    internal class RecommendResponse
    {
        public int KnowledgeCount { get; set; }
        public List<RecommendedItem> Items { get; set; } = new List<RecommendedItem>();
    }
}
