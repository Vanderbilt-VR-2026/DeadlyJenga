namespace DeadlyJenga.Core
{
    public interface IDemoLoadoutControls
    {
        EquipmentKind RespawnEquipment {get;}
        bool GiveEveryoneEquipment(EquipmentKind kind,bool everyRespawn);
    }
}
