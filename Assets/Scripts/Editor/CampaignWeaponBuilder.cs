#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CampaignWeaponBuilder
{
    public static void Build()
    {
        const string folder = "Assets/Resources/CampaignWeapons";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources", "CampaignWeapons");
        string[] names = { "ChaliceBottle", "MineLantern", "Frozen_Tuna" };
        string[] keys = { "chalice", "mine_lantern", "secret_tuna" };
        for (int i = 0; i < names.Length; i++)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/" + names[i] + ".prefab");
            var go = Object.Instantiate(source);
            try
            {
                go.name = i == 2 ? "SecretTuna" : names[i];
                var weapon = go.GetComponent<Weapon>(); weapon.campaignKey = keys[i]; weapon.SetWeaponId(-1);
                weapon.startsAvailable = true;
                if (i == 2) weapon.SetDisplayName("봉인된 냉동참치");
                PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + go.name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
