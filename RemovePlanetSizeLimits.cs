using Sandbox.Game.World.Generator;
using System;
using HarmonyLib;
using VRage.Library.Utils;
using VRage.Noise;
using VRageMath;
using VRage.Plugins;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using VRage.Input;
using Sandbox;
using VRage.Voxels.Clipmap;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;
using System.Reflection;

namespace RemovePlanetSizeLimits
{
    public class RemovePlanetSizeLimits : IPlugin
    {
        public bool initialized = false;
        public bool renameScreenOpen = false;
        public Type tMyTerminalControlPanel;
        public RemovePlanetSizeLimits()
        {
            var harmony = new Harmony("RemovePlanteSizeLimits");
            var other = typeof(MyGuiScreenEditor).Assembly.GetType("Sandbox.Game.Gui.MyGuiScreenDebugSpawnMenu");
            var CreatePlanetsSpawnMenu = AccessTools.Method(other, "CreatePlanetsSpawnMenu");
            harmony.Patch(CreatePlanetsSpawnMenu, null, new HarmonyMethod(typeof(Patch_MyGuiScreenDebugSpawnMenu), "Postfix"));
            harmony.Patch(AccessTools.Method(typeof(MyVoxelClipmap), "ApplySettings"), null, null, new HarmonyMethod(AccessTools.Method(typeof(Patch_ApplySettings), "Transpiler")));
        }

        public void Init(object gameObject)
        {
            initialized = true;
        }
        public void Update()
        {
        }

        public void Dispose()
        {

        }
    }
    public static class Patch_ApplySettings
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();
            for (var i = 0; i < instructionsList.Count; i++)
            {

                var instruction = instructionsList[i];
                if (instruction.opcode == OpCodes.Stloc_0 && instructionsList[i - 1].opcode == OpCodes.Call && (MethodInfo)instructionsList[i - 1].operand == AccessTools.Method(typeof(MathHelper), "Log2Ceiling"))
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Math), "Min", new Type[] { typeof(int), typeof(int) }));
                    yield return new CodeInstruction(OpCodes.Stloc_0);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
    public static class Patch_MyGuiScreenDebugSpawnMenu
    {
        public static bool AmountDialogEnabled = true;
        public static void Postfix(object __instance)
        {
            MyGuiControlSlider m_planetSizeSlider = (MyGuiControlSlider)AccessTools.Field(__instance.GetType(), "m_planetSizeSlider").GetValue(__instance);
            m_planetSizeSlider.MaxValue = 2500000;
            m_planetSizeSlider.MinValue = 1000;
            m_planetSizeSlider.SliderClicked = new Func<MyGuiControlSlider, bool>(OnSliderClicked);
        }
        public static bool OnSliderClicked(MyGuiControlSlider arg)
        {
            if (AmountDialogEnabled && MyInput.Static.IsAnyCtrlKeyPressed())
            {
                float min = arg.MinValue;
                float max = arg.MaxValue;
                float value = arg.Value;
                MyGuiScreenDialogAmount myGuiScreenDialogAmount = new MyGuiScreenDialogAmount(min, max, MyCommonTexts.DialogAmount_SetValueCaption, 3, false, new float?(value), MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity);
                myGuiScreenDialogAmount.OnConfirmed += (float newValue) =>
                {
                    arg.Value = newValue;
                };
                MyGuiSandbox.AddScreen(myGuiScreenDialogAmount);
                return true;
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(MyProceduralPlanetCellGenerator), MethodType.Constructor, new Type[] { typeof(int), typeof(double), typeof(float), typeof(float), typeof(float), typeof(float), typeof(MyProceduralWorldModule) })]
    public static class Patch_MyProceduralPlanetCellGenerator
    {
        public static bool Prefix(object __instance, int seed, double density, float planetSizeMax, float planetSizeMin, float moonSizeMax, float moonSizeMin, MyProceduralWorldModule parent = null)
        {
            MyProceduralPlanetCellGenerator instance = (MyProceduralPlanetCellGenerator)__instance;
            if (planetSizeMax < planetSizeMin)
            {
                float num = planetSizeMax;
                planetSizeMax = planetSizeMin;
                planetSizeMin = num;
            }
            AccessTools.Field(typeof(MyProceduralPlanetCellGenerator), "PLANET_SIZE_MAX").SetValue(instance, planetSizeMax);
            AccessTools.Field(typeof(MyProceduralPlanetCellGenerator), "PLANET_SIZE_MIN").SetValue(instance, planetSizeMin);
            //instance.PLANET_SIZE_MAX = planetSizeMax;
            //instance.PLANET_SIZE_MIN = planetSizeMin;
            if (moonSizeMax < moonSizeMin)
            {
                float num2 = moonSizeMax;
                moonSizeMax = moonSizeMin;
                moonSizeMin = num2;
            }
            AccessTools.Field(typeof(MyProceduralPlanetCellGenerator), "MOON_SIZE_MAX").SetValue(instance, moonSizeMax);
            AccessTools.Field(typeof(MyProceduralPlanetCellGenerator), "MOON_SIZE_MIN").SetValue(instance, moonSizeMin);
            //instance.MOON_SIZE_MAX = moonSizeMax;
            //instance.MOON_SIZE_MIN = moonSizeMin;
            AccessTools.Field(typeof(MyProceduralPlanetCellGenerator), "OBJECT_SEED_RADIUS").SetValue(instance, (double)planetSizeMax / 2.0 * 1.1 + 2.0 * ((double)moonSizeMax / 2.0 * 1.1 + 64000.0));
            //instance.OBJECT_SEED_RADIUS = (double)instance.PLANET_SIZE_MAX / 2.0 * 1.1 + 2.0 * ((double)instance.MOON_SIZE_MAX / 2.0 * 1.1 + 64000.0);
            AccessTools.Method(typeof(MyProceduralWorldModule), "AddDensityFunctionFilled").Invoke(instance, new object[] { new MyInfiniteDensityFunction(MyRandom.Instance, 0.001) });
            return false;
        }
    }
    internal class MyInfiniteDensityFunction : IMyAsteroidFieldDensityFunction, IMyModule
    {
        // Token: 0x060053C7 RID: 21447 RVA: 0x001E727C File Offset: 0x001E547C
        public MyInfiniteDensityFunction(MyRandom random, double frequency)
        {
            this.noise = new MySimplexFast(random.Next(), frequency);
        }

        // Token: 0x060053C8 RID: 21448 RVA: 0x0000822C File Offset: 0x0000642C
        public bool ExistsInCell(ref BoundingBoxD bbox)
        {
            return true;
        }

        // Token: 0x060053C9 RID: 21449 RVA: 0x001E7296 File Offset: 0x001E5496
        public double GetValue(double x)
        {
            return this.noise.GetValue(x);
        }

        // Token: 0x060053CA RID: 21450 RVA: 0x001E72A4 File Offset: 0x001E54A4
        public double GetValue(double x, double y)
        {
            return this.noise.GetValue(x, y);
        }

        // Token: 0x060053CB RID: 21451 RVA: 0x001E72B3 File Offset: 0x001E54B3
        public double GetValue(double x, double y, double z)
        {
            return this.noise.GetValue(x, y, z);
        }

        // Token: 0x04003FEC RID: 16364
        private IMyModule noise;
    }
}
