public class WeaponSlot
{
    public WeaponInstance? Instance;

    public WeaponBase? Controller;


    public bool IsEmpty
    {
        get
        {
            return Instance == null;
        }
    }
}