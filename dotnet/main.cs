//welcome to hell
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using VintagestoryLib;

using ElectricalProgressive.Interface;
using ElectricalProgressive.Utils;
using ElectricalProgressive;
using ElectricalProgressive.Content.Block;
using Vintagestory.Client.NoObf;
using System.Text;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client;
using System.Reflection;
using System.Net.NetworkInformation;

namespace BirkelandEyde;

//contains useful code to steal:
//https://github.com/anegostudios/vssurvivalmod/blob/master/BlockEntity/BEBloomery.cs

public class BirkelandEydeModSystem : ModSystem
{
    public static byte[] lighthsv = new byte[] { 11, 4, 6 };//uses an incredibly idiotic non-base 10 numerical system, good luck

    ///the default spark offset position for one of the orientations
    //public static Vec3d defaultSparkOffset { get => new Vec3d(0.5, 0.2, 0.1); }
    //public Vec3d sparkOffset = new Vec3d(0.5, 0.2, 1);//this is what its supposed to be for one of the orientations
    public static ICoreAPI coreapi;
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        coreapi = api;

        api.RegisterBlockClass("BlockBirkelandEydeMini", typeof(BlockBirkelandEydeMini));
        api.RegisterBlockEntityClass("BlockEntityBirkelandEydeMini", typeof(BlockEntityBirkelandEydeMini));
        api.RegisterBlockEntityBehaviorClass("BEBehaviorBirkelandEydeMini", typeof(BEBehaviorBirkelandEydeMini));

        api.Logger.Notification("[BirklandEyde] Loaded!");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
    }

    /// <summary>
    /// weighted interpolation between 2 numbers from Orions Quest source code. rtfm to figure out how to properly use this
    /// </summary>
    /// <param name="n1">the first number to interpolate with. if amount is set to 0, this is the number that will return</param>
    /// <param name="n2">the second number to interpolte with. if amount is set to 1.0, this is the number that will return</param>
    /// <param name="amount">what weight of n2 to use. 0.5 is a 50/50 split aka the average. 0 is n1 and 1 is n2</param>
    /// <returns></returns>
    public static double WeightedInterpolate(double n1, double n2, double amount)
    {
        if (amount >= 1.0)
        {
            return n2;
        }
        else if (amount <= 0)
        {
            return n1;
        }
        else
        {
            return (1.0 - amount) * n1 + amount * n2;
        }
    }

    /// <summary>
    /// converts irl hours to vintage story hours
    /// </summary>
    /// <param name="irlHours"></param>
    /// <returns></returns>
    public static float IrlToVsHours(float irlHours)
    {
        //float sot = coreapi.World.Calendar.SpeedOfTime;
        //float calmult = coreapi.World.Calendar.CalendarSpeedMul;
        //float daylengthirlseconds = coreapi.ga
        return irlHours * 30f;
    }
}

class BlockBirkelandEydeMini : BlockEBase
{
    public bool playEffect = false;
    private static readonly Dictionary<(Facing, string), Cuboidf[]> SelectionBoxesCache = new();
    private static readonly Dictionary<(Facing, string), Cuboidf[]> CollisionBoxesCache = new();

    //Claude code decided i dont need this
    /*public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
    }*/

    public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
    {
        //string lightHsvStr = LightHsv[0].ToString() + " " + LightHsv[1].ToString() + " " + LightHsv[2].ToString();
        if (pos != null && blockAccessor.GetBlockEntity(pos) is BlockEntityBirkelandEydeMini { } entity && entity.receivedPower > 0)
        {
            //api.Logger.Notification("LIGHT. recievedPower = " + entity.receivedPower.ToString());
            return BirkelandEydeModSystem.lighthsv; // match your spark's blue-violet hue; tune the value/brightness component
        }
        else
        {
            //api.Logger.Notification("default GetLightHsv");
            //return base.GetLightHsv(blockAccessor, pos, stack);   
            return new byte[] { 0, 0, 0 };
        }
    }


