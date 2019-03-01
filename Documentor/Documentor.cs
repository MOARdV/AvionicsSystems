/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016-2019 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Documentor
{
    /// <summary>
    /// Documentor is a quick-and-dirty tool to suck doxygen-style comments out
    /// of a source file, along with a little bit of context to those comments,
    /// and to assemble them into a textfile that can be used with markdown.
    /// 
    /// It supports a couple of extra XML element types <LuaName></LuaName> and <mdDoc></mdDoc>.
    /// The former is used to hold the Lua table name that is used to access the
    /// documented variables, while the latter is used to contain the documentation
    /// at the top of the MD file, so that the 'summary' element may be used for
    /// source code documentation.
    /// 
    /// The code is not robust, and it's not intended for general use - it was
    /// written specifically for my needs documenting some of the source files
    /// in the Avionics Systems mod, and that's it.
    /// </summary>
    public class Documentor
    {
        internal class DocumentToken
        {
            internal string rawName;
            internal StringBuilder rawSummary;
        }

        internal class IndexToken
        {
            internal enum IndexType
            {
                INDEX_FUNCTION,
                INDEX_REGION
            };
            internal string rawName;
            internal string sourceFile;
            internal string summary;
            internal StringBuilder decoratedName;
            internal IndexType indexType;
        };

        static bool anyWritten = false;

        static List<DocumentToken> tokens = new List<DocumentToken>();
        static List<IndexToken> index = new List<IndexToken>();
        static List<IndexToken> region = new List<IndexToken>();

        static void Main(string[] args)
        {
            //System.Console.WriteLine("Documentifying:");
            for (int i = 0; i < args.Length; ++i)
            {
                //System.Console.WriteLine(string.Format(" ...{0}", args[i]));
                Documentify(args[i]);
            }

            if (anyWritten)
            {
                int backslash = args[0].LastIndexOf('\\');
                string indexFile = args[0].Substring(0, backslash + 1) + "index.md";

                index.Sort(delegate(IndexToken a, IndexToken b)
                {
                    if (a.rawName == b.rawName)
                    {
                        return string.Compare(a.decoratedName.ToString(), b.decoratedName.ToString());
                    }
                    else
                    {
                        return string.Compare(a.rawName, b.rawName);
                    }
                });

                StringBuilder indexContents = new StringBuilder();
                indexContents.AppendLine("The following is an index to all functions available in MOARdV's Avionics Systems, sorted by function name.  Partial summaries are included, along with a link to the full documentation.").AppendLine();
                indexContents.AppendLine("The list of all categories is found [here](https://github.com/MOARdV/AvionicsSystems/wiki/Index-of-Functions#categories).").AppendLine();
                indexContents.AppendFormat("**Master Function Index**, {0} functions:", index.Count).AppendLine().AppendLine();

                foreach (var i in index)
                {
                    string[] b = i.sourceFile.Split('.');

                    string decoratedName = i.decoratedName.ToString();
                    i.decoratedName.Replace(' ', '-').Replace('(', '.').Replace(')', '.').Replace(',', '.');

                    string[] linkName = i.decoratedName.ToString().Split('.');
                    string link = string.Join("", linkName);
                    link = link.ToLower();

                    indexContents.AppendLine(string.Format("* **{0}:** `{1}` {4} ([see](https://github.com/MOARdV/AvionicsSystems/wiki/{2}#{3}))", i.rawName, decoratedName, b[0], link, i.summary));
                }

                indexContents.AppendLine().AppendLine("***").AppendLine().AppendLine("### Categories");
                indexContents.AppendFormat("**Master Category Index**, {0} categories:", region.Count).AppendLine().AppendLine();
                region.Sort(delegate(IndexToken a, IndexToken b)
                {
                    if (a.rawName == b.rawName)
                    {
                        return string.Compare(a.decoratedName.ToString(), b.decoratedName.ToString());
                    }
                    else
                    {
                        return string.Compare(a.rawName, b.rawName);
                    }
                });
                foreach (var i in region)
                {
                    string[] b = i.sourceFile.Split('.');

                    string decoratedName = i.decoratedName.ToString();
                    string rawName = i.rawName;
                    rawName = rawName.Replace(' ', '-').Replace('(', '.').Replace(')', '.').Replace(',', '.');

                    string[] linkName = rawName.Split('.');
                    string link = string.Join("", linkName);
                    link = link.ToLower();

                    indexContents.AppendLine(string.Format("* **{0}:** `{1}` {4} ([see](https://github.com/MOARdV/AvionicsSystems/wiki/{2}#{3}-category))", i.rawName, decoratedName, b[0], link, i.summary));
                }

                indexContents.AppendLine().AppendLine("***");
                DateTime now = DateTime.UtcNow;
                indexContents.AppendLine(string.Format("*This documentation was automatically generated from source code at {0,2:00}:{1,2:00} UTC on {2}/{3}/{4}.*",
                    now.Hour, now.Minute, now.Day, MonthAbbr[now.Month], now.Year));

                File.WriteAllText(indexFile, indexContents.ToString(), Encoding.UTF8);
            }
        }

        static readonly string[] MonthAbbr =
        {
            "???",
            "Jan",
            "Feb",
            "Mar",
            "Apr",
            "May",
            "Jun",
            "Jul",
            "Aug",
            "Sep",
            "Oct",
            "Nov",
            "Dec",
        };

        /// <summary>
        /// Do the documentation parsing.
        /// </summary>
        /// <param name="sourceFile"></param>
        static void Documentify(string sourceFile)
        {
            int tail = sourceFile.LastIndexOf('.');
            if (tail <= 0)
            {
                throw new Exception("Can't find a file name dot in sourceFile");
            }
            int backslash = sourceFile.LastIndexOf('\\');

            string mdStr = sourceFile.Substring(0, tail) + ".md";

            // Read the file.
            string[] lines = File.ReadAllLines(sourceFile, Encoding.UTF8);

            //string xmlStr = sourceFile.Substring(0, tail) + ".xml";

            bool inComments = false;
            DocumentToken docToken = null;
            // Iterate over the lines to assemble the list of potential documentations.
            for (int i = 0; i < lines.Length; ++i)
            {
                string token = lines[i].Trim();
                if (inComments)
                {
                    if (token.StartsWith("///"))
                    {
                        docToken.rawSummary.AppendLine(token.Substring(3));
                    }
                    else
                    {
                        if (token != "[MoonSharpHidden]")
                        {
                            docToken.rawName = token;
                            tokens.Add(docToken);
                        }
                        inComments = false;
                    }
                }
                else
                {
                    if (token.StartsWith("///"))
                    {
                        inComments = true;
                        docToken = new DocumentToken();
                        docToken.rawSummary = new StringBuilder();
                        docToken.rawSummary.AppendLine("<token>");
                        docToken.rawSummary.AppendLine(token.Substring(3));
                    }
                }
            }
            docToken = null;

            List<string> contents = new List<string>();

            //System.Console.WriteLine("Potential Tokens:");
            StringBuilder docString = new StringBuilder();
            docString.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            docString.Append("<documentor file=\"");
            string sourceFileName = sourceFile.Substring(backslash + 1);
            docString.Append(sourceFileName);
            docString.AppendLine("\">");
            for (int i = 0; i < tokens.Count; ++i)
            {
                //System.Console.WriteLine(string.Format("[{0,3}]: {1}", i, tokens[i].rawName));
                int openParen = tokens[i].rawName.IndexOf('(');
                if (openParen > 0)
                {
                    int lastSpace = tokens[i].rawName.LastIndexOf(' ', openParen);
                    if (lastSpace >= 0)
                    {
                        tokens[i].rawSummary.Append("<method>");
                        tokens[i].rawSummary.Append(tokens[i].rawName.Substring(lastSpace + 1));
                        tokens[i].rawSummary.AppendLine("</method>");
                    }
                }
                else if (tokens[i].rawName.StartsWith("#region"))
                {
                    tokens[i].rawSummary.Append("<region>");
                    string regionName = tokens[i].rawName.Substring(tokens[i].rawName.IndexOf(' ') + 1);
                    contents.Add(regionName);
                    tokens[i].rawSummary.Append(regionName);
                    tokens[i].rawSummary.AppendLine("</region>");
                }
                tokens[i].rawSummary.AppendLine("</token>");
                docString.Append(tokens[i].rawSummary.ToString());
            }
            docString.AppendLine("</documentor>");
            string rawDocument = docString.ToString();

            // Uncomment to spew the XML document to a file for perusal.
            //File.WriteAllText(xmlStr, rawDocument, Encoding.UTF8);

            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            try
            {
                doc.LoadXml(rawDocument);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                tokens.Clear();
                return;
            }

            string luaNamespace = string.Empty;
            XmlNode root = doc.DocumentElement;
            docString = new StringBuilder();
            docString.Append("# ");
            docString.AppendLine(sourceFileName);
            docString.AppendLine();

            if (contents.Count > 1)
            {
                docString.AppendLine("## Contents");
                docString.AppendLine();
                for (int i = 0; i < contents.Count; ++i)
                {
                    docString.Append("* [");
                    docString.Append(contents[i]);
                    docString.Append("](#");
                    string anchor = contents[i].ToLower().Replace(' ', '-');
                    string[] finalAnchor = anchor.Split(',');
                    foreach (string a in finalAnchor)
                    {
                        docString.Append(a);
                    }
                    docString.AppendLine("-category)");
                }
                docString.AppendLine();
            }

            if (root.HasChildNodes)
            {
                XmlNode child = root.FirstChild;
                while (child != null)
                {
                    if (child is XmlElement && child.Name == "token")
                    {
                        IndexToken indexEntry = ParseToken(child, docString, ref luaNamespace);
                        if (indexEntry != null)
                        {
                            indexEntry.sourceFile = sourceFileName;
                            if (indexEntry.indexType == IndexToken.IndexType.INDEX_FUNCTION)
                            {
                                index.Add(indexEntry);
                            }
                            else if (indexEntry.indexType == IndexToken.IndexType.INDEX_REGION)
                            {
                                region.Add(indexEntry);
                            }
                        }
                        //System.Console.WriteLine(child.Name);
                    }
                    child = child.NextSibling;
                }
            }
            docString.AppendLine("***");
            DateTime now = DateTime.UtcNow;
            docString.AppendLine(string.Format("*This documentation was automatically generated from source code at {0,2:00}:{1,2:00} UTC on {2}/{3}/{4}.*",
                now.Hour, now.Minute, now.Day, MonthAbbr[now.Month], now.Year));
            docString.AppendLine();
            if (!File.Exists(mdStr) || (File.Exists(mdStr) && File.GetLastWriteTimeUtc(mdStr) <= File.GetLastWriteTimeUtc(sourceFile)))
            {
                File.WriteAllText(mdStr, docString.ToString(), Encoding.UTF8);
                anyWritten = true;
            }

            tokens.Clear();
        }

        /// <summary>
        /// Handle writing the markdown documentation for a single XML element.
        /// </summary>
        /// <param name="child"></param>
        /// <param name="docString"></param>
        /// <param name="luaNamespace"></param>
        private static IndexToken ParseToken(XmlNode child, StringBuilder docString, ref string luaNamespace)
        {
            IndexToken indexEntry = null;

            XmlElement luaNameTag = child["LuaName"];
            if (luaNameTag != null)
            {
                luaNamespace = luaNameTag.InnerText;

                XmlElement markdownDoc = child["mdDoc"];
                if (markdownDoc != null)
                {
                    string innerText = markdownDoc.InnerText.Replace("&lt;", "<").Replace("&gt;", ">");
                    docString.AppendLine(innerText);
                    docString.AppendLine();
                }
            }

            XmlElement methodName = child["method"];
            if (methodName != null)
            {
                indexEntry = new IndexToken();
                indexEntry.indexType = IndexToken.IndexType.INDEX_FUNCTION;
                indexEntry.decoratedName = new StringBuilder();

                docString.Append("### ");
                if (!string.IsNullOrEmpty(luaNamespace))
                {
                    docString.Append(luaNamespace);
                    docString.Append('.');
                    indexEntry.decoratedName.Append(luaNamespace).Append('.');
                }
                indexEntry.rawName = methodName.InnerText;
                indexEntry.decoratedName.Append(methodName.InnerText);

                docString.AppendLine(methodName.InnerText);

                docString.AppendLine();
                int paramCount = 0;
                XmlNode param = child["param"];
                while (param != null)
                {
                    if (param is XmlElement && !string.IsNullOrEmpty(param.InnerText.Trim()))
                    {
                        XmlAttribute attr = param.Attributes["name"];
                        if (attr != null)
                        {
                            docString.Append("* `");
                            docString.Append(attr.Value);
                            docString.Append("`: ");
                            docString.AppendLine(param.InnerText.Trim());
                            ++paramCount;
                        }
                    }

                    param = param.NextSibling;
                }
                if (paramCount > 0)
                {
                    docString.AppendLine();
                }

                XmlElement returns = child["returns"];
                if (returns != null && !string.IsNullOrEmpty(returns.InnerText.Trim()))
                {
                    docString.Append("**Returns**: ");
                    docString.AppendLine(returns.InnerText.Trim());
                    docString.AppendLine();
                }

                XmlElement seealso = child["seealso"];
                if (seealso != null && !string.IsNullOrEmpty(seealso.InnerText.Trim()))
                {
                    docString.Append("**Supported Mod(s)**: ");
                    docString.AppendLine(seealso.InnerText.Trim());
                    docString.AppendLine();
                }

                XmlElement summary = child["summary"];
                if (summary != null && methodName != null)
                {
                    string innerText = summary.InnerText.Replace("&lt;", "<").Replace("&gt;", ">");
                    indexEntry.summary = innerText;
                    indexEntry.summary = indexEntry.summary.Replace(Environment.NewLine, " ").Replace("`", " ").Trim(' ');
                    if (indexEntry.summary.Length > 32)
                    {
                        indexEntry.summary = indexEntry.summary.Substring(0, 32);
                        indexEntry.summary += "...";
                    }
                    docString.AppendLine(innerText);
                    docString.AppendLine();
                }
            }

            XmlElement regionName = child["region"];
            if (regionName != null)
            {
                indexEntry = new IndexToken();
                indexEntry.indexType = IndexToken.IndexType.INDEX_REGION;
                indexEntry.decoratedName = new StringBuilder();

                docString.AppendLine("***");
                docString.Append("## ");
                docString.Append(regionName.InnerText);

                indexEntry.rawName = regionName.InnerText;
                if (!string.IsNullOrEmpty(luaNamespace))
                {
                    indexEntry.decoratedName.Append(luaNamespace);
                }

                docString.AppendLine(" Category");
                docString.AppendLine();
                XmlElement summary = child["summary"];
                if (summary != null && regionName != null)
                {
                    string innerText = summary.InnerText.Replace("&lt;", "<").Replace("&gt;", ">");
                    indexEntry.summary = innerText;
                    indexEntry.summary = indexEntry.summary.Replace(Environment.NewLine, " ").Replace("`", " ").Trim(' ');
                    if (indexEntry.summary.Length > 64)
                    {
                        indexEntry.summary = indexEntry.summary.Substring(0, 64);
                        indexEntry.summary += "...";
                    }
                    docString.AppendLine(innerText);
                    docString.AppendLine();
                }
            }

            return indexEntry;
        }
    }
}
