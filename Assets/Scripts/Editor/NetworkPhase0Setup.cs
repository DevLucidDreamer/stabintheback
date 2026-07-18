#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetworkPhase0Setup
{
    private const string BootstrapName = "NetworkBootstrap";

    [MenuItem("Tools/Multiplayer/Phase 0/Create Empty Network Bootstrap")]
    public static void CreateEmptyNetworkBootstrap()
    {
        Type networkManagerType = FindType("NetworkManager");
        Type telepathyTransportType = FindType("TelepathyTransport");
        Type networkManagerHudType = FindType("NetworkManagerHUD");

        if (networkManagerType == null || telepathyTransportType == null)
        {
            EditorUtility.DisplayDialog(
                "Mirror is not imported",
                "Import Mirror first, then run this menu again.\nExpected components: Mirror.NetworkManager and TelepathyTransport.",
                "OK");
            return;
        }

        GameObject bootstrap = GameObject.Find(BootstrapName);
        if (bootstrap == null)
        {
            bootstrap = new GameObject(BootstrapName);
            Undo.RegisterCreatedObjectUndo(bootstrap, "Create Network Bootstrap");
        }

        Component transport = EnsureComponent(bootstrap, telepathyTransportType);
        Component manager = EnsureComponent(bootstrap, networkManagerType);
        SetMember(manager, "transport", transport);
        SetMember(manager, "autoCreatePlayer", false);

        if (networkManagerHudType != null)
            EnsureComponent(bootstrap, networkManagerHudType);

        Selection.activeGameObject = bootstrap;
        EditorSceneManager.MarkSceneDirty(bootstrap.scene);
        Debug.Log("[Phase0] NetworkBootstrap is ready. Start Host in one editor and Client in a ParrelSync clone.");
    }

    private static Component EnsureComponent(GameObject target, Type type)
    {
        Component component = target.GetComponent(type);
        if (component != null)
            return component;

        return Undo.AddComponent(target, type);
    }

    private static Type FindType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(type => type.Name == typeName);
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).ToArray();
        }
    }

    private static void SetMember(Component component, string memberName, object value)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = component.GetType();

        FieldInfo field = type.GetField(memberName, Flags);
        if (field != null && IsAssignable(field.FieldType, value))
        {
            field.SetValue(component, value);
            EditorUtility.SetDirty(component);
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, Flags);
        if (property != null && property.CanWrite && IsAssignable(property.PropertyType, value))
        {
            property.SetValue(component, value);
            EditorUtility.SetDirty(component);
        }
    }

    private static bool IsAssignable(Type targetType, object value)
    {
        if (value == null)
            return !targetType.IsValueType;

        return targetType.IsAssignableFrom(value.GetType());
    }
}
#endif