    //all this crap is really convoluted but it was the only way to make orientations work, and collisions line up in all 4 orientations and still allow the electric connection point to actually work almost 50% of the time and be in the same spot in each orientation
    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) =>
        GetRotatedBoxes(pos, CollisionBoxesCache, CollisionBoxes);

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos) =>
        GetRotatedBoxes(pos, SelectionBoxesCache, SelectionBoxes);

    private Cuboidf[] GetRotatedBoxes_old(BlockPos pos, Dictionary<(Facing, string), Cuboidf[]> cache, Cuboidf[] sourceBoxes)
    {
        if (!(api?.World?.BlockAccessor.GetBlockEntity(pos) is BlockEntityBirkelandEydeMini { Facing: not Facing.None, Facing: var facing }))
            return Array.Empty<Cuboidf>();

        string code = Code.ToString();
        if (!cache.TryGetValue((facing, code), out Cuboidf[] boxes))
        {
            //i don't really know how this works but mirroring whatever electric progressives qol code does wasn't allowing orientations to work correctly.
            boxes = (Cuboidf[])sourceBoxes.Clone();
            for (int i = 0; i < boxes.Length; i++)
                boxes[i] = boxes[i].RotatedCopy(0f, 0f, 180f, RotationOriginVec3d); // same cancellation
            FacingRotations.ApplyRotations(boxes, facing);
            cache.TryAdd((facing, code), boxes);
        }
        return boxes ?? Array.Empty<Cuboidf>();
    }
    private Cuboidf[] GetRotatedBoxes(BlockPos pos, Dictionary<(Facing, string), Cuboidf[]> cache, Cuboidf[] sourceBoxes)
    {
        /*if (!(api?.World?.BlockAccessor.GetBlockEntity(pos) is BlockEntityBirkelandEydeMini { Facing: not Facing.None, Facing: var facing }))
            return Array.Empty<Cuboidf>();

        string code = Code.ToString();
        if (!cache.TryGetValue((facing, code), out Cuboidf[] boxes))
        {
            //i don't really know how this works but mirroring whatever electric progressives qol code does wasn't allowing orientations to work correctly.
            boxes = (Cuboidf[])sourceBoxes.Clone();
            for (int i = 0; i < boxes.Length; i++)
                boxes[i] = boxes[i].RotatedCopy(0f, 0f, 180f, RotationOriginVec3d); // same cancellation
            FacingRotations.ApplyRotations(boxes, facing);
            cache.TryAdd((facing, code), boxes);
        }
        return boxes ?? Array.Empty<Cuboidf>();*/
        if (!(api?.World?.BlockAccessor.GetBlockEntity(pos) is BlockEntityBirkelandEydeMini { ModelFacing: not Facing.None, ModelFacing: var facing }))
            return Array.Empty<Cuboidf>();

        string code = Code.ToString();
        if (!cache.TryGetValue((facing, code), out Cuboidf[] boxes))
        {
            boxes = (Cuboidf[])sourceBoxes.Clone();
            for (int i = 0; i < boxes.Length; i++)
                boxes[i] = boxes[i].RotatedCopy(0f, 0f, 180f, RotationOriginVec3d);
            FacingRotations.ApplyRotations(boxes, facing);
            cache.TryAdd((facing, code), boxes);
        }
        return boxes ?? Array.Empty<Cuboidf>();
    }

    private static readonly Vec3f RotationOriginVec3f = new Vec3f(0.5f, 0.5f, 0.5f);
    private static readonly Vec3d RotationOriginVec3d = new Vec3d(0.5, 0.5, 0.5);

    //another "i have no idea what this does" kind of function but it sure did take Claude code a lot of attempts to get it right
    public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
    {
        /*base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);
        if (api is ICoreClientAPI && api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityBirkelandEydeMini { Facing: not Facing.None, Facing: var facing })
        {
            MeshData rotated = sourceMesh.Clone();
            rotated.Rotate(RotationOriginVec3f, 0f, 0f, (float)Math.PI); // cancel the framework's built-in Up* 180° flip
            FacingRotations.ApplyRotations(rotated, facing);
            sourceMesh = rotated;
        }*/
        base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);
        if (api is ICoreClientAPI && api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityBirkelandEydeMini { ModelFacing: not Facing.None, ModelFacing: var facing })
        {
            MeshData rotated = sourceMesh.Clone();
            rotated.Rotate(RotationOriginVec3f, 0f, 0f, (float)Math.PI);
            FacingRotations.ApplyRotations(rotated, facing);
            sourceMesh = rotated;
        }
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSelection, ItemStack byItemStack)
    {
        Selection selection = new Selection(blockSelection);
        BlockFacing playerFacing = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
        Facing electricalFacing = FacingHelper.From(selection.Face, playerFacing);
        Facing modelFacing = FacingHelper.From(BlockFacing.UP, playerFacing.GetCCW());

        if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack) && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is BlockEntityBirkelandEydeMini entity)
        {
            entity.Facing = electricalFacing;
            entity.ModelFacing = modelFacing;
            LoadEProperties.Load(this, entity, selection.Face.Index, electricalFacing);

            var mb = GetBehavior<Vintagestory.GameContent.BlockBehaviorMultiblock>();
            api.Logger.Notification($"[BirklandEyde] placed {Code} side={Variant["side"]} " +
                $"sizeX={mb?.GetType().GetField("SizeX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mb)} " +
                $"sizeY={mb?.GetType().GetField("SizeY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mb)} " +
                $"sizeZ={mb?.GetType().GetField("SizeZ", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mb)} " +
                $"cposition={mb?.ControllerPositionRel}");
            entity.ScheduleNetworkRefresh(2000);
            return true;
        }
        return false;
        /*BlockFacing playerFacing = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
        BlockFacing adjustedFacing = playerFacing.GetCCW(); // try GetCW() instead if this goes the wrong way
        Facing facing = FacingHelper.From(BlockFacing.UP, adjustedFacing);

        if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack) && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is BlockEntityBirkelandEydeMini entity)
        {
            entity.Facing = facing;
            LoadEProperties.Load(this, entity, BlockFacing.UP.Index, facing);
            //LoadEProperties.Load(this, entity, adjustedFacing.Index, facing);

            return true;
        }
        return false;*/

        /*BlockFacing playerFacing = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
        Selection selection = new Selection(blockSelection);
		Facing facing = FacingHelper.From(selection.Face, playerFacing);
		if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack) && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is BlockEntityBirkelandEydeMini entity)
		{
			entity.Facing = facing;
			LoadEProperties.Load(this, entity, selection.Face.Index, facing);
			return true;
		}
		return false;*/

        //chatgpt's first attempt
        /*BlockFacing playerFacing =
        BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);

        BlockFacing adjustedFacing = playerFacing.GetCCW();

        Selection selection = new Selection(blockSelection);

        Facing facing =
            FacingHelper.From(BlockFacing.UP, adjustedFacing);

        if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack)
            && world.BlockAccessor.GetBlockEntity(blockSelection.Position)
                is BlockEntityBirkelandEydeMini entity)
        {
            entity.Facing = facing;

            // Give EP the ACTUAL face that was clicked,
            // while giving the entity the desired orientation.
            LoadEProperties.Load(
                this,
                entity,
                selection.Face.Index,
                facing
            );

            return true;
        }

        return false;*/

        //it took forever to come up with this 
        //world.BlockAccessor.blo
        /*BlockFacing playerFacing =
        BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);

        BlockFacing adjustedFacing =
            playerFacing.GetCCW();

        // ONLY model orientation
        Facing modelFacing =
            FacingHelper.From(BlockFacing.UP, adjustedFacing);

        if (base.DoPlaceBlock(
                world,
                byPlayer,
                blockSelection,
                byItemStack)
            && world.BlockAccessor.GetBlockEntity(
                blockSelection.Position)
                is BlockEntityBirkelandEydeMini entity)
        {
            entity.Facing = modelFacing;

            // do it differently from anything that happens in electric progressives qol
            LoadEProperties.Load(this, entity, adjustedFacing.Opposite.GetCCW().Index);

            return true;
        }

        return false;*/

        //99% working with only 2 bugs DONT LOSE because its the BEST solution EVER FOUND so far
        /*Selection selection = new Selection(blockSelection);

        BlockFacing playerFacing = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
        //Facing electricalFacing = FacingHelper.From(selection.Face, selection.Direction); // exactly like the lamp — proven to work
        Facing electricalFacing = FacingHelper.From(selection.Face, playerFacing); // exactly like the lamp — proven to work

        Facing modelFacing = FacingHelper.From(BlockFacing.UP, playerFacing.GetCCW()); // cosmetic only, your preference

        if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack) && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is BlockEntityBirkelandEydeMini entity)
        {
            entity.Facing = electricalFacing;       // drives Connection — must match how cables/lamps compute it
            entity.ModelFacing = modelFacing;        // drives only rendering/boxes
            LoadEProperties.Load(this, entity, selection.Face.Index, electricalFacing);


            return true;
        }
        return false;*/

        //keeps crashing and i can't fix it:
        /*
        try
        {
            
        Selection selection = new Selection(blockSelection);
        BlockFacing playerFacing = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);

        Facing electricalFacing = FacingHelper.From(selection.Face, playerFacing);
        Facing modelFacing = FacingHelper.From(BlockFacing.UP, playerFacing.GetCCW());

        string sideVariant = playerFacing.Code; // "north"/"east"/"south"/"west" — confirm this matches your JSON variant states
        Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithVariant("side", sideVariant));
        if (orientedBlock == null) orientedBlock = this;

        if (orientedBlock.DoPlaceBlock(world, byPlayer, blockSelection, new ItemStack(orientedBlock)) && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is BlockEntityBirkelandEydeMini entity)
        {
            entity.Facing = electricalFacing;
            entity.ModelFacing = modelFacing;
            LoadEProperties.Load(this, entity, selection.Face.Index, electricalFacing);
            return true;
        }
        return false;
        }
        catch (Exception e)
        {
            api.Logger.Notification("[birkleand eyde] exception: " + e.Message);
        }
        return false;
        */
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        BlockFacing playerFacing = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
        string desiredSide = playerFacing.Code;

        if (Variant["side"] != desiredSide)
        {
            Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithVariant("side", desiredSide));
            if (orientedBlock != null)
            {
                ItemStack orientedStack = new ItemStack(orientedBlock);
                return orientedBlock.TryPlaceBlock(world, byPlayer, orientedStack, blockSel, ref failureCode);
            }
        }


        return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        return (ItemStack[])(object)new ItemStack[1] { ((Block)this).OnPickBlock(world, pos) };
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        bool holdingShift = byPlayer.WorldData.EntityControls.ShiftKey; //figure out if the player is holding down the shift key or not
        BlockEntityBirkelandEydeMini be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBirkelandEydeMini;

        if (be != null && be.ProductToExtract() && be.TryTakeAcidFromDevice(byPlayer))
        {
            return true;
        }
        else if (be != null && be.TryAddWaterFromPlayer(byPlayer))
        {
            return true;
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    //doesn't seem to do anything
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
    {
        BlockEntityBirkelandEydeMini be = null;
        if (blockSel.Position != null)
        {
            be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBirkelandEydeMini;
        }
        /*if (be != null)
        {
            return Array.Empty<WorldInteraction>();
        }*/
        return base.GetPlacedBlockInteractionHelp(world, blockSel, forPlayer);
    }
}

