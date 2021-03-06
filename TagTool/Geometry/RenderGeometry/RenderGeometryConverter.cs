using TagTool.Cache;
using TagTool.Common;
using TagTool.IO;
using TagTool.Tags;
using TagTool.Tags.Definitions;
using TagTool.Tags.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TagTool.Commands.Porting;
using TagTool.Serialization;
using TagTool.Havok;

namespace TagTool.Geometry
{
    public class RenderGeometryConverter
    {
        private GameCache HOCache { get; }
        private GameCache SourceCache;

        public RenderGeometryConverter(GameCache hoCache, GameCache sourceCache)
        {
            HOCache = hoCache;
            SourceCache = sourceCache;
        }

        /// <summary>
        /// Converts RenderGeometry class in place and returns a new RenderGeometryApiResourceDefinition
        /// </summary>
        public RenderGeometryApiResourceDefinition Convert(RenderGeometry geometry, RenderGeometryApiResourceDefinition resourceDefinition)
        {
            //
            // Convert byte[] of UnknownBlock
            //

            foreach (var block in geometry.Unknown2)
            {
                var data = block.Unknown3;
                if (data != null || data.Length != 0)
                {
                    var result = new byte[data.Length];

                    using (var inputReader = new EndianReader(new MemoryStream(data), SourceCache.Endianness))
                    using (var outputWriter = new EndianWriter(new MemoryStream(result), HOCache.Endianness))
                    {
                        while (!inputReader.EOF)
                            outputWriter.Write(inputReader.ReadUInt32());

                        block.Unknown3 = result;
                    }
                }
            }

            //
            // Convert mopps in cluster visibility
            //

            foreach(var clusterVisibility in geometry.MeshClusterVisibility)
                clusterVisibility.MoppData = HavokConverter.ConvertHkpMoppData(SourceCache.Version, HOCache.Version, clusterVisibility.MoppData);

            //
            // Port resource definition
            //

            var wasNull = false;
            if (resourceDefinition == null)
            {
                wasNull = true;
                Console.Error.WriteLine("Render geometry does not have a valid resource definition, continuing anyway.");
                resourceDefinition = new RenderGeometryApiResourceDefinition
                {
                    VertexBuffers = new TagBlock<D3DStructure<VertexBufferDefinition>>(CacheAddressType.Definition),
                    IndexBuffers = new TagBlock<D3DStructure<IndexBufferDefinition>>(CacheAddressType.Definition)
                };
            }

            geometry.SetResourceBuffers(resourceDefinition);

            // do conversion (PARTICLE INDEX BUFFERS, WATER CONVERSION TO DO) AMBIENT PRT TOO

            var generateParticles = false; // temp fix when pmdf geo is null

            if (wasNull)
            {
                if (geometry.Meshes.Count == 1 && geometry.Meshes[0].Type == VertexType.ParticleModel)
                {
                    generateParticles = true;
                }
                else
                {
                    geometry.Resource = HOCache.ResourceCache.CreateRenderGeometryApiResource(resourceDefinition);
                    geometry.Resource.HaloOnlinePageableResource.Resource.ResourceType = TagResourceTypeGen3.None;
                    return resourceDefinition;
                }
            }

            //
            // Convert Blam data to ElDorado data
            //

            if (generateParticles)
            {
                var mesh = geometry.Meshes[0];
                mesh.Flags |= MeshFlags.MeshIsUnindexed;
                mesh.PrtType = PrtSHType.None;

                var newVertexBuffer = new VertexBufferDefinition
                {
                    Format = VertexBufferFormat.ParticleModel,
                    VertexSize = (short)VertexStreamFactory.Create(HOCache.Version, null).GetVertexSize(VertexBufferFormat.ParticleModel),
                    Data = new TagData
                    {
                        Data = new byte[32],
                        AddressType = CacheAddressType.Data
                    }
                };
                mesh.ResourceVertexBuffers[0] = newVertexBuffer;
            }
            else
            {
                foreach (var mesh in geometry.Meshes)
                {
                    foreach (var vertexBuffer in mesh.ResourceVertexBuffers)
                    {
                        if (vertexBuffer == null)
                            continue;

                        // Gen3 order 0 coefficients are stored in ints but should be read as bytes, 1 per vertex in the original buffer
                        if (vertexBuffer.Format == VertexBufferFormat.AmbientPrt)
                            vertexBuffer.Count = mesh.ResourceVertexBuffers[0].Count;

                        // skip conversion of water vertices, done right after the loop
                        if (vertexBuffer.Format == VertexBufferFormat.Unknown1A || vertexBuffer.Format == VertexBufferFormat.Unknown1B)
                            continue;

                        VertexBufferConverter.ConvertVertexBuffer(SourceCache.Version, HOCache.Version, vertexBuffer);
                    }

                    // convert water vertex buffers
                    if(mesh.ResourceVertexBuffers[6] != null && mesh.ResourceVertexBuffers[7] != null)
                    {
                        // Get total amount of indices and prepare for water conversion

                        int indexCount = 0;
                        foreach (var subpart in mesh.SubParts)
                            indexCount += subpart.IndexCount;

                        WaterConversionData waterData = new WaterConversionData();

                        for (int j = 0; j < mesh.Parts.Count(); j++)
                        {
                            var part = mesh.Parts[j];
                            if(part.FlagsNew.HasFlag(Mesh.Part.PartFlagsNew.IsWaterPart))
                                waterData.PartData.Add(new Tuple<int, int>(part.FirstIndexOld, part.IndexCountOld));
                        }

                        if(waterData.PartData.Count > 1)
                            waterData.Sort();

                        // read all world vertices, unknown1A and unknown1B into lists.
                        List<WorldVertex> worldVertices = new List<WorldVertex>();
                        List<Unknown1B> h3WaterParameters = new List<Unknown1B>();
                        List<Unknown1A> h3WaterIndices = new List<Unknown1A>();

                        using(var stream = new MemoryStream(mesh.ResourceVertexBuffers[0].Data.Data))
                        {
                            var vertexStream = VertexStreamFactory.Create(HOCache.Version, stream);
                            for(int v = 0; v < mesh.ResourceVertexBuffers[0].Count; v++)
                                worldVertices.Add(vertexStream.ReadWorldVertex());
                        }
                        using (var stream = new MemoryStream(mesh.ResourceVertexBuffers[6].Data.Data))
                        {
                            var vertexStream = VertexStreamFactory.Create(SourceCache.Version, stream);
                            for (int v = 0; v < mesh.ResourceVertexBuffers[6].Count; v++)
                                h3WaterIndices.Add(vertexStream.ReadUnknown1A());
                        }
                        using (var stream = new MemoryStream(mesh.ResourceVertexBuffers[7].Data.Data))
                        {
                            var vertexStream = VertexStreamFactory.Create(SourceCache.Version, stream);
                            for (int v = 0; v < mesh.ResourceVertexBuffers[7].Count; v++)
                                h3WaterParameters.Add(vertexStream.ReadUnknown1B());
                        }

                        // create vertex buffer for Unknown1A -> World
                        VertexBufferDefinition waterVertices = new VertexBufferDefinition
                        {
                            Count = indexCount,
                            Format = VertexBufferFormat.World,
                            Data = new TagData(),
                            VertexSize = 0x38   // this size is actually wrong but I replicate the errors in HO data, size should be 0x34
                            
                        };

                        // create vertex buffer for Unknown1B
                        VertexBufferDefinition waterParameters = new VertexBufferDefinition
                        {
                            Count = indexCount,
                            Format = VertexBufferFormat.Unknown1B,
                            Data = new TagData(),
                            VertexSize = 0x24   // wrong size, this is 0x18 on file, padded with zeroes.
                        };

                        using (var outputWorldWaterStream = new MemoryStream())
                        using(var outputWaterParametersStream = new MemoryStream())
                        {
                            var outWorldVertexStream = VertexStreamFactory.Create(HOCache.Version, outputWorldWaterStream);
                            var outWaterParameterVertexStream = VertexStreamFactory.Create(HOCache.Version, outputWaterParametersStream);

                            // fill vertex buffer to the right size HO expects, then write the vertex data at the actual proper position
                            VertexBufferConverter.DebugFill(outputWorldWaterStream, waterVertices.VertexSize * waterVertices.Count);
                            VertexBufferConverter.Fill(outputWaterParametersStream, waterParameters.VertexSize * waterParameters.Count);

                            var unknown1ABaseIndex = 0; // unknown1A are not separated into parts, if a mesh has multiple parts we need to get the right unknown1As

                            for (int k = 0; k < waterData.PartData.Count(); k++)
                            {
                                Tuple<int, int> currentPartData = waterData.PartData[k];

                                //seek to the right location in the buffer
                                outputWorldWaterStream.Position = 0x34 * currentPartData.Item1;
                                outputWaterParametersStream.Position = 0x18 * currentPartData.Item1;

                                for(int v = 0; v < currentPartData.Item2; v +=3)
                                {
                                    var unknown1A = h3WaterIndices[(v / 3) + unknown1ABaseIndex];
                                    for (int j = 0; j < 3; j++)
                                    {
                                        var worldVertex = worldVertices[unknown1A.Vertices[j]];
                                        var unknown1B = h3WaterParameters[unknown1A.Indices[j]];

                                        // conversion should happen here
                                        
                                        outWorldVertexStream.WriteWorldWaterVertex(worldVertex);
                                        outWaterParameterVertexStream.WriteUnknown1B(unknown1B);
                                    }
                                }
                                unknown1ABaseIndex += currentPartData.Item2 / 3;    // tells next part we read those indices already
                            }
                            waterVertices.Data.Data = outputWorldWaterStream.ToArray();
                            waterParameters.Data.Data = outputWaterParametersStream.ToArray();
                        }

                        mesh.ResourceVertexBuffers[6] = waterVertices;
                        mesh.ResourceVertexBuffers[7] = waterParameters;
                    }

                    foreach (var indexBuffer in mesh.ResourceIndexBuffers)
                    {
                        if (indexBuffer == null)
                            continue;

                        IndexBufferConverter.ConvertIndexBuffer(SourceCache.Version, HOCache.Version, indexBuffer);
                    }

                    // create index buffers for decorators, gen3 didn't have them
                    if (mesh.Flags.HasFlag(MeshFlags.MeshIsUnindexed) && mesh.Type == VertexType.Decorator)
                    {
                        mesh.Flags &= ~MeshFlags.MeshIsUnindexed;

                        var indexCount = 0;

                        foreach (var part in mesh.Parts)
                            indexCount += part.IndexCountOld;

                        mesh.ResourceIndexBuffers[0] = IndexBufferConverter.CreateIndexBuffer(indexCount);
                    }
                    
                }
            }

            foreach (var perPixel in geometry.InstancedGeometryPerPixelLighting)
            {
                if(perPixel.VertexBuffer != null)
                    VertexBufferConverter.ConvertVertexBuffer(SourceCache.Version, HOCache.Version, perPixel.VertexBuffer);
            }

            return geometry.GetResourceDefinition();
        }

        

        
        private class WaterConversionData
        {
            // offset, count of vertices to write
            public List<Tuple<int, int>> PartData;

            public WaterConversionData()
            {
                PartData = new List<Tuple<int, int>>();
            }

            public void Sort()
            {
                PartData.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            }
        }
    }
}