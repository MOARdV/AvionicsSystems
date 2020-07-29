/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018-2020 MOARdV
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
using System.Text;
using System.Xml;

namespace PropConfig
{
    /// <summary>
    /// PropConfig is a tool to automate creating MAS-enabled prop configuration
    /// files without having to copy / paste / edit dozens of KSP config files
    /// by hand.
    /// 
    /// It uses an XML file (or several) for input, and it generates an arbitrary
    /// number of config files.
    /// 
    /// The XML schema is documented on the MAS wiki under Prop Config
    /// at https://github.com/MOARdV/AvionicsSystems/wiki/Prop-Config.
    /// </summary>
    public class PropConfig
    {
        static bool forceRebuild = false;

        static void Main(string[] args)
        {
            int startIndex = 0;
            if (args[0] == "--force")
            {
                forceRebuild = true;
                startIndex = 1;
            }

            for (int i = startIndex; i < args.Length; ++i)
            {
                //Console.WriteLine(string.Format(" ...{1}: {0}", args[i], i));
                try
                {
                    MakeConfigs(args[i]);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Something went wrong processing source {0}:", args[i]);
                    Console.WriteLine("{0}", e.ToString());
                }
            }
        }

        /// <summary>
        /// ConfigNode represents a single config node within the document.
        /// </summary>
        internal class ConfigNode
        {
            public string name;
            public string comment;
            public int id;
            public bool delete;
            public List<Tuple<string, string>> fields = new List<Tuple<string, string>>();
        };

        /// <summary>
        /// Style represents a single style or prop element.
        /// </summary>
        internal class Style
        {
            /// <summary>
            /// Name of this style / prop
            /// </summary>
            public string name;
            /// <summary>
            /// List of MODEL nodes
            /// </summary>
            public List<ConfigNode> model = new List<ConfigNode>();
            /// <summary>
            /// List of MASComponent nodes
            /// </summary>
            public List<ConfigNode> node = new List<ConfigNode>();
            /// <summary>
            /// List of other MODULE nodes.
            /// </summary>
            public List<ConfigNode> module = new List<ConfigNode>();
        };

        /// <summary>
        /// Process a single XML source file.
        /// </summary>
        /// <param name="sourceXmlFile"></param>
        static void MakeConfigs(string sourceXmlFile)
        {
            // Load the file.
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(sourceXmlFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to open XML source {0}:", sourceXmlFile);
                Console.WriteLine("{0}", e.ToString());
                return;
            }
            DateTime lastModified = File.GetLastWriteTimeUtc(sourceXmlFile);

            // Minimally validate the root node.
            XmlNode root = doc.DocumentElement;
            if (root.Name != "PropConfig")
            {
                Console.WriteLine("XML source {0} does not have a <PropConfig> root element", sourceXmlFile);
                return;
            }

            foreach (XmlNode child in root.ChildNodes)
            {
                if (child is XmlElement)
                {
                    ProcessPropGroup(child as XmlElement, lastModified);
                }
            }
        }

        /// <summary>
        /// Prop Config allows normal C# formatting strings, eg {0}.  However,
        /// those are illegal as text in a KSP ConfigNode field, since those
        /// strings are apparently fed to a function that reads them as formatting
        /// strings within KSP.  The RPM and MAS convention is to use '<=' and '=>'
        /// instead of '{' and '}', but '<' and '>' are not legal in an XML text
        /// node.  Which means using the cumbersome '&lt;=' and '=&gt;' tokens.
        /// 
        /// Instead, Prop Config will accept '{' and '}' and string-substitute the
        /// ConfigNode-safe versions.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        static string ScrubBrackets(string source)
        {
            return source.Replace("{", "<=").Replace("}", "=>");
        }

        /// <summary>
        /// Process a single ConfigNode element.
        /// </summary>
        /// <param name="nodeElement"></param>
        /// <param name="node"></param>
        static void ProcessNode(XmlElement nodeElement, ConfigNode node)
        {
            node.name = nodeElement.Name;

            // Find the id.
            string idString = nodeElement.GetAttribute("id");
            if (!string.IsNullOrEmpty(idString))
            {
                int id;
                if (int.TryParse(idString, out id))
                {
                    node.id = id;
                }
                else
                {
                    node.id = 0;
                }
            }
            else
            {
                node.id = 0;
            }

            string deleteMe = nodeElement.GetAttribute("delete");
            if (deleteMe == "true")
            {
                node.delete = true;
                return; // early: 
            }
            else
            {
                node.delete = false;
            }

            foreach (XmlNode child in nodeElement)
            {
                if (child is XmlElement)
                {
                    XmlElement elem = child as XmlElement;
                    if (elem.Name == "comment")
                    {
                        node.comment = elem.InnerText;
                    }
                    else
                    {
                        node.fields.Add(new Tuple<string, string>(elem.Name, ScrubBrackets(elem.InnerText)));
                    }
                }
            }
        }

