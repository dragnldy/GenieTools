using Stormfront2Genie;
using System.Text;

namespace GenieUtilsFE
{
    public class SettingsExtractor
    {
        private static SettingsImporter importer = null;
        private static string importFile = string.Empty;
        public async Task<string> LoadSettingsDoc(FileResult sfFile)
        {
            if (importer == null || !sfFile.FullPath.Equals(importFile))
            {
                importer = new SettingsImporter();
                var taskResult = await importer.LoadDocumentAsync(sfFile.FullPath);
                return taskResult;
            }
            return "already loaded";
        }
        public string ExtractScripts(MainPage main, string outputFolder, bool doOverwrite = true)
        {
            string status = "Success";
            if (importer == null)  // Haven't successfully loaded document yet
                return "Document not loaded";

            try
            {
                int totalScripts = GetNumberScripts();
                if (totalScripts > 0)
                {
                    main.LoadingProgress = 0.0;
                    double increment = 1.0 / totalScripts;
                    IEnumerable<(string, string)> extractedScripts = importer.ExtractedScripts();
                    foreach ((string script, string sbody) in extractedScripts)
                    {
                        main.LoadingProgress += increment;
                        SaveScript(script, sbody, outputFolder, doOverwrite);
                    }
                }
                return status;
            }
            catch (Exception exc)
            {
                status = "Exception thrown: " + exc.Message;
            }
            return status;
        }

        private void SaveScript(string scriptName, string scriptBody,string outputFolder, bool doOverwrite = true)
        {
            SaveFile("scripts", scriptName + ".cmd", scriptBody,outputFolder,doOverwrite);
        }
        public void SaveFile(string contentType, string fileName, string contents, string outputFolder, bool doOverwrite = true)
        {
            if (string.IsNullOrEmpty(outputFolder))
                outputFolder = FileSystem.AppDataDirectory;
            string directory = System.IO.Path.Combine(outputFolder, "Extracted", contentType);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string targetFile = System.IO.Path.Combine(directory, fileName);
            if (doOverwrite || !File.Exists(targetFile))
                File.WriteAllText(targetFile, contents);

            //using FileStream outputStream = System.IO.File.OpenWrite(targetFile);
            //using StreamWriter streamWriter = new StreamWriter(outputStream);

            //await streamWriter.WriteAsync(contents);
        }

        internal int GetNumberScripts()
        {
            if (importer != null)
                return importer.GetNumberScripts();
            return 0;
        }

        public string ExtractOtherElements(MainPage main, string outputFolder, bool doOverwrite = true)
        {
            string status = "Success";
            if (importer == null)  // Haven't successfully loaded document yet
                return "Document not loaded";

            try
            {
                Dictionary<string,string> configDictionary = new Dictionary<string,string>();
                MergeDictionary(configDictionary, "color", importer.ExtractColors());
                MergeDictionary(configDictionary,"name",importer.ExtractNames());
                MergeList(configDictionary, "ignore", importer.ExtractIgnores());
                MergeDictionary(configDictionary, "highlight", importer.ExtractCustomStrings());
                MergeDictionary(configDictionary, "variable", importer.ExtractVariables());

                // Generate a string suitable for a csv file from the merged dictionary
                StringBuilder sb = new StringBuilder();
                foreach (string key in configDictionary.Keys)
                {
                    string line = $"{key},\"{configDictionary[key]}\"";
                    sb.AppendLine(line);
                }

                SaveFile("config", "configs.csv", sb.ToString(),outputFolder, doOverwrite);

                return status;
            }
            catch (Exception exc)
            {
                status = "Exception thrown: " + exc.Message;
            }
            return status;
        }

        private void MergeList(Dictionary<string, string> configDictionary, string configType, List<string> list)
        {
            int listnum = 0;
            foreach (string key in list)
            {
                if (!configDictionary.ContainsKey(configType + "_" + listnum.ToString()))
                    configDictionary[configType + "_" + listnum.ToString()] = key;
                listnum++;
            }
        }

        private void MergeDictionary(Dictionary<string, string> configDictionary, string configtype, Dictionary<string, string> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
                return;

            foreach (string key in dictionary.Keys)
            {
                if (!configDictionary.ContainsKey(configtype + "_" + key))
                {
                    configDictionary[configtype + "_" + key] = dictionary[key];
                }
                else
                {

                }
            }
        }
    }
}
