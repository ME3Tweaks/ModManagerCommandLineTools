using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCCTools
{
    public class TreeNode
    {
        private readonly TreeNodeList _children = new TreeNodeList();
        public Interpreter.nodeType Tag { get; set; }
        public string Text { get; set; }
        public string Name { get; set; }

        public TreeNode(string text)
        {
            Text = text;
            _children.containingnode = this;
        }

        public TreeNode()
        {
            _children.containingnode = this;
        }

        public TreeNode this[int i]
        {
            get { return _children[i]; }
        }

        public TreeNode Parent { get; set; }

        public TreeNodeList Children
        {
            get { return _children; }
        }

        public TreeNodeList Nodes
        {
            get { return _children; }
        }

        public TreeNode Add(string value)
        {
            var node = new TreeNode(value) { Parent = this };
            _children.Add(node);
            return node;
        }

        public TreeNode Add(TreeNode node)
        {
            node.Parent = this;
            _children.Add(node);
            return node;
        }

        public TreeNode[] AddChildren(params string[] values)
        {
            return values.Select(Add).ToArray();
        }

        public bool RemoveChild(TreeNode node)
        {
            return _children.Remove(node);
        }

        public TreeNode LastNode
        {
            get
            {
                return _children.Last();
            }
        }

        public void Remove()
        {
            Parent._children.Remove(this);
        }

        public void PrintPretty(string indent, StreamWriter str, bool last)
        {

            str.Write(indent);
            if (last)
            {
                str.Write("└─");
                indent += "  ";
            }
            else
            {
                str.Write("├─");
                indent += "| ";
            }
            str.Write(Text);
            if (Children.Count > 1000 && Tag == Interpreter.nodeType.ArrayProperty)
            {
                str.Write(" > 1000, (" + Children.Count + ") suppressed.");
                return;
            }

            if (Tag == Interpreter.nodeType.ArrayProperty && (Text.Contains("LookupTable") || Text.Contains("CompressedTrackOffsets")))
            {
                str.Write(" - suppressed by data dumper.");
                return;
            }
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].Tag == Interpreter.nodeType.None)
                {
                    continue;
                }
                str.Write("\n");
                Children[i].PrintPretty(indent, str, i == Children.Count - 1 || (i == Children.Count - 2 && Children[Children.Count - 1].Tag == Interpreter.nodeType.None));
            }
            return;
        }

        internal void PrintMeshViewer(string indent, StreamWriter str, bool value)
        {
            Console.WriteLine(Text);
            foreach (TreeNode Child in Children)
            {
                Child.PrintMeshViewer("", str, true);
            }
            //if (Text.Contains("location"))
            //{
            //    str.Write("(");

            //    foreach (TreeNode child in Children)
            //    {
            //        Console.Write(child.Text);

            //        str.WriteLine(child.Text + " ");

            //    }
            //    str.WriteLine(")");
            //}
            //else
            //{
            //    foreach (TreeNode child in Children)
            //    {
            //        if (child.Text.Contains("location"))
            //        {
            //            Console.Write(child.Text);
            //            child.PrintMeshViewer("", str, true);
            //        }
            //    }
            //}
        }



        //public void Traverse(Action action)
        //{
        //    action(Label);
        //    foreach (var child in _children)
        //        child.Traverse(action);
        //}

        //public IEnumerable Flatten()
        //{
        //    return new[] { Label }.Union(_children.SelectMany(x => x.Flatten()));
        //}
    }


}