        /// <summary>
        /// Process a style element.
        /// </summary>
        /// <param name="styleElement"></param>
        /// <param name="style"></param>
        static void ProcessStyle(XmlElement styleElement, Style style)
        {
            foreach (XmlNode child in styleElement)
            {
                if (child is XmlElement)
                {
                    XmlElement elem = child as XmlElement;
                    ConfigNode node = new ConfigNode();

                    ProcessNode(elem, node);

                    bool duplicate = false;
                    if (node.name == "MODEL")
                    {
                        if (style.model.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                        {
                            style.model.Add(node);
                        }
                        else
                        {
                            duplicate = true;
                        }
                    }
                    else if (node.name.StartsWith("MODULE"))
                    {
                        if (style.module.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                        {
                            style.module.Add(node);
                        }
                        else
                        {
                            duplicate = true;
                        }
                    }
                    else
                    {
                        if (style.node.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                        {
                            style.node.Add(node);
                        }
                        else
                        {
                            duplicate = true;
                        }
                    }
                    if (duplicate)
                    {
                        Console.WriteLine("Duplicate <{0}> ids found in style {1} (id {2} is repeated)",
                            node.name, style.name, node.id);
                    }
                }
            }
        }

        /// <summary>
        /// Generate the node for a prop, including prop-specific overrides.
        /// </summary>
        /// <param name="propElement"></param>
        /// <param name="prop"></param>
        static void ProcessProp(XmlElement propElement, Style prop)
        {
            foreach (XmlNode child in propElement)
            {
                if (child is XmlElement)
                {
                    XmlElement elem = child as XmlElement;
                    if (elem.Name != "name" && elem.Name != "style" && elem.Name != "startupScript")
                    {
                        ConfigNode node = new ConfigNode();

                        ProcessNode(elem, node);

                        bool duplicate = false;
                        if (node.name == "MODEL")
                        {
                            if (prop.model.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                            {
                                prop.model.Add(node);
                            }
                            else
                            {
                                duplicate = true;
                            }
                        }
                        else if (node.name.StartsWith("MODULE"))
                        {
                            if (prop.module.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                            {
                                prop.module.Add(node);
                            }
                            else
                            {
                                duplicate = true;
                            }
                        }
                        else
                        {
                            if (prop.node.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                            {
                                prop.node.Add(node);
                            }
                            else
                            {
                                duplicate = true;
                            }
                        }
                        if (duplicate)
                        {
                            Console.WriteLine("Duplicate <{0}> ids found in prop {1} (id {2} is repeated)",
                                node.name, prop.name, node.id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Merge a style node and a prop node.
        /// </summary>
        /// <param name="style"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        static ConfigNode MergeNodes(ConfigNode style, ConfigNode prop)
        {
            ConfigNode node = new ConfigNode();

            // Copy the relevant portions.
            node.name = style.name;
            node.comment = (string.IsNullOrEmpty(prop.comment)) ? style.comment : prop.comment;
            node.id = style.id;

            // Copy the style, override with any in the prop/
            foreach (var pair in style.fields)
            {
                var replacement = prop.fields.Find(x => x.Item1 == pair.Item1);
                if (replacement != null)
                {
                    node.fields.Add(replacement);
                }
                else
                {
                    node.fields.Add(pair);
                }
            }
            // Add the prop-unique fields.
            foreach (var pair in prop.fields)
            {
                if (node.fields.FindIndex(x => x.Item1 == pair.Item1) < 0)
                {
                    node.fields.Add(pair);
                }
            }

            return node;
        }

        static Style CollateConfigs(Style prop, Style style)
        {
            // This Style contains the final, collated prop configs.  What we do
            // to create this is iterate over the components of the style.  For
            // each ConfigNode, we scan the prop to see if it is in the prop as well.
            // If it is, we copy fields from the style that are not duplicated
            // in the prop.  We then add any fields in the prop that are not in
            // the collated ConfigNode.
            Style finalConfig = new Style();

            // The algorithm isn't particularly efficient - it's a "prove it works"
            // design that will be improved once I dig through the C# documentation
            // for containers, unless performance is adequate as-is.
            foreach (ConfigNode model in style.model)
            {
                ConfigNode replacement = prop.model.Find(x => x.name == model.name && x.id == model.id);
                if (replacement != null)
                {
                    if (replacement.delete == false)
                    {
                        finalConfig.model.Add(MergeNodes(model, replacement));
                    }
                }
                else
                {
                    finalConfig.model.Add(model);
                }
            }
            foreach (ConfigNode model in prop.model)
            {
                if (finalConfig.model.FindIndex(x => x.name == model.name && x.id == model.id) < 0)
                {
                    if (model.delete == false)
                    {
                        finalConfig.model.Add(model);
                    }
                }
            }

            foreach (ConfigNode node in style.node)
            {
                ConfigNode replacement = prop.node.Find(x => x.name == node.name && x.id == node.id);
                if (replacement != null)
                {
                    if (replacement.delete == false)
                    {
                        finalConfig.node.Add(MergeNodes(node, replacement));
                    }
                }
                else
                {
                    finalConfig.node.Add(node);
                }
            }
            foreach (ConfigNode node in prop.node)
            {
                if (finalConfig.node.FindIndex(x => x.name == node.name && x.id == node.id) < 0)
                {
                    if (node.delete == false)
                    {
                        finalConfig.node.Add(node);
                    }
                }
            }

            foreach (ConfigNode module in style.module)
            {
                ConfigNode replacement = prop.module.Find(x => x.name == module.name && x.id == module.id);
                if (replacement != null)
                {
                    if (replacement.delete == false)
                    {
                        finalConfig.module.Add(MergeNodes(module, replacement));
                    }
                }
                else
                {
                    finalConfig.module.Add(module);
                }
            }
            foreach (ConfigNode module in prop.module)
            {
                if (finalConfig.module.FindIndex(x => x.name == module.name && x.id == module.id) < 0)
                {
                    if (module.delete == false)
                    {
                        finalConfig.module.Add(module);
                    }
                }
            }

            return finalConfig;
        }

        /// <summary>
        /// Generate the prop config.
        /// </summary>
        /// <param name="propName"></param>
        /// <param name="selectedStyle"></param>
        /// <param name="startupScript"></param>
        /// <param name="elem"></param>
        /// <param name="writeDirectory"></param>
        /// <param name="ifOlderThan">Date/time stamp of the source config.  The prop is only written if its older than this timestamp.</param>
        static void GenerateProp(string propName, Style selectedStyle, string startupScript, XmlElement elem, string writeDirectory, DateTime ifOlderThan)
        {
            string fileName = writeDirectory + propName.Replace('.', '_') + ".cfg";

            if (forceRebuild == false)
            {
                bool writeFile = true;
                if (File.Exists(fileName))
                {
                    DateTime fileModified = File.GetLastWriteTimeUtc(fileName);
                    writeFile = (fileModified < ifOlderThan);
                }
                if (!writeFile)
                {
                    return;
                }
            }

            Style propChanges = new Style();
            ProcessProp(elem, propChanges);

            Style finalConfig = CollateConfigs(propChanges, selectedStyle);
            if (finalConfig != null)
            {
                StringBuilder prop = new StringBuilder(4096);

                prop.AppendLine("PROP").AppendLine("{").AppendFormat("\tname = {0}", propName).AppendLine().AppendLine();

                // Write MODEL nodes
                foreach (var model in finalConfig.model)
                {
                    if (!string.IsNullOrEmpty(model.comment))
                    {
                        prop.AppendFormat("\t// {0}", model.comment).AppendLine();
                    }
                    prop.AppendLine("\tMODEL").AppendLine("\t{");

                    foreach (var field in model.fields)
                    {
                        prop.AppendFormat("\t\t{0} = {1}", field.Item1, field.Item2).AppendLine();
                    }

                    prop.AppendLine("\t}").AppendLine();
                }

                // Write the MASComponent
                prop.AppendLine("\tMODULE").AppendLine("\t{").AppendLine("\t\tname = MASComponent").AppendLine();
                if (!string.IsNullOrEmpty(startupScript))
                {
                    prop.AppendFormat("\t\tstartupScript = {0}", startupScript).AppendLine().AppendLine();
                }

                foreach (var node in finalConfig.node)
                {
                    if (!string.IsNullOrEmpty(node.comment))
                    {
                        prop.AppendFormat("\t\t// {0}", node.comment).AppendLine();
                    }
                    prop.AppendFormat("\t\t{0}", node.name).AppendLine().AppendLine("\t\t{");

                    foreach (var field in node.fields)
                    {
                        prop.AppendFormat("\t\t\t{0} = {1}", field.Item1, field.Item2).AppendLine();
                    }

                    prop.AppendLine("\t\t}").AppendLine();
                }
                prop.AppendLine("\t}");

                // Write any optional modules
                foreach (var module in finalConfig.module)
                {
                    prop.AppendLine();
                    if (!string.IsNullOrEmpty(module.comment))
                    {
                        prop.AppendFormat("\t// {0}", module.comment).AppendLine();
                    }
                    prop.AppendLine("\tMODULE").AppendLine("\t{");
                    foreach (var field in module.fields)
                    {
                        prop.AppendFormat("\t\t{0} = {1}", field.Item1, field.Item2).AppendLine();
                    }
                    prop.AppendLine("\t}");
                }
                prop.AppendLine("}");

                Directory.CreateDirectory(writeDirectory);
                File.WriteAllText(fileName, prop.ToString(), Encoding.UTF8);
            }
        }

        /// <summary>
        /// Process a prop group (logical collection of related props).
        /// 
        /// Only writes props whose configs are older than their source XML file.
        /// </summary>
        /// <param name="group">The prop group</param>
        /// <param name="ifOlderThan">The date stamp on the source XML file.</param>
        static void ProcessPropGroup(XmlElement group, DateTime ifOlderThan)
        {
            string writeDirectory = group.GetAttribute("folder");
            if (string.IsNullOrEmpty(writeDirectory))
            {
                // If the write directory is unspecified, set it to the
                // current working directory.
                writeDirectory = ".";
            }
            writeDirectory = writeDirectory + "\\";

            Console.WriteLine("Processing {0} group props", group.Name);

            List<Style> style = new List<Style>();
            // Find all of the style elements
            foreach (XmlNode child in group)
            {
                if (child is XmlElement)
                {
                    XmlElement elem = child as XmlElement;
                    if (elem.Name == "style")
                    {
                        string styleName = elem.GetAttribute("name");
                        if (!string.IsNullOrEmpty(styleName))
                        {
                            Style st = new Style();
                            st.name = styleName;
                            ProcessStyle(elem, st);
                            if (style.FindIndex(x => x.name == st.name) < 0)
                            {
                                style.Add(st);
                            }
                            else
                            {
                                Console.WriteLine("Duplicate style {0} ignored (accidental duplicate?).", styleName);
                            }
                        }
                    }
                }
            }

            // Now process props.
            foreach (XmlNode child in group)
            {
                if (child is XmlElement)
                {
                    XmlElement elem = child as XmlElement;
                    if (elem.Name == "prop")
                    {
                        XmlElement nameElem = elem["name"];
                        if (nameElem == null)
                        {
                            Console.WriteLine("<prop> found that does not have a <name>");
                        }
                        else
                        {
                            string propName = nameElem.InnerText.Replace(' ', '_');

                            XmlElement styleElem = elem["style"];
                            Style selectedStyle;
                            if (styleElem == null)
                            {
                                // empty style
                                selectedStyle = new Style();
                            }
                            else
                            {
                                string styleName = styleElem.InnerText;
                                selectedStyle = style.Find(x => x.name == styleName);
                                if (selectedStyle == null)
                                {
                                    Console.WriteLine("Unable to find <style> \"{0}\" for <prop> {1}", styleName, propName);
                                }
                            }

                            if (selectedStyle != null)
                            {
                                XmlElement startupElem = elem["startupScript"];
                                string startupScript = string.Empty;
                                if (startupElem != null)
                                {
                                    startupScript = startupElem.InnerText;
                                }

                                GenerateProp(propName, selectedStyle, startupScript, elem, writeDirectory, ifOlderThan);
                            }
                        }
                    }
                }
            }
        }
    }
}
