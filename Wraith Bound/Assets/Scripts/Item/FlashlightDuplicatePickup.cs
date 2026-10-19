using UnityEngine;

/// <summary>
/// 손전등을 이미 가진 상태에서 월드 손전등을 주우면, 인벤토리에 추가하지 않고 배터리를 가득 채웁니다.
/// 배터리가 이미 가득 차 있으면 처리하지 않음 → 기존 획득 흐름(최대 소지 수량으로 실패)을 타서 바닥에 그대로 남습니다.
/// </summary>
public static class FlashlightDuplicatePickup
{
    /// <returns>리필했으면 true (호출 측에서 월드 오브젝트 제거)</returns>
    public static bool TryRefill(GameObject interactor, InventoryManager inventory, Items itemData)
    {
        FlashlightEnergyController energy = GetRefillableEnergy(interactor, inventory, itemData);
        if (energy == null)
        {
            return false;
        }

        energy.RefillToFull();
        inventory.PlayItemPickupSound();
        return true;
    }

    /// <summary>상호작용 안내 문구용: 지금 주우면 리필되는지.</summary>
    public static bool WouldRefill(Items itemData)
    {
        GameObject player = GameObject.FindWithTag("Player");
        return player != null
            && GetRefillableEnergy(player, player.GetComponent<InventoryManager>(), itemData) != null;
    }

    private static FlashlightEnergyController GetRefillableEnergy(GameObject interactor, InventoryManager inventory, Items itemData)
    {
        if (interactor == null || inventory == null || !(itemData is Equipment equipment))
        {
            return null;
        }

        FlashlightEnergyController energy = interactor.GetComponent<FlashlightEnergyController>();
        if (energy == null
            || !energy.IsFlashlightEquipment(equipment)
            || inventory.CountItem(equipment) < 1
            || energy.IsFullyCharged)
        {
            return null;
        }

        return energy;
    }
}
