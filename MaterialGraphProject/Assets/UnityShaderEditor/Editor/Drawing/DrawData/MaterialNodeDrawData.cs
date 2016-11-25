using System;
using UnityEditor.Graphing.Drawing;
using UnityEngine.Graphing;
using UnityEngine.MaterialGraph;

namespace UnityEditor.MaterialGraph.Drawing
{
    [Serializable]
    public class MaterialNodeDrawData : NodeDrawData
    {
        NodePreviewDrawData nodePreviewDrawData;

        public bool requiresTime
        {
            get { return node is IRequiresTime; }
        }

        public override void OnModified(ModificationScope scope)
        {
            base.OnModified(scope);
            // TODO: Propagate callback rather than setting property
            if (nodePreviewDrawData != null)
                nodePreviewDrawData.modificationScope = scope;
        }

        protected MaterialNodeDrawData()
        {}

        public override void Initialize(INode inNode,AbstractGraphDataSource dataSource)
        {
            base.Initialize(inNode, dataSource);
            AddPreview(inNode);
        }

        private void AddPreview(INode inNode)
        {
            var materialNode = inNode as AbstractMaterialNode;
            if (materialNode == null || !materialNode.hasPreview)
                return;

            nodePreviewDrawData = CreateInstance<NodePreviewDrawData>();
            nodePreviewDrawData.Initialize(materialNode);
            m_Children.Add(nodePreviewDrawData);
        }
    }
}
