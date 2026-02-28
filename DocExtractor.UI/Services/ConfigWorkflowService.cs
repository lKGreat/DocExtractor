using System.Collections.Generic;
using DocExtractor.Core.Models;
using DocExtractor.Data.Repositories;

namespace DocExtractor.UI.Services
{
    internal class ConfigWorkflowService
    {
        private readonly ExtractionConfigRepository _repository;

        public ConfigWorkflowService(ExtractionConfigRepository repository)
        {
            _repository = repository;
        }

        public void SeedBuiltInConfigs() => _repository.SeedBuiltInConfigs();

        public List<(int Id, string Name)> GetAll() => _repository.GetAll();

        public ExtractionConfig? GetById(int id) => _repository.GetById(id);

        public int Save(ExtractionConfig config) => _repository.Save(config);

        public bool Delete(int id) => _repository.Delete(id);

        public int GetDefaultConfigId() => _repository.GetDefaultConfigId();

        public void SetDefaultConfigId(int id) => _repository.SetDefaultConfigId(id);
    }
}