public class BlockEntityBirkelandEydeMini : BlockEntityEFacingBase
{
    public Facing ModelFacing { get; set; } = Facing.None;
    public long lastUpdateMs;

    public float receivedPower { get; set; }

    public float receivedVoltage { get; set; }
    public float receivedAmps { get; set; }

    private const float MaxConsumptionWatts = 100f;

    private const float FullProductionLitresPerHour = 1f; // tune this — acid produced per hour at full 100W

    private long listenerId = -1;

    private long listenerId2 = -1;

    public bool lightExists = false;

    private long lastSoundMs;
    private const long SoundIntervalMs = 500; // 0.668 seconds. (doesnt matter / not used anymore)

    private double totalHoursLastUpdate;

    private ILoadedSound zappingSound;

    public static int millisecondTickInterval = 1000;
    public float mostRecentHoursTillFull {get; set; }
    public float mostRecentLitersPerHour {get; set; }
    //public static Vec3d defaultSparkOffset => new Vec3d(0.5, 0.2, 0.1);
    //public Vec3d SparkOffset { get; private set; } = defaultSparkOffset;
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        lastUpdateMs = api.World.ElapsedMilliseconds;

        //if (api.Side == EnumAppSide.Server)//doesnt prevent the listener from getting registered on the client and it never gets called from the server side. should be renamed to RegisterGameTickListenerClientOnly
        //{
        //causes lots of errors when the block is deleted. automatically dispose the listener my ass.
        listenerId2 = RegisterDelayedCallback(OnDelayedNetworkRefresh, 1000);
        listenerId = RegisterGameTickListener(OnGameTick, 1500);
        //}
        Api.World.BlockAccessor.RemoveBlockLight(BirkelandEydeModSystem.lighthsv, Pos);//make the light go away if there is a light
        lightExists = false;

        //a workaround that will hopefully connect the device on load if its in position to be connected and solve that bug
        //if (api.Side == EnumAppSide.Server)
        //{
        //}

        mostRecentHoursTillFull = 6;

