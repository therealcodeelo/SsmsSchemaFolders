using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SsmsSchemaFolders
{
    /// <summary>
    /// Used to organize Databases and Tables in Object Explorer into groups
    /// </summary>
    public class ObjectExplorerExtender : IObjectExplorerExtender
    {

        private ISchemaFolderOptions Options { get; }
        private IServiceProvider Package { get; }
        //private Regex NodeSchemaRegex;


        /// <summary>
        /// 
        /// </summary>
        public ObjectExplorerExtender(IServiceProvider package, ISchemaFolderOptions options)
        {
            Package = package;
            Options = options;
            //NodeSchemaRegex = new Regex(@"@Schema='((''|[^'])+)'");
        }


        public string GetFolderName(TreeNode node, int folderLevel, bool quickSchemaName)
        {
            FolderType folderType = FolderType.None;
            switch (folderLevel)
            {
                case 1:
                    folderType = Options.Level1FolderType;
                    break;

                case 2:
                    folderType = Options.Level2FolderType;
                    break;
            }
            switch (folderType)
            {
                case FolderType.Schema:
                    string schema = (quickSchemaName) ? GetNodeSchemaQuick(node) : GetNodeSchema(node);

                    if (schema != null && Options.Level1FolderType == Options.Level2FolderType)
                    {
                        int dotIndex = schema.IndexOf('.');
                        if (dotIndex != -1)
                        {
                            schema = (folderLevel == 1) ? schema.Substring(0, dotIndex) : schema.Substring(dotIndex + 1);
                        }
                        else
                        {
                            if (folderLevel == 2)
                                // Already sorted by schema. Don't add again.
                                return null;
                        }
                    }
                    return schema;

                case FolderType.Alphabetical:
                    var name = GetNodeName(node);
                    //debug_message("{0} > {1}", node.Text, name);

                    if (!string.IsNullOrEmpty(name))
                    {
                        return name.Substring(0, 1).ToUpper();
                    }
                    break;

            }
            return null;
        }

        private int GetFolderLevelMinNodeCount(int folderLevel)
        {
            switch (folderLevel)
            {
                case 1:
                    return Options.Level1MinNodeCount;

                case 2:
                    return Options.Level2MinNodeCount;
            }
            return 0;
        }

        /// <summary>
        /// Gets the underlying object which is responsible for displaying object explorer structure
        /// </summary>
        /// <returns></returns>
        public TreeView GetObjectExplorerTreeView()
        {
            var objectExplorerService = (IObjectExplorerService)Package.GetService(typeof(IObjectExplorerService));
            if (objectExplorerService != null)
            {
                var oesTreeProperty = objectExplorerService.GetType().GetProperty("Tree", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (oesTreeProperty != null)
                    return (TreeView)oesTreeProperty.GetValue(objectExplorerService, null);
                //else
                //    debug_message("Object Explorer Tree property not found.");
            }
            //else
            //    debug_message("objectExplorerService == null");

            return null;
        }

        /// <summary>
        /// Gets node information from underlying type of tree node view
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <remarks>Copy of private method in ObjectExplorerService</remarks>
        private INodeInformation GetNodeInformation(TreeNode node)
        {
            INodeInformation result = null;
            IServiceProvider serviceProvider = node as IServiceProvider;
            if (serviceProvider != null)
            {
                result = (serviceProvider.GetService(typeof(INodeInformation)) as INodeInformation);
                //debug_message(node.Text);
                //debug_message("NodeInformation\n UrnPath:{0}\n Name:{1}\n InvariantName:{2}\n Context:{3}\n NavigationContext:{4}", result.UrnPath, result.Name, result.InvariantName, result.Context, result.NavigationContext);
            }
            return result;
        }

        public bool GetNodeExpanding(TreeNode node)
        {
            var lazyNode = node as ILazyLoadingNode;
            if (lazyNode != null)
                return lazyNode.Expanding;
            else
                return false;
        }

        public string GetNodeUrnPath(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
                return ni.UrnPath;
            else
                return null;
        }

        private String GetNodeFullName(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
            {
                return ni.InvariantName;
            }
            return null;
        }

        private String GetNodeName(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            // Only return name if object is schema bound.
            if (ni != null && ni.Context.Contains("@Schema="))
            {
                return ni.Name;
            }
            return null;
        }

        private String GetNodeSchemaQuick(TreeNode node)
        {
            var dotIndex = node.Text.IndexOf('.');
            if (dotIndex != -1)
                return node.Text.Substring(0, dotIndex);
            else
                return null;
        }

        private String GetNodeSchema(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
            {
                // parse ni.Context = Server[@Name='NR-DEV\SQL2008R2EXPRESS']/Database[@Name='tempdb']/Table[@Name='test.''escape''[value]' and @Schema='dbo']
                // or compare ni.Name vs ni.InvariantName = ObjectName vs SchemaName.ObjectName

                //var match = NodeSchemaRegex.Match(ni.Context);
                //if (match.Success)
                //    return match.Groups[1].Value;

                if (ni.InvariantName.EndsWith("." + ni.Name))
                    return ni.InvariantName.Substring(0, ni.InvariantName.Length - ni.Name.Length - 1);
            }
            return null;
        }

        /// <summary>
        /// Removes schema name from object node.
        /// </summary>
        /// <param name="node">Object node to rename</param>
        public void RenameNode(TreeNode node)
        {
            if (GetNodeName(node) is string name)
            {
                node.Text = name;
            }
        }

        /// <summary>
        /// Removes schema name from object node.
        /// </summary>
        /// <param name="node">Object node to rename</param>
        /// <param name="quick">Use quick substring method</param>
        private void RenameNode(TreeNode node, bool quick)
        {
            if (quick)
            {
                // Simple method, doesn't work correctly when schema name contains a dot.
                node.Text = node.Text.Substring(node.Text.IndexOf('.') + 1);
            }
            else
            {
                RenameNode(node);
            }
        }

        /// <summary>
        /// Create schema nodes and move tables, functions and stored procedures under its schema node
        /// </summary>
        /// <param name="node">Table node to reorganize</param>
        /// <param name="nodeTag">Tag of new node</param>
        /// <returns>The count of schema nodes.</returns>
        public int ReorganizeNodes(TreeNode node, string nodeTag, Dictionary<string, string> dictionary)
        {
            if (node.Nodes.Count <= 1)
                // 1 is the lazy expanding placeholder node.
                return 0;

            if (Options.UseClear > 0 && node.Nodes.Count >= Options.UseClear)
                //BUG: Doesn't support folder levels. Need to rewrite.
                return ReorganizeNodesWithClear(node, nodeTag);

            return ReorganizeNodes(node, nodeTag, 1, dictionary);
        }

        /// <summary>
        /// Create schema nodes and move tables, functions and stored procedures under its schema node
        /// </summary>
        /// <param name="node">Table node to reorganize</param>
        /// <param name="nodeTag">Tag of new node</param>
        /// <param name="folderLevel">The folder level of the current node</param>
        /// <returns>The count of schema nodes.</returns>
        private int ReorganizeNodes(TreeNode node, string nodeTag, int folderLevel, Dictionary<string,string> dictionary)
        {
            if (node.Nodes.Count < GetFolderLevelMinNodeCount(folderLevel))
                return 0;
            
            var nodeText = node.Text;
            node.Text += " (sorting...)";

            var quickAndDirty = (Options.QuickSchema > 0 && node.Nodes.Count > Options.QuickSchema);


            node.TreeView.BeginUpdate();

            var unresponsive = Stopwatch.StartNew();


            var folders = new SortedDictionary<string, List<TreeNode>>();
            int folderNodeIndex = -1;
            var newFolderNodes = new List<TreeNode>();

            Dictionary<string, TreeNode> lastNodes = new Dictionary<string, TreeNode>();
            foreach (TreeNode childNode in node.Nodes)
            {
                TreeNode secondLevel, thirdLevel, fourthLevel = null;
                //skip schema node folders but make sure they are in the folders list
                if (childNode.Tag != null && childNode.Tag.ToString() == nodeTag)
                {
                    if (!folders.ContainsKey(childNode.Name))
                        folders.Add(childNode.Name, new List<TreeNode>());

                    folderNodeIndex = childNode.Index;
                    continue;
                }

                string folderName = GetFolderName(childNode, folderLevel, quickAndDirty);

                if (string.IsNullOrEmpty(folderName))
                    continue;
                //create schema node
                if (!node.Nodes.ContainsKey(folderName))
                {
                    TreeNode folderNode;
                    if (Options.CloneParentNode)
                    {
                        folderNode = new SchemaFolderTreeNode(node);
                        node.Nodes.Add(folderNode);
                    }
                    else
                    {
                        folderNode = node.Nodes.Add(folderName);
                    }
                    newFolderNodes.Add(folderNode);

                    folderNode.Name = folderName;
                    folderNode.Text = folderName;
                    folderNode.Tag = nodeTag;

                    if (Options.AppendDot)
                        folderNode.Text += ".";

                    if (Options.UseObjectIcon)
                    {
                        folderNode.ImageIndex = childNode.ImageIndex;
                        folderNode.SelectedImageIndex = childNode.ImageIndex;
                    }
                    else
                    {
                        folderNode.ImageIndex = node.ImageIndex;
                        folderNode.SelectedImageIndex = node.ImageIndex;
                    }
                    if(dictionary != null)
                    {
                        if(dictionary.Count == 0)
                        {
                            lastNodes.Add(folderName, folderNode);
                        }
                        else
                        {
                            var pattern = dictionary.Where(x=>x.Key == folderName).FirstOrDefault().Value;
                            if(pattern != null)
                            {
                                int currentIndex = 0;
                                string schema = string.Empty;

                                // Используем регулярные выражения для извлечения значений из строки
                                string regex = @"{(.*?)}"; // Паттерн для извлечения значения внутри {}
                                Match match = Regex.Match(pattern, regex);
                                string p1 = match.Groups[1].Value; // Значение внутри {}

                                regex = @"\[c(\d+)\]"; // Паттерн для извлечения числовых значений внутри []
                                MatchCollection matches = Regex.Matches(pattern, regex);

                                // Присваиваем значения переменным
                                int p2 = Convert.ToInt32(matches[0].Groups[1].Value);
                                int p3 = Convert.ToInt32(matches[1].Groups[1].Value);
                                int p4 = Convert.ToInt32(matches[2].Groups[1].Value);
                                int p5 = Convert.ToInt32(matches[3].Groups[1].Value);

                                //string p1 = "_.";
                                //int p2 = 3;
                                //int p3 = 4;
                                //int p4 = 3;
                                //int p5 = 999;

                                foreach (char c in folderName)
                                {
                                    if (!p1.Contains(c))
                                    {
                                        schema += c;
                                    }
                                }
                                folderNode.Text = schema.Substring(currentIndex, p2);
                                currentIndex += p2;
                                secondLevel = new TreeNode(schema.Substring(currentIndex, p3));
                                currentIndex += p3;
                                thirdLevel = new TreeNode(schema.Substring(currentIndex, p4));
                                currentIndex += p4;
                                var length = schema.Length - currentIndex > p5 ? p5 : schema.Length - currentIndex;
                                fourthLevel = new TreeNode(schema.Substring(currentIndex, length));

                                folderNode.Nodes.Add(secondLevel);
                                secondLevel.Nodes.Add(thirdLevel);
                                thirdLevel.Nodes.Add(fourthLevel);

                                lastNodes.Add(folderName, fourthLevel);
                            }
                            else
                            {
                                lastNodes.Add(folderName, folderNode);
                            }
                        }
                    }
                }

                //add node to folder list
                List<TreeNode> folderNodeList;
                if (!folders.TryGetValue(folderName, out folderNodeList))
                {
                    folderNodeList = new List<TreeNode>();
                    folders.Add(folderName, folderNodeList);
                }
                folderNodeList.Add(childNode);

                if (unresponsive.ElapsedMilliseconds > Options.UnresponsiveTimeout)
                {
                    node.TreeView.EndUpdate();
                    Application.DoEvents();
                    if (node.TreeView == null)
                        return 0;
                    node.TreeView.BeginUpdate();
                    unresponsive.Restart();
                }
            }

            if (folderNodeIndex >= 0)
            {
                // Move schema nodes to top of tree
                foreach (var folderNode in newFolderNodes)
                {
                    node.Nodes.Remove(folderNode);
                    node.Nodes.Insert(++folderNodeIndex, folderNode);
                }
            }

            //BUG: Alphabetical may not be in order if no schema folder.
            bool sortRequired = (folderLevel == 1 && Options.Level1FolderType == FolderType.Alphabetical);

            //move nodes to schema node
            foreach (string nodeName in folders.Keys)
            {
                var folderNode = node.Nodes[nodeName];
                if (sortRequired)
                {
                    node.Nodes.Remove(folderNode);
                    node.Nodes.Add(folderNode);
                }
                foreach (TreeNode childNode in folders[nodeName])
                {
                    var currentLastNode = lastNodes.Where(x => x.Key == nodeName).FirstOrDefault().Value;
                    if (currentLastNode == null)
                        continue;

                    node.Nodes.Remove(childNode);

                    if (Options.RenameNode)
                    {
                        // Note: Node is renamed back to orginal after expanding.
                        RenameNode(childNode, quickAndDirty);
                    }
                    var foler = nodeName + ".";
                    childNode.Text = childNode.Text.Replace(foler, string.Empty);
                    currentLastNode.Nodes.Add(childNode);
                    //folderNode.Nodes.Add(childNode);
                    if (unresponsive.ElapsedMilliseconds > Options.UnresponsiveTimeout)
                    {
                        node.TreeView.EndUpdate();
                        Application.DoEvents();
                        if (node.TreeView == null)
                            return 0;
                        node.TreeView.BeginUpdate();
                        unresponsive.Restart();
                    }
                }
            }

            node.TreeView.EndUpdate();
            node.Text = nodeText;
            unresponsive.Stop();

            //process next folder level
            if (folderLevel < 2)
                foreach (string nodeName in folders.Keys)
                    ReorganizeNodes(node.Nodes[nodeName], nodeTag, folderLevel + 1, dictionary);

            return folders.Count;
        }

        /// <summary>
        /// Create schema nodes and move tables, functions and stored procedures under its schema node
        /// </summary>
        /// <param name="node">Table node to reorganize</param>
        /// <param name="nodeTag">Tag of new node</param>
        /// <returns>The count of schema nodes.</returns>
        public int ReorganizeNodesWithClear(TreeNode node, string nodeTag)
        {
            debug_message("ReorganizeNodesWithClear");

            var nodeText = node.Text;
            node.Text += " (sorting...)";
            node.TreeView.Update();

            var quickAndDirty = (Options.QuickSchema > 0 && node.Nodes.Count > Options.QuickSchema);

            var sw = Stopwatch.StartNew();
            //debug_message("Sort Nodes:{0}", sw.ElapsedMilliseconds);

            var schemas = new Dictionary<string, List<TreeNode>>();
            var schemaNodes = new Dictionary<string, TreeNode>();
            var nodeNodes = new List<TreeNode>();

            foreach (TreeNode childNode in node.Nodes)
            {
                // schema node folder
                if (childNode.Tag != null && childNode.Tag.ToString() == nodeTag)
                {
                    schemas.Add(childNode.Name, new List<TreeNode>());
                    schemaNodes.Add(childNode.Name, childNode);
                    nodeNodes.Add(childNode);
                    continue;
                }

                var schema = (quickAndDirty) ? GetNodeSchemaQuick(childNode) : GetNodeSchema(childNode);

                // other folder
                if (string.IsNullOrEmpty(schema))
                {
                    nodeNodes.Add(childNode);
                    continue;
                }

                List<TreeNode> schemaNodeList;
                if (schemas.TryGetValue(schema, out schemaNodeList))
                {
                    // add to existing schema
                    schemaNodeList.Add(childNode);
                }
                else
                {
                    // add to new schema
                    schemaNodeList = new List<TreeNode>();
                    schemaNodeList.Add(childNode);

                    schemas.Add(schema, schemaNodeList);

                    // create schema folder
                    TreeNode schemaNode;
                    if (Options.CloneParentNode)
                    {
                        schemaNode = new SchemaFolderTreeNode(node);
                    }
                    else
                    {
                        schemaNode = new TreeNode(schema);
                    }
                    schemaNodes.Add(schema, schemaNode);
                    nodeNodes.Add(schemaNode);

                    schemaNode.Name = schema;
                    schemaNode.Text = schema;
                    schemaNode.Tag = nodeTag;

                    if (Options.AppendDot)
                        schemaNode.Text += ".";

                    if (Options.UseObjectIcon)
                    {
                        schemaNode.ImageIndex = childNode.ImageIndex;
                        schemaNode.SelectedImageIndex = childNode.ImageIndex;
                    }
                    else
                    {
                        schemaNode.ImageIndex = node.ImageIndex;
                        schemaNode.SelectedImageIndex = node.ImageIndex;
                    }
                }
            }

            //debug_message("Clear Nodes:{0}", sw.ElapsedMilliseconds);

            //node.TreeView.BeginUpdate();
            node.Text = nodeText + " (clearing...)";
            node.TreeView.Update();
            node.Nodes.Clear();

            //debug_message("DoEvents:{0}", sw.ElapsedMilliseconds);

            if (sw.ElapsedMilliseconds > Options.UnresponsiveTimeout)
            {
                Application.DoEvents();
                if (node.TreeView == null)
                    return 0;
            }

            node.Text = nodeText + " (adding...)";
            node.TreeView.Update();

            //debug_message("Add schemaNode.Nodes:{0}", sw.ElapsedMilliseconds);

            foreach (string schema in schemas.Keys)
            {
                schemaNodes[schema].Nodes.AddRange(schemas[schema].ToArray());
            }

            //debug_message("Add node.Nodes:{0}", sw.ElapsedMilliseconds);

            node.Nodes.AddRange(nodeNodes.ToArray());
            node.Text = nodeText;

            //debug_message("EndUpdate:{0}", sw.ElapsedMilliseconds);

            //node.TreeView.EndUpdate();

            //debug_message("Done:{0}", sw.ElapsedMilliseconds);
            sw.Stop();

            return schemas.Count;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void debug_message(string message)
        {
            if (Package is IDebugOutput)
            {
                ((IDebugOutput)Package).debug_message(message);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void debug_message(string message, params object[] args)
        {
            if (Package is IDebugOutput)
            {
                ((IDebugOutput)Package).debug_message(message, args);
            }
        }

    }

}
