using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace CTMMod;

public class CTMModSystem : ModSystem
{
    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }

    public Harmony? harmony;

    public override void StartPre(ICoreAPI api)
    {
        harmony = new Harmony("CTMMod");
        harmony.PatchAll();
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll();
    }
}

public class Patches
{
    [HarmonyPatch(typeof(TopsoilTesselator), "Tesselate")]
    public class TestPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(TopsoilTesselator __instance, TCTCache vars)
        {
            int specialTextureId = vars.fastBlockTextureSubidsByFace[6];
            int drawFaceFlags = vars.drawFaceFlags;
            int vertexFlags = vars.VertexFlags;
            int colorMapDataValue = vars.ColorMapData.Value;

            // Variant array for both alternates AND tiles.
            BakedCompositeTexture[][] variantArray = null!;

            // Check for alternates.
            uint randomValue = 0u;
            int sideMod = 0;
            bool hasAlternates = vars.block.HasAlternates;
            bool hasTiles = vars.block.HasTiles;

            if (hasAlternates || hasTiles)
            {
                variantArray = vars.block.FastTextureVariants;
                randomValue = GameMath.oaatHashU(vars.posX, vars.posY, vars.posZ);
            }

            // This is not friendly for the tesselator so reflection should be improved here. Get the block above it.
            // If there is a snow block above use the snow special texture.
            Block block = vars.tct.GetField<Block[]>("currentChunkBlocksExt")[vars.extIndex3d + 1156];
            if ((block.BlockMaterial == EnumBlockMaterial.Snow || block.snowLevel > 0f) && vars.block.Textures.TryGetValue("snowed", out CompositeTexture? snowTexture))
            {
                specialTextureId = snowTexture.Baked.TextureSubId;
                colorMapDataValue = 0;
            }
            else if (hasTiles && variantArray[6] != null)
            {
                BakedCompositeTexture[] specialSecondTextureArray = variantArray[6];
                if (specialSecondTextureArray != null)
                {
                    int tilesWidth = specialSecondTextureArray[0].TilesWidth;
                    int depth = specialSecondTextureArray.Length / specialSecondTextureArray[0].TilesWidth;
                    string path = specialSecondTextureArray[0].BakedName.Path;
                    int indexOfAtSymbol = path.IndexOf('@');
                    int result = 0;
                    if (indexOfAtSymbol > 0)
                    {
                        int plusOne = indexOfAtSymbol + 1;
                        _ = int.TryParse(path.AsSpan(plusOne, path.Length - plusOne), out result);
                        result /= 90;
                    }

                    int xSide = vars.posX;
                    int ySide = vars.posY;
                    int zSide = vars.posZ;

                    switch (result)
                    {
                        case 0:
                            sideMod = GameMath.Mod(-xSide + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(-zSide, depth));
                            break;
                        case 1:
                            sideMod = GameMath.Mod(zSide + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(xSide, depth));
                            break;
                        case 2:
                            sideMod = GameMath.Mod(xSide + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(zSide, depth));
                            break;
                        case 3:
                            sideMod = GameMath.Mod(-zSide + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(-xSide, depth));
                            break;
                    }

                    specialTextureId = specialSecondTextureArray[GameMath.Mod(sideMod, specialSecondTextureArray.Length)].TextureSubId;
                }
            }

