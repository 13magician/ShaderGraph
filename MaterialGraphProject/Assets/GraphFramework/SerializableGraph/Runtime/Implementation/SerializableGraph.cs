using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.Graphing
{
    [Serializable]
    public class SerializableGraph : IGraph, ISerializationCallbackReceiver
    {
        [NonSerialized]
        List<IEdge> m_Edges = new List<IEdge>();

        [NonSerialized]
        Dictionary<Guid, List<IEdge>> m_NodeEdges = new Dictionary<Guid, List<IEdge>>();

        [NonSerialized]
        Dictionary<Guid, INode> m_Nodes = new Dictionary<Guid, INode>();

        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializableNodes = new List<SerializationHelper.JSONSerializedElement>();

        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializableEdges = new List<SerializationHelper.JSONSerializedElement>();

        public IEnumerable<T> GetNodes<T>() where T : INode
        {
            return m_Nodes.Values.OfType<T>();
        }

        public IEnumerable<IEdge> edges
        {
            get { return m_Edges; }
        }

        public virtual void AddNode(INode node)
        {
            m_Nodes.Add(node.guid, node);
            node.owner = this;
            NotifyChange(new NodeAddedGraphChange(node));
            ValidateGraph();
        }

        public virtual void RemoveNode(INode node)
        {
            if (!node.canDeleteNode)
                return;

            m_Nodes.Remove(node.guid);
            ValidateGraph();
            NotifyChange(new NodeRemovedGraphChange(node));
        }

        void RemoveNodeNoValidate(INode node)
        {
            if (!node.canDeleteNode)
                return;

            m_Nodes.Remove(node.guid);
            //NotifyChange(new NodeRemovedGraphChange(node));
        }

        void AddEdgeToNodeEdges(IEdge edge)
        {
            List<IEdge> inputEdges;
            if (!m_NodeEdges.TryGetValue(edge.inputSlot.nodeGuid, out inputEdges))
                m_NodeEdges[edge.inputSlot.nodeGuid] = inputEdges = new List<IEdge>();
            inputEdges.Add(edge);

            List<IEdge> outputEdges;
            if (!m_NodeEdges.TryGetValue(edge.outputSlot.nodeGuid, out outputEdges))
                m_NodeEdges[edge.outputSlot.nodeGuid] = outputEdges = new List<IEdge>();
            outputEdges.Add(edge);
        }

        public virtual Dictionary<SerializationHelper.TypeSerializationInfo, SerializationHelper.TypeSerializationInfo> GetLegacyTypeRemapping()
        {
            return new Dictionary<SerializationHelper.TypeSerializationInfo, SerializationHelper.TypeSerializationInfo>();
        }

        public virtual IEdge Connect(SlotReference fromSlotRef, SlotReference toSlotRef)
        {
            if (fromSlotRef == null || toSlotRef == null)
                return null;

            var fromNode = GetNodeFromGuid(fromSlotRef.nodeGuid);
            var toNode = GetNodeFromGuid(toSlotRef.nodeGuid);

            if (fromNode == null || toNode == null)
                return null;

            // if fromNode is already connected to toNode
            // do now allow a connection as toNode will then
            // have an edge to fromNode creating a cycle.
            // if this is parsed it will lead to an infinite loop.
            var dependentNodes = new List<INode>();
            NodeUtils.CollectNodesNodeFeedsInto(dependentNodes, toNode);
            if (dependentNodes.Contains(fromNode))
                return null;

            var fromSlot = fromNode.FindSlot<ISlot>(fromSlotRef.slotId);
            var toSlot = toNode.FindSlot<ISlot>(toSlotRef.slotId);

            SlotReference outputSlot = null;
            SlotReference inputSlot = null;

            // output must connect to input
            if (fromSlot.isOutputSlot)
                outputSlot = fromSlotRef;
            else if (fromSlot.isInputSlot)
                inputSlot = fromSlotRef;

            if (toSlot.isOutputSlot)
                outputSlot = toSlotRef;
            else if (toSlot.isInputSlot)
                inputSlot = toSlotRef;

            if (inputSlot == null || outputSlot == null)
                return null;

            var slotEdges = GetEdges(inputSlot).ToList();
            // remove any inputs that exits before adding
            foreach (var edge in slotEdges)
            {
                RemoveEdgeNoValidate(edge);
            }

            var newEdge = new Edge(outputSlot, inputSlot);
            m_Edges.Add(newEdge);
            NotifyChange(new EdgeAddedGraphChange(newEdge));
            AddEdgeToNodeEdges(newEdge);

            Debug.Log("Connected edge: " + newEdge);
            ValidateGraph();
            return newEdge;
        }

        public virtual void RemoveEdge(IEdge e)
        {
            RemoveEdgeNoValidate(e);
            ValidateGraph();
        }

        public void RemoveElements(IEnumerable<INode> nodes, IEnumerable<IEdge> edges)
        {
            foreach (var edge in edges.ToArray())
                RemoveEdgeNoValidate(edge);

            foreach (var serializableNode in nodes.ToArray())
                RemoveNodeNoValidate(serializableNode);

            ValidateGraph();

            foreach (var serializableNode in nodes.ToArray())
                NotifyChange(new NodeRemovedGraphChange(serializableNode));
        }

        void RemoveEdgeNoValidate(IEdge e)
        {
            m_Edges.Remove(e);

            List<IEdge> inputNodeEdges;
            if (m_NodeEdges.TryGetValue(e.inputSlot.nodeGuid, out inputNodeEdges))
                inputNodeEdges.Remove(e);

            List<IEdge> outputNodeEdges;
            if (m_NodeEdges.TryGetValue(e.outputSlot.nodeGuid, out outputNodeEdges))
                outputNodeEdges.Remove(e);
            
            NotifyChange(new EdgeRemovedGraphChange(e));
        }

        public INode GetNodeFromGuid(Guid guid)
        {
            INode node;
            m_Nodes.TryGetValue(guid, out node);
            return node;
        }

        public bool ContainsNodeGuid(Guid guid)
        {
            return m_Nodes.ContainsKey(guid);
        }

        public T GetNodeFromGuid<T>(Guid guid) where T : INode
        {
            var node = GetNodeFromGuid(guid);
            if (node is T)
                return (T)node;
            return default(T);
        }

        public IEnumerable<IEdge> GetEdges(SlotReference s)
        {
            if (s == null)
                return Enumerable.Empty<IEdge>();

            List<IEdge> candidateEdges;
            if (!m_NodeEdges.TryGetValue(s.nodeGuid, out candidateEdges))
                return Enumerable.Empty<IEdge>();

            return candidateEdges.Where(x =>
                (x.outputSlot.nodeGuid == s.nodeGuid && x.outputSlot.slotId == s.slotId)
                || x.inputSlot.nodeGuid == s.nodeGuid && x.inputSlot.slotId == s.slotId);
        }

        public virtual void OnBeforeSerialize()
        {
            m_SerializableNodes = SerializationHelper.Serialize<INode>(m_Nodes.Values);
            m_SerializableEdges = SerializationHelper.Serialize<IEdge>(m_Edges);
        }

        public virtual void OnAfterDeserialize()
        {
            var nodes = SerializationHelper.Deserialize<INode>(m_SerializableNodes, GetLegacyTypeRemapping());
            m_Nodes = new Dictionary<Guid, INode>(nodes.Count);
            foreach (var node in nodes)
            {
                node.owner = this;
                node.UpdateNodeAfterDeserialization();
                m_Nodes.Add(node.guid, node);
            }

            m_SerializableNodes = null;

            m_Edges = SerializationHelper.Deserialize<IEdge>(m_SerializableEdges, null);
            m_SerializableEdges = null;
            foreach (var edge in m_Edges)
                AddEdgeToNodeEdges(edge);

            ValidateGraph();
        }

        public virtual void ValidateGraph()
        {
            //First validate edges, remove any
            //orphans. This can happen if a user
            //manually modifies serialized data
            //of if they delete a node in the inspector
            //debug view.
            foreach (var edge in edges.ToArray())
            {
                var outputNode = GetNodeFromGuid(edge.outputSlot.nodeGuid);
                var inputNode = GetNodeFromGuid(edge.inputSlot.nodeGuid);

                if (outputNode == null
                    || inputNode == null
                    || outputNode.FindOutputSlot<ISlot>(edge.outputSlot.slotId) == null
                    || inputNode.FindInputSlot<ISlot>(edge.inputSlot.slotId) == null)
                {
                    //orphaned edge
                    RemoveEdgeNoValidate(edge);
                }
            }

            foreach (var node in GetNodes<INode>())
                node.ValidateNode();
        }

        public void OnEnable()
        {
            foreach (var node in GetNodes<INode>().OfType<IOnAssetEnabled>())
            {
                node.OnEnable();
            }
        }

        public OnGraphChange onChange { get; set; }

        void NotifyChange(GraphChange change)
        {
            if (onChange != null)
                onChange(change);
        }
    }
}
