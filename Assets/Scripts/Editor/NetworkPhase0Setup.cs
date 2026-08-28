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
        Type relayTransportType = FindType("RelayMirrorTransport");

        if (networkManagerType == null || relayTransportType == null)
        {
            EditorUtility.DisplayDialog(
                "Relay transport is not ready",
                "Mirror와 RelayMirrorTransport가 컴파일되었는지 확인하세요.",
                "OK");
            return;
        }

        GameObject bootstrap = GameObject.Find(BootstrapName);
        if (bootstrap == null)
        {
            bootstrap = new GameObject(BootstrapName);
            Undo.RegisterCreatedObjectUndo(bootstrap, "Create Network Bootstrap");
        }

        Component transport = EnsureComponent(bootstrap, relayTransportType);
        Component manager = EnsureComponent(bootstrap, networkManagerType);
        SetMember(manager, "transport", transport);
        SetMember(manager, "autoCreatePlayer", false);

        Selection.activeGameObject = bootstrap;
        EditorSceneManager.MarkSceneDirty(bootstrap.scene);
        Debug.Log("[Phase0] Unity Relay용 NetworkBootstrap이 준비되었습니다. MainTitle에서 방을 생성하거나 참가하세요.");
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
