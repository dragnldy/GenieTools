using Stormfront2Genie;

namespace xUnitTests
{
    public class UnitTest1
    {
        const string SAMPLEXML = @"C:\\Users\\trish\\source\\repos\\GenieTools\\xUnitTests\\bin\\Debug\\net6.0\\stormfront.xml";
        [Fact]
        public void CanExtractScripts()
        {
            // should succeed
            try
            {
                SettingsImporter importer = new SettingsImporter();
                var result = importer.LoadDocumentSync(SAMPLEXML);
                Dictionary<string, string> scripts = importer.ExtractScripts();

            }
            catch (Exception exc)
            {

            }

        }
        [Fact]
        public void CanLoadStormfrontXml()
        {
            // should succeed
            try
            {
                SettingsImporter importer = new SettingsImporter();
                var result = importer.LoadDocumentSync(SAMPLEXML);
            }
            catch (Exception exc)
            {
                
            }

            // Should fail
            try
            {
                SettingsImporter importer = new SettingsImporter();
                var result = importer.LoadDocumentSync("junk");
            }
            catch (Exception exc)
            {

            }

        }
    }
}