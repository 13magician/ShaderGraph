﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Drawing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    public abstract class AbstractLightweightMasterNode : MasterNode, ICustomNodeUI
    {
        private const int kMaxInterpolators = 8;

        [SerializeField]
        private SurfaceMaterialOptions m_MaterialOptions = new SurfaceMaterialOptions();

        protected abstract IEnumerable<int> masterSurfaceInputs { get; }
        protected abstract IEnumerable<int> masterVertexInputs { get; }

        public SurfaceMaterialOptions materialOptions
        {
            get { return m_MaterialOptions; }
            set { m_MaterialOptions = value; }
        }

        protected abstract string GetTemplateName();

        protected virtual void GetLightweightDefinesAndRemap(ShaderGenerator defines, ShaderGenerator surfaceOutputRemap, MasterRemapGraph remapper)
        {
            // Step 1: no remapper, working with raw master node..
            if (remapper == null)
            {
                foreach (var slot in GetInputSlots<MaterialSlot>())
                {
                    surfaceOutputRemap.AddShaderChunk(string.Format("{0} = surf.{0};", slot.shaderOutputName), true);
                }
            }
            // Step 2: remapper present... complex workflow time
            else
            {
                surfaceOutputRemap.AddShaderChunk("{", false);
                surfaceOutputRemap.Indent();

                foreach (var prop in remapper.properties)
                {
                    surfaceOutputRemap.AddShaderChunk(prop.GetInlinePropertyDeclarationString(), true);
                    surfaceOutputRemap.AddShaderChunk(string.Format("{0} = surf.{0};", prop.referenceName), true);
                }

                List<INode> nodes = new List<INode>();
                NodeUtils.DepthFirstCollectNodesFromNode(nodes, this, NodeUtils.IncludeSelf.Exclude);
                foreach (var activeNode in nodes.OfType<AbstractMaterialNode>())
                {
                    if (activeNode is IGeneratesBodyCode)
                        (activeNode as IGeneratesBodyCode).GenerateNodeCode(surfaceOutputRemap, GenerationMode.ForReals);
                }

                foreach (var input in GetInputSlots<MaterialSlot>())
                {
                    foreach (var edge in owner.GetEdges(input.slotReference))
                    {
                        var outputRef = edge.outputSlot;
                        var fromNode = owner.GetNodeFromGuid<AbstractMaterialNode>(outputRef.nodeGuid);
                        if (fromNode == null)
                            continue;

                        surfaceOutputRemap.AddShaderChunk(
                            string.Format("{0} = {1};", input.shaderOutputName,
                                fromNode.GetVariableNameForSlot(outputRef.slotId)), true);
                    }
                }

                surfaceOutputRemap.Deindent();
                surfaceOutputRemap.AddShaderChunk("}", false);
            }
        }

        public override IEnumerable<string> GetSubshader(ShaderGraphRequirements graphRequirements, MasterRemapGraph remapper)
        {
            var tagsVisitor = new ShaderGenerator();
            var blendingVisitor = new ShaderGenerator();
            var cullingVisitor = new ShaderGenerator();
            var zTestVisitor = new ShaderGenerator();
            var zWriteVisitor = new ShaderGenerator();

            materialOptions.GetTags(tagsVisitor);
            materialOptions.GetBlend(blendingVisitor);
            materialOptions.GetCull(cullingVisitor);
            materialOptions.GetDepthTest(zTestVisitor);
            materialOptions.GetDepthWrite(zWriteVisitor);

            var interpolators = new ShaderGenerator();
            var vertexShader = new ShaderGenerator();
            var localPixelShader = new ShaderGenerator();
            var surfaceInputs = new ShaderGenerator();

            ShaderGenerator.GenerateStandardTransforms(
                GetInterpolatorStartIndex(),
                kMaxInterpolators,
                interpolators,
                vertexShader,
                localPixelShader,
                surfaceInputs,
                graphRequirements,
                GetNodeSpecificRequirements(),
                CoordinateSpace.World);

            ShaderGenerator defines = new ShaderGenerator();
            ShaderGenerator surfaceOutputRemap = new ShaderGenerator();
            GetLightweightDefinesAndRemap(defines, surfaceOutputRemap, remapper);

            var templateLocation = ShaderGenerator.GetTemplatePath(GetTemplateName());

            if (!File.Exists(templateLocation))
                return new string[] {};

            var subShaderTemplate = File.ReadAllText(templateLocation);
            var resultShader = subShaderTemplate.Replace("${Defines}", defines.GetShaderString(3));
            resultShader = resultShader.Replace("${Interpolators}", interpolators.GetShaderString(3));
            resultShader = resultShader.Replace("${VertexShader}", vertexShader.GetShaderString(3));
            resultShader = resultShader.Replace("${LocalPixelShader}", localPixelShader.GetShaderString(3));
            resultShader = resultShader.Replace("${SurfaceInputs}", surfaceInputs.GetShaderString(3));
            resultShader = resultShader.Replace("${SurfaceOutputRemap}", surfaceOutputRemap.GetShaderString(3));

            resultShader = resultShader.Replace("${Tags}", tagsVisitor.GetShaderString(2));
            resultShader = resultShader.Replace("${Blending}", blendingVisitor.GetShaderString(2));
            resultShader = resultShader.Replace("${Culling}", cullingVisitor.GetShaderString(2));
            resultShader = resultShader.Replace("${ZTest}", zTestVisitor.GetShaderString(2));
            resultShader = resultShader.Replace("${ZWrite}", zWriteVisitor.GetShaderString(2));
            resultShader = resultShader.Replace("${LOD}", string.Format("{0}", materialOptions.lod));
            return new[] {resultShader};
        }

        protected abstract int GetInterpolatorStartIndex();
        public ModificationScope DrawCustomNodeUI()
        {
            return materialOptions.DoGUI();
        }
    }
}
