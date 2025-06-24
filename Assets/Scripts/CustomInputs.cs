using UnityEngine;

[CreateAssetMenu(fileName = "CustomInputs", menuName = "Game/Input Configuration")]
public class CustomInputs : ScriptableObject
{
    public KeyCode MoveUp = KeyCode.W;
    public KeyCode MoveDown = KeyCode.S;
    public KeyCode MoveLeft = KeyCode.A;
    public KeyCode MoveRight = KeyCode.D;

    public KeyCode Dodge = KeyCode.LeftShift;
    public KeyCode Ascend = KeyCode.Space;
    public KeyCode Shoot = KeyCode.Mouse0;
    public KeyCode Overboost = KeyCode.Q;
    public KeyCode Heal = KeyCode.F;
    public KeyCode RageMode = KeyCode.R;
    public KeyCode AdrenalineMode = KeyCode.E;

    public void SaveInputs()
    {
        PlayerPrefs.SetString("MoveUp", MoveUp.ToString());
        PlayerPrefs.SetString("MoveDown", MoveDown.ToString());
        PlayerPrefs.SetString("MoveLeft", MoveLeft.ToString());
        PlayerPrefs.SetString("MoveRight", MoveRight.ToString());
        PlayerPrefs.SetString("Dodge", Dodge.ToString());
        PlayerPrefs.SetString("Ascend", Ascend.ToString());
        PlayerPrefs.SetString("Shoot", Shoot.ToString());
        PlayerPrefs.SetString("Overboost", Overboost.ToString());
        PlayerPrefs.SetString("Heal", Heal.ToString());
        PlayerPrefs.SetString("RageMode", RageMode.ToString());
        PlayerPrefs.SetString("AdrenalineMode", AdrenalineMode.ToString());

        PlayerPrefs.Save();
    }

    public void LoadInputs()
    {
        MoveUp = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("MoveUp", MoveUp.ToString()));
        MoveDown = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("MoveDown", MoveDown.ToString()));
        MoveLeft = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("MoveLeft", MoveLeft.ToString()));
        MoveRight = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("MoveRight", MoveRight.ToString()));
        Dodge = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Dodge", Dodge.ToString()));
        Ascend = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Ascend", Ascend.ToString()));
        Shoot = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Shoot", Shoot.ToString()));
        Overboost = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Overboost", Overboost.ToString()));
        Heal = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Heal", Heal.ToString()));
        RageMode = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("RageMode", RageMode.ToString()));
        AdrenalineMode = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("AdrenalineMode", AdrenalineMode.ToString()));
    }
}
