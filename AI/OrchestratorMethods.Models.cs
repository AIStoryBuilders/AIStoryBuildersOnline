using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using AIStoryBuilders.Models.JSON;
using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<List<AIStoryBuilderModel>> ListAllModelsAsync()
        public async Task<List<AIStoryBuilderModel>> ListAllModelsAsync()
        {
            List<AIStoryBuilderModel> colAIStoryBuilderModel = new List<AIStoryBuilderModel>();

            AIStoryBuilderModel objAIStoryBuilderModelGPT5Mini = new AIStoryBuilderModel();
            objAIStoryBuilderModelGPT5Mini.ModelId = "gpt-5-mini";
            objAIStoryBuilderModelGPT5Mini.ModelName = "gpt-5-mini";
            colAIStoryBuilderModel.Add(objAIStoryBuilderModelGPT5Mini);

            AIStoryBuilderModel objAIStoryBuilderModelGPT5 = new AIStoryBuilderModel();
            objAIStoryBuilderModelGPT5.ModelId = "gpt-5";
            objAIStoryBuilderModelGPT5.ModelName = "gpt-5";
            colAIStoryBuilderModel.Add(objAIStoryBuilderModelGPT5);

            AIStoryBuilderModel objAIStoryBuilderModelGPT4 = new AIStoryBuilderModel();
            objAIStoryBuilderModelGPT4.ModelId = "gpt-4o";
            objAIStoryBuilderModelGPT4.ModelName = "gpt-4o";
            colAIStoryBuilderModel.Add(objAIStoryBuilderModelGPT4);

            AIStoryBuilderModel objAIStoryBuilderModelGPT3 = new AIStoryBuilderModel();
            objAIStoryBuilderModelGPT3.ModelId = "gpt-3.5-turbo";
            objAIStoryBuilderModelGPT3.ModelName = "GPT-3.5";
            colAIStoryBuilderModel.Add(objAIStoryBuilderModelGPT3);

            return colAIStoryBuilderModel;
        }
        #endregion

        #region public async Task UpdateModelNameAsync(AIStoryBuilderModel paramaModel)
        public async Task UpdateModelNameAsync(AIStoryBuilderModel paramaModel)
        {
            // Get the Model alias names from the database
            await DatabaseService.LoadDatabaseAsync();
            var colDatabase = DatabaseService.colAIStoryBuildersDatabase;

            // Create a new collection to store the updated model names
            Dictionary<string, string> colUpdatedDatabase = new Dictionary<string, string>();

            bool ModelExists = false;

            // Iterate through the existing database
            foreach (var item in colDatabase)
            {
                if (item.Key == paramaModel.ModelId)
                {
                    colUpdatedDatabase.Add(paramaModel.ModelId, paramaModel.ModelName);
                    ModelExists = true;
                }
                else
                {
                    colUpdatedDatabase.Add(item.Key, item.Value);
                }
            }

            if (!ModelExists)
            {
                colUpdatedDatabase.Add(paramaModel.ModelId, paramaModel.ModelName);
            }

            await DatabaseService.SaveDatabaseAsync(colUpdatedDatabase);
        }
        #endregion
    }
}
