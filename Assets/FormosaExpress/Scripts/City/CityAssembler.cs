using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    /// <summary>
    /// Turns a <see cref="CityModel"/> into scene geometry. Everything a block owns collapses
    /// into two meshes (lit and emissive), which keeps the draw-call count roughly at
    /// 2 x blocks while still giving frustum culling ~50 m granularity.
    /// </summary>
    public sealed class CityAssembler
    {
        public GameObject Root { get; private set; }
        public int TriangleEstimate { get; private set; }

        public void Assemble(CityModel model, CityBuilder layout, MaterialLibrary mats, float nightFactor)
        {
            Root = new GameObject("City");
            model.Root = Root.transform;

            var ground = new GroundFactory(mats);
            ground.Build(model, layout, Root.transform);

            var buildings = new BuildingFactory(mats.Palette);
            var props = new PropFactory(mats.Palette);

            int buildingLayer = LayerMask.NameToLayer(Tuning.LayerBuilding);

            // Group lots by block so each block's geometry is emitted together.
            var lotsByBlock = new List<BuildingLot>[model.Blocks.Count];
            for (int i = 0; i < layout.Lots.Count; i++)
            {
                BuildingLot lot = layout.Lots[i];
                lotsByBlock[lot.BlockIndex] ??= new List<BuildingLot>(24);
                lotsByBlock[lot.BlockIndex].Add(lot);
            }

            var blocksRoot = new GameObject("Blocks");
            blocksRoot.transform.SetParent(Root.transform, false);

            var surface = new MeshBuilder(mats.Palette);
            var glow = new MeshBuilder(mats.Palette);
            var additive = new MeshBuilder(mats.Palette);

            for (int b = 0; b < model.Blocks.Count; b++)
            {
                CityBlock block = model.Blocks[b];
                var rng = new Rng(model.Seed * 7717 + b * 131 + 7);

                var blockRoot = new GameObject($"Block_{b}");
                blockRoot.transform.SetParent(blocksRoot.transform, false);
                blockRoot.transform.position = Vector3.zero;
                block.Root = blockRoot.transform;

                var colliderHolder = new GameObject("Colliders");
                colliderHolder.transform.SetParent(blockRoot.transform, false);

                List<BuildingLot> lots = lotsByBlock[b];
                if (lots != null)
                {
                    foreach (BuildingLot lot in lots)
                    {
                        float height = buildings.Build(lot, surface, glow, additive, ref rng, nightFactor);
                        AddBuildingCollider(colliderHolder.transform, lot, height, buildingLayer);

                        if (lot.SiteIndex >= 0 && lot.SiteIndex < model.Sites.Count)
                            model.Sites[lot.SiteIndex].DoorPosition = lot.FrontCentre + lot.Forward * 0.4f;
                    }
                }

                props.BuildBlockProps(model, layout, block, surface, glow, additive, ref rng, nightFactor);

                TriangleEstimate += surface.VertexCount / 2 + glow.VertexCount / 2;
                surface.Flush($"Block_{b}_Surface", blockRoot.transform, mats.Surface);
                DisableShadows(glow.Flush($"Block_{b}_Glow", blockRoot.transform, mats.GlowSoft));
                DisableShadows(additive.Flush($"Block_{b}_Light", blockRoot.transform, mats.Additive));
            }

            // Perimeter shophouses: the wall of the play area.
            var perimeterRoot = new GameObject("Perimeter");
            perimeterRoot.transform.SetParent(Root.transform, false);

            var perimeterColliders = new GameObject("Colliders");
            perimeterColliders.transform.SetParent(perimeterRoot.transform, false);

            var perimeterRng = new Rng(model.Seed * 6151 + 29);
            int flushCounter = 0;
            int perimeterChunk = 0;

            for (int i = 0; i < layout.PerimeterLots.Count; i++)
            {
                BuildingLot lot = layout.PerimeterLots[i];
                float height = buildings.Build(lot, surface, glow, additive, ref perimeterRng, nightFactor);
                AddBuildingCollider(perimeterColliders.transform, lot, height, buildingLayer);

                if (++flushCounter < 18) continue;

                flushCounter = 0;
                TriangleEstimate += surface.VertexCount / 2 + glow.VertexCount / 2;
                surface.Flush($"Perimeter_{perimeterChunk}_Surface", perimeterRoot.transform, mats.Surface);
                DisableShadows(glow.Flush($"Perimeter_{perimeterChunk}_Glow", perimeterRoot.transform, mats.GlowSoft));
                DisableShadows(additive.Flush($"Perimeter_{perimeterChunk}_Light", perimeterRoot.transform,
                    mats.Additive));
                perimeterChunk++;
            }

            TriangleEstimate += surface.VertexCount / 2 + glow.VertexCount / 2;
            surface.Flush($"Perimeter_{perimeterChunk}_Surface", perimeterRoot.transform, mats.Surface);
            DisableShadows(glow.Flush($"Perimeter_{perimeterChunk}_Glow", perimeterRoot.transform, mats.GlowSoft));
            DisableShadows(additive.Flush($"Perimeter_{perimeterChunk}_Light", perimeterRoot.transform, mats.Additive));

            SkylineFactory.Build(model, mats, nightFactor, Root.transform);

            // Intersection furniture lives in its own chunked group.
            var junctions = new GameObject("Junctions");
            junctions.transform.SetParent(Root.transform, false);

            var jSurface = new MeshBuilder(mats.Palette);
            var jGlow = new MeshBuilder(mats.Palette);
            var jAdditive = new MeshBuilder(mats.Palette);
            int flushEvery = 12;
            int emitted = 0;
            int chunkIndex = 0;

            for (int n = 0; n < model.Nodes.Count; n++)
            {
                var rng = new Rng(model.Seed * 991 + n * 37 + 3);
                props.BuildIntersectionProps(model, model.Nodes[n], jSurface, jGlow, ref rng, nightFactor);
                emitted++;

                if (emitted >= flushEvery)
                {
                    FlushJunctionChunk(jSurface, jGlow, jAdditive, junctions.transform, mats, chunkIndex++);
                    emitted = 0;
                }
            }

            FlushJunctionChunk(jSurface, jGlow, jAdditive, junctions.transform, mats, chunkIndex);

            mats.CommitPalette();
        }

        void FlushJunctionChunk(MeshBuilder surface, MeshBuilder glow, MeshBuilder additive, Transform parent,
            MaterialLibrary mats, int index)
        {
            TriangleEstimate += surface.VertexCount / 2 + glow.VertexCount / 2;
            surface.Flush($"Junctions_{index}_Surface", parent, mats.Surface);
            DisableShadows(glow.Flush($"Junctions_{index}_Glow", parent, mats.GlowSoft));
            DisableShadows(additive.Flush($"Junctions_{index}_Light", parent, mats.Additive));
        }

        static void DisableShadows(GameObject go)
        {
            if (go == null) return;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static void AddBuildingCollider(Transform parent, BuildingLot lot, float height, int layer)
        {
            var go = new GameObject("BuildingCollider");
            go.transform.SetParent(parent, false);

            Vector3 centre = lot.FrontCentre - lot.Forward * (lot.Depth * 0.5f);
            go.transform.position = new Vector3(centre.x, 0f, centre.z);
            go.transform.rotation = Quaternion.LookRotation(lot.Forward, Vector3.up);
            go.layer = layer;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(lot.Width, height + Tuning.CurbHeight, lot.Depth);
            box.center = new Vector3(0f, (height + Tuning.CurbHeight) * 0.5f, 0f);
        }
    }
}