        Api.Logger.Notification("[BirklandEyde] initialise: register game tick callback");
    }

    public override void OnBlockUnloaded()
    {
        UnregisterAllTickListeners();//why not just do this
        Api.World.BlockAccessor.RemoveBlockLight(BirkelandEydeModSystem.lighthsv, Pos);//make the light go away if there is a light
        lightExists = false;
        if (zappingSound != null)
        {
            zappingSound.Stop();
            zappingSound.Dispose();
            zappingSound = null;
        }
        base.OnBlockUnloaded();
        /*UnregisterGameTickListener(listenerId);

        if (Api.Side == EnumAppSide.Server)
        {
            UnregisterGameTickListener(listenerId2);      
        }*/
    }

    public override void OnBlockRemoved()
    {
        UnregisterAllTickListeners();//why not just do this
        Api.World.BlockAccessor.RemoveBlockLight(BirkelandEydeModSystem.lighthsv, Pos);//make the light go away if there is a light
        lightExists = false;
        if (zappingSound != null)
        {
            zappingSound.Stop();
            zappingSound.Dispose();
            zappingSound = null;
        }
        base.OnBlockRemoved();
    }
    public float WaterLitres { get; private set; } = 0f;
    public const float MaxWaterLitres = 3f;

    public float AcidLitres { get; private set; } = 0f;

    //allow the player to add water into the device. you add 5 liters
    public bool TryAddWaterFromPlayer(IPlayer byPlayer)
    {
        bool holdingShift = byPlayer.WorldData.EntityControls.ShiftKey;

        if (WaterLitres >= MaxWaterLitres) return false;

        ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (hotbarSlot.Empty) return false;
        if (hotbarSlot.Itemstack.Collectible is not BlockLiquidContainerBase liquidContainer) return false;

        ItemStack contentStack = liquidContainer.GetContent(hotbarSlot.Itemstack);
        if (contentStack == null || contentStack.Collectible.Code.Path != "waterportion") return false;

        float litresAvailable = liquidContainer.GetCurrentLitres(hotbarSlot.Itemstack);
        float litresToTake = System.Math.Min(litresAvailable, MaxWaterLitres - WaterLitres);
        if (litresToTake <= 0) return false;
        if (holdingShift && litresAvailable >= 1)
        {
            //holding shift needs to let the player load it with 1 liter at a time
            litresToTake = 1;
        }

        ItemStack takenStack = liquidContainer.TryTakeContent(hotbarSlot.Itemstack, (int)litresToTake * 100);//this has to be multiplied by 100 to get the right number in-game for some reason
        if (takenStack == null) return false;

        WaterLitres += (litresToTake);
        hotbarSlot.MarkDirty();
        MarkDirty(true);

        Api.Logger.Notification("[BirklandEyde] litresAvailable=" + litresAvailable.ToString() + " litresToTake=" + litresToTake.ToString() + " WaterLitres=" + WaterLitres.ToString() + " MaxWaterLitres=" + MaxWaterLitres.ToString());

        Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/water-pour"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer);
        return true;
    }

    //if AcidLitres is equal to or greater than 5, let the player transfer the acid from to an empty liquid container
    public bool TryTakeAcidFromDevice(IPlayer byPlayer)
    {
        bool holdingShift = byPlayer.WorldData.EntityControls.ShiftKey;

        if (AcidLitres < WaterLitres) return false; // process not complete

        ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (hotbarSlot.Empty) return false;
        if (hotbarSlot.Itemstack.Collectible is not BlockLiquidContainerBase liquidContainer) return false;

        ItemStack existingContent = liquidContainer.GetContent(hotbarSlot.Itemstack);
        if (existingContent != null) return false; // require an empty container

        float capacityLitres = liquidContainer.CapacityLitres; // confirm this property name too
        float litresToGive = System.Math.Min(AcidLitres, capacityLitres);
        if (litresToGive <= 0) return false;

        AssetLocation acidPlaceholderCode = new AssetLocation("game:acid-full-nitric"); // confirm exact code from grep above
        Item acidItem = Api.World.GetItem(acidPlaceholderCode);
        if (acidItem == null) return false;

        ItemStack acidStack = new ItemStack(acidItem, (int)(litresToGive * 100)); // same *100 convention as TryAddWaterFromPlayer

        int litresMoved = liquidContainer.TryPutLiquid(hotbarSlot.Itemstack, acidStack, litresToGive); // confirm exact signature/return type
        if (litresMoved <= 0) return false;
        if (holdingShift && litresMoved >= 1)
        {
            //holding shift lets the player remove just 1 liter and not 2
            litresMoved = 1;
        }

        AcidLitres -= litresMoved / 100f; // adjust divisor if TryPutLiquid's units differ from what you pass in
        WaterLitres -= litresMoved / 100f;
        hotbarSlot.MarkDirty();
        MarkDirty(true);

        //Api.Logger.Notification("[BirklandEyde] litresToGive=" + litresToGive + " AcidLitres=" + AcidLitres);
        Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/water-pour"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer);
        return true;
    }

    /// <summary>
    /// returns true if the reaction has been completed enough to allow the player to extract some nitric acid
    /// </summary>
    /// <returns>true if there is product to extract, false if there is not</returns>
    public bool ProductToExtract()
    {
        if (AcidLitres >= WaterLitres)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("waterLitres", WaterLitres);
        tree.SetBytes("birkelandeyde:modelFacing", SerializerUtil.Serialize(ModelFacing));
        tree.SetFloat("acidLitres", AcidLitres);

        //based on a rotation fix mod claude made, im trying to use that same idea to make the power numbers sync up with server/client
        tree.SetFloat("receivedPower", receivedPower);
        tree.SetFloat("receivedVoltage", receivedVoltage);
        tree.SetFloat("receivedAmps", receivedAmps);
        tree.SetDouble("totalHoursProductionCheck", totalHoursLastUpdate);
        tree.SetFloat("mostRecentHoursTillFull", mostRecentHoursTillFull);
        tree.SetFloat("mostRecentLitersPerHour", mostRecentLitersPerHour);
        // and in FromTreeAttributes:
        //AcidLitres = tree.GetFloat("acidLitres");
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        WaterLitres = tree.GetFloat("waterLitres");
        //tree.SetFloat("acidLitres", AcidLitres);
        // and in FromTreeAttributes:
        totalHoursLastUpdate = tree.GetDouble("totalHoursProductionCheck");
        AcidLitres = tree.GetFloat("acidLitres");
        receivedPower = tree.GetFloat("receivedPower");
        receivedVoltage = tree.GetFloat("receivedVoltage");
        receivedAmps = tree.GetFloat("receivedAmps");
        mostRecentHoursTillFull = tree.GetFloat("mostRecentHoursTillFull");
        mostRecentLitersPerHour = tree.GetFloat("mostRecentLitersPerHour");
        //Api?.Logger.Notification("[BirklandEyde] FromTreeAttributes side=" + Api?.Side + " receivedPower=" + receivedPower);//actually prints the corect number on client side, WTF? THEN WHY DOES IT NOT WORK IN BLOCK NFO

        try
        {
            ModelFacing = SerializerUtil.Deserialize<Facing>(tree.GetBytes("birkelandeyde:modelFacing", null));
        }
        catch
        {
            ModelFacing = Facing.None;
        }
    }

    public void ConsumeWaterProduceAcid(float litres)
    {
        if (litres <= 0) return;
        //WaterLitres -= litres;    //don't decrement water. water amount will get set to 0 when the mixture is removed/fetched by the player

        if (AcidLitres < WaterLitres)
        {
            //stop making acid when its fully concentrated
            AcidLitres += litres;
        }

        //make sure the amount of acid doesn't exceed the amount of water by even a little bit. no more decimaled acid quantities
        if (AcidLitres > WaterLitres)
        {
            AcidLitres = WaterLitres;
        }
        MarkDirty(true);
    }

    /// <summary>
    /// estimates the amount of time remaining until it's full
    /// </summary>
    /// <param name="litresThisTick"></param>
    /// <returns></returns>
    public float HoursTillFull(float litresThisTick, bool saveLitersPerHour = true)
    {
        //Api?.Logger.Notification("[BirklandEyde] waterLitres=" + WaterLitres.ToString() + " AcidLitres="+AcidLitres.ToString() + " litersThisTick=" + litresThisTick.ToString());
        float remainingLiters = WaterLitres - AcidLitres;
        if (remainingLiters > 0)
        {
            float litersPerSecond = litresThisTick * 1000 / millisecondTickInterval;
            float remainingSeconds = remainingLiters / litersPerSecond;
            if (saveLitersPerHour)
            {
                //convert liters per second to liters per hour
                mostRecentLitersPerHour = litersPerSecond / 3600;
            }
            //Api?.Logger.Notification("[BirklandEyde] waterLitres=" + WaterLitres.ToString() + " AcidLitres="+AcidLitres.ToString() + " litersThisTick=" + litresThisTick.ToString() + "remainingSeconds=" + remainingSeconds.ToString() + " remainingHours:" + (remainingSeconds / 60.0f / 60.0f).ToString());
            return remainingSeconds / 60.0f / 60.0f;

        }
        else
        {
            return 0;
        }
    }

    /// <summary>
    /// calculates the nitric acid percentage
    /// </summary>
    /// <returns>a number between 0.00 and 0.68. 0.68 is the concentration required to let the player take acid from the devices</returns>
    public double GetAcidPercentage(out double ph)
    {
        float c = (AcidLitres / WaterLitres) * 68f; //68% is the azeotrope of nitric acid
        //dsc.AppendLine("acid concentration: " + c.ToString("0.00") + "%");
        //dsc.AppendLine("acid: " + be.AcidLitres.ToString("0.00") + "L");
        ph = (float)BirkelandEydeModSystem.WeightedInterpolate(7d, -1.2d, (AcidLitres / WaterLitres));
        //dsc.AppendLine("ph: " + ph.ToString("0.00") + "%");
        return c;
    }

    public override Facing GetConnection(Facing value)
    {
        //return base.GetConnection(value);
        return FacingHelper.FullFace(value);
    }

    private void OnDelayedNetworkRefresh(float dt)
    {
        var behavior = GetBehavior<BEBehaviorElectricalProgressive>();
        if (behavior == null) return;

        Facing currentFacing = behavior.Connection;
        behavior.Connection = Facing.None;   // forces RemoveConnections, drops from Parts
        behavior.Connection = currentFacing; // forces full AddConnections against current neighbors
        Api?.Logger.Notification("[BirklandEyde] forced reconnect cycle after load");
    }

    //patch yet ANOTHER "electrical network not connecting in specific situation" bug
    public void ScheduleNetworkRefresh(int delayMs = 1000)
    {
        //if (Api?.Side != EnumAppSide.Server) return;
        RegisterDelayedCallback(OnDelayedNetworkRefresh, delayMs);
    }

    private void advanceProductionByAmount(double hours)
    {
        double numLiters = mostRecentLitersPerHour * hours;
        ConsumeWaterProduceAcid((float)numLiters);
        Api?.Logger.Notification("[BirklandEyde] fake made " + numLiters.ToString() + " liters to account for time spent unloaded");
    }

    //mimic callback ongametick from: https://github.com/anegostudios/vssurvivalmod/blob/master/BlockEntity/BEBloomery.cs lines 166 and 102
    private void OnGameTick(float dt)
    {
        //stolen from here: https://github.com/anegostudios/vssurvivalmod/blob/849fa8cad9e392368566efc7474e73db6404a145/BlockEntity/BlockEntityFastForwardGrowth.cs
        //how in the god damn actual fucking shit does this work..?
        double hoursSinceLastUpdate = Api.World.Calendar.TotalHours - totalHoursLastUpdate;
        //if (hoursSinceLastUpdate < 0)
        //{
               // We need to rollback time when the blockEntity saved date is ahead of the calendar date: can happen if a schematic is imported
        //        onRollback(-hoursSinceLastUpdate);
        //        totalHoursLastUpdate = Api.World.Calendar.TotalHours;
        //}
        if (hoursSinceLastUpdate > 0.1)
        {
            //if player was away for 6 or more minutes, do additional stuff
            Api?.Logger.Notification("[BirklandEyde] chunk was unloaded, NEED to advance OnGameTick by an amount but this feature hasn't been programmed yet. totalHoursLastUpdate=" + totalHoursLastUpdate + " Api.World.Calendar.TotalHours=" + Api.World.Calendar.TotalHours.ToString());

            //do stuff
            double numHrs = Api.World.Calendar.TotalHours - totalHoursLastUpdate;
            advanceProductionByAmount(numHrs);

            //force it to reconnect to fix yet another useless disconnect bug. god, i fucking hate how picky electric progressives is.
            //ScheduleNetworkRefresh(2000);
        }

        totalHoursLastUpdate = Api.World.Calendar.TotalHours;

        
        
        try
        {
            //todo: try to reverse engineer the particle code in BEBehaviorBurning.onAsyncParticles() to figure out how to spawn particles
            if (Api.Side == EnumAppSide.Server)
            {
                long nowMs = Api.World.ElapsedMilliseconds;
                float dtSeconds = (nowMs - lastUpdateMs) / 1000f;
                lastUpdateMs = nowMs;

                var be = GetBehavior<BEBehaviorBirkelandEydeMini>();
                /*EParams[] allParams = ElectricalProgressive.AllEparams;
                BlockFacing face = ElectricalProgressive.AllEparams != null ? FacingHelper.Faces(ElectricalProgressive.Connection).FirstOrDefault() : null;

                //this doesnt work at all because voltage and current always comes back as 0 no matter what.
                if (allParams != null && face != null && face.Index < allParams.Length && allParams[face.Index] != null)
                {
                    EParams myParams = allParams[face.Index];
                    float primaryVoltage = myParams.voltage;
                    float primaryCurrent = myParams.current;
                    receivedPower = primaryVoltage * primaryCurrent;
                    Api.Logger.Notification("[BirklandEyde] rp=" + receivedPower.ToString());
                }*/

                string s = Api.Side.ToString();
                //Api.Logger.Notification("[BirklandEyde]" + s + " doing actual tick. dtSeconds= " + dtSeconds.ToString() + " receivedPower=" + be.receivedPower.ToString() + " be.WaterLitres=" + WaterLitres.ToString());

                //if you disable all the checks that cause it to abort, ConsumeWaterProduceAcid still can't actually modify anything
                if (dtSeconds <= 0 || receivedPower <= 0 || WaterLitres <= 0)
                {
                    //Block.LightHsv = new byte[] {0, 0, 0};
                    /*if (lightExists)
                    {
                        Api?.Logger.Notification("[BirklandEyde] removing light");
                        byte[] lighthsv = new byte[] { 195, 200, 14 };
                        Api.World.BlockAccessor.RemoveBlockLight(lighthsv, Pos);
                        //crappilyHackRemoveLight(Pos);
                        lightExists = false;
                    }*/
                    mostRecentHoursTillFull = 9999;
                    mostRecentLitersPerHour = 0;
                    MarkDirty(true);//maybe this will make the light turn off. edit: it didn't work.
                    return;
                }
                else
                {
                    /*if (!lightExists)
                    {
                        Api?.Logger.Notification("[BirklandEyde] adding light (by doing nothing and just letting the GetLightHsv function do its thing without interfering with it)");
                        byte[] lighthsv = new byte[] { 195, 200, 14 };
                        //crappilyHackAddLight(lighthsv, Pos);
                        lightExists = true;
                    }*/
                }

                float powerFraction = GameMath.Clamp(be.receivedPower / MaxConsumptionWatts, 0f, 1f);
                float litresPerSecondAtFull = FullProductionLitresPerHour / 3600f;//multiply by 1000 to make it faster for debugging purposes
                float litresThisTick = System.Math.Min(litresPerSecondAtFull * powerFraction * dtSeconds, WaterLitres);

                //Api.Logger.Notification("[BirklandEyde] running ConsumeWaterProduceAcid powerFraction=" + powerFraction.ToString() + " litresPerSecondAtFull=" + litresPerSecondAtFull.ToString() + " litresThisTick=" + litresThisTick.ToString());
                ConsumeWaterProduceAcid(litresThisTick);
                mostRecentHoursTillFull = HoursTillFull(litresThisTick);
                //Api?.Logger.Notification("set mostRecentHoursTillFull to " + mostRecentHoursTillFull.ToString());
            }

            try
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    if (receivedPower > 0)
                    {
                        if (!lightExists)
                        {
                            Api?.Logger.Notification("[BirklandEyde] adding light");
                            lightExists = true;
                        }

                        /*long nowMs = Api.World.ElapsedMilliseconds;
                        Api?.Logger.Notification("nowMs=" + nowMs.ToString() + " lastSoundMs=" + lastSoundMs.ToString() + " SoundIntervalMs=" + SoundIntervalMs.ToString());
                        if (nowMs - lastSoundMs >= SoundIntervalMs)
                        {
                            lastSoundMs = nowMs;
                            Api.World.PlaySoundAt(new AssetLocation("birkelandeyde:sounds/zapper/zap_oneshot_short"), Pos, 0.25, null, false, 16f, 0.2f);
                        }*/
                        if (zappingSound == null)
                        {
                            zappingSound = (Api as ICoreClientAPI).World.LoadSound(new SoundParams()
                            {
                                Location = new AssetLocation("birkelandeyde:sounds/zapper/zap_oneshot_short"),
                                ShouldLoop = true,
                                Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                                DisposeOnFinish = false,
                                Volume = 0.3f,
                                ReferenceDistance = 1f,
                                Range = 8,
                            });
                            //zappingSound.SetPosition(Pos.ToVec3f());
                            zappingSound.Start();
                        }
                    }
                    else
                    {
                        if (lightExists)
                        {
                            //Api?.Logger.Notification("[BirklandEyde] removing light");
                            Api.World.BlockAccessor.RemoveBlockLight(BirkelandEydeModSystem.lighthsv, Pos);
                            lightExists = false;
                        }

                        if (zappingSound != null)
                        {
                            zappingSound.Stop();
                            zappingSound.Dispose();
                            zappingSound = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Api?.Logger.Error("[BirklandEyde] failed while creating a dynamic light for some stupid reason: " + e.InnerException.ToString());
            }
        }
        catch (Exception e)
        {
            Api?.Logger.Error("[BirklandEyde] detected a possible orphan callback but here's the exception: " + e.Message);
            UnregisterAllTickListeners();

            /*
            UnregisterGameTickListener(listenerId);

            if (Api.Side == EnumAppSide.Server)
            {
                UnregisterGameTickListener(listenerId2);      
            }*/
        }
    }

    // Cache the FieldInfo so reflection isn't doing a string lookup every time this function runs
    private static readonly FieldInfo MaxDynLightsField = typeof(ClientMain).GetField(
        "maxDynLights",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    //a crappy hack fuck to maybe add lights
    /*public void crappilyHackAddLight(byte[] lighthsv, BlockPos pos)
    {
        if (Api.World is ClientMain d)
        {
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight");
            int cnt = d.shUniforms.PointLightsCount;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 1");

            // Fetch the private field value from instance 'd' and cast it to an int
            int maxDynLights = (int)MaxDynLightsField.GetValue(d);
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 2");

            if (cnt >= maxDynLights)
            {
                Api.Logger.Warning("there are too many dynamic lights so a new one couldn't be created");
                return;
            }
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 3");

            // Rest of your logic...
            Vec4d inval = new Vec4d();
            Vec4d outval = new Vec4d();
            Vec3f outval3 = new Vec3f();
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 4");

            inval.Set(pos.X, pos.InternalY, pos.Z, 1.0);
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 5");
			Mat4d.MulWithVec4(d.CurrentModelViewMatrixd, inval, outval);
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 6");
			outval.W = (double)lighthsv[2];
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 7");
			d.shUniforms.PointLights3[3 * cnt] = (float)outval.X;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 8");
			d.shUniforms.PointLights3[3 * cnt + 1] = (float)outval.Y;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 9");
			d.shUniforms.PointLights3[3 * cnt + 2] = (float)outval.Z;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 10");
			int v = (int)lighthsv[2];
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 11");

			int num = (int)d.WorldMap.hueLevels[(int)lighthsv[0]];
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 12");
			int blocks = (int)d.WorldMap.satLevels[(int)lighthsv[1]];
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 13");
			int blockv = (int)(d.WorldMap.BlockLightLevels[v] * 255f);
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 14");
			ColorUtil.ToRGBVec3f(ColorUtil.HsvToRgba(num, blocks, blockv), ref outval3);
            //ColorUtil.ToRGBVec3f(ColorUtil.HsvToRgba((int)lighthsv[0], (int)lighthsv[1], blockv), ref outval3);
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 15");
			d.shUniforms.PointLightColors3[3 * cnt] = outval3.Z * (float)v;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 16");
			d.shUniforms.PointLightColors3[3 * cnt + 1] = outval3.Y * (float)v;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 17");
			d.shUniforms.PointLightColors3[3 * cnt + 2] = outval3.X * (float)v;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 18");
			d.shUniforms.PointLightsCount++;
            Api?.Logger.Notification("[BirklandEyde] crappilyHackAddLight step 19");
        }
    }*/

    //a crappy hack fuck to maybe remove lights after adding them
    /*public void crappilyHackRemoveLight(BlockPos pos)
    {
        if (Api.World is ClientMain d)
        {
            Api?.Logger.Notification("[BirklandEyde] crappilyHackRemoveLight");
            int cnt = d.shUniforms.PointLightsCount;
            if (cnt <= 0) return;

            // 1. Calculate the view-space coordinates of the entity (exactly how you placed it)
            Vec4d inval = new Vec4d();
            Vec4d outval = new Vec4d();
            inval.Set(pos.X, pos.InternalY, pos.Z, 1.0);
            Mat4d.MulWithVec4(d.CurrentModelViewMatrixd, inval, outval);

            float targetX = (float)outval.X;
            float targetY = (float)outval.Y;
            float targetZ = (float)outval.Z;

            int targetIndex = -1;
            float epsilon = 0.01f; // Margin of error due to floating-point precision

            // 2. Scan the shader array to find the matching light index
            for (int i = 0; i < cnt; i++)
            {
                float dx = d.shUniforms.PointLights3[3 * i] - targetX;
                float dy = d.shUniforms.PointLights3[3 * i + 1] - targetY;
                float dz = d.shUniforms.PointLights3[3 * i + 2] - targetZ;

                // If the positions match closely, we found our light
                if (Math.Abs(dx) < epsilon && Math.Abs(dy) < epsilon && Math.Abs(dz) < epsilon)
                {
                    targetIndex = i;
                    break;
                }
            }

            // If we didn't find a matching light, exit early
            if (targetIndex == -1) return;

            // 3. Shift all elements after the target index down by 1 slot (Overwriting the removed light)
            for (int i = targetIndex; i < cnt - 1; i++)
            {
                // Move positions
                d.shUniforms.PointLights3[3 * i] = d.shUniforms.PointLights3[3 * (i + 1)];
                d.shUniforms.PointLights3[3 * i + 1] = d.shUniforms.PointLights3[3 * (i + 1) + 1];
                d.shUniforms.PointLights3[3 * i + 2] = d.shUniforms.PointLights3[3 * (i + 1) + 2];

                // Move colors
                d.shUniforms.PointLightColors3[3 * i] = d.shUniforms.PointLightColors3[3 * (i + 1)];
                d.shUniforms.PointLightColors3[3 * i + 1] = d.shUniforms.PointLightColors3[3 * (i + 1) + 1];
                d.shUniforms.PointLightColors3[3 * i + 2] = d.shUniforms.PointLightColors3[3 * (i + 1) + 2];
            }

            // 4. Clean up the trailing slot (optional, but good practice so data doesn't duplicate)
            int lastIdx = cnt - 1;
            d.shUniforms.PointLights3[3 * lastIdx] = 0;
            d.shUniforms.PointLights3[3 * lastIdx + 1] = 0;
            d.shUniforms.PointLights3[3 * lastIdx + 2] = 0;

            d.shUniforms.PointLightColors3[3 * lastIdx] = 0;
            d.shUniforms.PointLightColors3[3 * lastIdx + 1] = 0;
            d.shUniforms.PointLightColors3[3 * lastIdx + 2] = 0;

            // 5. Decrement the active light count so the engine stops rendering the last index
            d.shUniforms.PointLightsCount--;
        }
    }*/
}

public class BEBehaviorBirkelandEydeMini : BlockEntityBehavior, IElectricConsumer
{
    private const float MaxConsumptionWatts = 100f;
    private const float FullProductionLitresPerHour = 1f; // tune this — acid produced per hour at full 100W
    public float receivedPower;
    private long lastUpdateMs;
    private float concentration;
    public float AvgConsumeCoeff { get; set; }
    public float currentOutputVoltage { get; set; }

    private bool removed = false;

    SimpleParticleProperties particle;
    private ICoreClientAPI capi;
    public static Vec3d defaultSparkOffset => new Vec3d(0.5, 0.2, 0.1);

    /// <summary>
    /// required constructor in order for it to be valid
    /// </summary>
    /// <param name="blockEntity"></param>
    public BEBehaviorBirkelandEydeMini(BlockEntity blockEntity) : base(blockEntity)
    {
        AvgConsumeCoeff = 5;//placeholder, i dont know what this does
        //concentration = 0.0f;
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        lastUpdateMs = api.World.ElapsedMilliseconds;

        capi = api as ICoreClientAPI;

        /*Block fireBlock = capi.World.GetBlock(new AssetLocation("fire"));
        int index = Math.Min(fireBlock.ParticleProperties.Length - 1, this.Api.World.Rand.Next(fireBlock.ParticleProperties.Length + 1));
        sparkParticles = fireBlock.ParticleProperties[index];*/



        if (capi != null)
        {
            capi.Event.RegisterAsyncParticleSpawner(onAsyncParticles);
        }



        //taken from BEBehaviorBurning
        //initSoundsAndTicking();
    }



    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        if (Blockentity is BlockEntityBirkelandEydeMini be)
        {
            /*dsc.AppendLine("water: " + hasWater.ToString());
            int c = (int)(concentration * 100.0f);
            float cc = c / 100.0f;
            dsc.AppendLine("acid concentration: " + cc.ToString() + "%");
            dsc.Append("primary voltage: " + "0V");
            dsc.Append("secondary voltage: " + "0V");
            dsc.AppendLine();*/
            //Api.Logger.Notification("[BirkelandEyde] GetBlockInfo. receivedPower= " + be.receivedPower.ToString());
            dsc.AppendLine("water: " + be.WaterLitres.ToString("0.0") + "L / " + BlockEntityBirkelandEydeMini.MaxWaterLitres + "L");
            double c, ph;
            c = be.GetAcidPercentage(out ph);
            //float c = (be.AcidLitres / be.WaterLitres) * 68f; //68% is the azeotrope of nitric acid
            dsc.AppendLine("acid concentration: " + c.ToString("0.00") + "%");
            //dsc.AppendLine("acid: " + be.AcidLitres.ToString("0.00") + "L");
            //float ph = (float)BirkelandEydeModSystem.WeightedInterpolate(7d, -1.2d, (be.AcidLitres / be.WaterLitres));
            dsc.AppendLine("ph: " + ph.ToString("0.00") + "%");
            
            //Api?.Logger.Notification("be.mostRecentHoursTillFull= " + be.mostRecentHoursTillFull.ToString());
            if (be.ProductToExtract() && be.WaterLitres > 0)
            {
                dsc.AppendLine("Reaction complete");   
            }
            else if (be.WaterLitres == 0)
            {
                dsc.AppendLine("Add water to begin producing nitric acid");
            }
            else if (be.receivedPower <= 0)
            {
                dsc.AppendLine("Time remaining: No power. Reaction stopped");   
            }
            else
            {
                float vshours = BirkelandEydeModSystem.IrlToVsHours(be.mostRecentHoursTillFull);
                if (vshours < 24)
                {
                    dsc.AppendLine("Time remaining: " + vshours.ToString("0.00") + " hours");  
                }
                else
                {
                    dsc.AppendLine("Time remaining: " + (vshours/24.0f).ToString("0.0") + " days");  
                } 
            }

            //amps doesnt work, it has to be derived
            float calculatedAmps = be.receivedPower / be.receivedVoltage;

            const float TransformerRatio = 313f;
            float secondaryVoltage = be.receivedVoltage * TransformerRatio;
            float secondaryCurrent = calculatedAmps / TransformerRatio;

            dsc.AppendLine("power: " + be.receivedPower.ToString("0") + "W / " + MaxConsumptionWatts + "W");
            dsc.AppendLine("primary voltage: " + be.receivedVoltage.ToString("0") + "V");
            dsc.AppendLine("primary current: " + calculatedAmps.ToString("0.00") + "A");
            dsc.AppendLine("secondary voltage: " + secondaryVoltage.ToString("0") + "V");
            dsc.AppendLine("secondary current: " + secondaryCurrent.ToString("0.0000") + "A");
        }
    }

    //taken from BEBehaviorBurning
    private void initSoundsAndTicking()
    {
        ICoreClientAPI c = Api as ICoreClientAPI;

        c.Event.RegisterAsyncParticleSpawner(new ContinousParticleSpawnTaskDelegate(onAsyncParticles));
    }

    public static Vec3d GetRotatedOffset(BlockEntityBirkelandEydeMini be, Vec3d northFacingOffset)
    {
        Facing facing = be.ModelFacing;
        if (facing == Facing.None) return northFacingOffset;

        Vec3d rotationOrigin = new Vec3d(0.5, 0.5, 0.5); // pivot = block center, matches box/mesh rotation elsewhere

        Cuboidf point = new Cuboidf(
            (float)northFacingOffset.X, (float)northFacingOffset.Y, (float)northFacingOffset.Z,
            (float)northFacingOffset.X, (float)northFacingOffset.Y, (float)northFacingOffset.Z
        );

        Cuboidf[] boxes = { point.RotatedCopy(0f, 90f, 180f, rotationOrigin) };
        FacingRotations.ApplyRotations(boxes, facing);

        return new Vec3d(boxes[0].X1, boxes[0].Y1, boxes[0].Z1);
    }

    private bool onAsyncParticles(float dt, IAsyncParticleManager manager)
    {
        if (removed || Blockentity == null) return false;

        if (Blockentity is BlockEntityBirkelandEydeMini be)
        {
            if (be.receivedPower > 0)
            {
                Vec3d offset = GetRotatedOffset(be, new Vec3d(0.5, 0.2, 1));
                Vec3d pos = be.Pos.ToVec3d().Add(offset); // now actually uses the rotated value
                BirkelandEyde.Utils.ParticleManager.SpawnElectricSparksAsync(manager, pos, new Vec3d(0.1, 0.0, 0.1));
            }
        }

        return true;
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        removed = true;
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        removed = true;
    }

    private void GetInfoFromServer(float amount)
    {
        if (Api.Side == EnumAppSide.Server)
        {
            if (Blockentity is BlockEntityBirkelandEydeMini be)
            {
                receivedPower = amount;
                be.receivedPower = amount;
                EParams[] allParams = be.ElectricalProgressive.AllEparams;
                BlockFacing face = be.ElectricalProgressive != null ? FacingHelper.Faces(be.ElectricalProgressive.Connection).FirstOrDefault() : null;

                if (allParams != null && face != null && face.Index < allParams.Length && allParams[face.Index] != null)
                {
                    EParams myParams = allParams[face.Index];
                    be.receivedVoltage = myParams.voltage;
                    be.receivedAmps = myParams.current;
                }
            }

        }
    }

    public float Consume_request()
    {
        //not sure if this is watts or amps. treating it as watts for now
        //Api?.Logger.Notification("[BirklandEyde] returning Consume_request = 100f");
        //return MaxConsumptionWatts;//maybe for some reason MaxConsumptionWatts initialization isn't respected for some reason
        return 100f;
    }

    public void Consume_receive(float amount)
    {
        //amount is ALWAYS 0 so this doesn't work correctly. I can't find where in the electrical progressive mod suite source code this ever gets called. The source code is incredibly complicated just like everything else in this game's source code
        //Api?.Logger.Notification("[BirklandEyde] Consume_receive called, amount=" + amount);
        //receivedPower = amount;
        GetInfoFromServer(amount);
        Blockentity.MarkDirty(true);   //maybe mark dirty forces a sync which will magically fix the consume recieve bug. edit: nope, didn't make a difference
    }

    public float getPowerReceive()
    {
        //return a placeholder value just to get it to compile
        return receivedPower;
    }

    public float getPowerRequest()
    {
        //not sure if this is watts or amps. treating it as watts for now
        return MaxConsumptionWatts;
    }

    public void Update()
    {
        /*
        Api.Logger.Notification("[BirklandEyde] update");
        if (Api == null || Blockentity is not BlockEntityBirkelandEydeMini be) return;

        long nowMs = Api.World.ElapsedMilliseconds;
        float dtSeconds = (nowMs - lastUpdateMs) / 1000f;
        lastUpdateMs = nowMs;

        //var electricProgressive = Blockentity.GetBehavior<BEBehaviorElectricalProgressive>();
        EParams[] allParams = be.ElectricalProgressive.AllEparams;
        BlockFacing face = be.ElectricalProgressive.AllEparams != null ? FacingHelper.Faces(be.ElectricalProgressive.Connection).FirstOrDefault() : null;

        //this doesnt work at all because voltage and crrent always comes back as 0 no matter what. this function only ever gets called on the client side

        if (allParams != null && face != null && face.Index < allParams.Length && allParams[face.Index] != null)
        {
            EParams myParams = allParams[face.Index];
            float primaryVoltage = myParams.voltage;
            float primaryCurrent = myParams.current;
            receivedPower = primaryVoltage * primaryCurrent;
            Api.Logger.Notification("[BirklandEyde] rp=" + receivedPower.ToString());
        }
        
        Api.Logger.Notification("[BirklandEyde] doing actual tick. dtSeconds= " + dtSeconds.ToString() + " receivedPower=" + receivedPower.ToString() + " be.WaterLitres=" + be.WaterLitres.ToString());

        //if you disable all the checks that cause it to abort, ConsumeWaterProduceAcid still can't actually modify anything
        if (dtSeconds <= 0 || receivedPower <= 0 || be.WaterLitres <= 0) return;


        float powerFraction = GameMath.Clamp(receivedPower / MaxConsumptionWatts, 0f, 1f);
        float litresPerSecondAtFull = FullProductionLitresPerHour / 3600f;
        float litresThisTick = System.Math.Min(litresPerSecondAtFull * powerFraction * dtSeconds, be.WaterLitres);

        Api.Logger.Notification("[BirklandEyde] running ConsumeWaterProduceAcid powerFraction=" + powerFraction.ToString() + " litresPerSecondAtFull=" + litresPerSecondAtFull.ToString() + " litresThisTick=" + litresThisTick.ToString());
        be.ConsumeWaterProduceAcid(litresThisTick);
        */

        //im pretty sure the intended usage of this is to swap out burned, activated and deactivated versions of the mesh on the client side and the only acceptable way of getting that data is to get it from: BlockEntityBirkelandEydeMini BlockEntity
        //doesn't do anything

        if (Blockentity is BlockEntityBirkelandEydeMini be)
        {
            //if (Api.Side == EnumAppSide.Client)
            //{
            /*if (be.receivedPower > 0)
            {
                if (!be.lightExists)
                {
                    Api?.Logger.Notification("[BirklandEyde] adding light");
                    byte[] lighthsv = new byte[] { 195, 200, 14 };
                    be.crappilyHackAddLight(lighthsv, Pos);
                    be.lightExists = true;
                }
            }
            else
            {
                if (be.lightExists)
                {
                    Api?.Logger.Notification("[BirklandEyde] removing light");
                    be.crappilyHackRemoveLight(Pos);
                    be.lightExists = false;
                }
            }*/
            //}
            /*if (capi != null && 3 == 3)
            {
                Api.Logger.Notification("[BirkelandEyde] spawning particles");
                SimpleParticleProperties s = new SimpleParticleProperties()
                {
                    MinPos = be.Pos.ToVec3d(),
                    AddPos = new Vec3d(0, 0, 0),
                    MinVelocity = new Vec3f(0, 0, 0),
                    AddVelocity = new Vec3f(0, 0, 0),
                    LifeLength = 2f,
                    MinQuantity = 1,
                    ParticleModel = EnumParticleModel.Cube,
                    Color = ColorUtil.ToRgba(25, 0, 255, 255),
                    MinSize = 6f,
                    MaxSize = 6f
                };

                capi.World.SpawnParticles(s);
            }*/
        }
    }
}