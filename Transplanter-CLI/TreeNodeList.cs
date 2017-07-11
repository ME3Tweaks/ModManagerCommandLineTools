using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransplanterLib
{
    public class TreeNodeList : List<TreeNode>
    {
        public TreeNode containingnode = null;
        public new void Add(TreeNode node)
        {
            node.Parent = this.containingnode;
            base.Add(node);
        }

    }
}
