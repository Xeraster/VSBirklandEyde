using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BirkelandEyde.Utils
{
    public static class ParticleManager
    {
        private static readonly AdvancedParticleProperties SparksTemplateAdvanced = ParticleManager.CreateSparksTemplate();
        private static readonly Random rand = new Random();

        private static AdvancedParticleProperties CreateSparksTemplate()
        {
            AdvancedParticleProperties advancedParticleProperties = new AdvancedParticleProperties();
            advancedParticleProperties.Quantity.avg = 7.5f;
            advancedParticleProperties.Quantity.var = 2.5f;

            // Hue ~265-280 = blue-violet arc; wide var so it flickers between electric blue and violet
            advancedParticleProperties.HsvaColor[0].avg = 195f;
            advancedParticleProperties.HsvaColor[0].var = 15f;
            advancedParticleProperties.HsvaColor[1].avg = 200f; // slightly desaturated so it doesn't look neon/cartoonish
            advancedParticleProperties.HsvaColor[1].var = 30f;
            advancedParticleProperties.HsvaColor[2].avg = 255f; // full brightness — arcs are intensely bright
            advancedParticleProperties.HsvaColor[2].var = 10f;
            advancedParticleProperties.HsvaColor[3].avg = 255f;
            advancedParticleProperties.HsvaColor[3].var = 10f;

            advancedParticleProperties.Velocity[0].avg = 0f;
            advancedParticleProperties.Velocity[1].avg = 0.3f; // less upward drift — arcs jitter in place more than they fly
            advancedParticleProperties.Velocity[2].avg = 0f;
            advancedParticleProperties.Velocity[0].var = 0.75f; // more erratic horizontal jitter for that jacob's-ladder crackle
            advancedParticleProperties.Velocity[1].var = 0.75f;
            advancedParticleProperties.Velocity[2].var = 0.75f;

            advancedParticleProperties.WindAffectednes = 0f;
            advancedParticleProperties.LifeLength.avg = 0.04f; // 1/10th of original 0.5f
            advancedParticleProperties.LifeLength.var = 0.01f;  // 1/10th of original 0.1f
            advancedParticleProperties.GravityEffect.avg = 0.05f; // was 1f — arcs shouldn't visibly fall
            advancedParticleProperties.GravityEffect.var = 0f;

            advancedParticleProperties.Bounciness = 0f; // no more bouncing onto the table/floor
            advancedParticleProperties.VertexFlags = 255;
            advancedParticleProperties.ParticleModel = EnumParticleModel.Cube;
            advancedParticleProperties.Size.avg = 0.25f;
            advancedParticleProperties.Size.var = 0.1f;
            //advancedParticleProperties.LightEmission = 255;
            return advancedParticleProperties;
        }
        private static AdvancedParticleProperties CreateSparksTemplate_old()
        {
            //GEE DO I LOVE HAVING TO WAIT FOR IT TO COMPILE, WAIT FOR THE GAME TO LOAD AND WAIT FOR IT TO LOAD MY SAVE JUST TO SEE WHAT EVERY MINOR CHANGE DID
            AdvancedParticleProperties advancedParticleProperties = new AdvancedParticleProperties();
            advancedParticleProperties.Quantity.avg = 7.5f;
            advancedParticleProperties.Quantity.var = 2.5f;
            advancedParticleProperties.HsvaColor[0].avg = 39f;
            advancedParticleProperties.HsvaColor[0].var = 2f;
            advancedParticleProperties.HsvaColor[1].avg = 255f;
            advancedParticleProperties.HsvaColor[1].var = 2f;
            advancedParticleProperties.HsvaColor[2].avg = 255f;
            advancedParticleProperties.HsvaColor[2].var = 2f;
            advancedParticleProperties.HsvaColor[3].avg = 255f;
            advancedParticleProperties.HsvaColor[3].var = 10f;
            advancedParticleProperties.Velocity[0].avg = 0f;
            advancedParticleProperties.Velocity[1].avg = 1.1f;
            advancedParticleProperties.Velocity[2].avg = 0f;
            advancedParticleProperties.Velocity[0].var = 3.5f;
            advancedParticleProperties.Velocity[1].var = 0.2f;
            advancedParticleProperties.Velocity[2].var = 3.5f;
            advancedParticleProperties.WindAffectednes = 0f;
            advancedParticleProperties.LifeLength.avg = 0.5f;
            advancedParticleProperties.LifeLength.var = 0.1f;
            advancedParticleProperties.GravityEffect.avg = 1f;
            advancedParticleProperties.GravityEffect.var = 0f;
            advancedParticleProperties.Bounciness = 1f;
            advancedParticleProperties.VertexFlags = 128;
            advancedParticleProperties.ParticleModel = EnumParticleModel.Cube;
            advancedParticleProperties.Size.avg = 0.25f;
            advancedParticleProperties.Size.var = 0.1f;
            return advancedParticleProperties;
        }

        public static void SpawnElectricSparksAsync(IAsyncParticleManager manager, Vec3d pos, Vec3d variationPos)
        {
            AdvancedParticleProperties particles = ParticleManager.SparksTemplateAdvanced.Clone();
            particles.WindAffectednesAtPos = 0.1f;
            particles.basePos = ParticleManager.RandomBlockPos(pos, variationPos);
            manager.Spawn(particles);
        }

        public static Vec3d RandomBlockPos(Vec3d pos, Vec3d variation)
        {
            return new Vec3d(pos.X + (ParticleManager.rand.NextDouble() * 2.0 - 1.0) * variation.X, pos.Y + (ParticleManager.rand.NextDouble() * 2.0 - 1.0) * variation.Y, pos.Z + (ParticleManager.rand.NextDouble() * 2.0 - 1.0) * variation.Z);
        }

    }
}