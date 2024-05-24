﻿using SPT.Reflection.Patching;
using SPT.SinglePlayer.Models.ScavMode;
using Comfort.Common;
using EFT.InventoryLogic.BackendInventoryInteraction;
using EFT.InventoryLogic;
using HarmonyLib;
using System.Reflection;
using System.Threading.Tasks;
using System;
using SPT.Reflection.Utils;

namespace SPT.SinglePlayer.Patches.ScavMode
{
    /**
     * When the user clicks "Sell All" after a scav raid, create a custom request object
     * that includes the calculated sell price
     */
    public class ScavSellAllRequestPatch : ModulePatch
    {
        private static MethodInfo _sendOperationMethod;
        private string TargetMethodName = "SellAllFromSavage";

        protected override MethodBase GetTargetMethod()
        {
            // We want to find a type that contains `SellAllFromSavage` but doesn't extend from `IBackendStatus`
            Type targetType = PatchConstants.EftTypes.SingleCustom(IsTargetType);

            Logger.LogDebug($"{this.GetType().Name} Type: {targetType?.Name}");

            // So we can call "SendOperationRightNow" without directly referencing a GClass
            _sendOperationMethod = AccessTools.Method(targetType, "SendOperationRightNow");

            return AccessTools.Method(targetType, TargetMethodName);
        }

        private bool IsTargetType(Type type)
        {
            // Isn't an interface, isn't part of the dummy class, and contains our target method
            if (!type.IsInterface
                && type.DeclaringType != typeof(BackendDummyClass)
                && type.GetMethod(TargetMethodName) != null)
            {
                return true;
            }

            return false;
        }

        [PatchPrefix]
        private static bool PatchPrefix(object __instance, ref Task<IResult> __result, string playerId, string petId)
        {
            // Build request with additional information
            OwnerInfo fromOwner = new OwnerInfo
            {
                Id = petId,
                Type = EOwnerType.Profile
            };
            OwnerInfo toOwner = new OwnerInfo
            {
                Id = playerId,
                Type = EOwnerType.Profile
            };

            SellAllRequest request = new SellAllRequest
            {
                Action = "SellAllFromSavage",
                TotalValue = ScavSellAllPriceStorePatch.StoredPrice, // Retrieve value stored in earlier patch
                FromOwner = fromOwner, // Scav
                ToOwner = toOwner // PMC
            };

            // We'll re-use the same logic/methods that the base code used
            TaskCompletionSource<IResult> taskCompletionSource = new TaskCompletionSource<IResult>();
            _sendOperationMethod.Invoke(__instance, new object[] { request, new Callback(taskCompletionSource.SetResult) });
            __result = taskCompletionSource.Task;

            // Skip original
            return false;
        }
    }
}