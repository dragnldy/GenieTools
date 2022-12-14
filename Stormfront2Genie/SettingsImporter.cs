using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace Stormfront2Genie
{
    // All the code in this file is included in all platforms.
    public class SettingsImporter
    {
        private XDocument _xDoc = null;
        private int _numScripts = 0;
        public SettingsImporter()
        {
        }
        public string LoadDocumentSync(string sfFilename)
        {
            try
            {
                var reader = new StreamReader(sfFilename);
                var xDocTemp = XDocument.Load(reader, new LoadOptions());

                _xDoc = xDocTemp;

                return "success";
            }
            catch (Exception exc)
            {
                Debug.WriteLine("Error loading settings file: " + exc.Message);
                _xDoc = null;
                return "failed";
            }
        }
        public async Task<string> LoadDocumentAsync(string sfFilename)
        { 
            try
            {
                var reader = new StreamReader(sfFilename);
                var xDocTemp = await XDocument.LoadAsync(reader, new LoadOptions(), new CancellationToken());
                
                _xDoc = xDocTemp;

                // Determine the number of scripts to be processed
                XElement xScripts = _xDoc.Descendants("scripts").FirstOrDefault();
                if (xScripts != null)
                {
                    _numScripts = xScripts.Elements("s").Count();
                }

                return "success";
            }
            catch(Exception exc)
            {
                Debug.WriteLine("Error loading settings file: " + exc.Message);
                _xDoc = null;
                return "failed";
            }
        }
        // Returns an enumerable object of script names with script contents with any comment preappended to the script body
        // <scripts prefix="." local=""><s name="Land_To_Haven" comment="Rossmans landing to Haven" fmt="tok">\\.....>
        public IEnumerable<(string,string)> ExtractedScripts()
        {
            XElement xScripts = _xDoc.Descendants("scripts").FirstOrDefault();
            if (xScripts != null)
            {
                foreach (XElement xScript in xScripts.Elements("s"))
                {
                    (string sname, string sbody) = AddScript(xScript);
                    yield return (sname, sbody);
                }
            }
        }
        // Returns a dictionary of script names with script contents with any comment preappended to the script body
        // <scripts prefix="." local=""><s name="Land_To_Haven" comment="Rossmans landing to Haven" fmt="tok">\\.....>
        public Dictionary<string, string> ExtractScripts()
        {
            if (_xDoc == null)
                return null;

            Dictionary<string, string> sDict = new Dictionary<string, string>();

            IEnumerable<(string, string)> extractedScripts = ExtractedScripts();
            foreach ((string script, string sbody) in extractedScripts)
            {
                if (!sDict.ContainsKey(script))
                    sDict[script] = sbody;
            }

            return sDict;
        }

        // Stormfront 'tokenizes' a limited number of movement commands.  This is the translation table for the tokens
        // This only applies to xml files that are used by the Stormfront runtime, not the ones that are generated by exporting.
        // Files created by exporting have the tokens normalized but it double and triple spaces everything due to the way it resolves the tokens
        // See end of this file for examples
        private Dictionary<string,string> directions = new Dictionary<string, string>() {
            {"A","move n" },
            {"B","move ne"},
            {"C","move e" },
            {"D","move se" },
            {"E","move s"},
            {"F","move sw" },
            {"G","move w" },
            {"H","move nw" },
            {"I","move out" },
            {"J","move up" },
            {"K","move down" },
            {"L","put " }

        };
        private Regex tokens = new Regex("(\\\\.[A-L]+\\\\).?");

        private (string,string) AddScript(XElement xScript)
        {
            string sName = FindStringAttribute(xScript, "name");
            string sComment = "#" + FindStringAttribute(xScript, "comment");
            string sFmt = FindStringAttribute(xScript, "fmt");
            string sBody = xScript.Value;

            if (sFmt.Equals("tok"))
            {
                // unprocessed tokenized xml file used by stormfront directly
                sBody = sBody.Replace(@"\.\", "\n");

                // There may be places where a bunch of moves are compressed into one long string
                while (tokens.IsMatch(sBody))
                {
                    Match match = tokens.Match(sBody);
                    string sequence = match.Groups[1].Value.ToString();
                    int stindex = match.Index;
                    int length = sequence.Length;
                    string head = sBody.Substring(0, stindex);
                    string tail = sBody.Substring(stindex + length);

                    StringBuilder sb = new StringBuilder(head.Last().Equals("\n") ? "" : "\n");
                    foreach (char c in sequence)
                    {
                        if (!directions.ContainsKey(c.ToString()))
                            continue;

                        string direction = directions[c.ToString()];

                        if (!c.Equals('L'))
                            sb.AppendLine(direction);
                        else
                            sb.Append(direction);

                    }
                    sb.Append(tail);
                    sBody = head + sb.ToString();
                }
                // We may need to compress this
                // sBody = sBody.Replace("\n\n", "\n");

                if (sBody.EndsWith("\\."))
                    sBody = sBody.Substring(0, sBody.Length - 2);
                if (sBody.StartsWith(@"\"))
                    sBody = sBody.Substring(1);
            }
            else
            {
                // exported xml file that was produced by stormfront but has to be imported back into SF to be useful
                sBody = sBody.Replace("\n\n", "\n");
            }
            return (sName, HttpUtility.HtmlDecode(sComment + "\n" + sBody));
        }

        // Color palette
        // <palette local=""><i id="0" color="#FFFF90"/>
        public Dictionary<string, string> ExtractColors()
        {
            Dictionary<string, string> dColors = new Dictionary<string, string>();

            XElement xColors = _xDoc.Descendants("palette").FirstOrDefault();
            if (xColors != null)
            {
                xColors.Elements("i").ToList().ForEach(n => AddColor(n,dColors));
            }
            return dColors;
        }
        private void AddColor(XElement xColor, Dictionary<string,string> cDict)
        {
            int? id = FindIntAttribute(xColor, "id");
            if (id != null && !cDict.ContainsKey(id.Value.ToString()))
                cDict[id.Value.ToString()] = FindStringAttribute(xColor, "color");
        }


        // Variables
        // <vars local=""><v name="CALLFC" value="forage"/>
        public Dictionary<string,string> ExtractVariables()
        {
            Dictionary<string,string> dVariables = new Dictionary<string,string>();

            XElement xVars = _xDoc.Descendants("vars").FirstOrDefault();
            if (xVars != null)
            {
                xVars.Elements("v").ToList().ForEach(n => AddVariable(n, dVariables));
            }

            return dVariables;
        }
        private void AddVariable(XElement xVar, Dictionary<string, string> vDict)
        {
            string sVarName = FindStringAttribute(xVar, "name");
            string sValue = FindStringAttribute(xVar, "value");

            if (!string.IsNullOrWhiteSpace(sVarName) && !vDict.ContainsKey(sVarName))
                vDict[sVarName] = sValue;
        }

        // ignore list
        // <ignores disable="n" local=""><h text="One of the shadowlings slinks off"/>
        public List<string> ExtractIgnores()
        {
            List<string> iList = new List<string>();

            XElement xIgnore = _xDoc.Descendants("ignores").FirstOrDefault();
            if (xIgnore != null)
            {
                xIgnore.Elements("h").ToList().ForEach(n => AddIgnore(n, iList));
            }

            return iList;
        }
        private void AddIgnore(XElement xIgnore, List<string> iList)
        {
            string sVarName = FindStringAttribute(xIgnore, "text");

            if (!string.IsNullOrWhiteSpace(sVarName) && !iList.Contains(sVarName))
                iList.Add(sVarName);
        }

        // Name highlights
        // <names local=""><h text="Aliney" color="@0" bgcolor=""/>
        public Dictionary<string,string> ExtractNames()
        {
            Dictionary<string ,string> dNames = new Dictionary<string ,string>();

            XElement xNames = _xDoc.Descendants("names").FirstOrDefault();
            if (xNames != null)
            {
                xNames.Elements("h").ToList().ForEach(n => AddName(n, dNames));
            }

            return dNames;
        }
        private void AddName(XElement xName, Dictionary<string, string> nDict)
        {
            string sName = FindStringAttribute(xName, "text");
            string fgColor = FindStringAttribute(xName, "color");
            string bgColor = FindStringAttribute(xName, "bgcolor");

            if (!string.IsNullOrWhiteSpace(sName) && !nDict.ContainsKey(sName))
                nDict[sName] = fgColor + ";" + bgColor;
        }

        // Custom strings
        // <strings local=""><h text="whimpers" color="@30" bgcolor="" line="y"/>
        public Dictionary<string,string> ExtractCustomStrings()
        {
            Dictionary <string ,string> dCustomStrings = new Dictionary<string ,string>();

            XElement xCustom = _xDoc.Descendants("strings").FirstOrDefault();

            if (xCustom != null)
            {
                xCustom.Elements("h").ToList().ForEach(n => AddCustomHighlight(n,dCustomStrings));
            }

            return dCustomStrings;
        }
        private void AddCustomHighlight(XElement xCustom, Dictionary<string, string> cDict)
        {
            string sText = FindStringAttribute(xCustom, "text");
            string fgColor = FindStringAttribute(xCustom, "color");
            string bgColor = FindStringAttribute(xCustom, "bgcolor");
            string doLine = FindStringAttribute(xCustom, "line");
            if (string.IsNullOrEmpty(doLine))
                doLine = "n";

            if (!string.IsNullOrWhiteSpace(sText) && !cDict.ContainsKey(sText))
                cDict[sText] = fgColor + ";" + bgColor + ";" + doLine;
        }


        private string FindStringAttribute(XElement xElement, string attribute)
        {
            var found = xElement.Attributes(attribute).FirstOrDefault();
            if (found == null)
                return "";
            return HttpUtility.HtmlDecode(found.Value.ToString());
        }
        private int? FindIntAttribute(XElement xElement, string attribute)
        {
            var found = xElement.Attributes(attribute).FirstOrDefault();
            if (found == null || !int.TryParse(found.Value.ToString(), out int intvalue))
                return null;
            return intvalue;
        }

        public int GetNumberScripts()
        {
            return _numScripts;
        }


        /*
            private static void SaveScript(string name)
            {
                ScriptModel script = ScriptModel.GetScript(name);
                if (script != null)
                {
                    // Don't save over any that are already on disk
                    if (!File.Exists(App.GetScriptFilename(name, false)))
                        File.WriteAllLines(App.GetScriptFilename(name, false), script.Text);
                }
            }

            private static void SaveScript(XElement script)
            {
                SaveScript(FindAttribute(script, "name"));
            }

        */

        /*
         * Sample tokenized script (simple)  Not that line endings are \.\  directions are \.A\ thru \.L\
         * <s name="Land_To_Haven" comment="Rossmans landing to Haven" fmt="tok">\\.\#Landing to Haven\.\\.\\.\\.G\\.F\\.F\\.G\\.\move go bank\.\\.\pause 5\.\\.F\\.E\\.D\\.E\\.E\\.E\\.L\sear outc\.\\.\move climb hand\.\\.\move climb hand\.\\.F\\.D\\.E\\.F\\.E\\.D\\.C\\.F\\.E\\.D\\.F\\.E\\.F\\.D\\.E\\.E\\.E\\.D\\.</s>
         * Some files might have long runs of movement compressed like this (Kraelyst travel script has lots of this):
         * move go ridge\.ABHAABBABABABABBCCAABABABCBAABBBBBABBAABB\
         */
    }

}