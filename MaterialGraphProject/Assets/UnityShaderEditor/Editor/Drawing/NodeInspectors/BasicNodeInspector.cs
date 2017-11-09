using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Drawing.Inspector;

namespace UnityEditor.ShaderGraph.Drawing
{
    public class BasicNodeInspector : AbstractNodeInspector
    {
        public override void OnInspectorGUI()
        {
            GUILayout.Label(node.name, EditorStyles.boldLabel); 

            GUILayout.Space(10);

            var scope = DoSlotsUI();

            var ui = node as ICustomNodeUI;
            if (ui != null)
            {
                var scope2 = ui.DrawCustomNodeUI();
                if (scope2 > scope)
                    scope = scope2;
            }

            if (scope == ModificationScope.Graph || scope == ModificationScope.Topological)
                node.owner.ValidateGraph();

            if (scope > ModificationScope.Nothing && node.onModified != null)
                node.onModified(node, scope);
        }

        protected virtual ModificationScope DoSlotsUI()
        {
            var slots = node.GetSlots<MaterialSlot>();
            if (!slots.Any())
                return ModificationScope.Nothing;

            GUILayout.Label("Default Slot Values", EditorStyles.boldLabel);

            var modified = false;
            foreach (var slot in node.GetSlots<MaterialSlot>())
                modified |= IMGUISlotEditorView.SlotField(slot);

            GUILayout.Space(10);

            return modified ? ModificationScope.Node : ModificationScope.Nothing;
        }
    }
}