            // Proceed to tesselate all 6 faces.
            MeshData[] poolForPass = vars.tct.GetPoolForPass(vars.RenderPass, 1);
            for (int i = 0; i < 6; i++)
            {
                if ((drawFaceFlags & TileSideEnum.ToFlags(i)) == 0)
                {
                    continue;
                }

                vars.CallMethod("CalcBlockFaceLight", i, vars.extIndex3d + TileSideEnum.MoveIndex[i]);

                int textureSubId = 0;
                if (hasAlternates)
                {
                    if (variantArray[i] is BakedCompositeTexture[] faceBakedArray)
                    {
                        textureSubId = faceBakedArray[randomValue % faceBakedArray.Length].TextureSubId;
                    }
                }

                // Soil CTM.

                if (hasTiles && variantArray[i] is BakedCompositeTexture[] faceBakedArrayTiles)
                {
                    int tilesWidth = faceBakedArrayTiles[0].TilesWidth;
                    int depth = faceBakedArrayTiles.Length / faceBakedArrayTiles[0].TilesWidth;
                    string path = faceBakedArrayTiles[0].BakedName.Path;
                    int indexOfAtSymbol = path.IndexOf('@');
                    int result = 0;
                    if (indexOfAtSymbol > 0)
                    {
                        int plusOne = indexOfAtSymbol + 1;
                        _ = int.TryParse(path.AsSpan(plusOne, path.Length - plusOne), out result);
                        result /= 90;
                    }

                    int xSize = 0;
                    int ySide = 0;
                    int zSide = 0;
                    switch (i)
                    {
                        case 0:
                            xSize = vars.posX;
                            ySide = vars.posZ;
                            zSide = vars.posY;
                            break;
                        case 1:
                            xSize = vars.posZ;
                            ySide = -vars.posX;
                            zSide = vars.posY;
                            break;
                        case 2:
                            xSize = -vars.posX;
                            ySide = vars.posZ;
                            zSide = vars.posY;
                            break;
                        case 3:
                            xSize = -vars.posZ;
                            ySide = -vars.posX;
                            zSide = vars.posY;
                            break;
                        case 4:
                            xSize = vars.posX;
                            ySide = vars.posY;
                            zSide = vars.posZ;
                            break;
                        case 5:
                            xSize = vars.posX;
                            ySide = vars.posY;
                            zSide = -vars.posZ;
                            break;
                    }

                    switch (result)
                    {
                        case 0:
                            sideMod = GameMath.Mod(-xSize + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(-zSide, depth));
                            break;
                        case 1:
                            sideMod = GameMath.Mod(zSide + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(xSize, depth));
                            break;
                        case 2:
                            sideMod = GameMath.Mod(xSize + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(zSide, depth));
                            break;
                        case 3:
                            sideMod = GameMath.Mod(-zSide + ySide, tilesWidth) + (tilesWidth * GameMath.Mod(-xSize, depth));
                            break;
                    }

                    textureSubId = faceBakedArrayTiles[GameMath.Mod(sideMod, faceBakedArrayTiles.Length)].TextureSubId;
                }

                // Soil CTM.

                // Fallback.
                if (textureSubId == 0)
                {
                    textureSubId = vars.fastBlockTextureSubidsByFace[i];
                }

                __instance.CallMethod("DrawBlockFaceTopSoil", vars, vertexFlags | BlockFacing.ALLFACES[i].NormalPackedFlags, vars.blockFaceVertices[i], colorMapDataValue, textureSubId, specialTextureId, poolForPass);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ShapeTesselatorManager), "CreateFastTextureAlternates")]
    public class SoilTessReplacer
    {
        [HarmonyPrefix]
        public static bool Prefix(ShapeTesselatorManager __instance, Block block)
        {
            if (block.HasAlternates && block.DrawType != EnumDrawType.JSON)
            {
                block.FastTextureVariants = new BakedCompositeTexture[7][];
                foreach (BlockFacing blockFacing in BlockFacing.ALLFACES)
                {
                    if (block.Textures.TryGetValue(blockFacing.Code, out CompositeTexture? texture))
                    {
                        BakedCompositeTexture[] bakedVariants = texture.Baked.BakedVariants;
                        if (bakedVariants != null && bakedVariants.Length != 0)
                        {
                            block.FastTextureVariants[blockFacing.Index] = bakedVariants;
                        }
                    }
                }
            }

            if (!block.HasTiles || block.DrawType == EnumDrawType.JSON)
            {
                return false;
            }

            block.FastTextureVariants = new BakedCompositeTexture[7][];
            foreach (BlockFacing blockFacingTwo in BlockFacing.ALLFACES)
            {
                if (block.Textures.TryGetValue(blockFacingTwo.Code, out CompositeTexture? texture))
                {
                    BakedCompositeTexture[] bakedTiles = texture.Baked.BakedTiles;
                    if (bakedTiles != null && bakedTiles.Length != 0)
                    {
                        block.FastTextureVariants[blockFacingTwo.Index] = bakedTiles;
                    }
                }
            }

            if (block.Textures.TryGetValue("specialSecondTexture", out CompositeTexture? specialTexture))
            {
                BakedCompositeTexture[] bakedSpecialTiles = specialTexture.Baked.BakedTiles;
                if (bakedSpecialTiles != null && bakedSpecialTiles.Length != 0)
                {
                    block.FastTextureVariants[6] = bakedSpecialTiles;
                }
            }

            return false;
        }
    }
}