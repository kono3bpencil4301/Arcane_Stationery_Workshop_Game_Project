using Godot;

public class WeaponInstance
{
    public WeaponData Data { get; private set; }

    public float CurrentCharge { get; set; }

    public bool IsBound { get; private set; }
    public float CooldownRemaining { get; set; }
    public float SkillCooldownRemaining { get; set; }
    public float SkillCooldownDuration { get; set; }

    public float SkillCooldownRatio
    {
        get
        {
            float duration = SkillCooldownDuration;
            return duration <= 0.0f
                ? 0.0f
                : Mathf.Clamp(SkillCooldownRemaining / duration, 0.0f, 1.0f);
        }
    }


    public bool CanDrop
    {
        get
        {
            return !IsBound &&
                   Data.AllowDrop;
        }
    }


    public WeaponInstance(
        WeaponData data,
        bool isBound = false)
    {
        Data = data;

        IsBound = isBound;

        CurrentCharge = 0.0f;
        SkillCooldownDuration = 0.0f;
    }


}
